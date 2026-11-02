using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SiteAdaptation.Common
{
    // R496. The Revit format year of a .rvt, read from the compound file by seeks, so only the header, the directory and the stream
    // BasicFileInfo are touched whatever the size of the file. Same reading as python/rvt_info.py (R203). A file is only opened to
    // read, and a file that does not read gives null, never an error
    public static class RevitFormat
    {
        private const uint Free = 0xFFFFFFFF;
        private const uint End = 0xFFFFFFFE;
        private const uint FatSector = 0xFFFFFFFD;
        private const int EntrySize = 128;
        private const int Stream = 2;
        private const int Root = 5;
        private const int MaxChain = 1 << 20;
        private const long MaxStream = 1 << 20;
        private const string InfoStream = "BasicFileInfo";

        // A key starts a line or follows a stray character of the binary head, so Reformat and Central Model Path give no Format
        private static readonly Regex Key = new Regex(@"(?<![A-Z' ])Format\s*:\s*(\d{4})", RegexOptions.CultureInvariant);

        public static int? Of(string path)
        {
            try
            {
                using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.RandomAccess))
                {
                    return Of(file);
                }
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException || error is NotSupportedException)
            {
                return null;
            }
        }

        public static int? Of(Stream source)
        {
            try
            {
                byte[] info = new Compound(source).Read(InfoStream);
                return info == null ? (int?)null : Year(info);
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is ArgumentException || error is OverflowException)
            {
                return null;
            }
        }

        public static int? Year(byte[] raw)
        {
            string best = string.Empty;
            for (int start = 0; start < 2 && start <= raw.Length; start++)
            {
                string text = Encoding.Unicode.GetString(raw, start, (raw.Length - start) / 2 * 2);
                if (Marks(text) > Marks(best))
                {
                    best = text;
                }
            }
            foreach (string line in best.Split('\r', '\n', '\0'))
            {
                Match match = Key.Match(line);
                if (match.Success && !(match.Index > 0 && line[match.Index - 1] >= 'a' && line[match.Index - 1] <= 'z'))
                {
                    return int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                }
            }
            return null;
        }

        private static int Marks(string text)
        {
            return text.Count(letter => letter == '\n' || letter == ':');
        }

        private sealed class Compound
        {
            private readonly Stream source;
            private readonly int sector;
            private readonly int mini;
            private readonly uint cutoff;
            private readonly List<uint> fatSectors = new List<uint>();
            private readonly uint directoryStart;
            private readonly uint miniFatStart;

            public Compound(Stream source)
            {
                this.source = source;
                byte[] head = Bytes(0, 512);
                if (BitConverter.ToUInt64(head, 0) != 0xE11AB1A1E011CFD0UL)
                {
                    throw new InvalidDataException("no compound file signature");
                }
                int sectorShift = BitConverter.ToUInt16(head, 0x1E);
                int miniShift = BitConverter.ToUInt16(head, 0x20);
                if (sectorShift < 9 || sectorShift > 16 || miniShift > sectorShift)
                {
                    throw new InvalidDataException("odd sector size");
                }
                sector = 1 << sectorShift;
                mini = 1 << miniShift;
                directoryStart = BitConverter.ToUInt32(head, 0x30);
                cutoff = BitConverter.ToUInt32(head, 0x38);
                miniFatStart = BitConverter.ToUInt32(head, 0x3C);
                uint fatCount = BitConverter.ToUInt32(head, 0x2C);
                for (int at = 0; at < 109; at++)
                {
                    fatSectors.Add(BitConverter.ToUInt32(head, 0x4C + at * 4));
                }
                uint next = BitConverter.ToUInt32(head, 0x44);
                uint difatCount = BitConverter.ToUInt32(head, 0x48);
                int per = sector / 4;
                for (uint seen = 0; next < FatSector && seen < difatCount && seen < MaxChain; seen++)
                {
                    byte[] block = Bytes(Offset(next), sector);
                    for (int at = 0; at < per - 1; at++)
                    {
                        fatSectors.Add(BitConverter.ToUInt32(block, at * 4));
                    }
                    next = BitConverter.ToUInt32(block, (per - 1) * 4);
                }
                fatSectors.RemoveAll(item => item >= FatSector);
                if (fatSectors.Count > fatCount)
                {
                    fatSectors.RemoveRange((int)fatCount, fatSectors.Count - (int)fatCount);
                }
            }

            public byte[] Read(string name)
            {
                uint rootStart = Free;
                long rootSize = 0;
                uint start = Free;
                long size = -1;
                int steps = 0;
                foreach (uint current in Chain(directoryStart))
                {
                    byte[] block = Bytes(Offset(current), sector);
                    for (int at = 0; at + EntrySize <= block.Length; at += EntrySize)
                    {
                        int kind = block[at + 66];
                        int length = BitConverter.ToUInt16(block, at + 64);
                        if (kind == Root)
                        {
                            rootStart = BitConverter.ToUInt32(block, at + 116);
                            rootSize = BitConverter.ToUInt32(block, at + 120);
                        }
                        else if (kind == Stream && size < 0 && length >= 2 && length <= 64
                            && Encoding.Unicode.GetString(block, at, length - 2) == name)
                        {
                            start = BitConverter.ToUInt32(block, at + 116);
                            size = BitConverter.ToUInt32(block, at + 120);
                        }
                        steps++;
                    }
                    if ((size >= 0 && rootStart != Free) || steps > MaxChain)
                    {
                        break;
                    }
                }
                if (size < 0)
                {
                    return null;
                }
                if (size > MaxStream)
                {
                    throw new InvalidDataException("the stream is too large to be the file info");
                }
                return size < cutoff ? MiniBytes(start, (int)size, rootStart, rootSize) : Big(start, (int)size);
            }

            private byte[] Big(uint start, int size)
            {
                var result = new byte[size];
                int filled = 0;
                foreach (uint current in Chain(start))
                {
                    if (filled >= size)
                    {
                        break;
                    }
                    int take = Math.Min(sector, size - filled);
                    Array.Copy(Bytes(Offset(current), take), 0, result, filled, take);
                    filled += take;
                }
                if (filled < size)
                {
                    throw new InvalidDataException("the stream chain is short");
                }
                return result;
            }

            private byte[] MiniBytes(uint start, int size, uint rootStart, long rootSize)
            {
                var result = new byte[size];
                int filled = 0;
                uint mine = start;
                int steps = 0;
                while (filled < size)
                {
                    if (mine >= FatSector || steps++ > MaxChain)
                    {
                        throw new InvalidDataException("the mini chain is short");
                    }
                    long at = (long)mine * mini;
                    if (at + mini > rootSize)
                    {
                        throw new InvalidDataException("the mini sector lies outside the mini stream");
                    }
                    uint holder = Nth(rootStart, (int)(at / sector));
                    int take = Math.Min(mini, size - filled);
                    Array.Copy(Bytes(Offset(holder) + at % sector, take), 0, result, filled, take);
                    filled += take;
                    mine = Word(Nth(miniFatStart, (int)((long)mine * 4 / sector)), (int)((long)mine * 4 % sector));
                }
                return result;
            }

            private uint Nth(uint start, int index)
            {
                uint current = start;
                for (int at = 0; at < index; at++)
                {
                    current = FatNext(current);
                }
                if (current >= FatSector)
                {
                    throw new InvalidDataException("a chain ends early");
                }
                return current;
            }

            private uint Word(uint holder, int within)
            {
                return BitConverter.ToUInt32(Bytes(Offset(holder) + within, 4), 0);
            }

            private IEnumerable<uint> Chain(uint start)
            {
                uint current = start;
                for (int steps = 0; current < FatSector; steps++)
                {
                    if (steps > MaxChain)
                    {
                        throw new InvalidDataException("a sector chain loops");
                    }
                    yield return current;
                    current = FatNext(current);
                }
            }

            private uint FatNext(uint current)
            {
                int per = sector / 4;
                long index = current / per;
                if (index >= fatSectors.Count)
                {
                    throw new InvalidDataException("a sector leaves the table");
                }
                return BitConverter.ToUInt32(Bytes(Offset(fatSectors[(int)index]) + current % per * 4, 4), 0);
            }

            private long Offset(uint number)
            {
                return ((long)number + 1) * sector;
            }

            private byte[] Bytes(long at, int count)
            {
                if (at < 0 || at + count > source.Length)
                {
                    throw new InvalidDataException("the read leaves the file");
                }
                source.Seek(at, SeekOrigin.Begin);
                var buffer = new byte[count];
                int got = 0;
                while (got < count)
                {
                    int read = source.Read(buffer, got, count - got);
                    if (read <= 0)
                    {
                        throw new InvalidDataException("the file ends early");
                    }
                    got += read;
                }
                return buffer;
            }
        }
    }
}
