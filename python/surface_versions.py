"""Every DWG and XML under the source root and the road surfaces it holds, Part X of round DATA-4 (R242).

Each file is opened only as its copy under dataBook.root/work, at the same place with the first folders renamed as
dataBook.surfaceVersions.renames says. A DWG is read by CadReader --header-only (R241): units, the created and saved times
of its header and the instance count of each class, so the Civil 3D surface classes of surfaceClasses tell whether it holds
a surface. An XML is read for a LandXML root, its date and the names and point counts of its surfaces. Rows go to
surface_versions.csv in the data book, counts to surface_versions.json there.
"""
import argparse
import csv
import json
import subprocess
import sys
import tempfile
from collections import Counter
from datetime import datetime
from pathlib import Path
from xml.etree import ElementTree

import c3d_run

REPO = Path(__file__).resolve().parent.parent


def copy_path(source, source_root, work, renames):
    """The working copy of a source file, the first folder renamed as the data book does."""
    parts = list(Path(source).relative_to(source_root).parts)
    parts[0] = renames.get(parts[0], parts[0])
    return Path(work).joinpath(*parts)


def surface_count(header, classes):
    """Instances of the surface classes in a header of R241, by class."""
    found = {c["dxfName"]: c["instanceCount"] for c in header.get("classes") or [] if c["dxfName"] in classes and c["instanceCount"]}
    return sum(found.values()), found


def landxml_facts(path):
    """Root, date, application and the surfaces of a LandXML file, or the root tag of another XML."""
    surfaces, date, application, root_tag = [], None, None, None
    for event, element in ElementTree.iterparse(path, events=("start",)):
        tag = element.tag.split("}")[-1]
        if root_tag is None:
            root_tag = tag
            date = element.get("date")
            if tag != "LandXML":
                break
        elif tag == "Application":
            application = element.get("name")
        elif tag == "Surface":
            surfaces.append(element.get("name"))
    return {"root": root_tag, "date": date, "application": application, "surfaces": surfaces}


def read_header(cad_reader, copy):
    with tempfile.TemporaryDirectory(prefix="hdr_") as folder:
        done = subprocess.run([str(cad_reader), str(copy), folder, "--header-only"], capture_output=True, text=True, timeout=1800)
        if done.returncode != 0:
            return {"error": done.stderr.strip() or "exit %d" % done.returncode}
        return json.loads((Path(folder) / "header.json").read_text(encoding="utf-8"))


def survey(config, header_reader=None):
    book = config["dataBook"]
    settings = book["surfaceVersions"]
    source_root = Path(config["sourceRoot"])
    work = Path(book["root"]) / "work"
    reader = header_reader or (lambda copy: read_header(REPO / book["cadReader"], copy))
    rows = []
    for source in sorted(p for p in source_root.rglob("*") if p.is_file() and p.suffix.lower() in settings["suffixes"] and not p.name.startswith("~$")):
        copy = copy_path(source, source_root, work, settings["renames"])
        row = {"file": str(source.relative_to(source_root)), "folder": source.relative_to(source_root).parts[0], "bytes": source.stat().st_size,
               "fileDate": datetime.fromtimestamp(source.stat().st_mtime).strftime("%Y-%m-%d %H:%M"), "copy": copy.is_file(),
               "created": None, "updated": None, "surfaces": 0, "surfaceClasses": "", "landxmlSurfaces": "", "error": None}
        if not copy.is_file():
            row["error"] = "no working copy, not opened"
        elif source.suffix.lower() == ".dwg":
            header = reader(copy)
            if "error" in header:
                row["error"] = header["error"]
            else:
                count, by_class = surface_count(header, set(settings["surfaceClasses"]))
                row.update({"created": header.get("created"), "updated": header.get("updated"), "surfaces": count,
                            "surfaceClasses": " ".join("%s=%d" % kv for kv in sorted(by_class.items()))})
        else:
            facts = landxml_facts(copy)
            row.update({"created": facts["date"], "surfaces": len(facts["surfaces"]) if facts["root"] == "LandXML" else 0,
                        "landxmlSurfaces": "|".join(facts["surfaces"])})
        rows.append(row)
    with_surfaces = [r for r in rows if r["surfaces"]]
    report = {"files": len(rows), "byFolder": dict(Counter(r["folder"] for r in rows)), "withSurfaces": len(with_surfaces),
              "errors": sum(1 for r in rows if r["error"]), "noCopy": sum(1 for r in rows if not r["copy"]),
              "surfaceFiles": [{k: r[k] for k in ("file", "bytes", "created", "updated", "surfaces", "surfaceClasses", "landxmlSurfaces")} for r in with_surfaces]}
    return report, rows


def main(argv=None):
    parser = argparse.ArgumentParser(description="Every DWG and XML under the source root and the road surfaces it holds (R242)")
    parser.add_argument("--config", default=str(c3d_run.CONFIG))
    args = parser.parse_args(argv)
    config = c3d_run.load_config(args.config)
    report, rows = survey(config)
    out = Path(config["dataBook"]["root"]) / config["dataBook"]["surfaceVersions"]["outFolder"]
    out.mkdir(parents=True, exist_ok=True)
    with open(out / "surface_versions.csv", "w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, list(rows[0]) if rows else ["file"], lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)
    (out / "surface_versions.json").write_text(json.dumps(report, indent=1), encoding="utf-8")
    print(json.dumps(report, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
