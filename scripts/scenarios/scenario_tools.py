"""The parts of the scenario tools that hold no path of a machine, so a test can hold them. Every real value stays under the work root."""
import hashlib
import json
import os
import re
from pathlib import Path

SCENARIOS = {
    "S1": "raw inputs only, every past output removed",
    "S2": "as S1, footprint drawing removed",
    "S3": "as S1, zone and neighbourhood words changed to Phase and Block",
    "S4": "as S1, no LandXML and no Civil 3D surfaces, 2D CAD only",
    "S5": "as S1, plot numbers in another style",
}
NUMBER_STYLE = "LOT/%s-X"
# The run folder of a scenario run on what its own first run proposed, as a person pressing Confirm on all of it
CONFIRMED_RUN = "_confirmed"
# Folders of the source that hold past outputs, by the truth audit, left out of every scenario
PAST_FOLDERS = {"01_output", "data_book", "position"}


def work_root(environ=None, home=None):
    """As python/project_profile.py work_root."""
    environ = os.environ if environ is None else environ
    machine = Path(environ["APPDATA"]) / "SiteAdaptation" / "machine.json" if environ.get("APPDATA") else None
    if machine is not None and machine.is_file():
        root = json.loads(machine.read_text(encoding="utf-8-sig")).get("workRoot")
        if root:
            return Path(root)
    return Path(home if home is not None else Path.home()) / "Documents" / "SiteAdaptation"


def sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def manifest_of(folder):
    """SHA-256 and size of every file under a folder, by its path inside the folder."""
    folder = Path(folder)
    return {path.relative_to(folder).as_posix(): {"sha256": sha256(path), "size": path.stat().st_size} for path in sorted(folder.rglob("*")) if path.is_file()}


def manifest_faults(folder, manifest):
    """Each file changed, gone or added since the manifest was written."""
    now = manifest_of(folder)
    faults = ["gone: " + name for name in manifest if name not in now]
    faults += ["added: " + name for name in now if name not in manifest]
    faults += ["changed: " + name for name in manifest if name in now and now[name]["sha256"] != manifest[name]["sha256"]]
    return faults


def is_past(relative):
    return any(part.lower() in PAST_FOLDERS for part in Path(relative).parts)


def raw_files(source):
    """Every file of the source that is not a past output, as paths inside it."""
    source = Path(source)
    return [path.relative_to(source) for path in sorted(source.rglob("*")) if path.is_file() and not is_past(path.relative_to(source))]


def files_of(scenario, files):
    """The raw files a scenario keeps. S2 drops the footprint drawing, S4 every LandXML."""
    if scenario == "S2":
        return [path for path in files if "footprint" not in path.name.lower()]
    if scenario == "S4":
        return [path for path in files if path.suffix.lower() != ".xml" and "landxml" not in (part.lower() for part in path.parts)]
    return list(files)


WORD_RULES = [
    (re.compile(r"(?<![A-Za-z])neighbou?rhoods?(?![A-Za-z])|(?<![A-Za-z])nbhs?(?![A-Za-z])", re.IGNORECASE), "Block"),
    (re.compile(r"(?<![A-Za-z])zones?(?![A-Za-z])", re.IGNORECASE), "Phase"),
]


def changed_words(text):
    """Zone and neighbourhood words as Phase and Block, upper case kept upper."""
    for pattern, word in WORD_RULES:
        text = pattern.sub(lambda found: word.upper() if found.group(0).isupper() else word, text)
    return text


# Text a person reads in a drawing. Dictionaries and extension records also hold strings under codes 1 and 3, they are never touched
TEXT_ENTITIES = {"TEXT", "MTEXT", "ATTRIB", "ATTDEF"}


def rewrite_dxf(text, change_text=None, change_layer=None):
    """A DXF with the values of its text entities and its layer names passed through the two changes. A DXF is lines of code then value."""
    lines = text.split("\n")
    in_layer_table = False
    entity = ""
    for index in range(0, len(lines) - 1, 2):
        code = lines[index].strip()
        value = lines[index + 1]
        if code == "0":
            entity = value.strip()
            if entity == "ENDTAB":
                in_layer_table = False
        elif code == "2" and entity == "TABLE" and value.strip() == "LAYER":
            in_layer_table = True
        elif change_text and code in ("1", "3") and entity in TEXT_ENTITIES:
            lines[index + 1] = change_text(value)
        elif change_layer and (code == "8" or (code == "2" and in_layer_table and entity == "LAYER")):
            lines[index + 1] = change_layer(value)
    return "\n".join(lines)


def styled(number):
    return NUMBER_STYLE % number


# A line of an MTEXT holding only a number: after a format code or a line break, up to the next line break or the closing brace
MTEXT_NUMBER = re.compile(r"(?:(?<=\\P)|(?<=\x3B)|(?<=\{))(\d+[A-Za-z]?)(?=\\P|\}|\s*$)")


def style_text(numbers):
    """A change that takes a known plot number to the other style, as a whole text or as a whole line of an MTEXT, any other text as it is."""
    def change(value):
        if value.strip() in numbers:
            return styled(value.strip())
        return MTEXT_NUMBER.sub(lambda found: styled(found.group(1)) if found.group(1) in numbers else found.group(0), value)
    return change


def columns_of_numbers(rows, numbers, share=0.5):
    """The column indexes where at least the share of the filled cells below the first row are known plot numbers."""
    found = {}
    filled = {}
    for row in rows[1:]:
        for index, cell in enumerate(row):
            if cell is None or str(cell).strip() == "":
                continue
            filled[index] = filled.get(index, 0) + 1
            if number_text(cell) in numbers:
                found[index] = found.get(index, 0) + 1
    return {index for index, count in found.items() if count >= share * filled[index]}


def number_text(cell):
    if isinstance(cell, float) and cell.is_integer():
        return str(int(cell))
    return str(cell).strip()


def plain_value(field):
    """A field of a plan is an object of its value and source."""
    return field.get("value") if isinstance(field, dict) else field


def answer_rows(plans):
    """From the plans of past models read for compare: per official number its villa type, mirror and area. Plots of two plans are listed."""
    rows = {}
    twice = []
    for plan in plans:
        for plot in plan.get("plots", []):
            number = str(plot.get("plot"))
            row = {"villaType": plain_value(plot.get("unit")), "mirror": plain_value(plot.get("mirrored")), "area": plan.get("neighbourhood")}
            if not isinstance(row["mirror"], bool) or not isinstance(row["villaType"], str):
                raise ValueError("plot %s holds no villa type or mirror" % number)
            if number in rows:
                twice.append(number)
            rows[number] = row
    return rows, twice


def holdout_rows(plans, holdout):
    """Per plot of the held out areas: type, mirror and each level the past model holds, in metres."""
    rows = {}
    for plan in plans:
        if plan.get("neighbourhood") not in holdout:
            continue
        for plot in plan.get("plots", []):
            levels = {name: plain_value(field) for name, field in (plot.get("levels") or {}).items()}
            rows[str(plot.get("plot"))] = {"villaType": plain_value(plot.get("unit")), "mirror": plain_value(plot.get("mirrored")), "levels": levels}
    return rows


def split_holdout(rows, holdout):
    """The rows to tune on, and the rows of the held out areas, which no lane sees."""
    keep = {number: row for number, row in rows.items() if row["area"] not in holdout}
    held = {number: row for number, row in rows.items() if row["area"] in holdout}
    return keep, held


def scenario_answer(rows, scenario):
    plots = {(styled(number) if scenario == "S5" else number): {"villaType": row["villaType"], "mirror": row["mirror"]} for number, row in rows.items()}
    return {"scenario": scenario, "plotCount": len(plots), "plots": plots}


def score(answer, found_plots):
    """Counts only. Each right plot is looked up by its number in what the tool read."""
    by_number = {str(row.get("number")): row for row in found_plots}
    right = answer["plots"]
    numbers = sum(1 for number in right if number in by_number)
    villa = sum(1 for number, row in right.items() if number in by_number and by_number[number].get("villaType") == row["villaType"])
    mirror = sum(1 for number, row in right.items() if number in by_number and by_number[number].get("mirror") is row["mirror"])
    return {"right": len(right), "read": len(found_plots), "numbers": numbers, "villaType": villa, "mirror": mirror}


# S3 is the one folder whose group words are known, as make_scenarios.py wrote them
S3_WORDS = ["Phase", "Block"]


def words_score(name, levels):
    """Group words right, for the folder that has an answer. A word the tool should not have found counts against it."""
    if name != "S3":
        return "not scored"
    words = [row.get("word") for row in levels]
    right = sum(1 for index, word in enumerate(S3_WORDS) if index < len(words) and words[index] == word)
    return "%d of %d" % (right, len(S3_WORDS))


def lines_of(results):
    """What each data check said, for the work root only. It quotes layers and words of the real drawings, so it stays out of git."""
    out = []
    for name in sorted(SCENARIOS):
        result = results.get(name)
        if result:
            out += ["", "## %s" % name, ""]
            out += ["- %s, %s: %s" % (row["need"], row["mark"], row["line"]) for row in result["needs"]]
    return "\n".join(out) + "\n"


def areas_read(rows):
    """The areas of the plot table of a run, the key every later button works by."""
    return len({row.get("nbhId") for row in rows if row.get("nbhId")})


def score_rows(results):
    """One row per scenario of a set of runs, counts and marks only."""
    out = ["| Scenario | What it is | Ran | Built from | Plots read | Numbers right | Villa type right | Mirror right | Group words right | Marks: strong, usable, weak, missing | Copies unchanged | Areas read | Areas on the sheet, second check |",
           "|---|---|---|---|---|---|---|---|---|---|---|---|---|"]
    for name in sorted(SCENARIOS):
        result = results.get(name)
        if not result:
            out.append("| %s | %s | not run | UNKNOWN | 0 of 0 | 0 of 0 | 0 of 0 | 0 of 0 | not scored | none | UNKNOWN | UNKNOWN | UNKNOWN |" % (name, SCENARIOS[name]))
            continue
        marks = [row["mark"] for row in result["needs"]]
        scored = result["score"]
        out.append("| %s | %s | %s | %s | %d of %d | %d of %d | %d of %d | %d of %d | %s | %s | %s | %s | %s |" % (
            name, SCENARIOS[name], result["ran"], result.get("built", "UNKNOWN"), scored["read"], scored["right"], scored["numbers"], scored["right"], scored["villaType"], scored["right"], scored["mirror"], scored["right"],
            words_score(name, result.get("levels", [])),
            ", ".join(str(marks.count(mark)) for mark in ("strong", "usable", "weak", "missing")), "yes" if not result["faults"] else "NO, %d files" % len(result["faults"]), result.get("areas", "UNKNOWN"), result.get("sheetAreas", "UNKNOWN")))
    return out


def not_read_line(results):
    """How many files each run could not read, as a run that skipped one reads as done with the same score. Counts only."""
    return "Files not read: " + ", ".join("%s %s" % (name, (results.get(name) or {}).get("notRead", "UNKNOWN")) for name in sorted(SCENARIOS)) + "."


def mark_rows(results):
    """The mark of each need per scenario."""
    out = ["| Need | S1 | S2 | S3 | S4 | S5 |", "|---|---|---|---|---|---|"]
    needs = []
    for name in sorted(SCENARIOS):
        for row in (results.get(name) or {}).get("needs", []):
            if row["need"] not in needs:
                needs.append(row["need"])
    for need in needs:
        cells = []
        for name in sorted(SCENARIOS):
            rows = [row for row in (results.get(name) or {}).get("needs", []) if row["need"] == need]
            cells.append(rows[0]["mark"] if rows else "not run")
        out.append("| %s | %s |" % (need, " | ".join(cells)))
    return out


BUILD_RUN = "_build"


def build_counts(plan, seconds=None):
    """Counts of one build plan. A value counts as built when it has a source other than missing, a missing value is empty and its plot is marked."""
    plots = plan.get("plots", [])
    values = [value for plot in plots for value in plot.get("values", {}).values()]
    return {
        "plots": len(plots), "built": sum(1 for value in values if value.get("source") != "missing"), "missing": sum(1 for value in values if value.get("source") == "missing"),
        "marked": sum(1 for plot in plots if plot.get("marked")), "blocking": len(plan.get("blocking", [])), "issues": len(plan.get("issues", [])),
        "past": sum(1 for value in values if value.get("source") == "past"), "seconds": seconds,
    }


def build_rows(results):
    """The build plan of each scenario, counts only. Built and Missing count values of the plan, not plots."""
    out = ["| Scenario | Plots planned | Built | Missing | Plots marked | Blocking | Issues | Copied from the past | Seconds |", "|---|---|---|---|---|---|---|---|---|"]
    for name in sorted(SCENARIOS):
        count = (results.get(name) or {}).get("build")
        if not count:
            out.append("| %s | no build made | | | | | | | |" % name)
        else:
            seconds = "UNKNOWN" if count["seconds"] is None else count["seconds"]
            out.append("| %s | %d | %d | %d | %d | %d | %d | %d | %s |" % (name, count["plots"], count["built"], count["missing"], count["marked"], count["blocking"], count["issues"], count["past"], seconds))
    return out


def table(results, confirmed=None):
    """The tables of steps/scenarios.md. Counts and marks only, this file is in a public repo. confirmed holds the runs on what each first run
    proposed, as a person pressing Confirm on all of it, left out when there are none."""
    out = ["# Scenarios", "",
           "Written by scripts/scenarios/score_scenarios.py, never by hand. Stage 1 on each folder under the work root, scored against its right answer. Counts and marks only, the folders stay out of git and what each data check said is in scenarios\\report.md under the work root. The right answers leave the hold-out neighbourhoods out.", "",
           "## With no confirmed input", ""]
    out += score_rows(results) + ["", not_read_line(results), "", "## Mark of each need", ""] + mark_rows(results)
    if any((result or {}).get("build") for result in results.values()):
        out += ["", "## Build plan, what was built and what was missing", "",
                "siteBuild on the plot table of each run above (scripts/scenarios/run_build.py). No past file and no override list is read. A value with no rule stays missing and its plot is marked.", ""]
        out += build_rows(results)
    if confirmed and any(confirmed.values()):
        out += ["", "## With a confirmed profile", "", "Each scenario again on what its own first run proposed, as a person pressing Confirm on all of it (scripts/scenarios/confirmed_profile.py). Nothing comes from a past file or an answer, a proposal that is wrong stays wrong and scores so.", ""]
        out += score_rows(confirmed) + ["", not_read_line(confirmed), "", "## Mark of each need with a confirmed profile", ""] + mark_rows(confirmed)
    return "\n".join(out) + "\n"
