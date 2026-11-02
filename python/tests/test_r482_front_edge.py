"""R482. An override frontEdge of a plot names the edge of its baseplan ring that is its front, on a notched plot made up in code (R10). R482_FrontEdgeTests.cs of the
C# holds the same plot and the same values, so the two sides give one front."""
import argparse
import json
from pathlib import Path

import pytest
from shapely.geometry import LineString, Polygon

import cad_links
import corner_front
import majority
import plan_from_book
import plan_from_past
import plan_rules
import project_profile
import test_level_floor_steps
from test_level_floor_steps import DP2_FILE, TH1_FILE, VD2_FILE, made_rules

# Drawn clockwise from the east end of the notch, so the ring is walked as drawn: edge 0 the notch of 3 m, edge 1 its side of 5 m, edge 2 the front of 9 m, 3 west, 4 back, 5 east
NOTCHED = [(72.0, 5.0), (69.0, 5.0), (69.0, 0.0), (60.0, 0.0), (60.0, 25.0), (72.0, 25.0)]
ROADS = [LineString([(-50.0, -3.0), (100.0, -3.0)]), LineString([(-3.0, -10.0), (-3.0, 40.0)])]
NUMBERS = [str(index) for index in range(1, 7)]
UNITS = {"1": "VD2", "2": "DP2", "3": "DP2", "4": "DP2", "5": "DP2", "6": "DP2"}
# Both the front and the notch look south, the first of the ring wins the tie of the Orientation, as on the real plots the notch by a hair
SALES = {"1": {"Orientation": "WS"}, "6": {"Orientation": "WS"}}
FRONT = {"6": {"edge": 2}}
NOT_WRITTEN = "the plot is not written, it lacks "
NO_FACE = "the plot has no face, so no ring holds the edge (R482)"


def faces_of():
    faces = {number: Polygon([(index * 12.0, 0.0), (index * 12.0 + 12.0, 0.0), (index * 12.0 + 12.0, 25.0), (index * 12.0, 25.0)]).buffer(0)
             for index, number in enumerate(NUMBERS[:-1])}
    faces["6"] = Polygon(NOTCHED).buffer(0)
    return faces


def rows_of(numbers=NUMBERS):
    return {number: {"locationPlanNumber": number, "typologyRev9": UNITS.get(number, "DP2"), "footprintMirrored": "no", "zone": "Zone 2"} for number in numbers}


def settings_and_profile():
    return project_profile.load_settings(), made_rules()


def fronts(edges=None, numbers=("6",), faces=None, level=None):
    settings, profile = settings_and_profile()
    limits = cad_links.front_limits(profile, settings.get("analysis"))
    return plan_from_book.fronts(list(numbers), rows_of(), faces_of() if faces is None else faces, ROADS, limits, [], 0.0, profile, settings.get("siteBuild")["corner"], SALES, edges)


def list_file(tmp_path, entries):
    path = tmp_path / "list.json"
    path.write_text(json.dumps({"overrides": entries}), encoding="utf-8")
    return str(path)


def entry(plot="6", value=None, use=None):
    found = {"plot": plot, "field": "frontEdge", "value": {"edge": 2} if value is None else value, "rule": None, "source": "parsons", "model": "made.rvt", "key": "imc",
             "decision": "parsons frontEdge", "reason": "the notch is no front, made up"}
    if use is not None:
        found.update(byHand=True, use=use)
    return found


def build(monkeypatch, tmp_path, entries=None, faceless=()):
    """The row of test_level_floor_steps with plot 6 notched, no road level at the notch, built through the list as rule_inputs reads it."""
    settings, profile = settings_and_profile()
    numbers = NUMBERS + list(faceless)
    rows = rows_of(numbers)
    faces = faces_of()
    clusters = [{"code": "Duplex_DP2", "plots": ["2", "3"], "source": "footprint"}]
    # The notch stands 5 m in, so no level at y = 5 leaves every front on y = 0 its level
    monkeypatch.setattr(plan_from_book, "road_level", lambda surfaces, point, snap: (None, None) if 4.0 < point[1] < 6.0 else (10.0 + point[0] / 100.0, "road"))
    args = argparse.Namespace(level_profile=None, overrides=list_file(tmp_path, entries or [entry("99", {"edge": 0})]), override_source=None, override_model=[], override_fields=None)
    rules = plan_from_book.rule_inputs(args, settings, profile, numbers, rows, faces, ROADS, SALES, clusters)
    floors = plan_from_past.load_floors(settings)
    site = {"levels": {floors["levels"][kind]: 0.0 for kind in ("generic", "yard", "ramp")}, "surveyPoint": {"position": [0, 0, 0], "sharedPosition": [0, 0, 0]}, "angleToTrueNorthDeg": 0.0}
    outlines = json.loads((Path(test_level_floor_steps.__file__).resolve().parents[2] / "config" / "floor_outlines.json").read_text(encoding="utf-8"))
    files = {"DP2": DP2_FILE, "VD2": VD2_FILE, "TH1-IM": TH1_FILE}
    trace = {}
    plan = plan_from_book.build(numbers, rows, {}, faces, ROADS, clusters, profile, settings, {"units": {}}, files, "folder", site, [("road", None)], outlines, "N1", SALES, True, rules, trace=trace)
    return {"plan": {item["plot"]: item for item in plan["plots"]}, "missing": {item["plot"]: item for item in plan["missing"]}, "trace": trace, "rules": rules, "files": files, "rows": rows}


def test_r482_the_ring_is_walked_from_its_first_vertex_clockwise_and_the_rule_takes_the_notch():
    assert plan_from_book.face_ring(faces_of()["6"]) == NOTCHED
    item = fronts()["6"]
    assert item["frontRule"] == "orientation"
    assert {item["frame"]["left"], item["frame"]["right"]} == {NOTCHED[0], NOTCHED[1]}


def test_r482_the_frame_of_the_edge_is_built_as_the_rule_builds_a_frame_rule_named_front_edge():
    item = fronts(FRONT)["6"]
    frame = item["frame"]
    assert item["frontRule"] == "frontEdge"
    # Seen from the road south of it the plot looks north, so the left end is the west end
    assert (frame["left"], frame["right"], frame["n"], frame["r"]) == ((60.0, 0.0), (69.0, 0.0), (0.0, 1.0), (1.0, 0.0))
    assert (frame["length"], frame["distance"], frame["tied"]) == (9.0, 3.0, False)
    # The ramp outline and the depth stand on the frame
    assert [tuple(point) for point in item["corners"][:2]] == [(60.0, 0.0), (69.0, 0.0)] and item["depth"] > 0


@pytest.mark.parametrize("number", range(6))
def test_r482_each_number_of_the_ring_gives_the_frame_of_that_edge(number):
    frame = fronts({"6": {"edge": number}})["6"]["frame"]
    assert {frame["left"], frame["right"]} == {NOTCHED[number], NOTCHED[(number + 1) % 6]}
    # Into the plot, which a probe off the middle along the normal finds inside the face
    middle = ((frame["left"][0] + frame["right"][0]) / 2 + frame["n"][0] * 0.01, (frame["left"][1] + frame["right"][1]) / 2 + frame["n"][1] * 0.01)
    assert faces_of()["6"].contains(LineString([middle, middle]).centroid)


@pytest.mark.parametrize("value, line", [
    ({"edge": 6}, "override frontEdge of plot 6 names edge 6, the ring of its face holds edges 0 to 5 (R482)"),
    ({"edge": -1}, "override frontEdge of plot 6 names edge -1, the ring of its face holds edges 0 to 5 (R482)"),
    ({"edge": 1.5}, 'override frontEdge of plot 6 holds {"edge": 1.5}, its value is {"edge": n}, n a whole number (R482)'),
    ({"edge": True}, 'override frontEdge of plot 6 holds {"edge": true}, its value is {"edge": n}, n a whole number (R482)'),
    ({"edge": "2"}, 'override frontEdge of plot 6 holds {"edge": "2"}, its value is {"edge": n}, n a whole number (R482)'),
    ({"edge": 2, "more": 1}, 'override frontEdge of plot 6 holds {"edge": 2, "more": 1}, its value is {"edge": n}, n a whole number (R482)'),
    (2, 'override frontEdge of plot 6 holds 2, its value is {"edge": n}, n a whole number (R482)'),
])
def test_r482_a_number_no_ring_holds_or_another_value_is_refused_by_name(value, line):
    with pytest.raises(project_profile.ProfileError) as error:
        fronts({"6": value})
    assert str(error.value) == line


def test_r482_no_override_gives_the_fronts_as_before_byte_for_byte():
    before = fronts(None, NUMBERS)
    for edges in ({}, {"99": {"edge": 0}}):
        after = fronts(edges, NUMBERS)
        assert {number: {name: value for name, value in item.items() if name != "face"} for number, item in after.items()} == \
            {number: {name: value for name, value in item.items() if name != "face"} for number, item in before.items()}


def test_r482_a_plot_with_no_face_takes_no_frame_and_the_build_says_why(monkeypatch, tmp_path):
    item = fronts({"8": {"edge": 0}}, ("8",))["8"]
    assert item["frame"] is None and item["frontRule"] == "nearestCurb"
    built = build(monkeypatch, tmp_path, [entry("8", {"edge": 0})], faceless=("8",))
    assert built["missing"]["8"]["overrides"]["notApplied"]["frontEdge"] == NO_FACE


def test_r482_a_notched_plot_is_written_with_the_override_and_not_without(monkeypatch, tmp_path):
    without = build(monkeypatch, tmp_path)
    assert "6" not in without["plan"] and without["missing"]["6"]["written"] is False
    assert {"key": "levels.ffl", "why": "levels.py gives no FFL"} in without["missing"]["6"]["missing"]
    built = build(monkeypatch, tmp_path, [entry()])
    plot = built["plan"]["6"]
    assert plot["overrides"]["applied"] == ["frontEdge"] and plot["overrides"]["notApplied"] == {}
    # The ramp, the road level and the levels follow the front on y = 0
    assert built["trace"]["front"]["6"]["front"] == [10.6, 10.69]
    assert plot["levels"]["ffl"]["value"] is not None and plot["rotationDeg"]["value"] in (0.0, 180.0)
    ramp = plot["floors"]["ramp"]["corners"]["value"]
    assert sorted(point[:2] for point in ramp)[0] == [60.0, 0.0] and all(point[1] >= 0.0 for point in ramp)
    # Plot 5, its row partner, shares its FFL by keyRules.ffl as any pair does, the other plots of the row keep every value
    assert built["plan"]["5"]["levels"]["ffl"]["value"] == plot["levels"]["ffl"]["value"]
    for number in NUMBERS[:4]:
        assert built["plan"][number] == without["plan"][number]


def test_r482_a_row_marked_use_rule_keeps_the_rule_front_and_says_why(monkeypatch, tmp_path):
    built = build(monkeypatch, tmp_path, [entry(use="rule")])
    assert built["missing"]["6"]["overrides"]["notApplied"]["frontEdge"] == plan_rules.USE_RULE
    assert built["trace"]["front"]["6"]["frontRule"] == "orientation"


def test_r482_the_rules_of_a_build_see_the_override_before_they_walk_the_row(monkeypatch, tmp_path):
    taken = {}
    walk = majority.side_neighbours

    def spy(units, faces, frames, *rest):
        taken.update(frames)
        return walk(units, faces, frames, *rest)
    monkeypatch.setattr(majority, "side_neighbours", spy)
    build(monkeypatch, tmp_path, [entry(value={"edge": 5})])
    assert {taken["6"]["left"], taken["6"]["right"]} == {NOTCHED[5], NOTCHED[0]}


def test_r482_the_list_gives_the_edges_of_its_plots():
    assert plan_rules.FRONT_EDGE == "frontEdge"
    assert plan_rules.front_edges({"6": {"frontEdge": {"edge": 2}, "heightM": {"gapToRuleM": 0.1}}, "5": {"heightM": {"gapToRuleM": 0.1}}}) == FRONT
    assert plan_rules.front_edges({}) == {}


def test_r482_a_majority_run_measures_on_the_front_the_list_names(monkeypatch, tmp_path):
    # A rule entry stays the rule, and a list that is not there holds none
    path = list_file(tmp_path, [entry(), entry("5", use="rule"), {**entry("4"), "field": "heightM", "value": {"gapToRuleM": 0.1}}])
    assert majority.front_edges_of(path) == FRONT
    assert majority.front_edges_of(str(tmp_path / "none.json")) == {} and majority.front_edges_of(None) == {}
    taken = {}

    def spy(*args):
        taken["edges"] = args[-1]
        raise KeyError("stop")
    monkeypatch.setattr(plan_from_book, "fronts", spy)
    monkeypatch.setattr(plan_from_book, "footprint_clusters", lambda *args: [])
    settings, profile = settings_and_profile()
    with pytest.raises(KeyError):
        majority.site_context([{"plot": "6"}], rows_of(), {}, faces_of(), ROADS, [], SALES, profile, settings, settings.get("majority"), edges=FRONT)
    assert taken["edges"] == FRONT


def test_r482_the_check_table_and_the_report_know_the_field():
    settings = project_profile.load_settings()
    field = settings.get("checkRows")["fields"]["frontEdge"]
    assert field["check"] == "Position" and isinstance(field["past"], str)
    # Like the insert and the rotation it counts under Place villas by the default step
    assert settings.get("addin.report")["stepOfField"].get("frontEdge", settings.get("addin.report")["defaultStep"]) == settings.get("addin.report")["defaultStep"]


def test_r482_each_line_of_the_list_is_a_hand_edit_whose_plot_holds_no_gap_of_the_old_front():
    data = json.loads((project_profile.ROOT / "config" / "overrides" / "roshn-makkah.json").read_text(encoding="utf-8"))
    lines = [item for item in data["overrides"] if item["field"] == "frontEdge"]
    plots = {item["plot"] for item in lines}
    assert len(plots) == len(lines) > 0
    for item in lines:
        assert isinstance(item["value"]["edge"], int) and set(item["value"]) == {"edge"} and item["byHand"] is True and item["source"] == "parsons"
        assert item["decision"] == "parsons frontEdge" and item["rule"] is None and "Q299" in item["reason"]
    # The gap of about 15 m measured on the notch would throw the link off the plot on the new front
    assert not [item for item in data["overrides"] if item["field"] == "insert" and item["source"] == "parsons" and item["plot"] in plots]
