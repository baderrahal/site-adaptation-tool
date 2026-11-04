"""Writes a small made up project into a folder, so the acceptance tests run on GitHub with no real file. Every name and number here is
invented. Two areas far apart, the first with two blocks split by a road, the second with one block. Layer and text words are arguments,
so a test can change every name and see that the groups, found by shape, stay the same.
"""
from pathlib import Path

from openpyxl import Workbook

PLOT_W = 12.0
PLOT_D = 20.0
ROAD = 15.0
FAR = 300.0

# number, area, block, villa type, mirrored, the column and row of the plot in its block
PLOTS = [
    ("101", "A", "A1", "VA1", False, 0, 0),
    ("102", "A", "A1", "VA1", True, 1, 0),
    ("103", "A", "A1", "VB2", False, 2, 0),
    ("104", "A", "A2", "VB2", True, 0, 1),
    ("105", "A", "A2", "VA1", False, 1, 1),
    ("201", "B", "B1", "VB2", False, 0, 0),
    ("202", "B", "B1", "VA1", True, 1, 0),
    ("203", "B", "B1", "VA1", False, 2, 0),
]

NAMES = {"plots": "LOTS", "numbers": "LOT-NO", "houses": "HOUSES", "kerb": "KERB"}
WORDS = ("Phase", "Block")


def corner(plot):
    _, area, _, _, _, column, row = plot
    x = column * PLOT_W + (FAR if area == "B" else 0.0)
    y = row * (PLOT_D + ROAD)
    return x, y


def pairs(*items):
    return "".join("%d\n%s\n" % (code, value) for code, value in items)


def ring(layer, x, y, w, d):
    text = pairs((0, "LWPOLYLINE"), (8, layer), (90, 4), (70, 1))
    for px, py in ((x, y), (x + w, y), (x + w, y + d), (x, y + d)):
        text += pairs((10, px), (20, py))
    return text


def dxf_text(names=None, words=WORDS, number_style="%s"):
    """A DXF of the plots, their numbers, a block of each villa type inserted in each plot, and the road edges."""
    names = dict(NAMES, **(names or {}))
    blocks = ""
    for code in sorted({plot[3] for plot in PLOTS}):
        blocks += pairs((0, "BLOCK"), (8, "0"), (2, code), (70, 0), (10, 0.0), (20, 0.0), (30, 0.0), (3, code))
        blocks += ring("0", -4.0, -6.0, 8.0, 12.0)
        blocks += pairs((0, "ENDBLK"), (8, "0"))
    entities = ""
    for plot in PLOTS:
        x, y = corner(plot)
        entities += ring(names["plots"], x, y, PLOT_W, PLOT_D)
        entities += pairs((0, "TEXT"), (8, names["numbers"]), (10, x + 3.0), (20, y + 2.0), (30, 0.0), (40, 1.0), (1, number_style % plot[0]))
        entities += pairs((0, "INSERT"), (8, names["houses"]), (2, plot[3]), (10, x + PLOT_W / 2), (20, y + PLOT_D / 2), (30, 0.0), (41, -1.0 if plot[4] else 1.0), (42, 1.0), (43, 1.0))
    for row in (0, 1):
        y = PLOT_D + row * (PLOT_D + ROAD) + (ROAD if row else 0.0)
        for x0, x1 in ((0.0, 3 * PLOT_W), (FAR, FAR + 3 * PLOT_W)):
            entities += pairs((0, "LINE"), (8, names["kerb"]), (10, x0), (20, y), (30, 0.0), (11, x1), (21, y), (31, 0.0))
    for word, x in ((words[0] + " A", 0.0), (words[0] + " B", FAR)):
        entities += pairs((0, "TEXT"), (8, "NOTES"), (10, x), (20, -10.0), (30, 0.0), (40, 2.0), (1, word))
    return (pairs((0, "SECTION"), (2, "HEADER"), (9, "$ACADVER"), (1, "AC1015"), (0, "ENDSEC"))
            + pairs((0, "SECTION"), (2, "BLOCKS")) + blocks + pairs((0, "ENDSEC"))
            + pairs((0, "SECTION"), (2, "ENTITIES")) + entities + pairs((0, "ENDSEC"), (0, "EOF")))


def write_sheet(path, words=WORDS, number_style="%s"):
    book = Workbook()
    sheet = book.active
    sheet.title = "Plots"
    sheet.append(["Plot", words[0], words[1], "House type"])
    for plot in PLOTS:
        sheet.append([number_style % plot[0], plot[1], plot[2], plot[3]])
    book.save(path)


def write_project(folder, names=None, words=WORDS, number_style="%s", with_prototypes=True):
    """The folder as a team would hand it over: one drawing, one sheet, the prototype files by name."""
    folder = Path(folder)
    folder.mkdir(parents=True, exist_ok=True)
    (folder / "site.dxf").write_text(dxf_text(names, words, number_style), encoding="utf-8", newline="\n")
    write_sheet(folder / "plots.xlsx", words, number_style)
    if with_prototypes:
        protos = folder / "Prototypes"
        protos.mkdir(exist_ok=True)
        for code in sorted({plot[3] for plot in PLOTS}):
            (protos / ("ARC-%s-MOD.rvt" % code)).write_bytes(b"")
    return folder
