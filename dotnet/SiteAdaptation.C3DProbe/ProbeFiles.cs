using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace SiteAdaptation.C3DProbe
{
    // What the runner hands the probe. A command in a core console takes no arguments a script can pass safely,
    // so the runner sets these before it starts the process (R151)
    public static class ProbeEnv
    {
        public const string Work = "C3DPROBE_WORK";
        public const string Out = "C3DPROBE_OUT";
        public const string Points = "C3DPROBE_POINTS";
        public const string Surfaces = "C3DPROBE_SURFACES";
    }

    // The probe only reads a drawing that lies under the folder of working copies (R150)
    public static class WorkGuard
    {
        public static string Refusal(string workRoot, string drawing)
        {
            if (string.IsNullOrWhiteSpace(workRoot))
            {
                return ProbeEnv.Work + " is not set";
            }
            if (string.IsNullOrWhiteSpace(drawing))
            {
                return "the drawing has no file name";
            }
            string root = Path.GetFullPath(workRoot).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            string file = Path.GetFullPath(drawing);
            if (!file.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return "the drawing " + file + " is not under the working copies in " + root;
            }
            return null;
        }
    }

    public sealed class QueryPoint
    {
        public string Id { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
    }

    // Plain CSV keeps the core console free of a JSON library that could clash with the one Civil 3D loads
    public static class PointTable
    {
        public static List<QueryPoint> Read(IEnumerable<string> lines)
        {
            var points = new List<QueryPoint>();
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0)
                {
                    continue;
                }
                string[] cells = line.Split(',');
                if (cells.Length < 3
                    || !double.TryParse(cells[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double x)
                    || !double.TryParse(cells[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double y))
                {
                    // The header row and any row that is not id,x,y
                    continue;
                }
                points.Add(new QueryPoint { Id = cells[0].Trim(), X = x, Y = y });
            }
            return points;
        }

        public static string Row(string id, string surface, double z)
        {
            return id + "," + surface.Replace(",", " ") + "," + Round.Text(z, 4);
        }
    }

    public static class Round
    {
        // R2: 3 decimals, levels keep 4. Negative zero would print as -0
        public static double To(double value, int decimals = 3)
        {
            double rounded = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
            return rounded == 0 ? 0 : rounded;
        }

        public static string Text(double value, int decimals = 3)
        {
            return To(value, decimals).ToString("0.####", CultureInfo.InvariantCulture);
        }
    }

    public static class Utf8
    {
        public static void Write(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }
    }
}
