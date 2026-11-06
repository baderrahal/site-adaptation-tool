"""R702, R703. The second check of the report on a new project: no past share anywhere, and each value sheet names the source of its values."""
import copy

import report_excel_check as check
import test_report_excel_check as base


def as_new_project(monkeypatch, mode):
    config = copy.deepcopy(base.CONFIG)
    config["projectRules"] = {"pastJobMode": "pastMode"}
    config["designBasis"] = {"mode": mode, "modes": ["newMode", "pastMode"]}
    monkeypatch.setattr(base, "CONFIG", config)


def test_r702_a_new_project_prints_no_past_share_and_lists_no_weak_rule(tmp_path, monkeypatch):
    as_new_project(monkeypatch, "newMode")
    book = base.build(tmp_path)[2]["books"]["012"]
    assert all(row[2] == check.NO_PAST_SHARE and row[3] == check.NO_PAST_SHARE for row in book["box"])
    assert not any("share" in str(cell) for sheet in ("Assumed", "Issues", "Rules used") for row in book["sheets"][sheet] for cell in row)
    assert not any(row[2] == "Weak rule" for row in book["sheets"]["Issues"])


def test_r702_the_past_job_mode_keeps_its_shares(tmp_path, monkeypatch):
    as_new_project(monkeypatch, "pastMode")
    book = base.build(tmp_path)[2]["books"]["012"]
    assert book["box"][0][2] == 0.96 and any(row[2] == "Weak rule" for row in book["sheets"]["Issues"])


def test_r703_each_value_sheet_has_a_source_column_before_its_ids(made):
    want = made[2]["books"]["012"]["sheets"]
    assert check.SCHEDULE_COLUMNS[17] == "Source" and want["Plot schedule"][0][17]
    assert check.FLOOR_COLUMNS[-2] == "Source" and all(row[-2] for row in want["Floors"])
    assert check.WALL_COLUMNS[-2] == "Source" and all(row[-2] for row in want["Walls"])


def test_r703_sources_of_names_each_tag_once_in_order():
    values = [{"value": 1, "source": "footprint"}, {"value": None}, {"value": 2, "source": "pastJob"}, {"value": 3, "source": "keyRules"}, {"value": 4, "source": "computed"}]
    assert check.sources_of(*values) == "computed, rule, past, missing, footprint"
    assert check.sources_of(None, {"value": 5}) is None


made = base.made
