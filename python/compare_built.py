"""R835 to R839. A built model against the answer key, joined by plot number, at the pass level of docs/truth.md. A dev tool.

The past side is the answer key folder of the work root, one folder per past model with links.csv, key_reads.json and the raw
read. The built side is a folder with one folder per neighbourhood that holds the same two files of the read of a built model.
With no built folder every neighbourhood counts 0 built. Each past model is first counted against itself, which proves the
reader and the join. Every limit comes from the block compare of the settings, never from this file.

It writes steps/compare.md, counts, shares and differences in mm only, and the rows that fail as row numbers of links.csv into
compare/rows.csv of the work root. No coordinate, no level and no plot number leaves the two inputs.

python python/compare_built.py [--built <folder>]
"""
import argparse
import csv
import json
import sys
from decimal import ROUND_CEILING, Decimal, InvalidOperation
from pathlib import Path

import project_profile

ROOT = Path(__file__).resolve().parents[1]
LIMIT_NAMES = ("plotsBuiltPercent", "heldPercent", "positionMm", "fflMm", "rotationDeg", "countPercent")
LIST_NAMES = ("yardModels", "linksOnlyModels", "neighbourhoods")
MEASURES = ("position", "ffl", "rotation", "mirror")
COUNTS = ("floors", "yards", "walls", "levels")
NOT_COUNTED = "not counted"
NO_PAST = "UNKNOWN, no past model"
TABLE_HEAD = ("| Neighbourhood | Past model | Plots | Position | FFL | Rotation | Mirror | Floors | Yards | Walls | Levels | Result |\n"
              "|---|---|---|---|---|---|---|---|---|---|---|---|")


class CompareError(Exception):
    """An input the compare cannot count from, said in one line, never passed over."""


def plain(number):
    return format(Decimal(str(number)).normalize(), "f")


def load_limits(settings):
    """R836. The limits and lists of the block compare, each with its value and its source."""
    block = settings.get("compare")
    if not isinstance(block, dict):
        raise CompareError("the settings hold no block compare")
    limits = {}
    for name in LIMIT_NAMES + LIST_NAMES + ("job",):
        entry = block.get(name)
        if not isinstance(entry, dict) or "value" not in entry or not str(entry.get("source") or "").strip():
            raise CompareError("compare.%s needs a value and a source" % name)
        value = entry["value"]
        if name in LIMIT_NAMES and (isinstance(value, bool) or not isinstance(value, (int, float)) or not 0 <= value < float("inf")):
            raise CompareError("compare.%s is no number of 0 or more: %r" % (name, value))
        if name in LIST_NAMES and (not isinstance(value, list) or not all(isinstance(item, str) and item for item in value)):
            raise CompareError("compare.%s is no list of names: %r" % (name, value))
        limits[name] = Decimal(str(value)) if name in LIMIT_NAMES else value
        limits[name + "Source"] = str(entry["source"]).strip()
    return limits


def neighbourhood_of(areas, job):
    """R835. The neighbourhood of each past model by its code, from the rows of kind model of one job of the area codes."""
    found = {}
    for row in areas.get("jobs", {}).get(job) or []:
        names = sorted({area["nbh"] for area in row.get("areas", []) if area.get("kind") == "model"})
        if len(names) != 1:
            raise CompareError("the area codes name %d neighbourhoods for the model %s of job %s, one is needed" % (len(names), row.get("code"), job))
        found[row["code"]] = names[0]
    if not found:
        raise CompareError("the area codes hold no model of job %s" % job)
    return found


def number(text):
    try:
        value = Decimal(str(text).strip())
    except InvalidOperation:
        return None
    return value if value.is_finite() else None


def read_links(path):
    """R835. The rows of links.csv by plot number, each with its line of the file, the header being line 1. A link that names no
    plot is counted and left out."""
    by_plot, unnamed = {}, 0
    with open(path, encoding="utf-8-sig", newline="") as handle:
        for line, row in enumerate(csv.DictReader(handle), start=2):
            plot = (row.get("plot") or "").strip()
            if not plot:
                unnamed += 1
                continue
            by_plot.setdefault(plot, []).append({
                "line": line, "x": number(row.get("originX")), "y": number(row.get("originY")), "z": number(row.get("originZ")),
                "rotation": number(row.get("rotationDeg")), "mirror": {"true": True, "false": False}.get((row.get("mirrored") or "").strip().lower())})
    return by_plot, unnamed


def read_counts(path):
    """R837. Floors that are no yard, yards, walls and levels of one read, None where the read lacks the count."""
    try:
        keys = json.loads(Path(path).read_text(encoding="utf-8-sig")).get("keys") or {}
    except ValueError as error:
        raise CompareError("%s is no JSON: %s" % (Path(path).name, error)) from error
    if len(keys) != 1:
        raise CompareError("key_reads.json of %s holds %d reads, one is needed" % (Path(path).parent.name, len(keys)))
    counts = next(iter(keys.values())).get("counts") or {}
    floors = counts.get("floors") or {}
    yard = (floors.get("byKind") or {}).get("yard")
    whole = floors.get("count")
    found = {"floors": whole - yard if isinstance(whole, int) and isinstance(yard, int) else None, "yards": yard,
             "walls": (counts.get("walls") or {}).get("count"), "levels": (counts.get("levels") or {}).get("count")}
    return {name: value if isinstance(value, int) and not isinstance(value, bool) and value >= 0 else None for name, value in found.items()}


def read_side(folder, side):
    for name in ("links.csv", "key_reads.json"):
        if not (folder / name).is_file():
            raise CompareError("the %s read %s holds no %s" % (side, folder.name, name))
    by_plot, unnamed = read_links(folder / "links.csv")
    return {"links": by_plot, "unnamed": unnamed, "counts": read_counts(folder / "key_reads.json")}


def base_of(folder):
    """R838. The coordinate base of one read: project position, survey point and base point of its points file, None with no such
    file. Rounded at a tenth of a millimetre, so two reads of one base are one base."""
    files = sorted(folder.glob("*/testhost_*_points.json"))
    if len(files) != 1:
        return None
    try:
        read = json.loads(files[0].read_text(encoding="utf-8-sig"))
        place = read["projectPosition"]
        values = [place["eastWest"], place["northSouth"], place["elevation"], place["angleDeg"]]
        for point in ("surveyPoint", "basePoint"):
            values += list(read[point]["position"]) + list(read[point]["sharedPosition"])
        return tuple(round(float(value), 4) for value in values)
    except (ValueError, KeyError, TypeError):
        return None


def base_lines(bases):
    """R838. Whether the past models share one coordinate base, said before any compare."""
    count = len(bases)
    missing = sum(1 for base in bases if base is None)
    if missing:
        return "Measured first, on the past models alone: UNKNOWN, %d of %d reads hold no points file, so whether the past models share one coordinate base is not known." % (missing, count)
    distinct = len(set(bases))
    head = "Measured first, on the past models alone: %d distinct base%s of %d reads, by the project position, the survey point and the base point of each read." % (distinct, "" if distinct == 1 else "s", count)
    return head + (" The %d past models share one coordinate base." if distinct == 1 else " The %d past models do not share one coordinate base.") % count


def turn_between(first, second):
    gap = abs(first - second) % 360
    return min(gap, 360 - gap)


def measure(past, built, limits):
    """R836. The four measures of one built plot against its past link: (held, difference as text) per measure. Held in exact
    decimals, so a row that stands on a limit holds it."""
    found = {}
    if None in (past["x"], past["y"], built["x"], built["y"]):
        found["position"] = (False, None, "no number")
    else:
        square = (built["x"] - past["x"]) ** 2 + (built["y"] - past["y"]) ** 2
        found["position"] = (square <= (limits["positionMm"] / 1000) ** 2, square.sqrt() * 1000, None)
    if None in (past["z"], built["z"]):
        found["ffl"] = (False, None, "no number")
    else:
        found["ffl"] = (abs(built["z"] - past["z"]) * 1000 <= limits["fflMm"], abs(built["z"] - past["z"]) * 1000, None)
    if None in (past["rotation"], built["rotation"]):
        found["rotation"] = (False, None, "no number")
    else:
        gap = turn_between(past["rotation"], built["rotation"])
        found["rotation"] = (gap <= limits["rotationDeg"], gap, None)
    if None in (past["mirror"], built["mirror"]):
        found["mirror"] = (False, None, "no value")
    else:
        found["mirror"] = (past["mirror"] == built["mirror"], None, None)
    return found


def tenth_up(value):
    """One decimal, rounded up, so a difference just over a limit never reads as the limit."""
    return str(value.quantize(Decimal("0.1"), rounding=ROUND_CEILING))


def share_cell(held, of, limit, largest=None):
    if of == 0:
        return "no plot built, fail", False
    passed = held * 100 >= limit * of
    text = "%d of %d, %s percent" % (held, of, "%d.%d" % divmod(held * 1000 // of, 10))
    if largest is not None:
        text += ", largest %s mm" % tenth_up(largest)
    return text + (", pass" if passed else ", fail"), passed


def count_cell(built, past, limit):
    if past is None:
        return "UNKNOWN, the past read holds no count", None
    if built is None:
        return "UNKNOWN, the built read holds no count", None
    passed = abs(built - past) * 100 <= limit * past
    return "%d of %d, %s" % (built, past, "pass" if passed else "fail"), passed


def compare_model(code, nbh, past, built, limits):
    """R835 to R837. One neighbourhood: the cell and the verdict of each column, the rows that fail and the notes. built is None
    when no read of the neighbourhood was given, which counts 0 of everything."""
    links_only = code in limits["linksOnlyModels"]
    built_links = built["links"] if built else {}
    fails, verdicts, cells = [], {}, {}
    joined = [plot for plot in past["links"] if plot in built_links]
    for plot in sorted(set(past["links"]) - set(built_links), key=lambda plot: past["links"][plot][0]["line"]):
        fails.append((nbh, "plots", "past", past["links"][plot][0]["line"], "not built"))
    cells["plots"], verdicts["plots"] = share_cell(len(joined), len(past["links"]), limits["plotsBuiltPercent"])
    held = dict.fromkeys(MEASURES, 0)
    largest = {"position": Decimal(0), "ffl": Decimal(0)}
    twice = 0
    for plot in joined:
        rows = built_links[plot]
        if len(rows) > 1:
            twice += 1
            fails += [(nbh, name, "built", row["line"], "two links") for name in MEASURES for row in rows]
            continue
        for name, (ok, gap, why) in measure(past["links"][plot][0], rows[0], limits).items():
            if gap is not None and name in largest:
                largest[name] = max(largest[name], gap)
            if ok:
                held[name] += 1
            else:
                fails.append((nbh, name, "built", rows[0]["line"], why or ("" if gap is None else tenth_up(gap) + (" deg" if name == "rotation" else " mm"))))
    for name in MEASURES:
        if name == "ffl" and links_only:
            cells[name], verdicts[name] = NOT_COUNTED, True
            continue
        cells[name], verdicts[name] = share_cell(held[name], len(joined), limits["heldPercent"], largest.get(name) if joined else None)
    for name in COUNTS:
        if links_only or (name == "yards" and code not in limits["yardModels"]):
            cells[name], verdicts[name] = NOT_COUNTED, True
            continue
        cells[name], verdicts[name] = count_cell(built["counts"][name] if built else 0, past["counts"][name], limits["countPercent"])
    fails = [fail for fail in fails if cells.get(fail[1]) != NOT_COUNTED]
    result = "fail" if False in verdicts.values() else "UNKNOWN" if None in verdicts.values() else "pass"
    whole = result == "pass" and len(joined) == len(past["links"]) and not any(largest.values())         and all(held[name] == len(joined) for name in MEASURES if cells[name] != NOT_COUNTED)         and all(built["counts"][name] == past["counts"][name] for name in COUNTS if cells[name] != NOT_COUNTED)
    note = "- %s: past links with no plot number %d, built links with no plot number %d, built plots not in the past model %d, built plots with two links %d." % (
        nbh, past["unnamed"], built["unnamed"] if built else 0, len(set(built_links) - set(past["links"])), twice)
    line = "| %s | %s | %s | %s |" % (nbh, code, " | ".join(cells[name] for name in ("plots",) + MEASURES + COUNTS), result)
    return {"line": line, "result": result, "whole": whole, "fails": sorted(fails, key=lambda fail: (fail[1], fail[2], fail[3])), "note": note}


def limit_lines(limits, models):
    """R836. The pass level as the settings hold it, each line with its source."""
    held = plain(limits["heldPercent"])
    rows = [("Plots", "%s percent of the plots of the past model built" % plain(limits["plotsBuiltPercent"]), "plotsBuiltPercent"),
            ("Position", "%s percent of the built plots within %s mm, in plan" % (held, plain(limits["positionMm"])), "positionMm"),
            ("FFL", "%s percent of the built plots within %s mm, by the origin Z of the villa link" % (held, plain(limits["fflMm"])), "fflMm"),
            ("Rotation", "%s percent of the built plots within %s degree" % (held, plain(limits["rotationDeg"])), "rotationDeg"),
            ("Mirror", "%s percent of the built plots right" % held, "heldPercent"),
            ("Floors, yards, walls, levels", "each within %s percent of the past count" % plain(limits["countPercent"]), "countPercent")]
    lines = ["| Column | Limit | Source |", "|---|---|---|"] + ["| %s | %s | %s |" % (name, text, limits[key + "Source"]) for name, text, key in rows]
    yards = [code for code in models if code in limits["yardModels"]]
    links_only = [code for code in models if code in limits["linksOnlyModels"]]
    return lines + ["", "Yards are counted on %s. Plots, position, rotation and mirror only are counted on %s. Floors are the floors that are no yard." % (
        ", ".join(yards) or "no model", ", ".join(links_only) or "no model")]


def render(limits, pasts, builts, built_given, bases):
    """R839. The whole page and the rows that fail. pasts is code to (neighbourhood, read), builts is neighbourhood to read."""
    models = sorted(pasts, key=lambda code: (pasts[code][0], code))
    lines = ["# The compare", "", "Written by python/compare_built.py, never by hand. Counts, shares and differences in mm only, no coordinate, no level, no plot number.",
             "", "## The coordinate base of the past models", "", base_lines(bases), "", "## The pass level", ""] + limit_lines(limits, sorted(pasts))
    lines += ["", "## The past models against themselves", "", TABLE_HEAD]
    own = [compare_model(code, pasts[code][0], pasts[code][1], pasts[code][1], limits) for code in models]
    lines += [row["line"] for row in own]
    lines += ["", "%d of %d past models give 100 percent in every column that is counted." % (sum(1 for row in own if row["whole"]), len(own))]
    lines += ["", "## The built side against the past models", ""]
    lines += ["A built folder was given, %d of its folders are named as a neighbourhood below." % len(builts) if built_given
              else "No built folder was given, every neighbourhood counts 0 built.", "", TABLE_HEAD]
    by_nbh = {pasts[code][0]: code for code in models}
    results, fails, notes = [], [], []
    for nbh in sorted(set(limits["neighbourhoods"]) | set(by_nbh)):
        if nbh not in by_nbh:
            lines.append("| %s | none | %s | UNKNOWN |" % (nbh, " | ".join([NO_PAST] * 9)))
            results.append("UNKNOWN")
            continue
        row = compare_model(by_nbh[nbh], nbh, pasts[by_nbh[nbh]][1], builts.get(nbh), limits)
        lines.append(row["line"])
        results.append(row["result"])
        fails += row["fails"]
        notes.append(row["note"])
    lines += ["", "%d of %d neighbourhoods pass, %d fail, %d UNKNOWN." % (results.count("pass"), len(results), results.count("fail"), results.count("UNKNOWN"))]
    lines += ["", "## Notes", ""] + notes
    return "\n".join(lines) + "\n", fails


def write_rows(path, fails):
    """R839. The rows that fail, as the line of links.csv on the side named and the difference, no plot number."""
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle, lineterminator="\n")
        writer.writerow(["neighbourhood", "column", "side", "row", "difference"])
        writer.writerows(fails)


def run(args):
    limits = load_limits(project_profile.read_object(args.config))
    codes = neighbourhood_of(project_profile.read_object(args.areas), limits["job"])
    key = Path(args.key) if args.key else project_profile.work_root() / "answer-key"
    folders = sorted(folder for folder in key.iterdir() if folder.is_dir()) if key.is_dir() else []
    if not folders:
        raise CompareError("the answer key folder holds no past model")
    pasts = {}
    for folder in folders:
        if folder.name not in codes:
            raise CompareError("the area codes name no neighbourhood for the past model %s" % folder.name)
        read = read_side(folder, "past")
        if any(len(rows) > 1 for rows in read["links"].values()):
            raise CompareError("the past model %s names a plot twice" % folder.name)
        pasts[folder.name] = (codes[folder.name], read)
    if len({nbh for nbh, _ in pasts.values()}) != len(pasts):
        raise CompareError("two past models name one neighbourhood")
    builts = {}
    if args.built:
        if not Path(args.built).is_dir():
            raise CompareError("the built folder is not there")
        known = {nbh for nbh, _ in pasts.values()}
        builts = {folder.name: read_side(folder, "built") for folder in sorted(Path(args.built).iterdir()) if folder.is_dir() and folder.name in known}
    text, fails = render(limits, pasts, builts, bool(args.built), [base_of(folder) for folder in folders])
    write_rows(Path(args.rows) if args.rows else project_profile.work_root() / "compare" / "rows.csv", fails)
    Path(args.out).write_bytes(text.encode("utf-8"))
    return text


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--built", help="the folder of the built reads, one folder per neighbourhood")
    parser.add_argument("--key", help="the answer key folder, answer-key of the work root when not given")
    parser.add_argument("--config", default=str(ROOT / "config" / "project.json"))
    parser.add_argument("--areas", default=str(ROOT / "config" / "area_codes.json"))
    parser.add_argument("--out", default=str(ROOT / "steps" / "compare.md"))
    parser.add_argument("--rows", help="the file of the rows that fail, compare/rows.csv of the work root when not given")
    args = parser.parse_args(argv)
    try:
        text = run(args)
    except (CompareError, project_profile.ProfileError, OSError) as error:
        print("compare stopped: %s" % error, file=sys.stderr)
        return 2
    print(text.split("## The past models against themselves")[1].split("## Notes")[0].strip())
    return 0


if __name__ == "__main__":
    sys.exit(main())
