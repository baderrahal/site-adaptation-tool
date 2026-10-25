"""Where an IM code appears and which neighbourhoods its files hold, Part A of round DATA-5 (R257).

An IM code is IM and one more capital letter, standing alone between other characters that are not letters. It is looked
for in three places, by text only and with no Revit file opened: the file names under the source root, the texts of the
location plans and of every data book table (the PDF copies read with pdfplumber, texts.csv of each drawing folder), and
the link names and paths of the past models earlier rounds already read (their prototypes.json and the BasicFileInfo
record of R203). Then the plots of each file of dataBook.data5.imCodes.sources are tied to the area key of R256 by plot
number, a schedule by the plots.csv rows it wired, a past plan by the official number of each plot, so each code gets the
neighbourhoods of the plots its files hold, with counts. The IM letter is only the name of the evidence, the area of a plot
is never read from it. Counts go to im_codes.json in the area_key folder of data5. Since Round ADDIN config/area_codes.json
holds one table per job (R301) and each source names its job, parsons or native.
"""
import argparse
import csv
import json
import os
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path

import c3d_run
import data_book_analysis

REPO = Path(__file__).resolve().parent.parent
CODE =re.compile(r"(?<![A-Za-z])(IM[A-Z])(?![A-Za-z])")


def codes_in(text):
    return CODE.findall(text or "")


def file_names(root):
    """Every file under root whose name holds an IM code: code, folder relative to root and name."""
    found = []
    for folder, _, names in os.walk(root):
        for name in names:
            for code in set(codes_in(name)):
                found.append({"code": code, "folder": os.path.relpath(folder, root), "file": name})
    return found


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


JOBS = ("parsons", "native")
FILE_KINDS = ("model", "schedule")


def measured_count(value):
    return isinstance(value, int) and not isinstance(value, bool) and value > 0


def check_job(job, rows):
    """R301. The rows of one job, refused when a row has no IM code, a status, no areas, an IM code twice, or an area with no
    neighbourhood, zone, positive plot count, file kind, file or the place it was measured. A neighbourhood two files hold is
    allowed, 01A is in two Native models."""
    if not isinstance(rows, list):
        raise ValueError("area codes of %s are not a list of rows" % job)
    seen = Counter(row.get("code") for row in rows if isinstance(row, dict))
    for row in rows:
        if not isinstance(row, dict) or not CODE.fullmatch(row.get("code") or "") or "status" in row:
            raise ValueError("area code row of %s is not an IM code with measured areas: %s" % (job, row))
        if seen[row["code"]] > 1:
            raise ValueError("area code %s is twice in the table of %s" % (row["code"], job))
        areas = row.get("areas")
        if not isinstance(areas, list) or not areas:
            raise ValueError("area code %s of %s holds no area" % (row["code"], job))
        for area in areas:
            if not (isinstance(area, dict) and area.get("nbh") and area.get("zone") and measured_count(area.get("plots"))
                    and area.get("kind") in FILE_KINDS and area.get("file") and area.get("measured")):
                raise ValueError("area of %s in %s is not measured with its count and source: %s" % (row["code"], job, area))
    return rows


def load_table(path):
    """R301. config/area_codes.json as {job: rows}, one table per job of JOBS, every row measured (check_job)."""
    data = json.loads(Path(path).read_text(encoding="utf-8"))
    jobs = data.get("jobs") if isinstance(data, dict) else None
    if not isinstance(jobs, dict) or set(jobs) != set(JOBS):
        raise ValueError("area codes hold one table per job, %s" % ", ".join(JOBS))
    return {job: check_job(job, jobs[job]) for job in JOBS}


def table_against_sources(table, sources):
    """R301. Each source against the row of its code in the table of its own job: agrees when every keyed plot is in a
    neighbourhood of the row, differs otherwise, noRow when the job has no row of the code, noJob when the table has no such job."""
    found = []
    for source in sources:
        rows = {row["code"]: row for row in table.get(source.get("job")) or []}
        for code in source["codes"]:
            row = rows.get(code)
            nbhs = {key.split(" ")[-1] for key in source["byZoneAndNbh"] if key != "noKey"}
            if source.get("job") not in table:
                verdict = "noJob"
            elif row is None:
                verdict = "noRow"
            elif nbhs <= {area["nbh"] for area in row["areas"]}:
                verdict = "agrees"
            else:
                verdict = "differs"
            found.append({"source": source["name"], "job": source.get("job"), "code": code, "nbh": sorted(nbhs), "verdict": verdict})
    return found


def run(config):
    book = config["dataBook"]
    settings = book["data5"]["imCodes"]
    root = Path(book["root"])
    out = root / book["data5"]["outFolder"] / book["data5"]["areaKey"]["outFolder"]
    out.mkdir(parents=True, exist_ok=True)
    names = file_names(config["sourceRoot"])
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
    by_code = defaultdict(lambda: {"fileNames": 0, "folders": Counter(), "texts": Counter(), "sources": []})
    for entry in names:
        by_code[entry["code"]]["fileNames"] += 1
        by_code[entry["code"]]["folders"][entry["folder"]] += 1
    for entry in texts:
        by_code[entry["code"]]["texts"][entry["where"]] += 1
    for source in sources:
        for code in source["codes"]:
            by_code[code]["sources"].append(source["name"])
    report = {"codes": {code: {"fileNames": v["fileNames"], "folders": dict(v["folders"]), "texts": dict(v["texts"]), "sources": v["sources"]}
                        for code, v in sorted(by_code.items())},
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
