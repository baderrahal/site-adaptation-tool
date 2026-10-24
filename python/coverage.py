"""The coverage table of the four source folders, Part T of round DATA-5 (R259).

One row per file under the folders of dataBook.data5.folders (the source names, Rev.09 renamed as the data book does): the
reader of its data book tables from their data.json (C3DProbe for the full Civil 3D read, CadReader for the stand-in), or
the reader dataBook.data5.coverage.readers names for a workbook or a PDF, the coordinate space (site when rows are kept
inside Phase 1, own when every row lies outside, page fitted to site for a PDF the location plan or Rev 9 fit reads, none
for a workbook), the rows kept inside Phase 1, the Civil 3D entity objects the DWG header counts and those the probe
read, and the note of coverage.notes on what is still not read and why. Only counts are written. Rows go to coverage.json
and coverage.md in the data5 folder.
"""
import argparse
import json
import subprocess
import sys
import tempfile
from pathlib import Path

import c3d_run

REPO = Path(__file__).resolve().parent.parent


def civil_classes(reader, copy):
    """Civil 3D entity classes with instances in a DWG header (R241), by class."""
    with tempfile.TemporaryDirectory() as folder:
        done = subprocess.run([str(reader), str(copy), folder, "--header-only"], capture_output=True, text=True, timeout=600)
        if done.returncode != 0:
            return None
        header = json.loads((Path(folder) / "header.json").read_text(encoding="utf-8"))
    return {c["dxfName"]: c["instanceCount"] for c in header.get("classes") or [] if c["dxfName"].startswith("AECC") and c.get("isAnEntity") and c["instanceCount"]}


def kept(counts):
    """Rows kept inside Phase 1 by table, kept and keptCut together."""
    return {table: values.get("kept", 0) + values.get("keptCut", 0) for table, values in (counts or {}).items()
            if table in ("entities", "blocks", "texts", "surfaces")}


def probe_civil(counts):
    found = {k: v for k, v in ((counts or {}).get("civil") or {}).items() if k != "drawing"}
    if (counts or {}).get("surfaces"):
        found["surfacesKept"] = counts["surfaces"].get("kept", 0)
    return found


def space_of(rows_kept, kind, data):
    if kind == "workbook":
        return "none"
    if kind == "pdf":
        return "page fitted to site"
    if data is None:
        return "not read"
    return "site" if any(rows_kept.values()) else "own"


def row_of(file, kind, data, readers, civil, note):
    rows_kept = kept((data or {}).get("counts"))
    return {"file": file, "kind": kind, "reader": (data or {}).get("reader") or readers.get(file) or "not read", "space": space_of(rows_kept, kind, data),
            "keptInsidePhase1": rows_kept, "civilInFile": civil, "civilRead": probe_civil((data or {}).get("counts")) if data else {},
            "notRead": note}


def table_md(rows):
    lines = ["| File | Reader | Space | Kept inside Phase 1 | Civil 3D objects in the file | Civil 3D read | Not read and why |", "|---|---|---|---|---|---|---|"]
    for r in rows:
        kept_text = ", ".join("%s %s" % (k, format(v, ",")) for k, v in r["keptInsidePhase1"].items()) or "-"
        civil = ", ".join("%s %d" % (k.replace("AECC_", ""), v) for k, v in (r["civilInFile"] or {}).items()) or "-"
        read = ", ".join("%s %d" % kv for kv in r["civilRead"].items()) or "-"
        lines.append("| %s | %s | %s | %s | %s | %s | %s |" % (r["file"], r["reader"], r["space"], kept_text, civil, read, r["notRead"]))
    return "\n".join(lines)


def run(config, civil=None):
    book = config["dataBook"]
    settings = book["data5"]
    cover = settings["coverage"]
    root = Path(book["root"])
    renames = book["surfaceVersions"]["renames"]
    reverse = {v: k for k, v in renames.items()}
    civil = civil or (lambda copy: civil_classes(REPO / book["cadReader"], copy))
    rows = []
    for folder in settings["folders"]:
        source = Path(config["sourceRoot"]) / reverse.get(folder, folder)
        for path in sorted(p for p in source.rglob("*") if p.is_file() and p.suffix.lower() in (".dwg", ".xlsx", ".pdf")):
            relative = (Path(folder) / path.relative_to(source)).as_posix()
            kind = {".dwg": "dwg", ".xlsx": "workbook", ".pdf": "pdf"}[path.suffix.lower()]
            data_path = root / path.stem / "data.json"
            data = json.loads(data_path.read_text(encoding="utf-8")) if kind == "dwg" and data_path.is_file() else None
            rows.append(row_of(relative, kind, data, cover["readers"], civil(root / "work" / relative) if kind == "dwg" else None,
                               cover["notes"].get(relative, "not given")))
    out = root / settings["outFolder"]
    out.mkdir(parents=True, exist_ok=True)
    (out / "coverage.json").write_text(json.dumps(rows, indent=1), encoding="utf-8")
    (out / "coverage.md").write_text(table_md(rows) + "\n", encoding="utf-8")
    return rows


def main(argv=None):
    parser = argparse.ArgumentParser(description="The coverage table of the four source folders (R259)")
    parser.add_argument("--config", default=str(c3d_run.CONFIG))
    args = parser.parse_args(argv)
    rows = run(c3d_run.load_config(args.config))
    print(table_md(rows))
    return 0


if __name__ == "__main__":
    sys.exit(main())
