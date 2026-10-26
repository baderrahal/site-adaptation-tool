"""R351, R352. The one rule of a site point on a baseplan face in Python and C#, python/face_index.py, and the grid by bounding box that
finds the faces of a point, on made up rings built in code (R10). dotnet/SiteAdaptation.Tests/LocationPlanTests.cs holds the C# side on
the same rings, on net48 and net8.0."""
from shapely.geometry import Point

import face_index
import location_plan
import yard_rule


def square(x0, y0, x1, y1):
    return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]


# Face 5 is drawn as two rings that share the edge x = 10, face 6 is one ring that shares the edge x = 20 with the second ring of 5
RINGS = {"5": [square(0, 0, 10, 10), square(10, 0, 20, 10)], "6": [square(20, 0, 30, 10)]}


def plots_of(rings):
    return [{"kind": "plot_shape", "plot": label, "vertices": [list(point) for point in ring]} for label, found in rings.items() for ring in found]


def test_r351_a_label_on_the_edge_two_rings_of_one_label_share_is_on_that_face_as_the_union_of_its_rings_holds_it():
    index = face_index.FaceIndex(RINGS)
    assert index.face_of(10.0, 5.0) == "5"
    assert index.face_of(10.0, 0.0) == "5"
    assert index.face_of(5.0, 5.0) == "5" and index.face_of(15.0, 5.0) == "5" and index.face_of(25.0, 5.0) == "6"
    # On the edge of two faces, or on the outline of one ring alone, no face holds the point
    assert index.face_of(20.0, 5.0) is None and index.holding(20.0, 5.0) == []
    assert index.face_of(0.0, 5.0) is None and index.face_of(35.0, 5.0) is None
    # shapely holds the edge of the two rings of 5 within their union, so the rule gives the face the union gives
    union = yard_rule.face_shapes(plots_of(RINGS), 0)[0]
    assert Point(10.0, 5.0).within(union["5"]) and not Point(20.0, 5.0).within(union["5"])
    faces = face_index.plan_faces(plots_of(RINGS))
    assert location_plan.face_of_points([(10.0, 5.0), (20.0, 5.0), (25.0, 5.0)], faces) == ["5", None, "6"]
    assert sorted(faces) == ["5", "6"] and faces["5"].area == 200.0


# The faces, points and points one face holds on the rings and points below, the same counts as LocationPlanTests.FaceIndexCounts
FACES, POINTS, ONE = 78, 5130, 2715


def made_rings():
    """A grid of 12 by 9 squares of 10 m at site coordinates, a face of two squares that share an edge where (7 row + 3 column) mod 10 is
    under 3, labels by row and column, the same rings as LocationPlanTests.MadeRings."""
    rings, x0, y0 = {}, 571000.0, 2364000.0
    for row in range(9):
        column = 0
        while column < 12:
            label = str(100 + row * 12 + column)
            width = 2 if column < 11 and (7 * row + 3 * column) % 10 < 3 else 1
            rings[label] = [square(x0 + 10 * (column + k), y0 + 10 * row, x0 + 10 * (column + k + 1), y0 + 10 * (row + 1)) for k in range(width)]
            column += width
    return rings


def made_points():
    """Every point of a lattice of 2.5 m over the grid and around it, on its edges and vertices too, and a point at 3 decimals near each,
    the same points as LocationPlanTests.MadePoints."""
    points = []
    for i in range(-4, 53):
        for k in range(-4, 41):
            points.append((571000.0 + 2.5 * i, 2364000.0 + 2.5 * k))
            points.append((571000.0 + 2.5 * i + 0.001 * ((i * 7 + k * 3) % 5 - 2), 2364000.0 + 2.5 * k + 0.001 * ((i * 3 + k * 5) % 5 - 2)))
    return points


def test_r351_the_grid_gives_every_point_the_faces_a_scan_of_every_face_gives():
    rings = made_rings()
    index = face_index.FaceIndex(rings)
    one = 0
    for x, y in made_points():
        scan = sorted((label for label, found in rings.items() if face_index.holds(found, x, y)), key=int)
        assert index.holding(x, y) == scan
        assert index.face_of(x, y) == (scan[0] if len(scan) == 1 else None)
        one += len(scan) == 1
    # The C# test counts the same on the same rings and points
    assert (len(rings), len(made_points()), one) == (FACES, POINTS, ONE)
    assert face_index.FaceIndex({}).holding(0.0, 0.0) == []
