using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using SiteAdaptation.AddIn.NewProject;
using SiteAdaptation.AddIn.Pane;
using SiteAdaptation.Common.Settings;
using SiteAdaptation.UiShots;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R445, Q280. The foot of New project says in one line when Save setup has no model to write to, and stays as the board draws it
    // while a model is open
    public class R445_SaveSetupWindowTests
    {
        [Fact]
        public void R445_the_no_model_line_shows_only_when_save_setup_has_no_model_to_write_to()
        {
            OnSta(() =>
            {
                NewProjectWindow open = Shots.LaidOutNewProject();
                Assert.False(((NewProjectViewModel)open.DataContext).NoModelShown);
                Assert.Equal(Visibility.Collapsed, Line(open).Visibility);

                var model = new NewProjectViewModel(new ProjectSettings(), "Site", new SavedStart[0], new DrawingFactsResult(), (path, folder) => false, null, path => null,
                    null, false);
                var none = new NewProjectWindow(model);
                Assert.True(model.NoModelShown);
                Assert.Equal(Visibility.Visible, Line(none).Visibility);
                Assert.Equal("No model is open to write to, so the setup is saved with the next button run.", Line(none).Text);
                none.Close();
            });
        }

        [Fact]
        public void R445_save_setup_still_gives_the_settings_and_closes_the_window_with_no_model()
        {
            var model = new NewProjectViewModel(new ProjectSettings(), "Site", new SavedStart[0], new DrawingFactsResult(), (path, folder) => false, null, path => null,
                null, false);
            bool finished = false;
            model.Finished += () => finished = true;

            model.Save();

            Assert.True(finished);
            Assert.NotNull(model.Result);
        }

        // Breaker of FIX-1NOV-C: an undo takes the entity back, the session reads it again so the pane shows what the model holds
        [Fact]
        public void R445_an_undo_makes_the_session_read_the_settings_the_model_holds_again()
        {
            var sessions = new PaneSessions();
            object document = new object();
            DocumentSession session = sessions.Opened(document, @"C:\work\site.rvt", "site");
            var entity = new Entity { Text = SettingsJson.Write(new ProjectSettings { ConfigFile = @"C:\work\fresh.json" }) };
            session.LoadFrom(entity);
            Assert.Equal(@"C:\work\fresh.json", session.Settings.ConfigFile);

            entity.Text = SettingsJson.Write(new ProjectSettings { ConfigFile = @"C:\work\old.json" });
            DocumentSession undone = sessions.Undone(document, entity);

            Assert.Same(session, undone);
            Assert.Equal(@"C:\work\old.json", session.Settings.ConfigFile);
            Assert.Null(sessions.Undone(new object(), entity));
        }

        private sealed class Entity : ISettingsEntity
        {
            public string Text { get; set; }

            public string Read()
            {
                return Text;
            }

            public void Write(string json)
            {
                Text = json;
            }
        }

        private static TextBlock Line(Window window)
        {
            return Logical((DependencyObject)window.Content).OfType<TextBlock>().Single(text => text.Text == NewProjectText.NoModel);
        }

        private static IEnumerable<DependencyObject> Logical(DependencyObject root)
        {
            yield return root;
            foreach (DependencyObject child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>().SelectMany(Logical))
            {
                yield return child;
            }
        }

        private static void OnSta(Action action)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception caught)
                {
                    failure = caught;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null)
            {
                throw new Exception(failure.Message, failure);
            }
        }
    }
}
