using System;
using System.Globalization;
using System.Numerics;
using System.Text;
using Newtonsoft.Json.Linq;

namespace SiteAdaptation.Common
{
    // R354. A JSON text read as json.loads of Python reads it, for the inputs of a build (PyMath.ReadJson): a number of a point or an
    // exponent the nearest double of its text by ExactDouble, inf or 0.0 past the doubles, -0.0 kept, one of digits alone an integer, long or
    // BigInteger, NaN, Infinity and -Infinity taken, a key met again keeping its place with the last value, and a text json.loads refuses
    // refused: a trailing comma, a comment, a quote of ', a leading zero or plus, a point with no digit beside it, a control character in a
    // string, extra data. The review of PR 55 found Newtonsoft by decimal gave 1.2345678901234567e-15, 3e-29 and -0.0 other doubles and threw
    // on 8e28 (B4, round_addin logs p4efix1_b4_probe_head.txt)
    public sealed class PyJson
    {
        private readonly string text;
        private int at;

        private PyJson(string text)
        {
            this.text = text ?? "";
        }

        // P2 of the review of PR 55: the int digit limit of Python, int() of a text of more digits raises ValueError, so json.loads does
        private const int IntDigits = 4300;

        // A key met again in one object where the reader refuses it, as no_duplicate_keys of project_profile.read_object does
        public sealed class DuplicateKey : FormatException
        {
            public DuplicateKey(string key) : base("duplicate key " + key)
            {
                Key = key;
            }

            public string Key { get; }
        }

        private readonly bool refuseDuplicates;

        private PyJson(string text, bool refuseDuplicates) : this(text)
        {
            this.refuseDuplicates = refuseDuplicates;
        }

        // The value of the whole text, FormatException with the place when json.loads would refuse it, DuplicateKey for a key met again
        // in one object when refuseDuplicates, as project_profile.read_object refuses it
        public static JToken Parse(string text, bool refuseDuplicates = false)
        {
            var reader = new PyJson(text, refuseDuplicates);
            reader.Space();
            JToken value = reader.Value();
            reader.Space();
            if (reader.at != reader.text.Length)
            {
                throw reader.Fail("extra data");
            }
            return value;
        }

        private FormatException Fail(string why)
        {
            return new FormatException(why + " at char " + at.ToString(CultureInfo.InvariantCulture));
        }

        private void Space()
        {
            while (at < text.Length && (text[at] == ' ' || text[at] == '\t' || text[at] == '\n' || text[at] == '\r'))
            {
                at++;
            }
        }

        // R345. The text json.dumps of Python gives a value with separators (",", ":") and ensure_ascii: a text in double quotes with every
        // character past ~ and every control as \uXXXX in small hex, a float by repr with NaN, Infinity and -Infinity bare, an integer by
        // its digits, a list and an object in their order. Newtonsoft wrote é bare and -1E+20 (H2 of the fifth review of PR 55)
        public static string Dumps(JToken value)
        {
            var written = new StringBuilder();
            Dump(value, written);
            return written.ToString();
        }

        private static void Dump(JToken value, StringBuilder written)
        {
            switch (value?.Type ?? JTokenType.Null)
            {
                case JTokenType.Null:
                case JTokenType.Undefined:
                    written.Append("null");
                    break;
                case JTokenType.Boolean:
                    written.Append((bool)value ? "true" : "false");
                    break;
                case JTokenType.Integer:
                    written.Append(((JValue)value).Value is BigInteger big ? big.ToString(CultureInfo.InvariantCulture) : Convert.ToInt64(((JValue)value).Value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture));
                    break;
                case JTokenType.Float:
                    double number = (double)value;
                    written.Append(double.IsNaN(number) ? "NaN" : double.IsPositiveInfinity(number) ? "Infinity" : double.IsNegativeInfinity(number) ? "-Infinity" : SheetRows.PyRepr(number));
                    break;
                case JTokenType.Array:
                    written.Append('[');
                    bool first = true;
                    foreach (JToken item in (JArray)value)
                    {
                        written.Append(first ? "" : ",");
                        first = false;
                        Dump(item, written);
                    }
                    written.Append(']');
                    break;
                case JTokenType.Object:
                    written.Append('{');
                    bool start = true;
                    foreach (JProperty property in ((JObject)value).Properties())
                    {
                        written.Append(start ? "" : ",");
                        start = false;
                        Quote(property.Name, written);
                        written.Append(':');
                        Dump(property.Value, written);
                    }
                    written.Append('}');
                    break;
                default:
                    Quote((string)value, written);
                    break;
            }
        }

        private static void Quote(string text, StringBuilder written)
        {
            written.Append('"');
            foreach (char letter in text)
            {
                switch (letter)
                {
                    case '"':
                        written.Append("\\\"");
                        break;
                    case '\\':
                        written.Append("\\\\");
                        break;
                    case '\n':
                        written.Append("\\n");
                        break;
                    case '\r':
                        written.Append("\\r");
                        break;
                    case '\t':
                        written.Append("\\t");
                        break;
                    case '\b':
                        written.Append("\\b");
                        break;
                    case '\f':
                        written.Append("\\f");
                        break;
                    default:
                        if (letter < ' ' || letter > '~')
                        {
                            written.Append("\\u").Append(((int)letter).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            written.Append(letter);
                        }
                        break;
                }
            }
            written.Append('"');
        }

        private static bool Digit(char letter)
        {
            return letter >= '0' && letter <= '9';
        }

        private void Word(string word, string why = "expecting a value")
        {
            if (string.CompareOrdinal(text, at, word, 0, word.Length) != 0 || at + word.Length > text.Length)
            {
                throw Fail(why);
            }
            at += word.Length;
        }

        private JToken Value()
        {
            if (at >= text.Length)
            {
                throw Fail("expecting a value");
            }
            switch (text[at])
            {
                case '{':
                    return Object();
                case '[':
                    return Array();
                case '"':
                    return new JValue(String());
                case 't':
                    Word("true");
                    return new JValue(true);
                case 'f':
                    Word("false");
                    return new JValue(false);
                case 'n':
                    Word("null");
                    return JValue.CreateNull();
                case 'N':
                    Word("NaN");
                    return new JValue(double.NaN);
                case 'I':
                    Word("Infinity");
                    return new JValue(double.PositiveInfinity);
                default:
                    if (text[at] == '-' && at + 1 < text.Length && text[at + 1] == 'I')
                    {
                        at++;
                        Word("Infinity");
                        return new JValue(double.NegativeInfinity);
                    }
                    return Number();
            }
        }

        private JObject Object()
        {
            var found = new JObject();
            at++;
            Space();
            if (at < text.Length && text[at] == '}')
            {
                at++;
                return found;
            }
            while (true)
            {
                if (at >= text.Length || text[at] != '"')
                {
                    throw Fail("expecting a property name in double quotes");
                }
                string key = String();
                Space();
                if (at >= text.Length || text[at] != ':')
                {
                    throw Fail("expecting ':' delimiter");
                }
                at++;
                Space();
                // A key met again keeps its first place and takes the last value, as a Python dict does, or is refused
                JToken value = Value();
                if (refuseDuplicates && found.ContainsKey(key))
                {
                    throw new DuplicateKey(key);
                }
                found[key] = value;
                Space();
                if (at < text.Length && text[at] == ',')
                {
                    at++;
                    Space();
                    continue;
                }
                if (at < text.Length && text[at] == '}')
                {
                    at++;
                    return found;
                }
                throw Fail("expecting ',' delimiter");
            }
        }

        private JArray Array()
        {
            var found = new JArray();
            at++;
            Space();
            if (at < text.Length && text[at] == ']')
            {
                at++;
                return found;
            }
            while (true)
            {
                found.Add(Value());
                Space();
                if (at < text.Length && text[at] == ',')
                {
                    at++;
                    Space();
                    continue;
                }
                if (at < text.Length && text[at] == ']')
                {
                    at++;
                    return found;
                }
                throw Fail("expecting ',' delimiter");
            }
        }

        private string String()
        {
            at++;
            int start = at;
            StringBuilder built = null;
            while (true)
            {
                if (at >= text.Length)
                {
                    throw Fail("unterminated string");
                }
                char letter = text[at];
                if (letter == '"')
                {
                    string found = built == null ? text.Substring(start, at - start) : built.Append(text, start, at - start).ToString();
                    at++;
                    return found;
                }
                if (letter < 0x20)
                {
                    throw Fail("invalid control character");
                }
                if (letter != '\\')
                {
                    at++;
                    continue;
                }
                built = (built ?? new StringBuilder()).Append(text, start, at - start);
                if (at + 1 >= text.Length)
                {
                    throw Fail("unterminated string");
                }
                char escape = text[at + 1];
                int step = 2;
                switch (escape)
                {
                    case '"':
                    case '\\':
                    case '/':
                        built.Append(escape);
                        break;
                    case 'b':
                        built.Append('\b');
                        break;
                    case 'f':
                        built.Append('\f');
                        break;
                    case 'n':
                        built.Append('\n');
                        break;
                    case 'r':
                        built.Append('\r');
                        break;
                    case 't':
                        built.Append('\t');
                        break;
                    case 'u':
                        if (at + 6 > text.Length || !int.TryParse(text.Substring(at + 2, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int code)
                            || text.Substring(at + 2, 4).IndexOfAny(new[] { '+', '-', ' ' }) >= 0)
                        {
                            throw Fail("invalid \\uXXXX escape");
                        }
                        // A surrogate pair stays two UTF-16 units, the one character Python joins them into
                        built.Append((char)code);
                        step = 6;
                        break;
                    default:
                        throw Fail("invalid \\escape");
                }
                at += step;
                start = at;
            }
        }

        // json.scanner NUMBER_RE, (-?(?:0|[1-9]\d*))(\.\d+)?([eE][-+]?\d+)? of ASCII digits as the C scanner of CPython reads it, a float
        // when a fraction or an exponent follows, else an integer
        private JToken Number()
        {
            int start = at;
            if (text[at] == '-')
            {
                at++;
            }
            if (at < text.Length && text[at] == '0')
            {
                at++;
            }
            else if (at < text.Length && text[at] >= '1' && text[at] <= '9')
            {
                while (at < text.Length && Digit(text[at]))
                {
                    at++;
                }
            }
            else
            {
                at = start;
                throw Fail("expecting a value");
            }
            bool real = false;
            if (at + 1 < text.Length && text[at] == '.' && Digit(text[at + 1]))
            {
                real = true;
                at++;
                while (at < text.Length && Digit(text[at]))
                {
                    at++;
                }
            }
            if (at < text.Length && (text[at] == 'e' || text[at] == 'E'))
            {
                int mark = at;
                at++;
                if (at < text.Length && (text[at] == '+' || text[at] == '-'))
                {
                    at++;
                }
                if (at < text.Length && Digit(text[at]))
                {
                    real = true;
                    while (at < text.Length && Digit(text[at]))
                    {
                        at++;
                    }
                }
                else
                {
                    // An e of no digits is no part of the number, the text after the digits is then extra data
                    at = mark;
                }
            }
            string number = text.Substring(start, at - start);
            if (real)
            {
                return new JValue(ExactDouble.Parse(number));
            }
            int digits = number.Length - (number[0] == '-' ? 1 : 0);
            if (digits > IntDigits)
            {
                throw Fail("Exceeds the limit (" + IntDigits + " digits) for integer string conversion: value has " + digits + " digits");
            }
            return long.TryParse(number, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long whole) ? new JValue(whole) : new JValue(BigInteger.Parse(number, CultureInfo.InvariantCulture));
        }
    }
}
