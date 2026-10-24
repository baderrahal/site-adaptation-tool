using System;
using System.IO;
using System.Linq;
using SiteAdaptation.C3DProbe;
using Xunit;

namespace SiteAdaptation.Tests
{
    // The plain parts of the Civil 3D probe. The Civil folder builds only where Civil 3D 2026 is installed, so these tests
    // say nothing about what Civil 3D returns
    public class C3DProbeTests
    {
        private static readonly string Work = Path.Combine(Path.GetTempPath(), "c3d_probe_test", "work");

        [Fact]
        public void R150_guard_accepts_a_copy_under_the_work_folder()
        {
            Assert.Null(WorkGuard.Refusal(Work, Path.Combine(Work, "Roads", "a.dwg")));
        }

        [Fact]
        public void R150_guard_refuses_a_drawing_outside_the_work_folder()
        {
            string source = Path.Combine(Path.GetTempPath(), "c3d_probe_test", "source", "Roads", "a.dwg");
            Assert.Contains("not under the working copies", WorkGuard.Refusal(Work, source));
        }

        [Fact]
        public void R150_guard_refuses_a_folder_that_only_starts_like_the_work_folder()
        {
            Assert.NotNull(WorkGuard.Refusal(Work, Work + "2" + Path.DirectorySeparatorChar + "a.dwg"));
        }

        [Fact]
        public void R150_guard_refuses_when_the_work_folder_is_not_set()
        {
            Assert.Contains(ProbeEnv.Work, WorkGuard.Refusal(null, Path.Combine(Work, "a.dwg")));
            Assert.NotNull(WorkGuard.Refusal(Work, ""));
        }

        [Fact]
        public void R151_point_table_reads_id_x_y_and_skips_the_header_and_bad_rows()
        {
            var points = PointTable.Read(new[] { "id,x,y", "2311-left,10.5,20.25", "", "bad,row", "2312-right,1e2,3" });
            Assert.Equal(2, points.Count);
            Assert.Equal("2311-left", points[0].Id);
            Assert.Equal(10.5, points[0].X);
            Assert.Equal(100.0, points[1].X);
        }

        [Fact]
        public void R151_level_row_keeps_four_decimals_and_no_comma_in_the_name()
        {
            Assert.Equal("p,Top Surface 01,170.1235", PointTable.Row("p", "Top Surface,01", 170.12345));
        }

        [Fact]
        public void R2_round_turns_negative_zero_into_zero()
        {
            Assert.Equal("0", Round.Text(-0.0001));
            Assert.Equal(1.235, Round.To(1.2345));
        }

        [Fact]
        public void R152_two_triangles_of_a_square_have_four_border_edges_and_one_ring()
        {
            var border = new TinBorder();
            border.Add(0, 0, 1, 0, 1, 1);
            border.Add(0, 0, 1, 1, 0, 1);
            Assert.Equal(2, border.Triangles);
            Assert.Equal(4, border.Vertices);
            Assert.Equal(4, border.BorderEdges().Count);
            var rings = border.Rings();
            Assert.Single(rings);
            Assert.Equal(5, rings[0].Count);
            Assert.Equal(rings[0][0], rings[0][4]);
        }

        [Fact]
        public void R152_a_frame_around_a_hole_has_an_outer_and_an_inner_ring()
        {
            // A 3 by 3 grid of unit cells with the middle cell left out, two triangles per cell
            var border = new TinBorder();
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    if (i == 1 && j == 1)
                    {
                        continue;
                    }
                    border.Add(i, j, i + 1, j, i + 1, j + 1);
                    border.Add(i, j, i + 1, j + 1, i, j + 1);
                }
            }
            Assert.Equal(16, border.Triangles);
            Assert.Equal(16, border.BorderEdges().Count);
            var rings = border.Rings().OrderByDescending(ring => ring.Count).ToList();
            Assert.Equal(2, rings.Count);
            Assert.Equal(13, rings[0].Count);
            Assert.Equal(5, rings[1].Count);
        }

        [Fact]
        public void R152_one_point_read_twice_a_tenth_of_a_millimetre_apart_is_one_vertex()
        {
            var border = new TinBorder();
            border.Add(0, 0, 1, 0, 1, 1);
            border.Add(0.00004, 0, 1, 1, 0, 1);
            Assert.Equal(4, border.Vertices);
            Assert.Equal(4, border.BorderEdges().Count);
        }

        [Fact]
        public void R152_a_sliver_with_two_corners_on_one_key_is_left_out()
        {
            var border = new TinBorder();
            border.Add(0, 0, 0.00001, 0, 1, 1);
            Assert.Equal(0, border.Triangles);
            Assert.Empty(border.BorderEdges());
        }
    }
}
