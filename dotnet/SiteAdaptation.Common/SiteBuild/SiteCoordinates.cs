using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SiteAdaptation.Common.SetUp;

namespace SiteAdaptation.Common.SiteBuild
{
    // R642. The coordinates of an empty model, taken from the CAD of the project: the centre of the box of every plot outline rounded to coordinates.originRoundM,
    // the rule of R455. The position file is only a check, it never sets a coordinate. Both are given with the difference in millimetres, so a person sees how far
    // the file lies from the drawing and decides, the build decides nothing from it
    internal static class SiteCoordinates
    {
        public static JObject Build(JObject config, IReadOnlyList<PlanPlot> plots, string positionPath, List<PlanIssue> issues)
        {
            CoordinateSettings settings;
            try
            {
                settings = CoordinateSettings.From(config);
            }
            catch (InvalidDataException refused)
            {
                issues.Add(new PlanIssue { Line = PlanTexts.CoordinatesNoSettings(refused.Message) });
                return Missing(PlanTexts.CoordinatesNoSettings(refused.Message));
            }
            List<double[]> vertices = plots.Where(plot => plot.In.Outline != null).SelectMany(plot => plot.In.Outline).ToList();
            if (vertices.Count == 0)
            {
                issues.Add(new PlanIssue { Line = PlanTexts.CoordinatesNoOutline });
                return Missing(PlanTexts.CoordinatesNoOutline);
            }
            double[] box = Rings.Box(vertices);
            double east = Round((box[0] + box[1]) / 2, settings.OriginRoundM);
            double north = Round((box[2] + box[3]) / 2, settings.OriginRoundM);
            var json = new JObject
            {
                ["source"] = CoordinateSettings.FromCadSource,
                ["eastM"] = east,
                ["northM"] = north,
                ["elevationM"] = settings.OriginElevationM,
                ["angleDeg"] = settings.AngleToTrueNorthDeg,
                ["originRoundM"] = settings.OriginRoundM,
                ["box"] = new JArray(box),
                ["positionFile"] = JValue.CreateNull(),
                ["differenceMm"] = JValue.CreateNull(),
                ["warning"] = JValue.CreateNull(),
            };
            // Held to the range of a UTM zone as button 1 holds it, a local drawing is said and still used as it is
            var cad = plots.Where(plot => plot.In.Outline != null).Select(plot => new CadPlot { Plot = plot.Number, Vertices = plot.In.Outline }).ToList();
            string outside = CoordinateSystems.OutsideZone(cad, settings, settings.ExpectedEpsg);
            if (outside != null)
            {
                json["warning"] = outside;
                issues.Add(new PlanIssue { Line = outside });
            }
            if (positionPath != null)
            {
                Check(json, positionPath, east, north, settings, issues);
            }
            return json;
        }

        private static JObject Missing(string why)
        {
            return new JObject
            {
                ["source"] = "missing", ["eastM"] = JValue.CreateNull(), ["northM"] = JValue.CreateNull(), ["positionFile"] = JValue.CreateNull(), ["differenceMm"] = JValue.CreateNull(), ["why"] = why,
            };
        }

        // The position file holds eastM, northM, elevationM and angleDeg. Each difference is CAD less file, in millimetres for a length and in degrees for the angle
        private static void Check(JObject json, string path, double east, double north, CoordinateSettings settings, List<PlanIssue> issues)
        {
            JObject file;
            try
            {
                file = JObject.Parse(WorkFiles.ReadText(path));
            }
            catch (Exception failure) when (failure is JsonException || failure is IOException)
            {
                issues.Add(new PlanIssue { Line = PlanTexts.PositionFileUnreadable(failure.Message) });
                json["positionFile"] = new JObject { ["error"] = failure.Message };
                return;
            }
            double? fileEast = Number(file["eastM"]);
            double? fileNorth = Number(file["northM"]);
            if (!fileEast.HasValue || !fileNorth.HasValue)
            {
                issues.Add(new PlanIssue { Line = PlanTexts.PositionFileNoPoint });
                json["positionFile"] = new JObject { ["error"] = PlanTexts.PositionFileNoPoint };
                return;
            }
            double? fileElevation = Number(file["elevationM"]);
            double? fileAngle = Number(file["angleDeg"]);
            double eastMm = (east - fileEast.Value) * 1000.0;
            double northMm = (north - fileNorth.Value) * 1000.0;
            json["positionFile"] = new JObject { ["eastM"] = fileEast.Value, ["northM"] = fileNorth.Value, ["elevationM"] = Value(fileElevation), ["angleDeg"] = Value(fileAngle) };
            json["differenceMm"] = Math.Round(Math.Sqrt(eastMm * eastMm + northMm * northMm), 1);
            json["differences"] = new JObject
            {
                ["eastMm"] = Math.Round(eastMm, 1),
                ["northMm"] = Math.Round(northMm, 1),
                ["elevationMm"] = fileElevation.HasValue ? new JValue(Math.Round((settings.OriginElevationM - fileElevation.Value) * 1000.0, 1)) : JValue.CreateNull(),
                ["angleDeg"] = fileAngle.HasValue ? new JValue(Math.Round(settings.AngleToTrueNorthDeg - fileAngle.Value, 6)) : JValue.CreateNull(),
            };
        }

        private static JToken Value(double? value)
        {
            return value.HasValue ? new JValue(value.Value) : JValue.CreateNull();
        }

        private static double? Number(JToken token)
        {
            return token != null && (token.Type == JTokenType.Float || token.Type == JTokenType.Integer) ? (double?)token : null;
        }

        private static double Round(double value, double step)
        {
            return Math.Round(value / step, MidpointRounding.AwayFromZero) * step;
        }
    }
}
