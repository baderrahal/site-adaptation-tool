"""Makes the five scenario folders under the work root from a folder of inputs, copies only, with a manifest of SHA-256 per folder.

Usage: make_scenarios.py --source <inputs folder> --worker <CadReader.exe>. The source is read in place and held unchanged (D19), the run fails if
it changes. S3 and S5 change the drawings, so their drawings are written as DXF by the worker, and the words are changed in that text.
"""
import argparse
import json
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

from openpyxl import load_workbook

import scenario_tools as tools

DRAWINGS = {".dwg", ".dxf"}
HEADER_ROWS = 20


def to_dxf(worker, source, target):
    with tempfile.TemporaryDirectory() as temp:
        empty = Path(temp) / "map.json"
        empty.write_text("{}", encoding="utf-8")
        subprocess.run([str(worker), "--rename-layers", str(source), str(target), str(empty)], check=True, capture_output=True, text=True)


def change_drawing(worker, source, target, change_text, change_layer):
    target = target.with_suffix(".dxf")
    to_dxf(worker, source, target)
    # The bytes of a DXF may not all be UTF-8, surrogates carry them through unchanged
    text = target.read_bytes().decode("utf-8", errors="surrogateescape")
    target.write_bytes(tools.rewrite_dxf(text, change_text, change_layer).encode("utf-8", errors="surrogateescape"))


def change_headers(source, target):
    book = load_workbook(source)
    for sheet in book.worksheets:
        for row in sheet.iter_rows(min_row=1, max_row=HEADER_ROWS):
            for cell in row:
                if isinstance(cell.value, str) and not cell.value.startswith("="):
                    cell.value = tools.changed_words(cell.value)
    book.save(target)


def change_numbers(source, target, numbers):
    book = load_workbook(source)
    for sheet in book.worksheets:
        rows = [[cell.value for cell in row] for row in sheet.iter_rows()]
        columns = tools.columns_of_numbers(rows, numbers)
        for row in sheet.iter_rows(min_row=2):
            for cell in row:
                if cell.column - 1 in columns and tools.number_text(cell.value) in numbers:
                    cell.value = tools.styled(tools.number_text(cell.value))
    book.save(target)


def make(name, source, files, root, worker, numbers):
    folder = root / name
    if folder.exists():
        raise SystemExit("%s is there already, take it away by hand first" % folder)
    for relative in tools.files_of(name, files):
        target = folder / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        suffix = relative.suffix.lower()
        if name == "S3" and suffix in DRAWINGS:
            change_drawing(worker, source / relative, target, tools.changed_words, tools.changed_words)
        elif name == "S3" and suffix == ".xlsx":
            change_headers(source / relative, target)
        elif name == "S5" and suffix in DRAWINGS:
            change_drawing(worker, source / relative, target, tools.style_text(numbers), None)
        elif name == "S5" and suffix == ".xlsx":
            change_numbers(source / relative, target, numbers)
        else:
            shutil.copy2(source / relative, target)
    manifest = tools.manifest_of(folder)
    (root / (name + "_manifest.json")).write_text(json.dumps(manifest, indent=1), encoding="utf-8")
    print("%s: %d files" % (name, len(manifest)))


def main(argv=None):
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", required=True)
    parser.add_argument("--worker", required=True)
    parser.add_argument("--only", nargs="*", help="only these scenarios")
    parser.add_argument("--out", help="write the folders here and not under the work root, a folder a run is using is never replaced")
    args = parser.parse_args(argv)
    source = Path(args.source)
    root = tools.work_root() / "scenarios"
    answers = json.loads((root / "answers" / "S1.json").read_text(encoding="utf-8"))
    labels = json.loads((root / "answers" / "labels.json").read_text(encoding="utf-8"))
    numbers = set(answers["plots"]) | set(labels)
    before = tools.manifest_of(source)
    files = tools.raw_files(source)
    out = Path(args.out) if args.out else root
    for name in args.only or sorted(tools.SCENARIOS):
        make(name, source, files, out, Path(args.worker).resolve(), numbers)
    faults = tools.manifest_faults(source, before)
    if faults:
        raise SystemExit("the source changed: %d files" % len(faults))
    print("source unchanged, %d files" % len(before))
    return 0


if __name__ == "__main__":
    sys.exit(main())
