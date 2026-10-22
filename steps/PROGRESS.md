# Progress

Live state of the tool. At most 60 lines. The round detail is in steps/log.md and steps/history/.

## State

- Round 7 (LandXML road surfaces, ramp level test) done on branch feat/road-levels. Round 6 was merged as PR 7. Extraction and checks only, no placement logic.
- Revit round 5 (link names, floors) was merged as PR 6 and has not run in Revit.
- Readers: CAD (C#, ACadSharp), PDF (pdfplumber), Excel (openpyxl), link table, second source check, face reasons, LandXML reader, surface lookup, ramp levels, road check and full run script (Python, shapely, numpy).
- Gate: Python 127 passed, .NET 77 passed.
- Real files stay in C:\02-Site Adaptation and are read in place. Nothing real is in git.
- The full run covered 11 DWG, 12 PDF, 2 schedule workbooks and the masterplan workbook, 26 of 26 ran, then 3 stages. The LandXML file runs as 2 more stages (6 s and 3 s). Outputs are in sample_output/full_run.

## Done

- Round 7: LandXML reader (7 surfaces, 1260324 points, 2511038 faces, 288402 invisible), lookup with grid index, config/road_surfaces.json, ramp test on 337 ramps (covered corners 0:119, 1:39, 2:162, 3:17, 4:0, only Top Surface-02 covers any), gap bands, second check with shapely and raw bytes, rules R40 to R46.
- Round 1 to 4: four readers, explode, layer filter, plot labels, PDF rows, link table, full run, main guard hook.
- Round 6: plot number shift config (IMC 10, IMD 11) read by the link table with a search of one lower and higher, Master Plan skip list, CAD type check dropped, second source check on R06_0000, masterplan workbook survey, reasons for the other faces and stray labels.
- Location check with the shift: 734 of 746 joined plots pass, 0 need another shift.

## Open bugs

- 12 plots fail the location check, unit corners 1.4 to 20 m outside the plot, no shift fits.
- Sheet 009027 plot 2549 code 5 and sheet 009028 plot 2589 code 2 hold bad northing values in the source.
- Baseplan: 17 faces with many labels (15 consecutive runs) and 12 faces with no label are not fixed. The 18 stray labels are road and buffer labels.
- R06_0000 against the baseplan: 334 faces only in the baseplan, 1100 only in R06_0000, 32 matched faces differ in area by more than 1 percent.
- The masterplan workbook has no per plot rows, so the numbering test could not run on it.
- Civil 3D road surface and rough grading DWG files hold almost no model space entities. The road surface is now read from the LandXML export. Rough grading has no export yet.
- The finished road surface is not confirmed. 119 ramps have no covered corner, 934 corners have no cover. The one plot check against the Revit tag did not reproduce.
- One unclosed face, 28 plot_open pieces, arc crossings not noded.
- The main guard hook only loads in a session that starts inside the repo.

## Bader's turn

1. Answer the numbered questions in the top entry of steps/log.md, the first is the finished road surface.
2. Rebuild the add-in, run it on the site model with the links loaded if possible and send back prototypes.json.

## Next three steps

1. Decide the finished road surface and the rule for ramps with no covered corner, then the 12 failing plots and the 17 many label faces.
2. Run the Revit result through link_summary.py once prototypes.json is back.
3. Placement work only after the conflicts list and the Revit result are understood.
