"""Audit of round DATA-4, Part Q of round DATA-5 (R251).

The 115 unit plots of R191 are listed with the verdicts of parcel_redo.csv and put in one group each: the plots that hold a
shape, number or code verdict and the plots that hold only the DWG unit text. The parcel code differences over all unit plots
are taken again from parcels.csv and plots.csv, each with the PDF class it had, so a parcel difference the PDF compare could
not see is named. Every count the DATA-4 records give is counted again from the tables, and the counts that differ are
listed. Then 20 unit plots per seed are recounted from parcels.csv against plots.csv. Rows go to audit_115.csv and
audit_codes.csv in the data5 folder of the data book, counts to audit.json there.
"""
import argparse
import csv
import json
import random
import sys
from collections import Counter
from pathlib import Path

import c3d_run
import data_book_analysis

VERDICT_KINDS = ("shape", "number", "code")


def group_of(verdicts):
    """named when any shape, number or code verdict is set, dwgTextOnly when only the DWG text is, none otherwise."""
    kinds = [v.split(" ")[0] for v in verdicts.split("|") if v]
    if any(k in VERDICT_KINDS for k in kinds):
        return "named"
    return "dwgTextOnly" if "dwgText" in kinds else "none"


def verdict_counts(rows):
    """Each verdict once per plot that holds it, and the plots per group."""
    verdicts = Counter(v for r in rows for v in r["verdicts"].split("|") if v)
    groups = Counter(group_of(r["verdicts"]) for r in rows)
    named = [r for r in rows if group_of(r["verdicts"]) == "named"]
    summed = sum(n for v, n in verdicts.items() if v.split(" ")[0] in VERDICT_KINDS)
    alone = Counter(v for r in named for v in r["verdicts"].split("|") if v and r["verdicts"].count("|") == 0)
    return {"plots": len(rows), "verdicts": dict(sorted(verdicts.items())), "groups": dict(groups),
            "verdictsAddedUp": summed, "namedPlots": len(named), "namedAlone": dict(alone),
            "combinations": dict(Counter(r["verdicts"] for r in rows).most_common())}


def single(text):
    values = [v for v in (text or "").split("|") if v]
    return values[0] if len(values) == 1 else None


def classes(parcel, row):
    """Number and code class of one unit plot as R244 sets them, from the parcels.csv row and the plots.csv row."""
    if parcel is None:
        return "noParcel", "noParcel"
    number = single(row["locationPlanNumber"])
    typology = row["typologyRev9"] or ""
    number_class = "noPlanNumber" if number is None else "same" if parcel["number"] == number else "differs"
    code_class = "noTypologyRev9" if not typology or "|" in typology else "same" if parcel["style"] == typology else "differs"
    return number_class, code_class


def unit_rows(plots, unit_types):
    return {r["baseplanLabel"]: r for r in plots if r["clientType"] in unit_types}


def code_differences(units, parcel_of, faces, redo):
    """Every unit plot whose parcel style differs from Typology_Rev9, with the PDF classes of R191 and the DATA-4 verdict."""
    found = []
    for label, row in sorted(units.items(), key=lambda item: int(item[0]) if item[0].isdigit() else 0):
        parcel = parcel_of.get(label)
        if classes(parcel, row)[1] != "differs":
            continue
        face = faces.get(label) or {}
        verdicts = (redo.get(label) or {}).get("verdicts") or ""
        found.append({"baseplanLabel": label, "clientType": row["clientType"], "typologyRev9": row["typologyRev9"], "parcelStyle": parcel["style"],
                      "parcel": parcel["number"], "planNumbers": row["locationPlanNumber"], "parcelFace": parcel["faceClass"],
                      "pdfFace": face.get("faceClass"), "pdfNumber": face.get("numberClass"), "pdfCode": face.get("codeClass"),
                      "verdicts": verdicts, "codeVerdict": "code real" in verdicts})
    return found


def recount(units, parcel_of, parcel_rows, plan_numbers, pdf_numbers):
    """The DATA-4 counts again, from parcels.csv, plots.csv and the two label tables only."""
    face = Counter()
    number, code = Counter(), Counter()
    for label, row in units.items():
        parcel = parcel_of.get(label)
        face["noFace" if parcel is None else parcel["faceClass"]] += 1
        n, c = classes(parcel, row)
        number[n] += 1
        code[c] += 1
    numbers = {p["number"] for p in parcel_rows}
    neither = [p for p in parcel_rows if p["number"] not in plan_numbers | pdf_numbers]
    return {"plotParcels": len(parcel_rows), "unitPlots": len(units), "faces": dict(face), "number": dict(number), "code": dict(code),
            "inLocationPlan": len(numbers & plan_numbers), "locationPlanNotParcel": len(plan_numbers - numbers),
            "inRev9Pdf": len(numbers & pdf_numbers), "rev9PdfNotParcel": len(pdf_numbers - numbers), "parcelNotInEither": len(neither),
            "styles": dict(Counter(p["style"] for p in parcel_rows))}


def lost_by_parcels_csv(units, parcel_of, parcel_rows):
    """Unit plots whose parcel parcels.csv names under another face, the reason a recount from parcels.csv alone falls short."""
    named = {p["baseplanLabel"] for p in parcel_rows if p["baseplanLabel"]}
    found = []
    for label in sorted(units):
        parcel = parcel_of.get(label)
        if parcel and label not in named:
            other = next((p["baseplanLabel"] for p in parcel_rows if p["number"] == parcel["number"]), None)
            found.append({"baseplanLabel": label, "parcel": parcel["number"], "faceClass": parcel["faceClass"], "parcelsCsvFace": other})
    return found


def lookup(found, path):
    value = found
    for key in path.split("."):
        if not isinstance(value, dict) or key not in value:
            return 0
        value = value[key]
    return value


def against_claims(found, claims):
    """Each count a record states against the count found, the place it is stated kept."""
    rows = []
    for claim in claims:
        value = lookup(found, claim["count"])
        rows.append({**claim, "found": value, "equal": value == claim["stated"]})
    return rows


def sample(units, parcel_of, redo, seed, size):
    """size unit plots at seed, each recounted from the parcel number and style against plots.csv and held against the
    classes R244 stored, and against parcel_redo.csv for a plot of the 115."""
    picked = random.Random(seed).sample(sorted(units), min(size, len(units)))
    rows = []
    for label in picked:
        row, parcel = units[label], parcel_of.get(label)
        number, code = classes(parcel, row)
        stored = (parcel["numberClass"], parcel["codeClass"]) if parcel else ("noParcel", "noParcel")
        agrees = stored == (number, code)
        redone = redo.get(label)
        if redone:
            agrees = agrees and (redone["parcelNumber"] or "noParcel") == number and (redone["parcelCode"] or "noParcel") == code
        rows.append({"baseplanLabel": label, "parcel": parcel["number"] if parcel else None, "number": number, "code": code,
                     "inRedo": bool(redone), "agrees": agrees})
    return {"seed": seed, "plots": len(rows), "agree": sum(1 for r in rows if r["agrees"]), "rows": rows}


def write_rows(path, rows, fields):
    with open(path, "w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fields, lineterminator="\n", extrasaction="ignore")
        writer.writeheader()
        writer.writerows(rows)


def run(config):
    book = config["dataBook"]
    settings = book["data5"]
    audit = settings["audit"]
    root = Path(book["root"])
    rev9 = root / book["rev9"]["outFolder"]
    out = root / settings["outFolder"]
    out.mkdir(parents=True, exist_ok=True)
    redo_rows = data_book_analysis.read_csv(rev9 / "parcel_redo.csv")
    redo = {r["baseplanLabel"]: r for r in redo_rows}
    faces = {r["baseplanLabel"]: r for r in data_book_analysis.read_csv(rev9 / "rev9_faces.csv")}
    parcel_rows = data_book_analysis.read_csv(rev9 / "parcels.csv")
    matches = data_book_analysis.read_csv(rev9 / "parcel_matches.csv")
    parcel_of = {m["baseplanLabel"]: {"number": m["parcel"], "style": m["style"], "faceClass": m["faceClass"], "numberClass": m["numberClass"],
                                      "codeClass": m["codeClass"]} for m in matches if m["parcel"]}
    units = unit_rows(data_book_analysis.read_csv(root / "plots.csv"), set(book["analysis"]["unitPlotTypes"]))
    lost = lost_by_parcels_csv(units, parcel_of, parcel_rows)
    plan_numbers = {l["number"] for l in data_book_analysis.read_csv(root / book["locationPlan"]["outFolder"] / "labels.csv")}
    pdf_numbers = {l["number"] for l in data_book_analysis.read_csv(rev9 / "rev9_labels.csv")}
    listed = [{**r, "group": group_of(r["verdicts"])} for r in redo_rows]
    codes = code_differences(units, parcel_of, faces, redo)
    found = recount(units, parcel_of, parcel_rows, plan_numbers, pdf_numbers)
    found["redo"] = verdict_counts(redo_rows)
    found["parcels"] = json.loads((rev9 / "rev9_parcels.json").read_text(encoding="utf-8"))["parcels"]
    claims = against_claims(found, audit["claims"])
    report = {"lostByParcelsCsv": lost, "redo": found["redo"], "codeDifferences": {"plots": len(codes), "withCodeVerdict": sum(1 for c in codes if c["codeVerdict"]),
                                                          "withoutCodeVerdict": [c for c in codes if not c["codeVerdict"]]},
              "recount": {k: v for k, v in found.items() if k != "redo"}, "claims": claims,
              "claimsDiffering": [c for c in claims if not c["equal"]],
              "samples": [sample(units, parcel_of, redo, seed, audit["size"]) for seed in audit["seeds"]]}
    write_rows(out / "audit_115.csv", listed, ["baseplanLabel", "clientType", "pdfFace", "parcelFace", "pdfNumber", "parcelNumber", "pdfCode", "parcelCode", "verdicts", "group"])
    write_rows(out / "audit_codes.csv", codes, list(codes[0]) if codes else ["baseplanLabel"])
    (out / "audit.json").write_text(json.dumps(report, indent=1), encoding="utf-8")
    return report


def main(argv=None):
    parser = argparse.ArgumentParser(description="Audit of round DATA-4: the 115 plots, the parcel codes and the counts (R251)")
    parser.add_argument("--config", default=str(c3d_run.CONFIG))
    args = parser.parse_args(argv)
    report = run(c3d_run.load_config(args.config))
    print(json.dumps({"redo": report["redo"], "codeDifferences": report["codeDifferences"], "claimsDiffering": report["claimsDiffering"],
                      "samples": [{k: s[k] for k in ("seed", "plots", "agree")} for s in report["samples"]]}, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
