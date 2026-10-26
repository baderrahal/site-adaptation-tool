"""R388. The units of a CadReader output the Python reference reads.

A CadReader read writes layers.json beside its outputs with the InsUnits of the drawing header (R5). Its plot, block and piece outputs
carry none of their own, so their units are those of the layers.json of their folder. The reference reads a drawing only
in Meters and never converts one: an output of another unit, or one with no layers.json beside it, such as a copy of plots.json alone,
is refused with the file and the unit named.
"""
import json
from pathlib import Path

METRES = "Meters"
LAYERS = "layers.json"


class UnitError(ValueError):
    """R388. A CadReader output whose drawing is not in Meters, or whose units are not known."""


def check(path):
    """R388. The units of the CadReader output at path, a file or its folder, Meters, else UnitError with the file and the unit."""
    path = Path(path)
    layers = (path if path.is_dir() else path.parent) / LAYERS
    if not layers.is_file():
        raise UnitError("%s has no %s of its read beside it, so the units of its drawing are not known, a drawing is read only in %s (R388)" % (path, LAYERS, METRES))
    units = json.loads(layers.read_text(encoding="utf-8")).get("units")
    if units != METRES:
        raise UnitError("%s is of a drawing in %s, a drawing is read only in %s and never converted (R388)" % (path, units, METRES))
    return units
