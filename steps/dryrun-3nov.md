# Dry run, 3 Nov 17:00, NBH 012

**GO. Steps 2 to 9 ran to Run complete on frozen main, no parameter value is wrong, the fallback opens with all rows, three known oddities have a line below. Condition: the kit opens at Run 2 with step 1 done.**

Rehearsed through the real windows from the pane on frozen main b1a27d9, not the test host. Add-in main b1a27d9 is installed. Lane B made the kit, no steps/dryrun-3nov-kit.md exists: C:\SA-demo\SA-dryrun3nov.rvt is SA-demo-start.rvt with New project from ROSHN Makkah (saved), all inputs picked, Prototypes prototypes_2024, the 13 masses ready and not remade, and **step 1 already run**. Picks in the pane stay in memory until a real run writes them, so a kit saved before step 1 opens with step 2 stopping on "needs Villa blocks, the file is not chosen" (RAN, Q413). So the kit opens at Run 2.

## Click order

About 3 minutes of wait in all, plus your talking. The seconds are from one rehearsal by the driver, a person adds to them.

| # | Do | Wait |
|---|---|---|
| 1 | Open C:\SA-demo\SA-dryrun3nov.rvt. On each security box press Load Once | 2 to 3 min, open before you start |
| 2 | Step 1 reads Done. Say: step 1 set the position from the position file, and levels, types and shared parameters from the setup (Q415). Leave Scope on Whole project | 8 s when it ran |
| 3 | Scroll the pane to Input files, show the green rows, Plot fronts (curb lines) is read only | |
| 4 | Press Run 2 Read data, then Apply on the dry run box | 100 s, the picks window at 49 s |
| 5 | In What the tool read press Use the proposed for all, then Use these picks | |
| 6 | Scope: press Neighbourhood, pick 012 | |
| 7 | Press Run 3 Review data. Confirm all, Save review, Close | 9 s |
| 8 | The box goes back to Run 2 Read data. Say: the scope changed, so it checks again, the read is reused. Press it, Apply, Close | 4 s |
| 9 | Press Run 3 Review data. Save review, Close | 6 s |
| 10 | Press Run 4 Check, Apply. When 4 Check opens, show it, Close it, then Close the box | 23 s |
| 11 | Press Run 5 Floors and ramps, Apply. Wait for the Revit box, say the line below, press OK | 17 s to the box, 4 s after OK |
| 12 | Press Run 6 Levels, Apply | 5 s |
| 13 | Press Run 7 Place villas, Apply | 18 s |
| 14 | Press the 3D house on the top toolbar. Click in the view, type ZF. The neighbourhood shows | |
| 15 | Show parameters: click one villa, open Properties, point to the RRE_ZZ_SP_ values. Drag the edge of Properties wider so RRE_ZZ_SP_RVT-Name reads whole | |
| 16 | Press Run 8 Walls, Apply | 5 s |
| 17 | Press Run 9 Report, Apply. The box reads Run complete, 012_Report.xlsx | 5 s |
| 18 | Press Open folder, open 012_Report.xlsx, go to the sheet Placed | |

Numbers to expect: 1,003 floors, 0 refused, 336 villas, 778 walls, 485 warnings in the box, 688 accepted.

## When the step 5 box shows

Say: "These are overlaps of two neighbouring plots, hairline, under 1.5 mm, plus 42 lines slightly off axis. The tool already took 688 known overlaps of one plot and counted them in the report. I press OK. It is known item Q324." Press OK. Cancel would undo the step.

## What not to do live

- No Ctrl+Z live. Proven after the dry run (R499, RAN on a copy): one undo takes the last press back and its rows, one redo brings them again. The first test here was void, a just opened model has nothing to undo.
- No click anywhere while the Revit box is open. A press waits behind it and fires after OK, a second 5 Floors and ramps opened in the rehearsal. If it does, press Cancel on it.
- Do not press Neighbourhood before step 2. The list is empty until step 2 ends, and step 2 then stops on "no neighbourhood is chosen". If it happens, open the fallback: leaving by Whole project was not tried, a reopen did fill the list.
- Do not stay on Level 1 after the build. It shows nothing but the elevation marks, the floors stand above its view range. Use the 3D view.
- No 2021 prototype folder. No Browse For Folder on Desktop (Q381).
- Do not open the top of the sheet Summary as proof, it has known gaps below.

Close and reopen is safe, Q384 is merged and proven: a new Revit on the finished model shows all nine rows as saved, with Run complete and the report name.

## What will look odd, and what to say

- Shared parameters reads Not chosen with an amber mark, and New project says the sheet is missing. Say nothing, button 1 takes the sheet from config and the parameters are on the villas, as shown.
- Pane rows read 5: 490 issues, 7: 24 issues, 8: 779 issues, Summary says other counts. Say: "Issues here are items listed for review, not failures. One count for pane and report is open, Q307."
- Summary shows [file name].rvt on top, 1 and 3 not run yet, Levels no run of button 6 yet, and Placed holds no wall rows (Q307, Lane A).
- Plot fronts (curb lines) is the row that was Curbs: read only, no curb is built.

## Shared parameters, read in Properties

Selected by Select by ID in the finished model, read off the screen, against the plan and the sheet Placed.

| Parameter | 2291, DT1, mirrored | 2441, VD2, mirrored | 2390, DT1, not mirrored | Against plan and Placed |
|---|---|---|---|---|
| RRE_ZZ_SP_Plot-Number | 2291 | 2441 | 2390 | match |
| RRE_ZZ_SP_Prototype | DT1 | VD2 | DT1 | match |
| RRE_ZZ_SP_Number-of-Steps | 1 | 1 | 1 | match |
| RRE_ZZ_SP_Façade-Color | XX | XX | XX | match |
| RRE_ZZ_SP_Façade-Design-Option | XX | XX | XX | match |
| RRE_ZZ_SP_Prototype-Foundation-Type | DT1-T2 | VD2-T2 | DT1-T1 | match |
| RRE_ZZ_SP_Prototype-HVAC-Schedule-Type | SCHEDULES OF VRF INDOOR & OUTDOOR UNITS-... | same, cut | same, cut | start matches, the end is cut in the palette, not read |
| RRE_ZZ_SP_RVT-Name | 2291 \| DT1 \| 1 \| ... cut | 2441 \| VD2 \| 1 \| XX \| XX, whole | 2390 \| DT1 \| 1 \| ... cut | match, the shape is right on 2441 |
| CASE | F | E | C | match |
| RRE_ZZ_SP_Prototype-FFL | empty | empty | empty | plan holds a value, not written |

Floors of 2291, Plot: generic, yard and ramp all read DT1-2291, as the plan.

Empty, plainly:

- RRE_ZZ_SP_Prototype-FFL on every villa: intended, Bader's rule is never to write it (R465, Q332), Filled by none in the sheet. Owner Lane D. Not a fault.
- On floors, RRE_ZZ_SP_Plot-Number, RRE_ZZ_SP_Prototype and the two Façade rows are empty. The sheet binds them to Floors and says Filled by 7 Place villas, button 7 writes villas only (R465), so this follows the rule and the sheet reads wider than it. Whether the floors should hold them is Q411, owner Lane D. Do not point at these rows live.

No value is wrong. Mirror read from Placed and the plan, not from the model.

## Fallback

If a step fails live, close without saving and open C:\SA-demo\SA-dryrun3nov-done.rvt. All nine rows read Done or their issues, with Run complete, 012_Report.xlsx and Open folder, and the report lies in its own work folder. Then press the 3D house. Rows read in a new Revit at 15:10, Open folder not pressed there.

To run again from step 2: copy Documents\SiteAdaptation\lane_b_check\p3nov_check\kit-step1.rvt over SA-dryrun3nov.rvt. Not run this way.

## Checked

- Ran through the pane, not the test host: 1,003 floors, 485 warnings shown, 688 accepted, 336 villas (no mass remade), Run complete. Walls by the row and report, read once. A driver fault stopped step 5 at its box and cost 12 minutes, not the tool.
- A second pass from the saved kit stopped at step 2, which found Q413, so steps 2 to 9 were run once end to end. The kit was then rebuilt with step 1 run.
- Inputs: same 44 files by SHA-256 at 15:12 (RAN). C:\SA-demo\config and rules refreshed from main before the run, backup in lane_b_check\p3nov_check\backup, the old config lacked the keys of PR 153.
- Not reached in the rehearsal: whether a close asks to save after 3, 4 or 9. Undo: steps/investigations/undo-3nov.md. Clicking a villa in the 3D view: selected here by Select by ID, the click itself not tried, if it misses use Manage, Select by ID.
- File dialogs took 30 s to 60 s late in the run, the machine was also under other load, so no Browse live.
