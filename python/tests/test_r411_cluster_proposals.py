"""R411. The cluster and mirror proposals of round 4i on made up rows built in code (R10): a detached unit alone with no hand (Q226), a pair,
a duplex townhouse cluster, a row of four duplexes split into two pairs, a row with no split or several, a plot with two neighbours on a side,
the opposite hands of the two halves, a file the hand table does not hold and the switch back to the past job. ClusterProposalTests.cs holds
the same made up rows for the C#."""
import pytest

import cluster_proposals
import design_basis
import project_profile
from test_r407_design_basis import PAST_JOB, basis_settings, rev6_build

DP1_FILE, TH1_FILE, DP2_FILE = "A-DP1.rvt", "A-TH1.rvt", "A-DP2.rvt"
PROFILE = {
    "typologies": {"VD2": {"attachment": "detached"}, "DP1": {"attachment": "one_side"}, "DP2": {"attachment": "one_side"}, "TH1": {"attachment": "both_sides"},
                   "XX1": {}},
    # D02 the same sequence as M02 under another name, as cluster_aliases names it, so the pair splits one way
    "clusters": {"M02": ["DP2", "DP2"], "M07": ["DP1", "TH1", "TH1", "DP1"], "M01": ["DP1", "DP1"], "D02": ["DP2", "DP2"]},
}
KEY = {"hand cluster": {"rule": "clusterSide", "byFile": {DP1_FILE: {"left": False, "right": True}, TH1_FILE: {"left": True, "right": False}}}}
FILES = {"DP1": DP1_FILE, "TH1": TH1_FILE, "DP2": DP2_FILE}


def row(units, first=11):
    """A row of plots first, first + 1 and on, left to right, each the side neighbour of the next, and their rows of plots.csv."""
    plots = [str(first + index) for index in range(len(units))]
    neighbours = {plot: {"left": plots[index - 1:index] if index else [], "right": plots[index + 1:index + 2]} for index, plot in enumerate(plots)}
    return plots, neighbours, {plot: {"typologyRev9": unit} for plot, unit in zip(plots, units)}


def proposed(units, profile=PROFILE, key=KEY, files=FILES, change=None):
    plots, neighbours, rows = row(units)
    if change:
        change(neighbours, rows)
    return cluster_proposals.proposals(plots, rows, neighbours, profile, key, files)


def test_r411_a_detached_unit_is_proposed_alone_with_no_mirror_flag_naming_q226():
    found = proposed(["VD2", "DP1", "DP1"])
    assert found["11"] == {"cluster": ["11"], "mirrored": None, "reason": cluster_proposals.DETACHED_WHY % "VD2"}
    assert "2.1.A" in found["11"]["reason"] and "Q226" in found["11"]["reason"]
    # The detached unit ends the row, so 12 and 13 are the pair M01
    assert found["12"]["cluster"] == found["13"]["cluster"] == ["12", "13"]


def test_r411_a_dp_pair_is_one_cluster_whose_code_is_the_first_of_its_sequence():
    found = proposed(["DP2", "DP2"])
    assert found["11"]["cluster"] == found["12"]["cluster"] == ["11", "12"]
    assert found["11"]["reason"].startswith("cluster M02 of the rules profile, the one split of the row 11 12 by Rev 06 2.1.E, the left half")
    assert found["12"]["reason"].startswith("cluster M02 of the rules profile, the one split of the row 11 12 by Rev 06 2.1.E, the right half")


def test_r411_dp_th_th_dp_is_one_cluster_and_its_halves_take_opposite_hands_of_the_hand_table():
    found = proposed(["DP1", "TH1", "TH1", "DP1"])
    assert all(found[plot]["cluster"] == ["11", "12", "13", "14"] for plot in found)
    # The left half takes the left hand of its file, the right half the right hand, so each unit and its mirror image face each other
    assert [found[plot]["mirrored"] for plot in ("11", "12", "13", "14")] == [False, True, False, True]
    assert found["12"]["reason"] == cluster_proposals.PROPOSED % ("M07", "11 12 13 14", "left", cluster_proposals.HAND % TH1_FILE)


def test_r411_four_duplexes_in_a_row_split_into_two_pairs():
    found = proposed(["DP2", "DP2", "DP2", "DP2"])
    assert (found["11"]["cluster"], found["12"]["cluster"], found["13"]["cluster"], found["14"]["cluster"]) == (["11", "12"], ["11", "12"], ["13", "14"], ["13", "14"])


def test_r411_a_row_with_no_split_or_several_gets_no_proposal_naming_q229():
    found = proposed(["DP2", "DP2", "DP2"])
    assert all(item["cluster"] is None and item["mirrored"] is None for item in found.values())
    assert found["11"]["reason"] == cluster_proposals.NO_SPLIT % ("11 12 13", "DP2 DP2 DP2") and "Q229" in found["11"]["reason"]
    several = dict(PROFILE, clusters={"M02": ["DP2", "DP2"], "Q04": ["DP2", "DP2", "DP2", "DP2"]})
    found = proposed(["DP2", "DP2", "DP2", "DP2"], profile=several)
    assert found["12"] == {"cluster": None, "mirrored": None, "reason": cluster_proposals.SEVERAL % ("11 12 13 14", "DP2 DP2 DP2 DP2", 2)}


def test_r411_a_plot_with_two_neighbours_on_a_side_leaves_its_row_with_no_proposal_naming_q229():
    def crowd(neighbours, rows):
        neighbours["12"]["right"] += ["98", "99"]
    found = proposed(["DP2", "DP2"], change=crowd)
    assert found["11"] == found["12"] == {"cluster": None, "mirrored": None, "reason": cluster_proposals.CROWDED % ("12", "11 12", 2, "right")}


def test_r411_a_file_the_hand_table_does_not_hold_gives_no_mirror_flag_with_the_reason():
    found = proposed(["DP2", "DP2"])
    assert found["11"]["cluster"] == ["11", "12"] and found["11"]["mirrored"] is None
    assert found["11"]["reason"].endswith(cluster_proposals.NO_HAND % ("left", DP2_FILE) + " (R411)")
    assert proposed(["DP2", "DP2"], files={})["12"]["reason"].endswith(cluster_proposals.NO_FILE % "DP2" + " (R411)")


def test_r411_a_unit_with_no_attachment_and_a_plot_with_no_unit():
    found = proposed(["XX1", "DP1", ""])
    assert "13" not in found and found["11"] == {"cluster": None, "mirrored": None, "reason": cluster_proposals.NO_ATTACHMENT % "XX1"}
    # The unit of no attachment and the plot of no unit end the row, so 12 stands alone and splits into no cluster
    assert found["12"]["reason"] == cluster_proposals.NO_SPLIT % ("12", "DP1")
    assert cluster_proposals.counts(found) == {"proposed": 0, "none": 2}


def test_r411_clusters_propose_must_be_true_or_false():
    settings = basis_settings(mode="rev6")
    settings.values["designBasis"]["clusters"]["propose"] = "yes"
    with pytest.raises(project_profile.ProfileError) as error:
        design_basis.basis_of(settings)
    assert str(error.value) == "designBasis.clusters.propose must be true or false, it holds 'yes' (R411)"
    assert design_basis.basis_of(basis_settings(mode="rev6"))["propose"] is True


def test_r411_the_build_under_rev6_puts_the_proposals_in_the_step_hands_and_the_plan_and_sets_no_other_value(monkeypatch):
    # Plot 6 a villa, so 2 to 5 are four DP2 in a row, the pairs 2 3 and 4 5, the DP2 file in the hand cluster table of the made up rules
    built = rev6_build(monkeypatch, basis_settings(mode="rev6"), units={"6": "VD2"})
    hands = built["hands"]
    assert [hands[plot]["proposedCluster"] for plot in ("1", "2", "3", "4", "5", "6", "7")] == [["1"], ["2", "3"], ["2", "3"], ["4", "5"], ["4", "5"], ["6"], None]
    assert [hands[plot]["proposedMirror"] for plot in ("2", "3", "4", "5")] == [False, True, False, True]
    assert hands["1"]["proposedMirror"] is None and "Q226" in hands["1"]["proposalReason"] and hands["7"]["proposalReason"] is None
    for plot, entry in built["plan"].items():
        assert entry["proposal"] == {"cluster": hands[plot]["proposedCluster"], "mirrored": hands[plot]["proposedMirror"], "reason": hands[plot]["proposalReason"]}
    assert built["summary"]["proposals"] == {"proposed": 6, "none": 0}
    # The proposal changes no value: the plan without the proposal key equals the build with propose false
    off = basis_settings(mode="rev6")
    off.values["designBasis"]["clusters"]["propose"] = False
    plain = rev6_build(monkeypatch, off, units={"6": "VD2"})
    assert {plot: {key: value for key, value in entry.items() if key != "proposal"} for plot, entry in built["plan"].items()} == plain["plan"]
    assert all(plain["hands"][plot]["proposedCluster"] is None and plain["hands"][plot]["proposalReason"] is None for plot in plain["hands"])
    assert "proposals" not in plain["summary"]


def test_r411_the_switch_back_to_the_past_job_gives_null_proposals_and_the_values_of_today(monkeypatch):
    past = rev6_build(monkeypatch, basis_settings(mode=PAST_JOB), units={"6": "VD2"})
    today = rev6_build(monkeypatch, project_profile.load_settings(), units={"6": "VD2"})
    assert all((item["proposedCluster"], item["proposedMirror"], item["proposalReason"]) == (None, None, None) for item in past["hands"].values())
    assert past["hands"] == today["hands"] and past["plan"] == today["plan"]
    assert not any("proposal" in entry for entry in past["plots"]) and "proposals" not in past["summary"]
