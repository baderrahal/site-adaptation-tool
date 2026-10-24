import pytest

import cross_sections
import entry_terms
import project_profile

SETTINGS = {"titlePattern": "CROSS SECTION", "titleColumnM": 3.0, "titleBelowM": 1.6, "roadTypePattern": "ROAD", "rowPattern": "ROW",
            "pavementLayers": ["PAVE"], "curbWidthM": [0.08, 0.3], "curbHeightM": [0.15, 0.45], "touchM": 0.01, "minRoadLineM": 0.5,
            "sidewalkStartM": 0.4, "sidewalkBelowTopM": 0.1, "flatSlope": 0.05, "percentAboveM": 0.5, "percentPattern": "([0-9]+(?:[.][0-9]+)?)%"}


def section_h():
    """A local road like section H: lane from the crown at x 4 down to the curb face at x 0, curb 0.12 by 0.20, sidewalk 5.7 m behind."""
    curb = {"kind": "polyline", "layer": "PAVE", "pts": [[-0.12, 0.0, 0], [0.0, 0.0, 0], [0.0, 0.2, 0], [-0.12, 0.2, 0]], "closed": True}
    lane = {"kind": "line", "layer": "PAVE", "pts": [[4.0, 0.05, 0], [0.0, 0.13, 0]]}
    walk = {"kind": "line", "layer": "PAVE", "pts": [[-0.35, 0.15, 0], [-6.05, 0.15, 0]]}
    small = {"kind": "polyline", "layer": "PAVE", "pts": [[-0.35, 0.1, 0], [-0.12, 0.1, 0], [-0.12, 0.2, 0], [-0.35, 0.2, 0]], "closed": True}
    texts = [{"text": "%%UTYPICAL CROSS SECTION H", "x": -5.0, "y": -3.0}, {"text": "%%ULOCAL ROAD", "x": -4.5, "y": -3.5},
             {"text": "2.0%%%", "x": -3.0, "y": 0.4}, {"text": "LANE", "x": 2.0, "y": 0.6}]
    return [curb, lane, walk, small], texts


# R192 cross sections
def test_r192_curb_up_stand_is_the_top_less_where_the_lane_meets_the_face():
    pieces, texts = section_h()
    found = cross_sections.curbs(pieces, SETTINGS)
    assert len(found) == 1
    curb = found[0]
    assert curb["upstandM"] == 0.07 and curb["roadSlopePercent"] == -2.0
    assert curb["sidewalkWidthM"] == 6.05 and curb["sidewalkLineM"] == 5.7


def test_r192_cross_fall_from_the_percent_text_over_the_sidewalk_else_the_line():
    pieces, texts = section_h()
    curb = cross_sections.curbs(pieces, SETTINGS)[0]
    assert cross_sections.cross_fall(curb, texts, SETTINGS) == (2.0, "text")
    assert cross_sections.cross_fall(curb, [t for t in texts if "%" not in t["text"] or "SECTION" in t["text"]], SETTINGS) == (0.0, "line")


def test_r192_titles_take_the_road_type_written_under_them():
    _, texts = section_h()
    heads = cross_sections.titles(texts, SETTINGS)
    assert heads == [{"title": "TYPICAL CROSS SECTION H", "x": -5.0, "y": -3.0, "roadType": "LOCAL ROAD", "row": None}]


def test_r192_a_box_out_of_the_curb_band_is_no_curb():
    pieces, _ = section_h()
    pieces[0]["pts"] = [[-0.12, 0.0, 0], [0.0, 0.0, 0], [0.0, 0.1, 0], [-0.12, 0.1, 0]]
    assert cross_sections.curbs(pieces, SETTINGS) == []


def test_r192_profile_values_finds_curb_and_sidewalk_keys_only():
    profile = {"plot": {"long_side": 25000}, "road": {"curb_height": 150, "sidewalk_width_m": 2}}
    assert cross_sections.profile_values(profile, ["sidewalk", "curb"]) == {"road.curb_height": 150, "road.sidewalk_width_m": 2}


# R193 El @ Entry from its terms
def test_r193_measured_takes_the_modal_values_of_curbs_with_a_sidewalk():
    cross = {"rows": [{"upstandM": 0.07, "sidewalkWidthM": 6.0, "sidewalkCrossFallPercent": 2.0, "crossFallSource": "text"},
                      {"upstandM": 0.07, "sidewalkWidthM": 3.0, "sidewalkCrossFallPercent": 2.0, "crossFallSource": "text"},
                      {"upstandM": 0.15, "sidewalkWidthM": 0.35, "sidewalkCrossFallPercent": 0.0, "crossFallSource": "line"}]}
    assert entry_terms.measured(cross, {"minSidewalkM": 1.0}) == {"curbs": 2, "upstandM": 0.07, "crossFallPercent": 2.0}


def test_r193_prediction_adds_the_terms_and_leaves_one_out():
    assert entry_terms.predict(100.0, 0.07, 2.0, 4.0, 0.5, 5.52) == pytest.approx(100.0 + 0.07 + 0.08 + 0.0276)
    assert entry_terms.predict(100.0, 0.07, 2.0, 4.0, 0.5, 5.52, "upstand") == pytest.approx(100.0 + 0.08 + 0.0276)
    assert entry_terms.predict(100.0, 0.07, 2.0, None, 0.5, 5.52, "sidewalk") == pytest.approx(100.0 + 0.07 + 0.0276)
    assert entry_terms.predict(100.0, 0.07, 2.0, None, 0.5, 5.52) is None
    assert entry_terms.predict(None, 0.07, 2.0, 4.0, 0.5, 5.52) is None


def test_r193_variants_band_each_plot_and_the_summary_counts_them():
    profile = project_profile.ProjectProfile("dummy.json", {"units_mm": True, "ramp": {"front_setback": {"default": 5520, "C10": 5500}},
                                                            "slopes_percent": {"front_min": 0.5, "front_max": 2.0}})
    row = {"pastEntry": "100.1776", "unitRev6": "DP2", "sidewalkWidthM": "4.0", "roadLevelLeft": "100.14", "roadLevelRight": "100.16",
           "officialNumber": "5", "neighbourhood": "IMC"}
    items = entry_terms.variants(row, 100.0, {"upstandM": 0.07, "crossFallPercent": 2.0}, profile, ["C10"], [0.005, 0.03])
    by = {i["variant"]: i for i in items}
    assert by["full"]["band"] == "within5mm" and by["noSidewalk"]["band"] == "worse"
    assert by["cornerByTypology"]["difference"] == pytest.approx(0.0)
    found = entry_terms.summary(items)
    assert found["full"]["all"]["within5mm"] == 1 and found["full"]["byUnit"]["DP2"]["within5mm"] == 1
