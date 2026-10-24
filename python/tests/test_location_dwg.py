import location_dwg

SETTINGS = {"fitDropFactor": 3, "fitRounds": 2, "fitFloorPt": 0.001, "fitFloorM": 0.001}


# R196 the location plan DWG against the PDF
def test_r196_plot_text_gives_the_number_on_its_last_line_and_the_code_above():
    texts = [{"layer": "MP_PLOT_TEXT", "text": "DP4 (BB)\n543", "x": 1.0, "y": 2.0}, {"layer": "MP_PLOT_TEXT", "text": "ROW - Internal Road\n3159", "x": 3, "y": 4},
             {"layer": "MP_PLOT_TEXT", "text": "NCC", "x": 5, "y": 6}, {"layer": "OTHER", "text": "VL4\n9", "x": 0, "y": 0},
             {"layer": "MP_PLOT_TEXT", "text": "VL4\n543", "x": 7, "y": 8}]
    found, twice = location_dwg.dwg_labels(texts, "MP_PLOT_TEXT")
    assert set(found) == {"543", "3159"} and found["3159"]["code"] == "ROW - Internal Road" and twice == ["543"]


def test_r196_compare_counts_numbers_in_one_only_and_codes_as_written():
    dwg = {"1": {"code": "VL4"}, "2": {"code": "DP4 (BB)"}, "17": {"code": "MIXEDUSE - HOTEL | FESTIVAL BUILDING"}}
    pdf = {"1": {"code": "VL4"}, "2": {"code": "DP4 (SA)"}}
    both, counts = location_dwg.compare(dwg, pdf)
    assert both == ["1", "2"] and counts["onlyDwg"] == ["17"] and counts["codes"] == {"same": 1, "differs": 1}
    assert counts["codeDifferences"] == {"DP4 (SA)>DP4 (BB)": 1}


def test_r196_position_fit_from_millimetres_to_page_and_site():
    dwg = {str(k): {"x": 1000.0 * k, "y": 500.0 * (k % 3)} for k in range(1, 8)}
    pdf = {str(k): {"pageX": 2.0 * k, "pageY": (k % 3) * 1.0, "east": 1.0 * k + 500, "north": 0.5 * (k % 3) + 900} for k in range(1, 8)}
    found = location_dwg.position_fit(dwg, pdf, sorted(dwg, key=int), SETTINGS)
    assert found["toPagePt"]["kept"] == 7 and found["toPagePt"]["residualKept"]["max"] < 1e-6
    assert found["toSiteM"]["residualKept"]["max"] < 1e-6
