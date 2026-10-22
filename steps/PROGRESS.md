# Progress

Live state of the tool. At most 60 lines. The round detail is in steps/log.md and steps/history/.

## State

- Round 4 (CAD, PDF, Excel) done on branch feat/explode-join-rows. Extraction only, no placement logic.
- Revit round 4 (links, walls, ramps) was merged separately as PR 4 and still needs a run by Bader.
- Readers: CAD (C#, ACadSharp), PDF (pdfplumber), Excel (openpyxl), link table and full run script (Python).
- Gate: Python 60 passed, .NET 63 passed.
- Real files stay in C:\02-Site Adaptation and are read in place. Nothing real is in git.
- The full run covered 13 DWG, 12 PDF and 2 workbooks, 27 of 27 ran, outputs in sample_output/full_run.

## Done

- Round 1 to 3: four read-only readers, first run on the real files, Excel header and stop rules, CAD plot shapes.
- Round 4: blocks exploded with a layer filter, plot faces joined to their labels, PDF table rows rebuilt, Excel row numbers, link table with location check and conflicts, full run script, main guard hook, CLAUDE.md.

## Open bugs

- The numbers in the PDF and Excel schedules are 11 (IMD) or 10 (IMC) higher than the baseplan labels. All 746 joined plots fail the location check because of it.
- Baseplan: 29 faces with no or many labels and 18 labels outside every face are not looked at.
- Civil 3D road surface and rough grading files hold almost no model space entities.
- Sheet 009028 plot 2589 code 2 has N with no decimal point in the source.
- Master Plan layer names differ, so no plot faces come from it.
- One unclosed face, 28 plot_open pieces, arc crossings not noded.
- The main guard hook only loads in a session that starts inside the repo.

## Bader's turn

1. Answer the numbered questions in the top entry of steps/log.md, the first is the number shift.
2. Run the Revit add-in by hand and send back prototypes.json.

## Next three steps

1. Settle the numbering and store the shift in a small config that the link step reads.
2. Look at the other faces and stray labels of the baseplan, then rerun the link table.
3. Placement work only after the conflicts list is understood and the Revit result is in.
