# Handover, Lane C, 4 Nov 2026

Main at 13356fe when written. Paths are from the repo root. "Note" means my session note under the memory folder of this user, not a file in the repo, so treat it as weaker proof. Numbers of plots and questions come from the files named.

## State at handover

- Open PRs: none. `gh pr list --state open` printed [].
- Revit: closed. `Get-Process Revit` count 0.
- Lock: free. `window.lock` under Documents\SiteAdaptation absent, config/project.json:276 names it.
- Installed add-in: main of PR 183 (Note lane-c-truth-4nov.md:11). Later commits touch records only, `git diff --stat 7c9c5a7 13356fe`.

## What I built

- New project window, rows, setup check, Save setup: dotnet/SiteAdaptation.AddIn/NewProject/. R430 to R434, R445, docs/rules/index.md:437-441, 451.
- Scan view, picks, gates, Current files: dotnet/SiteAdaptation.Common/Scan/ScanView.cs. R435, R436, R438 to R443, index.md:442-449.
- Pane that drives the steps, no dead end in windows: dotnet/SiteAdaptation.AddIn/Pane/. R444, R446, index.md:450, 452.
- Revit listener, run timing, two Revit slots: dotnet/SiteAdaptation.Common/RevitListener.cs, dotnet/SiteAdaptation.TestHost/RevitListener.cs. R447 to R449, index.md:453-455.
- Pane warning on old prototypes, slog files out of the input check: R496, R497, index.md:458-459.
- Windows match their boards: R505, index.md:460.
- Help beside each step: dotnet/SiteAdaptation.AddIn/Help/HelpFile.cs, built from steps/buttons-guide.md. R540, index.md:461.
- Team changes rule values, report name fields, step 1 source line: R541 to R543, index.md:462-464.
- Road topography tick: dotnet/SiteAdaptation.AddIn/Pane/PaneViewModel.cs:194. R574, index.md:495.
- All Lane C rule text: docs/rules/12-lane-c-r430-up.md.
- Audit, no code: steps/truth-audit.md, steps/questions-for-bader-4nov.md.

## Half done

- No half done tool work on any branch that I know of.
- origin/records/6c-3nov-c-stop: one commit d703764, one line of steps/HEADSUP.md, stale. `git diff --stat origin/main...origin/records/6c-3nov-c-stop`.
- Conflicts Plot column fix: not built. steps/bader-fix-1nov.md:89. `git log --grep` on the words found no fix commit.
- Drafts for Q286 and Q293: wait on Bader. steps/ui-shots/drafts-for-bader.md, steps/questions-open.md:516, 545.
- Report file name section: not seen in Revit, Rev 06 not run in Revit, Browse not measured. steps/log.md:145.
- Road tick: seen in a rendered image only, not in Revit. steps/log.md:38.
- Listener cases not run in Revit: throw inside Revit, killed, hung, slot 2. steps/log.md:594.
- Raw input work: not started, order waits on Q441. steps/truth-audit.md:273-288.

## Known bugs and traps

- The tool stops with no past output. See: steps/truth-audit.md:7-24, section 5 from line 175.
- A match with the past job proves little, no NBH was held back. steps/truth-audit.md section 6 from line 215.
- The audit is not all hand checked: line numbers of Tables 1 and 2, section 7 rows, section 3 marks. "How this was checked", truth-audit.md from line 294.
- Conflicts Plot column shows the CAD label, not Plot No. See: steps/check-2nov.md:33.
- Never change ScanView.ConflictKey in a display fix, saved picks stop matching. ScanView.cs:242, dotnet/SiteAdaptation.Common/ReadData/ReadPicks.cs:114.
- Keep the run input key "curbs" when renaming a label. dotnet/SiteAdaptation.Common/Read/ReadSteps.cs:16, R366 at index.md:335. The label is separate, PaneText.cs:53.
- 4 Check title reads "1 plots". steps/check-2nov.md:49, CheckTableText.cs:7, no single form found by grep.
- steps/questions-open.md is 431 bytes under the 100 KB cap of R400. `wc -c` printed 101969. A new question first needs text moved or shortened.
- Its index does not match its text. steps/questions-for-bader-4nov.md, "Faults in the open file".
- Commit b69260d says Q415 closed, but steps/questions-open.md:709 holds it open. A question named in config stays open until Bader answers, a test failed when I closed it. Note lane-c-rules-3nov.md:19. Which test: UNKNOWN.
- Closing a question with values in the status block needs rule_status.py --write. Note lane-c-fix-1nov-c-resume.md:13.
- R347 worker time limit tests fail under load, a rerun passes. steps/latent.md:473, 513.
- R310 process sample failed once, passed 3 of 3 alone, cause UNKNOWN. steps/log.md:44.
- A hidden window still takes a UI automation click, so a driver press proves nothing about what a person sees. Note lane-c-fix-1nov-c-resume.md:21.
- A locked screen stops every window press. Note lane-c-rules-3nov.md:14.
- Browse For Folder crash and the input folder write time stay unexplained. Q380, Q381, steps/questions-open.md:622, 626.
- Scripts sent through a Bash heredoc lost double backslashes, use a file writer. Note lane-c-truth-4nov.md:17.
- Do not run git worktree prune, some entries are stuck on permission denied. Note lane-c-speed-done.md:12.
- Documents\SiteAdaptation\lane_c_wt\listener is still on disk with no worktree entry. `Test-Path` True.
- Old flags in Documents\SiteAdaptation\check_6c (hold, rules_hold, rules_hold_b, help_hold) hold nothing. No matching process runs, count 0.

## What I wish I had known at the start

- That step 2 and step 4 need past outputs. Moving them out on a copy is a small read only run. truth-audit.md section 5.
- That 322 of 336 plots took past values in the dry run. A count like this belongs beside every match figure. truth-audit.md:12.
- That four lanes would collide on ids. Q296 to Q298 were renumbered. Note lane-c-scan-ui.md:13.
- Whether the goal was a past job tool or a new site tool. The managers read it as the second. CHOSEN, my view, truth-audit.md:9.

## What is missing

- A run from raw inputs. Short list 1, 2, steps/truth-audit.md:277-278.
- A hold out test or a project the tool has not seen. Short list 6, truth-audit.md:282.
- A mark on each value of computed, rule, pick or past. Short list 11, truth-audit.md:287.
- A whole project through the pane. Q414, Q417, steps/questions-open.md:705.
- In the way we worked: green tests only showed that the tool repeated the past job. No test asked the managers' question. CHOSEN, my view, from truth-audit.md:7-24.
- 197 questions wait on one person, far too many to answer well. steps/questions-for-bader-4nov.md. CHOSEN, my view.
- Clean up: `git worktree list | wc -l` printed 55.

## Lessons

1. Remove the input before you claim the tool does not need it. truth-audit.md section 5.
2. Never score on what you tuned on. truth-audit.md:7-24.
3. State who set the numbers on any demo sheet, a script with expected values reads as staged. truth-audit.md section 4.
4. "Not seen in Revit" belongs in the record each time, tests do not stand in for it. steps/log.md:38, 145.
5. Take an id only after checking origin/main and the open branches. Lane B took R498 while I held it. Note lane-c-6c-3nov.md:17.

## Questions only I know about

- Delete origin/records/6c-3nov-c-stop? Recommended: yes, it holds one stale line.
- Delete lane_c_wt\listener, by hand with a closed Revit? Recommended: yes, no worktree entry names it. Why the permission was denied: UNKNOWN.
- Keep Documents\SiteAdaptation\truth_4nov and check_6c? Recommended: keep truth_4nov, the new lanes can rerun removal.py and peel.py as their raw input test. Delete the old flags in check_6c. These folders are outside git.
- Conflicts Plot column: which value to show? Recommended: show Plot No and CAD label side by side, key unchanged.
- Show the dry run kit again? Recommended: no, not until short list 7, 5, 11 are done. It opens with step 1 done on the past position file, truth-audit.md section 4.
