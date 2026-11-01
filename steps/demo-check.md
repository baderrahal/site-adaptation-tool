# Demo check, by hand, 1 and 2 Nov 2026 (FIX-1NOV-B Task 6)

## Go or no-go

GO for a live run of 1 to 9 on NBH 012 with Dry run off, once the four points below are done.
NO-GO with Dry run on: step 4 refuses and 5 to 9 cannot be reached (Q195).

Do these before a live run:

1. Untick Dry run first before step 1.
2. Reinstall main and refresh C:\SA-demo\config (Q309). This run used a config copy of main 8f5412e and the add-in of 283ec9f, not C:\SA-demo\config, so C:\SA-demo\config as it stands was not run. Main has moved past this run: PR 130 of Lane A (R472, R473) changes how button 2 takes the picks, and that code was not run by hand here.
3. Decide on the stray export in C:\SA-demo\inputs (Q308).
4. Say the points in "What Bader should do or say" of pass B, above all press OK on the Revit box at step 5 (Q324). Cancel undoes the step.

Run: real windows by UI automation, every step from the pane's Next step box, on copies under Documents\SiteAdaptation\lane_b_task6, with log, shots and driver there, outside git. Saved project ROSHN Makkah task6. Times come from the driver, so a window with clicks in it holds the script's clicks too.

## Pass B, Dry run off, start-dryrun-off.rvt

| Step | Result | Time | Loader | Pane moved on | What Bader should do or say |
|---|---|---|---|---|---|
| New project | OK once Start from is picked | 17 s | no | yes | Pick Start from first. On Empty project, Save setup closes with no message and the pane stays on Start here |
| 1 Set up site | OK | 5.6 s | yes | yes | Tick Baseplan Current, press Dry run in the window, then Apply. Apply stays grey until then |
| 2 Read data | OK | 98.3 s with the picks | yes | yes | Use the proposed for all, Use these picks. The Neighbourhood list fills only now, so pick 012 here |
| 3 Review data | OK, then the box goes back to Run 2 | 16.8 s | no | to Run 2 | Confirm all, Save review, close by the X. Closing without Save review leaves step 3 Ready with no message |
| 2 and 3 again | OK, the read is reused | 2.3 s and 13.8 s | no | yes | Say the pick of 012 asks for step 2 again, it takes seconds (Q306) |
| 4 Check | OK: 337 plots, 2347 rows, 1 plot follows every rule | 32.9 s | yes | yes | Say the 1 is the strict count of Q118, a plot with any check row or override does not count. The sheet's 1237 rows is older, the R471 joint rows came since, whether they are all of the rise is UNKNOWN |
| 5 Floors and ramps | OK: place 1003. Stops on a Revit box, "Highlighted floors overlap.", 0 errors, 1402 warnings | UNKNOWN, the script missed the box, 2.8 s after OK | yes | yes | Press OK. Cancel undoes the step (Q324) |
| 6 Levels | OK: move 0, at the plan 1003, listed 1 | 4.9 s | no | yes | |
| 7 Place villas | OK: Masses place 336, over the footprint limit 336 | 19.1 s | yes | yes | Say the limit flag is Q277, the masses stand right. Every mass file was cached, mass files to make 0. Made new, each takes 2 to 3 min (Q322) |
| 8 Walls | OK: place 752 | 7.2 s | yes | yes | |
| 9 Report | OK: 012_Report.xlsx and 012_Levels_parsons2024Rev6.xlsx | 3.8 s | no | box empty, no end text | Say the run ends here. Do not open the Summary sheet live before Q307 |

## Pass A, Dry run first ticked, start-dryrun-on.rvt

| Step | Result | Time | Loader | Pane moved on | What Bader should do or say |
|---|---|---|---|---|---|
| Start Revit | Four unsigned add-in boxes at the first open after an install, none at the next open | | | | Click Always Load on each |
| New project, inputs | OK: 3 of 4 files ready, 7 of 8 found | 32 s, 26 s | no | yes | The stop box on the parameter sheet stops nothing |
| 1 Set up site | OK, and Apply writes | about 2 min | no | yes | Say Dry run first means a preview first. The Apply of the dialog still writes |
| Reopen | Step 1 Done kept, step 2 back to Ready before any pick, cause UNKNOWN | | | | Run step 2 again, it reuses the read |
| 2 Read data | OK: dry run dialog, What the tool read, Close, read reused | 76.5 s | yes | yes | |
| 3 Review data | X alone: "Not saved yet", stays Ready. With Save review OK | 16.9 s | no | yes | Save review is needed, demo.md says Close |
| 4 Check | FAILED twice: "the dry run of 4 Check waits for the worker, which the add-in gives no way to yet (R376)" | 2.8 s | no | no | Untick Dry run first (Q195) |
| 5 to 9 | Not reachable, each reads Needs step 4 | | | | |

## Windows and buttons

- Works: pane Export, How it works and its Close, View of New project once a read exists, Confirm all (379 of 379), Save review.
- Not built, and the window says so: Zoom to plot and Export to Excel of Review data.
- No visible effect: Zoom to plot and Export to Excel of Check gave no message and no change in the window, and no export file was found. Whether Zoom moved the view is UNKNOWN.
- Review data and Check have no Close button, only the window X. No window has a Back control.
- Export to Excel of What the tool read works, and it offers C:\SA-demo\inputs as its folder with no refusal. That is how the stray file was written (Q308).
- Not tested: Hide of the loader, OK and Wrong rows, Get template, Save overrides.

## Other findings

- What the tool read shows the Baseplan as Not read though it is read. The label comes from 3 xrefs it does not follow (READ scan.json). Say it is the xrefs.
- Review data shows "not read yet" under Villa type, Cluster and Road, by design until 4 Check has built (READ ReviewDataViewModel.cs). It lists 379 plots against 337 of Check, cause not traced.
- Set up site shows the Revit column equal to CAD while What the project has now shows the origin, seen in pass A.
- Revit quit at 22:53:48 in pass A by a normal application exit, saved with Yes, no error (READ journal). Cause UNKNOWN. It did not repeat in the passes after it.
- The Parsons shutdown notice was not seen in the two passes after the restart.

## R457, the empty Neighbourhood list

- Fixed for a model with a read: 012 and 013 listed at open and after step 2, 012 picked and kept through 3 to 9 (RAN, passes A and B).
- A new model has an empty list until step 2. That is the rule, the list comes from the read.
- The guard that puts a saved code back when the combo clears (2a02c6d) passed its test but was not in the installed build, and the runs did not reach that case in Revit.

## Second checks

- Report against the pane, by reading 012_Report.xlsx (RAN): Placed holds 336 generic, 331 yard, 336 ramp, so 1003 floors, and 336 links. Summary counts: Links 336 Revit and 336 Excel, Floors 1003 Revit and 1003 Excel, Walls 752. 0 mismatches on these.
- Placed holds no wall rows, the 752 is in Summary only (Q307).
- C:\SA-demo by write time since 21:30 on 1 Nov (RAN): 1 file changed, the stray export. Inputs were checked by write time, with no SHA-256 before and after, so the hash rule of the round was not met for this run.
