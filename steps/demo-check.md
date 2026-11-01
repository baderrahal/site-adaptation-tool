# Demo check, by hand, 1 Nov 2026 21:44 to 22:54 (Task 6, in progress)

## Go or no-go

NO-GO for a live run of 1 to 9 until the Neighbourhood list is fixed (item 1 below). Show C:\SA-demo\SA-demo-done.rvt for the result. The hand run reached step 2 of pass A (Dry run on). Steps 3 to 9 and pass B (Dry run off) are not run yet.

Run: main 8f5412e installed, a copy of SA-demo-start.rvt, a config copy of main pointing at C:\SA-demo\inputs, saved project ROSHN Makkah task6. Clicked by UI automation through the real windows, steps from the pane's step box, never the ribbon. Log, shots and driver outside git in Documents\SiteAdaptation\lane_b_task6.

## Pass A, Dry run first ticked

| Step | Result | Time | Loader | Pane moved on | What Bader should do or say |
|---|---|---|---|---|---|
| Start Revit | Problem: four "Security - Unsigned Add-In" dialogs, one per add-in (Prototype reader, Site Adaptation, Placer, test host), default Do Not Load | | | | Click Always Load with the mouse on each of the four |
| New project | OK: 3 of 4 files ready, stop box on the parameter sheet as demo-fill.md says | 32 s | no | yes, to Run 1 Set up site | Save setup, the stop box stops nothing |
| Input files of the pane | OK: 7 of 8 found | 26 s | no | | Browse each file of demo-fill.md table b |
| Scope Neighbourhood 012 | Problem: the Neighbourhood list is empty, the add-in never fills it | | | | Run Whole project, or wait for the fix |
| 1 Set up site | OK: window at 9.5 s, Apply grey until Baseplan Current is ticked. Problem: with Dry run first ticked, Apply still commits | about 2 min | no | yes, to Run 2 Read data | Say step 1 writes the coordinates, Dry run applies from step 2 |
| 2 Read data | OK: "Dry run done, nothing was changed. Reads 19 files, 223.2 MiB." then Apply, the loader "Running outside Revit", What the tool read 18 files, 2,964 plots, 386 conflicts, all 4 values Clear, Use the proposed for all, Use these picks, "22 files written, 0 issues" | about 11 min, the first scan included | yes | yes, to Run 3 Review data | Say the first read scans every drawing once |
| 3 to 9 | Not run yet | | | | |

## Other findings

- What the tool read shows ROSHN Makkah Baseplan.dwg as Not read (48 layers, 3,019 outlines) while the others show Read.
- Set up site shows the Revit column already equal to CAD while What the project has now lists the survey point at the origin.
- No Back control exists in the add-in's windows. View in New project is grey before Read data.
- Revit quit at 22:53:48 after step 2 by an application exit, saved with Yes, no error (journal). Cause UNKNOWN, by hand or the IT notice.
- A Parsons shutdown notice comes every 30 minutes and must be postponed by Bader.
