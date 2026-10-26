using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R354 to R357. What the build steps take from Python as Python gives it, so the C# of plan_from_book.py decides the same: the order of
    // link_summary.sort_key, str.isdigit and int of a plot number, and a JSON input read with every number the nearest double of its text
    public static class PyMath
    {
        // link_summary.sort_key: a text re.fullmatch(r"-?\d+(\.\d+)?") takes, \d any decimal digit, before every other text, by its float
        // then by the text, the other texts by the text
        private static readonly Regex NumberText = new Regex(@"^-?\d+(\.\d+)?\z");

        public static readonly IComparer<string> SortKey = Comparer<string>.Create(CompareSortKey);

        public static int CompareSortKey(string one, string other)
        {
            bool first = IsNumberText(one), second = IsNumberText(other);
            if (first != second)
            {
                return first ? -1 : 1;
            }
            if (first)
            {
                int byValue = Float(one).CompareTo(Float(other));
                if (byValue != 0)
                {
                    return byValue;
                }
            }
            return string.CompareOrdinal(one, other);
        }

        private static bool IsNumberText(string text)
        {
            return text != null && NumberText.IsMatch(text);
        }

        // Python float of a text of decimal digits, a sign and a point, each decimal digit of any script read as its value, as float() reads it
        private static double Float(string text)
        {
            var ascii = new StringBuilder();
            foreach (char letter in text)
            {
                int digit = CharUnicodeInfo.GetDecimalDigitValue(letter);
                ascii.Append(digit >= 0 ? (char)('0' + digit) : letter);
            }
            return ExactDouble.Parse(ascii.ToString());
        }

        // Python str.isdigit: one character or more, each with a digit value, a superscript digit among them
        public static bool IsDigit(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }
            for (int index = 0; index < text.Length; index += char.IsSurrogatePair(text, index) ? 2 : 1)
            {
                if (CharUnicodeInfo.GetDigitValue(text, index) < 0)
                {
                    return false;
                }
            }
            return true;
        }

        // A JSON input as Python json.loads reads it: every number with a point or an exponent the nearest double of its text by ExactDouble,
        // where double.Parse of net48 can take one unit of the last bit off, texts kept as texts and never taken for dates. A number past the
        // decimal type is refused with the role named
        public static JToken ReadJson(string path, string role)
        {
            JToken found;
            try
            {
                using (var reader = new JsonTextReader(new StringReader(WorkFiles.ReadText(path))) { DateParseHandling = DateParseHandling.None, FloatParseHandling = FloatParseHandling.Decimal })
                {
                    found = JToken.Load(reader);
                }
            }
            catch (JsonReaderException failure) when (failure.InnerException is OverflowException)
            {
                throw new InvalidDataException("the input " + role + " holds a number past 7.9E+28, which R385 never writes, " + failure.Message + " (R354)");
            }
            IEnumerable<JToken> tokens = found is JContainer container ? container.DescendantsAndSelf() : new[] { found };
            foreach (JValue value in tokens.OfType<JValue>().Where(item => item.Type == JTokenType.Float).ToList())
            {
                value.Value = ExactDouble.Parse(((decimal)value.Value).ToString(CultureInfo.InvariantCulture));
            }
            return found;
        }
    }
}
