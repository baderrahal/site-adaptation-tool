# Progress

Live state of the tool. At most 60 lines. The round detail is in steps/log.md and steps/history/.

## State

- Round 4 in progress on branch feat/explode-join-rows. Goal: extraction only, no placement logic.
- Readers: CAD (C#, ACadSharp), PDF (pdfplumber), Excel (openpyxl) run on the real files. Revit has never run and Bader runs it by hand.
- Gate before this round: Python 24 passed, .NET 28 passed.
- Real files stay in C:\02-Site Adaptation and are read in place. Nothing real is in git.

## Done

- Round 1: four read-only readers and the test gate.
- Round 2: first run on the real files and the list of mismatches.
- Round 3: Excel header row and stop rules, 4 decimals for El @ Entry and Height, CAD plot shapes from MP_PLOT_LINE, PDF layout checked on all 12 sheets.
- Round 4 housekeeping: CLAUDE.md, this file, steps/history/, main guard hook, three merged remote branches deleted.

## Open bugs

- CAD: 1 face of about 11432 square units is not closed and 28 open pieces remain on MP_PLOT_LINE.
- CAD: arc to line and arc to arc crossings are not noded.
- CAD: 3 xrefs of the base plan, anonymous block rows and MULTILEADER text were open before round 4.
- PDF: rows are not rebuilt before round 4, key plan text is garbled, the margin stamp is reversed.
- Excel: the default sheet name Coordinates fails on the real workbook, run it with --sheet MP_Phase 1_Zone21.
- Revit: not run on the real file.
- The hook that guards main is not live in a session that started outside the repo.

## Bader's turn

1. Run the Revit add-in by hand on the TH1 file and send back prototypes.json.
2. The open questions of round 4 are the numbered list in the top entry of steps/log.md.

## Next three steps

1. Finish round 4: explode, label join, PDF rows, link table, full run on the real files.
2. Read the conflicts list of the link table with Bader and decide what each kind means.
3. Placement work only after the conflicts are understood and the Revit prototypes are in.
