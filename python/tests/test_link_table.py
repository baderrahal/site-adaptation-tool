import json

import link_table


def square(plot, x, y, size=10, kind="VL4"):
    vertices = [[x, y], [x + size, y], [x + size, y + size], [x, y + size]]
    return {"kind": "plot_shape", "plot": str(plot), "type": kind, "area": size * size, "center": [x + size / 2, y + size / 2], "vertices": vertices}


def rows_file(source, rows, odd=()):
    return {"source": source, "rows": [dict(row, page=1, block=1) for row in rows], "odd": list(odd)}


def pdf_row(plot, code, e, n, kind="DT3", row=1):
    return {"plot": plot, "code": code, "type": kind, "e": e, "n": n, "row": row}


def excel_file(source, plots):
    rows = []
    for number, (plot, unit) in enumerate(plots, start=40):
        rows.append({"sheetRow": number, "Plot No.": plot, "Unit": unit, "FFL": 10.5, "SSL": 10.425, "El @ Entry*": 10.12345, "Height**": 0.37655, "Site Steps Needed": "2 Steps", "Steps Needed": "3 steps"})
    return {"source": source, "rows": rows}


def kinds(conflicts):
    return sorted(conflict["kind"] for conflict in conflicts)


def test_r22_plots_are_joined_by_number_to_pdf_rows_and_the_excel_row():
    plots = {"plots": [square(1, 0, 0), square(2, 20, 0)]}
    rows = [rows_file("a.pdf", [pdf_row(1, 1, 2, 3), pdf_row(1, 2, 8, 7), pdf_row(2, 1, 22, 3, row=3)])]
    summary, links, conflicts = link_table.build(plots, rows, [excel_file("book.xlsx", [(1, "DT3"), (2, "DT3")])])
    first = next(link for link in links if link["plot"] == 1)
    assert len(first["pdf"]) == 2
    assert first["excel"]["ffl"] == 10.5
    assert first["excel"]["entry"] == 10.12345
    assert first["excel"]["steps"] == "3 steps"
    assert first["excel"]["row"] == 40
    assert first["cad"]["area"] == 100
    assert summary["all3"] == 2
    assert conflicts == []


def test_r22_points_inside_the_plot_agree_and_points_outside_are_a_location_conflict():
    plots = {"plots": [square(1, 0, 0), square(2, 20, 0)]}
    rows = [rows_file("a.pdf", [pdf_row(1, 1, 2, 3), pdf_row(2, 1, 5, 5)])]
    summary, links, conflicts = link_table.build(plots, rows, [excel_file("book.xlsx", [(1, "DT3"), (2, "DT3")])])
    assert summary["locationOk"] == 1
    assert summary["locationBad"] == 1
    bad = [conflict for conflict in conflicts if conflict["kind"] == "location"]
    assert [conflict["plot"] for conflict in bad] == [2]
    assert bad[0]["holder"] == 1
    assert summary["holderOffsets"] == {"-1": 1, "0": 1}


def test_r22_point_just_outside_the_edge_is_inside_the_tolerance():
    plots = {"plots": [square(1, 0, 0)]}
    rows = [rows_file("a.pdf", [pdf_row(1, 1, 10.5, 5)])]
    assert link_table.build(plots, rows, [excel_file("b.xlsx", [(1, "DT3")])], 1.0)[0]["locationOk"] == 1
    assert link_table.build(plots, rows, [excel_file("b.xlsx", [(1, "DT3")])], 0.2)[0]["locationBad"] == 1


def test_r22_missing_matches_are_listed_by_kind_with_plot_numbers():
    plots = {"plots": [square(1, 0, 0), square(2, 20, 0)]}
    rows = [rows_file("a.pdf", [pdf_row(1, 1, 2, 3), pdf_row(7, 1, 100, 100)])]
    coords = [excel_file("b.xlsx", [(1, "DT3"), (9, "DT3")])]
    summary, links, conflicts = link_table.build(plots, rows, coords)
    missing = {conflict["kind"]: conflict["plots"] for conflict in conflicts if "plots" in conflict}
    assert missing["no_pdf"] == [2]
    assert missing["no_excel"] == [2]
    assert missing["pdf_no_cad"] == [7]
    assert missing["excel_no_cad"] == [9]
    assert missing["pdf_no_excel"] == [7]
    assert missing["excel_no_pdf"] == [9]
    assert summary["conflicts"]["no_pdf"] == 1


def test_r22_duplicates_are_conflicts_and_are_never_picked_silently():
    plots = {"plots": [square(1, 0, 0), square(1, 50, 0), square(2, 20, 0)]}
    rows = [rows_file("a.pdf", [pdf_row(2, 1, 22, 3), pdf_row(2, 1, 23, 3, row=2), pdf_row(1, 1, 2, 3)])]
    coords = [excel_file("a.xlsx", [(2, "DT3")]), excel_file("b.xlsx", [(2, "DT3")])]
    summary, links, conflicts = link_table.build(plots, rows, coords)
    assert "duplicate_cad" in kinds(conflicts)
    assert "duplicate_excel" in kinds(conflicts)
    point = next(conflict for conflict in conflicts if conflict["kind"] == "pdf_duplicate_point")
    assert point["plot"] == 2 and point["code"] == 1 and point["samePlace"] is False
    doubled = next(link for link in links if link["plot"] == 1)
    assert "cad" not in doubled
    assert "excel" not in next(link for link in links if link["plot"] == 2)


def test_r22_unit_type_that_differs_between_pdf_and_excel_is_a_conflict():
    plots = {"plots": [square(1, 0, 0)]}
    rows = [rows_file("a.pdf", [pdf_row(1, 1, 2, 3, "DT3")])]
    _, _, conflicts = link_table.build(plots, rows, [excel_file("b.xlsx", [(1, "VD2")])])
    assert conflicts[0] == {"kind": "type_mismatch", "plot": 1, "types": {"pdf": "DT3", "excel": "VD2"}}


def test_r22_several_unit_types_on_one_plot_and_odd_values_are_conflicts():
    plots = {"plots": [square(1, 0, 0)]}
    odd = [{"page": 1, "block": 1, "row": 2, "plot": 1, "why": "no decimal point in n"}]
    rows = [rows_file("a.pdf", [pdf_row(1, 1, 2, 3, "DT3"), pdf_row(1, 2, 3, 3, "DT1", row=2)], odd)]
    _, _, conflicts = link_table.build(plots, rows, [excel_file("b.xlsx", [(1, "DT3")])])
    assert "pdf_type_mixed" in kinds(conflicts)
    assert "pdf_odd_value" in kinds(conflicts)


def test_r22_arc_edge_of_a_plot_holds_a_point_inside_the_curve():
    half_disc = {"kind": "plot_shape", "plot": "1", "area": 39.3, "center": [0, 2], "vertices": [[5, 0], [-5, 0]], "arcs": [{"segment": 0, "center": [0, 0], "radius": 5, "ccw": True}]}
    outline = link_table.ring(half_disc)
    assert link_table.contains(outline, 0, 4.5)
    assert not link_table.contains(outline, 0, -1)


def test_r22_extents_say_yes_or_no_for_overlap():
    plots = {"plots": [square(1, 0, 0), square(2, 20, 0)]}
    near = rows_file("near.pdf", [pdf_row(1, 1, 5, 5)])
    far = rows_file("far.pdf", [pdf_row(1, 1, 5000, 5000)])
    layers = [{"source": "other.dwg", "extents": {"core": [0, 0, 100, 100], "kept": [-9, -9, 900, 900]}}]
    space = link_table.extents(plots, [near], layers)
    assert space["pdfOverlapCadPlots"] == "yes"
    assert space["allPairsOverlap"] == "yes"
    apart = link_table.extents(plots, [near, far], layers)
    assert apart["pdfOverlapCadPlots"] == "no"
    assert {pair["b"]: pair["overlap"] for pair in apart["pairs"] if pair["a"] == "cad plots"}["far.pdf"] is False


def test_r22_main_writes_links_conflicts_and_extents_and_keeps_four_decimals(tmp_path):
    cad = tmp_path / "cad"
    cad.mkdir()
    (cad / "plots.json").write_text(json.dumps({"plots": [square(1, 0, 0)]}), encoding="utf-8")
    (tmp_path / "rows.json").write_text(json.dumps(rows_file("a.pdf", [pdf_row(1, 1, 2, 3)])), encoding="utf-8")
    (tmp_path / "coords.json").write_text(json.dumps(excel_file("b.xlsx", [(1, "DT3")])), encoding="utf-8")
    out = tmp_path / "out"
    code = link_table.main([str(out), "--cad", str(cad), "--rows", str(tmp_path / "rows.json"), "--coords", str(tmp_path / "coords.json")])
    assert code == 0
    links = json.loads((out / "links.json").read_text(encoding="utf-8"))
    assert links["summary"]["all3"] == 1
    assert links["links"][0]["excel"]["entry"] == 10.1235
    assert links["links"][0]["excel"]["ffl"] == 10.5
    assert json.loads((out / "conflicts.json").read_text(encoding="utf-8"))["conflicts"] == []
    assert json.loads((out / "extents.json").read_text(encoding="utf-8"))["pdfOverlapCadPlots"] == "yes"
