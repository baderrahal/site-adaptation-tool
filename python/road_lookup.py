"""Road level at a point from the LandXML surfaces in a fixed order (R91).

The surfaces are asked one after the other and the first that covers the point answers. The ground surface is never a
road level. The order lives in config/road_surfaces.json and no code writes it.
"""
from pathlib import Path

import surface_lookup

# The two ways the order is known, said in the reports so a reader sees which one ran
FILE_ORDER = "file order, road first"
CONFIG_ORDER = "order in config"
ROLES = ("ground", "design", "road")


def order_of(config):
    """The names of the surfaces the lookup asks, in order, and where that order came from.

    config is the raw road_surfaces.json. A surface with the role ground is left out. When every other surface has an
    order key the keys decide, they must be different numbers. When none has one, the road surface is first and the
    design surfaces follow in the order of the file. A mix of some with and some without is refused, and so is an
    order on a ground surface, since the ground must never answer.
    """
    surfaces = config["surfaces"]
    for name, entry in surfaces.items():
        if entry.get("role") not in ROLES:
            raise ValueError("the surface " + name + " has the role " + repr(entry.get("role")) + ", which is not one of " + ", ".join(ROLES))
    roads = [name for name, entry in surfaces.items() if entry["role"] == "road"]
    if len(roads) > 1:
        raise ValueError("more than one surface has the role road: " + ", ".join(roads))
    asked = [name for name, entry in surfaces.items() if entry["role"] != "ground"]
    for name, entry in surfaces.items():
        if entry["role"] == "ground" and "order" in entry:
            raise ValueError("the ground surface " + name + " has an order, the ground is never a road level")
    keyed = [name for name in asked if "order" in surfaces[name]]
    if keyed and len(keyed) != len(asked):
        raise ValueError("these surfaces have no order: " + ", ".join(name for name in asked if name not in keyed))
    if keyed:
        numbers = [surfaces[name]["order"] for name in asked]
        if any(isinstance(number, bool) or not isinstance(number, (int, float)) for number in numbers):
            raise ValueError("an order must be a number")
        if len(set(numbers)) != len(numbers):
            raise ValueError("two surfaces have the same order")
        return sorted(asked, key=lambda name: surfaces[name]["order"]), CONFIG_ORDER
    return roads + [name for name in asked if name not in roads], FILE_ORDER


class RoadLevels:
    """Ordered surfaces. level_at gives the level and the name of the first surface that covers the point."""

    def __init__(self, ordered):
        self.ordered = list(ordered)
        self.names = [name for name, _ in self.ordered]

    def level_at(self, easting, northing):
        """(level, surface name) from the first surface that covers the point, or (None, None)."""
        for name, surface in self.ordered:
            z = surface.z_at(easting, northing)
            if z is not None:
                return z, name
        return None, None

    def levels(self, points):
        return [self.level_at(easting, northing) for easting, northing in points]


def load(config, landxml_dir):
    """The lookup of a road_surfaces.json config, with the surfaces loaded from the LandXML reader output."""
    names, source = order_of(config)
    lookup = RoadLevels([(name, surface_lookup.load_surface(landxml_dir, name)) for name in names])
    lookup.source = source
    return lookup


def default_config_path():
    return Path(__file__).resolve().parent.parent / "config" / "road_surfaces.json"
