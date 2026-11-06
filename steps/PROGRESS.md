STATE WAITING, 2026-11-06 02:13
# Progress
Written by scripts/status.ps1 -Progress at 2026-11-06 02:13 from git, PRs and checks, never by hand. Main is at 649b5a7.

## State

- Task R1-build-1, lane build-1, status done
- Task R1-revit-test, lane revit-test, status done
- Task R2-C1-build-1, lane build-1, status done
- Task R2-C1-fix-build-1, lane build-1, status done
- Task R2-C1-revit-test, lane revit-test, status done
- Task R2-C2-build-1, lane build-1, status open
- Task R2-C2-revit-test, lane revit-test, status waiting
- Task R2-C3-build-1, lane build-2, status done
- Task R2-C3-revit-test, lane revit-test, status open
- Task R2-C4-revit-test, lane revit-test, status waiting
- Task SETUP-sign, lane revit-test, status done
- Task SETUP-sign-2, lane revit-test, status waiting

## Done

- PR 217, merged 2026-11-05: R2-C2-build-1 opens, the R639 comment goes
- PR 216, merged 2026-11-05: R2-C1-fix-build-1: stage 1 says what it did, R639
- PR 215, merged 2026-11-05: Plan for Fri 6 Nov, R2-C2-build-1 holds the design files by name
- PR 214, merged 2026-11-05: Records of the recovery of 5 Nov, latent lines of PR 211 and 213, R2-C3-revit-test opens
- PR 213, merged 2026-11-05: R639 rule line from R2-C1-fix-build-1
- PR 212, merged 2026-11-05: Records of PR 209 and PR 211, a rule for a Revit driver, the rule test reads the lane test folders
- PR 211, merged 2026-11-05: R2-C3-build-1: rules per project, rulesSetup, rulesChange, RulesForBuild.Check and the templates
- PR 210, merged 2026-11-05: Split C2 and C3 for two lanes, the worker seam they share, R2-C1-fix

## Open bugs

- Known bugs and leftovers: steps/backlog.md, steps/latent.md

## Bader's turn

- 205 questions open, steps/questions-open.md and steps/history/questions-open-older.md
- Read steps/bader-trust.md

## Next three steps

- Task R2-C2-build-1 in lane build-1, open
- Task R2-C2-revit-test in lane revit-test, waiting
- Task R2-C3-revit-test in lane revit-test, open
Hash of the lines above, a hand edit fails a test: bf845b739b7ab5b57d615d8c13cea6e259885a1b65b4e8077b33384efad3d0e2
