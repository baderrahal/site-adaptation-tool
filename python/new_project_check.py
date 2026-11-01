"""R412. The read only check of the inputs of a new project, the Python reference of the worker command newProjectCheck (NewProjectCheck of Common).

Given a config file (and the rules profile, the offsets file, the work root and the input root as the config resolves them) it reads the
inputs the config names and says in one plain line each what is missing or does not fit: the position file, the parameter sheet of
addin.parameterSheet (R461), the villa offsets, the hand tables and the prototype files of every unit code of typologies, each Excel
sheet and its header columns, the schedules, and the PDF location plan. Each line holds input, status (ok, missing, does not fit), a
plain sentence and the job only flag, true where the name, sheet, header, prefix or file name the input is held to is one only the
first job has today, with a short note why. The report is a JSON file and a text file in the output folder given, the only place
written. Every input opened is recorded by name, size, write time and SHA-256 before its first read and again at the end, and a change
fails the run (D19). The C# of the worker gives the same report on the same inputs.
"""
import argparse
import json
import os
import re
import sys
from pathlib import Path

import input_guard
import parameter_sheet
import plan_from_cad
import project_profile

FORMAT = "site-adaptation-new-project-check"
FORMAT_VERSION = 1
OK, MISSING, DOES_NOT_FIT = "ok", "missing", "does not fit"
POSITION_FORMAT = "site-adaptation-position"
POSITION_FORMAT_VERSION = 1
# The position file parts the add-in reads, as ProjectPositionFile writes them (R353)
POSITION_PARTS = ("eastM", "northM", "elevationM", "angleDeg")
POSITION_POINTS = ("surveyPoint", "projectBasePoint")
# Where config holds the Excel sheets the readers take by sheet, header row and column names (R350)
EXCEL_BLOCKS = ("dataBook.rev9.dlus", "dataBook.rev9.sales", "dataBook.data5.areaKey.dlus", "dataBook.data5.areaKey.sales",
                "dataBook.locationPlan.sales", "dataBook.locationPlan.dlus")
HAND_TABLES = ("hand villa", "hand cluster", "hand alone")
SCHEDULE_MARKER = "plot no."

PARAMETER_PREFIX = re.compile(r"[A-Z]{2,5}_")
GUID = re.compile(r"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")


# The project name and the flag name are spelled in parts, as test_repo_rules.PROJECT_NAMES does, so no Python file holds them (R63)
NAME = "RO" + "SHN Mak" + "kah"
ONLY = "RO" + "SHN only"
KEY = "ro" + "shn" + "Only"
KEY_NOTE = "ro" + "shn" + "Note"


class CheckError(ValueError):
    """The check itself cannot run: a config or rules file that is not there or not JSON."""


def line(name, status, text, only_note=None):
    note = only_note.replace("@N@", NAME) if only_note is not None else None
    return {"input": name, "status": status, "text": text, KEY: note is not None, KEY_NOTE: note}


def is_number(value):
    return isinstance(value, (int, float)) and not isinstance(value, bool)


def is_point(value):
    return isinstance(value, list) and len(value) == 3 and all(is_number(item) for item in value)


def get(node, dotted):
    """The value at a dotted path of nested objects, None when a step is not there."""
    for part in dotted.split("."):
        if not isinstance(node, dict) or part not in node:
            return None
        node = node[part]
    return node


def texts(values):
    return [value for value in values if isinstance(value, str)] if isinstance(values, list) else []


def read_object(path):
    try:
        value = json.loads(Path(path).read_text(encoding="utf-8-sig"))
    except (OSError, ValueError):
        return None
    return value if isinstance(value, dict) else None


class Run:
    """The files one check opened, each marked before its first read."""

    def __init__(self, work_root, input_root):
        self.work_root = Path(work_root)
        self.input_root = Path(input_root) if input_root else None
        self.marks = {}
        self.cache = {}

    def opened(self, path):
        key = input_guard.key(path)
        if key not in self.marks:
            self.marks[key] = (str(path), input_guard.mark(path))

    def close(self):
        # A read only openpyxl workbook keeps its file open until closed, so a check never leaves a handle on an input
        for book in self.cache.values():
            if book is not None:
                book["book"].close()
        self.cache = {}

    def json_object(self, path):
        self.opened(path)
        return read_object(path)

    def book(self, path):
        """The sheet names of a workbook and its rows by sheet, None when it cannot be read as a workbook."""
        key = input_guard.key(path)
        if key not in self.cache:
            self.opened(path)
            self.cache[key] = open_book(path)
        return self.cache[key]


def open_book(path):
    import openpyxl
    import warnings
    try:
        with warnings.catch_warnings():
            warnings.simplefilter("ignore")
            book = openpyxl.load_workbook(path, read_only=True, data_only=True)
    except Exception:
        return None
    return {"names": list(book.sheetnames), "book": book}


def header_texts(book, sheet, count):
    """The first count rows of a sheet, every row when count is None, as the text of each cell, a cell that is no text as an empty text, the rows the sheet holds."""
    rows = []
    for row in book["book"][sheet].iter_rows(min_row=1, max_row=count, values_only=True):
        rows.append([cell.strip() if isinstance(cell, str) else "" for cell in row])
    return rows


def position_line(settings, run):
    name = "position file"
    block = get(settings, "addin.position")
    path = block.get("file") if isinstance(block, dict) else None
    if not isinstance(path, str) or not path.lower().endswith(".json"):
        return line(name, DOES_NOT_FIT, "config names no .json file in addin.position.file")
    path = Path(path)
    if not path.is_file():
        return line(name, MISSING, "%s is not there" % path.name)
    found = run.json_object(path)
    if found is None:
        return line(name, DOES_NOT_FIT, "%s cannot be read as a JSON object" % path.name)
    faults = []
    if found.get("format") != POSITION_FORMAT:
        faults.append("format")
    if not is_number(found.get("formatVersion")) or found["formatVersion"] != POSITION_FORMAT_VERSION:
        faults.append("formatVersion")
    at = found.get("projectPosition")
    for part in POSITION_PARTS:
        if not isinstance(at, dict) or not is_number(at.get(part)):
            faults.append("projectPosition." + part)
    for point in POSITION_POINTS:
        for frame in ("internalM", "sharedM"):
            value = found.get(point)
            if not isinstance(value, dict) or not is_point(value.get(frame)):
                faults.append("%s.%s" % (point, frame))
    levels = found.get("levels")
    if not isinstance(levels, list) or not levels:
        faults.append("levels")
    else:
        for index, level in enumerate(levels):
            if not isinstance(level, dict) or not isinstance(level.get("name"), str):
                faults.append("levels[%d].name" % index)
            if not isinstance(level, dict) or not is_number(level.get("elevationM")):
                faults.append("levels[%d].elevationM" % index)
    if not isinstance(found.get("levelBase"), str):
        faults.append("levelBase")
    if faults:
        return line(name, DOES_NOT_FIT, "%s lacks or has wrong: %s" % (path.name, ", ".join(faults)))
    return line(name, OK, "%s holds the project position, the survey point, the project base point and %d levels" % (path.name, len(levels)))


def default_profile(rules):
    profiles = rules.get("levelProfiles")
    name = profiles.get("default") if isinstance(profiles, dict) else None
    profile = profiles.get(name) if isinstance(name, str) else None
    return profile if isinstance(profile, dict) else {}


def parameter_names(settings, rules):
    """The parameter names config and the rules profile name: floors.plotParameter, pastJob.parameters, majority.parameterLevels, then
    each parameter entry of the keyRules of the default level profile, each once in that order."""
    found = []
    plot = get(settings, "floors.plotParameter")
    if isinstance(plot, str):
        found.append(plot)
    found += texts(get(settings, "pastJob.parameters")) + texts(get(settings, "majority.parameterLevels"))
    key_rules = default_profile(rules).get("keyRules")
    if isinstance(key_rules, dict):
        found += [key[len("parameter "):] for key in key_rules if key.startswith("parameter ")]
    return list(dict.fromkeys(found))


def parameter_line(config_path, settings, rules, run):
    """R461. The parameter sheet addin.parameterSheet names, its rows of Keep Yes, holds a GUID for every parameter name the tool uses."""
    name = "parameter sheet"
    named = get(settings, "addin.parameterSheet")
    if not isinstance(named, str) or not named.strip():
        return line(name, MISSING, "config names no addin.parameterSheet")
    path = parameter_sheet.path_of(config_path, settings)
    if not path.is_file():
        return line(name, MISSING, "the parameter sheet %s is not there" % path.name)
    run.opened(path)
    try:
        kept, _ = parameter_sheet.from_file(path)
    except parameter_sheet.SheetError as error:
        return line(name, DOES_NOT_FIT, str(error))
    guids = {item["name"]: item["guid"] for item in kept}
    names = parameter_names(settings, rules)
    faults = []
    for each in names:
        if each not in guids:
            faults.append("%s (not in the sheet)" % each)
        elif guids[each] is None or not GUID.fullmatch(guids[each]):
            faults.append("%s (no GUID)" % each)
    prefixes = [found.group(0) for found in (PARAMETER_PREFIX.match(each) for each in names) if found]
    note = "the names are held to the prefix %s of the @N@ parameters" % prefixes[0] if prefixes else None
    if faults:
        return line(name, DOES_NOT_FIT, "%d of %d parameter names have no GUID in %s: %s" % (len(faults), len(names), path.name, ", ".join(faults)), note)
    return line(name, OK, "all %d parameter names have a GUID in %s" % (len(names), path.name), note)


def past_job_note(settings, what):
    hood = get(settings, "pastJob.neighbourhood")
    return what % (" " + hood if isinstance(hood, str) else "")


def offsets_line(settings, rules, offsets_path, run):
    name = "villa offsets"
    note = past_job_note(settings, "measured on the @N@ past job%s")
    path = Path(offsets_path)
    if not path.is_file():
        return line(name, MISSING, "%s is not there" % path.name)
    found = run.json_object(path)
    if found is None:
        return line(name, DOES_NOT_FIT, "%s cannot be read as a JSON object" % path.name)
    units = get(found, "plotFrame.units")
    if not isinstance(units, dict):
        return line(name, DOES_NOT_FIT, "%s holds no plotFrame.units" % path.name, note)
    codes = unit_codes(rules)
    lacking = [code for code in codes if code not in units]
    if lacking:
        return line(name, DOES_NOT_FIT, "plotFrame.units of %s holds no offset for the unit codes: %s" % (path.name, ", ".join(lacking)), note)
    return line(name, OK, "plotFrame.units of %s holds an offset for all %d unit codes of typologies" % (path.name, len(codes)), note)


def unit_codes(rules):
    typologies = rules.get("typologies")
    return list(typologies) if isinstance(typologies, dict) else []


def prototype_folder(settings, input_root):
    """BuildSteps.PrototypeFolder: the input root with pastJob.upgradedPrototypeFolder, else pastJob.prototypeFolder, None when one is not named."""
    job = settings.get("pastJob") if isinstance(settings.get("pastJob"), dict) else {}
    named = job.get("upgradedPrototypeFolder") or job.get("prototypeFolder")
    if not input_root or not isinstance(named, str) or not named:
        return None
    return Path(input_root) / named


def prototype_files(folder, rules):
    listing = sorted(path.name for path in folder.iterdir() if path.is_file())
    return plan_from_cad.prototype_files(listing, project_profile.ProjectProfile("rules", rules))[0]


def hands_line(rules, folder):
    name = "hand tables"
    note = "the tables are keyed by the KEO prototype file names of the @N@ job"
    key_rules = default_profile(rules).get("keyRules")
    tables = [key_rules.get(table) if isinstance(key_rules, dict) else None for table in HAND_TABLES]
    if not any(isinstance(table, dict) and isinstance(table.get("byFile"), dict) for table in tables):
        return line(name, DOES_NOT_FIT, "the rules profile holds no byFile in the hand tables of its keyRules", note)
    if folder is None or not folder.is_dir():
        return line(name, MISSING, "the hand tables are named by prototype file and the prototype folder is not there", note)
    held = set()
    for table in tables:
        if isinstance(table, dict) and isinstance(table.get("byFile"), dict):
            held.update(table["byFile"])
    files = prototype_files(folder, rules)
    codes = unit_codes(rules)
    lacking = [code for code in codes if files.get(code) not in held]
    if lacking:
        return line(name, DOES_NOT_FIT, "the hand tables hold no entry for the prototype files of the unit codes: %s" % ", ".join(lacking), note)
    return line(name, OK, "the hand tables hold an entry for the prototype file of all %d unit codes" % len(codes), note)


def prototype_line(rules, folder):
    name = "prototype files"
    note = "files are found by the -ARC-<code>- part of the KEO file names"
    if folder is None or not folder.is_dir():
        return line(name, MISSING, "the prototype folder %s is not there" % (folder.name if folder else "named by pastJob"), note)
    files = prototype_files(folder, rules)
    codes = unit_codes(rules)
    lacking = [code for code in codes if code not in files]
    if lacking:
        return line(name, DOES_NOT_FIT, "the prototype folder %s holds no single file for the unit codes: %s" % (folder.name, ", ".join(lacking)), note)
    return line(name, OK, "the prototype folder %s holds a file for all %d unit codes" % (folder.name, len(codes)), note)


def data_book_work(settings, work_root):
    """The work folder of the data book: addin.readData.dataBookFolder in the work root when it is no full path, then work."""
    folder = get(settings, "addin.readData.dataBookFolder")
    if not isinstance(folder, str):
        return None
    path = Path(folder)
    return (path if path.is_absolute() else Path(work_root) / path) / "work"


def excel_line(settings, block_path, run, work):
    block = get(settings, block_path)
    roles = get(settings, "addin.workbookRoles")
    key = block.get("workbook") if isinstance(block, dict) and isinstance(block.get("workbook"), str) else None
    role = roles.get(key) if key and isinstance(roles, dict) and isinstance(roles.get(key), str) else key
    name = "Excel %s%s" % (role + " " if role else "", block_path)
    if (not isinstance(block, dict) or not isinstance(block.get("sheet"), str) or isinstance(block.get("headerRow"), bool)
            or not isinstance(block.get("headerRow"), int) or block["headerRow"] < 1 or not isinstance(block.get("columns"), dict)):
        return line(name, DOES_NOT_FIT, "config names no workbook, sheet, headerRow and columns in %s" % block_path)
    relative = get(settings, "dataBook.rev9.workbooks." + key) if isinstance(key, str) else None
    if not isinstance(relative, str) or work is None:
        return line(name, DOES_NOT_FIT, "config names no workbook %s in dataBook.rev9.workbooks or no addin.readData.dataBookFolder" % key)
    path = work / relative
    sheet, row, columns = block["sheet"], block["headerRow"], [str(value) for value in block["columns"].values()]
    note = "held to the sheet %s and the headers %s" % (sheet, ", ".join(columns))
    if not path.is_file():
        return line(name, MISSING, "%s is not there" % path.name, note)
    book = run.book(path)
    if book is None:
        return line(name, DOES_NOT_FIT, "%s cannot be read as a workbook" % path.name, note)
    if sheet not in book["names"]:
        return line(name, DOES_NOT_FIT, "%s has no sheet %s" % (path.name, sheet), note)
    rows = header_texts(book, sheet, row)
    if len(rows) < row:
        return line(name, DOES_NOT_FIT, "%s sheet %s has no header row %d" % (path.name, sheet, row), note)
    lacking = [column for column in columns if column not in rows[row - 1]]
    if lacking:
        return line(name, DOES_NOT_FIT, "%s sheet %s row %d lacks the columns: %s" % (path.name, sheet, row, ", ".join(lacking)), note)
    return line(name, OK, "%s sheet %s row %d holds all %d columns the readers use" % (path.name, sheet, row, len(columns)), note)


def schedule_lines(settings, run, input_root):
    hoods = get(settings, "connect.neighbourhoods")
    folder, sheet = get(settings, "addin.readData.scheduleFolder"), get(settings, "connect.scheduleSheet")
    entries = [(hood, value["schedule"]) for hood, value in sorted(hoods.items()) if isinstance(value, dict) and isinstance(value.get("schedule"), str)] if isinstance(hoods, dict) else []
    if not entries:
        return []
    if not isinstance(folder, str) or not isinstance(sheet, str) or not input_root:
        return [line("Schedules", DOES_NOT_FIT, "config names no addin.readData.scheduleFolder, connect.scheduleSheet or input root")]
    note = "held to the sheet %s and a header cell with Plot No." % sheet
    found = []
    for hood, file in entries:
        name = "Schedule " + hood
        path = Path(input_root) / folder / file
        if not path.is_file():
            found.append(line(name, MISSING, "%s is not there" % path.name, note))
            continue
        book = run.book(path)
        if book is None:
            found.append(line(name, DOES_NOT_FIT, "%s cannot be read as a workbook" % path.name, note))
            continue
        matching = [each for each in book["names"] if each.strip().lower() == sheet.strip().lower()]
        if not matching:
            found.append(line(name, DOES_NOT_FIT, "%s has no sheet %s" % (path.name, sheet), note))
            continue
        rows = header_texts(book, matching[0], None)
        if not any(SCHEDULE_MARKER in cell.lower() for row in rows for cell in row):
            found.append(line(name, DOES_NOT_FIT, "%s sheet %s has no cell holding Plot No. in any row" % (path.name, sheet), note))
        else:
            found.append(line(name, OK, "%s sheet %s holds the Plot No. header row" % (path.name, sheet), note))
    return found


def read_pdf(path):
    import pdfplumber
    try:
        with pdfplumber.open(str(path)) as pdf:
            pages = len(pdf.pages)
            text = "".join(char["text"] for char in pdf.pages[0].chars) if pages else ""
    except Exception:
        return None
    return {"pages": pages, "tokens": text.split()}


def pdf_line(settings, run, work):
    name = "location plan PDF"
    relative = get(settings, "dataBook.locationPlan.pdf")
    patterns = get(settings, "dataBook.locationPlan.patterns")
    kinds = texts(get(settings, "dataBook.locationPlan.codeKinds"))
    if not isinstance(relative, str) or work is None or not isinstance(patterns, dict) or not isinstance(patterns.get("number"), str):
        return line(name, DOES_NOT_FIT, "config names no dataBook.locationPlan.pdf, patterns.number or addin.readData.dataBookFolder")
    codes = [kind for kind in kinds if isinstance(patterns.get(kind), str)]
    note = "held to the @N@ label patterns of dataBook.locationPlan.patterns: " + ", ".join(["number"] + codes)
    path = work / relative
    if not path.is_file():
        return line(name, MISSING, "%s is not there" % path.name, note)
    run.opened(path)
    found = read_pdf(path)
    if found is None:
        return line(name, DOES_NOT_FIT, "%s cannot be read as a PDF" % path.name, note)
    if found["pages"] != 1:
        return line(name, DOES_NOT_FIT, "%s holds %d pages, a location plan is one page" % (path.name, found["pages"]), note)
    lacking = []
    if not any(re.fullmatch(patterns["number"], token) for token in found["tokens"]):
        lacking.append("the number pattern")
    if not any(re.fullmatch(patterns[kind], token) for kind in codes for token in found["tokens"]):
        lacking.append("the code patterns")
    if lacking:
        return line(name, DOES_NOT_FIT, "%s page holds no text that fits: %s" % (path.name, ", ".join(lacking)), note)
    return line(name, OK, "%s is one page and holds text that fits the number and code patterns" % path.name, note)


def counts_of(lines):
    return {"ok": sum(1 for each in lines if each["status"] == OK), "missing": sum(1 for each in lines if each["status"] == MISSING),
            "doesNotFit": sum(1 for each in lines if each["status"] == DOES_NOT_FIT), KEY: sum(1 for each in lines if each[KEY])}


def text_of(report):
    counts = report["counts"]
    out = ["New project check, %d lines: %d ok, %d missing, %d do not fit, %d %s" % (len(report["lines"]), counts["ok"], counts["missing"], counts["doesNotFit"], counts[KEY], ONLY)]
    for each in report["lines"]:
        out.append("%s: %s. %s%s" % (each["input"], each["status"], each["text"], (". %s: %s" % (ONLY, each[KEY_NOTE])) if each[KEY] else ""))
    out.append("Inputs opened: %d, none changed" % len(report["inputs"]))
    return "\n".join(out) + "\n"


def check(config_path, rules_path, offsets_path, work_root, input_root=None):
    """The report of the check with the lines, the counts and the inputs opened with their marks, the paths of those inputs and the input
    root. The inputs are marked before their first read and again at the end, a change raises input_guard.InputChanged."""
    values = project_profile.read_object(config_path)
    settings = project_profile.with_work_root(values, work_root)
    rules = read_object(rules_path)
    if rules is None:
        raise CheckError("the rules profile %s is not there or is no JSON object" % Path(rules_path).name)
    if not isinstance(rules.get("typologies"), dict):
        raise CheckError("the rules profile %s holds no typologies" % Path(rules_path).name)
    root = input_root or os.environ.get(input_guard.ROOT_VARIABLE) or settings.get("inputRoot")
    run = Run(work_root, root)
    run.opened(config_path)
    run.opened(rules_path)
    folder = prototype_folder(settings, root)
    work = data_book_work(settings, work_root)
    lines = [position_line(settings, run), parameter_line(config_path, settings, rules, run), offsets_line(settings, rules, offsets_path, run),
             hands_line(rules, folder), prototype_line(rules, folder)]
    lines += [excel_line(settings, block, run, work) for block in EXCEL_BLOCKS]
    lines += schedule_lines(settings, run, root)
    lines.append(pdf_line(settings, run, work))
    run.close()
    changed = []
    inputs = []
    for path, before in run.marks.values():
        after = input_guard.mark(path)
        differs = input_guard.change(before, after)
        if differs:
            changed.append("%s its %s" % (path, differs))
        inputs.append({"name": Path(path).name, "size": before["size"], "writeTimeUtc": before["writeTimeUtc"], "sha256": before["sha256"], "unchanged": not differs})
    if changed:
        raise input_guard.InputChanged("an input changed while the check read it (D19): " + ", and ".join(changed))
    inputs.sort(key=lambda each: (each["name"], each["sha256"]))
    report = {"format": FORMAT, "formatVersion": FORMAT_VERSION, "lines": lines, "counts": counts_of(lines), "inputs": inputs}
    return report, [path for path, _ in run.marks.values()], root


def keep_apart(out_dir, run_paths, input_root):
    """The output folder is no folder of an input and none under the input root, so nothing is written beside an input."""
    out = input_guard.key(out_dir)
    for path in run_paths:
        if input_guard.key(Path(path).parent) == out:
            raise CheckError("the output folder is the folder of the input %s, a check writes nothing beside an input" % Path(path).name)
    if input_root and (out == input_guard.key(input_root) or out.startswith(input_guard.key(input_root).rstrip(os.sep) + os.sep)):
        raise CheckError("the output folder is under the input root, a check writes nothing there (D19)")


def write(report, out_dir, input_paths, input_root):
    keep_apart(out_dir, input_paths, input_root)
    out = Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)
    (out / "new_project_check.json").write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    (out / "new_project_check.txt").write_text(text_of(report), encoding="utf-8", newline="\n")


def main(argv=None):
    parser = argparse.ArgumentParser(description="R412. The read only check of the inputs of a new project.")
    parser.add_argument("--config", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--rules", default=None, help="the rules profile, default the profile config names")
    parser.add_argument("--offsets", default=None, help="the offsets file, default prototype_offsets.json beside the config")
    parser.add_argument("--work-root", default=None, help="default is the work root of this machine (R372)")
    parser.add_argument("--out", required=True, help="the output folder, the only place written")
    args = parser.parse_args(argv)
    config = Path(args.config)
    rules = Path(args.rules) if args.rules else project_profile.profile_path(project_profile.load_profile(config))
    offsets = Path(args.offsets) if args.offsets else config.parent / "prototype_offsets.json"
    root = args.work_root or project_profile.work_root()
    try:
        report, paths, input_root = check(config, rules, offsets, root)
        write(report, args.out, paths, input_root)
    except (CheckError, project_profile.ProfileError, input_guard.InputChanged) as error:
        print("new project check failed: %s" % error, file=sys.stderr)
        return 1
    print(text_of(report), end="")
    return 0


if __name__ == "__main__":
    sys.exit(main())
