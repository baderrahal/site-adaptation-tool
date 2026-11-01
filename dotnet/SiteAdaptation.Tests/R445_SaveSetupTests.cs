using System;
using System.Collections.Generic;
using System.IO;
using SiteAdaptation.Common.Flow;
using SiteAdaptation.Common.Settings;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R445, Q280 by Bader on 1 Nov 2026. Save setup writes the Settings entity of R367 at once, in a transaction of its own that rolls
    // back on any failure, through SettingsStore and the entity the buttons write
    public class R445_SaveSetupTests
    {
        private const string Hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        // The entity as Revit holds it: a write inside a transaction stays only when it commits
        private sealed class Model : IModel, ISettingsEntity
        {
            public string Saved { get; set; }
            public string Pending { get; private set; }
            public bool Open { get; private set; }
            public List<string> Names { get; } = new List<string>();
            public int RolledBack { get; private set; }
            public int Disposed { get; private set; }
            public bool CommitFails { get; set; }
            public bool WriteThrows { get; set; }

            public string Read()
            {
                return Open && Pending != null ? Pending : Saved;
            }

            public void Write(string json)
            {
                if (!Open)
                {
                    throw new InvalidOperationException("no transaction");
                }
                if (WriteThrows)
                {
                    throw new InvalidOperationException("the project information is not editable");
                }
                Pending = json;
            }

            public IModelTransaction Start(string name)
            {
                Names.Add(name);
                Open = true;
                Pending = null;
                return new Transaction(this);
            }

            private sealed class Transaction : IModelTransaction
            {
                private readonly Model model;
                private readonly List<string> errors = new List<string>();

                public Transaction(Model model)
                {
                    this.model = model;
                }

                public IReadOnlyList<string> Warnings
                {
                    get { return new string[0]; }
                }

                public IReadOnlyList<string> Errors
                {
                    get { return errors; }
                }

                public bool Commit()
                {
                    if (model.CommitFails)
                    {
                        errors.Add("Revit refused the change");
                        model.Open = false;
                        model.Pending = null;
                        return false;
                    }
                    if (model.Pending != null)
                    {
                        model.Saved = model.Pending;
                    }
                    model.Open = false;
                    model.Pending = null;
                    return true;
                }

                public void RollBack()
                {
                    if (model.Open)
                    {
                        model.RolledBack++;
                        model.Open = false;
                        model.Pending = null;
                    }
                }

                public void Dispose()
                {
                    model.Disposed++;
                }
            }
        }

        private static ProjectSettings Setup()
        {
            var settings = new ProjectSettings { ConfigFile = @"C:\work\project.json", DryRunFirst = true };
            settings.Inputs[InputNames.Baseplan] = new SettingsInput { Path = @"C:\inputs\site.dwg", Size = 10, Sha256 = Hash };
            settings.LastRuns["1"] = new ButtonRun { Time = "2026-11-01T10:00:00Z", Mode = RunModes.Direct, Scope = new SettingsScope { Kind = ScopeKinds.Project } };
            return settings;
        }

        [Fact]
        public void R445_save_setup_writes_the_entity_at_once_in_one_transaction_and_keeps_the_last_runs()
        {
            var model = new Model { Saved = SettingsJson.Write(new ProjectSettings()) };

            SetupWriteResult result = SetupWrite.Write(Setup(), model, model);

            Assert.Equal(SetupWriteOutcome.Written, result.Outcome);
            Assert.Equal(new[] { SetupWrite.TransactionName }, model.Names);
            Assert.Equal(0, model.RolledBack);
            Assert.Equal(1, model.Disposed);
            ProjectSettings kept = new SettingsStore(model).Load();
            Assert.Equal(@"C:\work\project.json", kept.ConfigFile);
            Assert.Equal(@"C:\inputs\site.dwg", kept.Inputs[InputNames.Baseplan].Path);
            Assert.Equal("2026-11-01T10:00:00Z", kept.LastRuns["1"].Time);
            Assert.Equal(SettingsJson.Version, kept.FormatVersion);
            Assert.Equal(model.Saved, SettingsJson.Write(result.Settings));
        }

        [Fact]
        public void R445_a_commit_revit_refuses_rolls_back_and_the_model_keeps_the_settings_it_had()
        {
            string before = SettingsJson.Write(new ProjectSettings());
            var model = new Model { Saved = before, CommitFails = true };

            SetupWriteResult result = SetupWrite.Write(Setup(), model, model);

            Assert.Equal(SetupWriteOutcome.Failed, result.Outcome);
            Assert.Equal("Revit refused the change", result.Reason);
            Assert.Null(result.Settings);
            Assert.Equal(before, model.Saved);
            Assert.Equal(1, model.Disposed);
        }

        [Fact]
        public void R445_a_write_that_throws_rolls_back_before_the_error_goes_on()
        {
            string before = SettingsJson.Write(new ProjectSettings());
            var model = new Model { Saved = before, WriteThrows = true };

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => SetupWrite.Write(Setup(), model, model));

            Assert.Contains("not editable", error.Message);
            Assert.Equal(1, model.RolledBack);
            Assert.Equal(1, model.Disposed);
            Assert.False(model.Open);
            Assert.Equal(before, model.Saved);
        }

        [Fact]
        public void R445_settings_the_reader_refuses_never_reach_the_model()
        {
            ProjectSettings bad = Setup();
            bad.Inputs[InputNames.Baseplan].Sha256 = "not a hash";
            var model = new Model();

            Assert.Throws<InvalidDataException>(() => SetupWrite.Write(bad, model, model));

            Assert.Equal(1, model.RolledBack);
            Assert.Null(model.Saved);
        }

        [Fact]
        public void R445_with_no_model_open_the_setup_is_kept_for_the_next_run_and_nothing_is_written()
        {
            var entity = new Model();

            SetupWriteResult kept = SetupWrite.Write(Setup(), null, null);
            SetupWriteResult noEntity = SetupWrite.Write(Setup(), null, entity);

            Assert.Equal(SetupWriteOutcome.Kept, kept.Outcome);
            Assert.Equal(SetupWriteOutcome.Kept, noEntity.Outcome);
            Assert.Empty(entity.Names);
            Assert.Null(entity.Saved);
        }

        [Fact]
        public void R445_a_commit_with_no_reason_from_revit_still_names_why()
        {
            var model = new Model { CommitFails = true };

            SetupWriteResult result = SetupWrite.Write(Setup(), new SilentCommit(model), model);

            Assert.Equal(SetupWriteOutcome.Failed, result.Outcome);
            Assert.Equal(SetupWrite.NotCommitted, result.Reason);
        }

        // A transaction that ends uncommitted and gives no error text
        private sealed class SilentCommit : IModel
        {
            private readonly Model model;

            public SilentCommit(Model model)
            {
                this.model = model;
            }

            public IModelTransaction Start(string name)
            {
                return new Quiet(model.Start(name));
            }

            private sealed class Quiet : IModelTransaction
            {
                private readonly IModelTransaction inner;

                public Quiet(IModelTransaction inner)
                {
                    this.inner = inner;
                }

                public IReadOnlyList<string> Warnings
                {
                    get { return new string[0]; }
                }

                public IReadOnlyList<string> Errors
                {
                    get { return new string[0]; }
                }

                public bool Commit()
                {
                    return inner.Commit();
                }

                public void RollBack()
                {
                    inner.RollBack();
                }

                public void Dispose()
                {
                    inner.Dispose();
                }
            }
        }

        [Fact]
        public void R445_the_store_writes_a_setup_only_inside_a_transaction()
        {
            var model = new Model();

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => new SettingsStore(model).Save(Setup(), null));

            Assert.Contains("R367", error.Message);
            Assert.Null(model.Saved);
        }
    }
}
