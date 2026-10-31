# Demo, 1 Nov 2026, 11:00

Everything sits in C:\SA-demo. The add-in is installed per user from main 326422a, version 1.0.0+326422a. Masses only, no Links.

## Open

1. Start Revit 2024. If it asks about the Site Adaptation add-in, press Always Load.
2. Open C:\SA-demo\SA-demo-start.rvt, an empty project, nothing run. The Site Adaptation tab sits at the right end of the ribbon, past the >> arrows if the screen is narrow.
3. The pane opens on the right. In its first box press Open New project.

## Click order, with the time the test run took on NBH 012

| Step | What to press | What it shows | Time |
|---|---|---|---|
| New project | Start from: ROSHN Makkah, then Check files, then Save setup | the files filled in one click, every row Found, What the tool read | under 1 min |
| 1 Set up site | Run, then Apply | coordinates, levels, types and parameters from CAD | 2 s |
| 2 Read data | Run | reads the drawings and schedules. The first time it scans the drawing, 3 to 5 min, after that reused | 50 s plus the scan |
| 3 Review data | Run, look, Close | what was read, row by row | at once |
| 4 Check | Run | the check table, 337 plots, 1237 rows, rules against the past job | 30 s |
| 5 Floors and ramps | Run, Apply | Generic, yard and ramp floors, 1003 floors | 30 s |
| 6 Levels | Run, Apply | FFL and entry levels, writes the level Excel | 5 s |
| 7 Place villas | Run, Apply | 336 masses, one per plot at its FFL | 9 s |
| 8 Walls | Run, Apply | boundary walls along the plot edges | 4 s |
| 9 Report | Run, Apply | the report Excel of the run | 4 s |

The whole run 1 to 9 took 5.9 min in the test, peak memory 2.4 GB. The two Excel files of that run are in C:\SA-demo\outputs.

## If a step is slow

- Say: the first read scans every drawing once, the next runs reuse it.
- Switch: open C:\SA-demo\SA-demo-done.rvt, NBH 012 after buttons 1 to 9. It shows the masses, floors and walls. Its pane starts the steps again, it is for showing the result.

## If asked about the masses and the footprint

- Open C:\SA-demo\footprint-vs-mass.png: 5 masses, one per kind, inside their footprint blocks.
- Say plainly: every mass stands inside its block, 305 of 336. The check still flags all 336, because the footprint drawing holds plot blocks 22 m deep, 2 duplex or 4 townhouse plots per block, while a house is 10 to 16 m deep. Bader decides how the check measures (Q276).
- The base of each mass is the lowest floor outline of its prototype. BASELINE
