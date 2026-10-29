"""R369. The second method of Phase 4 on a made up plan, its step files and its inputs, each check with a planted fault it must catch."""
import copy
import json
import math
import random

import pytest

import phase4_check

FACADE = ["RRE_ZZ_SP_Facade-Color"]
GROUND_MM = -75
RISER_MM = 170
DEPTH = 5.52
SLOPE = 0.5


def fixed(value, decimals):
    return math.floor(value * 10 ** decimals + 0.5) / 10 ** decimals


def steps_of(height, base):
    return 1 + max(0, int(math.ceil(fixed((fixed(height, 4) - base) / (RISER_MM / 1000.0), 4) - 0.5)))


def config():
    kinds = {"points": 1e-06, "levels": 1e-06, "angles": 1e-06, "noGeometry": None}
    level = {"kind": "levels"}
    return {"majority": {"overrideDefaultSources": ["parsons"], "linkNameFacade": FACADE},
            "addin": {"parity": {"kinds": kinds, "steps": {
                "fronts": {"left": {"kind": "points", "decimals": 3}, "length": {"kind": "points", "decimals": 3},
                           "normal": {"kind": "points", "decimals": 6}, "rightHand": {"kind": "points", "decimals": 6},
                           "corners": {"kind": "points", "decimals": 3}, "depth": {"kind": "points", "decimals": 3}},
                "levels": {name: dict(level, decimals=places) for name, places in
                           (("roadAtFront", 4), ("entry", 4), ("ffl", 3), ("ssl", 3), ("height", 4))},
                "overrides": {"insert": {"kind": "points", "decimals": 3}, "rotationDeg": {"kind": "angles", "decimals": 3},
                              **{name: dict(level, decimals=places) for name, places in
                                 (("roadAtFront", 4), ("entry", 4), ("ffl", 3), ("ssl", 3), ("height", 4))}},
                "linkFrame": {"insert": {"kind": "points", "decimals": 3}, "rotationDeg": {"kind": "angles", "decimals": 3}}}}}}


def rules():
    files = {"DP2": "F-DP2.rvt", "VD4": "F-VD4.rvt"}
    key = {
        "hand villa": {"rule": "blockMirrorRoadSide", "byFile": {files["VD4"]: {"yes left": False, "no left": True}}},
        "hand cluster": {"rule": "clusterSide", "byFile": {files["DP2"]: {"left": False, "right": True}}},
        "hand alone": {"rule": "rowPairSideOrBlockMirrorOpenEnd", "byFile": {}},
        "ffl": {"rule": "oneFflPerPieceAtTheHighestEntry"},
        "baseHeight": {"rule": "profile"},
        "steps": {"rule": "stepsFromHeight"},
        "parameter RRE_ZZ_SP_Plot-Number": {"rule": "plot"},
        "parameter RRE_ZZ_SP_Prototype": {"rule": "typology", "table": {"DP2": "DP2", "VD4": "VD4"}},
        "parameter RRE_ZZ_SP_Number-of-Steps": {"rule": "steps", "table": {"1": "1", "2": "2", "3": "3"}},
        "parameter RRE_ZZ_SP_Facade-Color": {"rule": "all", "table": {"all": "XX"}},
        "parameter CASE": {"rule": "typologyMirrored", "table": {"DP2 False": "F", "DP2 True": "E", "VD4 False": "F", "VD4 True": "E"}},
        "parameter RRE_ZZ_SP_Prototype-FFL": {"rule": "fflDownToTheCm"},
        "parameter RRE_ZZ_SP_RVT-Name": {"rule": "plotCodeStepsAndFacade"},
    }
    profile = {"frontRoad": {"offsetM": 0.0}, "rampSlopePercent": {"default": SLOPE}, "rampTypes": {"0.5": "Ramps 0.5%", "flat": "Flat Ramps"},
               "stepBands": [[0.205, 1], [0.38, 2], [99.0, 3]], "keyRules": key}
    return {"levelProfiles": {"default": "p", "p": profile}, "levels": {"ground_ssl": GROUND_MM},
            "steps": {"base_height": {"default": 100, "DP2": 200, "VD4": 200}, "riser_added": RISER_MM}}, files


def case(road3=None):
    """Three plots: 1 and 2 a DP2 cluster and one piece, 3 a VD4 villa alone, 3 with an entryM override and 1 with a CASE override."""
    rule_file, files = rules()
    units = {"1": "DP2", "2": "DP2", "3": "VD4"}
    overrides = [{"plot": "3", "field": "entryM", "value": {"gapToRuleM": 0.0016}, "source": "parsons"},
                 {"plot": "1", "field": "parameter CASE", "value": "X", "source": "parsons"},
                 {"plot": "2", "field": "entryM", "value": {"gapToRuleM": 0.5}, "source": "native"},
                 {"plot": "2", "field": "insert", "value": {"gapToRuleM": [0.05, 0.02]}, "source": "parsons"}]
    steps = {name: {} for name in ("clusters", "faces", "fronts", "hands", "levels", "linkFrame", "overrides", "parameters")}
    steps["clusters"]["1|2"] = {"plots": ["1", "2"], "code": "Duplex_DP4", "source": "footprint"}
    entries = {}
    for index, plot in enumerate(("1", "2", "3")):
        x0 = 100.0 + 20.0 * index
        left, right = [x0, 50.0], [x0 + 12.0, 50.0]
        normal, hand = [0.0, 1.0], [1.0, 0.0]
        corners = [left, right, [right[0], right[1] + DEPTH], [left[0], left[1] + DEPTH]]
        road = road3 if plot == "3" and road3 else (10.0 + 0.01 * index, 10.02 + 0.013 * index)
        steps["faces"][plot] = {"face": True, "area": 300.0, "centroid": [x0 + 6.0, 62.5], "boundsMin": [x0, 50.0], "boundsMax": [x0 + 12.0, 75.0]}
        steps["fronts"][plot] = {"frontRule": "orientation", "unit": units[plot], "left": left, "right": right, "normal": normal, "rightHand": hand,
                                 "length": 12.0, "distance": 4.0, "tied": False, "depth": DEPTH, "corners": corners,
                                 "roadLevelLeft": road[0], "roadLevelRight": road[1], "noRoadLevel": None}
        entries[plot] = (road[0] + road[1]) / 2.0 + SLOPE / 100.0 * DEPTH
    clustered = {"1": ("left", 2, False), "2": ("right", 1, True)}
    for plot in ("1", "2"):
        side, place, mirrored = clustered[plot]
        steps["hands"][plot] = {"unit": "DP2", "kind": "duplex", "unitClass": "cluster", "cluster": ["2", "1"], "clusterCode": "Duplex_DP4", "clusterSide": side,
                                "clusterIndex": place, "clusterUnits": 2, "handTable": "cluster", "handRule": "clusterSide", "factor": side,
                                "linkFile": files["DP2"], "mirrored": mirrored}
    steps["hands"]["3"] = {"unit": "VD4", "kind": "villa", "unitClass": "villa", "cluster": None, "clusterCode": None, "clusterSide": None, "clusterIndex": None,
                           "clusterUnits": None, "blockMirrorRoadSide": "yes left", "handTable": "villa", "handRule": "blockMirrorRoadSide",
                           "factor": "yes left", "linkFile": files["VD4"], "mirrored": False}
    pieces = {"1": ["1", "2"], "2": ["1", "2"], "3": None}
    plan = []
    for plot in ("1", "2", "3"):
        unit = units[plot]
        members = pieces[plot] or [plot]
        ffl = fixed(max(fixed(entries[m], 4) + 0.2 for m in members), 3)
        entry = fixed(entries[plot], 4)
        height = fixed(ffl - entry, 4)
        steps_n = steps_of(height, 0.2)
        text = "%.3f" % ((int(fixed(ffl * 1000, 0)) // 10) / 100.0)
        steps["levels"][plot] = {"unit": unit, "partner": None, "piece": pieces[plot], "roadAtFront": fixed(sum(steps["fronts"][plot][k] for k in ("roadLevelLeft", "roadLevelRight")) / 2.0, 4),
                                 "entry": entry, "ffl": ffl, "ssl": fixed(ffl + GROUND_MM / 1000.0, 3), "height": height, "steps": steps_n,
                                 "parameters": {"parameter RRE_ZZ_SP_Prototype-FFL": text}}
        mirrored = steps["hands"][plot]["mirrored"]
        rotation = 0.0
        insert = [steps["fronts"][plot]["right"][0] - 1.0, 51.0]
        plan_insert = [insert[0] + 0.05, insert[1] - 0.02] if plot == "2" else insert
        steps["linkFrame"][plot] = {"prototypeFile": files[unit], "fitUnit": unit, "anchor": "end", "offset": [-1.0, 1.0], "mirrored": mirrored,
                                    "insert": insert, "rotationDeg": rotation, "lacks": []}
        plan_entry = fixed(entry + 0.0016, 4) if plot == "3" else entry
        plan_height = fixed(ffl - plan_entry, 4)
        plan_steps = steps_of(plan_height, 0.2)
        case_value = "X" if plot == "1" else ("E" if mirrored else "F")
        parameters = {"RRE_ZZ_SP_Plot-Number": plot, "RRE_ZZ_SP_Prototype": unit, "RRE_ZZ_SP_Number-of-Steps": str(plan_steps),
                      "RRE_ZZ_SP_Facade-Color": "XX", "CASE": case_value, "RRE_ZZ_SP_Prototype-FFL": text}
        parameters["RRE_ZZ_SP_RVT-Name"] = " | ".join([plot, unit, str(plan_steps), "XX"])
        sources = {name: "keyRules" for name in parameters}
        if plot == "1":
            sources["CASE"] = "override"
        steps["parameters"][plot] = {"unit": unit, "written": True, "parameters": parameters, "sources": sources, "lacks": [],
                                     "linkName": parameters["RRE_ZZ_SP_RVT-Name"]}
        applied = {"1": ["parameter CASE"], "2": ["insert"], "3": ["entryM"]}[plot]
        steps["overrides"][plot] = {"unit": unit, "skip": None, "written": True, "applied": applied, "notApplied": {}, "mirrored": mirrored}
        value = lambda v, source="computed": {"value": v, "source": source}
        plan.append({"plot": plot, "unit": value(unit), "prototypeFile": value(files[unit]), "insert": value(plan_insert + [ffl]),
                     "rotationDeg": value(rotation), "mirrored": value(mirrored),
                     "levels": {"entry": value(plan_entry), "ffl": value(ffl), "ssl": value(fixed(ffl + GROUND_MM / 1000.0, 3)),
                                "height": value(plan_height), "steps": value(plan_steps), "roadAtFrontM": value(steps["levels"][plot]["roadAtFront"])},
                     "parameters": {name: value(text_value, sources[name]) for name, text_value in parameters.items()},
                     "levelRule": {"entry": entry, "ffl": ffl, "height": height, "steps": steps_n, "parameter RRE_ZZ_SP_Prototype-FFL": text},
                     "linkName": value(parameters["RRE_ZZ_SP_RVT-Name"]),
                     "floors": {"ramp": {"type": value("Ramps 0.5%"), "outline": value({"loops": [{"points": steps["fronts"][plot]["corners"], "arcs": []}]})}}})
    return {"plan": {"plots": plan}, "steps": steps, "config": config(), "rules": rule_file, "overrides": {"overrides": overrides}}


def write_case(folder, data):
    folder.mkdir(parents=True, exist_ok=True)
    (folder / "steps").mkdir(exist_ok=True)
    (folder / "plan.json").write_text(json.dumps(data["plan"]), encoding="utf-8")
    for name, plots in data["steps"].items():
        payload = {"format": "siteAdaptationStep", "formatVersion": 2, "step": name, "side": "python", "scope": "project",
                   "plots": {plot: {"raw": entry, "written": entry} for plot, entry in plots.items()}}
        (folder / "steps" / ("step_%s_python.json" % name)).write_text(json.dumps(payload), encoding="utf-8")
    for name in ("config", "rules", "overrides"):
        (folder / (name + ".json")).write_text(json.dumps(data[name]), encoding="utf-8")


def run(tmp_path, data, out_name="report.json"):
    work = tmp_path / "case"
    write_case(work, data)
    out = tmp_path / "out" / out_name
    out.parent.mkdir(exist_ok=True)
    code = phase4_check.main([str(work / "plan.json"), "--steps", str(work / "steps"), "--settings", str(work / "config.json"),
                              "--rules", str(work / "rules.json"), "--overrides", str(work / "overrides.json"), "--out", str(out)])
    report = json.loads(out.read_text(encoding="utf-8")) if out.is_file() else None
    return code, report


def mismatches(report, check):
    return {(row["plot"], row["field"]) for seed in report["seeds"].values() for row in seed["checks"][check]["mismatches"]}


def test_r369_draw_seeds_14_and_7_over_the_sorted_plan_plots():
    plots = [str(n) for n in range(1, 60)]
    shuffled = list(reversed(plots))
    for seed in (14, 7):
        aim = sorted(random.Random(seed).sample(sorted(plots, key=int), 20), key=int)
        assert phase4_check.draw(shuffled, seed, 20) == aim
    assert phase4_check.draw(["3", "1", "2"], 14, 20) == ["1", "2", "3"]
    assert phase4_check.draw(["10", "9"], 7, 1) == random.Random(7).sample(["9", "10"], 1)


def test_r369_draw_sorts_by_number_not_text():
    assert phase4_check.draw(["10", "9", "100"], 14, 3) == ["9", "10", "100"]
    population = ["10", "9", "100", "2"]
    assert phase4_check.draw(population, 7, 2) == sorted(random.Random(7).sample(["2", "9", "10", "100"], 2), key=int)


def test_r369_clean_case_matches_every_check(tmp_path):
    code, report = run(tmp_path, case())
    assert code == 0
    assert set(report["seeds"]) == {"14", "7"}
    for seed in report["seeds"].values():
        assert seed["plots"] == ["1", "2", "3"]
        for name in phase4_check.CHECKS:
            tally = seed["checks"][name]
            assert tally["mismatch"] == 0, (name, tally["mismatches"])
            assert tally["match"] > 0, name


def plant(data, step, plot, field, value):
    data["steps"][step][plot][field] = value


FAULTS = [
    ("clusters", lambda d: plant(d, "hands", "1", "clusterCode", "Duplex_DP2"), ("1", "code")),
    ("clusters", lambda d: plant(d, "hands", "2", "clusterIndex", 2), ("2", "index")),
    ("clusters", lambda d: plant(d, "hands", "1", "cluster", ["1", "3"]), ("1", "plots")),
    ("fronts", lambda d: plant(d, "fronts", "2", "length", 12.01), ("2", "length")),
    ("fronts", lambda d: d["steps"]["fronts"]["3"]["corners"].__setitem__(2, [161.0, 55.52]), ("3", "corners")),
    ("fronts", lambda d: d["plan"]["plots"][0]["floors"]["ramp"]["outline"]["value"]["loops"][0].__setitem__("points", [[0, 0], [1, 0], [1, 1], [0, 1]]),
     ("1", "planRampOutline")),
    ("hands", lambda d: plant(d, "hands", "3", "mirrored", True), ("3", "table")),
    ("hands", lambda d: plant(d, "hands", "1", "factor", "right"), ("1", "factor")),
    ("hands", lambda d: d["plan"]["plots"][1]["mirrored"].__setitem__("value", False), ("2", "planMirrored")),
    ("levels", lambda d: plant(d, "levels", "3", "entry", d["steps"]["levels"]["3"]["entry"] + 0.001), ("3", "entry")),
    ("levels", lambda d: plant(d, "levels", "1", "ffl", d["steps"]["levels"]["1"]["ffl"] + 0.002), ("1", "ffl")),
    ("levels", lambda d: plant(d, "levels", "2", "steps", 3), ("2", "steps")),
    ("levels", lambda d: d["plan"]["plots"][1]["levelRule"].__setitem__("ffl", 99.0), ("2", "planFfl")),
    ("overrides", lambda d: plant(d, "overrides", "3", "applied", []), ("3", "outcome")),
    ("overrides", lambda d: d["plan"]["plots"][2]["levels"]["entry"].__setitem__("value", d["plan"]["plots"][2]["levelRule"]["entry"]), ("3", "entryM")),
    ("overrides", lambda d: plant(d, "overrides", "1", "applied", ["parameter CASE", "mirrored"]), ("1", "applied")),
    ("overrides", lambda d: d["plan"]["plots"][1]["insert"]["value"].__setitem__(1, 51.02), ("2", "insert")),
    ("overrides", lambda d: plant(d, "overrides", "2", "applied", None), ("2", "outcome")),
    ("parameters", lambda d: d["plan"]["plots"][2]["parameters"]["RRE_ZZ_SP_RVT-Name"].__setitem__("value", "3 | VD4 | 9 | XX"), ("3", "RRE_ZZ_SP_RVT-Name")),
    ("parameters", lambda d: d["plan"]["plots"][0]["linkName"].__setitem__("value", "other"), ("1", "linkName")),
    ("parameters", lambda d: d["plan"]["plots"][1]["parameters"]["RRE_ZZ_SP_Prototype-FFL"].__setitem__("value", "1.000"), ("2", "RRE_ZZ_SP_Prototype-FFL")),
]


@pytest.mark.parametrize("check,fault,aim", FAULTS, ids=["%s-%s-%s" % (f[0], f[2][0], f[2][1]) for f in FAULTS])
def test_r369_each_check_catches_its_planted_fault(tmp_path, check, fault, aim):
    data = case()
    fault(data)
    code, report = run(tmp_path, data)
    assert code == 1
    assert aim in mismatches(report, check)


def test_r369_a_villa_in_a_footprint_cluster_keeps_the_class_villa(tmp_path):
    data = case()
    data["steps"]["clusters"]["3|4"] = {"plots": ["3", "4"], "code": "Villa_VD4", "source": "footprint"}
    data["steps"]["hands"]["3"].update({"cluster": ["3", "4"], "clusterCode": "Villa_VD4", "clusterIndex": 1, "clusterUnits": 2})
    code, report = run(tmp_path, data)
    assert code == 0, [seed["checks"]["clusters"]["mismatches"] for seed in report["seeds"].values()]


def test_r369_ffl_half_a_mm_from_the_unrounded_top_holds(tmp_path):
    road = 9.972850000000001
    entry = road + SLOPE / 100.0 * DEPTH
    data = case(road3=(road, road))
    ffl = data["steps"]["levels"]["3"]["ffl"]
    assert abs(ffl - (fixed(entry, 4) + 0.2)) == pytest.approx(0.0005, abs=1e-9)
    assert abs(ffl - (entry + 0.2)) > 0.0005 + 1e-6
    code, report = run(tmp_path, data)
    assert code == 0, [seed["checks"]["levels"]["mismatches"] for seed in report["seeds"].values()]


def test_r369_override_row_is_named_by_its_row_number(tmp_path):
    data = case()
    plant(data, "overrides", "3", "applied", [])
    code, report = run(tmp_path, data)
    rows = [row for seed in report["seeds"].values() for row in seed["checks"]["overrides"]["mismatches"] if row["field"] == "outcome"]
    assert code == 1 and rows and all(row["row"] == 1 for row in rows)


def test_r369_override_of_another_source_is_left_out(tmp_path):
    code, report = run(tmp_path, case())
    assert code == 0
    assert all(row.get("row") != 3 for seed in report["seeds"].values() for row in seed["checks"]["overrides"]["mismatches"])


def test_r369_report_holds_counts_and_plot_numbers_only(tmp_path):
    data = case()
    plant(data, "levels", "3", "entry", 12.3456)
    code, report = run(tmp_path, data)
    assert code == 1

    def walk(node):
        if isinstance(node, dict):
            for item in node.values():
                walk(item)
        elif isinstance(node, list):
            for item in node:
                walk(item)
        else:
            assert not isinstance(node, float), node
    walk(report)


def test_r369_refuses_an_out_folder_inside_a_git_work_tree(tmp_path):
    tree = tmp_path / "tree"
    (tree / ".git").mkdir(parents=True)
    (tree / "deep").mkdir()
    work = tmp_path / "case"
    write_case(work, case())
    out = tree / "deep" / "report.json"
    code = phase4_check.main([str(work / "plan.json"), "--steps", str(work / "steps"), "--settings", str(work / "config.json"),
                              "--rules", str(work / "rules.json"), "--overrides", str(work / "overrides.json"), "--out", str(out)])
    assert code == 2
    assert not out.exists()


def test_r369_refuses_an_out_file_in_this_repo(tmp_path):
    work = tmp_path / "case"
    write_case(work, case())
    out = phase4_check.ROOT / "python" / "tests" / "r369_never.json"
    code = phase4_check.main([str(work / "plan.json"), "--steps", str(work / "steps"), "--settings", str(work / "config.json"),
                              "--rules", str(work / "rules.json"), "--overrides", str(work / "overrides.json"), "--out", str(out)])
    assert code == 2
    assert not out.exists()


def test_r369_refuses_a_step_file_of_another_step(tmp_path):
    data = case()
    work = tmp_path / "case"
    write_case(work, data)
    path = work / "steps" / "step_levels_python.json"
    payload = json.loads(path.read_text(encoding="utf-8"))
    payload["step"] = "hands"
    path.write_text(json.dumps(payload), encoding="utf-8")
    with pytest.raises(ValueError, match="step"):
        phase4_check.load_steps(work / "steps", "python")


def test_r369_case_is_not_changed_by_a_run(tmp_path):
    data = case()
    before = copy.deepcopy(data)
    run(tmp_path, data)
    assert data == before
