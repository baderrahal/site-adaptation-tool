"""The Civil 3D parcels of the Rev 9 drawing against the baseplan, the location plan and the Rev 9 PDF, Part P of round DATA-4
(R243, R244).

The probe writes each parcel of the drawing in civil.csv as property rows of kind parcel, those of a bound block too (R243),
one parcel after another, so a parcel ends where a property comes again. A plot parcel has a number. Its outline is matched
with the baseplan faces by overlap over union like the Rev 9 PDF faces of R191, its number is compared with the location plan
number of the face and its style with Typology_Rev9. Then the unit plots R191 found different are taken again: a difference is
real when the parcel shows it too, and comes from the PDF read when the parcel agrees with the baseplan. Rows go to parcels.csv
and parcel_redo.csv in the data book rev9 folder, counts to rev9_parcels.json there.
"""
import argparse
import csv
import json
import re
import sys
from collections import Counter
from pathlib import Path

from shapely.geometry import Polygon

import c3d_run
import data_book
import data_book_analysis
import link_summary
import location_plan
import rev9_drawing
import yard_rule


def read_parcels(path):
    """The parcels of civil.csv as dicts of their properties, the site or block in parent and the outline as points."""
    csv.field_size_limit(1 << 30)
    found, current = [], None
    with open(path, encoding="utf-8") as handle:
        for row in csv.DictReader(handle):
            if row["kind"] != "parcel":
                continue
            if current is None or row["property"] in current["properties"]:
                current = {"name": row["name"], "layer": row["layer"], "parent": row["parent"], "properties": {}}
                found.append(current)
            current["properties"][row["property"]] = row["value"]
    for parcel in found:
        text = parcel["properties"].get("outline") or ""
        points = [tuple(float(v) for v in pair.split(" ")) for pair in text.split("|") if pair.strip()] if text else []
        parcel["outline"] = points
    return found


def plot_parcels(parcels):
    """The parcels with a whole number, as number, style, description, area and polygon."""
    found = []
    for parcel in parcels:
        number = parcel["properties"].get("Number")
        if not number or not re.fullmatch(r"\d+", number) or len(parcel["outline"]) < 3:
            continue
        shape = Polygon(parcel["outline"]).buffer(0)
        found.append({"number": number, "style": parcel["properties"].get("StyleName"), "description": parcel["properties"].get("Description"),
                      "area": data_book_analysis.number(parcel["properties"].get("Area")), "parent": parcel["parent"], "shape": shape})
    return found


def against_plan(matches, parcels, row_of, labels):
    """Per baseplan face the parcel of the match, its number against the location plan number and its style against Typology_Rev9."""
    found = {}
    for label, match in matches.items():
        row = row_of[label]
        parcel = parcels[match["face"]] if match["face"] is not None else None
        plan_numbers = [n for n in (row["locationPlanNumber"] or "").split("|") if n]
        number_class = "noParcel" if parcel is None else "noPlanNumber" if len(plan_numbers) != 1 else "same" if parcel["number"] == plan_numbers[0] else "differs"
        typology = row["typologyRev9"] or ""
        code_class = "noParcel" if parcel is None else "noTypologyRev9" if not typology or "|" in typology else "same" if parcel["style"] == typology else "differs"
        found[label] = {"parcel": parcel["number"] if parcel else None, "style": parcel["style"] if parcel else None, "faceClass": match["class"],
                        "iou": match["iou"], "areaDifferenceM2": match["areaDifferenceM2"], "numberClass": number_class, "codeClass": code_class,
                        "planNumber": row["locationPlanNumber"], "typologyRev9": typology}
    return found


def redo(differing, by_label, pdf_outlines, parcels_by_number):
    """The unit plots R191 found different, taken again with the parcels. shape is real when the parcel differs from the baseplan
    as well, pdfRead when the parcel is the same as the baseplan. A number or code difference is real when the parcel holds it."""
    rows = []
    for item in differing:
        label = item["baseplanLabel"]
        parcel = by_label.get(label) or {}
        found = {"baseplanLabel": label, "clientType": item["clientType"], "pdfFace": item["faceClass"], "parcelFace": parcel.get("faceClass"),
                 "parcelIou": parcel.get("iou"), "pdfNumber": item["numberClass"], "parcelNumber": parcel.get("numberClass"),
                 "pdfCode": item["codeClass"], "parcelCode": parcel.get("codeClass"), "pdfAgainstParcelIou": None}
        outline = pdf_outlines.get(label)
        number = parcel.get("parcel")
        if outline and number in parcels_by_number:
            pdf = Polygon(outline).buffer(0)
            shape = parcels_by_number[number]["shape"]
            union = pdf.union(shape).area
            found["pdfAgainstParcelIou"] = round(pdf.intersection(shape).area / union, 4) if union else None
        verdicts = []
        if item["faceClass"] != "same":
            verdicts.append("shape real" if parcel.get("faceClass") != "same" else "shape pdfRead")
        if item["numberClass"] == "differs":
            verdicts.append("number real" if parcel.get("numberClass") == "differs" else "number pdfRead")
        if item["codeClass"] == "differs":
            verdicts.append("code real" if parcel.get("codeClass") == "differs" else "code pdfRead")
        if item["unitTextClass"] == "differs":
            verdicts.append("dwgText")
        found["verdicts"] = "|".join(verdicts)
        rows.append(found)
    return rows


def run(config):
    book = config["dataBook"]
    settings = book["rev9"]
    parcel_settings = settings["parcels"]
    root = Path(book["root"])
    out = root / settings["outFolder"]
    tables = root / Path(book["drawings"][settings["drawing"]["role"]]).stem
    parcels = read_parcels(tables / "civil.csv")
    plots = plot_parcels(parcels)
    rows = data_book_analysis.read_csv(root / "plots.csv")
    row_of = {r["baseplanLabel"]: r for r in rows}
    border = data_book.border_polygon(root / "phase1_border.csv")
    base = yard_rule.face_shapes(link_summary.load(config["connect"]["baseplanPlots"])["plots"], 0)[0]
    base = {k: v for k, v in base.items() if k in row_of and border.contains(v.representative_point())}
    unit_types = set(book["analysis"]["unitPlotTypes"])
    units = [k for k in base if row_of[k]["clientType"] in unit_types]
    matches = rev9_drawing.face_match(base, [p["shape"] for p in plots], parcel_settings["sameShareMin"], parcel_settings["matchShareMin"])
    for label, match in matches.items():
        match["hausdorffM"] = round(plots[match["face"]]["shape"].hausdorff_distance(base[label]), 3) if match["face"] is not None else None
    by_label = against_plan(matches, plots, row_of, None)
    labels = data_book_analysis.read_csv(root / book["locationPlan"]["outFolder"] / "labels.csv")
    plan_numbers = {l["number"] for l in labels}
    pdf_numbers = {r["number"] for r in data_book_analysis.read_csv(out / "rev9_labels.csv")}
    parcel_numbers = {p["number"] for p in plots}
    report = {"parcels": len(parcels), "plotParcels": len(plots), "parents": dict(Counter(p["parent"] for p in parcels)),
              "styles": dict(Counter(p["style"] for p in plots).most_common()),
              "numbers": {"parcels": len(parcel_numbers), "inLocationPlan": len(parcel_numbers & plan_numbers), "locationPlanNotParcel": len(plan_numbers - parcel_numbers),
                          "inRev9Pdf": len(parcel_numbers & pdf_numbers), "rev9PdfNotParcel": len(pdf_numbers - parcel_numbers),
                          "parcelNotInEither": len(parcel_numbers - plan_numbers - pdf_numbers),
                          "parcelNotInEitherStyles": dict(Counter(p["style"] for p in plots if p["number"] not in plan_numbers | pdf_numbers).most_common())},
              "faces": {"unitPlots": len(units), "byClass": dict(Counter(matches[k]["class"] for k in units)),
                        "hausdorffM": location_plan.stats([matches[k]["hausdorffM"] for k in units if matches[k]["hausdorffM"] is not None]),
                        "iou": location_plan.stats([matches[k]["iou"] for k in units if matches[k]["face"] is not None])},
              "number": dict(Counter(by_label[k]["numberClass"] for k in units)), "code": dict(Counter(by_label[k]["codeClass"] for k in units)),
              "codeDiffers": dict(Counter("%s>%s" % (by_label[k]["typologyRev9"], by_label[k]["style"]) for k in units if by_label[k]["codeClass"] == "differs").most_common())}
    faces = data_book_analysis.read_csv(out / "rev9_faces.csv")
    differing = [r for r in faces if r["faceClass"] != "same" or r["numberClass"] == "differs" or r["codeClass"] == "differs" or r["unitTextClass"] == "differs"]
    outlines = json.loads((out / "rev9_face_outlines.json").read_text(encoding="utf-8"))
    redone = redo(differing, by_label, outlines, {p["number"]: p for p in plots})
    report["redo"] = {"plots": len(redone), "verdicts": dict(Counter(v for r in redone for v in r["verdicts"].split("|") if v)),
                      "plotsWithARealDifference": sum(1 for r in redone if "real" in r["verdicts"]),
                      "plotsOnlyFromThePdfRead": sum(1 for r in redone if r["verdicts"] and "real" not in r["verdicts"] and "dwgText" not in r["verdicts"]),
                      "pdfAgainstParcelIou": location_plan.stats([r["pdfAgainstParcelIou"] for r in redone if r["pdfAgainstParcelIou"] is not None])}
    with open(out / "parcels.csv", "w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle, lineterminator="\n")
        writer.writerow(["number", "style", "description", "areaM2", "parent", "baseplanLabel", "iou", "faceClass"])
        label_of = {m["face"]: label for label, m in matches.items() if m["face"] is not None}
        for k, p in enumerate(plots):
            label = label_of.get(k)
            writer.writerow([p["number"], p["style"], p["description"], p["area"], p["parent"], label, matches[label]["iou"] if label else None,
                             matches[label]["class"] if label else None])
    with open(out / "parcel_redo.csv", "w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, list(redone[0]) if redone else ["baseplanLabel"], lineterminator="\n")
        writer.writeheader()
        writer.writerows(redone)
    (out / "rev9_parcels.json").write_text(json.dumps(report, indent=1), encoding="utf-8")
    return report


def main(argv=None):
    parser = argparse.ArgumentParser(description="The Civil 3D parcels of Rev 9 against the baseplan, the location plan and the Rev 9 PDF (R243, R244)")
    parser.add_argument("--config", default=str(c3d_run.CONFIG))
    args = parser.parse_args(argv)
    report = run(c3d_run.load_config(args.config))
    print(json.dumps({k: report[k] for k in ("parcels", "plotParcels", "numbers", "faces", "number", "code", "codeDiffers", "redo")}, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
