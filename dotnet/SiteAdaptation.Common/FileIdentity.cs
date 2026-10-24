using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace SiteAdaptation.Common
{
    // R101. A name that looks like a test copy is not enough, the file itself must be a copy of its own.
    // A hard link, a symbolic link or a junction to the original would let a step change the original
    public static class FileIdentity
    {
        private const uint ReadAttributes = 0x80;
        private const uint ShareAll = 0x7;
        private const uint OpenExisting = 3;
        private const uint BackupSemantics = 0x02000000;
        private const uint OpenReparsePoint = 0x00200000;

        [StructLayout(LayoutKind.Sequential)]
        private struct FileInformation
        {
            public uint Attributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME Created;
            public System.Runtime.InteropServices.ComTypes.FILETIME Accessed;
            public System.Runtime.InteropServices.ComTypes.FILETIME Written;
            public uint VolumeSerial;
            public uint SizeHigh;
            public uint SizeLow;
            public uint Links;
            public uint IndexHigh;
            public uint IndexLow;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation information);

        public sealed class Identity
        {
            public uint VolumeSerial;
            public ulong Index;
            public uint Links;
        }

        // The file as Windows holds it, read without following a reparse point
        public static Identity Of(string path)
        {
            using (SafeFileHandle handle = CreateFileW(path, ReadAttributes, ShareAll, IntPtr.Zero, OpenExisting, BackupSemantics | OpenReparsePoint, IntPtr.Zero))
            {
                if (handle.IsInvalid)
                {
                    throw new IOException("the file could not be opened to read its identity, error " + Marshal.GetLastWin32Error() + ": " + path);
                }
                FileInformation information;
                if (!GetFileInformationByHandle(handle, out information))
                {
                    throw new IOException("the file identity could not be read, error " + Marshal.GetLastWin32Error() + ": " + path);
                }
                return new Identity { VolumeSerial = information.VolumeSerial, Index = ((ulong)information.IndexHigh << 32) | information.IndexLow, Links = information.Links };
            }
        }

        // Null when the copy is a plain file of its own, else why not
        public static string CheckCopy(string copy, string original)
        {
            try
            {
                for (string folder = copy; !string.IsNullOrEmpty(folder); folder = Path.GetDirectoryName(folder))
                {
                    if (File.Exists(folder) || Directory.Exists(folder))
                    {
                        if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0)
                        {
                            return "the model copy or a folder above it is a link or junction: " + folder;
                        }
                    }
                }
                Identity mine = Of(copy);
                if (mine.Links != 1)
                {
                    return "the model copy has " + mine.Links + " hard links, a copy has one";
                }
                if (original != null && File.Exists(original))
                {
                    Identity theirs = Of(original);
                    if (theirs.VolumeSerial == mine.VolumeSerial && theirs.Index == mine.Index)
                    {
                        return "the model copy is the original file";
                    }
                }
                return null;
            }
            catch (Exception failure)
            {
                return "the model copy could not be checked: " + failure.Message;
            }
        }
    }
}
