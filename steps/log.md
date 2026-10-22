# Round log

Newest entry first.

## Round 4, 2026-10-22, Revit links, walls and ramps

Done:
- Branch feat/revit-links-walls-ramps. Changed: dotnet/RevitPrototypeReader, dotnet/SiteAdaptation.Common, the tests, docs/rules.md and README.md. Not touched: dotnet/CadReader, python/excel_reader.py, python/pdf_reader.py
- THIS CANNOT BE PROVEN AGAINST THE REAL FILE. There was no Revit here and I did not open 01101-PSN-IMC-ZZZ-PIM-TU-000001. What the tests prove is the reader logic and the JSON shape, against test doubles I built by hand. The code that talks to the Revit API (new RevitModelSource.cs and the edited RevitExtractor.cs) only compiles and has never run. Bader must run it in Revit to confirm anything about the real model
- Reported by Bader, not seen by me: the earlier add-in run on 01101-PSN-IMC-ZZZ-PIM-TU-000001 (Revit 2024, Zone 02 Neighborhood C) returned only Coordinate Point markers and title blocks. It missed the linked models, the walls and the ramps. No prototypes.json from that run is in the repo
- New in prototypes.json, project documents only. On the document entry, after prototypes: links, walls, ramps and errors. Every existing key keeps its name and place. A family document gets none of the new keys. Host prototype rows serialize exactly as before because mirrored is only set on link instances
- links, one entry per Revit link instance: file, transform (origin in metres, rotationDeg, mirrored, axisX, axisY, axisZ), instances, links, deeperLinks, errors. Instances are the family instances placed in the linked model, same shape as a prototypes row plus mirrored (the instance flip from FamilyInstance.Mirrored). They carry every instance parameter and every type parameter, shared ones included, and the instance value wins on a name clash. Instance points stay in the linked model's own coordinates
- Nested links: a link inside a link is read and sits under links of its parent entry. A link inside that is not read, it is only counted in deeperLinks. A nested transform is relative to the link that holds it
- walls: type name, location (line or arc ends, plus centre and radius for an arc), length, height, every instance parameter. Length is the location curve length. Height is the Unconnected Height parameter and is left out when the wall has none
- ramps: type name, location when Revit gives one, bounding box, every instance parameter. Ramps have no class in the Revit API, so they are found by category Ramps
- Design: Common now holds IModelSource, ModelReader, LinkMath and ParameterBag. RevitModelSource implements IModelSource over a real Document, and the tests implement it with a hand built fake. RevitExtractor.PlaceRow is the old InstanceRow location code moved out unchanged so host rows and link rows share it
- Added beyond the brief, say if you want any removed: axisX, axisY and axisZ (a mirrored transform cannot be rebuilt from rotation alone), deeperLinks and errors (so a skipped link or a bad element is visible and not silent), a bounding box on ramps (a ramp may have no location), parameters sorted by name, and link, wall, ramp and problem counts in the dialog that ends the command
- My reading of mirrored: the transform has the link mirror flag and each link instance has its own flip flag. Say if you meant only one of them
- Tests: 12 new, named R16 or R17. A link with translation and rotation, a mirrored link, the flip of one axis alone, rotation wrap, nesting one level, an unloaded and a failing link, a failure in the links section that keeps walls and ramps, the old keys kept, a wall with 8 documented parameters (and an arc wall), a ramp with 7 documented parameters, and the parameter merge. Mutation check: flipping the mirror test and raising the depth limit each made the matching tests fail, then both were put back
- Gate run locally before the PR: build 0 warnings and 0 errors, .NET 40 passed (28 before), Python 24 passed
- docs/rules.md has R16 and R17. README.md has one line on the new output. C# statement semicolons remain because the language needs them, as R15 allows

Remains:
- The Revit run on the real file, which only Bader can do. Until then none of the new output is confirmed
- Everything under Known bugs

Known bugs:
- UNPROVEN, Revit API behaviour I could not check without Revit: GetTotalTransform on a link (the pages I found suggest it matches GetTransform for a plain link, which I did not confirm), GetLinkDocument on a link inside a link, the file name from the link type file reference, collecting ramps by category, and whether a ramp has a location at all
- If the villa prototypes in the link are groups or in-place families and not loaded family instances, they will not appear in instances. That is the same limit round 2 predicted for the host
- Inside a link only family instances are read. Walls and ramps inside a link are not read. Type only rows (a family type with nothing placed) are not written for links
- Wall height is the Unconnected Height parameter. A wall whose top is attached to a level may show a value that is not its real height
- Instance points in links are in the link's coordinates and nothing converts them to host coordinates. A nested transform is not composed with its parent, so a consumer has to chain them
- Several instances of one link type each get their own entry and each walks the same linked model again, so the file can repeat content
- Output size is unmeasured. All parameters on every instance of a large link can make prototypes.json big, and R3 asks for small output
- A parameter that Revit cannot read throws, and the whole element is skipped and listed under errors. The rest of the parameters of that element are lost with it
- Still open from round 3 and untouched: CAD (one face not closed, 28 plot_open pieces, plot_shape holds every face, arc crossings not noded, 3 xrefs missing, anonymous *U0 blocks, 15 MULTILEADERs not in texts.json), Excel (default sheet name Coordinates, blank headers col_A col_K col_L, thin 4 decimal margin on the 0.205 threshold), PDF (single word labels, rows not rebuilt, reversed margin stamp, garbled key plan, large shapes file)
- RRE-WP-NSP-KEO-ARC-TH1-ZZ-MOD-3100.rvt is still NOT RUN, as in round 2

Next:
- Bader runs the rebuilt add-in on 01101-PSN-IMC-ZZZ-PIM-TU-000001 and sends back prototypes.json, then I check it against the model, link by link
- Fix whatever that run shows, most likely in RevitModelSource
- Decide whether to convert link instance points to host coordinates in the reader, and whether to read walls and ramps inside links
- The open CAD, Excel and PDF items from round 3

Your turn:
1. Rebuild the add-in, reinstall it, and run it again on the real file. This round can only be checked against that run. Build with dotnet build dotnet\SiteAdaptation.sln -c Release, copy dotnet\RevitPrototypeReader\bin\Release\net48\ and SiteAdaptation.addin to %APPDATA%\Autodesk\Revit\Addins\2024\ over the old copy, keep the Assembly line pointing at the full path of RevitPrototypeReader.dll, open Revit 2024, run External Tools, Read Prototypes, choose The open document, and send back Documents\SiteAdaptation\prototypes.json
2. Read the dialog at the end of the run. It now gives the link, wall and ramp counts and says if problems were listed. Tell me the counts you expected from the model, and whether the villa link shows up with the right file name
3. If errors is not empty in the file, send it as it is
4. Do you want link instance points converted to host coordinates in the reader, or left in the link's coordinates with the transform beside them? Recommended: keep both once the transform is confirmed on the real file
5. Are the villa prototypes in the link loaded family instances, or groups or in-place families? Recommended: check in Revit by selecting one and reading the type selector
6. Keep or drop the extras listed in Done (axes, deeperLinks, errors, ramp bounding box, sorted parameters, dialog counts)? Recommended: keep

## Round 3, 2026-10-22, fixes against the real files

Done:
- Branch fix/excel-header-cad-plots-pdf-second-sheet. sample_data/ and sample_output/ are still gitignored and nothing real is committed. This log holds counts, row numbers and distances only, no real coordinates or levels
- Gate run locally before the PR: Python 24 passed, .NET 28 passed
- Excel, 00101-PSN-IMD-XXX-SCH-TU-090000.xlsx, FIXED. python/excel_reader.py now finds the header row as the first row with a cell containing Plot No. and reads only the rows below it. It stops at the first fully empty row, at a row with a cell starting with Total, or at a row with no plot number. The real workbook has an unlabeled total row at sheet row 450 with an empty plot number cell, so the third condition is what stops it there. I added that condition on my own reading of that row, say if you want it removed
- Excel real run, sheet MP_Phase 1_Zone21: exit 0, header row 39, 12 headers (Plot & Unit, Plot No., Unit, FFL, SSL, El @ Entry*, Height**, Site Steps Needed, Steps Needed and col_A, col_K, col_L for blank headers), 410 rows from sheet row 40 to 449. The three note lines, the Villa Prototype counts and the Steps Legend are no longer in the rows. All 4920 cells (410 rows by 12 columns) match the workbook with 0 differences. The first and last plot numbers match rows 40 and 449. Row 450 is not read
- Excel default run without --sheet still fails with Sheet 'Coordinates' not found, exit 3. Not changed, the sheet name is still an argument
- Excel El @ Entry and Height now keep 4 decimals, every other column keeps 3. Test of whether this changes a step decision, using the workbook's own rules read from its formulas (Site Steps Needed switches at Height 0.24, Steps Needed switches at 0.205 and 0.38, SSL is FFL minus 0.075). Results over all 410 rows
- Excel 3 against 4 decimals: Height differs between the 3 and 4 decimal value on 356 of 410 rows, by at most 0.0005. Step results compared with the full precision value the workbook uses: with 3 decimals 2 rows change (sheet rows 189 and 423, both Steps Needed going from 1 to 2), with 4 decimals 0 rows change. Recomputing Height from rounded El @ Entry changes 0 rows at either precision
- Excel sample rows, distance to the nearest threshold (full, 3 decimals, 4 decimals) and the Site Steps Needed / Steps Needed result (full, 3 decimals, 4 decimals): row 189 threshold 0.205, -0.000248, 0.0000, -0.0002, results 1/1, 1/2, 1/1. Row 423 threshold 0.205, -0.000082, 0.0000, -0.0001, results 1/1, 1/2, 1/1. Row 415 threshold 0.205, -0.000876, -0.0010, -0.0009, results 1/1 on all three. Row 225 threshold 0.205, -0.000944, -0.0010, -0.0009, results 1/1 on all three. Row 203 threshold 0.205, -0.001834, -0.0020, -0.0018, results 1/1 on all three. Row 40 threshold 0.205, -0.105323, -0.1050, -0.1053, results 1/1 on all three. Row 41 threshold 0.24, +0.034414, +0.0340, +0.0344, results 2/2 on all three
- CAD, ROSHN Makkah Baseplan.dwg, FIXED for plot geometry. CadReader now reads LINE and ARC entities on MP_PLOT_LINE (1645 lines and 23 arcs) as plot geometry, and still reads polylines and blocks as before. New file dotnet/CadReader/PlotShapes.cs. Method: snap ends within 0.01 units, add a node where two lines cross, split lines at every node that lies on them, drop dangling pieces, then take every bounded face of what is left as one closed shape. Each shape has vertices, area and any arcs as centre and radius. Pieces that bound no face are written as plot_open with their segments
- CAD real run: exit 0, 0.65 s, plots 912 (was 107). Kinds: 777 plot_shape, 87 polyline, 28 plot_open, 20 block. The 87 polylines and 20 blocks are unchanged from round 2. plots.json is 320 KB. Of the shapes 584 have 4 vertices, 19 have arcs, 130 are under 100 square units and 20 are over 10000. Repeating areas look like real plot sizes (51 shapes of one size and 23 of another)
- CAD check, independent: the DXF export from round 2 was read with ezdxf and shapely, nodes were added in the reference, and its faces were found with the shapely polygonizer. Reference 778 faces, mine 777. All 777 of mine match a reference face by area (within 0.5 percent or 0.5 square units), 0 of mine unmatched. Before the line crossing step the count was 757 with 6 unmatched, the 15 real crossings explained the gap. Why the endpoint only method is not enough: of 1352 free line ends on that layer, 1289 sit in the middle of another line, so grouping by shared ends gives only about 62 closed loops
- CAD gate: 6 new tests, named R5, on dummy shapes built in code (rectangle by ends, T junction into 2 shapes, crossing lines, half disc with an arc, dangling line kept as open, other layer ignored)
- PDF, 01101-PSN-IMD-ZZZ-DWG-TU-009003.pdf (new, copied into sample_data), RAN with the reader unchanged. Exit 0, 1 page 2384 x 1684 pt (same as the first sheet), 2985 labels, 24806 shapes (24767 lines, 39 rects)
- PDF layout against 009018: same header of 10 words (Point Plot No., Point Code, Point Unit Type, N, E), same 5 columns and 6 vertical rules per block, same repeating rows of exactly 5 labels. Different: the schedule runs across 4 table blocks side by side (block starts at x 161, 548, 935 and 1322, 387 pt apart) with 150, 150, 150 and 47 rows, 497 data rows in all, where 009018 has 1 block of 82 rows. Every one of the 497 rows reads as whole number, whole number, text, number, number. Plot numbers do not decrease in reading order, and a plot can continue from the foot of one block to the top of the next
- PDF cross check with the workbook: the 497 rows hold 94 distinct plots. All 94 are in the Excel plot table with the same unit type. The 16 plots on 009018 are a different range and are not in that workbook, which is expected
- PDF other 10 sheets in the same folder, read in place with the reader and not copied, structure only: all 12 sheets are 1 page of the same size and all hold 1 to 5 blocks of 5 label rows, 3306 data rows in total, every row of the same type pattern, 0 bands with another label count. The block spacing is 387 pt on most sheets, 327 pt on 009027 and 304 pt on 009011
- PDF garbled text: same as 009018. Key plan area has 199 labels under 1.5 pt wide on both sheets and 6 labels with doubled letters on both. The margin stamp text at the left edge comes out with its letters in reverse order on both sheets. The label count gap (835 against 2985) is the 3 extra table blocks and not extra garble
- Correction to round 2: the two labels I logged as carrying U+FFFD are U+201C and U+201D, proper curly quotes, and the reader output them correctly. That item was my misreading of console output and is not a bug. It is marked withdrawn in the round 2 entry
- docs/rules.md updated for R2 (4 decimals for El @ Entry and Height), R5 (plot_shape and plot_open) and R9 (header row and stop rules)

Remains:
- Revit run on the real file, still needs a person at Revit 2024. Nothing changed since round 2
- Everything under Known bugs

Known bugs:
- ROSHN Makkah Baseplan.dwg, 1 face not closed: the reference finds one face of about 11432 square units that CadReader writes only as open pieces. It touches 2 plot_open pieces, one of which holds a 0.05 unit segment, which is more than the 0.01 snap. Cause not confirmed
- ROSHN Makkah Baseplan.dwg, plot_open holds 28 pieces with 51 segments (1 piece has an arc). These are dangling or unclosed lines on MP_PLOT_LINE and the one face above. They need a look in the drawing
- ROSHN Makkah Baseplan.dwg, plot_shape is every closed face, not only plots. 130 are under 100 square units and 20 are over 10000, so road, park and boundary faces are in there with the plots. Nothing links a shape to a plot number yet, the numbers are in texts.json as MTEXT. A shape that encloses another shape is written as its outer boundary with no hole, so its area includes the inner one
- ROSHN Makkah Baseplan.dwg, crossings between an arc and a line, or two arcs, are not noded. The check above found none that matter on this file, but the code does not cover them
- ROSHN Makkah Baseplan.dwg, STILL OPEN from round 2: three xrefs not found next to the file, 17 of 20 block rows are the anonymous block *U0, 15 MULTILEADER entities are not in texts.json
- 00101-PSN-IMD-XXX-SCH-TU-090000.xlsx, the default sheet name Coordinates still fails on this workbook, run it with --sheet MP_Phase 1_Zone21
- 00101-PSN-IMD-XXX-SCH-TU-090000.xlsx, the 3 decimal rounding of Height would have flipped Steps Needed on 2 plots, which 4 decimals fixes. The margin is thin: the closest row is 0.000082 from the 0.205 threshold and a 4 decimal value flips only inside 0.00005. The reader writes the number and does not apply the threshold
- 00101-PSN-IMD-XXX-SCH-TU-090000.xlsx, headers col_A, col_K and col_L come from blank header cells and hold no data
- PDF, all sheets: labels are still single words and rows are not rebuilt. The reader gives no table structure, so nothing joins a row, a block or a plot that continues into the next block. A person or the next round has to stitch 5 label rows in block order
- PDF, all sheets: margin stamp text at the left edge is reversed, key plan text is garbled. Neither is in the table. The shapes file is 2 to 3 MB per sheet and about 95 percent of it is key plan and logo lines
- RRE-WP-NSP-KEO-ARC-TH1-ZZ-MOD-3100.rvt, NOT RUN, unchanged from round 2

Next:
- Run the Revit command on the real file and compare prototypes.json, as in round 2
- Decide how a plot shape gets its plot number (nearest MTEXT label inside the shape is the obvious first try) and how to drop non plot faces
- Rebuild PDF table rows and stitch the blocks in order, then join to the workbook by plot number and unit type
- Decide what to do with the 28 open pieces

Your turn:
1. Revit still waits for you. Steps are in README.md, send back Documents\SiteAdaptation\prototypes.json
2. Excel: do you want 4 decimals for El @ Entry and Height kept, given that 3 decimals flips 2 plots at the 0.205 threshold and 4 does not? Recommended: yes
3. Excel: is it right that reading stops at a row with no plot number, which is how the unlabeled total at row 450 is caught? Recommended: yes
4. CAD: should plot_shape keep every closed face, or only faces that hold one plot number label? Recommended: keep only faces with one plot number label, once labels are joined
5. PDF: should the reader rebuild table rows (and stitch the blocks) in the next round, or leave that to the matching step? Recommended: rebuild them in the reader, the layout is the same on all 12 sheets
6. The round 2 list of questions about the xrefs and the .rfa is still open. Send the three xref DWGs or the TH1 .rfa when you can

## Round 2, 2026-10-22, readers run on real ROSHN Makkah files

Done:
- Branch test/readers-real-files. sample_data/ and sample_output/ are in .gitignore. The four real files were copied into sample_data/ and none is committed. This log holds counts and cell positions only, no real coordinates, levels or text, because the repo is public
- Machine DESKTOP-5VL7LTJ. The four source files were present in C:\02-Site Adaptation
- Method for the checks: Excel was re-read with a separate openpyxl load and compared cell by cell. PDF was rendered to an image (pypdfium2) and read by eye next to the JSON. DWG was copied, exported to DXF 2018 by the AutoCAD 2026 core console, and read with ezdxf (a different parser from ACadSharp). The original DWG was never opened in AutoCAD
- Excel, 00101-PSN-IMD-XXX-SCH-TU-090000.xlsx, FAILED on the default run. Exit 3, error text: Sheet 'Coordinates' not found. Sheets present: MP_Phase 1_Zone21
- Excel second run, same file, only the existing --sheet argument set to MP_Phase 1_Zone21 (no code change). Exit 0, 12 headers, 439 rows. All 5268 cells of the 439 rows match the workbook (0 differences, floats rounded to 3 decimals as R2 says). 2066 formula cells all had cached values, so none read as empty. Spot rows 40, 41, 448 and 449 were read by hand for Plot No., Unit, FFL, SSL, El @ Entry, Height, Site Steps Needed and Steps Needed and every value matches. The 410 plot rows (sheet rows 40 to 449) agree with the Total Units cell of 410
- PDF, 01101-PSN-IMC-ZZZ-DWG-TU-009018.pdf, RAN. Exit 0, 1 page of 2384 x 1684 pt, 835 labels, 20095 shapes (20056 lines, 39 rects). The page is a coordinates table with columns Point Plot No., Point Code, Point Unit Type, N and E. In the table area there are 82 rows of exactly 5 labels each and a 10 word header. I compared the first 15 and last 12 rows to the render by eye and all match. The other rows were checked by structure only. The grid has 84 distinct horizontal positions (header, 82 rows, bottom edge) and 6 vertical positions, which matches the render
- CAD, ROSHN Makkah Baseplan.dwg, RAN with ACadSharp, exit 0, 0.65 s, 48 layers, 107 plots, 3318 texts. No ezdxf fallback was needed for R6. Matched against the DXF: 48 layers equal the layer table. The 17 layers that hold model space entities (15791 entities) match on entity count and on count per type. The other 31 layers have 0 entities in both. All 3318 texts match as a set of layer, x, y and text (whitespace normalised), nothing extra on either side. 87 polylines match the 87 LWPOLYLINE entities including every vertex and the closed flag. 20 block rows match the 20 INSERT entities and their insert points. Units Meters match INSUNITS 6
- Revit, RRE-WP-NSP-KEO-ARC-TH1-ZZ-MOD-3100.rvt, NOT RUN. See Known bugs and Your turn
- Gate, run locally on this branch: Python 18 passed, .NET 22 passed

Remains:
- Revit run on the real file. It needs a person at Revit 2024
- Every item under Known bugs. No reader logic was changed this round
- Only one of the 12 PDFs in 1.5 Coordinates Schedules was read

Known bugs:
- 00101-PSN-IMD-XXX-SCH-TU-090000.xlsx, FAILED: the default sheet name Coordinates does not exist. The only sheet is MP_Phase 1_Zone21
- 00101-PSN-IMD-XXX-SCH-TU-090000.xlsx, MISMATCH: the header row is wrong. The reader takes the first non-empty row, which is the title in sheet row 2. Headers come out as col_A, then the title text, then col_C to col_L. The real header row is sheet row 39 (Plot & Unit, Plot No., Unit, FFL, SSL, El @ Entry*, Height**, Site Steps Needed, Steps Needed). Because of this the 410 plot rows have no usable keys, so a lookup by FFL or SSL finds nothing
- 00101-PSN-IMD-XXX-SCH-TU-090000.xlsx, MISMATCH: three other blocks sit in the same sheet and come out as rows with the plot rows. They are 3 note lines (rows 4 to 6), the Villa Prototype count table (rows 9 to 24) and the Steps Legend (rows 27 to 34). There is also a total row at 450. Nothing marks where the plot table starts
- 00101-PSN-IMD-XXX-SCH-TU-090000.xlsx, NOTE: El @ Entry and Height are rounded to 3 decimals by R2. A Height such as 0.0997 is written as 0.1. Check this does not change a steps decision before the placement work
- WITHDRAWN in round 3, not a bug: 01101-PSN-IMC-ZZZ-DWG-TU-009018.pdf, two labels in the sheet notes were logged as carrying U+FFFD. They are U+201C and U+201D, correct curly quotes
- 01101-PSN-IMC-ZZZ-DWG-TU-009018.pdf, MISMATCH: the key plan area at about x 2104 and y 715 to 720 holds tiny overlapping glyphs (about 0.7 pt wide). They come out as garbled strings with doubled letters. None of it is legible in the render
- 01101-PSN-IMC-ZZZ-DWG-TU-009018.pdf, KNOWN, now confirmed: labels are single words, so a row of the table is 5 labels and the header Point Plot No. is 3 labels. The reader gives positions only and does not rebuild rows
- 01101-PSN-IMC-ZZZ-DWG-TU-009018.pdf, SIZE: pdf_shapes.json is 2.4 MB for one sheet. Only 918 of the 20056 lines belong to the table grid and those come as 420 horizontal and 498 vertical segments, not one line per rule. The rest are in the key plan, logos and title block
- ROSHN Makkah Baseplan.dwg, MISMATCH with the purpose: plots.json holds no plots. Its 107 rows are 87 polylines (road centrelines, wadi, boundaries, existing) and 20 blocks. Plot outlines are on layer MP_PLOT_LINE as 1645 LINE and 23 ARC entities, which the reader does not list (known from round 1)
- ROSHN Makkah Baseplan.dwg, UNRESOLVED XREFS: the drawing references CEC Building Footprint, Makkah_Road Layout and Makkah Building Footprint. AutoCAD reported all three as not found next to the copy. 31 layers named like xref|layer exist with 0 entities here. The reader reports only one block row per xref and does not follow it
- ROSHN Makkah Baseplan.dwg, 17 of the 20 block rows are the anonymous block *U0 (15 at 0,0,0 and 2 at -5000,0,0) with no attributes, so they identify nothing
- ROSHN Makkah Baseplan.dwg, NOT READ: 15 MULTILEADER entities on layer MP_BOUN_CNTL_PNT are counted in layers.json and absent from texts.json, so their text is not read. Plot labels in texts.json are single strings with a line feed inside, a type name then a four digit number
- RRE-WP-NSP-KEO-ARC-TH1-ZZ-MOD-3100.rvt, NOT RUN. Reasons: the command shows a task dialog and a file picker and has no unattended mode, no SiteAdaptation.addin is installed in %APPDATA%\Autodesk\Revit\Addins\2024, and the .addin Assembly line still says C:\Path\To. I did not install into the Revit config or script the Revit window. Nothing in prototypes.json is confirmed
- RRE-WP-NSP-KEO-ARC-TH1-ZZ-MOD-3100.rvt, FROM THE FILE HEADER ONLY (no Revit): the file is a project .rvt and not a family .rfa, saved by Revit 2021 (build 20200715_1022 x64), workshared, central path on a company server. Revit 2024 will upgrade it on open. The command detaches from central and closes with no save, so R1 should hold, but that is unproven
- RRE-WP-NSP-KEO-ARC-TH1-ZZ-MOD-3100.rvt, UNKNOWN, from reading the code: for a project file the command lists family types and placed family instances only. If the TH1 house is modelled in place and is not a loaded family instance, no row will carry the prototype name, bounding box or insertion point you want to check. This is a prediction and not a result

Next:
- Run the Revit command on the real file (steps below under Your turn), then compare prototypes.json to the model
- Fix round, in this order, once the Revit result is in: Excel header row search and sheet choice, CAD plot lines and xref handling, PDF row rebuild and U+FFFD, Revit as the run shows
- Read two more PDFs from the same folder to see if the table layout holds

Your turn:
1. The repo is PUBLIC, not private as the round 1 entry said. Real files and their values stay out of git. Do you want it made private before any real sample goes in? Recommended: yes
2. Run the Revit add-in. Build is in dotnet\RevitPrototypeReader\bin\Release\net48\. Follow the install steps in README.md, open Revit 2024, run External Tools, Read Prototypes, choose Choose .rfa or .rvt files, pick RRE-WP-NSP-KEO-ARC-TH1-ZZ-MOD-3100.rvt, send back Documents\SiteAdaptation\prototypes.json and say what the Revit window showed for the prototype name, size and origin
3. Is there an .rfa of the TH1 prototype family? Recommended: send it too, because the family branch gives a bounding box and the project branch may not
4. Excel: confirm the plot table is the one to read, and that the reader may find its header row by looking for the row that holds Plot No. Recommended: yes
5. CAD: should plots come from the LINE and ARC entities on MP_PLOT_LINE, or from the three xref files? If xrefs, send the three DWGs
6. I used the first PDF in the folder (009018). Say if a different sheet is the one you care about

## Round 1, 2026-10-22, read-only readers

Done:
- RAN: Python tests, 18 passed, locally and on CI (run 39768241302, Ubuntu, Python 3.12)
- RAN: .NET tests, 22 passed, locally and on CI (windows-latest, .NET Framework 4.8)
- CAD path used: C#, ACadSharp 3.8.0. The ezdxf fallback was not needed
- RAN: ACadSharp opened three real AC1032 DWGs from the AutoCAD 2026 install with no reader errors (Civil Best Practices Guide current.dwg and source.dwg, Civil Tutorials Align-1.dwg). Smoke check only. They are not site data and not in the repo
- Found on that run: ACadSharp PlainText turned a label into LLOT 17l. MTextCleaner now strips the codes, with a test
- READ: pdfplumber 0.11.7, pdfminer.six and openpyxl 3.1.5 are MIT, from the installed package metadata. PyMuPDF is not installed or referenced
- Revit add-in compiles against the Nice3point Revit 2024.3.60 API package. It was not run in Revit
- docs/rules.md holds R1 to R15. Each rule is named by a test, except R6, R10 and R11 which are process rules
- Gate in .github/workflows/test.yml. Zero tests fails the job (pytest exit 5, trx count on .NET)
- R15 reading: C# statement semicolons are syntax and are the one exception. Prose and comments follow the rule

Remains:
- Any run on a real PDF, workbook or Revit file. No sample of those exists in the repo, so each of those readers stops at its dummy test
- The harness for this repo (CLAUDE.md, hooks, steps README and PROGRESS, checker agents). Not built this round
- The week one stress test round, before any placement work

Known bugs:
- UNKNOWN: the Revit command has never run inside Revit. Parameter values, level names and the family bounding box are unproven
- CHOSEN: the default key parameters are Width, Height, Depth, Length, Area, Mark, Type Mark, Description. They are my pick, not a stated list
- Revit: parameters match by English name. Project files list loadable family types only, not system families or groups
- Revit family files: insertion point is the family origin, level is empty, bounding box covers solid forms and nested instances only
- CAD: model space only. Lines, arcs and splines are not listed. Polyline bulges are ignored
- CAD: a dense drawing makes a big plots.json. Align-1.dwg gave 552 KB for 579 polylines
- PDF: labels are single words, so PLOT 12 is two labels. Curves are not read
- Excel: the first non-empty row is taken as the header row. The sheet defaults to Coordinates
- This session started outside the repo, so no hooks were live

Next:
- Run the Revit command on one prototype family in Revit 2024 and read prototypes.json against the family
- Then drop one real file per reader into a sample folder and confirm each reader against it

Your turn:
1. Which parameters count as key? Recommended: send the 8 to 12 names, they go in key-parameters.txt, no code change
2. May a small real DWG, PDF and coordinates workbook go into this private repo for tests? Recommended: yes, one each, added with git add -f. Say no if the client files must stay off GitHub
3. Should PDF labels stay as single words? Recommended: yes until a real PDF shows how plot labels are laid out
4. Build the harness next round, before any new reader work? Recommended: yes
5. Run the add-in. Open Revit 2024, run External Tools, Read Prototypes, pick a prototype .rfa, send back prototypes.json. Steps are in README.md
