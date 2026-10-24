using System.Collections.Generic;
using System.IO;
using RevitApp = Autodesk.Revit.ApplicationServices.Application;
using Autodesk.Revit.DB;
using SiteAdaptation.Common;

namespace RevitPrototypeReader
{
    // The one way into the reader, for the button and for the test host (R103), so neither holds a copy of the read
    public static class PrototypeReading
    {
        // The key parameter list and the reader options sit next to this DLL
        public static string Folder
        {
            get { return Path.GetDirectoryName(typeof(PrototypeReading).Assembly.Location); }
        }

        public static IList<string> KeyNames()
        {
            return KeyParameters.Load(Folder);
        }

        // Throws ArgumentException when the options file does not read
        public static ReaderOptions Options()
        {
            return ReaderOptions.Load(Folder);
        }

        public static PrototypesFile NewFile(RevitApp app)
        {
            return new PrototypesFile
            {
                Tool = "RevitPrototypeReader",
                RevitVersion = app.VersionNumber,
                LengthUnit = "m",
                AngleUnit = "deg",
                Documents = new List<DocumentEntry>(),
            };
        }

        public static DocumentEntry ReadDocument(Document document, IList<string> keyNames, ReaderOptions options)
        {
            return RevitExtractor.Read(document, document.Title, keyNames, options);
        }

        // The open document as the button reads it with The open document
        public static PrototypesFile ReadOpenDocument(RevitApp app, Document document)
        {
            ReaderOptions options = Options();
            PrototypesFile file = NewFile(app);
            file.Documents.Add(ReadDocument(document, KeyNames(), options));
            return file;
        }
    }
}
