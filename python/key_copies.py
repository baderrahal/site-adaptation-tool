"""R312. The Revit 2024 copies of the answer keys, checked by code from answerKeys of config/project.json.

answerKeys names the copy folder under inputRoot, the manifest in it, the format every copy must have, the prefix of the site
upgrade job codes, the prefix of the key codes, the zone shape, the kinds, where a file name holds the code of its key and one
entry per answer key: its code, zone, kind, the source each copy is made from and the copy name. No project value sits here.
check holds each key against the manifest beside the copies and the files of today and reuses a copy only when the manifest
names it with the source of the config, the SHA-256 of that source today, the SHA-256 of the copy today and the format, read
from the bytes (R203). Any other key gets a request for a new siteUpgrade job (R204), and no copy is ever overwritten, moved
or deleted in place. No step files a copy a siteUpgrade job made: record is withdrawn after two attempts whose run proof could
be fooled (Q069), so a remake is done by hand. import writes the manifest once from the sources.txt of Round ADDIN Phase 1.
Sources are only read.
"""
WITHDRAWN = "no step of key_copies.py files a made copy, record is withdrawn (Q069)"
import argparse
import json
import os
import re
import struct
import sys
from pathlib import Path

import project_profile
import round_files
import rvt_info

BLOCK_FIELDS = ("folder", "manifest", "format", "jobPrefix", "codePrefix", "zonePattern", "kinds", "codeInName", "keys")
KEY_FIELDS = ("code", "zone", "kind", "source", "copy")
ENTRY_FIELDS = ("code", "copy", "copySha256", "source", "sourceSha256", "format", "madeBy")
# The shapes the settings themselves must have, no project value: one capital, capitals, a code of capitals and digits, a hash
PREFIX = re.compile(r"[A-Z]")
CAPITALS = re.compile(r"[A-Z]+")
ENTRY_CODE = re.compile(r"[A-Z][A-Z0-9]*")
HASH = re.compile(r"[0-9a-f]{64}")
CODE_MARK = "{code}"
# One line of the sources.txt Round ADDIN Phase 1 wrote: the copy, its SHA-256, its source under the root, its SHA-256, the run
OLD_LINE = re.compile(r"(?P<copy>[^\\/]+?) sha256 (?P<copyHash>[0-9a-f]{64}) from (?P<source>.+?) sha256 (?P<sourceHash>[0-9a-f]{64})(?: by (?P<by>.+))?")
MANIFEST_NOTE = ("R312. One entry per Revit 2024 copy of an answer key in this folder: the code, the copy, its SHA-256, the source "
                 "under the input root it was made from, the SHA-256 of that source when the copy was made, the format of the copy read "
                 "from its bytes and the run that made it. Written by python/key_copies.py, a copy is reused only while all of it holds")


class KeyCopyError(ValueError):
    """R312. An answer key list, a manifest or a step that would make a copy wrong."""


def plain_parts(text, what):
    """R312. The parts of a path under the input root written with \\ or /. A drive, a parent step, an empty part or a blank around a
    part is refused, so a path names one place under the root."""
    parts = re.split(r"[\\/]", text) if isinstance(text, str) and text else []
    if not parts or ":" in text or any(part in ("", ".", "..") or part != part.strip() for part in parts):
        raise KeyCopyError("%s is no plain path under the input root: %r" % (what, text))
    return tuple(parts)


def folded(parts):
    """R312. Parts folded, since Windows matches names in any case."""
    return tuple(part.casefold() for part in parts)


def opened_parts(text, what):
    """R312. The parts of a plain path under the input root as Windows opens them, as GetFullPathNameW gives them: a part before the
    last that ends in one period loses it, and the last part loses every period and blank at its end. Either slash splits, and the
    refusals of plain_parts come first, so no drive, parent step, . part or empty part is left to resolve. A last part that loses
    everything names the folder above it and is refused."""
    parts = list(plain_parts(text, what))
    for index, part in enumerate(parts[:-1]):
        if part.endswith(".") and not part.endswith(".."):
            parts[index] = part[:-1]
    parts[-1] = parts[-1].rstrip(". ")
    if not parts[-1]:
        raise KeyCopyError("%s names the folder above its last part, Windows takes that part away: %r" % (what, text))
    return tuple(parts)


def place(text, what):
    """R312. The place a path under the input root opens, its opened parts folded, so two texts of one place compare equal."""
    return folded(opened_parts(text, what))


def name_key(text, what):
    """R312. The place of a plain file name with no folder, the key of a copy in the manifest and in every compare."""
    plain_name(text, what)
    return place(text, what)[0]


def plain_name(text, what):
    """R312. A file name with no folder, by the rules of plain_parts."""
    if len(plain_parts(text, what)) != 1:
        raise KeyCopyError("%s is a file name with no folder: %r" % (what, text))
    return text


def pattern(text, what):
    """R312. A regular expression of the settings, refused when it is no text or does not compile."""
    if not isinstance(text, str) or not text:
        raise KeyCopyError("%s is no pattern: %r" % (what, text))
    try:
        return re.compile(text)
    except re.error as error:
        raise KeyCopyError("%s does not compile: %r, %s" % (what, text, error))


def name_pattern(values, code):
    """R312. The pattern codeInName of answerKeys with the code of a key in place of {code}, matched in any case as Windows matches
    names. The file name of the copy and of the source of the key must hold its code there."""
    return re.compile(values["codeInName"].replace(CODE_MARK, re.escape(code)), re.IGNORECASE)


def load_keys(values):
    """R312. The answerKeys block of the project settings, every key whole: a code of codePrefix and one capital, a zone of
    zonePattern, a kind of kinds, a source under the input root outside the copy folder and a .rvt copy name, the copy name and the
    source file name each holding the code of its own key where codeInName says and no code of another key. Paths are compared by
    the place Windows opens, so a code, a copy name or a source twice, or a source in the copy folder, is refused whatever its case,
    slashes or trailing dots and blanks."""
    if not isinstance(values, dict) or set(values) != set(BLOCK_FIELDS):
        raise KeyCopyError("answerKeys holds exactly %s" % ", ".join(BLOCK_FIELDS))
    folder = place(values["folder"], "the copy folder")
    plain_name(values["manifest"], "the manifest")
    if not isinstance(values["format"], int) or isinstance(values["format"], bool):
        raise KeyCopyError("the format of the copies is a year: %r" % values["format"])
    if not isinstance(values["jobPrefix"], str) or not PREFIX.fullmatch(values["jobPrefix"]):
        raise KeyCopyError("the job prefix is one capital letter: %r" % values["jobPrefix"])
    if not isinstance(values["codePrefix"], str) or not CAPITALS.fullmatch(values["codePrefix"]):
        raise KeyCopyError("the code prefix is capitals: %r" % values["codePrefix"])
    code_shape = re.compile(re.escape(values["codePrefix"]) + "[A-Z]")
    zone = pattern(values["zonePattern"], "the zone pattern")
    kinds = values["kinds"]
    if not isinstance(kinds, list) or not kinds or not all(isinstance(kind, str) and CAPITALS.fullmatch(kind) for kind in kinds) or len(set(kinds)) != len(kinds):
        raise KeyCopyError("the kinds are a list of capitals, each once: %r" % kinds)
    in_name = values["codeInName"]
    if not isinstance(in_name, str) or in_name.count(CODE_MARK) != 1:
        raise KeyCopyError("codeInName holds %s once: %r" % (CODE_MARK, in_name))
    pattern(in_name.replace(CODE_MARK, "X"), "codeInName")
    keys = values["keys"]
    if not isinstance(keys, list) or not keys:
        raise KeyCopyError("answerKeys lists no key")
    seen = {"code": set(), "copy": set(), "source": set()}
    for key in keys:
        if not isinstance(key, dict) or set(key) != set(KEY_FIELDS):
            raise KeyCopyError("an answer key holds exactly %s: %r" % (", ".join(KEY_FIELDS), key))
        if not isinstance(key["code"], str) or not code_shape.fullmatch(key["code"]):
            raise KeyCopyError("the code of an answer key is %s and a capital: %r" % (values["codePrefix"], key))
        if not isinstance(key["zone"], str) or not zone.fullmatch(key["zone"]):
            raise KeyCopyError("the zone of an answer key is not of the zone pattern %s: %r" % (values["zonePattern"], key))
        if key["kind"] not in kinds:
            raise KeyCopyError("the kind of an answer key is %s: %r" % (" or ".join(kinds), key))
        if not plain_name(key["copy"], "a copy name").lower().endswith(".rvt"):
            raise KeyCopyError("a copy name is a .rvt file: %r" % key)
        source = place(key["source"], "a source")
        if source[:len(folder)] == folder:
            raise KeyCopyError("a source sits in the copy folder, among the copies: %r" % key)
        for field, value in (("code", key["code"]), ("copy", name_key(key["copy"], "a copy name")), ("source", source)):
            if value in seen[field]:
                raise KeyCopyError("the %s of an answer key is twice in the list: %r" % (field, key))
            seen[field].add(value)
    # A swap of two names between their keys is refused, so a copy is never checked against the source of another key
    for key in keys:
        for what, name in (("copy name", key["copy"]), ("source file name", opened_parts(key["source"], "a source")[-1])):
            if not name_pattern(values, key["code"]).search(name):
                raise KeyCopyError("the %s %s does not hold the code %s of its key where codeInName says: %s" % (what, name, key["code"], in_name))
            for other in keys:
                if other is not key and name_pattern(values, other["code"]).search(name):
                    raise KeyCopyError("the %s %s of the key %s holds the code %s of another key" % (what, name, key["code"], other["code"]))
    return values


def entry_faults(entry):
    """R312. Why one manifest entry is not whole, [] when it is: exactly the entry fields, a code of capitals and digits, two SHA-256
    in lower case, a format year, a run that made it, a copy that is a plain file name and a source that is a plain path under the
    root. The code of a key is compared by check, which lists an entry of no key."""
    if not isinstance(entry, dict) or set(entry) != set(ENTRY_FIELDS):
        return ["it holds not exactly %s" % ", ".join(ENTRY_FIELDS)]
    faults = []
    if not isinstance(entry["code"], str) or not ENTRY_CODE.fullmatch(entry["code"]):
        faults.append("its code is no code of capitals and digits")
    for field in ("copySha256", "sourceSha256"):
        if not isinstance(entry[field], str) or not HASH.fullmatch(entry[field]):
            faults.append("its %s is no SHA-256 in lower case" % field)
    if not isinstance(entry["format"], int) or isinstance(entry["format"], bool):
        faults.append("it has no format year, the copy may be no Revit file")
    if not isinstance(entry["madeBy"], str) or not entry["madeBy"].strip():
        faults.append("it names no run that made the copy")
    for check_text, field in ((name_key, "copy"), (place, "source")):
        try:
            check_text(entry[field], "its %s" % field)
        except KeyCopyError as error:
            faults.append(str(error))
    return faults


def manifest_faults(entries):
    """R312. Every reason a list of entries can not be a manifest, each with the copy it is about: the faults of each entry and a
    copy listed twice by the place Windows opens. Both read_manifest and import take these rules."""
    faults = []
    seen = set()
    for number, entry in enumerate(entries, start=1):
        name = entry.get("copy") if isinstance(entry, dict) else None
        faults.extend("entry %d (%s): %s" % (number, name, fault) for fault in entry_faults(entry))
        try:
            copy = name_key(name, "its copy")
        except KeyCopyError:
            continue
        if copy in seen:
            faults.append("entry %d (%s): the copy is twice in the list" % (number, name))
        seen.add(copy)
    return faults


def read_manifest(path):
    """R312. The entries of a manifest by the place of their copy name, its name folded as Windows opens it, {} when there is none. A
    manifest that is no JSON or holds no list of copies, or whose entries have a fault of manifest_faults, is refused with every
    fault."""
    path = Path(path)
    if not path.is_file():
        return {}
    try:
        data = json.loads(path.read_text(encoding="utf-8-sig"))
    except ValueError as error:
        raise KeyCopyError("the manifest %s is no JSON: %s" % (path, error))
    copies = data.get("copies") if isinstance(data, dict) else None
    if not isinstance(copies, list):
        raise KeyCopyError("the manifest %s holds no list of copies" % path)
    faults = manifest_faults(copies)
    if faults:
        raise KeyCopyError("the manifest %s is refused:\n%s" % (path, "\n".join(faults)))
    return {name_key(entry["copy"], "the copy of an entry"): entry for entry in copies}


def write_manifest(path, entries):
    """R312. The manifest as JSON, written next to it first and then put in place by a rename, so a run that stops never leaves half
    a file. A write or a rename that fails takes the temporary file away again and leaves the manifest as it was."""
    path = Path(path)
    temporary = path.with_name(path.name + ".writing")
    try:
        temporary.write_text(json.dumps({"note": MANIFEST_NOTE, "copies": entries}, indent=1), encoding="utf-8")
        os.replace(temporary, path)
    except BaseException:
        temporary.unlink(missing_ok=True)
        raise


def format_of(path):
    """R203. The format year of a model from its bytes, None when the file is no compound file with a BasicFileInfo, so such a copy
    is never reused and the check goes on with the other keys."""
    try:
        return rvt_info.read(path)["info"].get("formatYear")
    except (rvt_info.CfbError, struct.error):
        return None


def copy_folder(root, values):
    """R312. The copy folder of answerKeys under the input root, as Windows opens it."""
    return Path(root).joinpath(*opened_parts(values["folder"], "the copy folder"))


def source_path(root, key):
    """R312. The source of a key under the input root, the file its copy is made from, as Windows opens it."""
    return Path(root).joinpath(*opened_parts(key["source"], "a source"))


def job_code(values, key):
    """R312. The code of the siteUpgrade job of a key, jobPrefix and the code of the key after codePrefix, as KA to KG in Round ADDIN
    Phase 1."""
    return values["jobPrefix"] + key["code"][len(values["codePrefix"]):]


def made_name(source, code):
    """R204. The name a siteUpgrade job gives the copy it makes of a source in the work folder."""
    return Path(source).stem + "_TEST-" + code + ".rvt"


def request(root, values, key, work):
    """R312. What a new copy needs: the source copied into the work folder and the siteUpgrade job of R204 on it. No step of this
    tool files the copy it makes, record is withdrawn (Q069), so the note says a remake is done by hand."""
    source = source_path(root, key)
    code = job_code(values, key)
    return {"copyFrom": str(source), "spec": {"kind": "siteUpgrade", "code": code, "source": str(Path(work) / source.name)},
            "made": str(Path(work) / made_name(source, code)),
            "note": "copy the source into the work folder by a file copy and run the siteUpgrade job under the window lock. Then " + WITHDRAWN
                    + ", so the made copy is checked and filed by hand. A copy in the copy folder is never overwritten, move it aside by hand "
                    "before the new one goes in"}


def check(root, values, work, hasher=round_files.sha256, info=format_of):
    """R312. One row per key: reuse when the manifest names the copy with the code and the source of the config, the hash of that
    source today, the hash of the copy today and the format, all read again now. make when there is neither a copy nor an entry,
    noSource when the source is not there, else remake with its reasons. Then one row noKey per manifest entry of no key. Nothing
    is written."""
    folder = copy_folder(root, values)
    manifest = read_manifest(folder / values["manifest"])
    rows = []
    for key in values["keys"]:
        source = source_path(root, key)
        copy = folder / key["copy"]
        entry = manifest.get(name_key(key["copy"], "a copy name"))
        row = {"code": key["code"], "copy": key["copy"], "source": key["source"], "verdict": None, "reasons": [], "format": None,
               "sourceSha256": None, "copySha256": None, "request": None}
        if not source.is_file():
            row["verdict"] = "noSource"
            rows.append(row)
            continue
        row["sourceSha256"] = hasher(source)
        there = copy.is_file()
        if there:
            row["copySha256"] = hasher(copy)
            row["format"] = info(copy)
        if not there and entry is None:
            row["verdict"] = "make"
        else:
            reasons = []
            if entry is None:
                reasons.append("notInManifest")
            else:
                if entry["code"] != key["code"]:
                    reasons.append("otherCode")
                if place(entry["source"], "a source") != place(key["source"], "a source"):
                    reasons.append("otherSource")
                elif entry["sourceSha256"] != row["sourceSha256"]:
                    reasons.append("sourceChanged")
                if not there:
                    reasons.append("copyGone")
                elif entry["copySha256"] != row["copySha256"]:
                    reasons.append("copyChanged")
            if there and (row["format"] != values["format"] or (entry is not None and entry["format"] != values["format"])):
                reasons.append("notFormat")
            row["reasons"] = reasons
            row["verdict"] = "remake" if reasons else "reuse"
        if row["verdict"] != "reuse":
            row["request"] = request(root, values, key, work)
        rows.append(row)
    # An entry no key names is listed, never dropped in silence, and asks for nothing
    named = {name_key(key["copy"], "a copy name") for key in values["keys"]}
    for name, entry in manifest.items():
        if name not in named:
            rows.append({"code": entry["code"], "copy": entry["copy"], "source": entry["source"], "verdict": "noKey", "reasons": [], "format": None,
                         "sourceSha256": None, "copySha256": None, "request": None})
    # A Revit file in the copy folder that no key and no entry names, right in it or in a folder inside it, is listed too
    for relative in unlisted(folder, named | set(manifest)):
        rows.append({"code": None, "copy": relative, "source": None, "verdict": "notListed", "reasons": [], "format": None,
                     "sourceSha256": None, "copySha256": None, "request": None})
    return rows


def unlisted(folder, named):
    """R312. Every .rvt file in the copy folder, in any case and also in a folder inside it, that is not a file right in it whose
    name folded is in named, by its path relative to the folder with /."""
    found = []
    if Path(folder).is_dir():
        for path in sorted(Path(folder).rglob("*")):
            relative = path.relative_to(folder)
            if path.is_file() and path.suffix.casefold() == ".rvt" and not (len(relative.parts) == 1 and path.name.casefold() in named):
                found.append(relative.as_posix())
    return found


def import_sources(root, values, sources_txt, hasher=round_files.sha256, info=format_of):
    """R312. Writes the manifest once from the sources.txt of Round ADDIN Phase 1: per line its copy, the hash of the copy today, which
    must equal the line, the source and its hash as the line wrote them, the format today and the run the line names. Every entry is
    held to manifest_faults, the rules read_manifest takes, before anything is written. A line that names no copy, a line of no
    key, a copy changed since its line, any fault of an entry or a manifest already there refuses the whole import with every
    reason, and nothing is written."""
    folder = copy_folder(root, values)
    path = folder / values["manifest"]
    if path.exists():
        raise KeyCopyError("the manifest %s is already there, an import never overwrites it" % path)
    keys = {name_key(key["copy"], "a copy name"): key for key in values["keys"]}
    entries = []
    faults = []
    for number, line in enumerate(Path(sources_txt).read_text(encoding="utf-8-sig").splitlines(), start=1):
        if not line.strip():
            continue
        match = OLD_LINE.fullmatch(line.strip())
        if not match:
            faults.append("line %d names no copy with its source" % number)
            continue
        try:
            key = keys.get(name_key(match.group("copy"), "the copy of line %d" % number))
        except KeyCopyError as error:
            faults.append(str(error))
            continue
        if key is None:
            faults.append("line %d names a copy of no key: %s" % (number, match.group("copy")))
            continue
        copy = folder / key["copy"]
        if not copy.is_file() or hasher(copy) != match.group("copyHash"):
            faults.append("line %d: the copy %s is gone or changed since the line" % (number, key["copy"]))
            continue
        entries.append({"code": key["code"], "copy": key["copy"], "copySha256": match.group("copyHash"), "source": match.group("source"),
                        "sourceSha256": match.group("sourceHash"), "format": info(copy), "madeBy": match.group("by") or "line %d of %s" % (number, Path(sources_txt).name)})
    faults.extend(manifest_faults(entries))
    if not entries and not faults:
        faults.append("it names no copy")
    if faults:
        raise KeyCopyError("the import of %s is refused, nothing is written:\n%s" % (sources_txt, "\n".join(faults)))
    write_manifest(path, entries)
    return entries


def main(argv=None):
    """R312. check or import on the answer keys of the project settings, each step with what it needs as required arguments, so a
    step with one missing ends at the command line before anything is read or changed. check ends 0 only when every row is reuse,
    and when a key asks for a new copy it says that no step files the made copy, since record is withdrawn (Q069)."""
    parser = argparse.ArgumentParser(description="The Revit 2024 copies of the answer keys, checked by code (R312).")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    steps = parser.add_subparsers(dest="what", required=True)
    check_step = steps.add_parser("check", help="hold every key against the manifest and the files of today, write nothing")
    check_step.add_argument("--out", help="a JSON file of the rows, outside the repo")
    import_step = steps.add_parser("import", help="write the manifest once from the sources.txt of Round ADDIN Phase 1")
    import_step.add_argument("--sources", required=True, help="the sources.txt of Round ADDIN Phase 1")
    args = parser.parse_args(argv)
    settings = project_profile.load_settings(args.settings)
    root = Path(settings.get("inputRoot"))
    values = load_keys(settings.get("answerKeys"))
    work = Path(settings.get("siteBuild.folder")) / "work"
    if args.what == "import":
        for entry in import_sources(root, values, args.sources):
            print("%s %s written from %s" % (entry["code"], entry["copy"], entry["source"]))
        return 0
    rows = check(root, values, work)
    for row in rows:
        print("%s %s %s%s" % (row["code"], row["copy"], row["verdict"], " " + ", ".join(row["reasons"]) if row["reasons"] else ""))
    if args.out:
        Path(args.out).write_text(json.dumps({"rows": rows}, indent=1), encoding="utf-8")
    print("reuse %d of %d" % (sum(row["verdict"] == "reuse" for row in rows), len(rows)))
    if any(row["request"] for row in rows):
        print("A new copy is asked for, and " + WITHDRAWN + ". Make it by hand and file it by hand")
    return 0 if all(row["verdict"] == "reuse" for row in rows) else 1


if __name__ == "__main__":
    sys.exit(main())
