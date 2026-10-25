"""R203. BasicFileInfo and the link references of a Revit file, read from its bytes with no Revit and no extra library.

A .rvt file is a compound file (CFB). This module finds the BasicFileInfo and TransmissionData streams in it, so the
format, build, worksharing and paths of a model are known before any window opens. The file is only read (R1).
"""
import argparse
import re
import struct
import sys
import xml.etree.ElementTree as ElementTree
from datetime import datetime, timedelta, timezone
from pathlib import Path

import common

SIGNATURE = bytes.fromhex("D0CF11E0A1B11AE1")
FREE, END, FAT_SECTOR, DIFAT_SECTOR = 0xFFFFFFFF, 0xFFFFFFFE, 0xFFFFFFFD, 0xFFFFFFFC
NO_STREAM = 0xFFFFFFFF
STREAM, ROOT = 2, 5
ENTRY_SIZE = 128
CLOSING = "</TransmissionData>"
# The keys of BasicFileInfo this tool reports. Other lines of the stream are kept out (R3)
INFO_KEYS = {
    "Worksharing": "worksharing",
    "Username": "username",
    "Central Model Path": "centralModelPath",
    "Format": "format",
    "Build": "build",
    "Last Save Path": "lastSavePath",
    "Unique Document GUID": "documentGuid",
    "Unique Document Increments": "documentIncrements",
}
REFERENCE_KEYS = {"ExternalFileReferenceType": "type", "LastSavedPath": "path", "LastSavedAbsolutePath": "absolutePath",
                  "LastSavedPathType": "pathType", "LastSavedLoadState": "loadState", "DesiredLoadState": "desiredLoadState"}


class CfbError(Exception):
    """The file is not a compound file this reader can read."""


class Compound:
    """The streams of one compound file, read on demand from its bytes."""

    def __init__(self, data):
        if data[:8] != SIGNATURE:
            raise CfbError("no compound file signature")
        self.data = data
        self.sector = 1 << struct.unpack_from("<H", data, 0x1E)[0]
        self.mini = 1 << struct.unpack_from("<H", data, 0x20)[0]
        self.cutoff = struct.unpack_from("<I", data, 0x38)[0]
        fat_count = struct.unpack_from("<I", data, 0x2C)[0]
        self.fat = self._read_fat(fat_count)
        self.entries = self._read_entries(struct.unpack_from("<I", data, 0x30)[0])
        root = next((entry for entry in self.entries if entry["type"] == ROOT), None)
        if root is None:
            raise CfbError("no root entry")
        self.mini_stream = self._chain_bytes(root["start"], root["size"]) if root["size"] else b""
        mini_fat_start, mini_fat_count = struct.unpack_from("<II", data, 0x3C)
        self.mini_fat = self._u32s(self._chain_bytes(mini_fat_start, None)) if mini_fat_count and mini_fat_start < FAT_SECTOR else []

    def _offset(self, sector):
        return (sector + 1) * self.sector

    def _u32s(self, raw):
        return list(struct.unpack("<%dI" % (len(raw) // 4), raw[: len(raw) // 4 * 4]))

    def _read_fat(self, fat_count):
        difat = list(struct.unpack_from("<109I", self.data, 0x4C))
        next_difat, difat_count = struct.unpack_from("<II", self.data, 0x44)
        per = self.sector // 4
        seen = 0
        while next_difat < FAT_SECTOR and seen < difat_count:
            words = self._u32s(self.data[self._offset(next_difat): self._offset(next_difat) + self.sector])
            difat.extend(words[: per - 1])
            next_difat = words[per - 1]
            seen += 1
        sectors = [sector for sector in difat if sector < FAT_SECTOR][:fat_count]
        fat = []
        for sector in sectors:
            fat.extend(self._u32s(self.data[self._offset(sector): self._offset(sector) + self.sector]))
        return fat

    def _chain(self, start, table):
        sectors, seen = [], set()
        current = start
        while current < FAT_SECTOR:
            if current in seen or current >= len(table):
                raise CfbError("a sector chain loops or leaves the table")
            seen.add(current)
            sectors.append(current)
            current = table[current]
        return sectors

    def _chain_bytes(self, start, size):
        raw = b"".join(self.data[self._offset(sector): self._offset(sector) + self.sector] for sector in self._chain(start, self.fat))
        return raw if size is None else raw[:size]

    def _read_entries(self, start):
        raw = self._chain_bytes(start, None)
        entries = []
        for at in range(0, len(raw) - ENTRY_SIZE + 1, ENTRY_SIZE):
            length = struct.unpack_from("<H", raw, at + 64)[0]
            kind = raw[at + 66]
            if kind == 0:
                continue
            name = raw[at: at + max(0, length - 2)].decode("utf-16-le", errors="replace")
            start_sector = struct.unpack_from("<I", raw, at + 116)[0]
            size = struct.unpack_from("<Q", raw, at + 120)[0]
            if self.sector == 512:
                size &= 0xFFFFFFFF
            created, modified = struct.unpack_from("<QQ", raw, at + 100)
            entries.append({"name": name, "type": kind, "start": start_sector, "size": size, "created": created, "modified": modified})
        return entries

    def names(self):
        return sorted(entry["name"] for entry in self.entries if entry["type"] == STREAM)

    def stream(self, name):
        """The bytes of the first stream of this name anywhere in the file, or None."""
        entry = next((entry for entry in self.entries if entry["type"] == STREAM and entry["name"] == name), None)
        if entry is None:
            return None
        if entry["size"] < self.cutoff:
            raw = b"".join(self.mini_stream[sector * self.mini: (sector + 1) * self.mini] for sector in self._chain(entry["start"], self.mini_fat))
            return raw[: entry["size"]]
        return self._chain_bytes(entry["start"], entry["size"])


def utf16_text(raw):
    """The readable UTF-16 text of a stream. BasicFileInfo starts with a few binary bytes, so both alignments are tried."""
    best = ""
    for start in (0, 1):
        text = raw[start:].decode("utf-16-le", errors="replace")
        if text.count("\n") + text.count(":") > best.count("\n") + best.count(":"):
            best = text
    return best


# A key starts a line, follows a stray character of the binary head, or follows the value of the key before it on the same line,
# as Revit writes Username, Build and Last Save Path of a BIM 360 cloud model. A capital, an apostrophe or a space before it means
# the words are part of a longer name, so Reformat or Central Model Path never give Format or Model Path
KEYS = re.compile(r"(?<![A-Z' ])(" + "|".join(re.escape(key) for key in INFO_KEYS) + r")\s*:")


def basic_info(raw):
    """R203. The keys of INFO_KEYS from the BasicFileInfo stream. Lines are Key: value, a value runs to the next key on its line,
    and the first one of each key counts."""
    found = {}
    for line in re.split(r"[\r\n\x00]+", utf16_text(raw)):
        matches = list(KEYS.finditer(line))
        if not matches:
            continue
        # The first key of a line starts the line or follows no ASCII letter, as before, so a stray character of the head may
        # stand before it and ReFormat is no key
        if matches[0].start() > 0 and "a" <= line[matches[0].start() - 1] <= "z":
            continue
        for at, match in enumerate(matches):
            end = matches[at + 1].start() if at + 1 < len(matches) else len(line)
            name = INFO_KEYS[match.group(1)]
            if name not in found:
                found[name] = line[match.end(): end].strip()
    if "format" in found:
        match = re.search(r"\d{4}", found["format"])
        found["formatYear"] = int(match.group(0)) if match else None
    text = found.get("worksharing", "")
    found["workshared"] = bool(text) and not text.lower().startswith("not enabled")
    return found


def link_references(raw):
    """R203. Every ExternalFileReference of the TransmissionData XML with its type, paths and load states, in file order."""
    if raw is None:
        return []
    text = utf16_text(raw)
    at = text.find("<?xml")
    if at < 0:
        raise CfbError("TransmissionData holds no XML")
    # The declaration names no encoding, so the text after it is parsed as it is. R265. A stream rewritten shorter keeps the
    # tail of its older XML after the closing tag, so the body ends at the first closing tag
    end = text.find(CLOSING, at)
    body = text[text.index("?>", at) + 2: end + len(CLOSING) if end >= 0 else text.rfind(">") + 1].strip()
    references = []
    for node in ElementTree.fromstring(body).iter("ExternalFileReference"):
        entry = {key: (node.findtext(tag) or "").strip() for tag, key in REFERENCE_KEYS.items()}
        entry["file"] = entry["path"].replace("\\", "/").rsplit("/", 1)[-1]
        references.append(entry)
    return references


def read(path):
    """R203. Format, build, worksharing and paths of one model and the files its TransmissionData names, read only."""
    path = Path(path)
    with open(path, "rb") as handle:
        data = handle.read()
    compound = Compound(data)
    raw = compound.stream("BasicFileInfo")
    if raw is None:
        raise CfbError("no BasicFileInfo stream")
    info = basic_info(raw)
    links = link_references(compound.stream("TransmissionData"))
    return {"file": path.name, "bytes": len(data), "info": info, "references": links,
            "revitLinks": [link for link in links if link["type"] == "Revit Link"], "storageTimes": storage_times(compound.entries)}


def filetime(value):
    """A Windows FILETIME, 100 ns steps since 1601, as UTC text to the minute, or None for 0."""
    if not value:
        return None
    return (datetime(1601, 1, 1, tzinfo=timezone.utc) + timedelta(microseconds=value // 10)).strftime("%Y-%m-%d %H:%M")


def storage_times(entries):
    """R266. The earliest time a storage of the file was written and the latest it was changed. Revit writes the whole compound
    file again on a save as, so the first is the save as that made the file and the second its last save."""
    created = [entry["created"] for entry in entries if entry.get("created")]
    modified = [entry["modified"] for entry in entries if entry.get("modified")]
    return {"firstWritten": filetime(min(created)) if created else None, "lastSaved": filetime(max(modified)) if modified else None}


def main(argv=None):
    parser = argparse.ArgumentParser(description="BasicFileInfo and link references of Revit files, from their bytes (R203).")
    parser.add_argument("files", nargs="+")
    parser.add_argument("--out", required=True, help="JSON file, outside the repo")
    args = parser.parse_args(argv)
    rows = []
    for name in args.files:
        try:
            rows.append(read(name))
        except (CfbError, OSError, struct.error) as error:
            rows.append({"file": Path(name).name, "error": str(error)})
    common.write_json(args.out, {"files": rows})
    for row in rows:
        info = row.get("info", {})
        print("%s format %s build %s workshared %s references %d Revit links %d%s" % (
            row["file"], info.get("formatYear"), info.get("build"), info.get("workshared"), len(row.get("references", [])), len(row.get("revitLinks", [])),
            " error " + row["error"] if "error" in row else ""))
    return 1 if any("error" in row for row in rows) else 0


if __name__ == "__main__":
    sys.exit(main())
