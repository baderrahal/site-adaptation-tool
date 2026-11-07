# Plan, fast build, 6 to 8 Nov 2026

The one live file of the three days, Bader's order of 6 Nov. Lead keeps it true at every merge. The managers see the tool on Mon 9 Nov.

## build-1, FAST-1-build-1, the face of the tool in Revit

- Built: on main from R2, the Confirm window of R2-C1, Read data on a raw folder, Review data.
- Next: the pane in 3 stages, then stage 1 wired, then Settings and the rules, then the one Build button, then stage 3, then no road level per Q504 5A.
- 14:15 started hidden by Lead, while a session Bader opened ran in the same folder. The hidden one saw the other's work and stopped with no edit. Bader stopped his at a safe point, pushed as ebb346e: the 3 cards, Choose project folder, the data check rows, Fix items, Report. 4 tests fail, the handover is steps/heads-up/build-1.md.
- 15:10 Lead gave its seam line: UiLabelTests.cs on its list, R680 and R681 in docs/rules/18-fast-pane-r680-up.md, Revit opening to FAST-3. Restarted hidden from ebb346e, one session only.
- Blocks: none.

## build-2, FAST-2-build-2, the logic for any project

- Built: on main from R2, stage 1 data check, rules per project, siteBuild with its plan and 2D preview.
- Next: shared parameters for any project, then the source mark per value, then Update from report on a new project, then plots read, mirror, no RAM floor, wall thickness, no road level per Q504 5A, the R310 limit per 4A.
- 14:15: started hidden by Lead with scripts/lanes/run_lane.py, opus, effort high, after the session Bader opened from the sidebar found no bash. Branch task/FAST-2-build-2 on origin. Reading the parameter code, no PR yet.
- Blocks: none.

- 16:20: PR 228, the 3 cards and stage 1, reviewed, its 4 fixes pushed as d256226, merges on green. Next item 3, the rules table, on task/FAST-1-build-1-rules.

## build-2 at 16:20

- PR 229 merged 16:20 as 7568d40: item 1, shared parameters, R700, and the Python side of item 6, R701. The config and worker side of item 6 waits on the exact test paths. Next item 2, the source mark per value.

## build-3, FAST-4-build-3, the plot read and the mirror

- 20:00: FAST-4 done, PR 240 merged. S1 plots read 2964 to 2618, 346 listed apart with a reason, numbers right 1873, mirror right 448 to 735 of 1891. 745 plots with a villa sign lie in areas the answer leaves out, the tool cannot tell them. build-3 stops, Lead gives it no new task without Bader.
- 16:50: opened by Bader's order of 16:25, items 4 and 5 moved from FAST-2. Owns Common/DataCheck/, four files of CadReader, the scenario scripts and steps/scenarios.md. Started hidden by Lead once this PR is merged.

## At 16:50

- PR 229 and PR 230 merged. PR 228 merged as 389af4c, PR 231 as 1292714, PR 232 as 4c11b72, PR 233 as 12430f0 after Lead merged main into it twice. CadScan.cs went to FAST-4. FAST-2 makes its tests accept the floor lines both ways before FAST-1 removes them. Lead merges main into every open task branch that no lane has checked out, the same hour as each merge. A lane's working branch is merged by its lane, Lead tells it.

## The night of Fri 6 Nov, Bader's order of 21:10

The one test runs tonight, Sunday is the second run.

- 21:25: PR 237 and PR 238 of build-1 merged. PR 242 of build-2, items 6 and 9, fails test_r12 on R707, build-2 fixing. This PR enters R688 and opens FAST-3.
- build-3: done, FAST-4 merged by PR 240. Its hidden session ended after the PR.
- build-2: done once PR 242 merges, then Lead stops its hidden session by its own process id.
- build-1: opens the stage 3 PR and the Q504 PR by 22:55 with what works, stops at 23:00. Item 7, the window shots, moved to FAST-3.
- 23:00: Lead builds main and installs the add-in with Install-SiteAdaptation.ps1. Bader opens Revit once and presses Always Load.
- 23:30: Lead starts revit-test hidden on FAST-3-revit-test. It opens Revit itself. A Load Once box stops it, written here.
- Through the night: Lead reviews its PR, merges on green, reads steps/seam.md every 15 minutes.
- By 08:00 Sat: what the run showed, the fix list by lane, the Saturday order of work, here.

At 23:10:

- Merged tonight: PR 237, 238, 243, 244 of build-1, PR 240 of build-3, PR 242 of build-2, PR 241 of Lead. Main is at 14044dd.
- build-1 stopped: its hidden session ended at 22:40 with its folder clean, task FAST-1 open. Left for Saturday: the listener line on task/FAST-1-build-1-listener, as PR 242 landed at 22:33, after 22:30.
- build-2 done: FAST-2 status done by PR 242, its hidden session ended at 22:34, no process left.
- build-3 done: FAST-4 status done by PR 240, its hidden session ended after the PR.
- 23:00: Lead built main 14044dd and installed the add-in with Install-SiteAdaptation.ps1, SiteAdaptation.AddIn.dll written 23:00:40 under the add-ins root of Revit 2024. INSTALLED.
- Bader opens Revit once and presses Always Load.
- revit-test: its folder is on task/FAST-3-revit-test from main 14044dd, started hidden at 23:30.
- 23:43 to 00:16: revit-test waited, a Teams window "Meeting compact view" stood. Bader at 00:16: it keeps the laptop awake, ignore it. revit-test told at 00:16.
- 00:46: run 1 not started. At 00:44 the PC was 5 minutes idle, then the permission check of the revit-test session (auto mode) refused the launch of the Revit driver under python/window_lock.py, with no reason given. Revit not opened, no input sent. BLOCKED until Bader allows that launch in the revit-test session, or runs the lane where it is allowed. Lead does not launch it for the lane.
- 23:37: Bader's order, the driver presses Always Load on our own Unsigned Add-In box, PR 246.

## Sat 7 Nov 08:00, what run 1 showed

Run 1 of FAST-3, 01:11 to 06:37, three parts of under 2 hours each, on an empty model copy, folder S1, scope neighbourhood 010 (16 plots, not tuned). Record steps/revit-results/FAST-3-revit-test.md, fix list F1 to F17 in steps/seam.md, PR 249.

- Stage 1 works from the pane: Choose project folder, the worker read outside Revit, What the tool read, Confirm what was found, card 1 with its marks. The 37 input files of S1 unchanged by SHA-256.
- Stage 2 builds nothing. No floor, level, villa link or wall was placed in run 1. Build makes the plan and the preview (2615 plots with an outline, 2619 issues), then stops at 4 Check every time.
- The main fault, F16: the read of card 1 writes the data check files only, and step 4 of version 1 needs the outputs of the old step 2 (baseplan, villa blocks, curbs, workbooks, road surfaces). So no Build from card 1 can pass Check, for any neighbourhood. F5, F5b and F5c are its symptoms.
- F4: the first Build of a fresh Revit session stops with R405, no config of the run gave the design values. Seen twice.
- F15: the untuned neighbourhoods of S1 hold no villa type (0 of 16, 0 of 2, 0 of 1), so "not used for tuning" and "real villa links" cannot both hold on S1.
- F8: a new project still starts on the past job's profile, rules name and road surface names. The any project debt of steps/backlog.md.
- Always Load survives a clean close of Revit, it came back only after the forced end at 03:10 (F11 narrowed). A new build of the DLL brings it back, SETUP-sign-2 item 1.
- Run limits: a background tool call ends its whole tree at 2 hours, so a Revit run is relaunched in parts under 110 minutes. A Revit run pauses on every input at the PC.

## Fix list by lane, from steps/seam.md

- build-1, FAST-1, first: F16, the Build chain on the read of card 1. Then F4 with build-2 if the fix is in Common, F13 and F6 (Build reruns Read data and Review data), F12 (a stop says why in a box), F1 to F3 (card 1 runs step 1, takes the baseplan of the folder, holds Review data), F7 (the setup lost when the model is not saved), F9 and F17 (small words and How it works of version 1).
- build-2, FAST-2: F10, the parameter sheet does not reach the data check request (R685 sends it, the check still says Missing). F4 if DesignValues.cs:125-135 is the place.
- build-3: no item of run 1 on its files.
- Lead: F8, a new project with no past job value, a task. F14, shots for the repo with the coordinates line blank, in the revit rules. F15 for Bader below.
- revit-test: run 2 on Sun 8 Nov once F16 and F4 are on main, from a fresh install.

## Saturday order of work, CHOSEN by Lead

1. 08:30 Lead writes FAST-5-build-1 (F16, F4, F13, F6, F12, then F1 to F3, F7, F9, F17) and FAST-6-build-2 (F10, the Common side of F16 and F4), the files split by exact path as on 6 Nov, and starts both lanes hidden.
2. Lead writes the F8 task for build-3, the new project starts from the any project defaults, no past job value.
3. Each PR one review pass, merge on green, main into every open branch the same hour.
4. When F16 and F4 are on main: Lead builds and installs, revit-test runs a short check of stage 2 on neighbourhood 010, Build to the end with no stop at Check.
5. Sun 8 Nov: run 2, the full run of FAST-3 from an empty model.

## For Bader

- F15: to see villa links placed, name a tuned neighbourhood for run 2, marked tuned in the record, or a folder with a typology column for an untuned one. Lead's pick if no answer by 10:00: 01A, marked tuned.

## revit-test, FAST-3-revit-test, the one test on Sun 8 Nov

- Waiting: opens when FAST-1 and FAST-2 are merged.

## Asks between lanes

steps/seam.md, read by Lead every 30 minutes.

## Questions Lead closed with its recommended answer, Bader's order of 6 Nov

Bader's order of 6 Nov: a new question that blocks a lane takes Lead's recommended answer, marked CHOSEN, recorded in steps/questions-closed.md, and listed here for Bader.

- Q394 CHOSEN: on a new project Apply saves a change of the edited Excel as a new entry, the Excel as its model, no key. build-2, PR 235.
- Q149 CHOSEN: the outer face of a boundary wall on the plot limit, the 200 mm of M14 inside the plot. Q507 stays open. build-2, PR 235.
- Q570 CHOSEN: the listener floor goes in two steps, FAST-2 drops RevitListenerSetting.Refusal, then FAST-1 takes SITEADAPT_LISTENER_MIN_FREE_GB out of Start-RevitListener.ps1. StopBelowGB of Run-Parallel.ps1 stays, it is a stop and no start floor.
- Q560, Plot on floors, Bader answered A himself.
