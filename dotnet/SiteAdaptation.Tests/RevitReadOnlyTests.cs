using System;
using System.IO;
using System.Linq;
using Xunit;

namespace SiteAdaptation.Tests
{
    // The Revit API cannot load outside Revit, so the read only rule is checked on the source text
    public class RevitReadOnlyTests
    {
        private static string RepoFile(params string[] parts)
        {
            string folder = AppContext.BaseDirectory;
            while (folder != null && !File.Exists(Path.Combine(folder, "docs", "rules.md")))
            {
                folder = Path.GetDirectoryName(folder);
            }
            Assert.NotNull(folder);
            return Path.Combine(new[] { folder }.Concat(parts).ToArray());
        }

        [Fact]
        public void R1_command_runs_in_read_only_transaction_mode()
        {
            string command = File.ReadAllText(RepoFile("dotnet", "RevitPrototypeReader", "ExportPrototypesCommand.cs"));
            Assert.Contains("[Transaction(TransactionMode.ReadOnly)]", command);
            Assert.DoesNotContain("TransactionMode.Manual", command);
        }

        [Fact]
        public void R1_add_in_never_starts_a_transaction_or_saves_a_document()
        {
            string[] banned = { "new Transaction", "new TransactionGroup", "new SubTransaction", ".Save(", ".SaveAs(", "Close(true)", "SynchronizeWithCentral" };
            foreach (string source in Directory.GetFiles(RepoFile("dotnet", "RevitPrototypeReader"), "*.cs"))
            {
                string text = File.ReadAllText(source);
                foreach (string word in banned)
                {
                    Assert.False(text.Contains(word), Path.GetFileName(source) + " contains " + word);
                }
            }
        }

        [Fact]
        public void R1_every_opened_document_is_closed_without_saving()
        {
            string command = File.ReadAllText(RepoFile("dotnet", "RevitPrototypeReader", "ExportPrototypesCommand.cs"));
            Assert.Contains("doc.Close(false)", command);
        }
    }
}
