using System;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R220. The rotation the reader writes, with the location rotation of Revit and without it
    public class PlacementRotationTests
    {
        [Theory]
        [InlineData(30.0)]
        [InlineData(-135.0)]
        [InlineData(179.5)]
        public void R220_with_no_location_rotation_the_turn_of_the_transform_x_axis_is_taken(double degrees)
        {
            double radians = degrees * Math.PI / 180.0;

            Assert.True(PlacementRotation.NeedsTransform(null));
            Assert.Equal(degrees, PlacementRotation.Degrees(null, Math.Cos(radians), Math.Sin(radians)), 9);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(90.0)]
        [InlineData(-45.0)]
        public void R220_a_location_rotation_from_revit_is_kept_and_the_transform_is_never_asked(double degrees)
        {
            double radians = degrees * Math.PI / 180.0;

            Assert.False(PlacementRotation.NeedsTransform(radians));
            // The axis given is a decoy at 77 degrees, the location rotation wins
            Assert.Equal(degrees, PlacementRotation.Degrees(radians, Math.Cos(1.344), Math.Sin(1.344)), 9);
        }
    }
}
