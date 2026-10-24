"""Compares the mirror flag and the rotation of the block of a plot with those of its Revit link (R77).

Reads analysis.json of the cluster step, the links of the Revit reader output and the schedule workbooks, and changes
nothing. A link is matched to a plot by its official plot number, which is the baseplan label plus the shift of the
neighbourhood (R28). The neighbourhood of an official number is the one of the workbook that lists it. The report
holds counts only, no plot numbers.
"""
import argparse
import json
import sys
from collections import Counter
from pathlib import Path

import cad_analysis
import common
import link_table
import project_profile

DEFAULT_SHIFTS = Path(__file__).resolve().parent.parent / "config" / "plot_numbers.json"
ROTATION_DECIMALS = 1


def read_links(path):
    """The links of every document in the Revit reader output."""
    try:
        data = json.loads(Path(path).read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise project_profile.ProfileError("the Revit output cannot be read: " + str(error)) from error
    documents = data.get("documents") if isinstance(data, dict) else None
    if not isinstance(documents, list):
        raise project_profile.ProfileError("the Revit output has no documents list")
    return [link for document in documents if isinstance(document, dict) for link in document.get("links") or [] if isinstance(link, dict)]


def read_records(path):
    try:
        data = json.loads(Path(path).read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise project_profile.ProfileError("analysis.json cannot be read: " + str(error)) from error
    if not isinstance(data, dict) or not isinstance(data.get("plots"), list):
        raise project_profile.ProfileError("analysis.json has no plots list")
    return {str(record["plot"]): record for record in data["plots"] if isinstance(record, dict) and "plot" in record}


def shift_by_plot(books, shifts):
    """Official plot number to the shift of its neighbourhood, and the numbers that two neighbourhoods list."""
    found, twice = {}, set()
    for book in books:
        code = link_table.neighbourhood_of(book.get("source"), shifts)
        if code is None or code not in shifts["shifts"]:
            raise project_profile.ProfileError("no plot number shift for the workbook " + str(book.get("source")))
        for row in book.get("rows") or []:
            plot = next((row[header] for header in row if header.strip().lower().startswith("plot no")), None)
            key = link_table.plot_key(plot) if plot is not None and str(plot).strip() else None
            if isinstance(key, int):
                if key in found and found[key] != shifts["shifts"][code]:
                    twice.add(key)
                found[key] = shifts["shifts"][code]
    return found, twice


def turn(link_rotation, block_rotation):
    """How far the link is turned from the block, in degrees from 0 up to 360."""
    return round((link_rotation - block_rotation) % 360, ROTATION_DECIMALS) % 360


def compare(links, records, shifts_of, twice):
    counts = Counter()
    crosstab, by_unit, turned, link_turns = Counter(), {}, Counter(), Counter()
    for link in links:
        counts["links"] += 1
        transform = link.get("transform") or {}
        plot = str((link.get("nameParts") or {}).get("plot", "")).strip()
        link_turns[round(transform.get("rotationDeg"), ROTATION_DECIMALS) if isinstance(transform.get("rotationDeg"), (int, float)) else None] += 1
        if not plot.isdecimal():
            counts["noPlotNumber"] += 1
            continue
        official = int(plot)
        if official in twice:
            counts["inTwoWorkbooks"] += 1
            continue
        if official not in shifts_of:
            counts["noScheduleRow"] += 1
            continue
        record = records.get(str(official - shifts_of[official]))
        if record is None:
            counts["noPlotRecord"] += 1
            continue
        if record.get("block") is None:
            counts["noBlock"] += 1
            continue
        counts["withBlock"] += 1
        if isinstance(transform.get("rotationDeg"), (int, float)) and isinstance(record.get("rotation"), (int, float)):
            turned[turn(transform["rotationDeg"], record["rotation"])] += 1
        if record.get("mirrored") is not None and isinstance(transform.get("mirrored"), bool):
            counts["withFlag"] += 1
            same = bool(record["mirrored"]) == transform["mirrored"]
            counts["flagSame" if same else "flagOpposite"] += 1
            crosstab["block " + ("mirrored" if record["mirrored"] else "not mirrored") + ", link " + ("mirrored" if transform["mirrored"] else "not mirrored")] += 1
            entry = by_unit.setdefault(str(record.get("unit")), {"same": 0, "opposite": 0})
            entry["same" if same else "opposite"] += 1
    return {
        "links": counts["links"], "noPlotNumber": counts["noPlotNumber"], "inTwoWorkbooks": counts["inTwoWorkbooks"], "noScheduleRow": counts["noScheduleRow"],
        "noPlotRecord": counts["noPlotRecord"], "noBlock": counts["noBlock"], "withBlock": counts["withBlock"], "withMirrorFlag": counts["withFlag"],
        "mirrorSame": counts["flagSame"], "mirrorOpposite": counts["flagOpposite"], "mirrorCrosstab": dict(sorted(crosstab.items())),
        "mirrorByUnit": {unit: by_unit[unit] for unit in sorted(by_unit)},
        "linkRotations": {str(key): link_turns[key] for key in sorted(link_turns, key=lambda key: (key is None, key))},
        "linkTurnedFromBlock": {str(key): turned[key] for key in sorted(turned)},
    }


def main(argv=None):
    parser = argparse.ArgumentParser(description="Compare the mirror flag and the rotation of the block of a plot with those of its Revit link.")
    parser.add_argument("out_dir", help="where link_proof.json is written")
    parser.add_argument("--analysis", required=True, help="analysis.json of the cluster step")
    parser.add_argument("--prototypes", required=True, help="the output file of the Revit reader, with the links")
    parser.add_argument("--coords", nargs="+", required=True, help="coords.json files of the schedule workbooks")
    parser.add_argument("--shifts", default=str(DEFAULT_SHIFTS), help="plot number shift config")
    args = parser.parse_args(argv)
    try:
        shifts = cad_analysis.read_shifts(args.shifts)
        shifts_of, twice = shift_by_plot(cad_analysis.read_coords(args.coords), shifts)
        result = compare(read_links(args.prototypes), read_records(args.analysis), shifts_of, twice)
    except project_profile.ProfileError as error:
        print("link_proof: " + str(error), file=sys.stderr)
        return 2
    common.write_json(Path(args.out_dir) / "link_proof.json", result)
    print("links " + str(result["links"]) + ", with a block " + str(result["withBlock"]) + ", with a mirror flag " + str(result["withMirrorFlag"])
          + " (same " + str(result["mirrorSame"]) + ", opposite " + str(result["mirrorOpposite"]) + ")")
    print("link turned from the block by degrees: " + json.dumps(result["linkTurnedFromBlock"]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
