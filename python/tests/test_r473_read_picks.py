"""R473. The Python side of the read with the picks of button 2, python/read_steps.py, on a made up picks.json, CadReader outputs and a
workbook built in code. dotnet/SiteAdaptation.Tests/R473_ReadPicksTests.cs holds the C# side, ReadPicks of the same rules."""
import json

import openpyxl

import parity
import read_steps
from test_read_steps import BLOCKS, made_settings

FILES = {"baseplan": "Base.dwg", "villaBlocks": "f.dwg", "sales": "sales.xlsx", "salesSheet": "Sales"}


def picks(**kinds):
    entries = {kind: {"source": None, "from": "default"} for kind in read_steps.PICK_KINDS}
    entries.update({kind: {"source": source, "from": origin} for kind, (source, origin) in kinds.items()})
    return {"format": "site-adaptation-picks", "kinds": entries, "conflicts": [], "lines": []}


def test_r473_a_pick_or_proposal_is_taken_and_the_default_keeps_config():
    made = picks(plotOutline=("Base.dwg|MP_PLOT_ALT + MP_PLOT_LINE", "pick"), villaBlock=("f.dwg|MP_DP2", "proposal"),
                 villaType=("sales.xlsx|Sales|Type B|shift 0", "pick"))
    assert read_steps.pick_of(made, "plotOutline", FILES) == (["MP_PLOT_ALT", "MP_PLOT_LINE"], None)
    # Breaker 1 of FIX-1NOV-A2: one villa type per insert layer, so a villa block source of one layer is never taken (Q346)
    layer, line = read_steps.pick_of(made, "villaBlock", FILES)
    assert layer is None and "one layer per villa type" in line
    assert read_steps.pick_of(made, "villaType", FILES) == ("Type B", None)
    assert read_steps.pick_of(made, "plotNumber", FILES) == (None, None)


def test_r473_a_pick_of_another_file_keeps_the_default_with_the_line():
    made = picks(villaBlock=("f.dwg|MP_DP2", "pick"), villaType=("Blocks.dwg|block|MP_VL", "pick"))
    layer, line = read_steps.pick_of(made, "villaBlock", dict(FILES, villaBlocks="f rev2.dwg"))
    assert layer is None and "f.dwg|MP_DP2" in line and "f rev2.dwg" in line
    header, line = read_steps.pick_of(made, "villaType", FILES)
    assert header is None and "blocks or texts" in line
    header, line = read_steps.pick_of(picks(villaType=("sales.xlsx|Other|Type B|shift 0", "pick")), "villaType", FILES)
    assert header is None and "the sheet Other" in line


def test_r473_a_villa_block_pick_of_one_layer_keeps_every_block_of_the_read(tmp_path, capsys):
    settings, _ = made_settings(tmp_path)
    steps = tmp_path / "steps"
    assert read_steps.main(["--settings", str(settings), "--steps", str(steps), "--only", "blocks"]) == 0, capsys.readouterr().err
    assert len(parity.load_step(steps / parity.step_name("blocks", "python"), "python")["plots"]) == 3
    path = tmp_path / "picks.json"
    path.write_text(json.dumps(picks(villaBlock=("f.dwg|MP_DP2", "pick"))), encoding="utf-8")
    assert read_steps.main(["--settings", str(settings), "--steps", str(steps), "--only", "blocks", "--picks", str(path)]) == 0, capsys.readouterr().err
    payload = parity.load_step(steps / parity.step_name("blocks", "python"), "python")
    # Breaker 1 of FIX-1NOV-A2: the drawing holds one villa type per insert layer, a pick of one layer would drop the other types
    assert len(payload["plots"]) == 3 and "one layer per villa type" in capsys.readouterr().out
    assert "picks" in {item["role"] for item in payload["run"]["inputs"]}
    # A pick of another drawing than the blocks.json read keeps every block and says why
    path.write_text(json.dumps(picks(villaBlock=("g.dwg|MP_DP2", "pick"))), encoding="utf-8")
    assert read_steps.main(["--settings", str(settings), "--steps", str(steps), "--only", "blocks", "--picks", str(path)]) == 0
    assert "g.dwg|MP_DP2" in capsys.readouterr().out
    assert len(parity.load_step(steps / parity.step_name("blocks", "python"), "python")["plots"]) == 3


def test_r473_a_conflict_pick_changes_one_plots_villa_type():
    table = {"12": {"baseplanLabel": "12", "typologyRev9": "DP1"}, "13": {"baseplanLabel": "13", "typologyRev9": "DP1"}}
    conflicts = [{"plot": "12", "field": "villa type", "source": "Blocks.dwg|block|MP_VL", "value": "VL2"},
                 {"plot": "12", "field": "villa type", "source": "other.xlsx|S|T|shift 0", "value": "VL3"},
                 {"plot": "99", "field": "villa type", "source": "Blocks.dwg|block|MP_VL", "value": "VL2"}]
    lines = []
    assert read_steps.apply_conflicts(table, conflicts, lines) == 1
    assert table["12"]["typologyRev9"] == "VL2" and table["13"]["typologyRev9"] == "DP1"
    assert len(lines) == 2 and "VL3" in lines[0] and "99" in lines[1]


def test_r473_picks_json_of_first_sources_sets_nothing_and_a_second_source_sets_that_one_plot():
    # Breaker 2 of FIX-1NOV-A2: Python takes the picks ReadPicks.Resolve wrote, and that writes no conflict for a saved first source. The
    # rule itself lies in Resolve, R473_a_saved_first_source_of_every_conflict_gives_no_conflict_and_no_line of the C# proves it
    table = {"12": {"baseplanLabel": "12", "typologyRev9": "DP1"}, "13": {"baseplanLabel": "13", "typologyRev9": "DP1"}}
    lines = []
    assert read_steps.apply_conflicts(table, picks()["conflicts"], lines) == 0
    assert lines == [] and table["12"]["typologyRev9"] == "DP1" and table["13"]["typologyRev9"] == "DP1"
    second = dict(picks(), conflicts=[{"plot": "12", "field": "villa type", "source": "Blocks.dwg|block|MP_VL", "value": "VL2"}])
    assert read_steps.apply_conflicts(table, second["conflicts"], lines) == 1
    assert lines == [] and table["12"]["typologyRev9"] == "VL2" and table["13"]["typologyRev9"] == "DP1"


def test_r473_a_villa_type_sheet_pick_sets_the_typology_column_when_the_header_row_holds_it(tmp_path):
    book = openpyxl.Workbook()
    book.active.title = "Sales"
    book.active.append(["Plot No", "Typology_Rev9", " Type B "])
    path = tmp_path / "sales.xlsx"
    book.save(path)
    columns = {"plot": "Plot No", "typology": "Typology_Rev9"}
    cells = read_steps.sales_header(path, "Sales", 1)
    lines = []
    assert read_steps.typology_columns(columns, cells, 1, "Type B", lines) == {"plot": "Plot No", "typology": "Type B"}
    assert lines == []
    assert read_steps.typology_columns(columns, cells, 1, "Type C", lines) == columns
    assert len(lines) == 1 and "Type C" in lines[0]
    assert read_steps.typology_columns(columns, cells, 1, None, lines) == columns
