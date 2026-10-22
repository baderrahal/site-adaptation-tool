# Progress

Live state of the tool. At most 60 lines. The round detail is in steps/log.md and steps/history/.

## State

- Round 8 (ramp levels from the real corners, surface roles, floor count per plot, one error line) done on branch round/ramp-corner-levels. Extraction and checks only, no placement logic.
- The Revit round 7 HAS run in Revit (Bader, 22 Oct 23:12 UTC, 5.2 MB). It showed 1010 floors, 337 ramp floors all read with the shape editor (4 corners each), 987 spot elevations, 337 error lines (all yard floors with no Plot parameter) and no slope parameter on any floor. Plot 2441 has no Generic 150mm floor in it, Bader deleted it by mistake and will restore it.
- Revit round 5 (link names, floors) ran on 22 Oct before: 338 links (289 not loaded), 926 walls, 1011 floors, 0 ramps.
- Top Surface-02 is the finished road for the ramps (Bader decided, role road in config/road_surfaces.json). The other five design surfaces are not confirmed.
- Readers: CAD (C#, ACadSharp), PDF (pdfplumber), Excel (openpyxl), link table, second source check, face reasons, LandXML reader, surface lookup, ramp levels, surface roles, road check and full run script (Python, shapely, numpy).
- Gate: Python 166 passed, .NET 109 passed.
- Real files stay in C:\02-Site Adaptation and C:\Users\bader\Documents\SiteAdaptation and are read in place. Nothing real is in git.
- The full run covered 11 DWG, 12 PDF, 2 schedule workbooks and the masterplan workbook, 26 of 26 ran, then 3 stages. The LandXML file runs as 2 more stages (6 s and 3 s). Outputs are in sample_output/full_run.

## Done

- Round 8: ramp_levels.py reads the corners of the floors with the survey offset (R47). Per corner it holds the corner level, the road level and the difference. 414 corners are covered by Top Surface-02, 338 within 30 mm. Spots: 983 of 987 near a corner, 1339 of 1348 corners with a spot. Role proposal per surface (R48), floor count per plot in link_summary.py (R49, one finding: plot 2441 has no Generic floor), one error line per floor type with no Plot parameter (R50).
- Revit round 7: floors get corners and cornersSource, slope info, plotNumber and unit, sketchBoundary, spotElevations, link rawName with the name parsed from RRE_ZZ_SP_RVT-Name, link contents default none, rules R34 to R39. Prototype-FFL is lower than the Excel on 298 plots, equal on 39, higher on 0 (the earlier number 282 was wrong).
- Round 7: LandXML reader (7 surfaces), lookup with grid index, config/road_surfaces.json, ramp test on 337 ramps, gap bands, second check with shapely and raw bytes, rules R40 to R46.
- Round 1 to 4: four readers, explode, layer filter, plot labels, PDF rows, link table, full run, main guard hook.
- Round 6: plot number shift config (IMC 10, IMD 11), Master Plan skip list, CAD type check dropped, second source check, masterplan workbook survey, face reasons.
- Location check with the shift: 734 of 746 joined plots pass, 0 need another shift.

## Open bugs

- 12 plots fail the location check, unit corners 1.4 to 20 m outside the plot, no shift fits.
- Sheet 009027 plot 2549 code 5 and sheet 009028 plot 2589 code 2 hold bad northing values in the source.
- Baseplan: 17 faces with many labels (15 consecutive runs) and 12 faces with no label are not fixed. The 18 stray labels are road and buffer labels.
- R06_0000 against the baseplan: 334 faces only in the baseplan, 1100 only in R06_0000, 32 matched faces differ in area by more than 1 percent.
- The masterplan workbook has no per plot rows, so the numbering test could not run on it.
- Rough grading has no export yet. The road surface comes from the LandXML export.
- 119 ramps have no covered corner on Top Surface-02 and 934 corners have no cover. A corner that does not match the road is a design choice.
- Yard floors have no Plot parameter, so the floor count cannot check the yard per plot. Spots carry no value or target.
- Still unproven in Revit: the sketch plane at 150 m, the floor slope parameter and arrow on a sloped floor, spots inside links.
- One unclosed face, 28 plot_open pieces, arc crossings not noded. Plots 2589 and 2594 carry a stale link Name.
- The main guard hook only loads in a session that starts inside the repo.

## Bader's turn

1. Restore the Generic 150mm floor of plot 2441, rebuild the add-in and rerun Read Prototypes (steps in the top entry of steps/log.md).
2. Answer the numbered questions in the top entry of steps/log.md, the first is how the yard floors get a plot number.

## Next three steps

1. Run link_summary.py on the new prototypes.json, the floor count should show 0 findings.
2. Decide the yard plot numbers and the rule for the 119 ramps with no covered corner, then the 12 failing plots and the 17 many label faces.
3. Placement work only after the conflicts list and the Revit result are understood.
