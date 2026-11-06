using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common;
using SiteAdaptation.Common.SiteBuild;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R645. The outline of each plot comes from the rings stage 1 chose, made up reads only
    public sealed class R645_PlotOutlinesTests
    {
        private static DataCheckSettings Settings()
        {
            var config = new JObject
            {
                [DataCheckSettings.Block] = new JObject
                {
                    ["scan"] = new JObject
                    {
                        ["labelPattern"] = @"^(?:(.*?)\s+)?(\S*\d\S*)$", ["tokenSplit"] = "[^A-Za-z0-9]+", ["plotNumberShifts"] = new JArray(0), ["pairMinSegments"] = 100,
                        ["pairThreads"] = 1, ["pairTimeCapS"] = 60, ["pdfJoinGapPt"] = 0.3, ["pdfLineTolerancePt"] = 0.05, ["pdfPairDyPt"] = new JArray(1.3, 1.8), ["pdfPairDxPt"] = 1.5,
                    },
                    ["groupGapsM"] = new JArray(100.0, 1.0), ["wordReachM"] = 30, ["wordShare"] = 0.9, ["wordMinGroups"] = 3, ["standInMaxArea"] = 4, ["roadReachM"] = 20, ["roadMinSegments"] = 2, ["roadOutsideShare"] = 0.5,
                    ["drawingPeak"] = new JObject { ["dwg"] = 252, ["dxf"] = 106 },
                    ["strongShare"] = 0.9, ["usableShare"] = 0.5, ["prototypeFilePattern"] = "(?:^|-)ARC-([A-Za-z0-9]+)(?:-[A-Za-z0-9]{2})?-MOD",
                    ["codePattern"] = @"^[A-Za-z]+\d+[A-Za-z0-9.]*(?:-[A-Za-z0-9]+)*(?:\s*\([^()]*\))?$", ["codeTableMaxPairs"] = 100, ["handGuessShare"] = 0.6, ["areaNearPlots"] = 8, ["areaAgreement"] = 0.7, ["skipFolders"] = new JArray(), ["groupNamePattern"] = @"^(?:[A-Z0-9][A-Za-z0-9-]{0,3}|\S*\d\S*)$",
                },
            };
            return DataCheckSettings.From(config);
        }

        private static JArray Pt(double x, double y)
        {
            return new JArray(x, y);
        }

        private static JObject Face(double x, double y, double w, double d)
        {
            return new JObject { ["ring"] = new JArray(Pt(x, y), Pt(x + w, y), Pt(x + w, y + d), Pt(x, y + d)), ["centroid"] = Pt(x + w / 2, y + d / 2), ["area"] = w * d };
        }

        // Five plots of 12 by 20 units. With a tie the same faces come twice, once with a face of block lines added that carries no label
        private static DataCheckInput Input(string units, bool tie)
        {
            var origins = new[] { Tuple.Create(0.0, 0.0), Tuple.Create(12.0, 0.0), Tuple.Create(0.0, 30.0), Tuple.Create(12.0, 30.0), Tuple.Create(300.0, 0.0) };
            var faces = new JArray();
            var texts = new JArray();
            var inserts = new JArray();
            for (int index = 0; index < origins.Length; index++)
            {
                faces.Add(Face(origins[index].Item1, origins[index].Item2, 12, 20));
                texts.Add(new JObject { ["layer"] = "N", ["text"] = (101 + index).ToString(), ["x"] = origins[index].Item1 + 3, ["y"] = origins[index].Item2 + 2 });
                inserts.Add(new JObject { ["layer"] = "H", ["block"] = "VA1", ["x"] = origins[index].Item1 + 6, ["y"] = origins[index].Item2 + 10, ["mirrored"] = false });
            }
            texts.Add(new JObject { ["layer"] = "T", ["text"] = "Phase A", ["x"] = 0.0, ["y"] = -10.0 });
            texts.Add(new JObject { ["layer"] = "T", ["text"] = "Phase B", ["x"] = 300.0, ["y"] = -10.0 });
            var candidates = new JArray(new JObject { ["layers"] = new JArray("P"), ["faces"] = faces });
            if (tie)
            {
                JArray extra = new JArray(faces.Select(face => face.DeepClone()));
                extra.Add(Face(10, 10, 2, 2));
                candidates.Add(new JObject { ["layers"] = new JArray("0", "P"), ["faces"] = extra });
            }
            var drawing = new JObject
            {
                ["source"] = "site.dxf", ["units"] = units, ["layers"] = new JArray(), ["candidates"] = candidates, ["texts"] = texts, ["inserts"] = inserts,
                ["lines"] = new JObject { ["K"] = new JArray(new JArray(0.0, 20.0, 24.0, 20.0), new JArray(300.0, 20.0, 312.0, 20.0)) }, ["arcs"] = new JObject(), ["notRead"] = new JArray(), ["notTraced"] = new JArray(),
            };
            var input = new DataCheckInput();
            input.CadReads.Add(drawing);
            input.PrototypeNames.Add("ARC-VA1-MOD.rvt");
            return input;
        }

        [Fact]
        public void The_table_gets_the_ring_of_each_plot_in_metres()
        {
            DataCheckOutput output = DataCheckLogic.Run(Settings(), Input("Millimeters", false));
            Assert.Empty(PlotOutlines.Add(output));
            JObject plot = ((JArray)output.Plots["plots"]).OfType<JObject>().Single(row => (string)row["number"] == "101");
            var ring = (JArray)plot["outline"];
            Assert.True(ring.Count >= 4);
            Assert.Equal(0.012, ring.Max(point => (double)point[0]) - ring.Min(point => (double)point[0]), 6);
        }

        [Fact]
        public void After_a_tie_the_rings_are_the_ones_stage_1_chose_and_not_the_ones_of_a_scan_before_the_tie_is_broken()
        {
            DataCheckSettings settings = Settings();
            DataCheckOutput output = DataCheckLogic.Run(settings, Input("Meters", true));
            Assert.NotEmpty((JArray)output.Check["ties"]);
            Assert.Empty(PlotOutlines.Add(output));
            var plots = ((JArray)output.Plots["plots"]).OfType<JObject>().ToList();
            Assert.Equal(5, plots.Count(row => row["outline"] != null));
            Assert.Equal(output.Rings.Select(ring => ring.Key).OrderBy(key => key).ToArray(), plots.Select(row => (string)row["number"]).OrderBy(key => key).ToArray());
            // The old way scanned the reads again from the start, with the tie still in them
            var prototypes = new HashSet<string>(new[] { "VA1" }, StringComparer.Ordinal);
            List<KeyValuePair<string, List<double[]>>> before;
            string source;
            DataCheckInput fresh = Input("Meters", true);
            ScanLogic.Result(settings.Scan, new HashSet<string>(prototypes), prototypes, settings.StrongShare, new HashSet<string>(StringComparer.Ordinal), fresh.CadReads, fresh.Excels, fresh.Pdfs, new JArray(), out before, out source);
            Assert.NotEqual(before.Count, output.Rings.Count);
        }

        [Fact]
        public void An_unknown_unit_and_a_plot_with_no_ring_are_said_and_no_empty_outline_is_written()
        {
            var output = new DataCheckOutput { Plots = new JObject { ["plots"] = new JArray(new JObject { ["number"] = "1" }, new JObject { ["number"] = "2" }) } };
            Assert.Contains("unit", PlotOutlines.Add(output).Single());
            Assert.Null(output.Plots["plots"][0]["outline"]);
            output.MetersPerUnit = 1.0;
            output.Rings.Add(new KeyValuePair<string, List<double[]>>("1", new List<double[]> { new[] { 0.0, 0.0 }, new[] { 1.0, 0.0 }, new[] { 1.0, 1.0 } }));
            Assert.Contains("1 plots have no outline", PlotOutlines.Add(output).Single());
            Assert.NotNull(output.Plots["plots"][0]["outline"]);
            Assert.Null(output.Plots["plots"][1]["outline"]);
        }

        [Fact]
        public void A_table_with_no_plots_gets_no_note()
        {
            Assert.Empty(PlotOutlines.Add(new DataCheckOutput { Plots = new JObject { ["plots"] = new JArray() } }));
            Assert.Empty(PlotOutlines.Add(null));
        }
    }
}
