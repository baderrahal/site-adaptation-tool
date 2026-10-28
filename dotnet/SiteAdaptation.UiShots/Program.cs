using System;
using System.IO;

namespace SiteAdaptation.UiShots
{
    // Writes the draft PNG files of steps/ui-shots, or of the folder given as the one argument
    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            string folder = args.Length > 0 ? args[0] : Path.Combine("steps", "ui-shots");
            foreach (string path in Shots.WriteAll(folder))
            {
                Console.WriteLine(path + " " + new FileInfo(path).Length + " bytes");
            }
            return 0;
        }
    }
}
