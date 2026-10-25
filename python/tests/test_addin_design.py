"""R343. docs/addin-design.md, the written design of the add-in, holds its sections, the labels of the approved mock in order and no coordinate."""
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DESIGN = ROOT / "docs" / "addin-design.md"
MOCK = ROOT / "docs" / "addin-mock.md"
RULES = ROOT / "docs" / "rules.md"

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


def test_r343_the_design_holds_its_ten_sections_in_order():
    text = DESIGN.read_text(encoding="utf-8")
    places = [text.find("\n## %d. %s" % (number, name)) for number, name in enumerate(SECTIONS, start=1)]
    assert all(place >= 0 for place in places), [name for name, place in zip(SECTIONS, places) if place < 0]
    assert places == sorted(places)


def test_r343_every_label_of_the_mock_is_in_the_mock_and_in_section_7_in_the_order_of_the_mock():
    mock = MOCK.read_text(encoding="utf-8")
    assert [label for label in MOCK_LABELS if label not in mock] == []
    assert labels_in_order(section(DESIGN.read_text(encoding="utf-8"), 7), MOCK_LABELS) == []


def test_r343_the_order_check_finds_a_label_out_of_place():
    assert labels_in_order("Setup then Build then Output", ["Setup", "Build", "Output"]) == []
    assert labels_in_order("Build then Setup", ["Setup", "Build"]) == ["Build"]


def test_r343_the_design_holds_no_coordinate_and_shows_coordinates_and_levels_as_placeholders():
    text = DESIGN.read_text(encoding="utf-8")
    assert COORDINATE.findall(text) == []
    assert "[coordinate]" in text and "[level]" in text
    assert "mock_filled.md" in text


def test_r343_the_coordinate_pattern_finds_a_bare_easting_and_leaves_names_alone():
    assert COORDINATE.findall("E 512345.67 and N 2400000.5") == ["512345.67", "2400000.5"]
    assert COORDINATE.findall("01101-PSN-ZZZ-XXX-DWG-TU-005000.pdf, 12,424 overrides, 2,576 MiB, 0.0 mm, sha256 f5f417bb0834") == []


def test_r343_every_rule_the_design_names_below_the_planned_range_is_in_the_rules_file_and_the_planned_ones_lie_in_phases_4_and_5():
    text = DESIGN.read_text(encoding="utf-8")
    rules = set(re.findall(r"\*\*(R\d+) ", RULES.read_text(encoding="utf-8")))
    named = {int(found) for found in re.findall(r"\bR(\d{1,3})\b", text)}
    assert [number for number in sorted(named) if number < 345 and "R%d" % number not in rules] == []
    planned = {int(found) for found in re.findall(r"^\| R(\d{3}) \|", section(text, 10), flags=re.MULTILINE)}
    assert planned == set(range(345, 385))
    assert [number for number in sorted(named) if number > 399] == []
