# Bader's form, 8 Nov 2026, the new project

Filled by Lead at 12:25 from <work root>\intake\intake.md, written by scripts/new-project-intake.ps1, Bader's order of 8 Nov 11:20. At most 5 items, each with a recommended answer first. Answer by the letter. Counts and names only, no real coordinate or level. With no answer by 12:30, Lead takes every A.

Folder read: C:\02-Site Adaptation\Phase 3\Input, 26 files, read in place, SHA-256 of every file the same before and after. Intake run time: 8.7 min outside Revit, raw read 4.6 min, read with the columns confirmed 4 min. Of the raw read, 265 s gathering the files, of which one Civil 3D model drawing 215 s, the workbook 8.6 s, the PDF 7.4 s, each of the 10 road drawings 0.8 to 4.4 s, the check 7.4 s (part lines of R822 in the worker log).

Blocks a build: item 3. The folder holds no Land XML file, so no plot gets a road level and so no floor level, and steps 5 to 8 build no plot of this folder. With A of item 3 the demo shows this project to the plan preview of card 2.

1. The villa type column, the column that gives each plot its villa type.
   - A. Recommended. Landuse_Level_4 of sheet SEDRA_3-SDF of workbook 00100-PSN-ZZZ-XXX-SCH-TM-000201_R11.2.xlsx, with a code map of 10 pairs, old to new: C10 C1, DP1 D1, DP2 D2, DP4 D4, TH1 T1, TH3 T3, VL1 V1, VL2 V2, VL3 V3, VL4 V4. The column holds the old codes, the 10 villa files of 04-Revit the new ones, paired by the 4 digit number of the file names (C10 and C1 both 1500, DP1 and D1 both 2100, and so on). Checked by the worker on the read of the intake: 1904 plots read from the column, 1878 with a type of a villa file, 26 with C10-A, C10-B or C10-C stay with no villa file. The folder gives no code map, so the person enters it in Confirm what was found.
   - B. The same column with no code map: 0 plots get a type of a villa file, so no villa is placed.
2. The area column, the column that gives each plot its neighbourhood.
   - A. Recommended. Neighbourhood_ID of sheet SEDRA_3-DLUS of the same workbook, 3198 plots, 12 areas. Proposed by the read.
   - B. Neighbourhood_ID of sheet SEDRA_3-Primium Plots, the same 3198 plots and 12 areas.
3. The finished road surface, the one the floor levels come from.
   - A. Recommended for today. Go on with no road surface. The read says road levels Missing, every plot is left out of steps 5 to 8 with "no floor level", the demo shows this project to the plan preview and says so.
   - B. The team saves the finished road surfaces of the 10 road drawings of 05-Roads as Land XML files into a new folder beside Input by 14:00, Lead reads them and revit-test builds the neighbourhood of item 4 by 15:30.
4. The neighbourhood of this project for the demo.
   - A. Recommended. 3C: 197 plots with an outline, 193 with a type of a villa file, the smallest typed area, so a read, a Review and a preview fit the room.
   - B. 3H, the most typed plots: 516 with an outline, 497 with a type of a villa file. Others: 3B 467, 3G 365, 3A 356 of their plots typed.
5. Missing files.
   - A. Recommended. Go on without them, the tool says what it could not do: road levels (no Land XML), the rule set (chosen in the add-in, the manual template), the coordinate system (the drawing names none, the plots sit in drawing space). 392 of 3198 plots hold a villa block, so the hand of the others is missing and tagged.
   - B. The team sends the Land XML of item 3 B and a coordinate system note by 14:00.
