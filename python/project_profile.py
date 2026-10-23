import json
from pathlib import Path

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


def profile_path(settings):
    name = settings.get("profile")
    if not isinstance(name, str) or not name:
        raise ProfileError(settings.path.name + " has no profile path")
    path = Path(name)
    return path if path.is_absolute() else ROOT / path
