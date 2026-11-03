using System;
using System.Collections.Generic;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common.Settings;
using SiteAdaptation.Common.Steps;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R499, Q382. An undo or a redo gives each step the run the entity holds after it, on net48 with the sessions of the pane and no Revit
    public sealed class R499_UndoTests
    {
        private const string Hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        private static string Holding(params int[] buttons)
        {
            var settings = new ProjectSettings();
            foreach (int button in buttons)
            {
                settings.LastRuns[button.ToString()] = new ButtonRun { Time = "2026-11-03T08:00:00.000Z", Scope = new SettingsScope(), InputHashes = new SortedDictionary<string, string> { { "Curbs", Hash } } };
            }
            return SettingsJson.Write(settings);
        }

        private static StepRun Session()
        {
            return new StepRun(new DateTime(2026, 11, 3, 9, 0, 0, DateTimeKind.Utc), "session", new SettingsScope(), 0, null);
        }

        [Fact]
        public void R499_an_undo_of_button_1_takes_its_done_state_back()
        {
            var sessions = new PaneSessions();
            object document = new object();
            DocumentSession session = sessions.Opened(document, @"C:\work\site.rvt", "site");
            session.Runs[1] = Session();
            Assert.NotNull(session.LastRun(1));

            sessions.Undone(document, new Entity { Json = Holding() });

            Assert.Empty(session.Runs);
            Assert.Null(session.LastRun(1));
        }

        [Fact]
        public void R499_an_undo_of_a_later_step_leaves_the_steps_the_entity_still_holds()
        {
            var sessions = new PaneSessions();
            object document = new object();
            DocumentSession session = sessions.Opened(document, @"C:\work\site.rvt", "site");
            session.Runs[1] = Session();
            session.Runs[2] = Session();
            session.Runs[5] = Session();

            sessions.Undone(document, new Entity { Json = Holding(1, 2) });

            Assert.NotNull(session.LastRun(1));
            Assert.NotNull(session.LastRun(2));
            Assert.Null(session.LastRun(5));
        }

        // The event comes for a redo too, and the entity then holds the step again
        [Fact]
        public void R499_a_redo_gives_the_step_back()
        {
            var sessions = new PaneSessions();
            object document = new object();
            DocumentSession session = sessions.Opened(document, @"C:\work\site.rvt", "site");

            sessions.Undone(document, new Entity { Json = Holding() });
            Assert.Null(session.LastRun(5));

            sessions.Undone(document, new Entity { Json = Holding(5) });
            Assert.NotNull(session.LastRun(5));
        }

        private sealed class Entity : ISettingsEntity
        {
            public string Json { get; set; }

            public string Read()
            {
                return Json;
            }

            public void Write(string json)
            {
                Json = json;
            }
        }
    }
}
