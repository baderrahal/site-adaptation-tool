## Round 8, 2026-10-23, ramp levels from the real corners, surface roles, floor count, one error line

Done:
- Branch round/ramp-corner-levels from main at ba97b5e. This session started in C:\Users\bader, outside the repo, so the main guard hook was NOT live. I never committed to main. Main did not move during the round at my last fetch and R47 to R50 were free. An untracked folder Claude outputs/ with a handoff note appeared in the repo during the round, it is not mine and I left it out of the commit
- The new Revit run was read in place (prototypes.json 5.2 MB, 22 Oct 23:12 UTC): 1010 floors (337 ramps, 336 Generic 150mm, 337 yard), 337 ramp floors all with cornersSource shapeEditor and 4 corners, 987 spot elevations in 3 views, 337 error lines, 273 of 338 links not loaded. No floor holds a parameter with slope in its name and all 1010 report no slope. No spot has a value or a target, only elementId, origin and view. The survey offset is not zero in x and y and is zero in z. So the Revit round 7 code ran and the shape editor, the corners and the spots work. Plot 2441 has no Generic 150mm floor in the file, Bader deleted it and will restore it, the reader is not at fault
- Fix 1 (R47, R44): ramp_levels.py reads corners plus the offset (x, y, z), the corner z is the corner level. 337 of 337 ramps used corners, 0 used the sketchBoundary fallback, 0 left out. The old key boundary is not read, a test proves it does not crash. Each corner now holds cornerLevel, roadLevel (Top Surface-02) and difference. road_check.py also read boundary, so I moved its own corner code to corners, nothing else changed in it
- Fix 1d, chat numbers against my code. AGREE: 414 corners covered by Top Surface-02, 314 within 10 mm, 338 within 30 mm (24 more between 10 and 30), 37 up to 100 mm, 31 up to 300 mm, 8 above. AGREE: of 162 ramps with 2 covered neighbouring corners both within 30 mm 132, one 15, none 15. By type: Ramps 0.5% 120 / 12 / 12, Ramps 2% 8 / 3 / 2, Flat Ramps 4 / 0 / 1. AGREE: ramps by covered corners 0 119, 1 39, 2 162, 3 17, 4 0. Second method, a separate script with shapely on all 1348 corners and its own offset code: the same 414, the same bands, the same pairs, the same ramp counts. road_check.py with seed 2310: 7 of 7 surface counts equal, 0 point and 0 face differences, 0 mismatches in 168 corners of 6 ramps
- Fix 1e, spots, AGREE: 983 of 987 spots lie within 5 cm of a ramp corner, 1339 of 1348 corners have a spot, median of spot z minus corner z is 0, range -94 to +89 mm, 859 corners within 1 mm
- Fix 2 (R48): role_proposal.json lists all 7 surfaces. EXISTING_11.5 Sqkm: ground (covers 1348 of 1348, floor above it, 85 percent of its faces invisible). Top Surface-02: road (414 covered, 338 within 30 mm, median absolute difference 2 mm, 10 percent invisible). The other five cover 0 corners: unknown. With Top Surface-02 now road in the config there is 0 warning lines. Config: Top Surface-02 is road, the five others stay design, none confirmed (R43 and the note updated)
- Fix 3 (R49): every plot with a ramp floor has one Generic, one yard and one ramp floor, checked by plot number. Chat expected one finding, plot 2441 with no Generic floor. AGREE for Generic and ramp: 337 plots with a ramp floor, 1 finding, plot 2441. The yard count per plot is NOT computed. The 337 yard floors have no plot number and their outlines overlap the neighbours, so no match by place is safe. Only the total is known: 337 yard floors for 337 plots
- Fix 4 (R50, C#): the 337 lines floor <id>: Plot parameter is missing become one line per type, for example Plot parameter is missing on 337 floors of type yard, first 5 element ids. A floor whose value does not read keeps its own line. Nothing new needs Revit
- Housekeeping: the word shell for R06 is now no plot faces in R29 and in the skip text of full_run.py. PROGRESS.md fixed. Prototype-FFL below the Excel: 298 plots, equal 39, higher 0, checked again on the new file, 294 of the 298 gaps are 0 to 9 mm, 1 is 10 to 19 mm, 3 are 20 to 32 mm. The number 282 of the earlier round was wrong
- Mutation check: offset (plus, position minus shared, z axis dropped) 3 of 3 caught, corners key against sketchBoundary (2 mutations) caught, role threshold (cover 0.5, cover 0.99, near 50 mm, tie rule) 4 of 4 caught. Code put back, hashes equal
- Gate: Python 166 passed (135 before), .NET 109 passed (105 before), build 0 errors

Remains:
- Restore the Generic floor of plot 2441. Yard plot numbers. The 119 ramps with no corner on the road. Placement work

Known bugs:
- Yard floors cannot be tied to a plot, so R49 cannot count the yard per plot
- 119 ramps have no covered corner and 934 corners have no cover. A corner that does not match the road is a design choice, nothing is forced
- Spots carry no value and no target in the real file. The shown value is unread, only the origin works
- UNPROVEN, unchanged: the sketch plane at 150 m, Floor slope parameter and arrow reading on a sloped floor, spots inside links. Nothing in this round adds a Revit call
- Still open from earlier rounds: 12 failing plots, many label faces, the stale names of plots 2589 and 2594

Next:
- Bader restores the Generic floor of plot 2441 and reruns the add-in, then link_summary.py shows 0 findings. Decide how yard floors get a plot

Your turn:
1. Rebuild and rerun, only needed for the one error line. In a terminal in the repo run: dotnet build dotnet\SiteAdaptation.sln -c Release. Close Revit. Copy everything in dotnet\RevitPrototypeReader\bin\Release\net48\ except SiteAdaptation.addin into C:\Users\bader\AppData\Roaming\Autodesk\Revit\Addins\2024\SiteAdaptation\ and overwrite. Keep C:\Users\bader\AppData\Roaming\Autodesk\Revit\Addins\2024\SiteAdaptation_Revit2024.addin, its Assembly line must read C:\Users\bader\AppData\Roaming\Autodesk\Revit\Addins\2024\SiteAdaptation\RevitPrototypeReader.dll (the build copy has the C:\Path\To placeholder). Start Revit 2024, open the site model, Add-Ins, External Tools, Read Prototypes, choose The open document. The output is C:\Users\bader\Documents\SiteAdaptation\prototypes.json. Restore the Generic 150mm floor of plot 2441 first
2. Yard floors have no Plot parameter, so a yard cannot be tied to a plot. Recommended: add the Plot parameter to the 337 yard floors in the model, then R49 checks the yard per plot. The other way is to accept the total 337 for 337
3. Spot elevations carry no value. Recommended: no action now, the origin gives the level and agrees with the corners. Say if you want the shown value too
4. 119 ramps have no corner on Top Surface-02. Recommended: leave them, a design choice is not forced. Look at 3 of them in CAD with me first if you want an edge level for them
5. Plots 2589 and 2594 still carry a stale link Name. Recommended: trust the parameters and fix the Name in the model
6. The older open questions are in steps/history/round-7-revit.md, round-7-road.md and round-6.md
