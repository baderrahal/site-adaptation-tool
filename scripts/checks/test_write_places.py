"""scripts/checks/write_places.py fails on a name a test run left outside the work root."""
import subprocess

import write_places


def places(tmp_path):
    drive = tmp_path / "drive"
    documents = drive / "Users" / "x" / "Documents"
    repo = drive / "repo"
    documents.mkdir(parents=True)
    repo.mkdir()
    subprocess.run(["git", "init", "-q"], cwd=repo, check=True)
    (repo / ".gitignore").write_text("out/\nbin/\n", encoding="utf-8")
    return str(drive), str(documents), repo


def test_a_new_folder_on_the_drive_root_in_documents_or_in_the_repo_fails(tmp_path):
    drive, documents, repo = places(tmp_path)
    work = tmp_path / "drive" / "SA-work" / "SiteAdaptation"
    before = write_places.snapshot([drive], documents, repo)
    work.mkdir(parents=True)
    (tmp_path / "drive" / "sa-test-none").mkdir()
    (tmp_path / "drive" / "Users" / "x" / "Documents" / "SiteAdaptation").mkdir()
    (repo / "out").mkdir()
    (repo / "out" / "plan.json").write_text("{}", encoding="utf-8")
    (repo / "bin").mkdir()
    (repo / "bin" / "a.dll").write_text("", encoding="utf-8")
    new = write_places.new_rows(before, write_places.snapshot([drive], documents, repo), work, [drive], documents)
    assert new == ["%s sa-test-none" % drive, "%s SiteAdaptation" % documents, "repo out/plan.json"]


def test_the_work_root_in_documents_of_a_pc_with_no_machine_file_passes(tmp_path):
    drive, documents, repo = places(tmp_path)
    work = tmp_path / "drive" / "Users" / "x" / "Documents" / "SiteAdaptation"
    before = write_places.snapshot([drive], documents, repo)
    work.mkdir()
    assert write_places.new_rows(before, write_places.snapshot([drive], documents, repo), work, [drive], documents) == []
