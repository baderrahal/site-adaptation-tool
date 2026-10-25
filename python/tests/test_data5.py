import csv
import json
from pathlib import Path

import numpy as np
import pytest
from shapely.geometry import LineString, box

import area_key
import c3d_connect
import c3d_window
import coverage
import data4_audit
import data5_check
import dxf_count
import ifc_header
import im_codes
import project_profile
import rev9_parcels
import road_layouts
import xref_search
from test_data_book import Fake, book_config

ROOT = Path(__file__).resolve().parents[2]
END = chr(59)


def write_csv(path, rows):
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, list(rows[0]), lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)


def dxf_text(blocks, entities, classes=()):
    """A small ASCII DXF: each block is (name, base, entity lines), each entity a list of (code, value) pairs."""
    lines = ["0", "SECTION", "2", "HEADER", "9", "$DIMSTYLE", "2", "Standard", "0", "ENDSEC", "0", "SECTION", "2", "CLASSES"]
    for name, count in classes:
        lines += ["0", "CLASS", "1", name, "2", "Cpp" + name, "90", "0", "91", str(count), "280", "0", "281", "1"]
    lines += ["0", "ENDSEC", "0", "SECTION", "2", "BLOCKS"]
    for name, base, body in blocks:
        lines += ["0", "BLOCK", "8", "0", "2", name, "70", "0", "10", str(base[0]), "20", str(base[1]), "30", "0", "3", name, "1", ""]
        for pairs in body:
            for code, value in pairs:
                lines += [str(code), str(value)]
        lines += ["0", "ENDBLK", "8", "0"]
    lines += ["0", "ENDSEC", "0", "SECTION", "2", "ENTITIES"]
    for pairs in entities:
        for code, value in pairs:
            lines += [str(code), str(value)]
    lines += ["0", "ENDSEC", "0", "EOF"]
    return "\n".join(lines) + "\n"


def line(x1, y1, x2, y2, layer="L"):
    return [(0, "LINE"), (8, layer), (10, x1), (20, y1), (30, 0), (11, x2), (21, y2), (31, 0)]


def insert(name, x, y, rotation=0, scale=(1, 1), flip=False, attributes=()):
    pairs = [(0, "INSERT"), (8, "0")] + ([(66, 1)] if attributes else []) + [(2, name), (10, x), (20, y), (30, 0), (41, scale[0]), (42, scale[1]), (50, rotation)]
    if flip:
        pairs += [(210, 0), (220, 0), (230, -1)]
    for tag, value in attributes:
        pairs += [(0, "ATTRIB"), (8, "0"), (10, 99), (20, 99), (1, value), (2, tag), (70, 0)]
    if attributes:
        pairs += [(0, "SEQEND"), (8, "0")]
    return pairs


# R251 the audit of DATA-4
def test_r251_groups_tell_named_plots_from_dwg_text_only_and_count_each_plot_once():
    rows = [{"verdicts": "shape real"}, {"verdicts": "shape real|code real"}, {"verdicts": "shape real|number real|code real"}, {"verdicts": "dwgText"}, {"verdicts": ""}]
    found = data4_audit.verdict_counts(rows)
    assert [data4_audit.group_of(r["verdicts"]) for r in rows] == ["named", "named", "named", "dwgTextOnly", "none"]
    assert found["verdictsAddedUp"] == 6 and found["namedPlots"] == 3 and found["groups"] == {"named": 3, "dwgTextOnly": 1, "none": 1}


def test_r251_a_parcel_code_difference_the_pdf_could_not_see_is_named():
    units = {"5": {"clientType": "VL4", "typologyRev9": "DP2", "locationPlanNumber": "15|7"}, "6": {"clientType": "VL4", "typologyRev9": "VD4", "locationPlanNumber": "16"}}
    parcel_of = {"5": {"number": "7", "style": "OS", "faceClass": "shapeDiffers"}, "6": {"number": "16", "style": "VD4", "faceClass": "same"}}
    faces = {"5": {"faceClass": "noFace", "numberClass": "manyLabels", "codeClass": "noLabel"}}
    found = data4_audit.code_differences(units, parcel_of, faces, {"5": {"verdicts": "shape real"}})
    assert [(f["baseplanLabel"], f["pdfCode"], f["codeVerdict"]) for f in found] == [("5", "noLabel", False)]
    assert data4_audit.classes(parcel_of["5"], units["5"]) == ("noPlanNumber", "differs")
    assert data4_audit.classes(None, units["6"]) == ("noParcel", "noParcel")


def test_r251_claims_name_each_count_that_differs_and_the_faces_parcels_csv_lost():
    claims = data4_audit.against_claims({"faces": {"same": 3}, "redo": {"namedPlots": 97}}, [{"where": "log", "stated": 3, "count": "faces.same"},
                                                                                             {"where": "prompt", "stated": 105, "count": "redo.namedPlots"},
                                                                                             {"where": "log", "stated": 2, "count": "faces.noFace"}])
    assert [c["equal"] for c in claims] == [True, False, False] and claims[2]["found"] == 0
    lost = data4_audit.lost_by_parcels_csv({"1": {}, "2": {}}, {"1": {"number": "9", "faceClass": "same"}, "2": {"number": "9", "faceClass": "noFace"}},
                                           [{"number": "9", "baseplanLabel": "1"}])
    assert lost == [{"baseplanLabel": "2", "parcel": "9", "faceClass": "noFace", "parcelsCsvFace": "1"}]


def test_r251_parcel_matches_keep_every_face_when_two_share_a_parcel(tmp_path):
    by_label = {"1": {"parcel": "9", "style": "VL4", "iou": 0.8, "faceClass": "shapeDiffers", "numberClass": "same", "codeClass": "same"},
                "2": {"parcel": "9", "style": "VL4", "iou": 0.4, "faceClass": "noFace", "numberClass": "differs", "codeClass": "same"}}
    rev9_parcels.write_matches(tmp_path / "m.csv", by_label)
    rows = list(csv.DictReader(open(tmp_path / "m.csv", encoding="utf-8")))
    assert [(r["baseplanLabel"], r["parcel"], r["faceClass"]) for r in rows] == [("1", "9", "shapeDiffers"), ("2", "9", "noFace")]


def test_r251_the_sample_holds_the_recount_against_the_stored_classes():
    units = {str(k): {"clientType": "VL4", "typologyRev9": "VL4", "locationPlanNumber": str(k)} for k in range(30)}
    parcel_of = {k: {"number": k, "style": "VL4", "faceClass": "same", "numberClass": "same", "codeClass": "same"} for k in units}
    parcel_of["3"]["codeClass"] = "differs"
    found = data4_audit.sample(units, parcel_of, {}, 14, 30)
    assert found["plots"] == 30 and found["agree"] == 29


# R252 the xref search
def test_r252_the_file_an_xref_asks_for_is_read_as_a_windows_path_and_searched_by_name(tmp_path):
    assert xref_search.file_name(".\\Road Layout_new.dwg", "x") == "Road Layout_new.dwg"
    assert xref_search.file_name(None, "XR-TITLE BLOCK") == "XR-TITLE BLOCK.dwg"
    (tmp_path / "a" / "b").mkdir(parents=True)
    (tmp_path / "a" / "b" / "road layout_NEW.dwg").write_bytes(b"x")
    (tmp_path / "a" / "Road Layout.dwg").write_bytes(b"x")
    same, near = xref_search.search("Road Layout_new.dwg", [str(tmp_path)], 0.75)
    assert len(same) == 1 and same[0].endswith("road layout_NEW.dwg")
    assert [Path(n["file"]).name for n in near] == ["Road Layout.dwg"]


def test_r252_xrefs_come_with_their_inserts(tmp_path):
    (tmp_path / "layers.json").write_text(json.dumps({"xrefs": [{"name": "X", "path": ".\\X.dwg", "found": False, "inserts": 1}]}), encoding="utf-8")
    (tmp_path / "blocks.json").write_text(json.dumps({"blocks": [{"name": "X", "note": "xref", "insertPoint": [0, 0], "scale": [1, 1, 1], "rotationDeg": 0.0},
                                                                 {"name": "Y", "insertPoint": [5, 5], "scale": [1, 1, 1], "rotationDeg": 0.0}]}), encoding="utf-8")
    found = xref_search.xrefs_of(tmp_path)
    assert found[0]["path"] == ".\\X.dwg" and found[0]["insertRows"] == [{"point": [0, 0], "scale": [1, 1, 1], "rotationDeg": 0.0}]


# R253 the DXF of a copy and its count
def test_r253_the_dxf_script_runs_dxfout_in_place_of_the_probe_read(tmp_path):
    text = c3d_window.script_text(tmp_path / "probe.dll", tmp_path / "out" / "a.dxf")
    lines = text.splitlines()
    assert lines[0] == "NETLOAD" and "DXFOUT" in lines and "C3DQUIT" in lines and "C3DDATA" not in lines
    assert c3d_window.script_text(tmp_path / "probe.dll").splitlines()[2:] == ["C3DDATA", "C3DQUIT"]


def test_r253_a_dxf_path_in_the_work_or_source_folder_is_refused(tmp_path):
    config = book_config(tmp_path)
    (tmp_path / "book" / "work" / "Base Plans" / "cut.dwg").write_bytes(b"dwg")
    for target in (tmp_path / "book" / "work" / "cut.dxf", tmp_path / "source" / "cut.dxf"):
        with pytest.raises(ValueError):
            c3d_window.run(config, "cutsheet", launcher=lambda a, e: Fake(1, polls=[0]), memory=lambda: 4.0, sleep=lambda s: None, dxf=target)


def test_r253_a_dxf_run_is_done_when_the_dxf_was_written_and_the_copy_is_unchanged(tmp_path):
    config = book_config(tmp_path)
    (tmp_path / "book" / "work" / "Base Plans" / "cut.dwg").write_bytes(b"dwg")
    target = tmp_path / "dxf" / "cut.dxf"
    target.parent.mkdir()

    def launch(args, environment):
        if args[0] == "acad.exe":
            target.write_text("0\nEOF\n", encoding="ascii")
        return Fake(7, polls=[0])
    record = c3d_window.run(config, "cutsheet", launcher=launch, memory=lambda: 4.0, sleep=lambda s: None, clock=lambda: 0.0, dxf=target)
    assert record["done"] and record["unchanged"] and record["dxf"] == str(target)
    script = next((tmp_path / "book" / "logs").glob("*.scr")).read_text(encoding="ascii")
    assert "DXFOUT" in script


def test_r253_an_attribute_tag_never_renames_its_block_and_a_flipped_insert_mirrors_its_block(tmp_path):
    blocks = [("B", (0, 0), [insert("T", 0, 0, attributes=[("TAG", "v")]), line(1, 0, 2, 0)]), ("T", (0, 0), [line(0, 0, 0, 1)])]
    entities = [insert("B", 10, 10), insert("B", 10, 10, flip=True), line(50, 50, 60, 60)]
    path = tmp_path / "d.dxf"
    path.write_text(dxf_text(blocks, entities, classes=[("AECC_TIN_SURFACE", 2)]), encoding="utf-8")
    parsed = dxf_count.parse(path)
    assert sorted(parsed["blocks"]) == ["B", "T"] and parsed["blocks"]["B"]["entities"][0]["attributes"] == [{"type": "ATTRIB", "tag": "TAG", "text": "v"}]
    straight = dxf_count.placed(dxf_count.own_points(parsed["blocks"]["B"]["entities"][1]), parsed["entities"][0], (0, 0))
    mirrored = dxf_count.placed(dxf_count.own_points(parsed["blocks"]["B"]["entities"][1]), parsed["entities"][1], (0, 0))
    assert straight.tolist() == [[11.0, 10.0], [12.0, 10.0]] and mirrored.tolist() == [[-11.0, 10.0], [-12.0, 10.0]]
    found = dxf_count.count(parsed, box(0, 0, 30, 30))
    assert found["modelSpace"] == {"INSERT": 2, "LINE": 1} and found["explodedInside"] == {"LINE": 2}
    assert found["insertsInside"] == {"top": 1, "nested": 1} and found["attributesInside"] == {"top": 0, "nested": 1}
    assert found["classes"][0]["dxfName"] == "AECC_TIN_SURFACE" and found["civilTypes"] == {}


# R254 the readers against the third count
def test_r254_reader_rows_count_one_per_entity_with_both_polyline_kinds_as_one(tmp_path):
    write_csv(tmp_path / "entities.csv", [{"type": "LWPOLYLINE", "part": "0", "path": "/A"}, {"type": "LWPOLYLINE", "part": "1", "path": "/A"},
                                          {"type": "POLYLINE", "part": "0", "path": ""}, {"type": "LINE", "part": "0", "path": "/B/C"}])
    write_csv(tmp_path / "blocks.csv", [{"handle": "A", "name": "Arrow", "parentBlock": ""}, {"handle": "C", "name": "Inner", "parentBlock": "Outer"}])
    write_csv(tmp_path / "texts.csv", [{"type": "TEXT"}])
    found = dxf_count.reader_counts(tmp_path)
    assert found["entities"] == {"POLYLINE": 2, "LINE": 1} and found["partRows"] == 1 and found["nestedBlocks"] == 1
    rows = dxf_count.compare({"explodedInside": {"LWPOLYLINE": 2, "LINE": 2}}, found, {"entities": {"POLYLINE": 2, "LINE": 1}})
    assert rows == [{"type": "LINE", "dxf": 2, "probe": 1, "cadReader": 1}]


def test_r254_the_probe_rows_over_cadreader_are_named_by_their_top_insert(tmp_path):
    probe, cad = tmp_path / "p", tmp_path / "c"
    write_csv(probe / "entities.csv", [{"type": "LINE", "part": "0", "path": "/A/x"}, {"type": "LINE", "part": "0", "path": "/A/y"},
                                       {"type": "ARC", "part": "0", "path": "/Z"}, {"type": "TEXT", "part": "0", "path": "/A"}])
    write_csv(probe / "blocks.csv", [{"handle": "A", "name": "Arrow", "parentBlock": ""}])
    write_csv(cad / "entities.csv", [{"type": "LINE", "part": "0", "path": "A/x"}])
    found = dxf_count.gap_by_insert(probe, cad, {"LINE", "ARC"})
    assert sorted(found, key=lambda r: r["block"]) == [{"block": "Arrow", "rows": 1, "inserts": 1}, {"block": "insert outside Phase 1 Z", "rows": 1, "inserts": 1}]


# R255 the two road layouts
def test_r255_far_points_make_stretches_and_grid_cells():
    border = box(0, 0, 90, 90)
    a = [LineString([(0, 10), (60, 10)])]
    b = [LineString([(0, 10.2), (30, 10.2)])]
    far, total = road_layouts.far_points(a, c3d_connect.LineSet(b), 1.0, 1.0, border.contains)
    assert total == 60 and len(far) == 30
    assert road_layouts.stretches(far, 1.0) == [30.0]
    assert road_layouts.grid_cell(far[0][2], far[0][3], border.bounds) == "S" and road_layouts.grid_cell(1, 89, border.bounds) == "NW"


def test_r255_revision_texts_are_found_in_layouts_and_attributes_and_no_layout_is_picked():
    parsed = {"entities": [{"type": "TEXT", "layer": "T", "paper": False, "text": "Junction-03", "attributes": []},
                           {"type": "INSERT", "layer": "TB", "paper": False, "text": "", "attributes": [{"type": "ATTRIB", "tag": "REV", "text": "C02"}]}],
              "blocks": {"*Paper_Space0": {"entities": [{"type": "MTEXT", "layer": "TB", "paper": False, "text": "DATE 12/03/2023", "attributes": []}]}}}
    patterns = ["\\bREV", "\\b\\d{1,2}[-./]\\d{1,2}[-./](20)?\\d\\d\\b", "\\b(JUNE?|MAR(CH)?)[ -]\\d{2,4}\\b"]
    found = road_layouts.revision_texts(parsed, patterns)
    assert [(f["space"], f["tag"], f["text"]) for f in found] == [("model", "REV", "C02"), ("*Paper_Space0", "", "DATE 12/03/2023")]
    assert road_layouts.layout_content(parsed) == {"layouts": 1, "paperEntities": 1, "modelTexts": 1}
    image = road_layouts.svg(box(0, 0, 10, 10), [LineString([(1, 1), (9, 9)])], [], [(0, 0.0, 5, 5)], [], 100)
    assert image.startswith("<svg") and image.count("<circle") == 1


# R256 the area key
def test_r256_zone_and_neighbourhood_are_normal_and_dashes_are_no_value():
    found, clash = area_key.by_plot([{"plot": 12.0, "zone": "Zone 4", "nbh": "01a"}, {"plot": "13", "zone": 4, "nbh": "01A"},
                                     {"plot": "14", "zone": "-", "nbh": "-"}, {"plot": "Plot Number", "zone": "Zone", "nbh": "NBH"},
                                     {"plot": 15, "zone": "Zone 2", "nbh": "012"}, {"plot": 15, "zone": "Zone 2", "nbh": "013"}])
    assert found["12"] == ("Zone 4", "01A") == found["13"] and "14" not in found and "Plot Number" not in found and clash == {"15"}


def test_r256_one_key_per_plot_or_why_there_is_none():
    table = {"1": ("Zone 2", "012"), "2": ("Zone 2", "012"), "3": ("Zone 2", "013")}
    assert area_key.key_of(["1", "2"], table) == (("Zone 2", "012"), None)
    assert area_key.key_of(["1", "3"], table) == (None, "twoValues")
    assert area_key.key_of(["9"], table) == (None, "noRow") and area_key.key_of([], table) == (None, "noNumber")
    assert area_key.numbers_of({"locationPlanNumber": ""}, "44") == (["44"], "rev9Parcel")
    rows = area_key.area_rows([{"baseplanLabel": "a", "clientType": "VL4", "locationPlanNumber": "1"}, {"baseplanLabel": "b", "clientType": "OS", "locationPlanNumber": "3"},
                               {"baseplanLabel": "c", "clientType": "OS", "locationPlanNumber": "9"}], {}, table, {"1": ("Zone 2", "012"), "3": ("Zone 3", "013")})
    assert [(r["nbhId"], r["areaKeyMethod"], r["areaKeyCheck"]) for r in rows] == [("012", "locationPlanNumber", "same"), ("013", "locationPlanNumber", "differs"), (None, "noRow", None)]
    report = area_key.summary(rows, {"VL4"})
    assert report["noValue"] == ["c"] and report["differsFromSales"] == ["b"] and report["unitPlotsKeyed"] == 1


# R257 the IM codes
def test_r257_an_im_code_stands_alone_and_the_area_table_is_checked():
    assert im_codes.codes_in("01101-PSN-IMG-ZZZ-PIM-TU-000001 and IMC") == ["IMG", "IMC"]
    assert im_codes.codes_in("TRIMGAP SIMD IMGX") == []
    table = im_codes.load_table(ROOT / "config" / "area_codes.json")
    assert set(table) == set(im_codes.job_names(project_profile.load_settings()))
    assert all(area["plots"] > 0 for rows in table.values() for row in rows for area in row["areas"])


def test_r257_sources_are_held_against_the_row_of_their_code_in_their_job(tmp_path):
    area = {"nbh": "012", "zone": "Zone 2", "plots": 3, "kind": "schedule", "file": "s-IMX-1.xlsx", "measured": "a count"}
    table = {"parsons": [{"code": "IMX", "areas": [area]}], "native": []}
    sources = [{"name": "a", "file": "s-IMX-1.xlsx", "job": "parsons", "codes": ["IMX"], "byZoneAndNbh": {"Zone 2 012": 3, "noKey": 1}},
               {"name": "c", "file": "c-IMX-2.rvt", "job": "parsons", "codes": ["IMX"], "byZoneAndNbh": {"Zone 1 021": 2}},
               {"name": "d", "file": "d-IMQ-2.rvt", "job": "parsons", "codes": ["IMQ"], "byZoneAndNbh": {}}]
    assert [r["verdict"] for r in im_codes.table_against_sources(table, sources)] == ["agrees", "differs", "noRow"]
    (tmp_path / "x" / "y").mkdir(parents=True)
    (tmp_path / "x" / "y" / "01-PSN-IMA-ZZZ.rvt").write_bytes(b"")
    (tmp_path / "x" / "TRIMB.rvt").write_bytes(b"")
    assert im_codes.file_names(tmp_path) == [{"code": "IMA", "folder": str(Path("x") / "y"), "file": "01-PSN-IMA-ZZZ.rvt"}]
    record = tmp_path / "r.json"
    record.write_text(json.dumps({"documents": [{"file": "m-IMD-1", "links": [{"rawName": "p-IMC-2.rvt : 1"}]}],
                                  "files": [{"file": "f.rvt", "info": {"centralModelPath": "\\\\s\\Phase 4\\m-IMD-1.rvt"}, "references": [{"path": "..\\a-IMB.dwg"}]}]}), encoding="utf-8")
    assert sorted(c for t in im_codes.model_texts(record) for c in im_codes.codes_in(t)) == ["IMB", "IMC", "IMD", "IMD"]


# R258 the IFC header
def step_text():
    data = ["#1= IFCPROJECT('g1',#5,'Project A',$,$,$,$,(#9),#8)", "#2= IFCSITE('g2',#5,$,$,$,#7,$,$,.ELEMENT.,(0,0,0,0),(0,0,0,0),0.,$,$)",
            "#3= IFCCARTESIANPOINT((5.,5.,1.))", "#4= IFCCARTESIANPOINT((500.,500.,2.))",
            "#6= IFCPROPERTYSINGLEVALUE('Spot Level',$,IFCLABEL('a" + END + "b'),$)",
            "#10= IFCBUILDINGELEMENTPROXY('g3',#5,'Pipe (2)',$,$,#7,#7,$,$)"]
    head = ["ISO-10303-21", "HEADER", "FILE_DESCRIPTION(('View A'),'2" + END + "1')", "FILE_NAME('f.ifc','2023-08-06T17:47:21',('Me'),('Org'),'pre','app','')",
            "FILE_SCHEMA(('IFC4X1'))", "ENDSEC", "DATA"]
    return (END + "\n").join(head + data + ["ENDSEC", "END-ISO-10303-21"]) + END + "\n"


def test_r258_records_end_at_a_record_end_outside_a_string_and_names_read_their_arguments():
    text = step_text()
    rows = ifc_header.records(text)
    assert [t for _, t, _ in rows] == ["IFCPROJECT", "IFCSITE", "IFCCARTESIANPOINT", "IFCCARTESIANPOINT", "IFCPROPERTYSINGLEVALUE", "IFCBUILDINGELEMENTPROXY"]
    assert ifc_header.named(rows, "IFCPROJECT") == ["Project A"] and ifc_header.named(rows, "IFCSITE") == [None]
    head = ifc_header.header(text)
    assert head["schema"] == ["IFC4X1"] and head["timestamp"] == "2023-08-06T17:47:21" and head["fileName"][2] == "Me"
    assert ifc_header.split_arguments("'a',(1,2),'b,c',$") == ["'a'", "(1,2)", "'b,c'", "$"]
    assert ifc_header.points(rows).shape == (2, 3)
    assert ifc_header.level_names(rows, ["level", "spot"]) == {"IFCPROPERTYSINGLEVALUE Spot Level": 1}


def test_r258_the_ifc_is_read_from_its_copy_and_only_counts_are_written(tmp_path):
    root = tmp_path / "book"
    (root / "work" / "Downloads").mkdir(parents=True)
    (root / "work" / "Downloads" / "a.ifc").write_text(step_text(), encoding="utf-8")
    with open(root / "phase1_border.csv", "w", encoding="utf-8") as handle:
        handle.write("x,y\n0,0\n10,0\n10,10\n0,10\n")
    write_csv(root / "plots.csv", [{"roadLevelLeft": "1.5", "roadLevelRight": ""}])
    config = {"dataBook": {"root": str(root), "data5": {"outFolder": "data5", "ifc": {"outFolder": "ifc", "copy": "Downloads/a.ifc", "levelWords": ["spot"],
                                                                                         "labelProperties": ["Spot Level"]}}}}
    report = ifc_header.run(config)
    assert report["points"]["count"] == 2 and report["points"]["insidePhase1"] == 1 and report["points"]["zMeetsPhase1RoadLevels"]
    assert report["propertyValues"] == {"Spot Level a" + END + "b": 1} and (root / "data5" / "ifc" / "ifc.json").is_file()


# R259 the coverage table
def test_r259_rows_say_reader_space_kept_rows_and_what_is_not_read(tmp_path):
    source, root = tmp_path / "src", tmp_path / "book"
    for folder, name in (("Roads", "r.dwg"), ("Roads", "s.dwg"), ("Rev 9", "w.xlsx"), ("Rev 9", "p.pdf")):
        (source / folder).mkdir(parents=True, exist_ok=True)
        (source / folder / name).write_bytes(b"x")
    (root / "r").mkdir(parents=True)
    (root / "r" / "data.json").write_text(json.dumps({"reader": "C3DProbe", "counts": {"entities": {"kept": 3, "keptCut": 1, "outside": 9}, "surfaces": {"kept": 2},
                                                                                        "civil": {"drawing": 1, "alignmentOutside": 1}}}), encoding="utf-8")
    (root / "s").mkdir()
    (root / "s" / "data.json").write_text(json.dumps({"reader": "CadReader", "counts": {"entities": {"outside": 4}}}), encoding="utf-8")
    config = {"sourceRoot": str(source), "dataBook": {"root": str(root), "surfaceVersions": {"renames": {"Rev 9": "R9"}},
                                                      "data5": {"outFolder": "data5", "folders": ["Roads", "R9"],
                                                                "coverage": {"readers": {"R9/w.xlsx": "openpyxl"}, "notes": {"Roads/r.dwg": "nothing known"}}}}}
    rows = coverage.run(config, civil=lambda copy: {"AECC_TIN_SURFACE": 2})
    by_file = {r["file"]: r for r in rows}
    assert by_file["Roads/r.dwg"]["space"] == "site" and by_file["Roads/r.dwg"]["keptInsidePhase1"] == {"entities": 4, "surfaces": 2}
    assert by_file["Roads/r.dwg"]["civilRead"] == {"alignmentOutside": 1, "surfacesKept": 2} and by_file["Roads/r.dwg"]["notRead"] == "nothing known"
    assert by_file["Roads/s.dwg"]["space"] == "own" and by_file["Roads/s.dwg"]["notRead"] == "not given"
    assert by_file["R9/w.xlsx"]["reader"] == "openpyxl" and by_file["R9/w.xlsx"]["space"] == "none" and by_file["R9/p.pdf"]["space"] == "page fitted to site"
    assert (root / "data5" / "coverage.md").read_text(encoding="utf-8").count("\n| ") == 4


# R260 the second method
def test_r260_the_line_scan_of_a_dxf_agrees_with_the_parser(tmp_path):
    path = tmp_path / "d.dxf"
    path.write_text(dxf_text([("B", (0, 0), [line(0, 0, 1, 1)])], [insert("B", 1, 1, attributes=[("T", "v")]), line(0, 0, 2, 2), line(0, 0, 3, 3)]), encoding="utf-8")
    found = data5_check.check_dxf(path)
    assert found["typesEqual"] == found["types"] == 2 and found["differ"] == {} and found["blockRecords"] == found["blocksParsed"] == 1


def test_r260_brute_force_distance_and_the_area_and_ifc_checks():
    distance = data5_check.segment_distance(np.array([[0.0, 1.0], [5.0, 0.0]]), np.array([[0.0, 0.0]]), np.array([[2.0, 0.0]]))
    assert distance.round(6).tolist() == [1.0, 3.0]
    dlus = {"1": [{"zone": "Zone 4", "nbh": "01a"}], "2": [{"zone": 2.0, "nbh": "012"}]}
    rows = [{"locationPlanNumber": "1", "zone": "Zone 4", "nbhId": "01A"}, {"locationPlanNumber": "2", "zone": "Zone 2", "nbhId": "013"},
            {"locationPlanNumber": "9", "zone": "", "nbhId": ""}, {"locationPlanNumber": "", "zone": "", "nbhId": ""}]
    assert data5_check.check_area(rows, dlus) == {"match": 1, "mismatch": 1, "noDlusRow match": 1, "noPlanNumber noKey": 1}
    assert data5_check.check_curb([(1.0, 0.5), (1.0, 3.0)], [LineString([(0, 0), (2, 0)])], 1.0) == {"match": 2, "near": 1, "far": 1}
