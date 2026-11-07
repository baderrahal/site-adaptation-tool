#if NETFRAMEWORK
using SiteAdaptation.AddIn.ReviewData;
using SiteAdaptation.Common.Review;
using SiteAdaptation.UiShots;
using Xunit;

namespace SiteAdaptation.Tests
{
    // RULE_F13, F13 of run 1 of FAST-3. A Close of 3 Review data with nothing changed counts as saved, so the Build goes on. Made up values only
    public sealed class F13_ReviewCloseTests
    {
        [Fact]
        public void F13_a_close_with_nothing_changed_saves_once_and_counts_as_saved()
        {
            int saves = 0;
            var session = new ReviewDataSession(Shots.SampleReviewView(true), new ReviewCallbacks { Save = checks => { saves++; return "made up review"; } });
            Assert.False(session.Model.NotSavedShown);
            session.Closed();
            Assert.True(session.Saved);
            Assert.Equal(1, saves);
            // Already saved: a second close writes nothing more
            session.Closed();
            Assert.Equal(1, saves);
        }

        [Fact]
        public void F13_a_close_over_marks_not_saved_stays_not_saved()
        {
            int saves = 0;
            var session = new ReviewDataSession(Shots.SampleReviewView(true), new ReviewCallbacks { Save = checks => { saves++; return "made up review"; } });
            // Broken on purpose: a mark the person chose Don't save over
            session.Model.Selected = session.Model.Rows[0];
            session.Mark(ReviewCheckValue.Wrong);
            Assert.True(session.Model.NotSavedShown);
            session.Closed();
            Assert.False(session.Saved);
            Assert.Equal(0, saves);
        }
    }
}
#endif
