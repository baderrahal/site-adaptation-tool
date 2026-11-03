using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SiteAdaptation.Common;
using Xunit;

namespace SiteAdaptation.Tests
{
    // R496. The Revit format of a .rvt from its bytes and the oldest format of a folder. Compound files made up here, no Revit file
    public sealed class R496_RevitFormatTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "r496-" + Guid.NewGuid().ToString("N"));

        public R496_RevitFormatTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            Directory.Delete(folder, true);
        }

        private static byte[] Info(int year, int junk = 2, int padTo = 0)
        {
            string text = "Worksharing: Not enabled\r\nUsername: x\r\nCentral Model Path: \r\nFormat: " + year + "\r\nBuild: 20210309_1515(x64)\r\n";
            text = text.PadRight(padTo, ' ');
            var bytes = new List<byte>(new byte[junk]);
            bytes.AddRange(Encoding.Unicode.GetBytes(text));
            return bytes.ToArray();
        }

        // A compound file of 512 byte sectors with one stream BasicFileInfo. Under 4096 bytes the stream lives in the mini stream
        private static byte[] Compound(byte[] stream, string name = "BasicFileInfo")
        {
            bool mini = stream.Length < 4096;
            int dataSectors = (stream.Length + 511) / 512;
            var file = new List<byte>();
            var head = new byte[512];
            BitConverter.GetBytes(0xE11AB1A1E011CFD0UL).CopyTo(head, 0);
            BitConverter.GetBytes((ushort)0x3E).CopyTo(head, 0x18);
            BitConverter.GetBytes((ushort)3).CopyTo(head, 0x1A);
            BitConverter.GetBytes((ushort)0xFFFE).CopyTo(head, 0x1C);
            BitConverter.GetBytes((ushort)9).CopyTo(head, 0x1E);
            BitConverter.GetBytes((ushort)6).CopyTo(head, 0x20);
            BitConverter.GetBytes(1u).CopyTo(head, 0x2C);
            BitConverter.GetBytes(1u).CopyTo(head, 0x30);
            BitConverter.GetBytes(4096u).CopyTo(head, 0x38);
            BitConverter.GetBytes(mini ? 2u : 0xFFFFFFFEu).CopyTo(head, 0x3C);
            BitConverter.GetBytes(mini ? 1u : 0u).CopyTo(head, 0x40);
            BitConverter.GetBytes(0xFFFFFFFEu).CopyTo(head, 0x44);
            for (int at = 0; at < 109; at++)
            {
                BitConverter.GetBytes(at == 0 ? 0u : 0xFFFFFFFFu).CopyTo(head, 0x4C + at * 4);
            }
            file.AddRange(head);

            var fat = new uint[128];
            for (int at = 0; at < 128; at++)
            {
                fat[at] = 0xFFFFFFFF;
            }
            fat[0] = 0xFFFFFFFD;
            fat[1] = 0xFFFFFFFE;
            int first = mini ? 3 : 2;
            if (mini)
            {
                fat[2] = 0xFFFFFFFE;
            }
            for (int at = 0; at < dataSectors; at++)
            {
                fat[first + at] = at == dataSectors - 1 ? 0xFFFFFFFE : (uint)(first + at + 1);
            }
            var fatBytes = new byte[512];
            for (int at = 0; at < 128; at++)
            {
                BitConverter.GetBytes(fat[at]).CopyTo(fatBytes, at * 4);
            }
            file.AddRange(fatBytes);

            var directory = new byte[512];
            Entry(directory, 0, "Root Entry", 5, mini ? 3u : 0xFFFFFFFEu, mini ? (long)dataSectors * 512 : 0);
            Entry(directory, 128, name, 2, mini ? 0u : 2u, stream.Length);
            file.AddRange(directory);

            if (mini)
            {
                var miniFat = new byte[512];
                int miniSectors = (stream.Length + 63) / 64;
                for (int at = 0; at < 128; at++)
                {
                    BitConverter.GetBytes(at < miniSectors - 1 ? (uint)(at + 1) : at == miniSectors - 1 ? 0xFFFFFFFEu : 0xFFFFFFFFu).CopyTo(miniFat, at * 4);
                }
                file.AddRange(miniFat);
            }
            var data = new byte[dataSectors * 512];
            stream.CopyTo(data, 0);
            file.AddRange(data);
            return file.ToArray();
        }

        private static void Entry(byte[] directory, int at, string name, byte kind, uint start, long size)
        {
            byte[] raw = Encoding.Unicode.GetBytes(name);
            raw.CopyTo(directory, at);
            BitConverter.GetBytes((ushort)(raw.Length + 2)).CopyTo(directory, at + 64);
            directory[at + 66] = kind;
            BitConverter.GetBytes(start).CopyTo(directory, at + 116);
            BitConverter.GetBytes(size).CopyTo(directory, at + 120);
        }

        private string Write(string name, byte[] bytes)
        {
            string path = Path.Combine(folder, name);
            File.WriteAllBytes(path, bytes);
            return path;
        }

        [Fact]
        public void R496_the_format_is_read_from_a_stream_in_the_mini_stream()
        {
            Assert.Equal(2021, RevitFormat.Of(new MemoryStream(Compound(Info(2021)))));
            Assert.Equal(2024, RevitFormat.Of(new MemoryStream(Compound(Info(2024)))));
        }

        [Fact]
        public void R496_the_format_is_read_from_a_stream_of_ordinary_sectors_and_from_an_odd_head()
        {
            Assert.Equal(2022, RevitFormat.Of(new MemoryStream(Compound(Info(2022, 2, 6000)))));
            Assert.Equal(2023, RevitFormat.Of(new MemoryStream(Compound(Info(2023, 3)))));
        }

        [Fact]
        public void R496_a_file_that_does_not_read_gives_no_format()
        {
            Assert.Null(RevitFormat.Of(new MemoryStream(new byte[0])));
            Assert.Null(RevitFormat.Of(new MemoryStream(Encoding.ASCII.GetBytes("not a compound file at all, only text and more text"))));
            byte[] whole = Compound(Info(2021));
            Assert.Null(RevitFormat.Of(new MemoryStream(whole, 0, 700)));
            Assert.Null(RevitFormat.Of(new MemoryStream(Compound(Info(2021), "OtherStream"))));
            Assert.Null(RevitFormat.Of(Path.Combine(folder, "missing.rvt")));
            Assert.Null(RevitFormat.Of(Write("junk.rvt", new byte[] { 1, 2, 3 })));
        }

        [Fact]
        public void R496_a_word_that_only_ends_in_format_is_no_format()
        {
            byte[] bytes = Encoding.Unicode.GetBytes("Reformat: 1999\r\nCentral Model Path: x Format: 1998\r\n");
            Assert.Null(RevitFormat.Of(new MemoryStream(Compound(bytes))));
        }

        [Fact]
        public void R496_a_folder_of_2021_files_gives_2021_and_a_folder_of_2024_files_gives_2024()
        {
            string old = Directory.CreateDirectory(Path.Combine(folder, "old")).FullName;
            string current = Directory.CreateDirectory(Path.Combine(folder, "new")).FullName;
            File.WriteAllBytes(Path.Combine(old, "a.rvt"), Compound(Info(2021)));
            File.WriteAllBytes(Path.Combine(old, "b.rvt"), Compound(Info(2021)));
            File.WriteAllBytes(Path.Combine(current, "a.rvt"), Compound(Info(2024)));
            Assert.Equal(2021, PrototypeFormats.Oldest(old));
            Assert.Equal(2024, PrototypeFormats.Oldest(current));
        }

        [Fact]
        public void R496_a_folder_of_mixed_formats_gives_the_oldest_and_backups_are_left_out()
        {
            File.WriteAllBytes(Path.Combine(folder, "a.rvt"), Compound(Info(2024)));
            File.WriteAllBytes(Path.Combine(folder, "b.rvt"), Compound(Info(2022)));
            File.WriteAllBytes(Path.Combine(folder, "b.0001.rvt"), Compound(Info(2019)));
            Assert.Equal(2022, PrototypeFormats.Oldest(folder));
        }

        [Fact]
        public void R496_an_empty_a_missing_and_an_unreadable_folder_give_no_format()
        {
            Assert.Null(PrototypeFormats.Oldest(folder));
            Assert.Null(PrototypeFormats.Oldest(Path.Combine(folder, "missing")));
            Assert.Null(PrototypeFormats.Oldest(null));
            Write("junk.rvt", new byte[] { 9, 9, 9 });
            Assert.Null(PrototypeFormats.Oldest(folder));
            Write("good.rvt", Compound(Info(2021)));
            Assert.Equal(2021, PrototypeFormats.Oldest(folder));
        }

        [Fact]
        public void R496_the_look_opens_at_most_the_limit_of_files_and_other_files_are_not_opened()
        {
            for (int at = 0; at < 6; at++)
            {
                Write("f" + at + ".rvt", new byte[1]);
            }
            Write("note.txt", new byte[1]);
            var opened = new List<string>();
            int? oldest = PrototypeFormats.Oldest(folder, 3, path =>
            {
                opened.Add(Path.GetFileName(path));
                return 2021;
            });
            Assert.Equal(2021, oldest);
            Assert.Equal(new[] { "f0.rvt", "f1.rvt", "f2.rvt" }, opened);
            Assert.Equal(20, PrototypeFormats.Limit);
        }

        [Fact]
        public void R496_only_a_format_older_than_the_running_revit_is_old()
        {
            Assert.Equal(2021, PrototypeFormats.OldYear(2021, 2024));
            Assert.Null(PrototypeFormats.OldYear(2024, 2024));
            Assert.Null(PrototypeFormats.OldYear(2025, 2024));
            Assert.Null(PrototypeFormats.OldYear(null, 2024));
            Assert.Equal(2024, PrototypeFormats.OldYear(2024, 2025));
        }
    }
}
