"""The location plan DWG of 01_Output against the Rev.09 location plan PDF, Part L2 of round DATA-3 (R196).

The DWG copy is read whole by CadReader into dataBook.locationDwg.cadOut. Each plot text of labelLayer holds the code lines
and the number on its last line. It is joined by number with the labels the PDF reader of R179 to R183 wrote, the code is
compared as written, and the DWG text points are fitted to the PDF label points by an affine fit, so a position that moved
shows as a residual. The DWG header dates, the PDF dates and the title block lines of both PDF exports say which is newer.
Counts go to location_dwg.json in the data book.
"""
import argparse
import json
import re
import sys
from collections import Counter
from pathlib import Path

import pdfplumber

import c3d_run
import data_book_analysis
import location_plan


def dwg_labels(texts, layer):
    """number to code of every plot text of the layer, the number on the last line, and the numbers found twice."""
    found, seen = {}, Counter()
    for text in texts:
        if text.get("layer") != layer:
            continue
        parts = [p.strip() for p in (text.get("text") or "").split("\n") if p.strip()]
        if not parts or not re.fullmatch(r"\d{1,4}", parts[-1]):
            continue
        seen[parts[-1]] += 1
        found[parts[-1]] = {"code": " ".join(parts[:-1]), "x": text["x"], "y": text["y"]}
    return found, sorted((n for n, c in seen.items() if c > 1), key=int)


def compare(dwg, pdf):
    """Numbers in one only and the code of each number in both, as written."""
    both = sorted(set(dwg) & set(pdf), key=int)
    codes = Counter("same" if dwg[n]["code"] == pdf[n]["code"] else "differs" for n in both)
    return both, {"dwgLabels": len(dwg), "pdfLabels": len(pdf), "both": len(both), "onlyDwg": sorted(set(dwg) - set(pdf), key=int),
                  "onlyPdf": sorted(set(pdf) - set(dwg), key=int), "codes": dict(codes),
                  "codeDifferences": dict(Counter("%s>%s" % (pdf[n]["code"], dwg[n]["code"]) for n in both if dwg[n]["code"] != pdf[n]["code"]).most_common())}


def position_fit(dwg, pdf, both, settings):
    """The affine fit from DWG text points to PDF label points, residual in page points and in metres at the fitted page scale."""
    page = [(float(pdf[n]["pageX"]), float(pdf[n]["pageY"])) for n in both]
    site = [(float(pdf[n]["east"]), float(pdf[n]["north"])) for n in both]
    points = [(dwg[n]["x"], dwg[n]["y"]) for n in both]
    to_page, residual, keep = location_plan.robust_fit(points, page, settings["fitDropFactor"], settings["fitRounds"], settings["fitFloorPt"])
    to_site, site_residual, site_keep = location_plan.robust_fit(points, site, settings["fitDropFactor"], settings["fitRounds"], settings["fitFloorM"])
    return {"toPagePt": {"kept": len(keep), "residualKept": location_plan.stats(residual[keep]), "residualAll": location_plan.stats(residual)},
            "toSiteM": {"kept": len(site_keep), "residualKept": location_plan.stats(site_residual[site_keep]), "residualAll": location_plan.stats(site_residual)}}


def pdf_facts(path, words):
    """Dates, title lines that hold a word and annotation texts of a PDF export."""
    with pdfplumber.open(path) as pdf:
        page = pdf.pages[0]
        text = "".join(c["text"] for c in page.chars)
        meta = pdf.metadata or {}
        hits = {}
        for word in words:
            at = text.find(word)
            hits[word] = {"count": text.count(word), "text": text[at:at + 60] if at >= 0 else None}
        return {"file": Path(path).name, "created": meta.get("CreationDate"), "modified": meta.get("ModDate"), "producer": meta.get("Producer"),
                "characters": len(page.chars), "annotations": [a.get("contents") for a in page.annots or []], "words": hits}


def run(config):
    book = config["dataBook"]
    settings = book["locationDwg"]
    root = Path(book["root"])
    cad = root / settings["cadOut"]
    layers = json.loads((cad / "layers.json").read_text(encoding="utf-8"))
    texts = json.loads((cad / "texts.json").read_text(encoding="utf-8"))["texts"]
    dwg, twice = dwg_labels(texts, settings["labelLayer"])
    pdf = {r["number"]: r for r in data_book_analysis.read_csv(root / book["locationPlan"]["outFolder"] / "labels.csv")}
    both, counts = compare(dwg, pdf)
    report = {"dwg": {"file": layers.get("source"), "units": layers.get("units"), "created": layers.get("created"), "updated": layers.get("updated"),
                      "numbersTwice": twice, "imageEntities": sum(c["instanceCount"] for c in layers.get("classes") or [] if c["dxfName"] == "IMAGE")},
              "compare": counts, "onlyDwgCodes": {n: dwg[n]["code"] for n in counts["onlyDwg"]}, "positions": position_fit(dwg, pdf, both, settings),
              "pdfs": [pdf_facts(root / "work" / p, settings["titleWords"]) if not Path(p).is_absolute() else pdf_facts(p, settings["titleWords"]) for p in settings["pdfs"]]}
    out = root / settings["outFolder"]
    out.mkdir(parents=True, exist_ok=True)
    (out / "location_dwg.json").write_text(json.dumps(report, indent=1, default=str), encoding="utf-8")
    return report


def main(argv=None):
    parser = argparse.ArgumentParser(description="The location plan DWG of 01_Output against the Rev.09 location plan PDF (R196)")
    parser.add_argument("--config", default=str(c3d_run.CONFIG))
    args = parser.parse_args(argv)
    report = run(c3d_run.load_config(args.config))
    print(json.dumps({"dwg": report["dwg"], "compare": {k: v for k, v in report["compare"].items() if k != "onlyPdf"}, "positions": report["positions"]}, default=str))
    return 0


if __name__ == "__main__":
    sys.exit(main())
