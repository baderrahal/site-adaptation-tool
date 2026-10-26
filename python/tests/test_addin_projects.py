"""R346. Common on netstandard2.0 with Newtonsoft.Json 13.0.1, the worker and the test project on net48 and net8.0, a Linux leg in CI."""
import re
import xml.etree.ElementTree as ElementTree
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DOTNET = ROOT / "dotnet"
WORKFLOW = ROOT / ".github" / "workflows" / "test.yml"
# MSBuild parts the targets by a semicolon, R15 keeps it out of this file
TWO_TARGETS = "net48" + chr(59) + "net8.0"


def project(name):
    """The properties and the package and project references of a csproj, each reference with its condition."""
    root = ElementTree.parse(DOTNET / name / (name + ".csproj")).getroot()
    properties = {}
    for group in root.iter("PropertyGroup"):
        for item in group:
            properties[item.tag] = (item.text or "").strip()
    packages = [(item.get("Include"), item.get("Version"), item.get("PrivateAssets") or "") for item in root.iter("PackageReference")]
    references = []
    for group in root.iter("ItemGroup"):
        for item in group.iter("ProjectReference"):
            references.append((Path(item.get("Include").replace("\\", "/")).stem, item.get("Condition") or group.get("Condition") or ""))
    return properties, packages, references


def jobs(text):
    """The jobs of the workflow by name, each its text, read without a YAML library."""
    body = text.split("\njobs:\n", 1)[1]
    parts = re.split(r"^  (\S[^:]*):\n", body, flags=re.MULTILINE)
    return {parts[index]: parts[index + 1] for index in range(1, len(parts) - 1, 2)}


def test_r346_common_is_netstandard2_0_with_newtonsoft_json_13_0_1_its_one_runtime_package():
    properties, packages, references = project("SiteAdaptation.Common")
    assert properties.get("TargetFramework") == "netstandard2.0"
    assert "TargetFrameworks" not in properties
    # Q079. The runtime graph package of the restore is pinned to its first MIT version, build only, so nothing of it flows on
    assert packages == [("Newtonsoft.Json", "13.0.1", ""), ("Microsoft.NETCore.Platforms", "3.1.0", "all")]
    assert references == []


def test_r346_the_worker_is_an_exe_on_net48_and_net8_0():
    properties, packages, references = project("CadReader")
    assert properties.get("OutputType") == "Exe"
    assert properties.get("TargetFrameworks") == TWO_TARGETS
    # The props of the folder name net48 for every project, a project of two targets clears it so both are built
    assert properties.get("TargetFramework", "") == ""
    assert ("SiteAdaptation.Common", "") in references


def test_r346_the_test_project_runs_on_net48_and_net8_0_and_the_probe_only_on_net48():
    properties, packages, references = project("SiteAdaptation.Tests")
    assert properties.get("TargetFrameworks") == TWO_TARGETS
    assert properties.get("TargetFramework", "") == ""
    found = dict(references)
    assert found["SiteAdaptation.Common"] == "" and found["CadReader"] == ""
    assert "net48" in found["SiteAdaptation.C3DProbe"]
    text = (DOTNET / "SiteAdaptation.Tests" / "SiteAdaptation.Tests.csproj").read_text(encoding="utf-8")
    # The probe tests use the net48 probe, so the net8.0 leg leaves them out
    for name in ("C3DProbeTests.cs", "DataBookTests.cs"):
        assert re.search(r'<Compile Remove="%s"' % re.escape(name), text), name


def test_r346_ci_runs_every_test_on_windows_and_the_net8_0_leg_on_linux_and_fails_on_zero_tests():
    found = jobs(WORKFLOW.read_text(encoding="utf-8"))
    windows = [text for text in found.values() if "runs-on: windows-latest" in text]
    linux = [text for name, text in found.items() if "runs-on: ubuntu-latest" in text and "dotnet test" in text]
    assert len(windows) == 1 and len(linux) == 1
    assert "dotnet test dotnet/SiteAdaptation.sln" in windows[0]
    assert "-f net8.0" in linux[0] and "SiteAdaptation.Tests.csproj" in linux[0]
    # R11 on both legs: every trx file counted and a run of zero tests fails, the Windows leg writes one per target
    for text in windows + linux:
        assert "No .NET tests ran" in text and "executed" in text
    assert "foreach" in windows[0] or "Measure-Object" in windows[0]


def test_r346_the_job_reader_sees_a_made_up_workflow():
    text = "name: x\n\njobs:\n  one:\n    runs-on: ubuntu-latest\n    steps: a\n  two:\n    runs-on: windows-latest\n"
    assert jobs(text) == {"one": "    runs-on: ubuntu-latest\n    steps: a\n", "two": "    runs-on: windows-latest\n"}
