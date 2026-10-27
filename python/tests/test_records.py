"""R400. The caps of the records: the start reads small and no record outside steps/history/ over 100 KB."""
import record_parts


def make_repo(root, claude=100, progress_lines=10, line=50, log_top=100, record=100, history=100):
    (root / "steps" / "history").mkdir(parents=True)
    (root / "docs").mkdir()
    (root / "CLAUDE.md").write_text("c" * claude, encoding="utf-8")
    (root / "steps" / "PROGRESS.md").write_text("\n".join(["p" * line] * progress_lines) + "\n", encoding="utf-8")
    (root / "steps" / "log.md").write_text("# Round log\n\nHead.\n\n## Top\n\n" + "t" * log_top + "\n\n## Older\n\n" + "o" * 9000 + "\n", encoding="utf-8")
    (root / "docs" / "rules.md").write_text("r" * record, encoding="utf-8")
    (root / "steps" / "history" / "old.md").write_text("h" * history, encoding="utf-8")
    return root


def test_r400_record_sizes():
    assert record_parts.cap_breaches() == []


def test_r400_a_small_repo_passes_and_history_has_no_cap(tmp_path):
    assert record_parts.cap_breaches(make_repo(tmp_path, history=200 * 1024)) == []


def test_r400_each_cap_names_the_file_that_passes_it(tmp_path):
    found = record_parts.cap_breaches(make_repo(tmp_path, claude=10 * 1024 + 1, progress_lines=61, line=201, log_top=4 * 1024, record=100 * 1024 + 1))
    assert [item.split(" ")[0] for item in found] == ["CLAUDE.md", "steps/PROGRESS.md", "steps/PROGRESS.md"] + ["steps/PROGRESS.md"] * 61 + ["steps/log.md", "docs/rules.md"]


def test_r400_a_cap_reached_exactly_holds(tmp_path):
    assert record_parts.cap_breaches(make_repo(tmp_path, claude=10 * 1024, progress_lines=30, line=200, log_top=4 * 1024 - 20, record=100 * 1024)) == []
