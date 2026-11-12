#if NETFRAMEWORK
using System.Linq;
using SiteAdaptation.AddIn.HowItWorks;
using SiteAdaptation.AddIn.Pane;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R764, F17 of run 1 of FAST-3. How it works teaches the 3 cards of the pane, not the nine steps of version 1
    public sealed class R764_HowItWorksCardsTests
    {
        [Fact]
        public void R764_each_card_heads_its_first_step_with_its_title_and_main_button()
        {
            var model = new HowItWorksViewModel();
            var heads = model.Rows.Where(row => row.OpensCard).ToList();
            Assert.Equal(new[] { 1, 4, 9 }, heads.Select(row => row.Number));
            Assert.Equal("Card 1, Project data: Choose project folder", heads[0].Card);
            Assert.Equal("Card 2, Build site: Build", heads[1].Card);
            Assert.Equal("Card 3, Review and deliver: Report", heads[2].Card);
            // Every other step sits under the card before it with no head of its own
            Assert.All(model.Rows.Where(row => !new[] { 1, 4, 9 }.Contains(row.Number)), row => Assert.Equal(string.Empty, row.Card));
            Assert.Equal(string.Empty, HowItWorksViewModel.CardOf(0));
            Assert.Equal(string.Empty, HowItWorksViewModel.CardOf(10));
        }

        [Fact]
        public void R764_the_line_names_the_3_cards_in_order_and_no_longer_the_nine_steps()
        {
            string line = HowItWorksText.Line;
            Assert.DoesNotContain("nine steps", line);
            int one = line.IndexOf(StageText.Stage1, System.StringComparison.Ordinal);
            int two = line.IndexOf(StageText.Stage2, System.StringComparison.Ordinal);
            int three = line.IndexOf(StageText.Stage3, System.StringComparison.Ordinal);
            Assert.True(one > 0 && one < two && two < three, line);
            // R845. Advanced keeps no button per step, so the line no longer sends a person there
            Assert.DoesNotContain(StageText.Advanced, line);
        }

        [Fact]
        public void R764_the_window_draws_the_head_of_each_card()
        {
            Fast7Sta.Run(() =>
            {
                var body = SiteAdaptation.UiShots.Shots.Body(SiteAdaptation.UiShots.Shots.LaidOutHowItWorks());
                var texts = Fast7Sta.Visuals(body).OfType<System.Windows.Controls.TextBlock>().Where(text => text.ActualHeight > 0).Select(text => text.Text).ToList();
                foreach (string head in new[] { 1, 4, 9 }.Select(HowItWorksViewModel.CardOf))
                {
                    Assert.Contains(head, texts);
                }
            });
        }
    }
}
#endif
