# Demo, 1 Nov 2026, 11:00

Which file goes in which field, with the full path: [demo-fill.md](demo-fill.md).

Demo check of 10:34: NO-GO for the live run, show SA-demo-done.rvt: [demo-check.md](demo-check.md).

Everything sits in C:\SA-demo. The add-in is installed per user from main 691e1fc (Lane C SCAN-UI, Lane A 4i, Lane D 4j-1b in), version 1.0.0+691e1fc. The saved project ROSHN Makkah points at the copies. Masses only, no Links.

## Open

1. Start Revit 2024. If it asks about the Site Adaptation add-in, press Always Load.
2. Open C:\SA-demo\SA-demo-start.rvt, an empty project, nothing run. The Site Adaptation tab sits at the right end of the ribbon, past the >> arrows if the screen is narrow.
3. The pane opens on the right. In its first box press Open New project.

## Click order, with the time the test run took on NBH 012

| Step | What to press | What it shows | Time |
|---|---|---|---|
| New project | Start from: ROSHN Makkah, then Browse each file in C:\SA-demo\inputs, Check files, Save setup | config, rules, Masses and the scan picks from the saved project, every file Found. A saved project holds no drawing path (R434, Q296) | 1 to 2 min |
| 1 Set up site | Run, tick the Baseplan Current, then Apply | coordinates, levels, types and parameters from CAD. Apply stays grey until the Baseplan is ticked | 2 s |
| 2 Read data | Run. If What the tool read opens, press Use the proposed for all, then Use these picks | reads the drawings and schedules, every row Clear from the saved picks. The first time it scans the drawings, about 3 min, then reused | 40 s plus the scan |
| 3 Review data | Run, look, Close | what was read, row by row | at once |
| 4 Check | Run | the check table, 337 plots, 1237 rows, rules against the past job | 21 s |
| 5 Floors and ramps | Run, Apply | Generic, yard and ramp floors, 1003 floors | 25 s |
| 6 Levels | Run, Apply | FFL and entry levels, writes the level Excel | 4 s |
| 7 Place villas | Run, Apply | 336 masses, one per plot at its FFL | 7 s |
| 8 Walls | Run, Apply | boundary walls along the plot edges | 3 s |
| 9 Report | Run, Apply | the report Excel of the run | 3 s |

The whole run 1 to 9 took 5.2 min in the last test at 04:08 on 1 Nov, peak memory 2.5 GB. The two Excel files of that run are in C:\SA-demo\outputs.

## If a step is slow

- Say: the first read scans every drawing once, the next runs reuse it.
- Switch: open C:\SA-demo\SA-demo-done.rvt, NBH 012 after buttons 1 to 9. It shows the masses, floors and walls. Its pane starts the steps again, it is for showing the result.

## If asked about the masses and the footprint

- Open C:\SA-demo\footprint-vs-mass.png: 5 masses, one per kind, inside their footprint blocks.
- Say plainly: every mass stands inside its block, 305 of 336. The check still flags all 336, because the footprint drawing holds plot blocks 22 m deep, 2 duplex or 4 townhouse plots per block, while a house is 10 to 16 m deep. Bader decides how the check measures (Q276).
- The base of each mass is the outline of the largest floor slab on the lowest level of its prototype, every corner, never a box, read from the prototype file (13 of 13, the floor ids in steps/log.md). Five prototypes have a rectangular slab, so their mass is a rectangle: DP2, DP3, DP4, VD2, VD4.
- Say plainly: the slab is not the outer face of the walls. On DT3, the most used prototype (68 plots), the walls stand up to 649 mm outside the slab, 304 mm leaving out the entrance recess, more than the 100 mm limit of config (Q279). The four townhouses DT1, TH1, DT3, TH3 lie 600 to 650 mm from their walls. For the other nine the walls do not close into a ring, so no wall outline was found and the mass uses the slab.
- Slides: C:\SA-demo\dt3-outline-vs-mass.png, the DT3 walls over its mass base. C:\SA-demo\dt3-link-vs-mass.png, a real DT3 link placed on plot 2294 over its mass, the same 649 mm, so the placement is exact and the gap is the outline. The model is C:\SA-demo\SA-demo-link-check.rvt.
