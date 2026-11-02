"""R490, R491, R493. Road surfaces named in order, each corner on the first that covers it, and the stop on plots with no road level, all made up
in code (R10). R490_RoadSurfacesTests.cs holds the same made up names and the same lines for the C#."""
import pytest

import design_basis
import plan_from_book
import plan_rules
import project_profile
import surface_lookup
from test_r407_design_basis import PAST_JOB, basis_settings, rev6_build
from test_round_addin import SUMMARY, run_book

FIRST, SECOND = "Made Up Job__Road A", "Made Up Job__Road B"
STOP = ("Zone 2: 3 of 5 plots have no road level on the road surfaces named (Road A, Road B). What to do: add the road surface of this zone in Set up. (R493)")


def road_settings(**road):
    """config/project.json under rev6 with these keys in designBasis.roadLevel in place of its surfaces and its limit."""
    settings = basis_settings(mode="rev6")
    block = settings.values["designBasis"]["roadLevel"]
    block.pop("surfaces", None)
    block.pop("noRoadStopShare", None)
    block.update(road)
    return settings


def refusal(settings):
    with pytest.raises(project_profile.ProfileError) as error:
        design_basis.basis_of(settings)
    return str(error.value)


def flat(x0, x1, z):
    """A made up flat road surface from x0 to x1, across the row of test_level_floor_steps.made_build."""
    return surface_lookup.Surface([(x0, -60.0, z), (x1, -60.0, z), (x1, 60.0, z), (x0, 60.0, z)], [(0, 1, 2), (0, 2, 3)])


def rows_of(zones):
    return {str(index): {"zone": zone} for index, zone in enumerate(zones, 1)}


def stop_basis(share=0.1, surfaces=(FIRST, SECOND)):
    return {"mode": design_basis.REV6, "surfaces": list(surfaces), "noRoadStopShare": share}


# R490


def test_r490_the_surfaces_of_design_basis_road_level_are_read_in_their_order_and_a_build_takes_them():
    settings = road_settings(surfaces=[SECOND, FIRST])
    basis = design_basis.basis_of(settings)
    assert basis["surfaces"] == [SECOND, FIRST]
    profile = project_profile.load_profile(project_profile.profile_path(settings))
    assert plan_rules.road_names(settings.get("siteBuild"), plan_rules.level_profile(profile), basis) == [SECOND, FIRST]


def test_r490_an_old_config_with_one_surface_reads_as_a_list_of_one():
    assert design_basis.basis_of(road_settings(surface=FIRST))["surfaces"] == [FIRST]


def test_r490_both_keys_together_are_refused_by_name():
    assert refusal(road_settings(surface=FIRST, surfaces=[FIRST])) == design_basis.BOTH_SURFACES
    assert design_basis.BOTH_SURFACES == "designBasis.roadLevel holds both surface and surfaces, a build under rev6 takes its road surfaces from surfaces alone (R490)"


def test_r490_no_value_is_refused_by_name():
    assert refusal(road_settings()) == design_basis.NO_SURFACES
    assert refusal(road_settings(surface="")) == design_basis.NO_SURFACES
    assert design_basis.NO_SURFACES == "designBasis.roadLevel names no road surface, a build under rev6 takes the road level from designBasis.roadLevel.surfaces in their order (R490)"


@pytest.mark.parametrize("bad", ["one text", [], [""], [FIRST, FIRST], [FIRST, 1], None, {}])
def test_r490_a_bad_list_is_refused_by_name(bad):
    assert refusal(road_settings(surfaces=bad)) == design_basis.BAD_SURFACES
    assert design_basis.BAD_SURFACES == "designBasis.roadLevel.surfaces must be a list in order, not empty, of non empty unique texts (R490)"


def test_r490_the_past_job_reads_none_of_it():
    settings = basis_settings(mode=PAST_JOB)
    settings.values["designBasis"]["roadLevel"] = {"surface": 5, "surfaces": "broken", "noRoadStopShare": "broken"}
    assert design_basis.basis_of(settings) == {"mode": PAST_JOB}
    profile = project_profile.load_profile(project_profile.profile_path(settings))
    assert plan_rules.road_names(settings.get("siteBuild"), plan_rules.level_profile(profile), {"mode": PAST_JOB}) == list(settings.get("siteBuild")["roadSurfaces"])


def test_r490_check_surfaces_checks_each_surface_of_the_list(tmp_path):
    basis = design_basis.basis_of(road_settings(surfaces=[FIRST, SECOND]))
    (tmp_path / (FIRST + ".xml")).write_text("<LandXML/>", encoding="utf-8")
    with pytest.raises(project_profile.ProfileError) as error:
        design_basis.check_surfaces(basis, tmp_path)
    assert str(error.value) == "designBasis.roadLevel.surfaces names %s, which is no surface of the inputs, the landxml folder holds no %s.xml (R490)" % (SECOND, SECOND)
    (tmp_path / (SECOND + ".xml")).write_text("<LandXML/>", encoding="utf-8")
    design_basis.check_surfaces(basis, tmp_path)


def test_r490_the_config_of_the_repo_names_the_six_of_site_build_in_their_order_and_the_limit_with_its_why():
    settings = project_profile.load_settings()
    road = settings.get("designBasis")["roadLevel"]
    assert road["surfaces"] == settings.get("siteBuild")["roadSurfaces"] and len(road["surfaces"]) == 6 and "surface" not in road
    assert road["noRoadStopShare"] == 0.1 and "missing surface" in road["noRoadStopWhy"] and "missing surface" in road["note"]


def test_r490_the_level_excel_check_takes_the_order_of_the_config_under_rev6_and_site_build_under_the_past_job():
    assert design_basis.road_order(road_settings(surfaces=[SECOND, FIRST]).values) == [SECOND, FIRST]
    assert design_basis.road_order(basis_settings(mode=PAST_JOB).values) is None
    assert design_basis.road_order({}) is None


NOT_TAKEN = "Made Up Ground, Other Grading and More cover the corners but are ground"


def not_taken_settings(**road):
    settings = road_settings(**road)
    settings.values["siteBuild"]["roadFolder"]["notTaken"] = {"ground": NOT_TAKEN, "other": "a made up reason of no surface"}
    return settings


def test_r490_a_list_naming_a_not_taken_surface_is_refused_with_its_reason_and_the_past_job_reads_none_of_it():
    name = "Made Up Job__Other Grading"
    assert refusal(not_taken_settings(surfaces=[FIRST, name])) == "designBasis.roadLevel.surfaces names %s, which is no road surface: %s (R490)" % (name, NOT_TAKEN)
    # A longer name is another surface, the reason holds the whole name or nothing
    assert design_basis.basis_of(not_taken_settings(surfaces=["Made Up Job__Other Gradings"]))["surfaces"] == ["Made Up Job__Other Gradings"]
    past = not_taken_settings(surfaces=[name])
    past.values["designBasis"]["mode"] = PAST_JOB
    assert design_basis.basis_of(past) == {"mode": PAST_JOB}


def test_r490_the_list_of_the_repo_config_passes_under_rev6():
    assert len(design_basis.basis_of(basis_settings(mode="rev6"))["surfaces"]) == 6


# R491


def built(monkeypatch, surfaces, order):
    """The made up build of test_r407_design_basis.rev6_build on the made up surfaces by name, in the order of the list under rev6."""
    settings = road_settings(surfaces=list(order))
    basis = design_basis.basis_of(settings)
    tin = [(name.rpartition("__")[2], surfaces[name]) for name in plan_rules.road_names(settings.get("siteBuild"), None, basis)]
    return rev6_build(monkeypatch, settings, surfaces=tin)


def test_r491_a_plot_takes_each_front_corner_from_the_surface_under_it_and_road_surfaces_names_both(monkeypatch):
    # Plot 3 runs from x 24 to x 36, the two surfaces meet at x 30, so one front corner lies on each
    found = built(monkeypatch, {FIRST: flat(-60.0, 30.0, 10.0), SECOND: flat(30.0, 200.0, 20.0)}, [FIRST, SECOND])
    item = found["trace"]["front"]["3"]
    assert sorted(item["surfaces"]) == ["Road A", "Road B"]
    assert dict(zip(item["surfaces"], item["front"])) == {"Road A": 10.0, "Road B": 20.0}
    assert sorted(found["plan"]["3"]["roadSurfaces"]) == ["Road A", "Road B"]
    assert found["summary"]["plotsWithNoRoadLevel"] == []


def test_r491_a_corner_under_both_takes_the_first_of_the_list_and_swapping_the_order_swaps_it(monkeypatch):
    surfaces = {FIRST: flat(-60.0, 200.0, 10.0), SECOND: flat(-60.0, 200.0, 20.0)}
    first = built(monkeypatch, surfaces, [FIRST, SECOND])["trace"]["front"]["4"]
    assert list(first["surfaces"]) == ["Road A", "Road A"] and list(first["front"]) == [10.0, 10.0]
    swapped = built(monkeypatch, surfaces, [SECOND, FIRST])["trace"]["front"]["4"]
    assert list(swapped["surfaces"]) == ["Road B", "Road B"] and list(swapped["front"]) == [20.0, 20.0]


def test_r491_a_corner_under_none_stays_no_road_level(monkeypatch):
    # Plot 6 runs from x 60 to x 72, the surfaces end at x 66
    found = built(monkeypatch, {FIRST: flat(-60.0, 30.0, 10.0), SECOND: flat(30.0, 66.0, 20.0)}, [FIRST, SECOND])
    assert found["trace"]["front"]["6"]["front"] is None
    assert found["summary"]["plotsWithNoRoadLevel"] == ["6"]
    listed = found["summary"]["noRoadLevel"][0]
    assert listed["plot"] == "6" and listed["tried"] == ["Road A", "Road B"] and len(listed["corners"]) == 1


# R493


def test_r493_the_message_of_a_zone_above_the_share_is_the_plain_line():
    reached = ["1", "2", "3", "4", "5"]
    assert design_basis.no_road_stop(stop_basis(), reached, ["1", "2", "3"], rows_of(["Zone 2"] * 5)) == STOP


def test_r493_exactly_at_the_share_does_not_stop_and_above_does():
    rows = rows_of(["Zone 2"] * 10)
    reached = list(rows)
    assert design_basis.no_road_stop(stop_basis(), reached, ["1"], rows) is None
    assert "2 of 10 plots" in design_basis.no_road_stop(stop_basis(), reached, ["1", "2"], rows)
    # No plot with no road level is no stop at any share, and a share of 1 never stops
    assert design_basis.no_road_stop(stop_basis(0.0), reached, [], rows) is None
    assert design_basis.no_road_stop(stop_basis(1), reached, reached, rows) is None


def test_r493_one_sentence_per_zone_that_fails_in_zone_order_a_blank_zone_reads_no_zone():
    rows = rows_of(["Zone 3", "Zone 3", "", "  ", "Zone 1", "Zone 1"] + ["Zone 9"] * 10)
    reached = list(rows)
    line = "%s: %d of %d plots have no road level on the road surfaces named (Road A). What to do: add the road surface of this zone in Set up."
    found = design_basis.no_road_stop(stop_basis(surfaces=[FIRST]), reached, ["1", "3", "4", "5", "7"], rows)
    assert found == " ".join([line % ("Zone 1", 1, 2), line % ("Zone 3", 1, 2), line % ("no zone", 2, 2)]) + " (R493)"


def test_r493_the_limit_is_read_under_rev6_and_a_config_with_none_stops_at_any_plot():
    assert design_basis.basis_of(road_settings(surfaces=[FIRST], noRoadStopShare=0.25))["noRoadStopShare"] == 0.25
    old = design_basis.basis_of(road_settings(surface=FIRST))
    assert old["noRoadStopShare"] == 0
    rows = rows_of(["Zone 2"] * 1000)
    assert "1 of 1000 plots" in design_basis.no_road_stop(old, list(rows), ["1"], rows)
    assert design_basis.no_road_stop(old, list(rows), [], rows) is None


@pytest.mark.parametrize("bad, shown", [(True, "True"), (False, "False"), ("0.1", "'0.1'"), (None, "None"), (-0.01, "-0.01"), (1.5, "1.5"), (float("nan"), "nan"), ([], "[]")])
def test_r493_a_limit_that_is_no_number_in_0_to_1_or_a_bool_is_refused_by_name(bad, shown):
    assert refusal(road_settings(surfaces=[FIRST], noRoadStopShare=bad)) == "designBasis.roadLevel.noRoadStopShare must be a number from 0 to 1, it holds %s (R493)" % shown


@pytest.mark.parametrize("edge", [0, 1, 0.0, 1.0])
def test_r493_both_ends_of_the_limit_are_a_value(edge):
    assert design_basis.basis_of(road_settings(surfaces=[FIRST], noRoadStopShare=edge))["noRoadStopShare"] == edge


def test_r493_the_past_job_never_stops():
    rows = rows_of(["Zone 2"] * 3)
    assert design_basis.no_road_stop({"mode": PAST_JOB}, list(rows), list(rows), rows) is None


def parity_name(step):
    return plan_from_book.parity.step_name(step, "python")


def stop_main(monkeypatch, tmp_path, settings, no_level, corners=("left",)):
    """main on the stubbed inputs of test_round_addin.run_book under settings, each step a marker file so the steps left are seen, plot 1 a unit
    plot and no_level the plots with no road level, each lacking corners, the plans written given back."""
    planned = []

    def plan_built(*args, **named):
        monkeypatch.setattr(plan_from_book.plan_from_past, "write_plan", lambda out, plan: planned.append(out))
        named["trace"].update(front={"1": {"unit": "DP2"}}, snap=0.005)
        return {"summary": dict(SUMMARY, skipped=[], plotsWithNoRoadLevel=no_level, noRoadLevel=[{"plot": plot, "corners": list(corners), "tried": [], "reason": "made up"} for plot in no_level])}

    def marker(step):
        (tmp_path / "steps" / parity_name(step)).write_text("made up", encoding="utf-8")

    for name in ("build_step_rules", "cluster_step", "front_step", "hand_step", "link_step", "level_step", "floor_step", "override_step", "parameter_step", "site_clusters", "step_inputs"):
        monkeypatch.setattr(plan_from_book, name, lambda *args, **named: {})
    for owner, name in ((plan_from_book, "status_step"), (plan_from_book, "summary_step"), (plan_from_book.plan_walls, "step"), (plan_from_book.check_rows, "step")):
        monkeypatch.setattr(owner, name, lambda *args, **named: ({}, {}))
    monkeypatch.setattr(plan_from_book, "write_build_step", lambda args, config, step, *rest, **named: marker(step))
    monkeypatch.setattr(plan_from_book.parity, "write_step", lambda folder, step, *rest, **named: marker(step))
    monkeypatch.setattr(plan_from_book.design_basis, "check_surfaces", lambda basis, folder: None)
    monkeypatch.setattr(plan_from_book.project_profile, "load_settings", lambda path=None: settings)
    (tmp_path / "steps").mkdir()
    code, _ = run_book(tmp_path, monkeypatch, ["--steps", str(tmp_path / "steps")], build=plan_built)
    return code, sorted(path.name for path in (tmp_path / "steps").iterdir()), planned


def test_r493_main_ends_with_code_2_and_the_message_the_steps_stay_complete_and_no_plan_is_written(monkeypatch, tmp_path, capsys):
    code, steps, planned = stop_main(monkeypatch, tmp_path, road_settings(surfaces=[FIRST], noRoadStopShare=0.1), ["1"])
    assert code == 2 and planned == []
    assert capsys.readouterr().err.strip() == ("plan_from_book: no zone: 1 of 1 plots have no road level on the road surfaces named (Road A). "
                                               "What to do: add the road surface of this zone in Set up. (R493)")
    assert steps == sorted(parity_name(step) for step in plan_from_book.PARITY_STEPS)


def test_r493_a_unit_plot_with_no_front_is_no_missing_surface_main_writes_the_plan(monkeypatch, tmp_path, capsys):
    code, _, planned = stop_main(monkeypatch, tmp_path, road_settings(surfaces=[FIRST], noRoadStopShare=0.1), ["1"], corners=())
    assert code == 0 and len(planned) == 1
    assert "R493" not in capsys.readouterr().err


def test_r493_the_stop_counts_only_the_plots_that_have_a_front_and_lack_a_road_level_at_a_corner(monkeypatch):
    no_front = plan_from_book.no_road_level("7", {"corners": None}, [], 0.005)
    lacking = plan_from_book.no_road_level("8", {"corners": [[0.0, 0.0], [1.0, 0.0]], "surfaces": ("Road A", None)}, [("Road A", None)], 0.005)
    assert design_basis.road_gaps([no_front, lacking]) == ["8"]


def test_r493_main_below_the_limit_writes_the_plan(monkeypatch, tmp_path):
    code, steps, planned = stop_main(monkeypatch, tmp_path, road_settings(surfaces=[FIRST], noRoadStopShare=1), ["1"])
    assert code == 0 and len(planned) == 1 and len(steps) == len(plan_from_book.PARITY_STEPS)


def test_r493_main_of_the_past_job_writes_the_plan_with_every_plot_off_the_surfaces(monkeypatch, tmp_path):
    code, _, planned = stop_main(monkeypatch, tmp_path, basis_settings(mode=PAST_JOB), ["1"])
    assert code == 0 and len(planned) == 1
