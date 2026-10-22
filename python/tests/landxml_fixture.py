import json

# Northing first, then easting, then elevation, like a P tag of Civil 3D. Ids do not start at 1 on purpose
ROAD = {11: (5000, 1000, 10), 12: (5000, 1010, 20), 13: (5010, 1010, 30), 14: (5010, 1000, 20), 15: (5000, 1020, 30), 16: (5010, 1020, 40)}
# Triangle 12 15 16 is invisible, so it is a hole. Point 99 does not exist
ROAD_FACES = [((11, 12, 13), False), ((11, 13, 14), False), ((12, 15, 16), True), ((12, 16, 13), False), ((11, 12, 99), False)]
GROUND = {20: (4000, 0, 5), 21: (4000, 2000, 5), 22: (6000, 1000, 5)}
GROUND_FACES = [((20, 21, 22), False)]
SPARE = {1: (5000, 1000, 1), 2: (5000, 1001, 1), 3: (5001, 1000, 1)}
SPARE_FACES = [((1, 2, 3), False)]


def road_z(easting, northing):
    return 10 + (easting - 1000) + (northing - 5000)


def landxml_text(surfaces):
    parts = ['<?xml version="1.0"?><LandXML xmlns="http://www.landxml.org/schema/LandXML-1.2" version="1.2"><Units><Metric linearUnit="meter"></Metric></Units><CoordinateSystem epsgCode="32637"></CoordinateSystem><Surfaces>']
    for name, points, faces in surfaces:
        parts.append('<Surface name="' + name + '"><Definition surfType="TIN" elevMin="1" elevMax="40"><Pnts>')
        for point_id, (northing, easting, z) in points.items():
            parts.append('<P id="' + str(point_id) + '">' + str(northing) + " " + str(easting) + " " + str(z) + "</P>")
        parts.append("</Pnts><Faces>")
        for corners, invisible in faces:
            attr = 'i="1"' if invisible else 'n="2 0 0"'
            parts.append("<F " + attr + ">" + " ".join(str(corner) for corner in corners) + "</F>")
        parts.append("</Faces></Definition></Surface>")
    parts.append("</Surfaces></LandXML>")
    return "".join(parts)


def write_landxml(path, with_spare=True):
    surfaces = [("Road", ROAD, ROAD_FACES), ("Ground", GROUND, GROUND_FACES)]
    if with_spare:
        surfaces.append(("Spare", SPARE, SPARE_FACES))
    path.write_text(landxml_text(surfaces), encoding="utf-8")
    return path


def config_dict():
    return {"note": "test", "folder": ".", "landxml": "x.xml", "prototypes": "p.json", "rampTypes": ["Ramps 2%"], "bands": [1, 5, 20], "surfaces": {"Road": {"role": "road"}, "Ground": {"role": "ground"}}}


def write_config(path):
    path.write_text(json.dumps(config_dict()), encoding="utf-8")
    return path


def floor(plot, type_name, top, boundary, levels=None):
    """A floor of the new shape. corners hold the real level of each corner and sketchBoundary holds the sketch plane, 150, which is never a level."""
    levels = levels or [top] * len(boundary)
    return {"typeName": type_name, "elementId": 1, "topElevation": top, "corners": [[x, y, z] for (x, y), z in zip(boundary, levels)], "cornersSource": "shapeEditor",
            "sketchBoundary": [[x, y, 150] for x, y in boundary], "parameters": {"Plot": plot}}


# Survey point shared position is the internal origin shifted by 1000 east and 5000 north
# Corner levels are the road level of the corner plus a chosen gap, so the gap bands are known
def prototypes_dict():
    floors = [
        floor("DT1-77", "Ramps 2%", 101.0, [(5, 5), (8, 5), (8, -3), (5, -3)], [20.0, 23.02, 101.0, 101.0]),
        floor("DT1-78", "Ramps 2%", 102.0, [(5, 6), (30, 5), (7, 8), (30, 30)], [21.5, 102.0, 25.0, 102.0]),
        floor("DT1-79", "Ramps 2%", 103.0, [(2, 2), (8, 2), (8, 7), (2, 8)], [14.005, 20.02, 25.2, 20.0]),
        floor("DT1-80", "Ramps 2%", 104.0, [(100, 100), (101, 100), (101, 101), (100, 101)]),
        floor("DT1-81", "Ramps 2%", 105.0, [(-0.5, 5), (-0.5, 6), (-0.5, 7), (-0.5, 8)]),
        floor("DT1-77", "Generic 150mm", 106.0, [(0, 0), (1, 0), (1, 1), (0, 1)]),
    ]
    spots = [
        {"elementId": 1, "origin": [5.0, 5.04, 20.01], "view": "Plan"},
        {"elementId": 2, "origin": [8.0, 5.0, 23.02], "view": "Plan"},
        {"elementId": 3, "origin": [50.0, 50.0, 1.0], "view": "Plan"},
        {"elementId": 4, "origin": [2.0, 2.0, 14.006], "view": "Plan"},
        {"elementId": 5, "origin": [5.0, 5.06, 20.0], "view": "Plan"},
    ]
    return {"documents": [{"kind": "project", "surveyPoint": {"position": [0, 0, 100], "sharedPosition": [1000, 5000, 100]}, "floors": floors, "spotElevations": spots,
                           "links": [{"plotNumber": "77", "ffl": "105.500"}]}]}


def write_prototypes(path, data=None):
    path.write_text(json.dumps(data or prototypes_dict()), encoding="utf-8")
    return path
