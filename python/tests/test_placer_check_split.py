import copy
import math

import pytest
from shapely.geometry import box

import placer_check
import project_profile

FILE = "RRE-WP-NSP-KEO-ARC-DT3-ZZ-MOD-3300.rvt"
OLD = "RRE-WP-NSP-KEO-ARC-T3-ZZ-MOD-3300.rvt"
BLOCK = {
    "position": {"bandsMm": [0, 1, 10, 30], "passMm": 1.0},
    "level": {"bandsMm": [0, 1, 10, 30], "passMm": 1.0},
    "dialects": {
        "imc": {"plot": "parameter", "datum": "none"},
        "native": {"plot": "name", "datum": "surveyElevation", "floorTypes": {"generic": ["FFL"], "ramp": ["Ramps"]}},
    },
}
FLOORS = {"types": {"generic": ["Generic 150mm"], "yard": ["yard"], "ramp": ["Ramps 0.5%"]}, "levels": {"generic": "Level 1", "yard": "Level 2", "ramp": "Level 1"},
          "plotParameter": "Plot", "yardBelowFflM": 0.1, "yardShareTie": 0.01, "yardMinShare": 0.5}


def axes(rotation, mirrored):
    turn = math.radians(rotation)
    sign = -1 if mirrored else 1
    return [math.cos(turn), math.sin(turn), 0.0], [-sign * math.sin(turn), sign * math.cos(turn), 0.0]


def link(origin, file=FILE, rotation=90.0, mirrored=False, parameters=None):
    x, y = axes(rotation, mirrored)
    return {"file": file, "transform": {"origin": list(origin), "rotationDeg": rotation, "mirrored": mirrored, "axisX": x, "axisY": y, "axisZ": [0, 0, 1]},
            "parameters": parameters or {}}


def model(links, floors=(), shared_z=0.0):
    return {"documents": [{"kind": "project", "surveyPoint": {"position": [0, 0, 0], "sharedPosition": [0, 0, shared_z]}, "links": links, "floors": list(floors)}]}


def floor(type_name, plot_value, ring, top):
    return {"typeName": type_name, "parameters": {"Plot": plot_value}, "outline": [ring], "topElevationRange": {"min": top, "max": top}}


def plan(plots, ffl=7.5):
    return {"plots": [{"plot": plot, "prototypeFile": {"value": FILE}, "levels": {"ffl": {"value": ffl}}} for plot in plots]}


def imc_key(z=7.5):
    links = [link((10.0, 20.0, z), parameters={"RRE_ZZ_SP_Plot-Number": "1"}), link((30.0, 20.0, z), parameters={"RRE_ZZ_SP_Plot-Number": "2"})]
    ring = [[0, 0], [5, 0], [5, 5], [0, 5]]
    return model(links, [floor("Generic 150mm", "DT3-1", ring, z), floor("Generic 150mm", "DT3-2", [[20, 0], [25, 0], [25, 5], [20, 5]], z)])


def built(z=7.5, moves=None):
    moves = moves or {}
    return [link((10.0 + moves.get("1", 0.0), 20.0, z), parameters={"RRE_ZZ_SP_Plot-Number": "1"}),
            link((30.0 + moves.get("2", 0.0), 20.0, z), parameters={"RRE_ZZ_SP_Plot-Number": "2"})]


def split(key_doc, test_links, the_plan=None, dialect="imc", faces=None, numbers=None):
    key, _ = placer_check.key_views(key_doc, BLOCK["dialects"][dialect], placer_check.dialect_floors(FLOORS, BLOCK["dialects"][dialect]), faces, numbers)
    return placer_check.split_compare(the_plan or plan(["1", "2"]), key, placer_check.test_views(model(test_links)), BLOCK)


def test_r261_a_file_built_as_the_key_passes_position_and_level():
    result = split(imc_key(), built())
    assert result["position"]["pass"] and result["level"]["pass"]
    assert result["position"]["bands"] == {"equal": 2}
    assert result["level"]["againstKeyFfl"] == {"equal": 2}


def test_r261_a_level_off_fails_the_level_check_alone_and_the_position_still_passes():
    result = split(imc_key(), built(z=7.55), plan(["1", "2"], ffl=7.55))
    assert result["position"]["pass"]
    assert not result["level"]["pass"]
    assert result["level"]["againstKeyFfl"] == {"worse": 2}
    # The placer put the link at the plan FFL, so the gap is the level rule of the plan and not the placer
    assert result["level"]["againstPlanFfl"] == {"equal": 2}
    assert result["level"]["worst"][0]["mm"] == pytest.approx(50.0)


def test_r261_a_position_off_in_plan_fails_the_position_check_alone_with_its_band():
    result = split(imc_key(), built(moves={"1": 0.005}))
    assert not result["position"]["pass"]
    assert result["level"]["pass"]
    assert result["position"]["bands"] == {"equal": 1, "within10mm": 1}
    assert result["position"]["worst"][0] == {"plot": "1", "mm": 5.0}


def test_r261_the_position_is_x_and_y_only_so_a_z_gap_is_not_a_position_gap():
    result = split(imc_key(), built(z=9.0), plan(["1", "2"], ffl=9.0))
    assert result["position"]["bands"] == {"equal": 2}


def test_r261_a_link_of_another_file_than_the_key_is_counted_apart_and_fails_the_position():
    key = imc_key()
    key["documents"][0]["links"][0]["file"] = OLD
    result = split(key, built())
    assert result["position"]["bands"] == {"otherFile equal": 1, "equal": 1}
    assert result["position"]["rotationAndMirror"]["fileDiffers"] == 1
    assert not result["position"]["pass"]


def test_r261_a_wrong_hand_or_turn_fails_the_position_check():
    test = built()
    test[0] = link((10.0, 20.0, 7.5), mirrored=True, parameters={"RRE_ZZ_SP_Plot-Number": "1"})
    result = split(imc_key(), test)
    assert result["position"]["rotationAndMirror"]["mirroredDiffers"] == 1
    assert not result["position"]["pass"] and result["level"]["pass"]


def test_r261_a_missing_plot_fails_both_checks():
    result = split(imc_key(), built()[:1])
    assert result["plots"]["missing"] == 1
    assert not result["position"]["pass"] and not result["level"]["pass"]


@pytest.mark.parametrize("change", [{"position": {"bandsMm": [1, 10], "passMm": 1}}, {"level": {"bandsMm": [0, 1], "passMm": -1}}, {"dialects": {}},
                                    {"dialects": {"x": {"plot": "id", "datum": "none"}}}])
def test_r261_the_split_block_is_refused_when_a_band_pass_limit_or_dialect_is_wrong(change):
    block = dict(copy.deepcopy(BLOCK), **change)

    class Settings:
        path = project_profile.SETTINGS_PATH

        def get(self, key):
            return block if key == "placerCheck" else None

    with pytest.raises(project_profile.ProfileError):
        placer_check.load_split(Settings())


def test_r262_the_footprint_is_the_third_check_and_marked_edge_only():
    report = {"test": {"measured": 3, "notMeasured": {}}, "testJudged": {"pass": 2, "fail": 1, "residualBands": {"within10mm": 2, "worse": 1}, "worst10": [{"plot": "9"}]}}
    part = placer_check.footprint_part(report)
    assert part["edgeOnly"] is True and "hand" in part["note"]
    assert part["pass"] is False and part["failed"] == 1


def test_r263_a_native_key_reads_the_plot_by_name_the_floors_by_type_ffl_and_takes_the_datum_off():
    faces = {"501": box(0, 0, 5, 5), "502": box(20, 0, 25, 5)}
    numbers = {"501": "1", "502": "2"}
    datum = 100.0
    key = model([link((10.0, 20.0, 7.5), file=OLD, parameters={"Name": "T3-77"}), link((30.0, 20.0, 7.5), file=OLD, parameters={"Name": "T3-78"})],
                [floor("FFL", "T3-77", [[0, 0], [5, 0], [5, 5], [0, 5]], datum + 7.5), floor("FFL", "T3-78", [[20, 0], [25, 0], [25, 5], [20, 5]], datum + 7.5),
                 floor("Ramps", "T3-77", [[0, -1], [5, -1], [5, 0], [0, 0]], datum + 7.3)], shared_z=datum)
    views, info = placer_check.key_views(key, BLOCK["dialects"]["native"], placer_check.dialect_floors(FLOORS, BLOCK["dialects"]["native"]), faces, numbers)
    assert set(views) == {"1", "2"}
    assert views["1"]["ffl"] == pytest.approx(7.5)
    assert views["1"]["origin"][2] == pytest.approx(7.5)
    assert info["datumM"] == datum
    result = split(key, built(), dialect="native", faces=faces, numbers=numbers)
    assert result["level"]["againstKeyFfl"] == {"equal": 2}
    assert result["position"]["rotationAndMirror"]["fileDiffers"] == 2


def test_r263_the_native_dialect_has_no_yard_and_the_imc_dialect_keeps_the_project_types():
    native = placer_check.dialect_floors(FLOORS, BLOCK["dialects"]["native"])
    assert native["types"] == {"generic": ["FFL"], "yard": [], "ramp": ["Ramps"]}
    assert placer_check.dialect_floors(FLOORS, BLOCK["dialects"]["imc"]) is FLOORS


def test_r263_the_key_is_not_changed_by_the_name_mapping():
    faces, numbers = {"501": box(0, 0, 5, 5)}, {"501": "1"}
    key = model([link((10.0, 20.0, 7.5), parameters={"Name": "T3-77"})], [floor("FFL", "T3-77", [[0, 0], [5, 0], [5, 5], [0, 5]], 7.5)])
    before = copy.deepcopy(key)
    placer_check.key_views(key, BLOCK["dialects"]["native"], placer_check.dialect_floors(FLOORS, BLOCK["dialects"]["native"]), faces, numbers)
    assert key == before
