"""The scenario tools on made up values, each broken on purpose as well."""
import json
import sys
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "scripts" / "scenarios"))

import make_answers  # noqa: E402
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


def test_scrub_takes_out_file_names():
    assert tools.scrub("Read from Client Site.dwg, layer X") == "Read from Client <file>, layer X"
    assert "Base.xlsx" not in tools.scrub("in C:\\a\\Base.xlsx")


def test_the_table_holds_all_five_and_says_not_run():
    needs = [{"need": "rules", "mark": "missing", "line": "No rule set in Base.xlsx"}]
    result = {"ran": "done", "needs": needs, "score": {"right": 2, "read": 3, "numbers": 2, "villaType": 1, "mirror": 1}, "faults": []}
    text = tools.table({"S1": result})
    assert "| S1 | " in text and "| 3 of 2 | 2 of 2 | 1 of 2 | 1 of 2 | 0, 0, 0, 1 | yes |" in text
    assert "| S2 | " in text and "not run" in text and "Base.xlsx" not in text and "<file>" in text


def test_golden_spreads_over_types_and_repeats():
    book = [{"neighbourhood": "A", "pastSource": "schedule A", "pastFfl": "1", "officialNumber": str(index), "unitRev6": "V%d" % (index % 3)} for index in range(60)]
    book.append({"neighbourhood": "Z", "pastSource": "schedule Z", "pastFfl": "1", "officialNumber": "900", "unitRev6": "V1"})
    book.append({"neighbourhood": "A", "pastSource": "past job plan", "pastFfl": "1", "officialNumber": "901", "unitRev6": "V1"})
    first = make_answers.golden_plots(book, {"A"})
    assert [row["officialNumber"] for row in first] == [row["officialNumber"] for row in make_answers.golden_plots(book, {"A"})]
    assert len(first) == 30 and {row["unitRev6"] for row in first} == {"V0", "V1", "V2"}
    assert not {"900", "901"} & {row["officialNumber"] for row in first}
    assert sum(1 for row in first if row["unitRev6"] == "V1") == 10
