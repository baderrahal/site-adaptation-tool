STATE RUNNING, lead/records-recovery, 2026-11-05 23:38
# Progress
Written by scripts/status.ps1 -Progress at 2026-11-05 23:38 from git, PRs and checks, never by hand. Main is at 9af6803.

## State

- Task R1-build-1, lane build-1, status done
- Task R1-revit-test, lane revit-test, status done
- Task R2-C1-build-1, lane build-1, status done
- Task R2-C1-fix-build-1, lane build-1, status open
- Task R2-C1-revit-test, lane revit-test, status done
- Task R2-C2-build-1, lane build-1, status waiting
- Task R2-C2-revit-test, lane revit-test, status waiting
- Task R2-C3-build-1, lane build-2, status done
- Task R2-C3-revit-test, lane revit-test, status waiting
- Task SETUP-sign, lane revit-test, status done
- Task SETUP-sign-2, lane revit-test, status waiting

## Done

- PR 213, merged 2026-11-05: R639 rule line from R2-C1-fix-build-1
- PR 212, merged 2026-11-05: Records of PR 209 and PR 211, a rule for a Revit driver, the rule test reads the lane test folders
- PR 211, merged 2026-11-05: R2-C3-build-1: rules per project, rulesSetup, rulesChange, RulesForBuild.Check and the templates
- PR 210, merged 2026-11-05: Split C2 and C3 for two lanes, the worker seam they share, R2-C1-fix
- PR 209, merged 2026-11-05: R2-C1-revit-test: the window Confirm what was found, button 2 on a raw folder, checked in Revit on an empty model
- PR 208, merged 2026-11-05: Hold-out score of S5, questions Q495 to Q498, the S3 latent line
- PR 207, merged 2026-11-05: R2-C1: scores of the merged code, before and after a confirmed profile
- PR 206, merged 2026-11-05: R347 out of the required job, open R2-C1-revit-test

## Open bugs

- Known bugs and leftovers: steps/backlog.md, steps/latent.md

## Bader's turn

- 204 questions open, steps/questions-open.md and steps/history/questions-open-older.md
- Read steps/bader-trust.md

## Next three steps

- Task R2-C1-fix-build-1 in lane build-1, open
- Task R2-C2-build-1 in lane build-1, waiting
- Task R2-C2-revit-test in lane revit-test, waiting
Hash of the lines above, a hand edit fails a test: c1266328715ba81ba2e4920821b80d35e88855a6cd6609e0137d391f24260de3
