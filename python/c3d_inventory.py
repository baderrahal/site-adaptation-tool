"""Counts what each working copy holds, Civil 3D objects included, with plain AutoLISP in the core console (R153).

ACadSharp skips the Civil 3D objects, and the probe plug-in needs a trusted folder, so this inventory types its LISP at the
prompt: every entity of the drawing with its class, layer and space, the Z of lines and polylines and the block of an insert.
It reads entity data only and writes one text file per drawing into out/inventory.
"""
import argparse
import json
import sys
from collections import Counter, defaultdict
from pathlib import Path

import c3d_run
import common

CIVIL_PREFIX = "AECC_"

LISP = r'''(defun c3dpz (e flags / lo hi v d z)
 (setq v (entnext e) lo nil hi nil)
 (while (and v (= (cdr (assoc 0 (setq d (entget v)))) "VERTEX"))
  (setq z (caddr (cdr (assoc 10 d))) lo (if lo (min lo z) z) hi (if hi (max hi z) z) v (entnext v)))
 (strcat (itoa flags) " " (if lo (rtos lo 2 4) "") " " (if hi (rtos hi 2 4) "")))
(defun c3dinv (out / ss i e d f cls x)
 (setq ss (ssget "X") i 0 f (open out "w"))
 (if ss (repeat (sslength ss)
  (setq e (ssname ss i) d (entget e) i (1+ i) cls (cdr (assoc 0 d)) x "")
  (cond
   ((= cls "LINE") (setq x (strcat (rtos (caddr (cdr (assoc 10 d))) 2 4) " " (rtos (caddr (cdr (assoc 11 d))) 2 4))))
   ((= cls "LWPOLYLINE") (setq x (rtos (if (assoc 38 d) (cdr (assoc 38 d)) 0.0) 2 4)))
   ((= cls "POLYLINE") (setq x (c3dpz e (cdr (assoc 70 d)))))
   ((= cls "INSERT") (setq x (cdr (assoc 2 d)))))
  (write-line (strcat cls "\t" (cdr (assoc 8 d)) "\t" (if (assoc 410 d) (cdr (assoc 410 d)) "") "\t" x) f)))
 (close f) (princ (strcat "\nC3DPROBE inventory " (itoa i))) (princ))
(c3dinv "OUT")
'''


def lisp_for(out_file):
    return LISP.replace('"OUT"', '"' + str(out_file).replace("\\", "/") + '"')


def numbers(text):
    values = []
    for word in text.split():
        try:
            values.append(float(word))
        except ValueError:
            pass
    return values


def summarize(lines):
    """Counts by class, by layer and class in model space, the Z range of lines and polylines and the blocks inserted."""
    classes, spaces, blocks = Counter(), Counter(), Counter()
    layers = defaultdict(Counter)
    z_range = defaultdict(dict)
    three_d = Counter()
    for line in lines:
        cells = line.rstrip("\n").split("\t")
        if len(cells) < 4:
            continue
        cls, layer, space, extra = cells[:4]
        classes[cls] += 1
        spaces[space or "?"] += 1
        if space != "Model":
            continue
        layers[layer][cls] += 1
        if cls == "INSERT":
            blocks[extra] += 1
            continue
        values = numbers(extra)
        if cls == "POLYLINE":
            # 70 bit 8 is a 3D polyline, the two numbers after the flags are its lowest and highest vertex
            flags = int(values[0]) if values else 0
            values = values[1:]
            if flags & 8:
                cls = "POLYLINE3D"
                if len(values) == 2 and (values[0] != 0 or values[1] != 0):
                    three_d[layer] += 1
        if values:
            low, high = min(values), max(values)
            known = z_range[layer].get(cls)
            z_range[layer][cls] = [min(low, known[0]), max(high, known[1])] if known else [low, high]
    civil = {name: count for name, count in classes.items() if name.startswith(CIVIL_PREFIX)}
    return {
        "entities": sum(classes.values()),
        "spaces": dict(spaces),
        "classes": dict(classes.most_common()),
        "civil": civil,
        "isCivil": bool(civil),
        "layers": {name: dict(counts.most_common()) for name, counts in sorted(layers.items())},
        "zRange": {name: value for name, value in sorted(z_range.items()) if any(v != [0, 0] for v in value.values())},
        "polylines3dWithZ": dict(three_d),
        "blocks": dict(blocks.most_common()),
    }


def main(argv=None):
    parser = argparse.ArgumentParser(description="Inventory the working copies with plain LISP in the core console (R153)")
    parser.add_argument("roles", nargs="*", help="drawing roles of c3d_probe.json, all when left out")
    parser.add_argument("--config", default=str(c3d_run.CONFIG))
    args = parser.parse_args(argv)
    config = c3d_run.load_config(args.config)
    out = Path(config["probeRoot"]) / "out" / "inventory"
    out.mkdir(parents=True, exist_ok=True)
    report = {}
    for role in args.roles or list(config["drawings"]):
        drawing = c3d_run.work_root(config) / config["drawings"][role]
        target = out / (role + ".tsv")
        if target.exists():
            target.unlink()
        record = c3d_run.run(config, drawing, [], label=role + "_inventory", lisp=lisp_for(target))
        entry = {"drawing": drawing.name, "run": record}
        if target.exists():
            entry.update(summarize(target.read_text(encoding="utf-8", errors="replace").splitlines()))
        else:
            entry["error"] = "no inventory file, see the console log"
        report[role] = entry
        print("%s: entities %s, civil %s, pid %s, unchanged %s, %s s" % (role, entry.get("entities"), entry.get("civil"), record["pid"], record["unchanged"], record["seconds"]))
    common.write_json(out / "inventory.json", report, wide_keys=frozenset({"zRange"}))
    return 0


if __name__ == "__main__":
    sys.exit(main())
