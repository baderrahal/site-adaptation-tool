import argparse
import re
import sys
from pathlib import Path

import pdfplumber

from common import r3, write_json

HEADER_ORDER = (("Point", "Plot"), ("Point", "Code"), ("Point", "Unit"), ("N",), ("E",))
COLUMN_SLACK = 4
ROW_TOLERANCE = 2.5
TINY_WIDTH = 2.0
KEY_PLAN_MIN_WORDS = 20
KEY_PLAN_MARGIN = 6
STAMP_MAX_X = 100
LONG_LINE = 150
REJECTED_LIMIT = 50
AXIS_TOLERANCE = 0.5
JOIN_GAP = 0.6
INTEGER = re.compile(r"^\d+$")
NUMBER = re.compile(r"^-?\d+(\.\d+)?$")


def cluster(items, key, tolerance):
    groups = []
    for item in sorted(items, key=key):
        if groups and key(item) - key(groups[-1][-1]) <= tolerance:
            groups[-1].append(item)
        else:
            groups.append([item])
    return groups


def find_blocks(words):
    """Header bands read Point Plot No., Point Code, Point Unit Type, N, E. Each band can hold several blocks side by side."""
    blocks = []
    for band in cluster(words, lambda word: word["top"], ROW_TOLERANCE):
        band = sorted(band, key=lambda word: word["x0"])
        texts = [word["text"] for word in band]
        position = 0
        anchors = []
        for index, text in enumerate(texts):
            nxt = texts[index + 1] if index + 1 < len(texts) else None
            if text == "Point" and nxt == "Plot":
                position = 1
                anchors = [band[index]["x0"]]
                continue
            if position == 0:
                continue
            wanted = HEADER_ORDER[position]
            if text == wanted[0] and (len(wanted) == 1 or nxt == wanted[1]):
                anchors.append(band[index]["x0"])
                position += 1
                if position == len(HEADER_ORDER):
                    blocks.append({
                        "anchors": anchors,
                        "top": min(word["top"] for word in band),
                        "bottom": max(word["bottom"] for word in band),
                    })
                    position = 0
    return sorted(blocks, key=lambda block: (block["top"], block["anchors"][0]))


def block_edges(block):
    pitch = block["anchors"][4] - block["anchors"][3]
    edges = [anchor - COLUMN_SLACK for anchor in block["anchors"]]
    edges.append(block["anchors"][4] + pitch - COLUMN_SLACK)
    return edges


def read_row(words, edges):
    """Returns the record and a note. The note is set when the row was read but a value looks wrong."""
    cells = [[] for _ in range(5)]
    for word in words:
        for column in range(5):
            if edges[column] <= word["x0"] < edges[column + 1]:
                cells[column].append(word["text"])
                break
    if any(len(cell) != 1 for cell in cells):
        return None, "cells " + "/".join(str(len(cell)) for cell in cells)
    plot, code, unit, north, east = (cell[0] for cell in cells)
    if not (INTEGER.match(plot) and INTEGER.match(code) and NUMBER.match(north) and NUMBER.match(east)):
        return None, "format"
    record = {"plot": int(plot), "code": int(code), "type": unit, "n": float(north), "e": float(east)}
    odd = [name for name, text in (("n", north), ("e", east)) if "." not in text]
    return record, (("no decimal point in " + " and ".join(odd)) if odd else None)


def read_table(words, page_number, first_block_index):
    """Rows of every block on the page in reading order, blocks left to right, rows top to bottom."""
    blocks = find_blocks(words)
    blocks.sort(key=lambda block: (round(block["top"]), block["anchors"][0]))
    rows = []
    summary = []
    rejected = []
    odd = []
    claimed = set()
    for number, block in enumerate(blocks, start=first_block_index):
        edges = block_edges(block)
        inside = [
            word for word in words
            if word["top"] > block["bottom"] - 0.5 and edges[0] <= word["x0"] < edges[5]
        ]
        count = 0
        for band in cluster(inside, lambda word: word["top"], ROW_TOLERANCE):
            claimed.update(id(word) for word in band)
            record, problem = read_row(band, edges)
            if record is None:
                rejected.append({"page": page_number, "block": number, "top": r3(band[0]["top"]), "why": problem})
                continue
            count += 1
            record.update({"page": page_number, "block": number, "row": count})
            rows.append(record)
            if problem:
                odd.append({"page": page_number, "block": number, "row": count, "plot": record["plot"], "why": problem})
        summary.append({"page": page_number, "block": number, "x": r3(block["anchors"][0]), "rows": count})
    return blocks, rows, summary, rejected, odd, claimed


def continued_plots(rows):
    """Plots whose rows run from the foot of one block to the top of the next."""
    joined = set()
    by_block = {}
    for row in rows:
        by_block.setdefault((row["page"], row["block"]), []).append(row)
    keys = sorted(by_block)
    for first, second in zip(keys, keys[1:]):
        if first[0] == second[0] and by_block[first][-1]["plot"] == by_block[second][0]["plot"]:
            joined.add(by_block[first][-1]["plot"])
    return len(joined)


def key_plan_box(words, blocks):
    tiny = [word for word in words if word["x1"] - word["x0"] < TINY_WIDTH]
    if len(tiny) < KEY_PLAN_MIN_WORDS:
        return None
    return (
        min(word["x0"] for word in tiny) - KEY_PLAN_MARGIN,
        min(word["top"] for word in tiny) - KEY_PLAN_MARGIN,
        max(word["x1"] for word in tiny) + KEY_PLAN_MARGIN,
        max(word["bottom"] for word in tiny) + KEY_PLAN_MARGIN,
    )


def in_box(box, x0, top, x1, bottom):
    return box is not None and x0 >= box[0] and x1 <= box[2] and top >= box[1] and bottom <= box[3]


def keep_shape(shape, regions, box, table_page):
    if not table_page:
        return True
    x0, x1 = sorted((shape["x0"], shape["x1"]))
    top, bottom = sorted((shape["top"], shape["bottom"]))
    if in_box(box, x0, top, x1, bottom):
        return False
    for left, region_top, right, region_bottom in regions:
        if x0 >= left and x1 <= right and top >= region_top and bottom <= region_bottom:
            return True
    return max(x1 - x0, bottom - top) >= LONG_LINE


def merge_lines(lines):
    """A table rule is drawn cell by cell, so touching pieces on one rule become one line."""
    horizontal, vertical, other = {}, {}, []
    for line in lines:
        if abs(line["top"] - line["bottom"]) < AXIS_TOLERANCE:
            horizontal.setdefault((line["page"], round((line["top"] + line["bottom"]) / 2, 1)), []).append((min(line["x0"], line["x1"]), max(line["x0"], line["x1"])))
        elif abs(line["x0"] - line["x1"]) < AXIS_TOLERANCE:
            vertical.setdefault((line["page"], round((line["x0"] + line["x1"]) / 2, 1)), []).append((min(line["top"], line["bottom"]), max(line["top"], line["bottom"])))
        else:
            other.append(line)
    merged = []
    for (page, level), spans in sorted(horizontal.items()):
        for start, end in join_spans(spans):
            merged.append({"page": page, "kind": "line", "x0": r3(start), "top": level, "x1": r3(end), "bottom": level})
    for (page, level), spans in sorted(vertical.items()):
        for start, end in join_spans(spans):
            merged.append({"page": page, "kind": "line", "x0": level, "top": r3(start), "x1": level, "bottom": r3(end)})
    return merged + other


def join_spans(spans):
    joined = []
    for start, end in sorted(spans):
        if joined and start <= joined[-1][1] + JOIN_GAP:
            joined[-1][1] = max(joined[-1][1], end)
        else:
            joined.append([start, end])
    return joined


def point_label(word, number):
    return {
        "page": number,
        "text": word["text"],
        "x0": r3(word["x0"]),
        "top": r3(word["top"]),
        "x1": r3(word["x1"]),
        "bottom": r3(word["bottom"]),
    }


def read_pdf(pdf_path):
    pages, labels, shapes, rows, blocks, rejected, odd = [], [], [], [], [], [], []
    skipped = {"stamp": 0, "keyPlan": 0, "lines": 0}
    with pdfplumber.open(str(pdf_path)) as pdf:
        for number, page in enumerate(pdf.pages, start=1):
            pages.append({"page": number, "width": r3(page.width), "height": r3(page.height)})
            words = page.extract_words(extra_attrs=["upright"])
            stamp = [word for word in words if not word["upright"] and word["x0"] < STAMP_MAX_X]
            stamped = {id(word) for word in stamp}
            upright = [word for word in words if id(word) not in stamped]
            found, page_rows, summary, page_rejected, page_odd, claimed = read_table(upright, number, len(blocks) + 1)
            box = key_plan_box([word for word in upright if id(word) not in claimed], found)
            rows.extend(page_rows)
            blocks.extend(summary)
            rejected.extend(page_rejected)
            odd.extend(page_odd)
            skipped["stamp"] += len(stamp)
            for word in upright:
                if id(word) not in claimed and in_box(box, word["x0"], word["top"], word["x1"], word["bottom"]):
                    skipped["keyPlan"] += 1
                else:
                    labels.append(point_label(word, number))
            regions = []
            for block in found:
                edges = block_edges(block)
                last = max([word["bottom"] for word in upright if id(word) in claimed and edges[0] <= word["x0"] < edges[5]] or [block["bottom"]])
                regions.append((edges[0] - COLUMN_SLACK, block["top"] - COLUMN_SLACK, edges[5] + COLUMN_SLACK, last + COLUMN_SLACK))
            kept = []
            for line in page.lines:
                start, end = line["pts"][0], line["pts"][-1]
                shape = {"page": number, "kind": "line", "x0": r3(start[0]), "top": r3(start[1]), "x1": r3(end[0]), "bottom": r3(end[1])}
                if keep_shape(shape, regions, box, bool(found)):
                    kept.append(shape)
                else:
                    skipped["lines"] += 1
            shapes.extend(merge_lines(kept) if found else kept)
            for rect in page.rects:
                shapes.append({"page": number, "kind": "rect", "x0": r3(rect["x0"]), "top": r3(rect["top"]), "x1": r3(rect["x1"]), "bottom": r3(rect["bottom"])})
    table = {
        "blocks": blocks,
        "rows": rows,
        "continued": continued_plots(rows),
        "skipped": skipped,
        "rejected": rejected[:REJECTED_LIMIT],
        "rejectedCount": len(rejected),
        "odd": odd,
    }
    return pages, labels, shapes, table


def write_outputs(pdf_path, out_dir):
    pages, labels, shapes, table = read_pdf(pdf_path)
    source = Path(pdf_path).name
    out_dir = Path(out_dir)
    write_json(out_dir / "pdf_labels.json", {"source": source, "units": "pt from top left", "pages": pages, "labels": labels})
    write_json(out_dir / "pdf_shapes.json", {"source": source, "units": "pt from top left", "shapes": shapes})
    write_json(out_dir / "pdf_rows.json", dict({"source": source}, **table))
    return len(labels), len(shapes), len(table["rows"])


def main(argv=None):
    parser = argparse.ArgumentParser(description="Read text labels, vector shapes and table rows from a PDF.")
    parser.add_argument("pdf")
    parser.add_argument("out_dir")
    args = parser.parse_args(argv)
    if not Path(args.pdf).is_file():
        print("PDF not found: " + args.pdf, file=sys.stderr)
        return 2
    label_count, shape_count, row_count = write_outputs(args.pdf, args.out_dir)
    print("labels " + str(label_count) + ", shapes " + str(shape_count) + ", rows " + str(row_count))
    return 0


if __name__ == "__main__":
    sys.exit(main())
