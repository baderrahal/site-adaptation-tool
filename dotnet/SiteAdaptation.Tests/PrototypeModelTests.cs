using System;
using System.Collections.Generic;
using System.IO;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    public class PrototypeModelTests
    {
        [Fact]
        public void R4_feet_convert_to_metres_and_radians_to_degrees()
        {
            Assert.Equal(0.305, JsonOut.Round3(UnitMath.FeetToMetres(1)));
            Assert.Equal(90, JsonOut.Round3(UnitMath.RadiansToDegrees(Math.PI / 2)));
        }

        [Fact]
        public void R4_prototypes_file_lists_every_required_field()
        {
            var file = new PrototypesFile
            {
                Tool = "RevitPrototypeReader",
                RevitVersion = "2024",
                LengthUnit = "m",
                AngleUnit = "deg",
                Documents = new List<DocumentEntry>
                {
                    new DocumentEntry
                    {
                        File = "villa.rvt",
                        Kind = "project",
                        BasePoint = new ProjectPointInfo { Position = new[] { 0.0, 0, 0 }, SharedPosition = new[] { 100.1234, 200.0, 5.0 } },
                        SurveyPoint = new ProjectPointInfo { Position = new[] { 1.0, 2, 0 }, SharedPosition = new[] { 0.0, 0, 0 } },
                        AngleToTrueNorthDeg = 12.34567,
                        Prototypes = new List<PrototypeRow>
                        {
                            new PrototypeRow
                            {
                                FamilyName = "Villa A",
                                TypeName = "Type 1",
                                Placement = "placed",
                                Parameters = new Dictionary<string, object> { { "Width", 12.5 } },
                                BoundingBox = new BoxInfo { Min = new[] { 0.0, 0, 0 }, Max = new[] { 12.5, 20, 9 } },
                                InsertionPoint = new[] { 6.25, 10, 0 },
                                RotationDeg = 90,
                                Level = "Level 0",
                            },
                        },
                    },
                },
            };
            string json = JsonOut.ToJson(file);
            foreach (string field in new[] { "familyName", "typeName", "parameters", "boundingBox", "insertionPoint", "rotationDeg", "level", "surveyPoint", "basePoint" })
            {
                Assert.Contains("\"" + field + "\"", json);
            }
            Assert.Contains("12.346", json);
        }

        [Fact]
        public void R4_key_parameters_fall_back_to_the_default_list()
        {
            string folder = Path.Combine(Path.GetTempPath(), "sat-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                Assert.Equal(KeyParameters.Defaults, KeyParameters.Load(folder));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Fact]
        public void R4_key_parameters_file_replaces_the_default_list()
        {
            string folder = Path.Combine(Path.GetTempPath(), "sat-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                File.WriteAllText(Path.Combine(folder, KeyParameters.FileName), "# keys\nFront Setback\n\n  Plot Number  \n");
                var names = KeyParameters.Load(folder);
                Assert.Equal(new[] { "Front Setback", "Plot Number" }, names);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }
    }
}
