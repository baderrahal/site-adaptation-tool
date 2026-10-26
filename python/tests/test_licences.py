"""R342. docs/licences.md lists every package of the solution and of requirements.txt with its version and licence."""
import importlib.metadata
import json
import re
import xml.etree.ElementTree as ElementTree
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[2]
LICENCES = ROOT / "docs" / "licences.md"
ALLOWED = {"MIT", "Apache-2.0", "BSD-2-Clause", "BSD-3-Clause"}


def table(text, heading):
    """The rows of the first table under a heading, each a dict by the header cells."""
    part = text.split("## " + heading, 1)[1].split("\n## ", 1)[0]
    lines = [line for line in part.splitlines() if line.startswith("|")]
    header = [cell.strip() for cell in lines[0].strip("|").split("|")]
    return [dict(zip(header, [cell.strip() for cell in line.strip("|").split("|")])) for line in lines[2:]]


def package_references(root):
    """Every PackageReference of the csproj files and Directory.Build.props under root, as (name, version, file)."""
    found = []
    for path in sorted(root.rglob("*")):
        if path.suffix not in (".csproj", ".props") or {"bin", "obj"} & set(path.relative_to(root).parts):
            continue
        for item in ElementTree.parse(path).getroot().iter("PackageReference"):
            found.append((item.get("Include"), item.get("Version"), path.name))
    return found


def requirements(path):
    found = []
    for line in path.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if line and not line.startswith("#"):
            name, _, version = line.partition("==")
            found.append((name.strip(), version.strip()))
    return found


def metadata_licence(name):
    """The licence the installed package gives: License-Expression, else License, else its licence classifier."""
    meta = importlib.metadata.metadata(name)
    if meta.get("License-Expression"):
        return meta["License-Expression"].strip()
    if meta.get("License") and meta["License"].strip() not in ("", "UNKNOWN"):
        return meta["License"].strip()
    classifiers = [value.split("::")[-1].strip() for value in meta.get_all("Classifier") or [] if value.startswith("License ::")]
    return classifiers[-1] if classifiers else None


def test_r342_every_package_reference_of_the_solution_is_listed_with_its_version():
    rows = {row["Package"].lower(): row for row in table(LICENCES.read_text(encoding="utf-8"), ".NET packages")}
    references = package_references(ROOT / "dotnet")
    assert references, "no PackageReference found under dotnet"
    missing = [(name, version, where) for name, version, where in references if name.lower() not in rows]
    other = [(name, version, rows[name.lower()]["Version"], where) for name, version, where in references
             if name.lower() in rows and rows[name.lower()]["Version"] != version]
    assert missing == []
    assert other == []


def test_r342_every_dotnet_package_listed_has_an_allowed_licence_and_says_where_it_ships_and_how_it_was_checked():
    rows = table(LICENCES.read_text(encoding="utf-8"), ".NET packages")
    assert len(rows) >= 20
    wrong = [(row["Package"], row["Licence"]) for row in rows if row["Licence"] not in ALLOWED]
    assert wrong == []
    for row in rows:
        assert row["Ships with"], row["Package"]
        assert re.search(r"\b(READ|SEARCHED|RAN)\b", row["Checked"]), row["Package"]


def test_r342_a_package_that_ships_inside_revit_or_in_the_worker_is_never_one_with_another_licence():
    rows = table(LICENCES.read_text(encoding="utf-8"), ".NET packages")
    shipped = [row for row in rows if re.search(r"\b(add-in|worker)\b", row["Ships with"])]
    assert {row["Package"] for row in shipped} >= {"ACadSharp", "Newtonsoft.Json", "PdfPig", "DocumentFormat.OpenXml"}
    assert all(row["Licence"] in ALLOWED for row in shipped)


def test_r342_every_requirement_is_listed_with_its_version():
    rows = {row["Package"].lower(): row for row in table(LICENCES.read_text(encoding="utf-8"), "Python packages")}
    wanted = requirements(ROOT / "requirements.txt")
    assert wanted
    assert [(name, version) for name, version in wanted if name.lower() not in rows or rows[name.lower()]["Version"] != version] == []


def test_r342_the_licence_of_each_requirement_is_the_one_its_installed_metadata_gives():
    rows = {row["Package"].lower(): row for row in table(LICENCES.read_text(encoding="utf-8"), "Python packages")}
    checked = 0
    for name, version in requirements(ROOT / "requirements.txt"):
        try:
            installed = importlib.metadata.version(name)
        except importlib.metadata.PackageNotFoundError:
            continue
        if installed != version:
            continue
        assert rows[name.lower()]["Licence"] == metadata_licence(name), name
        checked += 1
    if checked == 0:
        pytest.skip("no requirement is installed at its pinned version")


def test_r342_the_table_reader_and_the_package_scan_see_a_made_up_project(tmp_path):
    (tmp_path / "a").mkdir()
    (tmp_path / "a" / "a.csproj").write_text('<Project><ItemGroup><PackageReference Include="Lib.One" Version="1.2.3" /></ItemGroup></Project>', encoding="utf-8")
    (tmp_path / "a" / "obj").mkdir()
    (tmp_path / "a" / "obj" / "skip.csproj").write_text('<Project><ItemGroup><PackageReference Include="Lib.Two" Version="9" /></ItemGroup></Project>', encoding="utf-8")
    assert package_references(tmp_path) == [("Lib.One", "1.2.3", "a.csproj")]
    text = "## .NET packages\n\n| Package | Version | Licence |\n|---|---|---|\n| Lib.One | 1.2.3 | MIT |\n\n## Next\n"
    assert table(text, ".NET packages") == [{"Package": "Lib.One", "Version": "1.2.3", "Licence": "MIT"}]


def assets_packages(root):
    """Every package of the obj/project.assets.json files under root, the ones a restore pulled in too, as {name: (version, project folder)}."""
    found = {}
    for path in sorted(root.rglob("project.assets.json")):
        if path.parent.name != "obj":
            continue
        libraries = json.loads(path.read_text(encoding="utf-8")).get("libraries") or {}
        for key, item in libraries.items():
            if item.get("type") == "package":
                name, version = key.split("/", 1)
                found.setdefault(name.lower(), (version, path.parent.parent.relative_to(root).as_posix()))
    return found


def test_r342_every_package_a_restore_pulled_in_has_a_row_with_its_version():
    found = assets_packages(ROOT / "dotnet")
    if not found:
        pytest.skip("no project.assets.json under dotnet, run dotnet restore first")
    rows = {row["Package"].lower(): row for row in table(LICENCES.read_text(encoding="utf-8"), ".NET packages")}
    assert sorted((name, version, project) for name, (version, project) in found.items() if name not in rows) == []
    assert sorted((name, version, rows[name]["Version"]) for name, (version, project) in found.items() if name in rows and rows[name]["Version"] != version) == []


def test_r342_the_assets_scan_sees_a_made_up_restore(tmp_path):
    (tmp_path / "a" / "obj").mkdir(parents=True)
    libraries = {"Lib.One/1.2.3": {"type": "package"}, "a/1.0.0": {"type": "project"}}
    (tmp_path / "a" / "obj" / "project.assets.json").write_text(json.dumps({"libraries": libraries}), encoding="utf-8")
    assert assets_packages(tmp_path) == {"lib.one": ("1.2.3", "a")}
