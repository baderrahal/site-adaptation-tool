## Round 6, 2026-10-22, plot number shift, second source, no type check

Done:
- Branch fix/plot-shift. Not touched: dotnet/RevitPrototypeReader and the Revit branch. This session started in C:\Users\bader, outside the repo, so the main guard hook was NOT live. I worked on the branch and never committed to main. Main did not move during the round
- Shift config (R28): config/plot_numbers.json holds IMC 10, IMD 11, the direction in a note, the file name pattern and a search range of 1. The link table joins the official number to the CAD label at number minus shift, runs the same location check at 1 m and tries the shifts one lower and one higher for a plot that fails. New conflict kinds: shift_edge, cad_claimed_twice, cad_unclaimed, no_neighbourhood, neighbourhood_mixed, not_numeric
- Real files: 734 of 746 joined plots pass the location check (IMC 328 of 337, IMD 406 of 409). shiftFound differs from the config for 0 plots, so conflicts.json has no shift_edge. The 3 edge plots at plus 12 from the last run did not show up. No plot gets a better fit from a shift one lower or higher. Widened to 3 lower and higher for the 12 failures, no shift brings any of them within 1 m. The best other shift gains only 1.2 m for plot 1384 and 0.4 m for plot 1401
- Type check (R30): CAD plot type is now the info field cadType. PDF unit type equals Excel unit on all 746
- Skip list (R29): R06 and R06_0001 are skipped with their reasons in full_run_summary.json. Full run: 26 of 26 files ran (11 DWG, 12 PDF, 2 schedule workbooks, the masterplan workbook), the link, second source and face stages ran ok
- Checked decision C against the old outputs. R06_0001 equals R06_0000 (731 layers with equal counts, 121042 of 121042 handles equal). R06 has 45 model space entities but they explode to 140064 pieces and only 1 insert points at the missing xref, so it is not empty after exploding. The skip stays as decided
- Second source (R31), R06_0000 against the baseplan: 3757 faces (1053 repeats removed) against 2991. Matched 2657, only baseplan 334, only R06_0000 1100, 32 matched faces differ in area by more than 1 percent. 696 of the 1100 hold two or more baseplan faces
- Masterplan workbook (R32): 9 sheets, header rows 14 (2 sheets), 3 (5 sheets), 2 (2 sheets), rows 128, 128, 123, 75, 60, 127, 97, 97, 97. No sheet has a plot number column, only Plot Count and No. of plots. It is a land use summary with no per plot rows, so the numbering test cannot run on it
- Other faces and stray labels (R33): 29 faces are 17 many_labels (15 consecutive runs, 1 run plus an outlier, 1 not consecutive) and 12 no_label (1 sliver, 4 small, 7 large). All 18 stray labels are road or buffer labels (12 ROW Internal, 4 ROW External, 2 Buffer Area), numbers 3134 to 3153, 6.5 to 32.5 m from the nearest face. No stray label lies within 30 m of a no_label face
- Second method: shapely on the plots.json faces and the raw PDF points with its own shift search gives 734 ok and 12 bad, shift differs 0, and 0 plots where it disagrees with the link table. Six random plots (2587, 1508, 2480, 1435, 1320, 1192): every PDF row is in the PDF text, every Excel row matches the raw cell for plot and unit, PDF unit equals Excel unit, CAD area equals the link
- Mutation check: search range off made 2 shift tests fail, skip list off made 2 skip tests fail, both put back. shapely 2.2.0 added to requirements.txt
- Gate: Python 96 passed (70 before), .NET 77 passed, build 0 errors

Remains:
- Revit run on the real model, by Bader. Placement work. A look at the 12 failures and the 17 many label faces

Known bugs:
- 12 plots fail the location check with their config shift: 1278, 1384, 1401, 2311, 2312, 2377, 2388, 2544, 2545, 2548, 2549, 2589. Ten have unit corners 1.4 to 20 m outside the plot. In 8 of them most points lie in the config plot, in 2312 and 2544 most points lie in the plot one number lower
- Plot 2549 sheet 009027 block 4 row 129 code 5 has a point millions of metres off, same kind as plot 2589 on sheet 009028. The odd list only catches a missing decimal point, so 2549 is not in it
- Plot 1453 has an Excel row and no PDF rows. 4 PDF plots repeat a point code, 58 plots sit on 2 or more sheets
- Old output folders of R06 and R06_0001 stay in sample_output/full_run from the last run and are not used
- Still open: unclosed face and plot_open pieces, arc crossings not noded, Civil 3D surfaces, Revit not run

Next:
- Decide the 12 failures and the 17 many label faces, then placement only after the Revit result is in

Your turn:
1. Plots 1278, 1384, 1401, 2311, 2312, 2377, 2388, 2544, 2545, 2548 have unit corners 1.4 to 20 m outside the plot. Can unit corners lie outside the plot edge? Recommended: look at 3 of them in CAD with me before the tolerance changes
2. Sheet 009027 plot 2549 code 5 and sheet 009028 plot 2589 code 2 hold bad northing values. Recommended: tell the drawing owner, I keep them as found
3. R06.dwg is not empty after exploding (140064 pieces). Keep the skip? Recommended: yes, R06_0000 stays the only Master Plan drawing
4. 696 of the 1100 faces only in R06_0000 hold two or more baseplan faces, so R06_0000 is less subdivided there. Which is the master for plot faces? Recommended: the baseplan
5. The masterplan workbook has no per plot rows. Recommended: it is a summary, send a per plot Master Plan schedule if one exists
6. The 18 stray labels are road and buffer labels with no face. Recommended: leave them, filter them by the ROW and Buffer words
7. The 15 many label faces hold consecutive plot numbers, so lines between those plots are probably missing in the baseplan. Recommended: send the CAD owner the list in sample_output/full_run/other_faces
8. Revit still waits for you, steps are in steps/history/round-5-revit.md
