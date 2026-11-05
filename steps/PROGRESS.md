STATE RUNNING, lead/plan-fri-6nov, 2026-11-05 23:50
# Progress
Written by scripts/status.ps1 -Progress at 2026-11-05 23:50 from git, PRs and checks, never by hand. Main is at 25257d4.

## State

- Task R1-build-1, lane build-1, status done
- Task R1-revit-test, lane revit-test, status done
- Task R2-C1-build-1, lane build-1, status done
- Task R2-C1-fix-build-1, lane build-1, status open
- Task R2-C1-revit-test, lane revit-test, status done
- Task R2-C2-build-1, lane build-1, status waiting
- Task R2-C2-revit-test, lane revit-test, status waiting
- Task R2-C3-build-1, lane build-2, status done
- Task R2-C3-revit-test, lane revit-test, status open
- Task SETUP-sign, lane revit-test, status done
- Task SETUP-sign-2, lane revit-test, status waiting

## Done

- PR 214, merged 2026-11-05: Records of the recovery of 5 Nov, latent lines of PR 211 and 213, R2-C3-revit-test opens
- PR 213, merged 2026-11-05: R639 rule line from R2-C1-fix-build-1
- PR 212, merged 2026-11-05: Records of PR 209 and PR 211, a rule for a Revit driver, the rule test reads the lane test folders
- PR 211, merged 2026-11-05: R2-C3-build-1: rules per project, rulesSetup, rulesChange, RulesForBuild.Check and the templates
- PR 210, merged 2026-11-05: Split C2 and C3 for two lanes, the worker seam they share, R2-C1-fix
- PR 209, merged 2026-11-05: R2-C1-revit-test: the window Confirm what was found, button 2 on a raw folder, checked in Revit on an empty model
- PR 208, merged 2026-11-05: Hold-out score of S5, questions Q495 to Q498, the S3 latent line
- PR 207, merged 2026-11-05: R2-C1: scores of the merged code, before and after a confirmed profile

## Open bugs

- Known bugs and leftovers: steps/backlog.md, steps/latent.md

## Bader's turn

- 205 questions open, steps/questions-open.md and steps/history/questions-open-older.md
- Read steps/bader-trust.md

## Next three steps

- Task R2-C1-fix-build-1 in lane build-1, open
- Task R2-C2-build-1 in lane build-1, waiting
- Task R2-C2-revit-test in lane revit-test, waiting
Hash of the lines above, a hand edit fails a test: 3331a1b76207149130ebb2b0f48b6f2907ffddf49d761cbcf41b494957e66ba4
