using System;
using System.Collections.Generic;
using System.Linq;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R355. The memory the road lines hold, measured by GC.GetTotalMemory of the whole process, so the test runs alone: in parallel with the
    // other tests a run of the third review of PR 55 read 4,740,504 bytes on net48, seen in the session and not saved, the 4 MiB bound broken by the others (round_addin logs p4efix3_b6_flaky.txt)
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class AloneCollection
    {
        public const string Name = "alone";
    }

    [Collection(AloneCollection.Name)]
    public sealed class RoadLinesMemoryTests
    {
        // B6 of the review of PR 55. RoadLines filled every 32 m cell of the box of each segment, so the vertex at 36000, 36000 here held
        // 130,354,888 bytes on net8.0 and 140,498,368 on net48. A stray vertex now costs no more than any segment, and the distance stays the least
        // over every segment, as STRtree.query_nearest and GEOS give it
        [Fact]
        public void R355_a_stray_curb_vertex_far_off_builds_the_road_lines_in_bounded_memory_with_the_distance_of_every_segment()
        {
            var near = new List<double[][]>
            {
                new[] { new[] { -50.0, -3.0 }, new[] { 100.0, -3.0 } }, new[] { new[] { -3.0, -10.0 }, new[] { -3.0, 40.0 }, new[] { 20.0, 60.0 } },
            };
            // A curb of 200 segments, so the boxes are halved many times
            near.Add(Enumerable.Range(0, 201).Select(index => new[] { index * 3.0, 80.0 + 5.0 * Math.Sin(index / 7.0) }).ToArray());
            foreach (double[] stray in new[] { new[] { 36000.0, 36000.0 }, new[] { -2.4e6, 5e5 }, new[] { 1e12, -1e12 } })
            {
                var lines = near.Concat(new[] { new[] { new[] { 10.0, 10.0 }, stray } }).ToList();
                long before = GC.GetTotalMemory(true);
                var roads = new RoadLines(lines.Select(line => (IReadOnlyList<double[]>)line));
                long held = GC.GetTotalMemory(true) - before;
                Assert.True(held < 4 * 1024 * 1024, "the road lines of a vertex at " + stray[0] + ", " + stray[1] + " hold " + held + " bytes");
                // The tree holds each segment once in a leaf, its boxes under twice the segments whatever the vertex
                Assert.True(roads.Boxes < 2 * roads.Count, "the road lines of a vertex at " + stray[0] + ", " + stray[1] + " hold " + roads.Boxes + " boxes");
                var segments = lines.SelectMany(line => line.Zip(line.Skip(1), (a, b) => new[] { a, b })).ToList();
                foreach (double[] point in new[] { new[] { 0.0, 0.0 }, new[] { 30.0, 5.0 }, new[] { 18000.0, 18000.0 }, new[] { stray[0] + 5.0, stray[1] - 7.0 }, new[] { -1e5, 3e5 }, new[] { 5e11, -5e11 } })
                {
                    double least = segments.Min(segment => PyMath.SegmentDistance(point, segment[0], segment[1]));
                    Assert.Equal(least, roads.Distance(point[0], point[1]));
                }
                GC.KeepAlive(roads);
            }
        }

    }
}
