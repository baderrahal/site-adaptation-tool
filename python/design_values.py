"""R405, Q103. The design values a build, the placer and button 1 take, from designValues of config/project.json, the same block
DesignValues.cs reads. A missing value is refused by its name, never given a default, since a default here would be a project value in code.
It reads the config with json alone, since common.py takes its decimals from here."""
import json
from pathlib import Path

SETTINGS = Path(__file__).resolve().parent.parent / "config" / "project.json"
BLOCK = "designValues"


class DesignValueError(ValueError):
    pass


def block(config):
    """The designValues block of a config, refused when the config holds none."""
    found = config.get(BLOCK) if isinstance(config, dict) else None
    if not isinstance(found, dict):
        raise DesignValueError("the config holds no %s block (R405)" % BLOCK)
    return found


def value_of(config, dotted):
    """The value under the dotted path of the designValues block of a config, refused by its name when missing."""
    node = block(config)
    for part in dotted.split("."):
        if not isinstance(node, dict) or part not in node:
            raise DesignValueError("the config holds no %s.%s (R405)" % (BLOCK, dotted))
        node = node[part]
    if node is None or isinstance(node, bool):
        raise DesignValueError("%s.%s of the config is no value (R405)" % (BLOCK, dotted))
    return node


_LOADED = {}


def value(dotted, path=SETTINGS):
    """The value of the dotted path in the config of the repo, read once."""
    key = str(path)
    if key not in _LOADED:
        _LOADED[key] = json.loads(Path(path).read_text(encoding="utf-8"))
    return value_of(_LOADED[key], dotted)
