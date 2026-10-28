using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SiteAdaptation.AddIn.Pane
{
    // R372. A text of the label list holds its values as <n>, <m> or <name>. Fill puts the values in, in their order, so the
    // code keeps the text word for word as the list gives it
    public static class TextTemplate
    {
        private static readonly Regex Slot = new Regex(@"<[a-z]+>");

        public static string Fill(string template, params object[] values)
        {
            int next = 0;
            string filled = Slot.Replace(template, match =>
            {
                if (next >= values.Length)
                {
                    throw new ArgumentException("the text " + template + " has more values than " + values.Length, nameof(values));
                }
                return Convert.ToString(values[next++], CultureInfo.InvariantCulture);
            });
            if (next != values.Length)
            {
                throw new ArgumentException("the text " + template + " has " + next + " values, not " + values.Length, nameof(values));
            }
            return filled;
        }

        // Thousands parted by commas as the mock writes 1,247
        public static string Count(int value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }
    }
}
