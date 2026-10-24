"""A small compound file (CFB version 3) writer for tests, so rvt_info.py is tested on dummy files built in code (R10).

Streams under the cutoff go to the mini stream, larger ones to their own sectors. One FAT sector, one directory chain.
"""
import struct

SECTOR = 512
MINI = 64
CUTOFF = 4096
FREE, END, FAT_SECTOR = 0xFFFFFFFF, 0xFFFFFFFE, 0xFFFFFFFD


def entry(name, kind, start, size, child=0xFFFFFFFF, right=0xFFFFFFFF, times=(0, 0)):
    raw = name.encode("utf-16-le") + b"\x00\x00"
    head = raw.ljust(64, b"\x00") + struct.pack("<HBB", len(raw), kind, 1) + struct.pack("<III", 0xFFFFFFFF, right, child)
    return head + b"\x00" * 16 + b"\x00" * 4 + struct.pack("<QQ", *times) + struct.pack("<IQ", start, size)


def sectors_of(data, size):
    return [data[at: at + size].ljust(size, b"\x00") for at in range(0, len(data), size)] or []


def build(streams, times=None):
    """Bytes of a compound file holding streams, a list of (name, bytes), all under the root. times gives an entry name its
    created and modified FILETIME, the root entry included."""
    times = times or {}
    small = [(name, data) for name, data in streams if len(data) < CUTOFF]
    large = [(name, data) for name, data in streams if len(data) >= CUTOFF]
    mini_fat, mini_data, mini_start = [], b"", {}
    for name, data in small:
        chunks = sectors_of(data, MINI)
        mini_start[name] = len(mini_fat) if chunks else END
        for k in range(len(chunks)):
            mini_fat.append(len(mini_fat) + 1 if k < len(chunks) - 1 else END)
        mini_data += b"".join(chunks)
    entries_count = 1 + len(streams)
    dir_sectors = (entries_count * 128 + SECTOR - 1) // SECTOR
    # Sector 0 is the FAT, then the directory, the mini FAT, the mini stream and the large streams
    fat = [FAT_SECTOR]
    body = []
    dir_start = len(fat)
    for k in range(dir_sectors):
        fat.append(len(fat) + 1 if k < dir_sectors - 1 else END)
    mini_fat_raw = struct.pack("<%dI" % len(mini_fat), *mini_fat) if mini_fat else b""
    mini_fat_sectors = sectors_of(mini_fat_raw, SECTOR)
    mini_fat_start = len(fat) if mini_fat_sectors else END
    for k in range(len(mini_fat_sectors)):
        fat.append(len(fat) + 1 if k < len(mini_fat_sectors) - 1 else END)
    mini_sectors = sectors_of(mini_data, SECTOR)
    root_start = len(fat) if mini_sectors else END
    for k in range(len(mini_sectors)):
        fat.append(len(fat) + 1 if k < len(mini_sectors) - 1 else END)
    large_start = {}
    large_sectors = []
    for name, data in large:
        chunks = sectors_of(data, SECTOR)
        large_start[name] = len(fat)
        for k in range(len(chunks)):
            fat.append(len(fat) + 1 if k < len(chunks) - 1 else END)
        large_sectors.extend(chunks)
    assert len(fat) <= SECTOR // 4, "the fixture holds one FAT sector only"
    fat += [FREE] * (SECTOR // 4 - len(fat))
    names = [name for name, _ in streams]
    directory = entry("Root Entry", 5, root_start, len(mini_data), child=1 if streams else 0xFFFFFFFF, times=times.get("Root Entry", (0, 0)))
    for k, (name, data) in enumerate(streams):
        start = mini_start[name] if len(data) < CUTOFF else large_start[name]
        directory += entry(name, 2, start, len(data), right=k + 2 if k + 1 < len(names) else 0xFFFFFFFF, times=times.get(name, (0, 0)))
    directory = directory.ljust(dir_sectors * SECTOR, b"\x00")
    header = bytes.fromhex("D0CF11E0A1B11AE1") + b"\x00" * 16 + struct.pack("<HHHHH", 0x3E, 3, 0xFFFE, 9, 6) + b"\x00" * 6
    header += struct.pack("<IIIIIIIII", 0, 1, dir_start, 0, CUTOFF, mini_fat_start, len(mini_fat_sectors), END, 0)
    header += struct.pack("<I", 0) + struct.pack("<108I", *([FREE] * 108))
    assert len(header) == SECTOR
    body = struct.pack("<128I", *fat) + directory + b"".join(mini_fat_sectors) + b"".join(mini_sectors) + b"".join(large_sectors)
    return header + body
