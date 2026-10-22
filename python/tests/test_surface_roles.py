import json

import pytest

import landxml_reader
import ramp_levels
import surface_roles
from landxml_fixture import prototypes_dict, write_config, write_landxml, write_prototypes


def stats(share, floor_minus_surface, within):
    return {"coveredShare": share, "medianFloorMinusSurface": floor_minus_surface, "within30mm": within}


def test_r48_measure_counts_corners_within_30mm_and_the_medians():
    corners = [10.0, 10.0, 10.0, 10.0, 10.0]
    surface = [10.0, 10.03, 10.031, None, 9.0]
    found = surface_roles.measure(corners, surface, 0.25)
    assert found["covered"] == 4 and found["corners"] == 5
    assert found["coveredShare"] == pytest.approx(0.8)
    assert found["within30mm"] == 2
    assert found["medianAbsDifference"] == pytest.approx(0.0305)
    assert found["medianFloorMinusSurface"] == pytest.approx(-0.015)
    assert found["invisibleShare"] == 0.25


def test_r48_a_surface_that_covers_nothing_has_no_medians():
    found = surface_roles.measure([1.0, 2.0], [None, None], 0.0)
    assert found["covered"] == 0 and found["within30mm"] == 0
    assert found["medianAbsDifference"] is None and found["medianFloorMinusSurface"] is None


def test_r48_ground_needs_nearly_every_corner_covered_and_the_floor_above_it():
    assert surface_roles.propose({"a": stats(0.95, 1.4, 0)}) == {"a": "ground"}
    assert surface_roles.propose({"a": stats(0.949, 1.4, 0)}) == {"a": "unknown"}
    assert surface_roles.propose({"a": stats(1.0, -0.2, 0)}) == {"a": "unknown"}
    assert surface_roles.propose({"a": stats(1.0, None, 0)}) == {"a": "unknown"}


def test_r48_road_is_the_surface_with_the_most_corners_within_30mm_and_ground_is_never_road():
    roles = surface_roles.propose({"ground": stats(1.0, 1.4, 900), "x": stats(0.3, 0.0, 338), "y": stats(0.1, 0.0, 12), "z": stats(0.0, None, 0)})
    assert roles == {"ground": "ground", "x": "road", "y": "unknown", "z": "unknown"}


def test_r48_a_tie_or_no_corner_within_30mm_gives_no_road():
    assert surface_roles.propose({"x": stats(0.3, 0.0, 5), "y": stats(0.3, 0.0, 5)}) == {"x": "unknown", "y": "unknown"}
    assert surface_roles.propose({"x": stats(0.3, 0.0, 0), "y": stats(0.2, 0.0, 0)}) == {"x": "unknown", "y": "unknown"}


def test_r48_near_limit_is_30_mm_inclusive():
    assert surface_roles.measure([10.0], [9.97], None)["within30mm"] == 1
    assert surface_roles.measure([10.0], [9.969], None)["within30mm"] == 0


def test_r48_config_design_agrees_with_unknown_and_other_differences_warn_with_both_names():
    proposal = {"A": "road", "B": "unknown", "C": "ground", "D": "unknown", "E": "road", "F": "unknown"}
    config = {"A": "road", "B": "design", "C": "design", "D": "road", "E": None, "F": None}
    lines = surface_roles.warnings(config, proposal)
    assert lines == [
        "warning: role proposal differs from config for C: config design, proposal ground",
        "warning: role proposal differs from config for D: config road, proposal unknown",
        "warning: role proposal differs from config for E: config none, proposal road",
    ]


@pytest.fixture
def proposed(tmp_path):
    xml = write_landxml(tmp_path / "road.xml")
    config_path = write_config(tmp_path / "config.json")
    config = ramp_levels.load_config(config_path)
    landxml_reader.read_landxml(xml, tmp_path / "landxml", set(config["surfaces"]))
    prototypes = write_prototypes(tmp_path / "p.json")
    return tmp_path, config_path, prototypes


def read_proposal(tmp_path):
    return json.loads((tmp_path / "levels" / "role_proposal.json").read_text(encoding="utf-8"))


def test_r48_proposal_on_a_tiny_fake_file_lists_every_surface_of_the_file_including_one_the_config_lacks(proposed):
    tmp_path, config_path, prototypes = proposed
    config = ramp_levels.load_config(config_path)
    summary = ramp_levels.run(prototypes, tmp_path / "landxml", config, tmp_path / "levels")
    surfaces = read_proposal(tmp_path)["surfaces"]
    assert set(surfaces) == {"Road", "Ground", "Spare"}
    assert (surfaces["Road"]["proposed"], surfaces["Road"]["configRole"]) == ("road", "road")
    assert (surfaces["Ground"]["proposed"], surfaces["Ground"]["configRole"]) == ("ground", "ground")
    assert (surfaces["Spare"]["proposed"], surfaces["Spare"]["configRole"]) == ("unknown", None)
    assert summary["roleWarnings"] == []


def test_r48_proposal_numbers_on_the_fake_file(proposed):
    tmp_path, config_path, prototypes = proposed
    ramp_levels.run(prototypes, tmp_path / "landxml", ramp_levels.load_config(config_path), tmp_path / "levels")
    road = read_proposal(tmp_path)["surfaces"]["Road"]
    assert (road["corners"], road["covered"], road["within30mm"]) == (20, 8, 6)
    assert road["invisibleShare"] == pytest.approx(0.2)
    ground = read_proposal(tmp_path)["surfaces"]["Ground"]
    assert ground["coveredShare"] == 1.0 and ground["medianFloorMinusSurface"] > 0 and ground["invisibleShare"] == 0


def test_r48_a_disagreement_writes_one_warning_line_with_both_names_and_never_changes_the_config(proposed, capsys):
    tmp_path, config_path, prototypes = proposed
    changed = json.loads(config_path.read_text(encoding="utf-8"))
    changed["surfaces"]["Road"]["role"] = "design"
    changed["surfaces"]["Ground"]["role"] = "design"
    config_path.write_text(json.dumps(changed), encoding="utf-8")
    before = config_path.read_bytes()
    code = ramp_levels.main([str(tmp_path / "levels"), "--config", str(config_path), "--prototypes", str(prototypes), "--landxml-dir", str(tmp_path / "landxml")])
    lines = [line for line in capsys.readouterr().out.splitlines() if line.startswith("warning")]
    assert code == 0
    assert sorted(lines) == [
        "warning: role proposal differs from config for Ground: config design, proposal ground",
        "warning: role proposal differs from config for Road: config design, proposal road",
    ]
    assert read_proposal(tmp_path)["warnings"] == lines
    assert config_path.read_bytes() == before


def test_r48_the_real_config_is_not_touched_by_a_run(proposed):
    tmp_path, _, prototypes = proposed
    before = ramp_levels.DEFAULT_CONFIG.read_bytes()
    config = ramp_levels.load_config(write_config(tmp_path / "other.json"))
    ramp_levels.run(prototypes, tmp_path / "landxml", config, tmp_path / "levels")
    assert ramp_levels.DEFAULT_CONFIG.read_bytes() == before


def test_r48_proposal_file_holds_counts_and_shares_and_no_coordinates(proposed):
    tmp_path, config_path, prototypes = proposed
    ramp_levels.run(prototypes, tmp_path / "landxml", ramp_levels.load_config(config_path), tmp_path / "levels")
    text = (tmp_path / "levels" / "role_proposal.json").read_text(encoding="utf-8")
    assert "1005" not in text and "5005" not in text
    assert set(json.loads(text)["thresholds"]) == {"groundCover", "nearMetres"}
