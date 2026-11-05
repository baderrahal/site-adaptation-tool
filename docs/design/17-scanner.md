# 17 The scanner, round 4j-1, Lane D

The rules are R420 to R427 of docs/rules/11-scanner-r420-up.md. This part holds the files, the order of every list and the text of every field, so the C# and Python sides write the same bytes of meaning.

## Entry points

- Worker command scan of CadReader (WorkerReads.Commands). Request roles: config, the project file. scanCad:<n>, scanExcel:<n>, scanPdf:<n>, one per file the user gives, n from 1 in the order given. prototype:<file name>, one per prototype file given. The profile is the one config names. The output folder of the request is the run folder.
- python/scan.py main: --settings config/project.json, --cad-read <scan_cad_<n>.json> per drawing, --excel <file> per workbook, --pdf <file> per PDF, --prototypes <folder>, --out <run folder>. Python reads no DWG, it reads the scan_cad files the worker wrote (as every Python step reads the CadReader outputs).
- CadReader --rename-layers <in> <out.dxf> <map.json>: the renamed proof set, a DXF copy whose layers carry the names of the map, written on copies in the work root only.
- Button 2 Read data does not start the scan in 4j-1. The scanner window of Lane B starts it (steps/waiting-on-others.md, Q246).

## scan_cad_<n>.json, by the worker, R420 and R421

At the decimals of R2 by JsonOut, so both sides score the same numbers.

- source: the file name. path, size, sha256, units.
- layers: one entry per layer, by name in ordinal order: layer, closedOutlines, texts, blocks, segments (lines plus arcs).
- candidates: one per layer with at least one face, then one per pair of layers that both hold scan.pairMinSegments segments or more, the pair in ordinal order, all candidates by their layers joined with " + " in ordinal order: layers (a list), faces, each face ring (a closed list of [x, y], first point not repeated), centroid [x, y] and area.
- texts: every text after the explode: layer, text (MText cleaned as MTextCleaner cleans it), x, y.
- inserts: every insert after the explode: layer, block, x, y.
- lines and arcs per layer for the second method, objects keyed by layer: lines [[x1, y1, x2, y2]], arcs [[cx, cy, r, startDeg, endDeg]].
- notRead: what the explode did not read, a list of source and value: "<file>|xref <name>" with "external reference", "<file>|depth limit" and "<file>|cycle" with the count when above 0.

Inside means the even odd ray test of the point against the ring, a point on an edge counted as the test gives it. Both sides use the same test (PlotLabels.Contains in C#).

## Speed, R428

- The pairs of a file are traced on scan.pairThreads threads and put back in pair order. The worker log holds one line per stage, "<file> | <stage> | <seconds> s", explode, texts and inserts, single layer faces, each pair, pairs done.
- PlotShapes looks for the crossings and T junctions of a piece in the cells along it, half a cell apart with the cells around, the cell the median line length.
- R429: past scan.pairTimeCapS no pair is begun, notTraced of scan_cad lists the pairs left and notRead holds "<file>|layer pairs" with their count and the cap.

## scan.json and scan_python.json, R427

- inputs: per file, in role order: role, name, size, sha256.
- rows, in this order of kind, then by source in ordinal order:
  - cadLayer: source "<file>|<layer>", found "<closedOutlines> closed outlines, <texts> texts, <blocks> blocks", proposedUse empty, score 0, conflicts 0
  - plotOutline: source "<file>|<layers joined by ' + '>", found "<faces> faces", score the plots covered (R421), proposedUse "plot outlines" for the proposed, else "check"
  - plotNumber: source "<file>|<layer>", found "<n> labels in plots", score (R422), proposedUse "plot numbers" or "check"
  - villaBlock: source "<file>|<layer>", found "<n> inserts", score (R423), proposedUse "villa blocks" or "check"
  - villaType: source "<file>|block|<layer>", "<file>|text|<layer>", "<file>|<sheet>|<column header or column letter when the header row is empty>|shift <s>", "<file>|page <p>|shift <s>", found "<distinct codes joined by ', ' in ordinal order>", score the plots given a prototype code (R426), proposedUse "villa type" for the lead at the strong share, else "check". Only sources of score above 0
  - conflicts: on a villaType row the conflicts of field villa type that name the source as sourceA or sourceB, on a plotOutline row those of field number, location or label, on every other row 0
- conflicts, by plot, field, sourceA, sourceB: plot, field (number, location, label or villa type), valueA, sourceA, valueB, sourceB. sourceA is the plot drawing or the lead villa type source.
- checks, by what, source, plot: what (tie, numberTwice, twoCodes, oldNewPair, noPlotDrawing, manyLabels, labelOutside, notRead, noStrongSource), source, plot (or empty), value. noStrongSource: source villaType, value "<lead source> <share at 3 decimals>" then " | <source> <share>" for the best of each later kind. manyLabels: source the file and the proposed outline, value the label numbers of the face joined by ", ". labelOutside: source the file and the text layer, plot the label number, value the label text. notRead: the entries of notRead of the scan_cad files.
- plots of the plot drawing by number as text in ordinal order: plot, label, centroid, villaType, an object of source to code.

A plot in these lists is its CAD label number as text. A centroid is written as "x, y" at 3 decimals. An Excel cell is its text, a number cell with no fraction written with no decimals.

## Second checks, python/scan_check.py

- scan.json against scan_python.json, field by field, counts of equal and differ by list.
- The faces of each candidate against a polygonize of its lines and arcs (shapely), each line snapped to its neighbours at the node tolerance of the trace and faces under its least area left out, the counts per candidate. The face count of a button 2 read of the proposed layers is the third method.
- The plots and villa types against a plot table of button 2 (read_plotTable.json, typologyRev9 and footprintBlock), counts of equal and differ, and the same on the plots of each answer key.
- The inputs by name, size, write time and SHA-256 before and after the run (D19), a change fails the check.

## The data check, round R1 (R600 to R608)

Stage 1 from the raw files of a folder, built on the files above and reading no rules profile.

- Worker command dataCheck of CadReader (DataCheckCommand.Run). Request roles: scanFolder, the folder as a team hands it over, and config. Both may carry an empty SHA-256, no other role or command may. Every file the command finds and opens is added to answer.opened by WorkerContext.OpenFile, marked before the read and again after the command.
- The drawings are read with CadScan.Inventory and withHand, which adds the mirror of each insert to scan_cad. Workbooks and PDF files are read as the scan reads them. The scan result is ScanLogic.Result with the ring of each plot, and every setting comes from the dataCheck block of config/project.json (DataCheckSettings), including the scan values the scan takes from the rules profile.
- data_check.json: needs, one row each of plotOutlines, plotNumbers, groupLevels, villaType, villaBlocks, roadEdges, roadLevels, coordinates, prototypes, rules and sharedParameters with mark and line. values holds the mirror, a weak value. files lists each file with name, kind, status and reason. ties names every tie the scan found and how it was broken or why it was not. stopped and scanChecks (what and count) follow.
- project_profile.json: levels, one row each with level, word (null when none) and source. A level with no word because candidates tie or one word covers too few groups also carries candidates and why.
- plots.json: plots, one row each with number, villaType, mirror, groups (the name of the group at each level, an empty list when the drawing's unit is unknown), source (a tag for each of the four values), missing (the reason for each value that is null), disputed (a villa type another kind of source contradicts) and weak (a value that is read but not strong, the mirror always, an outline with a hole, groups under an assumed unit). A plot whose label holds two numbers has number null, source missing and both numbers in its reason under missing.
- The groups, words and road edge layer are found by shape and distance, see R601, R603 and R604. Scenario runs: scripts/scenarios/run_stage1.py and score_scenarios.py write steps/scenarios.md. The clean folder build is scripts/scenarios/clean_stage1.py.

## What a person confirms, round R2 C1 (R630 to R634)

Three inputs of stage 1 are the project's own and a person confirms them. They are found by what the files hold, never by a name of a sheet or column in code.

- project_profile.json gains codeMap (proposed, confirmed, source, foundIn, direction, unsure, bothOldAndNew, notHeld, tooLarge, candidates), typologyColumn and areaColumn (proposed, confirmed with found and why, source, tie, candidates, and tooLarge for the first) and hands (proposed, proposedAs, evidence, confirmed, notHeld, source). A proposed row carries source computed, a confirmed one person entered.
- Input role projectProfile of dataCheck, written by the window of R2-C1-revit-test: codeMap, a list of old and new, hands, a table of code and left or right, levels, a list of level and word, typologyColumn and areaColumn, each an object of workbook, sheet and column, workbook optional. The typology column is in the window by the decision of the lead on Bader's order, Q496, as the code map alone does not reach a drawing of an older layout.
- The order on a plot: the confirmed column gives the code, the map turns it once, the hand reads the code the plot ended with.
- plots.json gains areaFrom, column or distance, and per plot area, areaM2 and codeRead, the code before the map when it differs. weak names villaType for a code the map leaves as read, two values of a plot listed twice and a code with no prototype file, and the mirror until a hand is confirmed and the block in the plot is of the plot's own type. disputed names villaType where the drawing gives another code than a confirmed column. data_check.json values gains codeMap, typologyColumn, areaColumn and hands, each saying what happened on this run, and proposalSources and skippedFolders.

## Read data on a raw folder, R635

- ReadDataRun.Body is the call of the add-in in both cases. ReadInputs.Raw decides: under rev6 with a past file of the old read missing it gives a plan with the input roles config, scanFolder and projectProfile when the window wrote it, the command dataCheck and no step. Otherwise ReadInputs.Make runs as before.
- ReadInputs.NoPastFiles(configPath, mode, workFolder) is the same decision for what lies before the body in the add-in: the pane files of ReadDataBody.Inputs that the flow stops on, and ScanStep.Wrap, which only the old read needs.
- The window writes the confirmed file to confirmed_profile.json in the work folder of the model (ConfirmedProfile.FileName). A new file is a new read.
- read_plotTable.json of such a read holds baseplanLabel and officialNumber, the plot number, locationPlanNumber null, nbhId, the area of the confirmed column or else the group of level 1, typologyRev9, footprintMirrored, faceAreaM2 and the source, missing, disputed and weak of plots.json. plots.json gains areaM2.
