using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R356, R121, R122, R332, R337. The link of a plot a build by keyRules places, as plan_from_cad.link_values gives it on the front the build
    // takes (R355) with the hand of the rule (R357), before an insert or rotation override of the list moves it (R360): the prototype file of
    // its code, the fit of keyRules.linkOrigin of the code, else of its keo_file, the anchor corner of the front, the start the link X axis
    // runs from or the end it runs to, the offset from it along the link X and Y axes, X the right hand of the front, the other way when
    // mirrored, Y into the plot, and the rotation of the X axis, written as the plan writes it (plan_rotation)
    public static class LinkFrames
    {
        public const string StepName = "linkFrame";

        // Python truthiness of a mirror flag a table gives, a flag being true or false
        private static bool Truthy(JToken flag)
        {
            switch (flag.Type)
            {
                case JTokenType.Boolean:
                    return (bool)flag;
                case JTokenType.Integer:
                case JTokenType.Float:
                    return BuildFace.Number(flag) != 0;
                case JTokenType.String:
                    return ((string)flag).Length > 0;
                case JTokenType.Array:
                case JTokenType.Object:
                    return flag.HasValues;
                default:
                    return false;
            }
        }

        // cad_links.link_axes: Y the inward normal, X the right hand, minus it when mirrored
        public static double[][] Axes(FrontFrame frame, bool mirrored)
        {
            return new[] { mirrored ? new[] { -frame.R[0], -frame.R[1] } : frame.R, frame.N };
        }

        // cad_links.anchor_corner: the front corner the link X axis starts from, start, or runs to, any other anchor
        public static double[] AnchorCorner(FrontFrame frame, bool mirrored, string anchor)
        {
            double[] start = mirrored ? frame.Right : frame.Left, end = mirrored ? frame.Left : frame.Right;
            return anchor == "start" ? start : end;
        }

        // cad_links.predicted_origin
        public static double[] PredictedOrigin(FrontFrame frame, bool mirrored, string anchor, double[] offset)
        {
            double[][] axes = Axes(frame, mirrored);
            double[] corner = AnchorCorner(frame, mirrored, anchor);
            return new[] { corner[0] + offset[0] * axes[0][0] + offset[1] * axes[1][0], corner[1] + offset[0] * axes[0][1] + offset[1] * axes[1][1] };
        }

        // cad_links.rotation_of: the angle of the link X axis in degrees, by Python %
        public static double RotationOf(FrontFrame frame, bool mirrored)
        {
            return PyMath.AngleOf(Axes(frame, mirrored)[0]);
        }

        // plan_from_past.plan_rotation: R385 at 3 decimals of the angle by Python %, one that rounds to 360 written as 0 (R80)
        public static double PlanRotation(double degrees)
        {
            double rounded = Rounding.Fixed(PyMath.Mod(degrees, 360.0), 3);
            return rounded >= 360 ? 0.0 : rounded;
        }

        private static JObject Lack(string key, string why)
        {
            return new JObject { ["key"] = key, ["why"] = why };
        }

        // plan_from_cad.link_values with the hand given: what the link lacks with why, in the order the plan lists it, the fit when the
        // link is placed, else null
        public static Tuple<JArray, JObject> Lacks(string number, string unit, BuildFace face, FrontFrame frame, JToken mirrored, JObject section,
            IReadOnlyDictionary<string, string> files, ClusterRole role, string fitUnit)
        {
            var lacks = new JArray();
            if (!files.ContainsKey(unit))
            {
                lacks.Add(Lack("prototypeFile", "no single prototype file in the folder carries the unit " + unit));
            }
            if (face == null)
            {
                lacks.Add(Lack("transform", "no baseplan face carries the label of the plot"));
                return Tuple.Create(lacks, (JObject)null);
            }
            if (frame == null)
            {
                lacks.Add(Lack("transform", "the face has no front edge"));
                return Tuple.Create(lacks, (JObject)null);
            }
            if (mirrored == null || mirrored.Type == JTokenType.Null)
            {
                lacks.Add(Lack("mirrored", "no rule gives the mirror flag of " + unit + (role == null ? " in no cluster" : " in this cluster")));
                return Tuple.Create(lacks, (JObject)null);
            }
            JObject fit = section[fitUnit ?? unit] as JObject;
            if (fit == null)
            {
                lacks.Add(Lack("insert", "plotFrame holds no anchor and offset for " + unit));
                return Tuple.Create(lacks, (JObject)null);
            }
            return Tuple.Create(lacks, fit);
        }

        // plan_from_book.link_step: per plot of the scope the link of its code on its front with the hand of the step hands
        public static Dictionary<string, Dictionary<string, object>> Step(IEnumerable<string> numbers, IReadOnlyDictionary<string, Dictionary<string, string>> rows,
            IReadOnlyDictionary<string, FrontItem> fronts, IReadOnlyDictionary<string, ClusterRole> roles, IReadOnlyDictionary<string, Dictionary<string, object>> hands, JObject keyRules,
            IReadOnlyDictionary<string, string> files, JObject rules)
        {
            // plan_rules.key_frame_section: a linkOrigin with no byCode object is refused with the line of check_key_rules, never read as empty
            JObject section = keyRules["linkOrigin"]?["byCode"] as JObject ?? throw new System.IO.InvalidDataException("keyRules linkOrigin codes "
                + ((keyRules["linkOrigin"] as JObject) == null ? "None" : SiteClusters.PyText(keyRules["linkOrigin"]["rule"])) + ", a build gives only plotFrameRefit with byCode (R337)");
            BuildRules.CheckLinkFits(keyRules);
            var found = new Dictionary<string, Dictionary<string, object>>();
            foreach (string number in numbers)
            {
                rows.TryGetValue(number, out Dictionary<string, string> row);
                string unit = null;
                row?.TryGetValue("typologyRev9", out unit);
                unit = string.IsNullOrEmpty(unit) ? null : unit;
                fronts.TryGetValue(number, out FrontItem item);
                JToken mirrored = hands.TryGetValue(number, out Dictionary<string, object> hand) ? hand["mirrored"] as JToken : null;
                string fitUnit = null;
                JObject fit = null, placed = null;
                var lacks = new JArray();
                if (unit != null)
                {
                    JToken keo = (rules["typologies"] as JObject)?[unit]?["keo_file"];
                    fitUnit = section[unit] != null ? unit : keo != null && keo.Type == JTokenType.String ? (string)keo : null;
                    fit = fitUnit == null ? null : section[fitUnit] as JObject;
                    roles.TryGetValue(number, out ClusterRole role);
                    Tuple<JArray, JObject> link = Lacks(number, unit, item?.Face, item?.Frame, mirrored, section, files, role, fitUnit);
                    lacks = link.Item1;
                    placed = link.Item2;
                }
                double[] offset = fit == null ? null : ((JArray)fit["offset"]).Select(BuildFace.Number).ToArray();
                bool turned = placed != null && Truthy(mirrored);
                double[] insert = placed == null ? null : PredictedOrigin(item.Frame, turned, (string)placed["anchor"], ((JArray)placed["offset"]).Select(BuildFace.Number).ToArray());
                found[number] = new Dictionary<string, object>
                {
                    ["prototypeFile"] = unit != null && files.TryGetValue(unit, out string file) ? file : null, ["fitUnit"] = fitUnit, ["anchor"] = fit == null ? null : fit["anchor"]?.DeepClone(),
                    ["offset"] = offset, ["mirrored"] = unit == null ? null : mirrored?.DeepClone(), ["insert"] = insert,
                    ["rotationDeg"] = placed == null ? null : new StepValue { Raw = RotationOf(item.Frame, turned), Written = PlanRotation(RotationOf(item.Frame, turned)) },
                    ["lacks"] = lacks,
                };
            }
            return found;
        }
    }
}
