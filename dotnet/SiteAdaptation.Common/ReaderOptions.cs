using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SiteAdaptation.Common
{
    // once reads the contents of each linked file one time into linkedFiles. none reads no contents at all
    public enum LinkContents
    {
        Once,
        None,
    }

    public class ReaderOptions
    {
        public const string FileName = "reader-options.txt";
        public const string LinkContentsFlag = "--link-contents";

        public LinkContents LinkContents { get; set; } = LinkContents.Once;

        // Accepts "--link-contents none" and "--link-contents=none". Anything else is an ArgumentException
        public static ReaderOptions Parse(IEnumerable<string> args)
        {
            var options = new ReaderOptions();
            var list = args.ToList();
            for (int index = 0; index < list.Count; index++)
            {
                string flag = list[index];
                string value = null;
                int equals = flag.IndexOf('=');
                if (equals > 0)
                {
                    value = flag.Substring(equals + 1);
                    flag = flag.Substring(0, equals);
                }
                if (!string.Equals(flag, LinkContentsFlag, StringComparison.Ordinal))
                {
                    throw new ArgumentException("Unknown option " + flag + ". The only option is " + LinkContentsFlag + " none|once");
                }
                if (value == null)
                {
                    index++;
                    value = index < list.Count ? list[index] : null;
                }
                options.LinkContents = ParseContents(value);
            }
            return options;
        }

        // The add-in has no command line, so the flags sit in reader-options.txt next to the DLL.
        // Words are split on white space and lines starting with # are skipped. A missing file gives the defaults
        public static ReaderOptions Load(string folder)
        {
            string path = Path.Combine(folder ?? "", FileName);
            if (!File.Exists(path))
            {
                return new ReaderOptions();
            }
            var words = File.ReadAllLines(path)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && !line.StartsWith("#", StringComparison.Ordinal))
                .SelectMany(line => line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
            return Parse(words);
        }

        private static LinkContents ParseContents(string value)
        {
            if (string.Equals(value, "once", StringComparison.OrdinalIgnoreCase))
            {
                return LinkContents.Once;
            }
            if (string.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
            {
                return LinkContents.None;
            }
            throw new ArgumentException(LinkContentsFlag + " takes none or once, not " + (value ?? "nothing"));
        }
    }
}
