"""R343. docs/design/, the written design of the add-in, holds its sections, the labels of the approved mock in order and no coordinate."""
import json
import re
from bisect import bisect_left
from decimal import Decimal, ROUND_HALF_UP
from pathlib import Path

import pytest

import record_parts

ROOT = Path(__file__).resolve().parents[2]
DESIGN = ROOT / "docs" / "design"
MOCK = ROOT / "docs" / "addin-mock.md"
RULES = ROOT / "docs" / "rules"
SETTINGS = ROOT / "config" / "project.json"

SECTIONS = ["Option B and the two parts", "Folder structure", "The eight buttons", "From each input file to Revit",
            "The plan builder steps Phase 4 moves to C#", "Settings", "Every screen and file of the mock", "Install and the unattended runs",
            "Risks", "Planned rule ids, conflicts and questions"]

# Every label of docs/addin-mock.md, item by item in the order of the mock
MOCK_LABELS = [
    "Site Adaptation", "Setup", "1 Set up site", "2 Read data", "3 Check", "Build", "4 Place villas", "5 Floors and ramps", "6 Levels", "7 Walls",
    "Output", "8 Report",
    "Dockable pane", "Site Adaptation", "Project", "Settings", "Rules", "Level profile", "Input files", "Baseplan", "Villa blocks", "Curbs",
    "Road surface", "DLUS and sales", "Plot numbers", "Prototypes", "Scope", "Whole project", "Neighbourhood", "Plot list",
    "Dry run first, change nothing", "Steps", "Done", "Done with", "Ready", "Needs step",
    "1 Set up site", "Empty project found", "Existing site file", "What the project has now", "Survey point", "Project base point", "Levels",
    "Floor types and wall types", "Shared parameters", "Units", "What the tool will set", "Coordinate system", "Survey point", "Angle to true north",
    "Levels", "Floor types", "Wall types", "Shared parameters", "Units", "Check points from CAD", "Point", "CAD", "Revit", "Diff", "Pass limit 1 mm.",
    "On an existing site file", "Cancel", "Dry run", "Apply",
    "3 Check, NBH", "Zoom to plot", "Export to Excel", "Save overrides", "All", "Plot cut", "Floor", "Hand", "Unit code", "Position", "Level",
    "plots follow every rule", "Plot", "NBH", "Unit", "Check", "Tool value (rule)", "Past job", "Rule", "Use", "Rule", "Past", "Skip",
    "_Levels_", "Plot No", "Unit", "Cluster", "Road at front", "El @ Entry", "FFL", "SSL", "Height", "Steps", "Source", "Levels", "Clusters",
    "Overrides used", "Settings",
    "_Report.xlsx", "Step", "Done", "Skipped", "Override", "Issues", "Counts agree across outputs", "Item", "Revit", "Excel", "Links", "Floors",
    "Levels", "Rules applied", "Summary", "Placed", "Skipped", "Overrides", "Issues", "Rules",
]

# A run of six digits or more that is no part of a name, a file or a hash, such as an easting or northing
COORDINATE = re.compile(r"(?<![\w.\\/-])\d{6,}(?:\.\d+)?(?![\w-])")
# A number as the records write it, with or without thousands commas, never inside a name, a date, a file or a hash
NUMBER = re.compile(r"(?<![\w.\\/-])(\d{1,3}(?:,\d{3})+|\d+)(?:\.(\d+))?(?![\w-])")
# The check types of the mock chips, and the type the filled check table found besides them
CHECK_TYPES = ("Plot cut", "Floor", "Hand", "Unit code", "Position", "Level", "Parameter")
# A schema name and a field name of Revit Extensible Storage hold letters, digits and underscore, starting with a letter
SCHEMA_NAME = re.compile(r"^[A-Za-z][A-Za-z0-9_]*$")
# Keys of a plan whose numbers are angles or shares, no coordinate and no level
NOT_PLACES = ("rotation", "angle", "Deg", "share", "slope", "Percent")


def section(text, number):
    start = text.index("\n## %d. " % number)
    end = text.find("\n## %d. " % (number + 1), start + 1)
    return text[start:] if end < 0 else text[start:end]


def labels_in_order(text, labels):
    """The labels not found in text in the order given, each searched after the one before it."""
    at, lost = 0, []
    for label in labels:
        found = text.find(label, at)
        if found < 0:
            lost.append(label)
        else:
            at = found + len(label)
    return lost


def table_after(text, marker):
    """The rows of the first Markdown table after marker, each a dict by the header cells, backticks taken off."""
    part = text[text.index(marker):]
    lines = []
    for line in part.splitlines()[1:]:
        if line.startswith("|"):
            lines.append(line)
        elif lines:
            break
    header = [cell.strip() for cell in lines[0].strip("|").split("|")]
    return [dict(zip(header, [cell.strip().replace("`", "") for cell in line.strip("|").split("|")])) for line in lines[2:]]


def fixed(value, decimals):
    """R385 as section 5 of the design gives it: the 15 significant digit text of the value as a decimal, rounded half away from zero."""
    rounded = float(Decimal(format(value, ".15g")).quantize(Decimal(1).scaleb(-decimals), rounding=ROUND_HALF_UP))
    return 0 if rounded == 0 else rounded


def written_numbers(text):
    """The numbers of a text that could carry a coordinate or a level: two decimals or more and 10 or over, or six digits or more,
    each with the size of the last unit written."""
    found = []
    for match in NUMBER.finditer(text):
        whole, decimals = match.group(1).replace(",", ""), match.group(2) or ""
        value = Decimal(whole + ("." + decimals if decimals else ""))
        if (len(decimals) >= 2 and value >= 10) or len(whole.lstrip("0")) >= 6:
            found.append((match.group(0), value, Decimal(1).scaleb(-len(decimals))))
    return found


def leaks(text, real):
    """The numbers of text that equal a real value to their last written unit, real a sorted list of Decimal."""
    hits = []
    for shown, value, unit in written_numbers(text):
        at = bisect_left(real, value - unit)
        if at < len(real) and abs(real[at] - value) < unit:
            hits.append(shown)
    return hits


def real_numbers(node, key="", out=None):
    """Every coordinate and level of a JSON output, the numbers of 10 or over, numbers held as text too, angles and shares left out."""
    out = [] if out is None else out
    if isinstance(node, dict):
        for name, item in node.items():
            real_numbers(item, str(name), out)
    elif isinstance(node, list):
        for item in node:
            real_numbers(item, key, out)
    elif not any(word in key for word in NOT_PLACES):
        if isinstance(node, str) and re.fullmatch(r"-?\d+\.\d+", node.strip()):
            node = float(node)
        if isinstance(node, float) and abs(node) >= 10:
            out.append(Decimal(repr(float(node))))
    return out


def round_outputs():
    """The coordinates and levels of the outputs of the round under siteBuild.folder of config: the filled mock, the NBH 012 plan
    and its mock values, and the survey point, base point, position and levels of the 7 key reads. None when the folder is not
    on disk, as on the Linux runner."""
    folder = Path(json.loads(SETTINGS.read_text(encoding="utf-8"))["siteBuild"]["folder"])
    if not (folder / "design" / "mock_filled.md").exists():
        return None
    real = [value for shown, value, unit in written_numbers((folder / "design" / "mock_filled.md").read_text(encoding="utf-8")) if value >= 100]
    for name in ("plan_012.json", "mock_values.json"):
        path = folder / "design" / "work" / name
        if path.exists():
            real_numbers(json.loads(path.read_text(encoding="utf-8")), out=real)
    for path in sorted((folder / "reads").glob("R?/testhost_R?_prototypes.json")):
        for document in json.loads(path.read_text(encoding="utf-8")).get("documents") or []:
            real_numbers({"survey": document.get("surveyPoint"), "base": document.get("basePoint"),
                          "position": (document.get("site") or {}).get("projectPosition"), "levels": (document.get("site") or {}).get("levels")}, out=real)
    return sorted(set(real))


def records():
    """The texts this round writes its design and its state into: the design, the licences, PROGRESS, the top log entry and the round file."""
    log = (ROOT / "steps" / "log.md").read_text(encoding="utf-8")
    texts = {"docs/design": record_parts.text(DESIGN), "docs/licences.md": (ROOT / "docs" / "licences.md").read_text(encoding="utf-8"),
             "steps/PROGRESS.md": (ROOT / "steps" / "PROGRESS.md").read_text(encoding="utf-8"), "steps/log.md top": re.split(r"^## ", log, flags=re.MULTILINE)[1]}
    texts["steps/history/round-addin"] = record_parts.text(record_parts.ROUND_ADDIN)
    texts["steps/investigations/round-addin.md"] = (ROOT / "steps" / "investigations" / "round-addin.md").read_text(encoding="utf-8")
    return texts


def test_r343_the_design_holds_its_ten_sections_in_order():
    text = record_parts.text(DESIGN)
    places = [text.find("\n## %d. %s" % (number, name)) for number, name in enumerate(SECTIONS, start=1)]
    assert all(place >= 0 for place in places), [name for name, place in zip(SECTIONS, places) if place < 0]
    assert places == sorted(places)


def test_r343_every_label_of_the_mock_is_in_the_mock_and_in_section_7_in_the_order_of_the_mock():
    mock = MOCK.read_text(encoding="utf-8")
    assert [label for label in MOCK_LABELS if label not in mock] == []
    assert labels_in_order(section(record_parts.text(DESIGN), 7), MOCK_LABELS) == []


def test_r343_the_order_check_finds_a_label_out_of_place():
    assert labels_in_order("Setup then Build then Output", ["Setup", "Build", "Output"]) == []
    assert labels_in_order("Build then Setup", ["Setup", "Build"]) == ["Build"]


def test_r343_the_design_holds_no_coordinate_and_shows_coordinates_and_levels_as_placeholders():
    text = record_parts.text(DESIGN)
    assert COORDINATE.findall(text) == []
    assert "[coordinate]" in text and "[level]" in text
    assert "mock_filled.md" in text


def test_r343_the_coordinate_pattern_finds_a_bare_easting_and_leaves_names_alone():
    assert COORDINATE.findall("E 512345.67 and N 2400000.5") == ["512345.67", "2400000.5"]
    assert COORDINATE.findall("01101-PSN-ZZZ-XXX-DWG-TU-005000.pdf, 12,424 overrides, 2,576 MiB, 0.0 mm, sha256 f5f417bb0834") == []


def test_r343_the_records_hold_no_coordinate_or_level_of_the_outputs_of_the_round():
    real = round_outputs()
    if real is None:
        pytest.skip("the outputs of the round are not on this machine, siteBuild.folder of config/project.json")
    assert len(real) > 1000
    found = {name: leaks(text, real) for name, text in records().items()}
    assert {name: hits for name, hits in found.items() if hits} == {}


def test_r343_the_leak_check_finds_a_real_level_or_coordinate_however_it_is_written_and_leaves_counts_alone():
    real = sorted({Decimal("171.5114"), Decimal("570048.532"), Decimal("2364316.517")})
    assert leaks("the FFL 171.511 m", real) == ["171.511"]
    assert leaks("the FFL 171.51 m", real) == ["171.51"]
    assert leaks("E 570,048.53 and N 2,364,316.517", real) == ["570,048.53", "2,364,316.517"]
    assert leaks("N 2364316 and E 570049", real) == ["2364316", "570049"]
    assert leaks("E 570050", real) == []
    # A count, a size, a gap, a tie of R385, a name, a date or a hash is no level and no coordinate
    assert leaks("336 links, 1,003 floors, 2,571 MiB, 3,056,293 bytes, 0.0077 m, 12.3455, 171.5 m, 01101-PSN-ZZZ-XXX-DWG-TU-005000.pdf, "
                 "2026-10-26, 7061ac0d, R385", real) == []


def test_r343_the_rounding_ties_the_design_gives_hold_under_r385_and_differ_from_python_round():
    text = section(record_parts.text(DESIGN), 5)
    against = re.findall(r"(\d+\.\d+) to (\d+(?:\.\d+)?) against (\d+\.\d+)", text)
    assert len(against) == 4
    for value, by_round, by_rule in against:
        assert round(float(value), 3) == float(by_round)
        assert fixed(float(value), 3) == float(by_rule)
    ties = re.findall(r"(\d+\.\d+) to (\d+\.\d+)", text[text.index("The ties under R385"):].split("\n", 1)[0])
    assert [value for value, _ in ties] == ["12.3455", "15.1235", "1.0005", "2.0465", "7.0625"]
    assert [fixed(float(value), 3) for value, _ in ties] == [float(result) for _, result in ties]
    assert [round(float(value), 3) for value, _ in ties] != [float(result) for _, result in ties]


def test_r343_the_rounding_rule_drops_float_noise_and_minus_zero():
    assert fixed(0.30000000000000004, 3) == 0.3
    assert fixed(150.38 - 150.0, 4) == 0.38
    assert fixed(-0.0004, 3) == 0 and str(fixed(-0.0004, 3)) == "0"


def test_r343_every_schema_name_of_the_design_is_letters_digits_and_underscore():
    text = record_parts.text(DESIGN)
    names = re.findall(r"\bschema (SiteAdaptation\w*(?:\.\w+)*)", text) + re.findall(r"\b(SiteAdaptation_\w+)", text)
    assert {"SiteAdaptation_Placed", "SiteAdaptation_Settings"} <= set(names)
    assert [name for name in names if not SCHEMA_NAME.match(name)] == []
    assert not SCHEMA_NAME.match("SiteAdaptation.Placed")


def test_r343_past_writes_the_form_the_build_reads_for_every_check_and_every_override_field():
    import plan_from_book
    import plan_rules
    rows = table_after(record_parts.text(DESIGN), "What Past writes, by check and field:")
    read = set(plan_rules.FIELDS) | set(plan_rules.KEY_FIELDS)
    fields = [(row, field.strip()) for row in rows for field in row["Field"].split(",")]
    assert [check for check in CHECK_TYPES if not any(row["Check"].startswith(check) for row in rows)] == []
    assert sorted((read | set(plan_from_book.LEFT)) - {field for _, field in fields}) == []
    assert {row["Offered"] for row in rows} == {"yes", "no"}
    offered = [field for row, field in fields if row["Offered"] == "yes"]
    assert [field for field in offered if field not in read and not field.startswith(plan_rules.PARAMETER)] == []
    assert [field for row, field in fields if row["Offered"] == "no" and field not in plan_from_book.LEFT and field != "none"] == []
    for row in rows:
        assert "[level]" not in row["Past writes"] and "[coordinate]" not in row["Past writes"]
        if row["Check"].startswith(("Level", "Position")) and row["Offered"] == "yes" and row["Field"] != "steps":
            assert "gapToRule" in row["Past writes"], row["Check"]


def test_r343_the_report_summary_of_the_design_has_the_five_columns_of_the_mock():
    mock = MOCK.read_text(encoding="utf-8")
    columns = re.search(r"Summary table with columns ([^,]+(?:, [^,]+)*?), one row per button", mock).group(1).split(", ")
    assert columns == ["Step", "Done", "Skipped", "Override", "Issues"]
    report = section(record_parts.text(DESIGN), 7).split("### 7.6", 1)[1].split("### 7.7", 1)[0]
    assert "the five columns of the mock" in report and "six columns" not in report


def mock_sentences():
    """Every sentence of items 1 to 6 of the mock with its own punctuation, the bullets of item 2 apart, in the order of the mock."""
    body = MOCK.read_text(encoding="utf-8").split("```text", 1)[1].split("```", 1)[0]
    items = re.findall(r"^\s*\d\. (.*?)(?=^\s*\d\. |\Z)", body, flags=re.S | re.M)
    return [piece for item in items for piece in re.split(r"(?<=\.)\s+|\s+- ", " ".join(item.split())) if piece]


def review_numbers(text, marker):
    """The item numbers of the first column of the table after marker, in the order written."""
    return [int(re.match(r"\d+", row["Item"]).group(0)) for row in table_after(text, marker)]


def test_r343_every_sentence_of_the_mock_stands_in_section_7_word_for_word_with_its_punctuation_in_order():
    sentences = mock_sentences()
    assert len(sentences) == 35
    assert "Level Excel, file <NBH>_Levels_<profile>.xlsx." in sentences
    assert labels_in_order(section(record_parts.text(DESIGN), 7), sentences) == []


def test_r343_the_sentence_check_finds_a_colon_made_a_period():
    assert labels_in_order("four sections in this order.", ["four sections in this order:"]) == ["four sections in this order:"]


def test_r343_the_rounding_rule_reads_the_15_digit_text_with_its_exponent_and_4a_tests_float_noise_in_csharp():
    text = section(record_parts.text(DESIGN), 5)
    assert "NumberStyles.Float" in text
    assert [format(value, ".15g") for value in (0.1 + 0.2 - 0.3, 0.000004)] == ["5.55111512312578e-17", "4e-06"]
    assert fixed(0.1 + 0.2 - 0.3, 3) == 0 and fixed(0.000004, 4) == 0
    row = [row for row in table_after(text, "### The Python changes for parity") if row["Id"] == "R385"][0]
    assert "R385_float_noise_does_not_move_a_value" in row["Tests named"]
    assert "the order of a sum cannot move a value" not in text


def test_r343_the_parity_tolerances_are_named_per_field_and_small_against_the_pass_limit():
    rows = table_after(section(record_parts.text(DESIGN), 5), "### Parity tolerances")
    by_kind = {row["Kind"]: row for row in rows}
    assert {"Points and lengths", "Levels", "Areas", "Angles", "No geometry"} <= set(by_kind)
    for kind, unit in (("Points and lengths", "m"), ("Levels", "m"), ("Areas", "m2"), ("Angles", "degree")):
        tolerance = re.fullmatch(r"(\d+\.\d+) (m|m2|degree)", by_kind[kind]["Before rounding"])
        assert tolerance and tolerance.group(2) == unit, kind
        assert Decimal(tolerance.group(1)) <= Decimal("0.000001"), kind
        assert "one unit" in by_kind[kind]["After rounding"] and "listed" in by_kind[kind]["After rounding"], kind
    assert by_kind["No geometry"]["Before rounding"] == "none" and by_kind["No geometry"]["After rounding"] == "exact"


def test_r343_every_build_is_numbered_by_the_schedule_fit_and_the_control_point_fit_is_a_check():
    text = record_parts.text(DESIGN)
    planned = {row["Id"]: row["Rule"] for row in table_after(section(text, 10), "Planned rules, taken from")}
    assert "schedule fit" in planned["R351"] and "schedule fit" in planned["R352"]
    assert "check" in planned["R387"] and "otherwise" in planned["R387"]
    assert "gives location_plan.py the fit of R351" not in text


def test_r343_the_steps_files_hold_no_control_byte_but_tab_and_line_feed():
    found = {}
    for path in sorted((ROOT / "steps").rglob("*.md")):
        data = path.read_bytes()
        bad = [at for at, byte in enumerate(data) if (byte < 32 and byte not in (9, 10)) or byte == 127]
        if bad:
            found[str(path.relative_to(ROOT))] = bad[:5]
    assert found == {}


def test_r343_the_review_tables_of_pr_49_number_every_item_once():
    text = record_parts.text(record_parts.ROUND_ADDIN)
    first = review_numbers(text, "### Review of PR 49, the breaker and the claim-checker")
    assert first == list(range(1, 28))
    last = review_numbers(text, "### Last pass on PR 49")
    assert last == list(range(1, 20))


def test_r343_the_round_plan_gives_phase_6_the_ids_after_the_python_changes():
    plan = record_parts.text(record_parts.ROUND_ADDIN).split("### Order of work", 1)[0]
    assert "Phase 6 R389 to R399" in plan and "Phase 6 R385 to R399" not in plan


def test_r343_every_point_only_a_revit_run_settles_is_on_the_open_list_with_the_phase_that_measures_it():
    text = record_parts.text(DESIGN)
    part = text[text.index("### Open until a Revit run"):]
    items = re.findall(r"^\d+\. (.*)$", part.split("\n## ", 1)[0], flags=re.MULTILINE)
    assert len(items) >= 8
    assert [item for item in items if not re.match(r"Phase 5[a-e]", item)] == []


def test_r343_every_rule_the_design_names_below_the_planned_range_is_in_the_rules_file_and_the_planned_ones_lie_in_phases_4_and_5():
    text = record_parts.text(DESIGN)
    rules = set(re.findall(r"\*\*(R\d+) ", record_parts.text(RULES)))
    named = {int(found) for found in re.findall(r"\bR(\d{1,3})\b", text)}
    assert [number for number in sorted(named) if number < 345 and "R%d" % number not in rules] == []
    planned = {int(found) for found in re.findall(r"^\| R(\d{3}) \|", section(text, 10), flags=re.MULTILINE)}
    # R401 of Round ADDIN 4g1 takes the next free id after R400 of Round REC, so an id from R400 up is named only once its rule is written
    written = {number for number in planned | named if number >= 400 and "R%d" % number in rules}
    assert planned - written == set(range(345, 389)) and 401 in written
    assert [number for number in sorted(named) if 399 < number and number not in written] == []
