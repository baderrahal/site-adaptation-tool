"""The Typical Cross Sections drawing read in full, Part E of round DATA-3 (R192).

The drawing holds the road sections side by side in its own frame, metres at 1 to 1, none of it inside the Phase 1 border, so it
is read whole by CadReader into dataBook.crossSections.cadOut and never cut. A curb is a closed pavement polyline of the curb
size band. Its road face is the side a carriageway line ends on, the up-stand is the curb top less the level where that line
meets the face, and the sidewalk is the near flat pavement line that starts behind the curb and runs away from the road. Its
width is taken from the road face of the curb to the far end of that line, its cross fall from a percent text over it, else from
the slope of the line. Each curb takes the section title nearest to it and the road type written under the title.
"""
import argparse
import json
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path

import c3d_run


def clean(text):
    return re.sub(r"%%[UuOo]", "", text or "").strip()


def titles(texts, settings):
    """The section titles with the road type and ROW lines written just under each."""
    found = []
    heads = [t for t in texts if re.search(settings["titlePattern"], clean(t["text"]))]
    for head in heads:
        under = [t for t in texts if t is not head and abs(t["x"] - head["x"]) <= settings["titleColumnM"] and 0 < head["y"] - t["y"] <= settings["titleBelowM"]]
        kinds = [clean(t["text"]) for t in sorted(under, key=lambda t: -t["y"])]
        found.append({"title": clean(head["text"]), "x": head["x"], "y": head["y"],
                      "roadType": next((k for k in kinds if re.search(settings["roadTypePattern"], k)), None),
                      "row": next((k for k in kinds if re.search(settings["rowPattern"], k)), None)})
    return found


def boxes(pieces, layers):
    """The closed four point polylines of the layers as boxes x0, x1, y0, y1."""
    found = []
    for piece in pieces:
        points = piece.get("pts") or []
        if piece.get("layer") not in layers or piece.get("kind") != "polyline" or len(points) != 4:
            continue
        xs, ys = [p[0] for p in points], [p[1] for p in points]
        found.append((min(xs), max(xs), min(ys), max(ys)))
    return found


def segments(pieces, layers):
    found = []
    for piece in pieces:
        points = piece.get("pts") or []
        if piece.get("layer") not in layers or piece.get("kind") not in ("line", "polyline") or len(points) < 2:
            continue
        for a, b in zip(points, points[1:]):
            if abs(a[0] - b[0]) > 1e-6:
                found.append(((a[0], a[1]), (b[0], b[1])))
    return found


def curbs(pieces, settings):
    """Each curb with its road face, up-stand and the sidewalk behind it."""
    layers = set(settings["pavementLayers"])
    lines = segments(pieces, layers)
    found = []
    for x0, x1, y0, y1 in boxes(pieces, layers):
        width, height = x1 - x0, y1 - y0
        if not (settings["curbWidthM"][0] <= width <= settings["curbWidthM"][1] and settings["curbHeightM"][0] <= height <= settings["curbHeightM"][1]):
            continue
        tol = settings["touchM"]
        for face, outward in ((x0, -1), (x1, 1)):
            # a carriageway line ends on this face between the bottom and the top of the curb and runs away from the curb body
            road = []
            for a, b in lines:
                for end, other in ((a, b), (b, a)):
                    if abs(end[0] - face) <= tol and y0 - tol <= end[1] <= y1 + tol and (other[0] - face) * outward > settings["minRoadLineM"]:
                        road.append((end, other))
            if not road:
                continue
            end, other = max(road, key=lambda item: item[0][1])
            upstand = y1 - end[1]
            back = x0 if outward == 1 else x1
            walks = []
            for a, b in lines:
                for start, far in ((a, b), (b, a)):
                    run = (far[0] - start[0]) * -outward
                    if run <= 0 or abs(start[0] - back) > settings["sidewalkStartM"] or abs(far[1] - start[1]) / run > settings["flatSlope"]:
                        continue
                    if not (y1 - settings["sidewalkBelowTopM"] <= start[1] <= y1 + tol):
                        continue
                    walks.append((run, start, far))
            walk = max(walks) if walks else None
            found.append({"face": face, "top": y1, "bottom": y0, "width": width, "height": height, "roadEdge": end[1], "upstandM": round(upstand, 3),
                          "roadSlopePercent": round(100 * (other[1] - end[1]) / abs(other[0] - end[0]), 2),
                          "sidewalkFrom": walk[1] if walk else None, "sidewalkTo": walk[2] if walk else None,
                          "sidewalkLineM": round(walk[0], 3) if walk else None,
                          "sidewalkWidthM": round(abs(walk[2][0] - face), 3) if walk else None,
                          "sidewalkLineSlopePercent": round(100 * (walk[2][1] - walk[1][1]) / walk[0], 2) if walk else None})
    return found


def cross_fall(curb, texts, settings):
    """The percent written over the sidewalk, else the slope of the sidewalk line."""
    if curb["sidewalkFrom"] is None:
        return None, None
    lo, hi = sorted((curb["sidewalkFrom"][0], curb["sidewalkTo"][0]))
    over = [t for t in texts if lo <= t["x"] <= hi and 0 <= t["y"] - curb["top"] <= settings["percentAboveM"]]
    values = [float(m.group(1)) for t in over for m in [re.search(settings["percentPattern"], clean(t["text"]))] if m]
    if values:
        return Counter(values).most_common(1)[0][0], "text"
    return curb["sidewalkLineSlopePercent"], "line"


def nearest_title(curb, heads):
    return min(heads, key=lambda h: abs(h["x"] - curb["face"]) * 0.3 + abs(h["y"] - curb["top"])) if heads else None


def read(folder, settings):
    pieces = json.loads((folder / "pieces.json").read_text(encoding="utf-8"))["pieces"]
    texts = json.loads((folder / "texts.json").read_text(encoding="utf-8"))["texts"]
    heads = titles(texts, settings)
    rows = []
    for curb in curbs(pieces, settings):
        fall, source = cross_fall(curb, texts, settings)
        head = nearest_title(curb, heads)
        rows.append({"section": head["title"] if head else None, "roadType": head["roadType"] if head else None, "row": head["row"] if head else None,
                     "upstandM": curb["upstandM"], "curbWidthM": round(curb["width"], 3), "curbHeightM": round(curb["height"], 3),
                     "roadSlopePercent": curb["roadSlopePercent"], "sidewalkWidthM": curb["sidewalkWidthM"], "sidewalkLineM": curb["sidewalkLineM"],
                     "sidewalkCrossFallPercent": fall, "crossFallSource": source})
    by_type = defaultdict(list)
    for row in rows:
        by_type[row["roadType"] or "none"].append(row)
    summary = {"titles": len(heads), "sections": sorted({h["title"] for h in heads}), "curbs": len(rows),
               "byRoadType": {kind: {"curbs": len(items), "sections": sorted({r["section"] for r in items if r["section"]}),
                                     "upstandM": dict(Counter("%.3f" % r["upstandM"] for r in items).most_common()),
                                     "sidewalkWidthM": dict(Counter("%.2f" % r["sidewalkWidthM"] if r["sidewalkWidthM"] is not None else "none" for r in items).most_common()),
                                     "crossFallPercent": dict(Counter("%s %s" % (r["sidewalkCrossFallPercent"], r["crossFallSource"]) for r in items).most_common())}
                              for kind, items in sorted(by_type.items())}}
    return summary, rows


def profile_values(profile, words):
    """Every key path of the rules profile whose name holds one of the words, with its value (Part E 4)."""
    found = {}

    def walk(value, path):
        if isinstance(value, dict):
            for key, item in value.items():
                walk(item, path + [key])
        elif any(w in "_".join(path).lower() for w in words):
            found[".".join(path)] = value
    walk(profile, [])
    return found


def main(argv=None):
    parser = argparse.ArgumentParser(description="The Typical Cross Sections drawing in full: curb up-stand, sidewalk width and cross fall per road type (R192)")
    parser.add_argument("--config", default=str(c3d_run.CONFIG))
    args = parser.parse_args(argv)
    book = c3d_run.load_config(args.config)["dataBook"]
    settings = book["crossSections"]
    root = Path(book["root"])
    summary, rows = read(root / settings["cadOut"], settings)
    out = root / settings["outFolder"]
    out.mkdir(parents=True, exist_ok=True)
    (out / "cross_sections.json").write_text(json.dumps({"summary": summary, "rows": rows}, indent=1), encoding="utf-8")
    print(json.dumps(summary))
    return 0


if __name__ == "__main__":
    sys.exit(main())
