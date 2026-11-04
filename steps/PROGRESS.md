STATE RUNNING, records/split-over-cap and task/R1-build-1, 2026-11-05 02:46
# Progress
Written by scripts/status.ps1 -Progress at 2026-11-05 02:46 from git, PRs and checks, never by hand. Main is at deb292d.

## State

- Task R1-build-1, lane build-1, status open
- Task R1-revit-test, lane revit-test, status waiting
- Task SETUP-sign-2, lane revit-test, status waiting
- Task SETUP-sign, lane revit-test, status done

## Done

- PR 198, merged 11/4/2026 : Scenarios: the repo table holds counts and marks only
- PR 196, merged 11/4/2026 : SETUP-sign: move what it did not prove to SETUP-sign-2
- PR 195, merged 11/4/2026 : SETUP-sign: one prompt instead of four, developer add-ins for a run only
- PR 194, merged 11/4/2026 : Scenario maker: change only text a person reads, and MTEXT lines
- PR 193, merged 11/4/2026 : Lanes: the branch names the current task, a later task waits
- PR 192, merged 11/4/2026 : R1-build-1: allow the dataCheck block of config
- PR 191, merged 11/4/2026 : R1: acceptance tests, tasks and scenario tools
- PR 190, merged 11/4/2026 : SETUP-sign: find why Load Once shows many times

## Open bugs

- PR 197 has a failed check: R1-build-1: stage 1 from a folder, the worker command dataCheck
- Known bugs and leftovers: steps/backlog.md, steps/latent.md

## Bader's turn

- 197 questions open, steps/questions-open.md and steps/history/questions-open-older.md

## Next three steps

- Task R1-build-1 in lane build-1, open
- Task R1-revit-test in lane revit-test, waiting
- Task SETUP-sign-2 in lane revit-test, waiting
Hash of the lines above, a hand edit fails a test: de7dfac28c86396492ab42e1d8fb468b9104a663e3d43af5939570b460cd779e
