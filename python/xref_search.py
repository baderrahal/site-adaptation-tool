"""The xrefs of every drawing of the data book folders and a search for each file, Part X of round DATA-5 (R252).

Each DWG copy under dataBook.root/work in the folders of dataBook.data5.folders is read by CadReader with an empty layer
list into the xrefs folder of data5, once, and its xref list and xref inserts are taken from layers.json and blocks.json.
Each xref file name is then looked for under every root of dataBook.data5.xrefs.searchRoots by its name only, no file is
opened. A file with the same name is found, a file whose stem is close to it by difflib is listed as near and never taken
as found. Rows go to xrefs.csv in the xrefs folder, counts to xrefs.json there.
"""
import argparse
import csv
import difflib
import json
import os
import subprocess
import sys
from pathlib import Path, PureWindowsPath

import c3d_run

REPO = Path(__file__).resolve().parent.parent


def copies(work, folders):
    """Every DWG copy under the work folders, in name order."""
    found = []
    for folder in folders:
        found.extend(sorted(p for p in (Path(work) / folder).rglob("*") if p.suffix.lower() == ".dwg"))
    return found


def read_copy(reader, copy, out, layers):
    """CadReader once per copy, an earlier read of this round kept. The error text when it fails."""
    target = Path(out) / copy.stem
    if (target / "layers.json").is_file():
        return None
    done = subprocess.run([str(reader), str(copy), str(target), "--layers", str(layers)], capture_output=True, text=True, timeout=1800)
    return None if done.returncode == 0 else (done.stderr.strip() or "exit %d" % done.returncode)


def xrefs_of(folder):
    """The xrefs of a CadReader output with the inserts of each in blocks.json."""
    layers = json.loads((Path(folder) / "layers.json").read_text(encoding="utf-8"))
    blocks_path = Path(folder) / "blocks.json"
    blocks = json.loads(blocks_path.read_text(encoding="utf-8"))["blocks"] if blocks_path.is_file() else []
    found = []
    for entry in layers.get("xrefs") or []:
        inserts = [b for b in blocks if b.get("note") == "xref" and b["name"] == entry["name"]]
        found.append({"name": entry["name"], "path": entry.get("path"), "foundNextToHost": entry.get("found"), "inserts": entry.get("inserts"),
                      "insertRows": [{"point": b["insertPoint"], "scale": b["scale"], "rotationDeg": b["rotationDeg"]} for b in inserts]})
    return found


def file_name(path, name):
    """The file name an xref path asks for, a Windows path read the same way on any system."""
    text = path or name + ".dwg"
    leaf = PureWindowsPath(text).name
    return leaf if leaf.lower().endswith(".dwg") else leaf + ".dwg"


def search(wanted, roots, near_ratio):
    """Files under the roots with the wanted name, and files with a close stem, by name only."""
    wanted_lower = wanted.lower()
    stem = os.path.splitext(wanted_lower)[0]
    same, near = [], []
    for root in roots:
        for folder, _, names in os.walk(root):
            for name in names:
                lower = name.lower()
                if lower == wanted_lower:
                    same.append(os.path.join(folder, name))
                elif os.path.splitext(lower)[1] == ".dwg":
                    ratio = difflib.SequenceMatcher(None, stem, os.path.splitext(lower)[0]).ratio()
                    if ratio >= near_ratio:
                        near.append({"file": os.path.join(folder, name), "ratio": round(ratio, 3)})
    return same, sorted(near, key=lambda n: -n["ratio"])


def run(config):
    book = config["dataBook"]
    settings = book["data5"]
    xref = settings["xrefs"]
    root = Path(book["root"])
    out = root / settings["outFolder"] / xref["outFolder"]
    out.mkdir(parents=True, exist_ok=True)
    layers = out / "empty_layers.json"
    layers.write_text(json.dumps({"keep": [], "drop": []}), encoding="utf-8")
    rows, errors, read = [], {}, []
    for copy in copies(root / "work", settings["folders"]):
        error = read_copy(REPO / book["cadReader"], copy, out, layers)
        if error:
            errors[str(copy.relative_to(root / "work"))] = error
            continue
        read.append(copy)
        for entry in xrefs_of(out / copy.stem):
            wanted = file_name(entry["path"], entry["name"])
            same, near = search(wanted, xref["searchRoots"], xref["nearRatio"])
            rows.append({"drawing": str(copy.relative_to(root / "work")), "xref": entry["name"], "path": entry["path"], "file": wanted,
                         "inserts": entry["inserts"], "insertRows": entry["insertRows"], "found": same, "near": near})
    report = {"drawings": len(read), "errors": errors, "xrefs": len(rows), "notFound": sum(1 for r in rows if not r["found"]),
              "rows": rows}
    with open(out / "xrefs.csv", "w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle, lineterminator="\n")
        writer.writerow(["drawing", "xref", "path", "inserts", "found", "near"])
        for r in rows:
            writer.writerow([r["drawing"], r["xref"], r["path"], r["inserts"], "|".join(r["found"]), "|".join(n["file"] for n in r["near"])])
    (out / "xrefs.json").write_text(json.dumps(report, indent=1), encoding="utf-8")
    return report


def main(argv=None):
    parser = argparse.ArgumentParser(description="The xrefs of the data book drawings and a search for each by file name (R252)")
    parser.add_argument("--config", default=str(c3d_run.CONFIG))
    args = parser.parse_args(argv)
    report = run(c3d_run.load_config(args.config))
    print(json.dumps({k: report[k] for k in ("drawings", "errors", "xrefs", "notFound")}, indent=1))
    for r in report["rows"]:
        print(r["drawing"], "|", r["xref"], "|", r["inserts"], "inserts | found", len(r["found"]), "| near", [n["file"] for n in r["near"]][:3])
    return 0


if __name__ == "__main__":
    sys.exit(main())
