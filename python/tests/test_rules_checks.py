import json

import project_profile
import rules_checks

PROFILE_VALUES = {
    "typologies": {
        "DP2": {"kind": "duplex", "attachment": "one_side"},
        "TH1": {"kind": "townhouse", "attachment": "both_sides"},
        "VL3": {"kind": "villa", "attachment": "detached"},
    },
}


def profile_in(tmp_path):
    path = tmp_path / "profile.json"
    path.write_text(json.dumps(PROFILE_VALUES), encoding="utf-8")
    return project_profile.load_profile(path)


def cluster(code, plots, typologies, sure=True):
    return {"code": code, "plots": plots, "typologies": typologies, "mirrored": [None] * len(plots), "orderSure": sure}


def test_r65_one_side_sits_at_a_cluster_end_both_sides_inside_and_detached_has_no_cluster(tmp_path):
    profile = profile_in(tmp_path)
    fine = [cluster("Q01", ["1", "2", "3", "4"], ["DP2", "TH1", "TH1", "DP2"]), cluster("D02", ["5", "6"], ["DP2", "DP2"])]
    assert rules_checks.check_attachment(fine, profile) == []
    wrong = [cluster("unknown", ["1", "2", "3", "4", "5"], ["TH1", "DP2", "DP2", "VL3", "DP2"]), cluster("unknown", ["9"], ["TH1"]), cluster("unknown", ["8"], ["ZZ9"])]
    issues = rules_checks.check_attachment(wrong, profile)
    assert [(found["rule"], found["plots"]) for found in issues] == [("R65", ["1"]), ("R65", ["2"]), ("R65", ["3"]), ("R65", ["4"]), ("R65", ["9"]), ("R65", ["8"])]
    assert "attached on both sides, but it is at the end of a cluster (cluster unknown, 5 units)" in issues[0]["text"]
    assert "attached on one side, but it is inside a cluster" in issues[1]["text"]
    assert "detached, but it is in a cluster" in issues[3]["text"]
    assert "no attachment in the profile" in issues[5]["text"]


def test_r66_a_cluster_with_the_code_unknown_is_a_conflict_named_in_the_issue_list():
    clusters = [cluster("D02", ["1", "2"], ["DP2", "DP2"]), cluster("unknown", ["3", "4", "5"], ["DP2", "TH1", "DP2"], sure=False)]
    issues = rules_checks.check_known_clusters(clusters)
    assert len(issues) == 1 and issues[0]["rule"] == "R66" and issues[0]["plots"] == ["3", "4", "5"]
    assert issues[0]["text"] == "Plots 3, 4, 5 (DP2, TH1, DP2) match no cluster in the profile. The order along the road is not sure"
