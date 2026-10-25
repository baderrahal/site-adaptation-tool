"""R201, R202 and R218. The files of a round outside the repo: the data book snapshot, the originals and the work copies.

snapshot: the manifest written when the snapshot was taken is checked against the snapshot now, so the round knows it read
the same bytes all through (R201). originals: the four models under inputRoot are hashed now and compared with the hashes
taken before the round (R202). clean: the work copies the round no longer needs are listed with their size and, with
--delete, deleted. Only names inside the work folder that siteBuild.keep does not name are ever touched (R218).
"""
import argparse
import hashlib
import json
import shutil
import sys
from pathlib import Path

import project_profile


def sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for block in iter(lambda: handle.read(1 << 20), b""):
            digest.update(block)
    return digest.hexdigest()


def read_manifest(path):
    """sha256sum lines, <hash> *<path> or <hash>  <path>, as {path: hash} with ./ dropped."""
    found = {}
    for line in Path(path).read_text(encoding="utf-8").splitlines():
        if not line.strip():
            continue
        digest, name = line[:64], line[64:].lstrip(" *")
        found[name[2:] if name.startswith("./") else name] = digest.lower()
    return found


def check_snapshot(folder, manifest):
    """R201. Each file of the manifest against the snapshot now: same, changed or gone, and files the manifest lacks."""
    folder = Path(folder)
    listed = read_manifest(manifest)
    result = {"same": 0, "changed": [], "gone": [], "notListed": []}
    for name, digest in listed.items():
        path = folder / name
        if not path.is_file():
            result["gone"].append(name)
        elif sha256(path) != digest:
            result["changed"].append(name)
        else:
            result["same"] += 1
    manifest_name = Path(manifest).name
    for path in folder.rglob("*"):
        name = path.relative_to(folder).as_posix()
        if path.is_file() and name not in listed and name != manifest_name:
            result["notListed"].append(name)
    return result


def check_originals(folder, before):
    """R202. The originals now against the hashes taken before the round."""
    listed = read_manifest(before)
    return {name: ("same" if (Path(folder) / name).is_file() and sha256(Path(folder) / name) == digest else "changed or gone") for name, digest in listed.items()}


def size_of(path):
    path = Path(path)
    return sum(item.stat().st_size for item in path.rglob("*") if item.is_file()) if path.is_dir() else path.stat().st_size


def clean_list(work, keep):
    """R218. The entries of the work folder not named in keep, with their size in bytes. Nothing outside work is listed."""
    work = Path(work)
    if not work.is_dir():
        return []
    kept = {name.lower() for name in keep}
    return [{"name": path.name, "bytes": size_of(path)} for path in sorted(work.iterdir()) if path.name.lower() not in kept]


def delete_listed(work, rows):
    """R218. Deletes the listed entries of the work folder, each checked to sit right in it and not to be a link."""
    work = Path(work).resolve()
    for row in rows:
        path = work / row["name"]
        if path.resolve().parent != work or path.is_symlink():
            raise project_profile.ProfileError("refused, not a plain entry of the work folder: " + row["name"])
        if path.is_dir():
            shutil.rmtree(path)
        else:
            path.unlink()


def main(argv=None):
    parser = argparse.ArgumentParser(description="Snapshot, originals and clean up of a round (R201, R202, R218).")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("what", choices=("snapshot", "originals", "clean"))
    parser.add_argument("--manifest", help="snapshot: its manifest. originals: the hashes taken before the round")
    parser.add_argument("--keep", nargs="*", default=[], help="clean: the names in work that stay")
    parser.add_argument("--delete", action="store_true", help="clean: delete what is listed")
    args = parser.parse_args(argv)
    block = project_profile.load_settings(args.settings).get("siteBuild")
    root = Path(block["folder"])
    if args.what == "snapshot":
        result = check_snapshot(root / "snapshot", args.manifest or root / "snapshot" / "manifest_snapshot.txt")
        print("snapshot same %d, changed %d, gone %d, not listed %d" % (result["same"], len(result["changed"]), len(result["gone"]), len(result["notListed"])))
        return 0 if not result["changed"] and not result["gone"] else 1
    if args.what == "originals":
        settings = project_profile.load_settings(args.settings)
        folder = Path(settings.get("inputRoot")) / block["answerKey"]["folder"]
        result = check_originals(folder, args.manifest)
        for name, state in result.items():
            print("%s %s" % (name, state))
        return 0 if all(state == "same" for state in result.values()) else 1
    if not (root / "work").is_dir():
        print("no work folder in %s, nothing to clean" % root)
        return 0
    rows = clean_list(root / "work", args.keep)
    for row in rows:
        print("%s %d bytes" % (row["name"], row["bytes"]))
    print("total %d bytes in %d entries" % (sum(row["bytes"] for row in rows), len(rows)))
    if args.delete:
        delete_listed(root / "work", rows)
        print("deleted")
    return 0


if __name__ == "__main__":
    sys.exit(main())
