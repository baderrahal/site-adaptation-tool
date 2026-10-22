# Progress

Live state of the tool. At most 60 lines. The round detail is in steps/log.md and steps/history/.

## State

- Round 6 (plot number shift, second source) done on branch fix/plot-shift. Extraction and checks only, no placement logic.
- Revit round 5 (link names, floors) was merged as PR 6 and has not run in Revit.
- Readers: CAD (C#, ACadSharp), PDF (pdfplumber), Excel (openpyxl), link table, second source check, face reasons and full run script (Python, shapely).
- Gate: Python 96 passed, .NET 77 passed.
- Real files stay in C:\02-Site Adaptation and are read in place. Nothing real is in git.
- The full run covered 11 DWG, 12 PDF, 2 schedule workbooks and the masterplan workbook, 26 of 26 ran, then 3 stages. Outputs are in sample_output/full_run.

## Done

- Round 1 to 4: four readers, explode, layer filter, plot labels, PDF rows, link table, full run, main guard hook.
- Round 6: plot number shift config (IMC 10, IMD 11) read by the link table with a search of one lower and higher, Master Plan skip list, CAD type check dropped, second source check on R06_0000, masterplan workbook survey, reasons for the other faces and stray labels.
- Location check with the shift: 734 of 746 joined plots pass, 0 need another shift.

## Open bugs

- 12 plots fail the location check, unit corners 1.4 to 20 m outside the plot, no shift fits.
- Sheet 009027 plot 2549 code 5 and sheet 009028 plot 2589 code 2 hold bad northing values in the source.
- Baseplan: 17 faces with many labels (15 consecutive runs) and 12 faces with no label are not fixed. The 18 stray labels are road and buffer labels.
- R06_0000 against the baseplan: 334 faces only in the baseplan, 1100 only in R06_0000, 32 matched faces differ in area by more than 1 percent.
- The masterplan workbook has no per plot rows, so the numbering test could not run on it.
- Civil 3D road surface and rough grading files hold almost no model space entities.
- One unclosed face, 28 plot_open pieces, arc crossings not noded.
- The main guard hook only loads in a session that starts inside the repo.

## Bader's turn

1. Answer the numbered questions in the top entry of steps/log.md, the first is the 12 failing plots.
2. Rebuild the add-in, run it on the site model with the links loaded if possible and send back prototypes.json.

## Next three steps

1. Decide the 12 failing plots and the 17 many label faces.
2. Run the Revit result through link_summary.py once prototypes.json is back.
3. Placement work only after the conflicts list and the Revit result are understood.
