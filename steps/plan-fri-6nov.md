# Plan, Fri 6 Nov 2026

Read on Thu 5 Nov at about 23:15 from main at 9af6803. Bader's order, item 4 of P-33. steps/STATUS.md stays written by scripts/status.ps1 and out of git, a line there points here.

## Lanes

| Lane | Task | Time | Output |
|---|---|---|---|
| build-1 | R2-C2-build-1 | 09:00 to 17:00 | PR by 13:00 |
| build-2 | no open task | | Next work comes after P2 adds R2-C4 and P3 adds R2-C5 |
| revit-test | finish R2-C1, then R2-C3-revit-test, then R2-C2-revit-test as logic merges | | |

What the files say at 9af6803:

- build-1 still holds R2-C1-fix-build-1 with status open, its PR not yet opened. R2-C2-build-1 is waiting and starts when R2-C1-fix-build-1 is merged, so its 09:00 start rests on that.
- R2-C3-build-1 is done, PR 211. build-2 holds no open task.
- R2-C1-revit-test is done, PR 209. Items 3 and 4 of R2-C1-fix-build-1 are seen in Revit in R2-C2-revit-test, by that task file.
- R2-C3-revit-test opens by PR 214, which is open. R2-C2-revit-test is waiting.
- R2-C2-build-1 no longer holds docs/design/14-ui-labels.md, which is R2-C3-revit-test's, Bader's answer of 5 Nov.

## Bader

- 08:30 decide D-46 on steps/ui-shots/confirm-profile.png.
- 16:00 look at each new screenshot.
- Keep the laptop awake.

## Lead

- 13:00 review of the open PRs.
- 17:00 slip check.

## Slip at 17:00

What was late and why:

## Requests in no task file

| Request | In steps/tasks | In steps/backlog.md |
|---|---|---|
| R-03 | no | no |
| R-07 | no | no |
| R-08 | no | no |
| R-19 | no | no |
| D-46 | no | no |
| R2-C4 and R2-C5 | no task file | no |
| Take the comment naming R639 out of R640_WorkerSeamTests.cs once R639_StageOneFixTests.cs is on main | no | no, steps/latent.md |
| Sign rules/templates/past-job.json, Bader | no | no, docs/signoff.md Not signed |
| Keep or remove nine old agent folders under .claude/worktrees that are not registered worktrees | no | no |
| The account name in public files | no | no, Q520 of PR 214 |

No file of the repo defines R-03, R-07, R-08, R-19, D-46, P2 or P3 as ids, git grep of steps, docs and CLAUDE.md on 9af6803 finds none. Their text is UNKNOWN here.

## Open PRs

- PR 214, Lead's records of the recovery, open, its checks failed on the format of the log entry and run again.
