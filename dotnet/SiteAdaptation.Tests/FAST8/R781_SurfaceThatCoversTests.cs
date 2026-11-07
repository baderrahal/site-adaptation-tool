using System;
using System.Collections.Generic;
using System.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.Floors;
using SiteAdaptation.Common.Flow;
using Xunit;

namespace SiteAdaptation.Tests.FAST8
{
    // R781, R782, F27 of the short check of 7 Nov. The toposolid takes, of the road surfaces the build read, the one the ramp road edges lie on,
    // and a warning of known words on floors the list does not pair is listed and shows no box. All made up in code
    public sealed class R781_SurfaceThatCoversTests
    {
        private static double Road(double x)
        {
            return 10 + 0.001 * (x - 1000);
        }

        // Ramps at x 1000 to 1100, y 2000, inside the made up grid of R570 that spans x 900 to 1400 and y 1900 to 2200
        private static List<RampEdge> Edges()
        {
            return Enumerable.Range(0, 4).Select(index => new RampEdge { Plot = "P" + index, Start = new[] { 1000.0 + 20 * index, 2000, Road(1000 + 20 * index) }, End = new[] { 1010.0 + 20 * index, 2000, Road(1010 + 20 * index) } }).ToList();
        }

        private static RoadSurfaceRead Read(string name, LandXmlSurface surface)
        {
            return new RoadSurfaceRead { Settings = R570_RoadTopographyTests.Settings().For(name), Surface = surface };
        }

        // A surface of another part of the site, none of the ramps on it
        private static LandXmlSurface Elsewhere()
        {
            return R570_RoadTopographyTests.Tin(5000, 5200, 7000, 7200);
        }

        [Fact]
        public void R781_the_first_surface_that_misses_the_ramps_gives_way_to_the_one_they_lie_on_and_the_line_says_which()
        {
            RoadSurfaceRead first = Read("Top Surface-02", Elsewhere());
            int asked = 0;
            RoadSurfaceRead covering = Read("Top Surface-07", R570_RoadTopographyTests.Tin());
            first.Others = new Func<RoadSurfaceRead>[] { () => { asked++; return Read("Top Surface-05", Elsewhere()); }, () => { asked++; return covering; } };
            RoadSurfaceRead chosen = RoadTopography.Covering(first, Edges());
            Assert.Same(covering, chosen);
            Assert.Equal(2, asked);
            Assert.Equal("Top Surface-07", chosen.Settings.Surface);
            Assert.Equal("Top Surface-02", first.Settings.Surface);
            Assert.Equal(RoadTopography.Line + "of 3 road surfaces the build read, Top Surface-07 holds the most ramp road edge points, 12 of 12, so the toposolid takes it, Top Surface-02 of floors.roadTopography.surface holds 0", chosen.Why);
            ToposolidPlan plan = RoadTopography.Plan(chosen, Edges().SelectMany(edge => new[] { edge.Start, edge.End, new[] { edge.Start[0], edge.Start[1] + 20, 0.0 } }), Edges());
            Assert.Null(plan.Refused);
        }

        [Fact]
        public void R781_the_named_surface_that_holds_every_point_is_kept_and_no_other_is_read()
        {
            RoadSurfaceRead first = Read("Top Surface-02", R570_RoadTopographyTests.Tin());
            first.Others = new Func<RoadSurfaceRead>[] { () => throw new InvalidOperationException("read though the first holds every point") };
            RoadSurfaceRead chosen = RoadTopography.Covering(first, Edges());
            Assert.Same(first, chosen);
            Assert.Null(chosen.Why);
        }

        [Fact]
        public void R781_a_tie_keeps_the_earlier_surface_and_with_no_other_read_the_named_one_stays_and_refuses_as_before()
        {
            RoadSurfaceRead first = Read("Top Surface-02", Elsewhere());
            first.Others = new Func<RoadSurfaceRead>[] { () => Read("Top Surface-05", Elsewhere()) };
            Assert.Same(first, RoadTopography.Covering(first, Edges()));
            RoadSurfaceRead alone = Read("Top Surface-02", Elsewhere());
            RoadSurfaceRead kept = RoadTopography.Covering(alone, Edges());
            Assert.Same(alone, kept);
            Assert.StartsWith("the surface Top Surface-02 does not cover the scope, 12 of 12", RoadTopography.Plan(kept, Edges().SelectMany(edge => new[] { edge.Start, edge.End, new[] { edge.Start[0], edge.Start[1] + 20, 0.0 } }), Edges()).Refused);
        }

        [Fact]
        public void R781_a_surface_with_more_points_wins_over_one_with_some()
        {
            // Half the grid, x 900 to 1040, holds the first two ramps and the start of the third
            RoadSurfaceRead first = Read("Half", R570_RoadTopographyTests.Tin(900, 1050, 1900, 2200));
            RoadSurfaceRead whole = Read("Whole", R570_RoadTopographyTests.Tin());
            first.Others = new Func<RoadSurfaceRead>[] { () => whole };
            RoadSurfaceRead chosen = RoadTopography.Covering(first, Edges());
            Assert.Same(whole, chosen);
            Assert.Contains("Whole holds the most ramp road edge points, 12 of 12", chosen.Why);
        }

        private static KnownWarningList Floors()
        {
            var types = new Dictionary<string, IReadOnlyList<string>> { ["generic"] = new[] { "Generic 150mm" }, ["ramp"] = new[] { "Ramps 2%" } };
            return new KnownWarningList(new[] { new KnownWarning { Text = "Highlighted floors overlap.", SamePlot = true, RolePairs = new[] { new[] { "generic", "ramp" } } } }, "Plot", types);
        }

        [Fact]
        public void R782_known_words_on_floors_of_two_plots_are_taken_and_listed_with_each_floor()
        {
            KnownWarningList list = Floors();
            var elements = new List<WarningElement> { new WarningElement { Id = "a", Plot = "VD4-1", TypeName = "Generic 150mm" }, new WarningElement { Id = "b", Plot = "VD4-2", TypeName = "Generic 150mm" } };
            AcceptedWarning accepted = list.Decide("Highlighted floors overlap.", elements);
            Assert.Null(accepted);
            Assert.True(KnownWordsWarning.TakeAndList(list, "Highlighted floors overlap.", accepted));
            Assert.Equal("Highlighted floors overlap." + KnownWordsWarning.Taken + "plot VD4-1 type Generic 150mm id a, plot VD4-2 type Generic 150mm id b (R782)", KnownWordsWarning.Line(" Highlighted floors overlap. ", elements));
        }

        [Fact]
        public void R782_a_pair_the_list_accepts_other_words_and_no_list_are_not_taken_by_the_line()
        {
            KnownWarningList list = Floors();
            var pair = new List<WarningElement> { new WarningElement { Id = "a", Plot = "VD4-1", TypeName = "Generic 150mm" }, new WarningElement { Id = "b", Plot = "VD4-1", TypeName = "Ramps 2%" } };
            AcceptedWarning accepted = list.Decide("Highlighted floors overlap.", pair);
            Assert.NotNull(accepted);
            Assert.False(KnownWordsWarning.TakeAndList(list, "Highlighted floors overlap.", accepted));
            Assert.False(KnownWordsWarning.TakeAndList(list, "Highlighted walls overlap.", null));
            Assert.False(KnownWordsWarning.TakeAndList(null, "Highlighted floors overlap.", null));
            Assert.Equal("Highlighted floors overlap." + KnownWordsWarning.Taken + "no element named (R782)", KnownWordsWarning.Line("Highlighted floors overlap.", null));
        }
    }
}
