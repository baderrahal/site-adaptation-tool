"""The scenario tools on made up values, each broken on purpose as well."""
import json
import sys
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "scripts" / "scenarios"))

import confirmed_profile  # noqa: E402
import make_answers  # noqa: E402
import score_holdout  # noqa: E402
import scenario_tools as tools  # noqa: E402


def test_past_folders_are_never_raw(tmp_path):
    for name in ("a.dwg", "landxml/r.xml", "01_Output/1.11 References/s.xlsx", "data_book/plots.csv", "position/p.json"):
        path = tmp_path / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text("x", encoding="utf-8")
    assert sorted(path.as_posix() for path in tools.raw_files(tmp_path)) == ["a.dwg", "landxml/r.xml"]


def test_each_scenario_keeps_its_files():
    files = [Path("Site Footprint.dwg"), Path("Base.dwg"), Path("landxml/r.xml"), Path("Prototypes/a.rvt")]
    assert tools.files_of("S1", files) == files
    assert Path("Site Footprint.dwg") not in tools.files_of("S2", files) and len(tools.files_of("S2", files)) == 3
    assert tools.files_of("S4", files) == [Path("Site Footprint.dwg"), Path("Base.dwg"), Path("Prototypes/a.rvt")]


def test_words_change_whole_and_keep_upper_case():
    assert tools.changed_words("Zone 2 NBH-012 Neighbourhood") == "Phase 2 BLOCK-012 Block"
    assert tools.changed_words("ZONE_A") == "PHASE_A"
    assert tools.changed_words("Ozone horizon") == "Ozone horizon"


def test_rewrite_dxf_touches_text_entities_and_layers_only():
    text = "0\nTABLE\n2\nLAYER\n0\nLAYER\n2\nZONE-A\n0\nENDTAB\n0\nTEXT\n8\nZONE-A\n1\nZone 2\n2\nZONEBLOCK\n0\nEOF\n"
    out = tools.rewrite_dxf(text, tools.changed_words, tools.changed_words).split("\n")
    assert out[7] == "PHASE-A" and out[13] == "PHASE-A" and out[15] == "Phase 2"
    assert out[17] == "ZONEBLOCK"


def test_rewrite_dxf_leaves_dictionaries_and_records_alone():
    text = "0\nDICTIONARY\n3\nZone 12\n0\nXRECORD\n1\nZone 12\n0\nBLOCK\n3\nZone 12\n0\nMTEXT\n1\nZone 12\n0\nEOF\n"
    out = tools.rewrite_dxf(text, tools.changed_words, None).split("\n")
    assert out[3] == "Zone 12" and out[7] == "Zone 12" and out[11] == "Zone 12" and out[15] == "Phase 12"


def test_style_changes_only_a_whole_known_number():
    change = tools.style_text({"101"})
    assert change("101") == "LOT/101-X" and change(" 101 ") == "LOT/101-X"
    assert change("1101") == "1101" and change("Plot 101") == "Plot 101"


def test_style_finds_a_number_on_its_own_line_of_an_mtext():
    change = tools.style_text({"101", "7"})
    masked = "{" + chr(92) + "C256" + chr(59) + "DP4" + chr(92) + "P101}"
    assert change(masked) == "{" + chr(92) + "C256" + chr(59) + "DP4" + chr(92) + "PLOT/101-X}"
    # A number that is not a plot, a colour code and a type code all stay
    other = "{" + chr(92) + "C7" + chr(59) + "DP7" + chr(92) + "P999}"
    assert change(other) == other


def test_only_columns_of_numbers_are_found():
    rows = [["Plot", "Area", "Rooms"], [101, 101, 3], [102.0, "x", 4], ["103", "y", 101]]
    assert tools.columns_of_numbers(rows, {"101", "102", "103"}, 0.6) == {0}


def test_manifest_finds_every_change(tmp_path):
    (tmp_path / "a.txt").write_text("1", encoding="utf-8")
    (tmp_path / "b.txt").write_text("2", encoding="utf-8")
    manifest = tools.manifest_of(tmp_path)
    assert tools.manifest_faults(tmp_path, manifest) == []
    (tmp_path / "a.txt").write_text("9", encoding="utf-8")
    (tmp_path / "b.txt").unlink()
    (tmp_path / "c.txt").write_text("3", encoding="utf-8")
    assert sorted(tools.manifest_faults(tmp_path, manifest)) == ["added: c.txt", "changed: a.txt", "gone: b.txt"]


PLANS = [{"neighbourhood": "A", "plots": [{"plot": 1, "unit": "V1", "mirrored": True}, {"plot": 2, "unit": "V2", "mirrored": False}]},
         {"neighbourhood": "H", "plots": [{"plot": 3, "unit": "V1", "mirrored": False}]}]


def test_a_plan_field_of_value_and_source_gives_the_value():
    plan = [{"neighbourhood": "A", "plots": [{"plot": "1", "unit": {"value": "V1", "source": "pastJob"}, "mirrored": {"value": False, "source": "pastJob"}}]}]
    rows, _ = tools.answer_rows(plan)
    assert rows["1"]["mirror"] is False and rows["1"]["villaType"] == "V1"


def test_a_plot_with_no_mirror_or_type_is_refused():
    with pytest.raises(ValueError):
        tools.answer_rows([{"neighbourhood": "A", "plots": [{"plot": "1", "unit": {"value": "V1"}, "mirrored": {"value": None}}]}])


def test_the_held_out_area_never_reaches_a_scenario_answer():
    rows, twice = tools.answer_rows(PLANS)
    keep, held = tools.split_holdout(rows, {"H"})
    assert twice == [] and sorted(keep) == ["1", "2"] and sorted(held) == ["3"]
    for name in tools.SCENARIOS:
        assert "3" not in json.dumps(sorted(tools.scenario_answer(keep, name)["plots"]))


def test_s5_answer_takes_the_other_style():
    rows, _ = tools.answer_rows(PLANS[:1])
    assert sorted(tools.scenario_answer(rows, "S5")["plots"]) == ["LOT/1-X", "LOT/2-X"]


def test_score_counts_each_field():
    rows, _ = tools.answer_rows(PLANS[:1])
    answer = tools.scenario_answer(rows, "S1")
    read = [{"number": "1", "villaType": "V1", "mirror": True}, {"number": "2", "villaType": "V9", "mirror": None}, {"number": "8", "villaType": "V1", "mirror": True}]
    assert tools.score(answer, read) == {"right": 2, "read": 3, "numbers": 2, "villaType": 1, "mirror": 1}
    assert tools.score(answer, []) == {"right": 2, "read": 0, "numbers": 0, "villaType": 0, "mirror": 0}


RESULT = {"ran": "done", "built": "abc1234", "needs": [{"need": "rules", "mark": "missing", "line": "No rule set in Base.xlsx on layer SECRET-LAYER, plot 2301"}], "score": {"right": 2, "read": 3, "numbers": 2, "villaType": 1, "mirror": 1}, "faults": [], "levels": [{"word": "Phase"}, {"word": "Awnings"}]}


def test_the_table_holds_all_five_and_says_not_run():
    text = tools.table({"S1": RESULT})
    assert "| S1 | " in text and "| abc1234 | 3 of 2 | 2 of 2 | 1 of 2 | 1 of 2 | not scored | 0, 0, 0, 1 | yes |" in text
    assert "| S2 | " in text and "not run" in text
    assert "| rules | missing | not run |" in text


def test_the_table_shows_the_confirmed_runs_apart_and_only_when_there_are_some():
    assert "With a confirmed profile" not in tools.table({"S1": RESULT})
    assert "With a confirmed profile" not in tools.table({"S1": RESULT}, {"S1": None})
    better = dict(RESULT, score={"right": 2, "read": 3, "numbers": 2, "villaType": 2, "mirror": 1})
    before, after = tools.table({"S1": RESULT}, {"S1": better}).split("With a confirmed profile")
    assert "| 1 of 2 | 1 of 2 |" in before and "| 2 of 2 | 1 of 2 |" in after
    # Nothing a data check said reaches the second table either
    assert "SECRET-LAYER" not in after


def test_the_table_counts_the_areas_read_beside_the_sheet():
    assert tools.areas_read([{"nbhId": "A"}, {"nbhId": "A"}, {"nbhId": "B"}, {"nbhId": None}, {}]) == 2
    text = tools.table({"S1": dict(RESULT, areas=2, sheetAreas=13)})
    assert "| yes | 2 | 13 |" in text
    assert "| 3 of 2 |" in tools.table({"S1": RESULT}) and "| yes | UNKNOWN | UNKNOWN |" in tools.table({"S1": RESULT})


def test_the_confirmed_file_names_the_area_column_it_was_offered():
    profile = {"areaColumn": {"proposed": {"workbook": "w.xlsx", "sheet": "S", "column": "C", "areas": 3}}}
    assert confirmed_profile.confirmed(profile)["areaColumn"] == {"workbook": "w.xlsx", "sheet": "S", "column": "C"}
    assert "areaColumn" not in confirmed_profile.confirmed({"areaColumn": {"proposed": None}})


def test_a_run_that_skipped_a_file_shows_in_the_table():
    skipped = dict(RESULT, notRead=1)
    text = tools.table({"S1": RESULT, "S2": skipped})
    assert "Files not read: S1 UNKNOWN, S2 1, S3 UNKNOWN, S4 UNKNOWN, S5 UNKNOWN." in text
    both = tools.table({"S1": dict(RESULT, notRead=0)}, {"S1": skipped})
    assert "S1 0," in both.split("With a confirmed profile")[0] and "S1 1," in both.split("With a confirmed profile")[1]


def test_the_confirmed_file_takes_every_proposal_and_nothing_else():
    import confirmed_profile
    profile = {
        "codeMap": {"proposed": [{"old": "A1", "new": "B1", "source": "computed"}], "confirmed": []},
        "hands": {"proposed": {"B1": "left", "C1": None}, "confirmed": {}},
        "levels": [{"level": 1, "word": "Phase", "source": "computed"}, {"level": 2, "word": None, "source": "missing"}],
        "typologyColumn": {"proposed": {"workbook": "w.xlsx", "sheet": "S", "column": "C", "plots": 5}},
    }
    assert confirmed_profile.confirmed(profile) == {
        "codeMap": [{"old": "A1", "new": "B1"}], "hands": {"B1": "left"}, "levels": [{"level": 1, "word": "Phase"}],
        "typologyColumn": {"workbook": "w.xlsx", "sheet": "S", "column": "C"},
    }
    profile["typologyColumn"]["proposed"] = None
    assert "typologyColumn" not in confirmed_profile.confirmed(profile)


def test_a_confirmed_run_names_the_profile_in_its_request():
    import run_stage1
    plain = run_stage1.request_for("S1", Path("f"), Path("o"), "c.json")
    assert [row["role"] for row in plain["inputs"]] == ["scanFolder", "config"]
    given = run_stage1.request_for("S1", Path("f"), Path("o"), "c.json", Path("p.json"))
    assert [row["role"] for row in given["inputs"]] == ["scanFolder", "config", "projectProfile"]


def test_nothing_a_data_check_said_reaches_the_table():
    text = tools.table({"S1": RESULT, "S3": RESULT})
    for word in ("Base.xlsx", "SECRET-LAYER", "2301", "Awnings"):
        assert word not in text


def test_what_a_data_check_said_goes_to_the_report():
    assert "SECRET-LAYER" in tools.lines_of({"S1": RESULT})


def test_s3_group_words_are_scored_and_others_are_not():
    assert tools.words_score("S3", [{"word": "Phase"}, {"word": "Block"}]) == "2 of 2"
    assert tools.words_score("S3", [{"word": "Phase"}, {"word": "Awnings"}]) == "1 of 2"
    assert tools.words_score("S3", [{"word": "Awnings"}]) == "0 of 2"
    assert tools.words_score("S1", [{"word": "Awnings"}]) == "not scored"


def test_golden_spreads_over_types_and_repeats():
    book = [{"neighbourhood": "A", "pastSource": "schedule A", "pastFfl": "1", "officialNumber": str(index), "unitRev6": "V%d" % (index % 3)} for index in range(60)]
    book.append({"neighbourhood": "Z", "pastSource": "schedule Z", "pastFfl": "1", "officialNumber": "900", "unitRev6": "V1"})
    book.append({"neighbourhood": "A", "pastSource": "past job plan", "pastFfl": "1", "officialNumber": "901", "unitRev6": "V1"})
    first = make_answers.golden_plots(book, {"A"})
    assert [row["officialNumber"] for row in first] == [row["officialNumber"] for row in make_answers.golden_plots(book, {"A"})]
    assert len(first) == 30 and {row["unitRev6"] for row in first} == {"V0", "V1", "V2"}
    assert not {"900", "901"} & {row["officialNumber"] for row in first}
    assert sum(1 for row in first if row["unitRev6"] == "V1") == 10


def test_the_stand_in_for_confirm_takes_only_what_was_proposed():
    profile = {"codeMap": {"proposed": [{"old": "A", "new": "B", "source": "s"}]}, "hands": {"proposed": {"A": "left", "C": None}}, "levels": [{"level": 1, "word": "Phase"}, {"level": 2, "word": None}]}
    assert confirmed_profile.confirmed(profile) == {"codeMap": [{"old": "A", "new": "B"}], "hands": {"A": "left"}, "levels": [{"level": 1, "word": "Phase"}]}
    assert confirmed_profile.confirmed({}) == {"codeMap": [], "hands": {}, "levels": []}


def test_holdout_rows_take_only_the_held_areas_with_their_levels():
    plans = [{"neighbourhood": "H", "plots": [{"plot": "3", "unit": {"value": "V1"}, "mirrored": {"value": False}, "levels": {"ffl": {"value": 1.5, "source": "x"}}}]}, {"neighbourhood": "A", "plots": [{"plot": "4", "unit": {"value": "V1"}, "mirrored": {"value": True}}]}]
    assert tools.holdout_rows(plans, {"H"}) == {"3": {"villaType": "V1", "mirror": False, "levels": {"ffl": 1.5}}}


def test_holdout_levels_score_at_each_limit():
    answers = {"1": {"levels": {"ffl": 10.000}}, "2": {"levels": {"ffl": 10.000}}, "3": {"levels": {"ffl": 10.000}}, "4": {"levels": {"ffl": 10.000}}, "5": {"levels": {"ffl": None}}}
    plots = [{"number": "1", "values": {"ffl": {"value": 10.004}}}, {"number": "2", "values": {"ffl": {"value": 10.015}}}, {"number": "3", "values": {"ffl": {"value": 10.200}}}, {"number": "4", "values": {"ffl": {"value": None}}}]
    assert score_holdout.scored_levels(answers, plots) == {"ffl": {"held": 4, "built": 3, "5": 1, "20": 2, "50": 2}}


def test_holdout_plots_score_counts_each_field():
    answers = {"1": {"villaType": "V1", "mirror": True}, "2": {"villaType": "V2", "mirror": False}}
    assert score_holdout.score_plots(answers, [{"number": "1", "villaType": "V1", "mirror": None}]) == {"held": 2, "numbers": 1, "villaType": 1, "mirror": 0}
