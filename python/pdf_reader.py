import argparse
import sys
from pathlib import Path

import pdfplumber

from common import r3, write_json


def read_pdf(pdf_path):
    labels = []
    shapes = []
    pages = []
    with pdfplumber.open(str(pdf_path)) as pdf:
        for number, page in enumerate(pdf.pages, start=1):
            pages.append({"page": number, "width": r3(page.width), "height": r3(page.height)})
            for word in page.extract_words():
                labels.append({
                    "page": number,
                    "text": word["text"],
                    "x0": r3(word["x0"]),
                    "top": r3(word["top"]),
                    "x1": r3(word["x1"]),
                    "bottom": r3(word["bottom"]),
                })
            for line in page.lines:
                start, end = line["pts"][0], line["pts"][-1]
                shapes.append({
                    "page": number,
                    "kind": "line",
                    "x0": r3(start[0]),
                    "top": r3(start[1]),
                    "x1": r3(end[0]),
                    "bottom": r3(end[1]),
                })
            for rect in page.rects:
                shapes.append({
                    "page": number,
                    "kind": "rect",
                    "x0": r3(rect["x0"]),
                    "top": r3(rect["top"]),
                    "x1": r3(rect["x1"]),
                    "bottom": r3(rect["bottom"]),
                })
    return pages, labels, shapes


def write_outputs(pdf_path, out_dir):
    pages, labels, shapes = read_pdf(pdf_path)
    source = Path(pdf_path).name
    out_dir = Path(out_dir)
    write_json(out_dir / "pdf_labels.json", {"source": source, "units": "pt from top left", "pages": pages, "labels": labels})
    write_json(out_dir / "pdf_shapes.json", {"source": source, "units": "pt from top left", "shapes": shapes})
    return len(labels), len(shapes)


def main(argv=None):
    parser = argparse.ArgumentParser(description="Read text labels and vector shapes from a PDF.")
    parser.add_argument("pdf")
    parser.add_argument("out_dir")
    args = parser.parse_args(argv)
    if not Path(args.pdf).is_file():
        print("PDF not found: " + args.pdf, file=sys.stderr)
        return 2
    label_count, shape_count = write_outputs(args.pdf, args.out_dir)
    print("labels " + str(label_count) + ", shapes " + str(shape_count))
    return 0


if __name__ == "__main__":
    sys.exit(main())
