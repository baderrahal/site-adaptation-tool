import json
from pathlib import Path


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


def load_profile(path):
    path = Path(path)
    if not path.is_file():
        raise ProfileError("profile file not found: " + str(path))
    try:
        # utf-8-sig so a file saved with a byte order mark by a Windows editor still reads
        values = json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, UnicodeDecodeError) as error:
        raise ProfileError(path.name + " cannot be read: " + str(error)) from error
    except json.JSONDecodeError as error:
        raise ProfileError(path.name + " is not valid JSON: " + str(error)) from error
    if not isinstance(values, dict):
        raise ProfileError(path.name + " must hold one JSON object")
    return ProjectProfile(path, values)
