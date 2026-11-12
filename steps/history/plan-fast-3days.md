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

## Sat 7 Nov 22:25, the intake and short check 2

- scripts/new-project-intake.ps1 tested on S1: 23 min, the raw read 10.9 min and the read with the proposed columns confirmed 12 min, the 37 files of S1 unchanged by SHA-256. It names the 7 tuned neighbourhoods with their villa types, 010 16 plots with none, as run 1. Level 1 takes the name of the area column when the read gives none, CHOSEN by Lead. The cover of each road surface stays UNKNOWN in the intake: the proxy of build-1 does not tell neighbourhoods apart, only a plan of 4 Check does (steps/seam.md, 22:00). After FAST-9 the second read should drop out.
- Q600 corrected by build-1: all 269 plots of 01A get an ffl from two road surfaces, only the toposolid needs the two joined. Short check 2 stays on 01A. Q600 for Bader, recommended: a next task joins the surfaces the front corners took, R485 kept.
- 22:20 main a99ea93 built and installed, F25, F26, F27 in. revit-test started on short check 2 of 01A.
- Lanes: build-2 on FAST-9, build-3 on FAST-10. build-1 stopped, FAST-8 open for Q600 and the Revit check.

## Sun 8 Nov 16:15, the end of the runs

- PR 288 demo-live merged. PR 289 the phase 3 record merged 16:05, main c6f8f9e.
- Phase 3 reached card 1 only: read 189 s, Confirm 17 s, a second full read of 216 s as one road drawing was open in Civil 3D. Stopped 15:38, the PC in use. No plot built.
- demo-live part 1 now says card 1 only, part 2 01A as rehearsed. Faults F35 to F42 wait for after the demo.

## Sun 8 Nov 15:10, the 01A rehearsal and the phase 3 run

- PR 286 the form merged 12:30, every A taken. PR 287 the demo run record merged 15:02, main 5e57f4f.
- 01A on demo-S1: card 1 16.9 min, not met, the read 454 s, Confirm 27 s, reuse 3.1 s, Build 4.2 min, step 7 96 s, reopen 70 s with no box. 263 of 278 plots built. Faults F35 to F39 in steps/seam.md, worked around in the run, no fix before the demo.
- Phase 3: Set up site took no baseplan (F40), found no outline in it (F41), refused 0, 0, 0 (F42). Lead typed 500000, 0, 0, not the site. Applied 15:04, the read runs, hard stop 15:45.
- steps/demo-live.md rewritten for 17:00 with the measured times. Next: the phase 3 times into it after its PR, then the reply to Bader.

## Sun 8 Nov 12:25, Bader's order of 11:20, the new project Phase 3

- Merged since 07:00: PR 279 run 2 record, 280 tasks, 281 FAST-14, 282 and 285 shared label records, 283 FAST-13, 284 FAST-15. Main 57cde47 installed at 10:09.
- Item 5 of 06:45, the timed run of 01A on demo-S1, runs since 10:20 in Revit. It waited for the PC to go idle until about 11:30. The one read took 454 s, the Confirm 27 s in the worker with no second read.
- The new project is C:\02-Site Adaptation\Phase 3\Input, 26 files. Files were still being copied into it until 11:48, the intake waited for 8 quiet minutes. Intake 8.7 min outside Revit, folder unchanged by SHA-256.
- Found: plot outlines strong, 3198 plots, 12 areas by Neighbourhood_ID, 10 villa files of new codes, the villa type column holds the old codes, a code map of 10 pairs types 1878 plots. No Land XML, so road levels missing and no plot gets a floor level.
- steps/bader-form-8nov.md filled, 5 items. Item 3 blocks a build. Every A at 12:30 with no answer.
- Next: when item 5 frees Revit, revit-test runs Phase 3 by the form, card 1 one read, Confirm with the code map, Scope 3C, Build to where it stops, cap 90 min, by 15:30. demo-live.md by 16:00.

## Sun 8 Nov 07:00, Bader's order of 06:45, fast enough for the room, demo at 17:00

Run 2 of 01A, started 03:20 on install 1b4f287: 263 of 278 plots, 526 floors and ramps, 1 road toposolid, 526 levels, 263 villa links, 582 walls, Report in 23 s. About 1 h 40 min, card 1 55 min, card 2 45 min. PR 279.

- 0. PR 279, the run 2 record, merges first.
- 1. FAST-13-build-3: villa files in Revit 2024, step 7 under 5 min, the reopen with no upgrade box. R810 to R814.
- 2. FAST-14-build-1: F32 step 8 takes its wall overlap warnings with no box, F34 card 3 counts the issues of the scope. R815 to R819.
- 3. FAST-15-build-2: F22 Confirm through the worker outside Revit, a reused read in seconds from card 1, the read by part and the cuts for one neighbourhood. Read under 8 min, reuse under 10 s. R820 to R829.
- Files split by exact path, no code path in two lists. The shared records index.md, log.md and seam.md are in each list, every PR carries its own log entry.
- The demo folder is <work root>\demo-S1, made by Lead at 07:00: a copy of S1 with the 13 Revit 2024 villa copies in Prototypes_2024. S1 unchanged, 37 files by SHA-256.
- 5. When 1 to 3 are on main: install, revit-test runs 01A on demo-S1 from an empty model copy, card 1 with one read, Build to its end, save as the demo model, reopen once. Pass: card 1 under 15 min, Build under 20 min, reopen under 5 min with no box.
- 6. The read of demo-S1 and of the new project folder runs once before 16:00.
- 7. New project: the intake first, then the form, FORM READY. A build of it only if Bader answers before 14:00.
- 8. steps/demo-live.md rewritten by 13:00 with measured times.

## Sun 8 Nov 00:45, short check 2 result, FAST-11 and FAST-12

- Short check 2 of 01A, PR 275: the Build of card 2 ran its 8 steps to the end with no stop at Check. 124 floors and ramps on 62 plots, the road toposolid, 124 levels, 62 villa links, 178 walls. But 62 of 278 plots, F29. New faults F30 and F31.
- PRs 272, 273 and 274 merged. Main holds F22 and F8, not yet installed.
- FAST-11-build-1: F29, a plot with no hand or no parameter source is still built, tagged missing, and card 2 counts what is left out. F30, card 3 counts the plots of the scope. R800 to R804.
- FAST-12-build-3: F31, step 7 closes the upgrade box of an older Revit prototype and says it upgrades. R805 to R809.
- Both may use Revit before 06:00 under the window lock. Run 2 at 06:00 on 01A as planned, on the install of main at that hour.

## Sat 7 Nov 23:50, run 2 moves, PR 273 widened

- Short check 2 of 01A on install a99ea93 did not end by 23:00, so run 2 does not start at 23:30. It moves to Sun 06:00, the full FAST-3 on 01A, said as tuned. At 23:22 the Build had passed Check on 278 plots and went on to Floors.
- PR 273 of FAST-10 failed only the folder rules check on 7 paths outside its allowed list. Lead read the change of DesignBasis.cs and BuildSteps.cs: a new project names no road surface, so the build takes the pick of Set up. Lead added the 7 paths to the task.
- PR 272 of FAST-9 failed only the caller check, 3 new functions of DataCheckApply.cs not under Callers. build-2 told at 23:40.
- Open risk for Sunday: a new project names no schedule, so the plot table comes from the folder by the read and Confirm alone. No test runs that on a real folder, the Sunday run of the new project is its first proof.
- R791 of PR 272 runs the apply in Revit: on S1 a 667 MiB gathered file, 106 to 126 s, 11 GB peak. CHOSEN by Lead at 00:00: kept for Sunday, the PC has 64 GB and 27.7 GB was free with Revit at 8.5 GB, 2 minutes beats a second read of 15. The worker way stays in steps/seam.md if the Sunday run runs short of memory.
- By R639 a read that is not whole is never reused, so on a folder with a drawing refused for memory every press of 2 reads again. Watched on the Sunday run, the time of each press of 2 goes in its record.
- When 272 and 273 are on main and Revit is free: Lead builds and installs main before the 06:00 run.

## Sat 7 Nov 21:40, the new project path

Bader's order of 21:06: Bader gets a real new project on Sun 8 Nov, files the tool has never seen, the demo is at 17:00. FAST-8 and short check 2 keep first place.

- FAST-9-build-2: F22, Confirm applies the confirmed villa type and area columns to the read just made, one read before Scope names the neighbourhoods. R790 to R794.
- FAST-10-build-3: F8, a project made with Empty project shows no past job value. R795 to R799. The test window of Revit for build-3 comes after short check 2.
- Lead: scripts/new-project-intake.ps1, the stage 1 read by the worker of the installed add-in outside Revit, into <work root>\intake\intake.md. Tested on S1 tonight.
- steps/bader-form-8nov.md, the template of at most 5 items, filled from intake.md.
- Sun 8 Nov, when Bader names the folder: the intake, the form, FORM READY. After his answers, revit-test builds the picked neighbourhood from an empty model copy, Build to the end, under 110 minutes. Then steps/demo-live.md part 1, said plainly as a new project.
- Q600 of build-1, 21:00: no road surface of S1 covers 01A, so 01A plots get no ffl and steps 6 to 8 skip them. Lead asked build-1 for the cover of every neighbourhood, the answer decides the neighbourhood of short check 2.

## Sat 7 Nov 19:10, the short check failed, run 2 waits

The short check of 01A on the install of 89d1c69, PR 265 of revit-test: the Build passed Check for the first time, F16 fixed, then looped on Check 5 times and was cancelled, F25. Floors and ramps placed only by Run 5 of Advanced: 538 floors on 269 plots, 269 generic and 269 ramp, 0 yard, no road toposolid, F27. Levels, villa links and walls 0. F24 needed a work around, its fix is PR 264 on main. Check read the plots file of the past job data book, F26.

The faults, given the same hour:
- build-3, FAST-7 item 10: F25, the Build goes on after Check committed, the Check window gets a Close. First PR 5, F6 and F13.
- build-1, FAST-8-build-1: F26, Check reads no file of the past job, and F27, Floors and ramps takes the surface that covers the scope and the overlap warning as known. F28 if time is left.

Run 2 waits for a second short check: when F25, F26 and F27 are on main, Lead builds and installs, revit-test runs the short check of 01A again. Pass by 23:00: run 2 at 23:30 on 01A. Else run 2 moves to Sun 8 Nov 06:00 and the 010 run on S1-new stays by 12:00.

## Sat 7 Nov 17:05, IT maintenance at 19:30, how to start again

Bader, 16:50: IT maintenance at 19:30 may shut the PC down without warning. If nothing happens after 19:30, work goes on.

Before 19:15: revit-test ends its Revit part by 19:10, saves the copy, closes Revit with File, Exit and pushes task/FAST-3-revit-test-short with its record. build-3 pushes task/FAST-7-build-3-chain with steps/heads-up/build-3.md. No Revit part starts after 19:00.

Where it stands at 17:05:
- Main 1ef4ab7. The add-in installed is 89d1c69 of 14:59, without PR 260 and the F24 fix.
- Short check of 01A, revit-test: part 1 reached step 1 Done on card 1, then F24 stopped Choose project folder. Part 2 tries the work around Lead gave: the folder first, then step 1, then Build. Nothing built yet. The driver paused from 15:56 on the full Teams view of the keep awake meeting, now exempt as the revit rules say.
- build-3, FAST-7: item 8, F24 in PaneHost.cs with the held card 1 fix, then item 9, F6 and F13.
- FAST-5 and FAST-6 open until the short check sees the Build to the end and the parameter sheet strong. build-1 and build-2 stopped.
- S1-new ready for 010. steps/bader-form-7nov.md item 1, A by default at 11:00 Sun.

After a shut down, Lead in the main folder, in this order:
1. git fetch, gh pr list. Merge a green PR, merge main into a red one.
2. Start build-3 again: .venv\Scripts\python scripts\lanes\run_lane.py build-3 <prompt> --model opus --effort high --resume FAST-7-build-3, the prompt says go on from steps/heads-up/build-3.md.
3. When the F24 PR of build-3 is on main and Revit is closed: build and install main with Install-SiteAdaptation.ps1, nothing pressed.
4. Start revit-test again on the short check: run_lane.py revit-test <prompt> --model opus --effort high --resume FAST-3-short, a fresh empty copy of the model, 01A, Choose project folder, Build to the end.
5. Pass: run 2 at 23:30, the full FAST-3 on 01A. Fail: the fault to steps/seam.md and to the lane that holds the file, the same hour.
6. Sun 8 Nov by 12:00 the run of 010 on S1-new, by 13:00 steps/demo-live.md in two parts, demo 17:00.

## Sat 7 Nov 16:15, S1-new for 010

The scan found no untuned neighbourhood that S1 can build with villa links from real data: 01B holds 105 plots with a villa type in PH_1-DLUS, but the S1 baseplan draws 2 of them (steps/investigations/roshen-mecca-scan.md). So the plan of 14:40 stands: the demo folder S1-new in the work root, a copy of S1, gives 11 of the 16 villa plots of 010 their type from the footprint blocks, rows added to the sales sheet of its DLUS workbook, proven by the worker of the installed add-in outside Revit, S1 unchanged by SHA-256. The 5 left and the clash with PH_1-DLUS are item 1 of steps/bader-form-7nov.md. revit-test runs 010 on S1-new on Sun 8 Nov by 12:00, the text is in FAST-3-revit-test.md.

## Sat 7 Nov 15:00, the demo moves to Sun 8 Nov 17:00 Riyadh

Bader's orders of 14:40 and 14:50: the managers see the tool on Sun 8 Nov at 17:00, on a new project, a neighbourhood not used for tuning.

Merged since 10:05, each on green: PR 251 the Saturday lanes, 252 the stop box F12, 253 and 255 file lists of FAST-7, 254 FAST-6 part 1 (F4 fixed, F10 half), 257 FAST-7 card 1 (F1, F3, F7, F9, F17, F2 line), 258 run 1b of revit-test (01A from Advanced stops at Check, F16, F18 to F23), 256 FAST-5 F16 at 14:57, 89d1c69.

Run 1b, 10:40 to 14:06 on the add-in of 6 Nov: steps 1 to 3 done on 01A, 4 Check stopped on the old step 2 outputs, F16. Nothing built.

15:05: Lead built main 89d1c69 and installed it with Install-SiteAdaptation.ps1, nothing pressed. revit-test started on the short check: 01A, an empty model copy, Choose project folder, Build to the end. Pass means floors and ramps, levels, villa links and walls placed with no stop at Check, counts in steps/revit-results/FAST-3-revit-test.md. Pass: run 2 at 23:30, the full FAST-3 on 01A. Fail: the fault to steps/seam.md and to build-1 the same hour.

Open: FAST-5 until the short check sees the Build to the end. FAST-6 until the F10 line, now on main by PR 256, is seen in the short check. FAST-7: F2 Baseplan, then F6 and F13 after the short check, when Lead moves PaneHost.cs and BuildChain.cs to it.

F15, the villa type on a new neighbourhood: a read only check of S1 for 010, and a read only scan of C:\10- ROSHEN MECCA, cap 90 minutes, findings in steps/investigations/roshen-mecca-scan.md. If an untuned neighbourhood has 10 or more plots with a villa type, the demo folder S1-new in the work root takes S1 plus that file and revit-test runs that neighbourhood. If none, the 16 cells of 010 are filled from the CAD block names when they exist, else steps/bader-form-7nov.md asks the team to fill them.

Sun 8 Nov: by 12:00 revit-test runs the new neighbourhood on S1-new from an empty model copy, Build to the end. By 13:00 Lead rewrites steps/demo-live.md in two parts, the new neighbourhood then 01A, used for tuning. Then F8, a task for build-3 if under one hour, else said in the room.

## Sat 7 Nov 10:05, Bader's answers and orders

- F15: run 2 on neighbourhood 01A, marked tuned in the record. The demo of Mon 9 Nov uses 01A too, steps/demo-live.md says plainly 01A was used for tuning.
- Three lanes hidden, opus, effort high: FAST-5-build-1 (F16, then F4, nothing else), FAST-6-build-2 (F10 and the Common side of F4), FAST-7-build-3 (the polish F12 first, then F1, F2, F3, F7, F9, F17, then F6 and F13 once FAST-5 is done). Files split by exact path in each task.
- revit-test now, run 1b of FAST-3: 01A, empty model copy, the old steps 1 to 9 from Advanced, faults to steps/seam.md as F18 and up. It ends a part when a builder writes its revit-wanted file in the lanes folder of the work root.
- Builders test in Revit before the PR: build-1 and build-3 may use the test host and the listener on a copy of the 01A model, one at a time by the window lock. The hook guard-revit.ps1 reads .claude/revit-lanes.txt, deleted after 8 Nov with the fast section of CLAUDE.md.
- F8, the new project from any project defaults, is a Lead task after these, not today.
- Then: one review pass per PR, merge on green, main into every open branch the same hour. When FAST-5 and FAST-6 are on main, Lead builds and installs, presses nothing, and starts revit-test for a short check: 01A from an empty model, Build to the end. If it passes, run 2 tonight at 23:30 as the full FAST-3 on 01A. Sun 8 Nov becomes the third run and the demo rehearsal.

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
