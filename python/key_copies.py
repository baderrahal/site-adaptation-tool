"""R312. The Revit 2024 copies of the answer keys, checked or made by code from answerKeys of config/project.json.

answerKeys names the copy folder under inputRoot, the manifest in it, the format every copy must have, the prefix of the site
upgrade job codes and one entry per answer key: its IM code, zone, kind, the source each copy is made from and the copy name.
check holds each key against the manifest beside the copies and the files of today and reuses a copy only when the manifest
names it with the source of the config, the SHA-256 of that source today, the SHA-256 of the copy today and the format, read
from the bytes (R203). Any other key gets a request for a new siteUpgrade job (R204), and no copy is ever overwritten, moved
or deleted in place. record moves a copy a siteUpgrade job made in the work folder into the copy folder, never over a file,
and writes its entry. import writes the manifest once from the sources.txt of Round ADDIN Phase 1. Sources are only read.
"""
import argparse
import json
import os
import re
import shutil
import struct
import sys
from datetime import datetime, timedelta, timezone
from pathlib import Path

import project_profile
import round_files
import rvt_info

BLOCK_FIELDS = ("folder", "manifest", "format", "jobPrefix", "keys")
KEY_FIELDS = ("code", "zone", "kind", "source", "copy")
KINDS = ("TU", "SE")
ENTRY_FIELDS = ("code", "copy", "copySha256", "source", "sourceSha256", "format", "madeBy")
CODE = re.compile(r"IM[A-Z]")
ZONE = re.compile(r"Zone [1-9][0-9]*")
PREFIX = re.compile(r"[A-Z]")
HASH = re.compile(r"[0-9a-f]{64}")
EPOCH = datetime(1970, 1, 1, tzinfo=timezone.utc)
# One line of the sources.txt Round ADDIN Phase 1 wrote: the copy, its SHA-256, its source under the root, its SHA-256, the run
OLD_LINE = re.compile(r"(?P<copy>[^\\/]+?) sha256 (?P<copyHash>[0-9a-f]{64}) from (?P<source>.+?) sha256 (?P<sourceHash>[0-9a-f]{64})(?: by (?P<by>.+))?")
MANIFEST_NOTE = ("R312. One entry per Revit 2024 copy of an answer key in this folder: the IM code, the copy, its SHA-256, the source "
                 "under the input root it was made from, the SHA-256 of that source when the copy was made, the format of the copy read "
                 "from its bytes and the run that made it. Written by python/key_copies.py, a copy is reused only while all of it holds")


class KeyCopyError(ValueError):
    """An answer key list, a manifest or a step that would make a copy wrong."""


def plain_parts(text, what):
    """The parts of a path under the input root written with \\ or /. A drive, a parent step, an empty part or a blank around a part
    is refused, so a path names one place under the root."""
    parts = re.split(r"[\\/]", text) if isinstance(text, str) and text else []
    if not parts or ":" in text or any(part in ("", ".", "..") or part != part.strip() for part in parts):
        raise KeyCopyError("%s is no plain path under the input root: %r" % (what, text))
    return tuple(parts)


def folded(parts):
    """Windows matches names in any case, so parts are compared folded."""
    return tuple(part.casefold() for part in parts)


def plain_name(text, what):
    if len(plain_parts(text, what)) != 1:
        raise KeyCopyError("%s is a file name with no folder: %r" % (what, text))
    return text


def load_keys(values):
    """R312. The answerKeys block of the project settings, every key whole: an IM code, a zone, the kind TU or SE, a source under the
    input root outside the copy folder and a .rvt copy name. A code, a copy name or a source twice, in any case, is refused."""
    if not isinstance(values, dict) or set(values) != set(BLOCK_FIELDS):
        raise KeyCopyError("answerKeys holds exactly %s" % ", ".join(BLOCK_FIELDS))
    folder = plain_parts(values["folder"], "the copy folder")
    plain_name(values["manifest"], "the manifest")
    if not isinstance(values["format"], int) or isinstance(values["format"], bool):
        raise KeyCopyError("the format of the copies is a year: %r" % values["format"])
    if not isinstance(values["jobPrefix"], str) or not PREFIX.fullmatch(values["jobPrefix"]):
        raise KeyCopyError("the job prefix is one capital letter: %r" % values["jobPrefix"])
    keys = values["keys"]
    if not isinstance(keys, list) or not keys:
        raise KeyCopyError("answerKeys lists no key")
    seen = {"code": set(), "copy": set(), "source": set()}
    for key in keys:
        if not isinstance(key, dict) or set(key) != set(KEY_FIELDS):
            raise KeyCopyError("an answer key holds exactly %s: %r" % (", ".join(KEY_FIELDS), key))
        if not isinstance(key["code"], str) or not CODE.fullmatch(key["code"]):
            raise KeyCopyError("the code of an answer key is IM and a capital: %r" % key)
        if not isinstance(key["zone"], str) or not ZONE.fullmatch(key["zone"]):
            raise KeyCopyError("the zone of an answer key is Zone <n>: %r" % key)
        if key["kind"] not in KINDS:
            raise KeyCopyError("the kind of an answer key is %s: %r" % (" or ".join(KINDS), key))
        if not plain_name(key["copy"], "a copy name").lower().endswith(".rvt"):
            raise KeyCopyError("a copy name is a .rvt file: %r" % key)
        source = folded(plain_parts(key["source"], "a source"))
        if source[:len(folder)] == folded(folder):
            raise KeyCopyError("a source sits in the copy folder, among the copies: %r" % key)
        for field, value in (("code", key["code"]), ("copy", key["copy"].casefold()), ("source", source)):
            if value in seen[field]:
                raise KeyCopyError("the %s of an answer key is twice in the list: %r" % (field, key))
            seen[field].add(value)
    return values


def entry_ok(entry):
    return (isinstance(entry, dict) and set(entry) == set(ENTRY_FIELDS) and isinstance(entry["code"], str) and CODE.fullmatch(entry["code"])
            and all(isinstance(entry[field], str) and HASH.fullmatch(entry[field]) for field in ("copySha256", "sourceSha256"))
            and isinstance(entry["format"], int) and not isinstance(entry["format"], bool)
            and isinstance(entry["madeBy"], str) and bool(entry["madeBy"].strip()))


def read_manifest(path):
    """R312. The entries of a manifest by copy name folded, {} when there is none. A manifest that is no JSON, an entry that is not
    whole, a copy that is no plain file name, a source that is no plain path under the root or a copy twice is refused."""
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
    found = {}
    for entry in copies:
        if not entry_ok(entry):
            raise KeyCopyError("a manifest entry is not whole: %r" % entry)
        plain_name(entry["copy"], "the copy of a manifest entry")
        plain_parts(entry["source"], "the source of a manifest entry")
        if entry["copy"].casefold() in found:
            raise KeyCopyError("the copy %s is twice in the manifest" % entry["copy"])
        found[entry["copy"].casefold()] = entry
    return found


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
    return Path(root).joinpath(*plain_parts(values["folder"], "the copy folder"))


def source_path(root, key):
    return Path(root).joinpath(*plain_parts(key["source"], "a source"))


def job_code(values, key):
    """The code of the siteUpgrade job of a key, the prefix and the letter of its IM code, as KA to KG in Round ADDIN Phase 1."""
    return values["jobPrefix"] + key["code"][2:]


def made_name(source, code):
    """R204. The name a siteUpgrade job gives the copy it makes of a source in the work folder."""
    return Path(source).stem + "_TEST-" + code + ".rvt"


def request(root, values, key, work):
    """R312. What a new copy needs: the source copied into the work folder, the siteUpgrade job of R204 on it and then record."""
    source = source_path(root, key)
    code = job_code(values, key)
    return {"copyFrom": str(source), "spec": {"kind": "siteUpgrade", "code": code, "source": str(Path(work) / source.name)},
            "made": str(Path(work) / made_name(source, code)),
            "note": "copy the source into the work folder by a file copy, run the siteUpgrade job under the window lock, then key_copies.py "
                    "record. A copy in the copy folder is never overwritten, move it aside by hand before the new one goes in"}


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
        entry = manifest.get(key["copy"].casefold())
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
                if folded(plain_parts(entry["source"], "a source")) != folded(plain_parts(key["source"], "a source")):
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
    named = {key["copy"].casefold() for key in values["keys"]}
    for name, entry in manifest.items():
        if name not in named:
            rows.append({"code": entry["code"], "copy": entry["copy"], "source": entry["source"], "verdict": "noKey", "reasons": [], "format": None,
                         "sourceSha256": None, "copySha256": None, "request": None})
    return rows


def import_sources(root, values, sources_txt, hasher=round_files.sha256, info=format_of):
    """R312. Writes the manifest once from the sources.txt of Round ADDIN Phase 1: per line its copy, the hash of the copy today, which
    must equal the line, the source and its hash as the line wrote them, the format today and the run the line names. A line of no
    key, a copy changed since the line or a manifest already there is refused and nothing is written."""
    folder = copy_folder(root, values)
    path = folder / values["manifest"]
    if path.exists():
        raise KeyCopyError("the manifest %s is already there, an import never overwrites it" % path)
    keys = {key["copy"].casefold(): key for key in values["keys"]}
    entries = []
    for number, line in enumerate(Path(sources_txt).read_text(encoding="utf-8-sig").splitlines(), start=1):
        if not line.strip():
            continue
        match = OLD_LINE.fullmatch(line.strip())
        if not match:
            raise KeyCopyError("line %d of %s names no copy with its source" % (number, sources_txt))
        key = keys.get(match.group("copy").casefold())
        if key is None:
            raise KeyCopyError("line %d of %s names a copy of no key: %s" % (number, sources_txt, match.group("copy")))
        copy = folder / key["copy"]
        if not copy.is_file() or hasher(copy) != match.group("copyHash"):
            raise KeyCopyError("the copy %s is gone or changed since line %d of %s" % (key["copy"], number, sources_txt))
        entries.append({"code": key["code"], "copy": key["copy"], "copySha256": match.group("copyHash"), "source": match.group("source"),
                        "sourceSha256": match.group("sourceHash"), "format": info(copy), "madeBy": match.group("by") or "line %d of %s" % (number, Path(sources_txt).name)})
    if not entries:
        raise KeyCopyError("%s names no copy" % sources_txt)
    write_manifest(path, entries)
    return entries


def utc_time(text, what):
    """R312. A UTC time of run.json as Run-TestHost.ps1 writes it, round trip with Z, as nanoseconds since 1970."""
    try:
        moment = datetime.fromisoformat(text)
    except (TypeError, ValueError):
        raise KeyCopyError("%s is no time: %r" % (what, text))
    if moment.utcoffset() is None:
        raise KeyCopyError("%s has no UTC offset: %r" % (what, text))
    return (moment - EPOCH) // timedelta(microseconds=1) * 1000


def written_ns(path):
    """R312. The latest of the times a file was made and last written, in nanoseconds. A file copy on Windows keeps the last write
    time of its source and gets a new creation time, so the creation time counts too."""
    found = os.stat(path)
    made = getattr(found, "st_birthtime_ns", None)
    return max(found.st_mtime_ns, found.st_ctime_ns if made is None else made)


def same_place(one, two):
    """R312. Two full paths of one file, in the case and the form Windows opens them."""
    return os.path.normcase(os.path.abspath(one)) == os.path.normcase(os.path.abspath(two))


def read_json(path, what):
    try:
        return json.loads(Path(path).read_text(encoding="utf-8-sig"))
    except (OSError, ValueError) as error:
        raise KeyCopyError("%s can not be read: %s" % (what, error))


def run_proof(run, code, work_copy, made):
    """R312. That the siteUpgrade job of that code made this copy from this work copy in this run, from what the run left by R110:
    run.json, testhost_<code>_upgrade_run.json, ended with exit code 0, no stop and the log ok, and the log beside it of the same name
    is the siteUpgrade log of that code, ended ok, with that work copy as source and that copy as target. run.json holds no source or
    target, the log does. The run started after the work copy was last made or written and the made copy was last written inside
    the run, so a copy an earlier job left in the work folder is never taken for a copy of a work copy put there later."""
    run = Path(run)
    name = code + "_upgrade"
    if run.name.casefold() != ("testhost_%s_run.json" % name).casefold():
        raise KeyCopyError("%s is not the run of the siteUpgrade job %s, testhost_%s_run.json" % (run, code, name))
    facts = read_json(run, "the run %s" % run)
    log_path = run.with_name("testhost_%s_log.json" % name)
    log = read_json(log_path, "the log %s" % log_path)
    if not isinstance(facts, dict) or not isinstance(log, dict):
        raise KeyCopyError("the run %s or its log is no JSON object" % run)
    if facts.get("name") != name:
        raise KeyCopyError("%s is not the run of the siteUpgrade job %s: %r" % (run, code, facts.get("name")))
    if facts.get("exitCode") != 0 or facts.get("stopReason") is not None or facts.get("logResult") != "ok" or log.get("result") != "ok":
        raise KeyCopyError("the run %s did not end ok: exit code %r, stop %r, log %r and %r" % (run, facts.get("exitCode"), facts.get("stopReason"),
                                                                                       facts.get("logResult"), log.get("result")))
    if log.get("kind") != "siteUpgrade" or log.get("code") != code:
        raise KeyCopyError("%s is not the log of the siteUpgrade job %s: %r %r" % (log_path, code, log.get("kind"), log.get("code")))
    for field, path in (("source", work_copy), ("target", made)):
        if not isinstance(log.get(field), str) or not same_place(log[field], path):
            raise KeyCopyError("the log %s names another %s than %s: %r" % (log_path, field, path, log.get(field)))
    start, end = utc_time(facts.get("startUtc"), "the start of the run"), utc_time(facts.get("endUtc"), "the end of the run")
    if written_ns(work_copy) >= start:
        raise KeyCopyError("the run %s started before the work copy %s was last made or written, so it did not make the copy from it" % (run, work_copy))
    if not start <= os.stat(made).st_mtime_ns <= end:
        raise KeyCopyError("the made copy %s was not written in that run, %s to %s" % (made, facts.get("startUtc"), facts.get("endUtc")))


def record(root, values, work, code, made, made_by, run, hasher=round_files.sha256, info=format_of):
    """R312. Moves the copy a siteUpgrade job made of the source of a key into the copy folder under the copy name and writes its entry.
    The copy must sit right in the work folder with the name of R204, its run must prove by run_proof that it made the copy from the
    work copy of the source beside it, that work copy must still equal the source by hash, the copy must have the format, the manifest
    must read whole and no file of the copy name may be in the copy folder, so nothing is ever overwritten. All of it is checked
    before the move. The manifest is written by a temporary file and a rename, and when that fails the copy goes back to the work
    folder, so a failure leaves both folders as they were."""
    key = next((key for key in values["keys"] if key["code"] == code), None)
    if key is None:
        raise KeyCopyError("no answer key %s" % code)
    if not isinstance(made_by, str) or not made_by.strip():
        raise KeyCopyError("record names the run that made the copy")
    made, work = Path(made), Path(work)
    if made.parent.resolve() != work.resolve() or not made.is_file():
        raise KeyCopyError("the made copy is not a file right in the work folder %s: %s" % (work, made))
    source = source_path(root, key)
    job = job_code(values, key)
    if made.name != made_name(source, job):
        raise KeyCopyError("the made copy is not named %s: %s" % (made_name(source, job), made.name))
    work_copy = work / source.name
    if not work_copy.is_file():
        raise KeyCopyError("the work copy %s is not there" % work_copy)
    run_proof(run, job, work_copy, made)
    source_hash = hasher(source)
    if hasher(work_copy) != source_hash:
        raise KeyCopyError("the work copy %s is not the source %s by hash" % (work_copy, source))
    if info(made) != values["format"]:
        raise KeyCopyError("the made copy is not of the format %d: %s" % (values["format"], made))
    folder = copy_folder(root, values)
    manifest_path = folder / values["manifest"]
    manifest = read_manifest(manifest_path)
    if any(path.name.casefold() == key["copy"].casefold() for path in folder.iterdir()):
        raise KeyCopyError("the copy %s is in %s, it is never overwritten, move it aside by hand first" % (key["copy"], folder))
    entry = {"code": key["code"], "copy": key["copy"], "copySha256": hasher(made), "source": key["source"], "sourceSha256": source_hash,
             "format": values["format"], "madeBy": made_by}
    manifest[key["copy"].casefold()] = entry
    target = folder / key["copy"]
    shutil.move(str(made), str(target))
    try:
        if hasher(target) != entry["copySha256"]:
            raise KeyCopyError("the copy %s changed in the move" % target)
        write_manifest(manifest_path, list(manifest.values()))
    except BaseException:
        shutil.move(str(target), str(made))
        raise
    return entry


def main(argv=None):
    """R312. check, import or record on the answer keys of the project settings. Each step names what it needs as required
    arguments, so a step with one missing ends at the command line before anything is read or changed. check ends 0 only when
    every row is reuse."""
    parser = argparse.ArgumentParser(description="The Revit 2024 copies of the answer keys, checked or made by code (R312).")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    steps = parser.add_subparsers(dest="what", required=True)
    check_step = steps.add_parser("check", help="hold every key against the manifest and the files of today, write nothing")
    check_step.add_argument("--out", help="a JSON file of the rows, outside the repo")
    import_step = steps.add_parser("import", help="write the manifest once from the sources.txt of Round ADDIN Phase 1")
    import_step.add_argument("--sources", required=True, help="the sources.txt of Round ADDIN Phase 1")
    record_step = steps.add_parser("record", help="move a copy a siteUpgrade job made into the copy folder and write its entry")
    record_step.add_argument("--code", required=True, help="the code of the key")
    record_step.add_argument("--made", required=True, help="the copy the siteUpgrade job made in the work folder")
    record_step.add_argument("--run", required=True, help="testhost_<job code>_upgrade_run.json of that job, its log beside it")
    record_step.add_argument("--by", required=True, help="the run that made it, written into the manifest")
    args = parser.parse_args(argv)
    settings = project_profile.load_settings(args.settings)
    root = Path(settings.get("inputRoot"))
    values = load_keys(settings.get("answerKeys"))
    work = Path(settings.get("siteBuild.folder")) / "work"
    if args.what == "import":
        for entry in import_sources(root, values, args.sources):
            print("%s %s written from %s" % (entry["code"], entry["copy"], entry["source"]))
        return 0
    if args.what == "record":
        entry = record(root, values, work, args.code, args.made, args.by, args.run)
        print("%s %s recorded, format %d, from %s" % (entry["code"], entry["copy"], entry["format"], entry["source"]))
        return 0
    rows = check(root, values, work)
    for row in rows:
        print("%s %s %s%s" % (row["code"], row["copy"], row["verdict"], " " + ", ".join(row["reasons"]) if row["reasons"] else ""))
    if args.out:
        Path(args.out).write_text(json.dumps({"rows": rows}, indent=1), encoding="utf-8")
    print("reuse %d of %d" % (sum(row["verdict"] == "reuse" for row in rows), len(rows)))
    return 0 if all(row["verdict"] == "reuse" for row in rows) else 1


if __name__ == "__main__":
    sys.exit(main())
