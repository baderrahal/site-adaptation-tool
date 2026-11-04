---
name: audit
description: Full repo check of tests, source tags, overrides, folder rules, rules docs and stale files, written to steps/audit.md.
---

# Audit

Read only apart from steps/audit.md. Every line gives a count and the command behind it, UNKNOWN when it could not run.

1. Tests: `python -m pytest python`, `python -m pytest scripts/checks tests/acceptance`, `dotnet test dotnet/SiteAdaptation.sln -c Release`. Counts passed, failed and skipped.
2. Checks: run source_tags, rules_docs and folder_rules of scripts/checks with `--all`, and clean_folder, golden and holdout. Note each one that reports NOT RUN.
3. Overrides: entries in config/overrides/, how many a new project would take, how many are signed in docs/signoff.md.
4. Past data: grep code and the shipped set for past job names.
5. Stale files: state lines older than the last merge, tasks with status open and no branch, worktrees with nothing ahead of main. List them, never delete them.
6. The open question count, and backlog items with no id.

Write steps/audit.md with the date, the counts and what changed since the last audit. Open a PR for it through /merge.
