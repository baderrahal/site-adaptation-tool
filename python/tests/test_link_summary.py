import json

import link_summary


def link(file, plot=None, prototype=None, case=None, ffl=None, z=0, rotation=0, flipped=False, errors=None, instances=None):
    entry = {
        "file": file,
        "transform": {"origin": [100, 200, z], "rotationDeg": rotation, "mirrored": flipped},
    }
    if plot is not None or prototype is not None or case is not None or ffl is not None:
        entry.update({"plotNumber": plot, "prototype": prototype, "ffl": ffl, "case": case})
        entry["nameParts"] = {"plot": plot}
    if errors:
        entry["errors"] = errors
    if instances is not None:
        entry["instances"] = instances
    return entry


def project(links):
    return {"documents": [{"file": "site.rvt", "kind": "project", "links": links}]}


def new_shape():
    return project([
        link("Type A.rvt", "9001", "VX1", "C1", "10.500", 10.5, 0, False),
        link("Type A.rvt", "9002", "VX1", "C1", "10.5015", 10.5, 90, True),
        link("Type A.rvt", "9003", "VX2", "C2", "10.503", 10.5, 90, True),
        link("Type B.rvt", "9001", "VX2", "C2", "not a number", 10.5, 180, False, ["Link is not loaded"]),
        link("Type B.rvt", "9004", "VX2", None, None, 0, 0, False),
    ])


def test_r27_links_are_counted_per_file_prototype_and_mirror():
    summary = link_summary.summarize(new_shape())
    assert summary["links"] == 5
    assert summary["notLoaded"] == 1
    assert summary["perFile"] == {"Type B.rvt": 2, "Type A.rvt": 3}
    assert summary["perPrototype"] == {"VX1": 2, "VX2": 3}
    assert summary["mirroredTotal"] == 2
    assert summary["mirroredByPrototype"] == {"VX1": {"links": 2, "mirrored": 1}, "VX2": {"links": 3, "mirrored": 1}}
    assert summary["notComputable"] == []


def test_r27_rotation_values_are_counted_in_numeric_order():
    summary = link_summary.summarize(new_shape())
    assert list(summary["rotations"].items()) == [("0", 2), ("90", 2), ("180", 1)]


def test_r27_case_is_crossed_with_mirrored_and_with_rotation():
    summary = link_summary.summarize(new_shape())
    assert summary["caseVsMirrored"] == {
        "(none)": {"mirrored": 0, "notMirrored": 1},
        "C1": {"mirrored": 1, "notMirrored": 1},
        "C2": {"mirrored": 1, "notMirrored": 1},
    }
    assert summary["caseVsRotation"]["C1"] == {"0": 1, "90": 1}
    assert summary["caseVsRotation"]["C2"] == {"90": 1, "180": 1}
    assert summary["caseVsRotation"]["(none)"] == {"0": 1}


def test_r27_ffl_is_compared_with_the_origin_z_within_one_and_a_half_millimetres():
    summary = link_summary.summarize(new_shape())
    # 10.500 and 10.5015 are within 1.5 mm of 10.5, 10.503 is not, the text and the missing value cannot be read
    assert summary["fflEqualsOriginZ"] == {"toleranceMm": 1.5, "unitAssumed": "m", "match": 2, "differ": 1, "unreadable": 2}


def test_r27_ffl_text_is_read_only_when_it_is_a_plain_number():
    assert link_summary.parse_number("12.5") == 12.5
    assert link_summary.parse_number(" +12,5 m ") == 12.5
    assert link_summary.parse_number(7) == 7
    for text in ("12.5 to 13", "FFL 12.5", "", None, True):
        assert link_summary.parse_number(text) is None


def test_r27_plot_numbers_on_more_than_one_link_are_listed():
    data = project([
        link("A.rvt", "9001", "VX1", "C1", "1", 1),
        link("B.rvt", "9001", "VX1", "C1", "1", 1),
        link("B.rvt", "12", "VX1", "C1", "1", 1),
        link("C.rvt", "9001", "VX2", "C1", "1", 1),
        link("C.rvt", "12", "VX2", "C1", "1", 1),
        link("C.rvt", "7", "VX2", "C1", "1", 1),
    ])
    summary = link_summary.summarize(data)
    assert summary["repeatedPlots"] == [{"plot": "12", "links": 2}, {"plot": "9001", "links": 3}]
    assert summary["plotNameMismatch"] == 0
    data["documents"][0]["links"][5]["nameParts"]["plot"] = "8"
    assert link_summary.summarize(data)["plotNameMismatch"] == 1


def test_r27_old_shape_computes_what_it_can_and_names_what_it_cannot():
    old = project([link("Type A.rvt", z=1, rotation=0), link("Type A.rvt", z=1, rotation=90, flipped=True, errors=["Link is not loaded"]), link("Type B.rvt", rotation=90)])
    summary = link_summary.summarize(old)
    assert summary["perFile"] == {"Type B.rvt": 1, "Type A.rvt": 2}
    assert summary["rotations"] == {"0": 1, "90": 2}
    assert summary["mirroredTotal"] == 1
    assert summary["notLoaded"] == 1
    for part in ("perPrototype", "mirroredByPrototype", "caseVsMirrored", "caseVsRotation", "fflEqualsOriginZ", "repeatedPlots"):
        assert summary[part] is None
    parts = [item["part"] for item in summary["notComputable"]]
    assert parts == ["perPrototype and mirroredByPrototype", "caseVsMirrored and caseVsRotation", "fflEqualsOriginZ", "repeatedPlots and plotNameMismatch"]
    assert all("old shape" in item["reason"] for item in summary["notComputable"])


def test_r27_a_file_with_no_links_says_so():
    summary = link_summary.summarize({"documents": [{"file": "villa.rfa", "kind": "family"}]})
    assert summary["links"] == 0
    assert summary["perFile"] == {}
    assert all(item["reason"] == "the file has no links" for item in summary["notComputable"])


def test_r27_loading_drops_the_repeated_contents_and_keeps_their_count(tmp_path):
    instances = [{"familyName": "Villa", "parameters": {"Mark": str(number)}} for number in range(50)]
    path = tmp_path / "prototypes.json"
    path.write_text(json.dumps(project([link("Type A.rvt", instances=instances)])), encoding="utf-8")
    loaded = link_summary.load(path)
    entry = loaded["documents"][0]["links"][0]
    assert "instances" not in entry
    assert entry["instanceCount"] == 50


def test_r27_main_prints_text_and_writes_link_summary_json(tmp_path, capsys):
    path = tmp_path / "prototypes.json"
    path.write_text(json.dumps(new_shape()), encoding="utf-8")
    out = tmp_path / "out"
    assert link_summary.main([str(path), str(out)]) == 0
    text = capsys.readouterr().out
    assert "links 5" in text
    assert "VX2: 1 of 3" in text
    assert "match 2, differ 1, unreadable 2" in text
    assert "9001: 2 links" in text
    written = json.loads((out / "link_summary.json").read_text(encoding="utf-8"))
    assert written["perFile"] == {"Type B.rvt": 2, "Type A.rvt": 3}
    assert written["repeatedPlots"] == [{"plot": "9001", "links": 2}]
    assert written["notComputable"] == []
