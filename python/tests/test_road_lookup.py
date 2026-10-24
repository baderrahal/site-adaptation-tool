import json

import pytest

import landxml_reader
import ramp_levels
import road_lookup
import surface_lookup
from landxml_fixture import config_dict, prototypes_dict, write_config, write_landxml, write_prototypes


def square(x0, y0, x1, y1, z):
    """A flat surface of two triangles over a square, as Surface takes it: points and faces."""
    xyz = [(x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z)]
    return surface_lookup.Surface(xyz, [(0, 1, 2), (0, 2, 3)])


def surfaces_config(roles, orders=None):
    """A road_surfaces.json shape with the given roles in file order, and optionally an order for each non-ground surface."""
    found = {}
    for name, role in roles:
        found[name] = {"role": role}
        if orders and name in orders:
            found[name]["order"] = orders[name]
    return {"surfaces": found}


ROLES = [("Ground", "ground"), ("DesignA", "design"), ("Road", "road"), ("DesignB", "design")]


def test_r91_with_no_order_in_the_config_the_road_is_first_then_the_design_surfaces_in_file_order_and_the_ground_never():
    assert road_lookup.order_of(surfaces_config(ROLES)) == (["Road", "DesignA", "DesignB"], road_lookup.FILE_ORDER)


def test_r91_an_order_in_the_config_decides_and_a_number_may_be_a_float():
    config = surfaces_config(ROLES, {"DesignA": 1, "Road": 2.5, "DesignB": 0})
    assert road_lookup.order_of(config) == (["DesignB", "DesignA", "Road"], road_lookup.CONFIG_ORDER)


@pytest.mark.parametrize("orders,text", [
    ({"DesignA": 1, "Road": 2}, "these surfaces have no order: DesignB"),
    ({"DesignA": 1, "Road": 1, "DesignB": 2}, "two surfaces have the same order"),
    ({"DesignA": "1", "Road": 2, "DesignB": 3}, "an order must be a number"),
    ({"DesignA": True, "Road": 2, "DesignB": 3}, "an order must be a number"),
    ({"Ground": 0, "DesignA": 1, "Road": 2, "DesignB": 3}, "the ground surface Ground has an order"),
])
def test_r91_a_partial_equal_text_or_ground_order_is_refused_in_one_message(orders, text):
    with pytest.raises(ValueError, match=text):
        road_lookup.order_of(surfaces_config(ROLES, orders))


def test_r91_two_road_surfaces_are_refused():
    with pytest.raises(ValueError, match="more than one surface has the role road"):
        road_lookup.order_of(surfaces_config([("A", "road"), ("B", "road")]))


def test_r91_the_first_surface_that_covers_the_point_answers_with_its_level_and_name():
    lookup = road_lookup.RoadLevels([("Road", square(0, 0, 10, 10, 1.0)), ("Design", square(5, 5, 20, 20, 2.0))])
    assert lookup.level_at(2, 2) == (pytest.approx(1.0), "Road")
    assert lookup.level_at(7, 7) == (pytest.approx(1.0), "Road")
    assert lookup.level_at(15, 15) == (pytest.approx(2.0), "Design")
    assert lookup.level_at(50, 50) == (None, None)
    assert lookup.levels([(2, 2), (15, 15), (50, 50)]) == [(pytest.approx(1.0), "Road"), (pytest.approx(2.0), "Design"), (None, None)]
    assert lookup.names == ["Road", "Design"]


def test_r91_the_order_changes_the_answer_where_two_surfaces_overlap():
    first = road_lookup.RoadLevels([("Road", square(0, 0, 10, 10, 1.0)), ("Design", square(5, 5, 20, 20, 2.0))])
    second = road_lookup.RoadLevels([("Design", square(5, 5, 20, 20, 2.0)), ("Road", square(0, 0, 10, 10, 1.0))])
    assert first.level_at(7, 7)[1] == "Road" and second.level_at(7, 7)[1] == "Design"


def test_r91_load_reads_the_surfaces_of_the_landxml_folder_in_the_order_and_never_the_ground(tmp_path):
    xml = write_landxml(tmp_path / "road.xml")
    config = ramp_levels.load_config(write_config(tmp_path / "config.json"))
    landxml_reader.read_landxml(xml, tmp_path / "landxml", set(config["surfaces"]))
    lookup = road_lookup.load(config, tmp_path / "landxml")
    assert lookup.names == ["Road"] and lookup.source == road_lookup.FILE_ORDER
    z, name = lookup.level_at(1005, 5005)
    assert name == "Road" and z == pytest.approx(20)
    # The ground covers this point but is not asked
    assert lookup.level_at(1500, 5500) == (None, None)


def test_r91_ramp_levels_load_config_carries_the_order_and_where_it_came_from(tmp_path):
    config = ramp_levels.load_config(write_config(tmp_path / "config.json"))
    assert config["order"] == ["Road"] and config["orderSource"] == road_lookup.FILE_ORDER
    shipped = ramp_levels.load_config(ramp_levels.DEFAULT_CONFIG)
    assert shipped["order"][0] == "Top Surface-02" and "EXISTING_11.5 Sqkm" not in shipped["order"]
    assert shipped["order"] == ["Top Surface-02", "Compiled West_MM", "Missed from Paste", "Part 4 Top Surfaces", "Top Surface-01", "Top Surface Part 2 East Of East"]


def test_r91_the_shipped_config_gives_no_surface_an_order_so_the_file_order_is_used_and_no_code_writes_it():
    raw = json.loads(ramp_levels.DEFAULT_CONFIG.read_text(encoding="utf-8"))
    assert all("order" not in entry for entry in raw["surfaces"].values())
    assert road_lookup.order_of(raw)[1] == road_lookup.FILE_ORDER


def test_r92_the_ramp_test_counts_the_corners_each_surface_answers_first_and_the_ramps_a_later_surface_gains(tmp_path):
    data = prototypes_dict()
    config_data = config_dict()
    config_data["surfaces"] = {"Road": {"role": "road"}, "Spare": {"role": "design"}, "Ground": {"role": "ground"}}
    config_path = tmp_path / "config.json"
    config_path.write_text(json.dumps(config_data), encoding="utf-8")
    xml = write_landxml(tmp_path / "road.xml")
    config = ramp_levels.load_config(config_path)
    landxml_reader.read_landxml(xml, tmp_path / "landxml", set(config["surfaces"]))
    summary = ramp_levels.run(write_prototypes(tmp_path / "p.json", data), tmp_path / "landxml", config, tmp_path / "levels")
    lookup = summary["lookup"]
    assert lookup["order"] == ["Road", "Spare"] and lookup["orderSource"] == road_lookup.FILE_ORDER
    assert lookup["corners"] == 20 and lookup["answeredBy"]["Road"] + lookup["answeredBy"]["Spare"] + lookup["noAnswer"] == 20
    assert sum(lookup["coveredCorners"].values()) == 5
    assert lookup["noFirstSurfaceCorner"] >= lookup["gainedFromLaterSurfaces"]
    records = json.loads((tmp_path / "levels" / "ramp_road_levels.json").read_text(encoding="utf-8"))["ramps"]
    corner = records[0]["corners"][0]
    assert corner["lookupSurface"] == "Road" and corner["lookupLevel"] == pytest.approx(20)


def test_r92_lookup_summary_counts_a_ramp_a_later_surface_covers_and_names_the_surface():
    config = {"order": ["Road", "Design"], "orderSource": road_lookup.FILE_ORDER}
    answers = [[(1.0, "Road"), (None, None), (None, None), (None, None)],
               [(None, None)] * 4,
               [(None, None), (None, None), (2.0, "Design"), (2.0, "Design")],
               [(None, None)] * 4]
    summary = ramp_levels.lookup_summary(config, answers)
    assert summary["answeredBy"] == {"Road": 1, "Design": 2} and summary["noAnswer"] == 13
    assert summary["noFirstSurfaceCorner"] == 3 and summary["gainedFromLaterSurfaces"] == 1 and summary["gainedBySurface"] == {"Design": 1}
    assert summary["coveredCorners"] == {"0": 2, "1": 1, "2": 1, "3": 0, "4": 0}
    assert summary["corners"] == 16


@pytest.mark.parametrize("role", ["Ground", "desgin", None, ""])
def test_r91_a_role_that_is_not_ground_design_or_road_is_refused_so_the_ground_can_never_answer(role):
    config = {"surfaces": {"a": {"role": "road"}, "g": {"role": role}}}
    with pytest.raises(ValueError, match="the surface g has the role"):
        road_lookup.order_of(config)
