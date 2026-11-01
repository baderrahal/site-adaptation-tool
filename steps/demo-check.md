# Demo check, 1 Nov 2026, 10:16 to 10:34

## Go or no-go

NO-GO for the live run from SA-demo-start.rvt. Show C:\SA-demo\SA-demo-done.rvt. The hand run of the presenter path was not done: Lane C's fix merged at 10:16, and the first Revit start lost the add-in on its security dialog. No step 1 to 9 was clicked by hand after the fix, with or without Dry run, so none is checked.

## What was checked

| Step | Result | What Bader should do or say |
|---|---|---|
| Install | OK. main 5115325 (Lane C fix R444 in) built and installed per user at 10:16, version 1.0.0+5115325 | nothing |
| Start Revit | Problem. The new DLL brings back "Security - Unsigned Add-In". Its default button is Do Not Load (journal 0187), so Enter or a stray key leaves no Site Adaptation tab and no pane | Click Always Load with the mouse. If the tab is missing, close Revit and start it again |
| Second start | OK. Site Adaptation started (journal 0188, SiteAdaptation.AddIn.App) | nothing |
| New project, 1 to 9 by Next step | UNKNOWN, not run | Show SA-demo-done.rvt. If asked for a live step, run only 1 Set up site and 2 Read data |
| Dry run on and off | UNKNOWN, not run | Keep Dry run first ticked if a step is run live |

## Not done and why

- The hand run needs about 15 minutes per pass and two passes. It could not start before 10:16 and had to merge by 10:40.
- My mouse clicks by UI automation did not reach the security dialog. The add-in loaded on the second start, so whether Always Load is now remembered is UNKNOWN.
- The copies C:\SA-demo\check\start-dryrun.rvt and start-real.rvt were made. Neither was changed or saved. Nothing else in C:\SA-demo changed.
