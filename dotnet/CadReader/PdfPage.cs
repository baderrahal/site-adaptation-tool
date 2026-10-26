using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SiteAdaptation.Common;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Graphics;
using UglyToad.PdfPig.Tokens;

namespace CadReader
{
    // R351. The first page of the location plan read by PdfPig, read only and shared, into the characters and curves pdfplumber 0.11.7 on
    // pdfminer.six gives: a character box is the box of the four corners of its advance, from the descent of the font descriptor to one
    // font size over it, through the text rendering matrix (pdfminer LTChar), in page points from the top left, its size the height of
    // that box, upright when the matrix keeps the text unturned and unmirrored. PdfPig gives the start and end of the base line and the
    // loose box of a letter from the ascent and descent of the font program, which give the matrix. A curve is a painted subpath that is
    // no line and no rectangle, its points the start, the end of each segment and the start again for a close (pdfminer paint_path).
    // Clipping paths are no shape. The page is turned by its Rotate as pdfplumber turns it
    public static class PdfPage
    {
        public sealed class Content
        {
            public List<PageChar> Chars { get; set; }
            public List<PageCurve> Curves { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
            public int Rotation { get; set; }
        }

        public static Content Read(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (PdfDocument document = PdfDocument.Open(stream))
            {
                Page page = document.GetPage(1);
                if (page.MediaBox.Bounds.Left != 0 || page.MediaBox.Bounds.Bottom != 0)
                {
                    throw new InvalidDataException(Path.GetFileName(path) + ": the media box of the page does not start at 0 0, a page read is refused (R351)");
                }
                Dictionary<string, double> descents = Descents(document);
                double height = page.Height;
                var chars = new List<PageChar>();
                foreach (Letter letter in page.Letters)
                {
                    chars.Add(Char(letter, descents, height, path));
                }
                return new Content { Chars = chars, Curves = Curves(page, height), Width = page.Width, Height = height, Rotation = page.Rotation.Value };
            }
        }

        // pdfminer: the descent of a font is the Descent of its font descriptor, of the descendant font for a Type0 font, made negative,
        // in thousandths of the text space, by the BaseFont name, which pdfplumber gives as fontname. Two descriptors of one name that
        // differ are refused
        private static Dictionary<string, double> Descents(PdfDocument document)
        {
            var found = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (IndirectReference reference in document.Structure.CrossReferenceTable.ObjectOffsets.Keys)
            {
                DictionaryToken font;
                try
                {
                    font = document.Structure.GetObject(reference)?.Data as DictionaryToken;
                }
                catch (Exception)
                {
                    continue;
                }
                if (font == null || Name(document, font, "Type") != "Font")
                {
                    continue;
                }
                string baseFont = Name(document, font, "BaseFont");
                DictionaryToken holder = font;
                if (Name(document, font, "Subtype") == "Type0" && Resolve(document, Get(font, "DescendantFonts")) is ArrayToken descendants && descendants.Length > 0)
                {
                    holder = Resolve(document, descendants[0]) as DictionaryToken ?? font;
                }
                if (baseFont == null || !(Resolve(document, Get(holder, "FontDescriptor")) is DictionaryToken descriptor) || !(Resolve(document, Get(descriptor, "Descent")) is NumericToken descent))
                {
                    continue;
                }
                double value = -Math.Abs(descent.Double) * 0.001;
                double known;
                if (found.TryGetValue(baseFont, out known) && known != value)
                {
                    throw new InvalidDataException("two fonts named " + baseFont + " hold another descent in their font descriptors, a page read is refused (R351)");
                }
                found[baseFont] = value;
            }
            return found;
        }

        private static IToken Get(DictionaryToken dictionary, string key)
        {
            IToken value;
            return dictionary.TryGet(NameToken.Create(key), out value) ? value : null;
        }

        private static IToken Resolve(PdfDocument document, IToken token)
        {
            int guard = 0;
            while (token is IndirectReferenceToken reference && guard++ < 32)
            {
                token = document.Structure.GetObject(reference.Data)?.Data;
            }
            return token;
        }

        private static string Name(PdfDocument document, DictionaryToken dictionary, string key)
        {
            return (Resolve(document, Get(dictionary, key)) as NameToken)?.Data;
        }

        private static PageChar Char(Letter letter, Dictionary<string, double> descents, double height, string path)
        {
            UglyToad.PdfPig.PdfFonts.IFont font = letter.GetFont();
            double ascent = font?.GetAscent() ?? double.NaN, programDescent = font?.GetDescent() ?? double.NaN;
            double span = ascent - programDescent;
            PdfRectangle loose = letter.GlyphRectangleLoose;
            // The second column of the rendering matrix, one font size up the glyph, from the loose box of the font program
            double vx = (loose.TopLeft.X - loose.BottomLeft.X) / span, vy = (loose.TopLeft.Y - loose.BottomLeft.Y) / span;
            if (double.IsNaN(vx) || double.IsInfinity(vx) || double.IsNaN(vy) || double.IsInfinity(vy))
            {
                throw new InvalidDataException(Path.GetFileName(path) + ": the letter " + letter.Value + " of the font " + letter.FontName + " gives no glyph height, a page read is refused (R351)");
            }
            double descent;
            if (!descents.TryGetValue(letter.FontName ?? "", out descent))
            {
                descent = programDescent;
            }
            PdfPoint start = letter.StartBaseLine, end = letter.EndBaseLine;
            double[] xs = { start.X + descent * vx, end.X + descent * vx, end.X + (descent + 1) * vx, start.X + (descent + 1) * vx };
            double[] ys = { start.Y + descent * vy, end.Y + descent * vy, end.Y + (descent + 1) * vy, start.Y + (descent + 1) * vy };
            double y0 = ys.Min(), y1 = ys.Max();
            // The first column of the matrix along the base line, or square to the second for a letter of no advance
            double ux = end.X - start.X, uy = end.Y - start.Y;
            if (ux == 0 && uy == 0)
            {
                ux = vy;
                uy = -vx;
            }
            return new PageChar
            {
                Text = letter.Value,
                X0 = xs.Min(),
                X1 = xs.Max(),
                Top = height - y1,
                Bottom = height - y0,
                Size = y1 - y0,
                Upright = ux * vy > 0 && uy * vx <= 0,
            };
        }

        private static List<PageCurve> Curves(Page page, double height)
        {
            var curves = new List<PageCurve>();
            foreach (PdfPath painted in page.Paths)
            {
                if (painted.IsClipping || (!painted.IsFilled && !painted.IsStroked))
                {
                    continue;
                }
                foreach (PdfSubpath subpath in painted)
                {
                    string shape = "";
                    var points = new List<PdfPoint>();
                    PdfPoint? first = null;
                    foreach (PdfSubpath.IPathCommand command in subpath.Commands)
                    {
                        switch (command)
                        {
                            case PdfSubpath.Move move:
                                shape += "m";
                                points.Add(move.Location);
                                first = first ?? move.Location;
                                break;
                            case PdfSubpath.Line line:
                                shape += "l";
                                points.Add(line.To);
                                break;
                            case PdfSubpath.BezierCurve bezier:
                                shape += "c";
                                points.Add(bezier.EndPoint);
                                break;
                            case PdfSubpath.Close _:
                                shape += "h";
                                points.Add(first ?? points[0]);
                                break;
                        }
                    }
                    if (points.Count == 0 || !shape.StartsWith("m", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    if (shape.Length > 3 && shape.EndsWith("lh", StringComparison.Ordinal) && points[points.Count - 2].Equals(points[0]))
                    {
                        shape = shape.Substring(0, shape.Length - 2) + "h";
                        points.RemoveAt(points.Count - 2);
                    }
                    if (shape == "mlh" || shape == "ml" || ((shape == "mlllh" || shape == "mllll") && IsRectangle(points)))
                    {
                        continue;
                    }
                    double y0 = points.Min(point => point.Y), y1 = points.Max(point => point.Y);
                    curves.Add(new PageCurve { X0 = points.Min(point => point.X), X1 = points.Max(point => point.X), Top = height - y1, Bottom = height - y0, Points = points.Count });
                }
            }
            return curves;
        }

        // pdfminer: a closed loop of four sides along the axes is a rectangle
        private static bool IsRectangle(List<PdfPoint> points)
        {
            PdfPoint p0 = points[0], p1 = points[1], p2 = points[2], p3 = points[3];
            bool closed = p0.Equals(points[4]);
            bool square = (p0.X == p1.X && p1.Y == p2.Y && p2.X == p3.X && p3.Y == p0.Y) || (p0.Y == p1.Y && p1.X == p2.X && p2.Y == p3.Y && p3.X == p0.X);
            return closed && square;
        }
    }
}
