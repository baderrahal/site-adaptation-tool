"""Each check is broken on purpose and must name the fault, and must let a good input pass."""
import json
import re

import pytest

import caller_check
import check_common
import clean_folder
import folder_rules
import holdout
import revit_result
import rules_docs
import source_tags

ROOT = check_common.ROOT


TASK = "---\nid: T1\nlane: build-1\nstage: 1\nstatus: open\nallowed:\n  - python/\n  - docs/one.md\n---\n# Scope\n"


def reader(path):
    return TASK if path == "steps/tasks/T1.md" else None


def test_read_task_takes_the_head_only():
    task = check_common.read_task(TASK + "allowed:\n  - nope/\n")
    assert task["id"] == "T1" and task["lane"] == "build-1" and task["allowed"] == ["python/", "docs/one.md"]


def test_folder_rules_pass_inside_the_task():
    assert folder_rules.pr_failures("Task: T1\n", ["python/a.py", "docs/one.md", "steps/tasks/T1.md"], [], reader) == []


@pytest.mark.parametrize("path", ["dotnet/a.cs", "docs/two.md", "pythonic/a.py"])
def test_folder_rules_fail_outside_the_task(path):
    found = folder_rules.pr_failures("Task: T1\n", [path], [], reader)
    assert len(found) == 1 and path in found[0] and "outside the allowed" in found[0]


@pytest.mark.parametrize("path", ["tests/acceptance/test_a.py", "tests/golden/a.json", "tests/holdout/a.py"])
def test_folder_rules_keep_lead_tests_from_a_task(path):
    assert "owned by the lead" in folder_rules.pr_failures("Task: T1\n", [path], [], reader)[0]


def test_folder_rules_need_a_task_or_lead_line():
    assert "no Task" in folder_rules.pr_failures("no such line", [], [], reader)[0]
    assert folder_rules.pr_failures("Lead: setup\n", ["tests/golden/a.json"], [], reader) == []


def test_folder_rules_name_a_missing_task():
    assert "is not there" in folder_rules.pr_failures("Task: T9\n", ["a/b.py"], [], reader)[0]


def test_folder_rules_block_new_root_files_and_status():
    assert "repo root" in folder_rules.pr_failures("Lead: x\n", [], ["notes.txt"], reader)[0]
    assert folder_rules.pr_failures("Lead: x\n", [], ["CLAUDE.md"], reader) == []
    assert "STATUS" in folder_rules.pr_failures("Lead: x\n", ["steps/STATUS.md"], [], reader)[0]


def test_source_tags_find_a_tag_at_any_depth():
    data = {"plots": [{"ffl": {"value": 1, "sourceTag": "past"}}, {"ffl": {"value": 1, "sourceTag": "computed"}}], "source": "override"}
    assert sorted(source_tags.tagged_values(data)) == ["/", "/plots/0/ffl"]


def test_source_tags_fail_unsigned_and_pass_signed():
    data = {"a": {"sourceTag": "override"}}
    assert "no entry" in source_tags.unsigned("f.json", data, set())[0]
    assert source_tags.unsigned("f.json", data, {"f.json#/a"}) == []
    assert source_tags.unsigned("f.json", data, {"f.json"}) == []


def test_signoff_needs_who_and_a_date():
    text = "| Value | Why | Signed by | Date |\n|---|---|---|---|\n| `a.json` | y | Bader | 2026-11-04 |\n| b.json | y |  |  |\n| c.json | y | Bader | soon |\n"
    assert source_tags.signed_entries(text) == {"a.json"}


def test_the_signoff_file_of_the_repo_reads():
    assert source_tags.signed_entries((ROOT / "docs" / "signoff.md").read_text(encoding="utf-8")) == set()


DIFF = """diff --git a/dotnet/SiteAdaptation.Common/A.cs b/dotnet/SiteAdaptation.Common/A.cs
+++ b/dotnet/SiteAdaptation.Common/A.cs
@@ -0,0 +1 @@
+        public static IReadOnlyList<int> NewThing(string name)
+        public override string Other()
+        public int Count { get
+        public sealed class Holder
+        var value = Called(name)
+++ b/python/b.py
@@ -0,0 +1 @@
+def new_helper(a):
+++ b/python/tests/test_b.py
@@ -0,0 +1 @@
+def test_new_thing():
"""


def test_caller_check_finds_new_functions_and_leaves_out_tests():
    assert caller_check.new_functions(DIFF) == [("dotnet/SiteAdaptation.Common/A.cs", "NewThing"), ("python/b.py", "new_helper")]


def test_caller_check_leaves_out_a_moved_function():
    diff = "+++ b/python/b.py\n-def kept(a):\n+def kept(a, b):\n"
    assert caller_check.new_functions(diff) == []


def test_caller_check_faults():
    functions = [("python/b.py", "new_helper")]
    assert "not listed" in caller_check.failures_of(functions, "no section", lambda name: 1)[0]
    body = "## Callers\nnew_helper by run_build\n\n## Proof\n"
    assert "no call" in caller_check.failures_of(functions, body, lambda name: 0)[0]
    assert caller_check.failures_of(functions, body, lambda name: 1) == []


def test_caller_check_takes_only_the_callers_section():
    body = "## Callers\nnone\n\n## Proof\nnew_helper ran\n"
    assert "not listed" in caller_check.failures_of([("python/b.py", "new_helper")], body, lambda name: 1)[0]


def test_caller_check_takes_a_framework_caller():
    body = "## Callers\nExecute by framework, Revit calls it\n"
    assert caller_check.failures_of([("dotnet/A.cs", "Execute")], body, lambda name: 0) == []


def test_rules_docs_format_faults():
    line = "- **R900 One two three four five six seven.** Input: a. Output: b."
    rule_id, name, whole, _ = ("R900", "One two three four five six seven", line, "x")
    found = rules_docs.format_faults(rule_id, name, whole)
    assert any("7 words" in item for item in found) and any("Test:" in item for item in found)
    assert rules_docs.format_faults("R901", "Short name", "Input: a. Output: b. Test: c.") == []


def test_rules_docs_old_line_passes_a_changed_line_fails():
    old = "- **R1 Read only.** A reader never changes the file."
    baseline = {"R1": {rules_docs.digest(old)}}
    assert rules_docs.check([("R1", "Read only", old, "x")], baseline) == ([], 1)
    faults, count = rules_docs.check([("R1", "Read only", old + " More.", "x")], baseline)
    assert count == 0 and "R1 has no Input:" in faults


def test_rules_docs_every_rule_of_the_repo_is_read():
    rules = rules_docs.rules_in(ROOT)
    index = (ROOT / "docs" / "rules" / "index.md").read_text(encoding="utf-8")
    listed = set(re.findall(r"^- (R\d+) ", index, flags=re.MULTILINE))
    assert listed - {rule[0] for rule in rules} == set()


def test_rules_docs_pass_on_the_repo():
    assert rules_docs.main([]) == 0


def test_revit_result_rule():
    changed = ["dotnet/SiteAdaptation.AddIn/Pane/A.cs"]
    assert "no file in" in revit_result.failures_of(changed, [])[0]
    assert revit_result.failures_of(changed, ["revit-later"]) == []
    assert revit_result.failures_of(changed + ["steps/revit-results/T1.md"], []) == []
    assert revit_result.failures_of(changed + ["steps/revit-results/README.md"], []) != []
    assert revit_result.failures_of(["python/a.py"], []) == []


def test_clean_folder_not_run_with_no_build(tmp_path):
    assert clean_folder.run(sample=tmp_path) is None


def make_build(tmp_path, body):
    sample = tmp_path / "sample"
    sample.mkdir()
    (sample / "input.txt").write_text("made up", encoding="utf-8")
    tool = tmp_path / "root" / "build.py"
    tool.parent.mkdir()
    tool.write_text(body, encoding="utf-8")
    (sample / "build.json").write_text(json.dumps({"command": ["build.py", "{folder}"]}), encoding="utf-8")
    return sample, tool.parent


def test_clean_folder_passes_a_build_that_reads_its_sample(tmp_path):
    sample, root = make_build(tmp_path, "import sys\nprint(open(sys.argv[1] + '/input.txt').read())\n")
    assert clean_folder.run(sample=sample, root=root, patterns=["*/plots.csv"]) == []


def test_clean_folder_fails_a_build_that_reads_a_past_path(tmp_path):
    past = tmp_path / "data" / "plots.csv"
    past.parent.mkdir()
    past.write_text("x", encoding="utf-8")
    sample, root = make_build(tmp_path, "print(open(%r).read())\n" % str(past))
    found = clean_folder.run(sample=sample, root=root, patterns=["*/plots.csv"])
    assert len(found) == 1 and "past job path" in found[0] and "plots.csv" in found[0]


def test_clean_folder_fails_a_build_that_fails(tmp_path):
    sample, root = make_build(tmp_path, "raise SystemExit(3)\n")
    assert any("exited 3" in item for item in clean_folder.run(sample=sample, root=root, patterns=[]))


def test_clean_folder_refuses_a_build_it_cannot_watch(tmp_path):
    sample, root = make_build(tmp_path, "")
    (sample / "build.json").write_text(json.dumps({"command": ["tool.exe"]}), encoding="utf-8")
    assert "not watched" in clean_folder.run(sample=sample, root=root, patterns=[])[0]


def test_past_paths_match_the_real_names():
    patterns = clean_folder.past_patterns()
    assert clean_folder.past_reads([r"D:\a\repo\config\overrides\list.json", r"C:\02-Site Adaptation\x.dwg", r"D:\w\plots.csv"], patterns) != []
    assert len(clean_folder.past_reads([r"D:\a\repo\config\overrides\list.json", r"C:\02-Site Adaptation\x.dwg", r"D:\w\plots.csv"], patterns)) == 3
    assert clean_folder.past_reads([r"D:\t\new-project\input.txt", r"D:\a\repo\config\project.json"], patterns) == []


def test_holdout_not_run_without_answers_or_predictions(tmp_path, capsys):
    assert holdout.main(secret="", folder=tmp_path) == 0
    assert "NOT RUN" in capsys.readouterr().out


def test_holdout_scores_and_prints_no_value(tmp_path, capsys):
    (tmp_path / "p.json").write_text(json.dumps({"a": 700.123, "b": "VD4"}), encoding="utf-8")
    secret = json.dumps({"passShare": 0.9, "tolerance": 0.005, "answers": {"a": 700.125, "b": "XX4"}})
    assert holdout.main(secret=secret, folder=tmp_path) == 1
    out = capsys.readouterr().out
    assert "1 of 2 match" in out and "700" not in out and "XX4" not in out


def test_holdout_passes_at_the_share(tmp_path):
    (tmp_path / "p.json").write_text(json.dumps({"a": 1}), encoding="utf-8")
    assert holdout.main(secret=json.dumps({"passShare": 1.0, "answers": {"a": 1}}), folder=tmp_path) == 0


def test_claude_md_holds_the_top_ten_lessons_and_its_limits():
    claude = (ROOT / "CLAUDE.md").read_text(encoding="utf-8")
    assert len(claude.splitlines()) < 150
    lessons = (ROOT / "docs" / "lessons.md").read_text(encoding="utf-8")
    top = re.findall(r"^- (.+)$", lessons, flags=re.MULTILINE)[:10]
    assert len(top) == 10
    for number, lesson in enumerate(top, start=1):
        assert "%d. %s" % (number, lesson) in claude


def test_lessons_have_no_repeat():
    lines = re.findall(r"^- (.+)$", (ROOT / "docs" / "lessons.md").read_text(encoding="utf-8"), flags=re.MULTILINE)
    assert len(lines) == len(set(lines))


def test_workflow_has_a_job_named_tests_on_windows():
    text = (ROOT / ".github" / "workflows" / "tests.yml").read_text(encoding="utf-8")
    assert re.search(r"^  tests:\n(?:.*\n)*?    runs-on: windows-latest$", text, flags=re.MULTILINE)
    assert "ready_for_review" in text
