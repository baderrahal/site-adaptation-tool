import json
from pathlib import Path, PureWindowsPath

ROOT = Path(__file__).resolve().parent.parent
SETTINGS_PATH = ROOT / "config" / "project.json"
MM_PER_M = 1000.0


class ProfileError(Exception):
    pass


class ProjectProfile:
    def __init__(self, path, values):
        self.path = Path(path)
        self.values = values

    # A missing key raises. A key that holds null returns None, so a value that is
    # not supplied yet is told apart from a key the file lacks
    def get(self, dotted_key):
        node = self.values
        for part in dotted_key.split("."):
            if not isinstance(node, dict) or part not in node:
                raise ProfileError(self.path.name + " has no value " + dotted_key)
            node = node[part]
        return node

    # The one place a profile length in mm becomes metres. Everything measured from
    # prototypes.json or the CAD output is in metres (R4)
    def metres(self, dotted_key):
        if self.get("units_mm") is not True:
            raise ProfileError(self.path.name + " does not say its lengths are in mm")
        value = self.get(dotted_key)
        if value is None:
            return None
        if isinstance(value, bool) or not isinstance(value, (int, float)):
            raise ProfileError(self.path.name + " value " + dotted_key + " is not a number")
        return value / MM_PER_M


def no_duplicate_keys(pairs):
    values = {}
    for key, value in pairs:
        if key in values:
            raise ProfileError("duplicate key " + key)
        values[key] = value
    return values


def read_object(path):
    path = Path(path)
    if not path.is_file():
        raise ProfileError("profile file not found: " + str(path))
    try:
        # utf-8-sig so a file saved with a byte order mark by a Windows editor still reads
        values = json.loads(path.read_text(encoding="utf-8-sig"), object_pairs_hook=no_duplicate_keys)
    except (OSError, UnicodeDecodeError) as error:
        raise ProfileError(path.name + " cannot be read: " + str(error)) from error
    except json.JSONDecodeError as error:
        raise ProfileError(path.name + " is not valid JSON: " + str(error)) from error
    except ProfileError as error:
        raise ProfileError(path.name + " has a " + str(error)) from error
    if not isinstance(values, dict):
        raise ProfileError(path.name + " must hold one JSON object")
    return values


def load_profile(path):
    return ProjectProfile(path, read_object(path))


# The project settings file reads with the same loader, so it gets the same errors
def load_settings(path=SETTINGS_PATH):
    return load_profile(path)


# The extensions a drawing, its backup or its lock file can carry, taken off in any order so R06_0000.dwg.bak has the stem of R06_0000
CAD_EXTENSIONS = (".dwg", ".dxf", ".dwl", ".dwl2", ".bak", ".dwt", ".dws", ".sv$")


def file_name(name):
    """The file name of a path, lower case, with no folder."""
    return PureWindowsPath(str(name or "").replace("/", "\\")).name.strip().lower()


def drawing_stem(name):
    """R300. The stem of a drawing name, lower case, with no folder and no CAD extension, so R06_0000.dwg and r06_0000.DXF
    have one stem. A dot that starts no CAD extension stays, as in 25.06.2025 Phase 1."""
    stem = file_name(name)
    while stem.endswith(CAD_EXTENSIONS):
        stem = stem[:stem.rindex(".")].strip()
    return stem


def reference_only(settings):
    """R300. The stems of the drawings drawings.referenceOnly keeps for reference only. A settings file with no such list
    keeps none."""
    drawings = settings.values.get("drawings")
    names = drawings.get("referenceOnly") if isinstance(drawings, dict) else None
    return {drawing_stem(name) for name in names or [] if drawing_stem(name)}


def holds_token(text, token):
    """True when token is in text with no letter or digit right before or after it."""
    start = text.find(token)
    while start >= 0:
        end = start + len(token)
        if (start == 0 or not text[start - 1].isalnum()) and (end == len(text) or not text[end].isalnum()):
            return True
        start = text.find(token, start + 1)
    return False


def is_reference_only(source, settings):
    """R300. True when the file name of source holds the full stem of a reference only drawing as a whole token, whatever comes
    before or after it, so Copy (2) of R06_0000.dwg, R06_0000 - Copy.dxf and Copy of Copy of R06_0001.dwg match and R06_00001
    does not."""
    name = file_name(source)
    return bool(name) and any(holds_token(name, ref) for ref in reference_only(settings))


def refuse_reference_only(source, settings=None):
    """R300. Villa blocks come from drawings.villaBlocks, the Building Footprint (Bader, Q045). A drawing or a blocks.json
    whose source is a drawing of drawings.referenceOnly, such as R06_0000 and R06_0001, is refused as a villa block source."""
    settings = settings if settings is not None else load_settings()
    if is_reference_only(source, settings):
        raise ProfileError(PureWindowsPath(str(source)).name + " is kept for reference only (drawings.referenceOnly), villa blocks come from drawings.villaBlocks (R300)")
    return source


def read_villa_blocks(path, settings=None):
    """R300. A blocks.json of the CAD reader read for its unit blocks, refused when its source or the folder it sits in names a
    drawing kept for reference only. settings are those the caller loaded, the repo config when None."""
    settings = settings if settings is not None else load_settings()
    path = Path(path)
    refuse_reference_only(path.parent.name, settings)
    data = read_object(path)
    if not isinstance(data.get("blocks"), list):
        raise ProfileError(str(path) + " has no blocks list")
    refuse_reference_only(data.get("source"), settings)
    return data


def profile_path(settings):
    name = settings.get("profile")
    if not isinstance(name, str) or not name:
        raise ProfileError(settings.path.name + " has no profile path")
    path = Path(name)
    return path if path.is_absolute() else ROOT / path
