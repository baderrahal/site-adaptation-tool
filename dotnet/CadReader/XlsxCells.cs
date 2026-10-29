using System;
using System.Collections.Generic;
using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Spreadsheet;
using SiteAdaptation.Common;

namespace CadReader
{
    // R364, R365. The one cell writer of the level Excel and the report Excel: the stylesheet, a cell of a text, a number or a flag, and a row,
    // so both files write a number, a bold title, the editable fill and a bordered range alike (review 1 of PR 96)
    internal static class XlsxCells
    {
        // The style indexes of the stylesheet Styles writes
        public const uint Plain = 0;
        public const uint Bold = 1;
        public const uint Editable = 2;
        public const uint Boxed = 6;
        public const uint BoxedThree = 7;

        // The style of a number by its format, LevelExcelText and ReportExcelText.ShareFormat, both 0.000 for three decimals
        public static readonly Dictionary<string, uint> FormatStyle = new Dictionary<string, uint>
        {
            [LevelExcelText.FourDecimals] = 3,
            [LevelExcelText.ThreeDecimals] = 4,
            [LevelExcelText.Whole] = 5,
        };

        public static Stylesheet Styles()
        {
            Func<BorderPropertiesType, BorderPropertiesType> thin = side => { side.Style = BorderStyleValues.Thin; return side; };
            return new Stylesheet(
                new NumberingFormats(
                    new NumberingFormat { NumberFormatId = 164, FormatCode = LevelExcelText.FourDecimals },
                    new NumberingFormat { NumberFormatId = 165, FormatCode = LevelExcelText.ThreeDecimals }) { Count = 2 },
                new Fonts(new Font(), new Font(new Bold())) { Count = 2 },
                new Fills(
                    new Fill(new PatternFill { PatternType = PatternValues.None }),
                    new Fill(new PatternFill { PatternType = PatternValues.Gray125 }),
                    new Fill(new PatternFill(new ForegroundColor { Rgb = LevelExcelText.EditableFill }) { PatternType = PatternValues.Solid })) { Count = 3 },
                new Borders(new Border(), new Border(thin(new LeftBorder()), thin(new RightBorder()), thin(new TopBorder()), thin(new BottomBorder()), new DiagonalBorder())) { Count = 2 },
                new CellStyleFormats(new CellFormat()) { Count = 1 },
                new CellFormats(
                    new CellFormat(),
                    new CellFormat { FontId = 1, ApplyFont = true },
                    new CellFormat { FillId = 2, ApplyFill = true },
                    new CellFormat { NumberFormatId = 164, ApplyNumberFormat = true },
                    new CellFormat { NumberFormatId = 165, ApplyNumberFormat = true },
                    new CellFormat { NumberFormatId = 1, ApplyNumberFormat = true },
                    new CellFormat { BorderId = 1, ApplyBorder = true },
                    new CellFormat { BorderId = 1, ApplyBorder = true, NumberFormatId = 165, ApplyNumberFormat = true }) { Count = 8 },
                new CellStyles(new CellStyle { Name = "Normal", FormatId = 0, BuiltinId = 0 }) { Count = 1 });
        }

        // A number as the shortest text that reads back as the same double, as Python repr gives it, G17 where R of net48 gives another double
        public static string NumberText(object value)
        {
            if (value is long whole)
            {
                return whole.ToString(CultureInfo.InvariantCulture);
            }
            double number = (double)value;
            string text = number.ToString("R", CultureInfo.InvariantCulture);
            return ExactDouble.Parse(text) == number ? text : number.ToString("G17", CultureInfo.InvariantCulture);
        }

        public static Cell MakeCell(string reference, object value, uint style)
        {
            if (value is string text)
            {
                return new Cell(new InlineString(new Text(text) { Space = SpaceProcessingModeValues.Preserve })) { CellReference = reference, DataType = CellValues.InlineString, StyleIndex = style };
            }
            if (value is bool flag)
            {
                return new Cell(new CellValue(flag ? "1" : "0")) { CellReference = reference, DataType = CellValues.Boolean, StyleIndex = style };
            }
            return new Cell(new CellValue(NumberText(value))) { CellReference = reference, StyleIndex = style };
        }

        // A row of cells, an empty cell written only where its style draws a border, so a bordered range holds no gap
        public static Row MakeRow(int index, IEnumerable<object> values, Func<int, uint> style)
        {
            var row = new Row { RowIndex = (uint)index };
            int column = 0;
            foreach (object value in values)
            {
                column++;
                uint chosen = style(column - 1);
                if (value != null)
                {
                    row.Append(MakeCell(XlsxSheet.ColumnLetters(column) + index, value, chosen));
                }
                else if (chosen == Boxed || chosen == BoxedThree)
                {
                    row.Append(new Cell { CellReference = XlsxSheet.ColumnLetters(column) + index, StyleIndex = chosen });
                }
            }
            return row;
        }
    }
}
