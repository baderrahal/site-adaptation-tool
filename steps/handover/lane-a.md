# Lane A handover, 4 Nov 2026

Main at 13356fe when written. Tags as CLAUDE.md. Paths under round_addin/logs/ are outside the repo, in Documents\SiteAdaptation.

## State

- Open PRs of any lane: 0. RAN gh pr list.
- Revit lock free, no Revit window. RAN window_lock.py --status: lock false, freeGB 35.82, processes empty.
- Lane A branches not merged on the remote: 0. RAN git branch -r --no-merged origin/main, filtered by Lane A names.

## What I built

- KNOWN, PR 166: R520 Start from reopens its project, R521 Review numbers a two number plot. docs/rules/10-addin-r407-up.md:78, dotnet/SiteAdaptation.Common/Settings/SavedStarts.cs:27, Review/ReviewState.cs:26.
- COORD, PR 171 and 174: R510 to R514, R523, no site coordinates, CAD default, side by side, asks when no CAD, no move with placed content. docs/rules/13-site-coords-r454-up.md:8, dotnet/SiteAdaptation.Common/SetUp/SiteSources.cs:49 and :205, config/project.json:1136.
- RAMP, PR 178: R470, R471 changed, R515 to R518 new, shared corners and 2 mm pull. docs/rules/10-addin-r407-up.md:28 and :71, Plan/FloorOutlines.cs:46, :124, :170, :232, python/plan_from_book.py:724, :744, :774, python/floor_fit.py:321, python/design_basis.py:307, config/project.json:221.
- Earlier Lane A rounds, by title: 4h-1 PR 107, 4h-2 PR 109, 5e and 5f PR 110, 4i PR 116, 4j-2 PR 122, FIX-1NOV-A PR 128 and 130, FIX-2NOV-A PR 139, 142, 149, FIX-3NOV-A PR 156. RAN gh pr list. Each PR body names its rules. I did not re-read those bodies now.

## Half done

- No code half done. No branch holds unmerged work. RAN git rev-list origin/main..branch, 0 for fix/coord-window-a, fix/known-3nov-a, fix/3nov-a-mode.
- Named but never built: the copy offer of Q397. steps/truth-audit.md:245.
- newProjectCheck has no sender in the add-in. WorkerRequest.CoordinateSource and PositionFile wait for one. steps/latent.md:576.

## Known bugs and traps

- A model with floors from before PR 178 stops at step 5, nothing placed, no toposolid. See it: press button 5 on a copy of SA-dryrun3nov-done.rvt, 1,003 listed. FloorsRun.cs:476 and :163, PR 180 body line 14, Q439. Start from SA-dryrun3nov.rvt, it has no floors.
- Past job misses the RAMP target: 2 two plot overlaps, 360 of 555 corners over 1 mm on NBH 012. Only a new project reaches 0 and 0. PR 178 body lines 9 and 10, Q430, Q432.
- Do not read 443 to 2 as one build compared. The 443 came from a hand run on a4551dd. PR 178 body line 13.
- One override entry turns the corner joint off for the whole build, with no line in the window. R516, Q437.
- C10 ramps beside a neighbour step up to 83 mm with no check row, back corners 20 mm apart. Q438.
- Accepted same plot warnings stay in the saved model, 481 and 176. PR 178 body line 47, Q385.
- Test host: a run file already in the folder is refused, AddinRun.cs:81 to 84. PR 174 records a buttons job with keyCopy refused there, yet Run-TestHost.ps1:331 says the key copy is taken in. Lane D pressed a copy of a model by a route UNKNOWN to me. Ask Lane D before you trust either.
- The main folder goes stale. RAN git worktree list now: main folder at 57b548e, main at 13356fe. Check the head before a Revit run, the installed build comes from where you build.
- CI dotnet can fail on R347_a_time_limit_that_fires_after... under load. Run 41780951928 attempt 1 failed, attempt 2 passed, on one line of Markdown. Commit 858a2d0 calls it load sensitive. Cause not fixed.
- 49 worktrees under .claude/worktrees. RAN git worktree list. Never gh --delete-branch with a worktree on that branch, delete the remote with git push. Reason held in my memory file only, Lane D's lost metadata, not found in the repo.
- steps/questions-open.md is 101,969 bytes against the 100 KB cap. RAN wc -c. test_r400 passed at 101,742, the next long question can fail python CI. Exact byte limit UNKNOWN, my grep of the test found no number.
- Line 1 of steps/PROGRESS.md reads STATE WAITING, 2026-11-01 04:40. RAN. Do not take its date as the last change.

## What I wish I had known

- A pull of 2 mm changes every outline, and button 5 treats that as a change it cannot make. FloorsRun.cs:473 to 476 was there to read before PR 178.
- A template model reads zero position yet its site location says Boston, MA. PR 171 body line 12. Decide no coordinates by position and points only.
- The real window driver reads the first of 5 buttons with one name, so Pick surfaces of the road row was never pressed. PR 174 body, Not proven.

## What is missing

- Tool: change a floor outline in place. FloorsRun.cs:473 says the design gives no way. Q439 needs this or a restart.
- Tool: the lines of a real press reach no window. PR 180 body line 38, Q476.
- Test host: a press of Use CAD, and a run of R523 on a real model. PR 174 body, Not proven.
- Way of work: 197 questions open against one person. RAN grep -c on steps/questions-open.md. A round closes with its choices still unanswered, so later rounds stack on them.
- Way of work: Not proven lists live in PR bodies only. steps/truth-audit.md had to gather them after the fact.

## Lessons

1. Run a claim before you tell Bader. My READ of Q439 said floors stay, the run said the button stops. PR 182.
2. Count by a second method and chase the gap. Plan count 0, Revit count 2, never explained. PR 178 body line 41.
3. Merging before the Revit pass moves the faults to a second PR. PR 171 merged, Revit found 2 window faults, PR 174.
4. Name a choice as CHOSEN by Code in the file itself. The breaker found my config crediting Bader for one. CHOSEN wording now at config/project.json:221 and :222, the finding itself UNKNOWN in the repo.
5. A broad rule needs its test on old models, not only new builds. R517 passed every check and broke a saved model. Q439.

## Open points only I know

These have no question of their own, the first touches Q432. SEARCHED git grep of steps and docs: no hit for bytes not equal or build_proof.py not run. The others not searched.

- Revit counts 2 overlaps where plan points count 0, why UNKNOWN. Recommended: name the 2 at the next past job run before any fix. PR 178 body line 41.
- C# and Python plans on the IMC cover: 324 plots, 0 fields differ, bytes not equal, cause not checked, whole project not compared. Recommended: compare by parsed fields on the whole project, then close it. PR 178 body line 37.
- python/build_proof.py was not run on the RAMP pull. Recommended: run it before citing RAMP as proven. PR 178 body line 45.
- RAMP on a new project ran on a scratch config, rev6 and an empty list, logs/p3nov-ramp/revit/repoN, not the New project window with the shipped set. Recommended: repeat NBH 012 through the real path first, it is the one result the target rests on. PR 178 body line 46.
- PR 174's two changes are unseen in Revit. Recommended: look at the badge and the side by side position at the next Set up site run. PR 174 body.
- Road slope 0.5% at back corners and the 2 mm pull are my values, not Bader's. Q433 and Q436 ask to accept the effect. Recommended: Bader confirms the values there. config/project.json:222.

## Questions on my rounds

Q355, Q379, Q394, Q395, Q397, Q401, Q402, Q420, Q430, Q432, Q433, Q436 to Q439, each with its recommended answer in steps/questions-open.md.
