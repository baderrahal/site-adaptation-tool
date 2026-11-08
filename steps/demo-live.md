# Live demo, Sun 8 Nov 2026, 17:00 Riyadh

The script for the managers, 40 minutes in the room, in plain words. Every time below was measured today on the add-in of main 57cde47, installed at 10:09, except the times named run 2, which are of 7 Nov. Nothing comes from the past job: no past model, no override list, no past Excel. Records: steps/revit-results/FAST-3-revit-test.md, sections "Result of the demo run" for 01A and phase 3 for the new project. The intake of the new project: steps/bader-form-8nov.md. Written by Lead, Bader's order of 11:20, item 6.

## What was run before the room

Say this first, plainly. The tool reads a whole project folder once, outside Revit. On the 01A folder that read took 454 s, one read, where run 2 of 7 Nov needed two reads of about 12 minutes each. Confirm what was found then applied the confirmed columns in 27 s, with no second read. A second press with nothing changed reuses the read, the box comes in 3.1 s and says "Reuses the read of <time>, nothing changed". Both models of the room were made before it, so nothing in the room waits for a read.

## Before the room, 10 minutes

1. Close every Revit. Close mail, chat and meeting windows on the shared screen.
2. Start Revit once on an empty model copy. If the box Security - Unsigned Add-In comes, press Always Load. Close Revit with File, Exit.
3. Open the demo model of part 2, FAST3-demo-01A.rvt in fast3-revit-test of the work root. It opened in 70 s with no box in the rehearsal. Do not press Build on it, it is built. Opening it sets Scope back to the whole project (F39), set Scope Neighbourhood 01A again in Advanced if card 2 is to name 01A.
4. For part 1, close every road drawing of the Phase 3 folder in Civil 3D. A drawing open there leaves the read not whole, and the next press reads the folder again (F36, 216 s in the phase 3 run). If Bader saved the phase 3 model by hand as FAST3-phase3-demo in the work root, have it open, it holds the read. If not, part 1 runs live on a fresh empty copy, about 7 minutes of tool time.

## Part 1, the new project, 12 minutes

Say: this is the first run of the tool on a second project, the Phase 3 folder, files it had never seen before today. The folder is read in place and never written, the SHA-256 of every file was the same before and after each run.

The phase 3 run in Revit, 8 Nov 15:04 to 15:38, reached card 1 only, it stopped when the PC was in use. Its times are below. Scope, Review data, the preview and the Build were not run on this project, so do not promise them in the room.

1. The setup: Settings, Project setup, Start from New project rules, Rev 06. Nothing of the past job is offered: no road surface, no schedule, no rules name.
2. Set up site: pick the site drawing in Advanced, row Baseplan, ACAD-00103-PSN-ZZZ-ZZZ-PIM-TM-000001-R11.dwg of 03-Civil3D (F40). Use CAD finds no outline there (F41), so type Easting 500000, Northing 0, Angle 0 and say plainly it is not the site, the drawing names no coordinate system (F42). Dry run, then Apply, 7.8 s.
3. Choose project folder, Phase 3\Input, one read: 189 s in the phase 3 run, 26 files.
4. What the tool read, for each need Strong, Usable, Weak or Missing with one line why. Read it out: the plot outlines, 3198 plots in 12 neighbourhoods. The villa files, 10. The villa type Missing until the code map: the schedule holds the old codes, the villa files the new ones. The road levels Missing, the folder holds no Land XML.
5. Confirm what was found: the villa type column Landuse_Level_4 with the 10 pairs of the code map, old to new, and the area column Neighbourhood_ID. 17 s, no second read. By the intake, 1878 of 1904 plots read then get a villa file.
6. If there is time: Advanced, row Prototypes, Browse 04-Revit (F35), Choose project folder once more for the reuse (F36), Scope 3C. By the intake 3C holds 197 plots, 193 typed. Not run in Revit today.
7. What is missing, said plainly: the finished road surfaces as Land XML, so no plot gets a floor level and nothing is built today. The site position, the drawings name none. The villa file of C10-A, C10-B and C10-C, 26 plots. The hand of most plots, 392 of 3198 hold a villa block. When the team sends the Land XML and the site position, the same buttons build it.

## Part 2, 01A, built before the room, 15 minutes

Say: 01A was used for tuning the rules of the past job, so this shows the tool on ground it was tuned on.

1. The demo model, opened before the room. The counts: 526 floors and ramps on 263 plots, 1 road toposolid, 526 levels, 263 villa links of 13 villa files, all 13 the Revit 2024 copies, 582 walls.
2. The times of the rehearsal, against the targets of the order of 06:45:

| What | Target | Today | Run 2 of 7 Nov |
|---|---|---|---|
| Card 1, Set up site to Scope set | under 15 min | 16.9 min, not met, the tool's own work about 8 min | 55 min |
| The read | under 8 min | 454 s, one read | two reads, 734 s and 736 s |
| Confirm | | 27 s, no second read | could not apply in Revit |
| A reused read | under 10 s | 3.1 s, from card 1 | about 4 s, inside the Build only |
| The Build, all steps | under 20 min | 4.2 min | card 2 45 min |
| Step 7 Place villas | under 5 min | 96 s | 28 min |
| Step 8 Walls | | 6.5 s, no box | 7.4 min on a box |
| The reopen | under 5 min, no box | 70 s, no box | 27.5 min, 13 boxes |

3. Card 2 names the plots built and the plots left out with why: "15 of 278 plots are not built: no floor level 11, no outline or front 2, no unit 2. Built with a value missing: 201 plots".
4. Card 3: 867 issues of the scope, the same count as card 2, with Open rules, "13 plots have no road level", and the source bar, where each value came from: computed, rule, person entered, missing.
5. Report writes the Excel file, 23 s in run 2: the plot schedule, floors, walls, issues, rules used, placed, skipped and assumed values. Open it. Update from report reads an edited Excel back and shows each change before it is applied. Report was not run on this copy in the rehearsal.

## Part 3, one live Build

Not shown. The Build of 01A takes 4.2 min, but the model of part 2 is built and must not change, and a new copy needs card 1 first. If there is time, say the times of the table instead.

## What does not work yet

Say these in plain words, they are true today.
- Extra presses after the one read. After Confirm, Review data stays grey until Choose project folder is pressed once more, it then reuses the read in seconds (F36). The Prototypes folder is not filled by the one read, it is picked once in Advanced (F35). The reuse box says one line that is not true of a reuse (F37).
- On a new project, Set up site does not take the site drawing from the folder, the person picks it once in Advanced, row Baseplan (F40). Then Use CAD finds no plot outline in that drawing, where the read finds 3445, so the phase 3 run typed the site position by hand, Easting 500000, Northing 0, Angle 0, the false easting of the zone and not the site (F41, F42).
- Scope goes back to the whole project when a model is opened again (F39).
- Plots not built and why. On 01A, 15 of 278: 11 have no floor level, as they get no road level, 2 have no outline or front, 2 have no villa type.
- Plots built with a value missing, 201 on 01A, the hand among them: where the files give no villa block, the plot is built not mirrored and the hand is tagged missing in the plan and the report.
- No yard floors. The rules of Rev 06 hold the back yard floor false, so step 5 places generic and ramp floors only.
- A new project with no Land XML builds no plot. On Phase 3 the run in Revit reached card 1 only.

All of these are in steps/seam.md for the next build.

## If something stops

Say what card 2 or the box says, word for word. It names the step and why. Do not run anything from the past job to cover it.
