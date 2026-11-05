STATE WAITING, 2026-11-05 11:35
# Progress
Written by scripts/status.ps1 -Progress at 2026-11-05 11:35 from git, PRs and checks, never by hand. Main is at 53c1425.

## State

- Task R1-build-1, lane build-1, status done
- Task R1-revit-test, lane revit-test, status done
- Task R2-C1-build-1, lane build-1, status done
- Task R2-C1-revit-test, lane revit-test, status open
- Task R2-C2-build-1, lane build-1, status waiting
- Task R2-C2-revit-test, lane revit-test, status waiting
- Task R2-C3-build-1, lane build-1, status waiting
- Task R2-C3-revit-test, lane revit-test, status waiting
- Task SETUP-sign-2, lane revit-test, status waiting
- Task SETUP-sign, lane revit-test, status done

## Done

- PR 205, merged 11/5/2026 : R1 check, the rules out of memory, the lane starter
- PR 204, merged 11/5/2026 : R1-revit-test: scope after its first review
- PR 203, merged 11/5/2026 : R2-C1-build-1: stage 1 finds the code table and typology column, takes what a person confirmed
- PR 202, merged 11/5/2026 : R1-revit-test: the section Data check in the window What the tool read
- PR 201, merged 11/5/2026 : R2: acceptance tests, tasks and hold-out scoring
- PR 200, merged 11/5/2026 : Harness to AI MAX version 5
- PR 199, merged 11/4/2026 : Records: cut questions-open and log under the cap, byte for byte
- PR 198, merged 11/4/2026 : Scenarios: the repo table holds counts and marks only

## Open bugs

- Known bugs and leftovers: steps/backlog.md, steps/latent.md

## Bader's turn

- 197 questions open, steps/questions-open.md and steps/history/questions-open-older.md
- Read steps/bader-trust.md

## Next three steps

- Task R2-C1-revit-test in lane revit-test, open
- Task R2-C2-build-1 in lane build-1, waiting
- Task R2-C2-revit-test in lane revit-test, waiting
Hash of the lines above, a hand edit fails a test: e9a7de8155bfb7cb0e21efeee2665ef8309b8c50776f0d8d0c4d141eb7fae8fd
