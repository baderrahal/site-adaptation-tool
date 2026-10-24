import argparse
import json
import shutil
import subprocess
import sys
import time
from pathlib import Path, PureWindowsPath

import project_profile

HERE = Path(__file__).resolve().parent
DWG_FOLDERS = ("Base Plans", "Master Plan", "Roads")
PDF_FOLDER = Path("01_Output") / "1.5 Coordinates Schedules" / "PDF"
XLSX_FOLDER = Path("01_Output") / "1.11 References"
DEFAULT_SHEET = "MP_Phase 1_Zone21"
TIMEOUT = 600
LANDXML_TIMEOUT = 600
TEXT_LIMIT = 20000
MASTER_FOLDER = "Master Plan"
PREVIOUS_WHY = "Previous master plan, kept for reference and not read in the run"


def plan(root, kinds=("dwg", "pdf", "xlsx"), settings=None):
    """Files to read and files left alone, each with the reason."""
    root = Path(root)
    settings = settings or project_profile.load_settings()
    skip_lower = {entry["file"].lower(): entry["why"] for entry in settings.get("drawings.skip")}
    # The master plan it replaced is kept for reference, a reason in the skip list wins over this one
    skip_lower.setdefault(str(settings.get("drawings.previousMasterplan")).lower(), PREVIOUS_WHY)
    jobs, skipped = [], []
    if "dwg" in kinds:
        for folder in DWG_FOLDERS:
            for path in sorted((root / folder).glob("*")):
                if path.suffix.lower() == ".dwg":
                    if path.name.lower() in skip_lower:
                        skipped.append({"file": path.name, "why": skip_lower[path.name.lower()]})
                    else:
                        jobs.append(("dwg", path))
                elif path.suffix.lower() == ".xlsx" and folder == MASTER_FOLDER and "xlsx" in kinds:
                    continue
                elif path.is_file():
                    skipped.append({"file": path.name, "why": "not a DWG (" + path.suffix.lower() + ")"})
        output = root / "01_Output"
        left = len([path for path in output.rglob("*") if path.suffix.lower() == ".dwg"]) if output.is_dir() else 0
        skipped.append({"file": "DWG files under 01_Output", "why": "repeat the base plan and road drawings", "count": left})
    if "pdf" in kinds:
        jobs.extend(("pdf", path) for path in sorted((root / PDF_FOLDER).glob("*.pdf")))
    if "xlsx" in kinds:
        for path in sorted((root / XLSX_FOLDER).glob("*.xlsx")):
            if path.name.startswith("~$"):
                skipped.append({"file": path.name, "why": "Excel lock file"})
            else:
                jobs.append(("xlsx", path))
        for path in sorted((root / MASTER_FOLDER).glob("*.xlsx")):
            if path.name.startswith("~$"):
                skipped.append({"file": path.name, "why": "Excel lock file"})
            else:
                jobs.append(("survey", path))
    return jobs, skipped


def find_by_name(root, name):
    """The drawing with this file name in the input folders, matched without case, or None."""
    wanted = str(name).lower()
    for folder in DWG_FOLDERS:
        for path in sorted((Path(root) / folder).glob("*")):
            if path.is_file() and path.name.lower() == wanted:
                return path
    return None


def resolve_xrefs(root, out_root, drawings, settings):
    """Where each xref of each drawing that was read is (R72), and an issue for each one that is nowhere.

    An xref the CAD reader found next to its drawing is found. Any other is looked up by file name in the input
    folders. A name that is still not there is looked up in drawings.xrefRoles, which names the role of the
    drawing that stands in for it. Whatever is still missing is an issue and the run goes on.
    """
    roles = {name.lower(): role for name, role in settings.get("drawings.xrefRoles").items()}
    found, issues = [], []
    for drawing in drawings:
        layers = Path(out_root) / drawing / "layers.json"
        if not layers.is_file():
            continue
        for xref in json.loads(layers.read_text(encoding="utf-8")).get("xrefs", []):
            name = PureWindowsPath(xref["path"]).name if xref.get("path") else str(xref["name"]) + ".dwg"
            entry = {"drawing": drawing, "xref": name}
            if xref.get("found"):
                entry["status"] = "found next to the drawing"
                found.append(entry)
                continue
            path = find_by_name(root, name)
            entry["status"] = "found by name"
            role = roles.get(name.lower())
            if path is None and role is not None:
                path = find_by_name(root, settings.get("drawings." + role))
                entry["status"] = "found by role " + role
            if path is None:
                entry["status"] = "not found"
                issues.append({"drawing": drawing, "xref": name, "text": "The xref " + name + " of " + drawing + " is not in the input folders and no role stands in for it"})
            else:
                entry["file"] = path.name
            found.append(entry)
    return found, issues


def command(kind, path, out_dir, cad_exe, layers, sheet, cad_plots=None, shifts=None):
    if kind == "dwg":
        return [str(cad_exe), str(path), str(out_dir), "--layers", str(layers)]
    if kind == "pdf":
        return [sys.executable, str(HERE / "pdf_reader.py"), str(path), str(out_dir)]
    if kind == "survey":
        found = [sys.executable, str(HERE / "excel_reader.py"), str(path), str(out_dir), "--survey"]
        if cad_plots:
            found += ["--cad-plots", str(cad_plots)]
        if shifts:
            found += ["--shifts", str(shifts)]
        return found
    return [sys.executable, str(HERE / "excel_reader.py"), str(path), str(out_dir), "--sheet", sheet]


def run_one(args, out_dir, timeout=TIMEOUT):
    """Runs one reader with a time limit. A failure or a timeout is recorded with its exact text and never raised."""
    out_dir = Path(out_dir)
    if out_dir.exists():
        shutil.rmtree(out_dir)
    out_dir.mkdir(parents=True)
    started = time.monotonic()
    record = {"command": args, "timeoutSeconds": timeout}
    try:
        done = subprocess.run(args, capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=timeout)
        record.update({"status": "ok" if done.returncode == 0 else "failed", "exit": done.returncode, "stdout": done.stdout[-TEXT_LIMIT:], "stderr": done.stderr[-TEXT_LIMIT:]})
    except subprocess.TimeoutExpired as late:
        record.update({"status": "timeout", "exit": None, "stdout": text_of(late.stdout), "stderr": text_of(late.stderr)})
    except OSError as problem:
        record.update({"status": "failed", "exit": None, "stdout": "", "stderr": type(problem).__name__ + ": " + str(problem)})
    record["seconds"] = round(time.monotonic() - started, 1)
    (out_dir / "run.json").write_text(json.dumps(record, indent=1, ensure_ascii=False), encoding="utf-8")
    return record


def text_of(value):
    if value is None:
        return ""
    return (value.decode("utf-8", "replace") if isinstance(value, bytes) else value)[-TEXT_LIMIT:]


def cad_notes(out_dir):
    """Reader notes of a drawing, which name the objects the reader could not open."""
    layers = Path(out_dir) / "layers.json"
    if not layers.is_file():
        return None
    notes = json.loads(layers.read_text(encoding="utf-8")).get("notes", [])
    return {"total": sum(note["count"] for note in notes), "notImplemented": sum(note["count"] for note in notes if note["text"].startswith("NotImplemented")), "groups": len(notes)}


def stage_commands(out_root, ran, second, shifts, settings=None):
    """The join and check steps that follow the readers, each with its inputs, or the reason it cannot run yet."""
    out_root = Path(out_root)
    settings = settings or project_profile.load_settings()
    baseplan = settings.get("drawings.baseplan")
    base = out_root / baseplan
    rows = sorted(str(out_root / name / "pdf_rows.json") for name in ran if (out_root / name / "pdf_rows.json").is_file())
    coords = sorted(str(out_root / name / "coords.json") for name in ran if (out_root / name / "coords.json").is_file())
    layers = sorted(str(out_root / name / "layers.json") for name in ran if name != baseplan and (out_root / name / "layers.json").is_file())
    stages = []
    if not (base / "plots.json").is_file():
        return [{"name": name, "why": "no baseplan output"} for name in ("link", "second_source", "other_faces", "analysis")]
    if rows and coords:
        stages.append({"name": "link", "args": [sys.executable, str(HERE / "link_table.py"), str(out_root / "link"), "--cad", str(base), "--rows"] + rows + ["--coords"] + coords + ["--layers"] + layers + ["--shifts", str(shifts)]})
    else:
        stages.append({"name": "link", "why": "no PDF rows or no coordinates"})
    if (out_root / second / "pieces.json").is_file():
        stages.append({"name": "second_source", "args": [sys.executable, str(HERE / "second_source.py"), str(out_root / "second_source"), "--cad", str(base), "--second", str(out_root / second)]})
    else:
        stages.append({"name": "second_source", "why": "no output for " + second})
    stages.append({"name": "other_faces", "args": [sys.executable, str(HERE / "face_reasons.py"), str(out_root / "other_faces"), "--cad", str(base)]})
    analysis = [sys.executable, str(HERE / "cad_analysis.py"), str(out_root / "analysis"), "--cad", str(base), "--profile", str(project_profile.profile_path(settings)), "--project", str(settings.path)]
    villa, road = out_root / settings.get("drawings.villaBlocks"), out_root / settings.get("drawings.roadLayout")
    if (villa / "blocks.json").is_file():
        analysis += ["--blocks", str(villa)]
    if (road / "pieces.json").is_file():
        analysis += ["--roads", str(road)]
    stages.append({"name": "analysis", "args": analysis})
    return stages


def road_stages(out_root, config_path, landxml=None, prototypes=None):
    """The LandXML reader with its own 600 second limit, then the ramp road levels, which need the reader output."""
    config_path = Path(config_path)
    if not config_path.is_file():
        return [{"name": "landxml", "why": "no " + config_path.name}, {"name": "road_levels", "why": "no " + config_path.name}]
    config = json.loads(config_path.read_text(encoding="utf-8"))
    folder = Path(config.get("folder", "."))
    xml = Path(landxml) if landxml else folder / config["landxml"]
    levels = Path(prototypes) if prototypes else folder / config["prototypes"]
    out_root = Path(out_root)
    if not xml.is_file():
        return [{"name": "landxml", "why": "no file " + xml.name}, {"name": "road_levels", "why": "no LandXML output"}]
    stages = [{"name": "landxml", "timeout": LANDXML_TIMEOUT, "args": [sys.executable, str(HERE / "landxml_reader.py"), str(xml), str(out_root / "landxml"), "--config", str(config_path)]}]
    if levels.is_file():
        stages.append({"name": "road_levels", "needs": "landxml", "args": [sys.executable, str(HERE / "ramp_levels.py"), str(out_root / "road_levels"), "--config", str(config_path), "--prototypes", str(levels), "--landxml-dir", str(out_root / "landxml")]})
    else:
        stages.append({"name": "road_levels", "why": "no file " + levels.name})
    return stages


def main(argv=None):
    parser = argparse.ArgumentParser(description="Run the readers in place on the real files, one time limit per file.")
    parser.add_argument("root", help="folder that holds Base Plans, Master Plan, Roads and 01_Output")
    parser.add_argument("out_dir", help="where each file gets its own folder")
    parser.add_argument("--cad-exe", default=str(HERE.parent / "dotnet" / "CadReader" / "bin" / "Release" / "net48" / "CadReader.exe"))
    parser.add_argument("--project", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--layers", default=str(HERE.parent / "config" / "cad_layers.json"))
    parser.add_argument("--sheet", default=DEFAULT_SHEET)
    parser.add_argument("--timeout", type=int, default=TIMEOUT)
    parser.add_argument("--kinds", default="dwg,pdf,xlsx")
    parser.add_argument("--shifts", default=str(HERE.parent / "config" / "plot_numbers.json"))
    parser.add_argument("--roads", default=str(HERE.parent / "config" / "road_surfaces.json"))
    parser.add_argument("--landxml")
    parser.add_argument("--prototypes")
    args = parser.parse_args(argv)
    settings = project_profile.load_settings(args.project)
    baseplan = settings.get("drawings.baseplan")
    jobs, skipped = plan(args.root, tuple(args.kinds.split(",")), settings)
    out_root = Path(args.out_dir)
    results = []
    for kind, path in jobs:
        out_dir = out_root / path.name
        cad_plots = out_root / baseplan / "plots.json"
        record = run_one(command(kind, path, out_dir, args.cad_exe, args.layers, args.sheet, cad_plots, args.shifts), out_dir, args.timeout)
        entry = {"file": path.name, "kind": kind, "status": record["status"], "exit": record["exit"], "seconds": record["seconds"]}
        if kind == "dwg":
            entry["notes"] = cad_notes(out_dir)
        if record["status"] != "ok":
            entry["error"] = (record["stderr"].strip() or record["stdout"].strip())[-500:]
        results.append(entry)
        print(path.name + ": " + record["status"] + " in " + str(record["seconds"]) + " s", flush=True)
    out_root.mkdir(parents=True, exist_ok=True)
    second = settings.get("drawings.masterplan")
    ran = [entry["file"] for entry in results if entry["status"] == "ok"]
    xrefs, xref_issues = resolve_xrefs(args.root, out_root, [entry["file"] for entry in results if entry["kind"] == "dwg" and entry["status"] == "ok"], settings)
    for issue in xref_issues:
        print("issue: " + issue["text"], flush=True)
    stage_results = []
    done = {}
    for stage in stage_commands(out_root, ran, second, args.shifts, settings) + road_stages(out_root, args.roads, args.landxml, args.prototypes):
        if "args" not in stage:
            stage_results.append({"stage": stage["name"], "status": "skipped", "why": stage["why"]})
            continue
        if stage.get("needs") and done.get(stage["needs"]) != "ok":
            stage_results.append({"stage": stage["name"], "status": "skipped", "why": "stage " + stage["needs"] + " did not finish ok"})
            continue
        record = run_one(stage["args"], out_root / stage["name"], stage.get("timeout", args.timeout))
        done[stage["name"]] = record["status"]
        entry = {"stage": stage["name"], "status": record["status"], "exit": record["exit"], "seconds": record["seconds"]}
        if record["status"] != "ok":
            entry["error"] = (record["stderr"].strip() or record["stdout"].strip())[-500:]
        stage_results.append(entry)
        print("stage " + stage["name"] + ": " + record["status"] + " in " + str(record["seconds"]) + " s", flush=True)
    summary = {"files": len(results), "ok": sum(1 for entry in results if entry["status"] == "ok"), "results": results, "stages": stage_results, "skipped": skipped, "xrefs": xrefs, "issues": xref_issues}
    (out_root / "full_run_summary.json").write_text(json.dumps(summary, indent=1, ensure_ascii=False), encoding="utf-8")
    print("files " + str(summary["files"]) + ", ok " + str(summary["ok"]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
