"""R111. A yard floor belongs to the plot whose baseplan plot face holds the largest share of its area.

The faces are the plot shapes of plots.json from the CAD reader run on the baseplan. A face label is the official plot
number minus the shift of the neighbourhood (R28), so label plus shift is the official number. The touch rule of R58
cannot decide on the past job, every plot touches 2 to 6 yards. Shares within the tie limit of each other are a tie,
listed with the candidates and never guessed.
"""
from shapely.geometry import Polygon
from shapely.strtree import STRtree

EPS = 1e-9
SHARE_DECIMALS = 4


def whole_label(value):
    text = str(value).strip() if value is not None else ""
    return int(text) if text.isascii() and text.isdigit() else None


def face_shapes(plots, shift):
    """The plot faces by official plot number, and the labels that are not whole numbers.

    plots is the plots list of plots.json. Two faces with one label are joined, so their shares add up.
    """
    if not isinstance(shift, int) or isinstance(shift, bool):
        raise ValueError("the plot number shift must be a whole number")
    faces, bad = {}, []
    for entry in plots or []:
        if entry.get("kind") != "plot_shape":
            continue
        label = whole_label(entry.get("plot"))
        vertices = entry.get("vertices") or []
        if label is None:
            bad.append(str(entry.get("plot")))
            continue
        if len(vertices) < 3:
            continue
        shape = Polygon([(vertex[0], vertex[1]) for vertex in vertices]).buffer(0)
        if shape.is_empty:
            continue
        number = str(label + shift)
        faces[number] = faces[number].union(shape) if number in faces else shape
    return faces, sorted(bad)


def assign(yards, faces, tie, least=0.0):
    """One result per yard: status assigned, tie, lowShare or none, the plot it goes to and the candidates with their shares.

    yards is a list of (element id, plan shape). faces is the result of face_shapes. A candidate is a plot whose face
    holds part of the yard, in order of share, largest first, then plot number. A best share under least is lowShare and
    goes to no plot, so a yard that lies mostly on a face with no usable label never lands on the neighbour that holds a sliver.
    """
    if not (isinstance(tie, (int, float)) and 0 <= tie < 1):
        raise ValueError("the tie limit must be a share from 0 to below 1")
    if not (isinstance(least, (int, float)) and 0 <= least < 1):
        raise ValueError("the least share must be a share from 0 to below 1")
    numbers = sorted(faces, key=int)
    tree = STRtree([faces[number] for number in numbers]) if numbers else None
    results = {}
    for element_id, shape in yards:
        candidates = []
        if tree is not None and shape is not None and shape.area > 0:
            for index in tree.query(shape):
                share = faces[numbers[index]].intersection(shape).area / shape.area
                if share > EPS:
                    candidates.append((share, numbers[index]))
        candidates.sort(key=lambda item: (-item[0], int(item[1])))
        listed = [[number, round(share, SHARE_DECIMALS)] for share, number in candidates]
        if not candidates:
            results[element_id] = {"status": "none", "plot": None, "share": None, "candidates": []}
        elif len(candidates) > 1 and candidates[0][0] - candidates[1][0] <= tie + EPS:
            results[element_id] = {"status": "tie", "plot": None, "share": round(candidates[0][0], SHARE_DECIMALS), "candidates": listed}
        elif candidates[0][0] < least - EPS:
            results[element_id] = {"status": "lowShare", "plot": None, "share": round(candidates[0][0], SHARE_DECIMALS), "candidates": listed}
        else:
            results[element_id] = {"status": "assigned", "plot": candidates[0][1], "share": round(candidates[0][0], SHARE_DECIMALS), "candidates": listed}
    return results


def by_plot(results):
    """The yards that went to each plot, in element id order."""
    found = {}
    for element_id in sorted(results):
        result = results[element_id]
        if result["status"] == "assigned":
            found.setdefault(result["plot"], []).append(element_id)
    return found


def low_with(results, plot):
    """The yards whose best share is too low that would have gone to the plot."""
    return [element_id for element_id in sorted(results) if results[element_id]["status"] == "lowShare" and results[element_id]["candidates"][0][0] == plot]


def tied_with(results, plot, tie):
    """The tied yards that name the plot among the candidates within the tie limit of the best, so a plot with no yard can say why."""
    found = []
    for element_id in sorted(results):
        result = results[element_id]
        if result["status"] != "tie":
            continue
        top = result["candidates"][0][1]
        if any(number == plot and share >= top - tie - EPS for number, share in result["candidates"]):
            found.append(element_id)
    return found
