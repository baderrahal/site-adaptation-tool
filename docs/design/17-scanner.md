# 17 The scanner, round 4j-1, Lane D

The rules are R420 to R427 of docs/rules/11-scanner-r420-up.md. This part holds the files, the order of every list and the text of every field, so the C# and Python sides write the same bytes of meaning.

## Entry points

- Worker command scan of CadReader (WorkerReads.Commands). Request roles: config, the project file. scanCad:<n>, scanExcel:<n>, scanPdf:<n>, one per file the user gives, n from 1 in the order given. prototype:<file name>, one per prototype file given. The profile is the one config names. The output folder of the request is the run folder.
- python/scan.py main: --settings config/project.json, --cad-read <scan_cad_<n>.json> per drawing, --excel <file> per workbook, --pdf <file> per PDF, --prototypes <folder>, --out <run folder>. Python reads no DWG, it reads the scan_cad files the worker wrote (as every Python step reads the CadReader outputs).
- Button 2 Read data does not start the scan in 4j-1. The scanner window of Lane B starts it (steps/waiting-on-others.md, Q243).

## scan_cad_<n>.json, by the worker, R420 and R421

At the decimals of R2 by JsonOut, so both sides score the same numbers.

- source: the file name. path, size, sha256, units.
- layers: one entry per layer, by name in ordinal order: layer, closedOutlines, texts, blocks, segments (lines plus arcs).
- candidates: one per layer with at least one face, then one per pair of layers that both hold scan.pairMinSegments segments or more, the pair in ordinal order, all candidates by their layers joined with " + " in ordinal order: layers (a list), faces, each face ring (a closed list of [x, y], first point not repeated), centroid [x, y] and area.
- texts: every text after the explode: layer, text (MText cleaned as MTextCleaner cleans it), x, y.
- inserts: every insert after the explode: layer, block, x, y.
- lines and arcs per layer for the second method: lines [[x1, y1, x2, y2]], arcs [[cx, cy, r, startDeg, endDeg]].

Inside means the even odd ray test of the point against the ring, a point on an edge counted as the test gives it. Both sides use the same test (PlotLabels.Contains in C#).

## scan.json and scan_python.json, R427

- inputs: per file, in role order: role, name, size, sha256.
- rows, in this order of kind, then by source in ordinal order:
  - cadLayer: source "<file>|<layer>", found "<closedOutlines> closed outlines, <texts> texts, <blocks> blocks", proposedUse empty, score 0, conflicts 0
  - plotOutline: source "<file>|<layers joined by ' + '>", found "<faces> faces", score the plots covered (R421), proposedUse "plot outlines" for the proposed, else "check"
  - plotNumber: source "<file>|<layer>", found "<n> labels in plots", score (R422), proposedUse "plot numbers" or "check"
  - villaBlock: source "<file>|<layer>", found "<n> inserts", score (R423), proposedUse "villa blocks" or "check"
  - villaType: source "<file>|block|<layer>", "<file>|text|<layer>", "<file>|<sheet>|<column header or column letter when the header row is empty>|shift <s>", "<file>|page <p>|shift <s>", found "<distinct codes joined by ', ' in ordinal order>", score the plots with a villa type (R426), proposedUse "villa type" or "check". Only sources of score above 0
  - conflicts: the count of conflict rows that name the source
- conflicts, by plot, field, sourceA, sourceB: plot, field (number, location, label or villa type), valueA, sourceA, valueB, sourceB. sourceA is the plot drawing or the proposed villa type source.
- checks, by what, source, plot: what (tie, numberTwice, twoCodes, oldNewPair, noPlotDrawing), source, plot (or empty), value.
- plots of the plot drawing by number as text in ordinal order: plot, label, centroid, villaType, an object of source to code.

A plot in these lists is its CAD label number as text. A centroid is written as "x, y" at 3 decimals. An Excel cell is its text, a number cell with no fraction written with no decimals.

## Second checks, python/scan_check.py

- scan.json against scan_python.json, field by field, counts of equal and differ by list.
- The faces of each candidate against a polygonize of its lines and arcs (shapely), the counts per candidate.
- The plots and villa types against a plot table of button 2 (read_plotTable.json, typologyRev9 and footprintBlock), counts of equal and differ, and the same on the plots of each answer key.
- The inputs by name, size, write time and SHA-256 before and after the run (D19), a change fails the check.
