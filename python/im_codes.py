"""Where an IM code appears and which neighbourhoods its files hold, Part A of round DATA-5 (R257).

An IM code is IM and one more capital letter, standing alone between other characters that are not letters. It is looked
for in three places, by text only and with no Revit file opened: the file names under the source root, the texts of the
location plans and of every data book table (the PDF copies read with pdfplumber, texts.csv of each drawing folder), and
the link names and paths of the past models earlier rounds already read (their prototypes.json and the BasicFileInfo
record of R203). Then the plots of each file of dataBook.data5.imCodes.sources are tied to the area key of R256 by plot
number, a schedule by the plots.csv rows it wired, a past plan by the official number of each plot, so each code gets the
neighbourhoods of the plots its files hold, with counts. The IM letter is only the name of the evidence, the area of a plot
is never read from it. Counts go to im_codes.json in the area_key folder of data5. Since Round ADDIN config/area_codes.json
holds one table per job of majority.groups (R301) and each source names its job.
"""
import argparse
import csv
import json
import os
import re
import sys
from collections import Counter, defaultdict, namedtuple
from pathlib import Path

import c3d_run
import data_book_analysis
import project_profile

REPO = Path(__file__).resolve().parent.parent
CODE =re.compile(r"(?<![A-Za-z])(IM[A-Z])(?![A-Za-z])")


def codes_in(text):
    return CODE.findall(text or "")


COPY_FIELDS = ("folder", "manifest", "reason")
CopyFolder = namedtuple("CopyFolder", "folder key manifest")
# One line of the manifest of a copy folder, as Round ADDIN Phase 1 wrote sources.txt: the copy, right in the folder, its SHA-256,
# the source relative to the source root and its SHA-256, then any text
MANIFEST_LINE = re.compile(r"(?P<copy>[^\\/]+?) sha256 [0-9a-f]{64} from (?P<source>.+?) sha256 [0-9a-f]{64}(?: |$)")


def path_parts(text, what):
    """R301. The parts of a path under the source root written with / or \\. A drive, a parent step, an empty part or a blank around
    a part is refused, since Windows drops such blanks and the path must name one place."""
    parts = text.replace("\\", "/").split("/") if isinstance(text, str) else []
    if not parts or ":" in text or any(part in ("", ".", "..") or part != part.strip() for part in parts):
        raise ValueError("%s is a path under the source root with no drive, no parent step and no blank around a part: %r" % (what, text))
    return tuple(parts)


def name_key(parts):
    """Windows matches names in any case, so names are compared folded."""
    return tuple(part.casefold() for part in parts)


def copy_folders(entries):
    """R301. The folders under the source root that hold copies this tool made, each named in the config with the manifest right in
    it and its reason, as folders relative to the root with / between their parts. A folder outside the root, with a parent step,
    a blank around a part, empty, with no reason, a manifest that is no plain file name, or two folders one inside the other is
    refused, so a copy is never counted apart in silence."""
    folders = []
    for entry in entries or []:
        if not isinstance(entry, dict) or set(entry) != set(COPY_FIELDS):
            raise ValueError("a copy folder holds exactly %s: %r" % (", ".join(COPY_FIELDS), entry))
        if not isinstance(entry["reason"], str) or not entry["reason"].strip():
            raise ValueError("a copy folder names its reason: %r" % entry)
        parts = path_parts(entry["folder"], "a copy folder")
        if len(path_parts(entry["manifest"], "the manifest of a copy folder")) != 1:
            raise ValueError("the manifest of a copy folder is a file right in it: %r" % entry)
        key = name_key(parts)
        for other in folders:
            if key[:len(other.key)] == other.key or other.key[:len(key)] == key:
                raise ValueError("a copy folder sits inside another: %r and %r" % (other.folder, "/".join(parts)))
        folders.append(CopyFolder("/".join(parts), key, entry["manifest"]))
    return tuple(folders)


def read_manifest(path, copy):
    """R301. The copies the manifest of a copy folder on disk names, by file name folded, each with its source. The manifest is found
    in any case and must be there, a line that names no copy right in the folder with its source under the root is refused, and a
    copy folder that holds a source its manifest names is listed too high, since a source has no copy entry, and is refused."""
    names = [name for name in os.listdir(path) if name.casefold() == copy.manifest.casefold() and os.path.isfile(os.path.join(path, name))]
    if len(names) != 1:
        raise ValueError("the copy folder %s holds no manifest %s, so it may be listed too high and no file in it counts as a copy" % (copy.folder, copy.manifest))
    copies = {}
    for number, line in enumerate(Path(path, names[0]).read_text(encoding="utf-8-sig").splitlines(), start=1):
        if not line.strip():
            continue
        match = MANIFEST_LINE.match(line)
        if not match:
            raise ValueError("line %d of the manifest of %s names no copy with its source" % (number, copy.folder))
        source = path_parts(match.group("source"), "the source of a copy")
        if name_key(source)[:len(copy.key)] == copy.key:
            raise ValueError("the copy folder %s is listed too high: it holds %s, a source its manifest names" % (copy.folder, "/".join(source)))
        copies[match.group("copy").casefold()] = "/".join(source)
    return copies


def file_names(root, copies=()):
    """Every file under root whose name holds an IM code: code, folder relative to root and name. A file inside a copy folder of
    copy_folders that the manifest of that folder names as a copy carries the folder as copyFolder, so it is counted apart from
    the source files. Any other file there, also in a folder inside it, stays a source file and carries the folder as notInManifest
    (R301). A copy folder that is not under root is refused."""
    found = []
    manifests = {}
    for folder, _, names in os.walk(root):
        relative = os.path.relpath(folder, root)
        parts = () if relative == os.curdir else Path(relative).parts
        copy = next((c for c in copies if name_key(parts[:len(c.key)]) == c.key), None)
        listed = {}
        if copy:
            top = os.path.join(root, *parts[:len(copy.key)])
            if top not in manifests:
                manifests[top] = read_manifest(top, copy)
            listed = manifests[top] if len(parts) == len(copy.key) else {}
        for name in names:
            for code in set(codes_in(name)):
                entry = {"code": code, "folder": relative, "file": name}
                if copy:
                    entry["copyFolder" if name.casefold() in listed else "notInManifest"] = copy.folder
                found.append(entry)
    seen = {name_key(Path(os.path.relpath(top, root)).parts) for top in manifests}
    missing = [copy.folder for copy in copies if copy.key not in seen]
    if missing:
        raise ValueError("copy folders not under the source root: %s" % ", ".join(missing))
    return found


def name_counts(names):
    """R301. Per IM code the source files and their folders with those in a copy folder its manifest does not name, and apart from
    them the copies and their copy folders."""
    counts = defaultdict(lambda: {"fileNames": 0, "folders": Counter(), "copyFileNames": 0, "copyFolders": Counter(), "notInManifest": 0})
    for entry in names:
        row = counts[entry["code"]]
        if entry.get("copyFolder"):
            row["copyFileNames"] += 1
            row["copyFolders"][entry["copyFolder"]] += 1
        else:
            row["fileNames"] += 1
            row["folders"][entry["folder"]] += 1
            row["notInManifest"] += 1 if entry.get("notInManifest") else 0
    return {code: {"fileNames": v["fileNames"], "folders": dict(v["folders"]), "copyFileNames": v["copyFileNames"], "copyFolders": dict(v["copyFolders"]),
                   "notInManifest": v["notInManifest"]}
            for code, v in counts.items()}


def in_texts(texts, where, context=50):
    """Each IM code in a list of texts, with a little text around it."""
    found = []
    for text in texts:
        for match in CODE.finditer(text or ""):
            found.append({"code": match.group(1), "where": where, "context": text[max(0, match.start() - context): match.end() + context].replace("\n", " ")})
    return found


def pdf_texts(path):
    """The characters of each page in content order. extract_text sorts the rotated title block into other lines, where the
    IMG of the Rev.09 location plan is lost, so the plain stream is read and extract_text is not."""
    import pdfplumber
    with pdfplumber.open(path) as pdf:
        return ["".join(c["text"] for c in page.chars) for page in pdf.pages]


def table_texts(folder):
    path = Path(folder) / "texts.csv"
    if not path.is_file():
        return []
    csv.field_size_limit(1 << 30)
    with open(path, encoding="utf-8") as handle:
        return [row.get("text") or "" for row in csv.DictReader(handle)]


def model_texts(path):
    """Link names and paths of a past model record: the file and raw name of each link, every path of a file info record."""
    data = json.loads(Path(path).read_text(encoding="utf-8"))
    texts = []
    for document in data.get("documents") or []:
        texts.append(document.get("file") or "")
        texts.extend((link.get("rawName") or link.get("file") or "") for link in document.get("links") or [])
    for entry in data.get("files") or []:
        texts.append(entry.get("file") or "")
        info = entry.get("info") or {}
        texts.extend([info.get("centralModelPath") or "", info.get("lastSavePath") or ""])
        texts.extend((ref.get("path") or "") + " " + (ref.get("absolutePath") or "") for ref in entry.get("references") or [])
    return texts


def plots_of(source, plots):
    """The plots a source file holds: baseplan labels for a schedule, official numbers for a past plan."""
    if source["kind"] == "schedule":
        return "label", [r["baseplanLabel"] for r in plots if r.get("scheduleFile") == source["file"]]
    plan = json.loads(Path(source["path"]).read_text(encoding="utf-8"))
    return "number", [str(entry["plot"]) for entry in plan["plots"]]


def nbh_counts(keys, key_of):
    found = Counter()
    for key in keys:
        value = key_of.get(key)
        found["%s %s" % value if value else "noKey"] += 1
    return dict(found.most_common())


FILE_KINDS = ("model", "schedule")


def job_names(settings):
    """R301. The jobs of the area code table, the groups of majority.groups in the project settings, so no job name sits in code."""
    groups = settings.get("majority.groups")
    if not isinstance(groups, dict) or not groups:
        raise ValueError("majority.groups of the project settings names no job")
    return tuple(groups)


def measured_count(value):
    """R301. A plot count of an area is a whole number above 0, never a flag, a text or a share."""
    return isinstance(value, int) and not isinstance(value, bool) and value > 0


# R301. The fields of a row and of an area. A row holds its code and areas and may name its sub-phase, an area holds exactly
# these, so a field that is no measurement, such as a guess, has no place to go
ROW_FIELDS = ("code", "areas")
ROW_OPTIONAL = ("subPhase",)
AREA_FIELDS = ("nbh", "zone", "plots", "kind", "file", "measured")


# R301. The forms of the names. NBH_FORM is the Neighbourhood_ID of the Rev 9 DLUS, two or three digits and at most one capital
# letter, as 02, 012 and 01A, ZONE_FORM the zone as area_key.zone_text writes it, SUB_PHASE_FORM a Native sub-phase as 1.1
NBH_FORM = re.compile(r"[0-9]{2,3}[A-Z]?")
ZONE_FORM = re.compile(r"Zone [1-9][0-9]*")
SUB_PHASE_FORM = re.compile(r"[0-9]+\.[0-9]+")


def text_value(value):
    return isinstance(value, str) and bool(value.strip())


def of_form(value, form):
    """A string of that form with nothing around it, a blank or another type is not."""
    return isinstance(value, str) and form.fullmatch(value) is not None


def measured_area(area):
    """R301. An area is measured when it holds exactly AREA_FIELDS: a neighbourhood of NBH_FORM, a zone of ZONE_FORM, a plot
    count, the kind of file it was counted from, the file and where the count was measured. Anything else is no measured area."""
    return (isinstance(area, dict) and set(area) == set(AREA_FIELDS) and of_form(area["nbh"], NBH_FORM) and of_form(area["zone"], ZONE_FORM)
            and measured_count(area["plots"]) and area["kind"] in FILE_KINDS and text_value(area["file"]) and text_value(area["measured"]))


def good_row(row):
    """R301. A row holds an IM code, a list of areas and at most a sub-phase of SUB_PHASE_FORM, nothing else."""
    return (isinstance(row, dict) and set(ROW_FIELDS) <= set(row) <= set(ROW_FIELDS + ROW_OPTIONAL) and of_form(row["code"], CODE)
            and ("subPhase" not in row or of_form(row["subPhase"], SUB_PHASE_FORM)))


def check_job(job, rows):
    """R301. The rows of one job. Every row is checked whole first (good_row), so a code of another type is refused as a
    ValueError, then an IM code is in one row only and every area is measured (measured_area). A neighbourhood two files hold
    is allowed, 01A is in two Native models."""
    if not isinstance(rows, list):
        raise ValueError("area codes of %s are not a list of rows" % job)
    for row in rows:
        if not good_row(row):
            raise ValueError("area code row of %s is not an IM code with measured areas: %s" % (job, row))
    seen = Counter(row["code"] for row in rows)
    for row in rows:
        if seen[row["code"]] > 1:
            raise ValueError("area code %s is twice in the table of %s" % (row["code"], job))
        areas = row["areas"]
        if not isinstance(areas, list) or not areas:
            raise ValueError("area code %s of %s holds no area" % (row["code"], job))
        for area in areas:
            if not measured_area(area):
                raise ValueError("area of %s in %s is not measured with its count and source: %s" % (row["code"], job, area))
    return rows


def load_table(path, settings=None):
    """R301. config/area_codes.json as {job: rows}, one table per job of the project settings (job_names), every row measured
    (check_job). settings are those the caller loaded, the repo config when None."""
    names = job_names(settings if settings is not None else project_profile.load_settings())
    data = json.loads(Path(path).read_text(encoding="utf-8"))
    jobs = data.get("jobs") if isinstance(data, dict) else None
    if not isinstance(jobs, dict) or set(jobs) != set(names):
        raise ValueError("area codes hold one table per job, %s" % ", ".join(names))
    return {job: check_job(job, jobs[job]) for job in names}


def zone_and_nbh(key):
    """The zone and the neighbourhood of a key of nbh_counts, Zone 2 012 as (Zone 2, 012)."""
    zone, _, nbh = key.rpartition(" ")
    return zone, nbh


def table_against_sources(table, sources):
    """R301. Each source against the row of its code in the table of its own job, by the zone and neighbourhood pairs first:
    differs when a keyed plot is in a pair the row does not hold, whatever the unkeyed count, else mostlyUnkeyed when its unkeyed
    plots outnumber its keyed plots, so a few keyed plots never speak for the source, else agrees. noKeyedPlot when the area key
    keyed none of its plots, noRow when the job has no row of the code, noJob when the table has no such job. Each verdict
    carries the keyed and unkeyed plot counts."""
    found = []
    for source in sources:
        rows = {row["code"]: row for row in table.get(source.get("job")) or []}
        counts = source["byZoneAndNbh"]
        keyed = sum(count for key, count in counts.items() if key != "noKey")
        unkeyed = counts.get("noKey", 0)
        for code in source["codes"]:
            row = rows.get(code)
            pairs = {zone_and_nbh(key) for key in counts if key != "noKey"}
            if source.get("job") not in table:
                verdict = "noJob"
            elif row is None:
                verdict = "noRow"
            elif not pairs:
                verdict = "noKeyedPlot"
            elif not pairs <= {(area["zone"], area["nbh"]) for area in row["areas"]}:
                verdict = "differs"
            elif unkeyed > keyed:
                verdict = "mostlyUnkeyed"
            else:
                verdict = "agrees"
            found.append({"source": source["name"], "job": source.get("job"), "code": code, "nbh": sorted(nbh for _, nbh in pairs),
                          "keyedPlots": keyed, "unkeyedPlots": unkeyed, "verdict": verdict})
    return found


def run(config):
    book = config["dataBook"]
    settings = book["data5"]["imCodes"]
    root = Path(book["root"])
    out = root / book["data5"]["outFolder"] / book["data5"]["areaKey"]["outFolder"]
    out.mkdir(parents=True, exist_ok=True)
    names = file_names(config["sourceRoot"], copy_folders(settings.get("copyFolders")))
    texts = []
    for pdf in settings["pdfs"]:
        texts += in_texts(pdf_texts(root / "work" / pdf), pdf)
    for folder in sorted(p for p in root.iterdir() if p.is_dir()):
        texts += in_texts(table_texts(folder), folder.name + "/texts.csv")
    for path in settings["modelRecords"]:
        texts += in_texts(model_texts(path), Path(path).name)
    plots = data_book_analysis.read_csv(root / "plots.csv")
    key_of = {"label": {}, "number": {}}
    for row in data_book_analysis.read_csv(out / "area_key.csv"):
        if row["nbhId"] or row["zone"]:
            key_of["label"][row["baseplanLabel"]] = (row["zone"], row["nbhId"])
            for number in row["numbers"].split("|"):
                key_of["number"].setdefault(number, (row["zone"], row["nbhId"]))
    sources = []
    for source in settings["sources"]:
        kind, keys = plots_of(source, plots)
        sources.append({"name": source["name"], "file": source["file"], "job": source["job"], "codes": sorted(set(codes_in(source["file"]))), "plots": len(keys),
                        "byZoneAndNbh": nbh_counts(keys, key_of[kind])})
    by_code = defaultdict(lambda: {"fileNames": 0, "folders": {}, "copyFileNames": 0, "copyFolders": {}, "notInManifest": 0, "texts": Counter(), "sources": []})
    for code, counts in name_counts(names).items():
        by_code[code].update(counts)
    for entry in texts:
        by_code[entry["code"]]["texts"][entry["where"]] += 1
    for source in sources:
        for code in source["codes"]:
            by_code[code]["sources"].append(source["name"])
    report = {"codes": {code: {"fileNames": v["fileNames"], "folders": v["folders"], "copyFileNames": v["copyFileNames"], "copyFolders": v["copyFolders"],
                               "notInManifest": v["notInManifest"], "texts": dict(v["texts"]), "sources": v["sources"]}
                        for code, v in sorted(by_code.items())},
              "notInManifest": [{"code": e["code"], "folder": e["folder"], "file": e["file"]} for e in names if e.get("notInManifest")],
              "sources": sources, "tableAgainstSources": table_against_sources(load_table(REPO / settings["table"]), sources), "textContexts": sorted({(t["code"], t["where"], t["context"]) for t in texts})[: settings["contextsKept"]]}
    (out / "im_codes.json").write_text(json.dumps(report, indent=1), encoding="utf-8")
    return report


def main(argv=None):
    parser = argparse.ArgumentParser(description="Where each IM code appears and the neighbourhoods its files hold (R257)")
    parser.add_argument("--config", default=str(c3d_run.CONFIG))
    args = parser.parse_args(argv)
    report = run(c3d_run.load_config(args.config))
    print(json.dumps({"codes": report["codes"], "sources": report["sources"], "tableAgainstSources": report["tableAgainstSources"]}, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
