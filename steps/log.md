# Round log

Newest entry first. The top entry is one page at most. Older rounds keep a short stub here and the detail lives in steps/history/.

## Round 9, 2026-10-23, floor, ramp and link relations

Done:
- Branch round/floor-link-relations from main at 61bfccc. This session started in C:\Users\bader, outside the repo, so the main guard hook was NOT live. I never committed to main. No round branch was open and nothing was uncommitted. Four old local branches from rounds 1 to 4 are not merged (feat/readers, feat/revit-links-walls-ramps, fix/excel-header-cad-plots-pdf-second-sheet, test/readers-real-files), I left them alone. The untracked folder Claude outputs/ is not mine and stays out of the commit. R51 to R61 were free
- Revit reader, floors (R51 to R53): typeId, levelElevation, topElevationRange from GetTopFaces, bottomElevationRange from the downward faces of the solid, boundingBox, outline loops (an edge on two top faces is dropped, the rest is chained), workset, phaseCreated, designOption, parameterDetails and floorTypes once per type. level and heightOffset already existed. New geometry is in shared coordinates. No existing field changed, a test proves a floor without the new fields keeps exactly its old keys
- Revit reader, links and spots (R54, R55): link elementId, status, loaded, parameterDetails and for loaded links 8 box corners and transformShared. Nothing inside a link is read, a test checks the link state code never opens the linked document. Spot host with category and type. Every failure is one line per reason with the count and the first 5 ids, the element stays in the list. The add-in dialog now shows the file size
- Revit API checks: the build against the Revit 2024.3 assembly has 0 errors, so every member name exists. The XML docs shipped with the package confirm GetEdgesAsCurveLoops, GetTopFaces, the LinkedFileStatus values, DesignOption null in the main model, the built in id INVALID, and that spot references can be unavailable. They say nothing about the space of the bounding box of a link instance
- Size (R56): a made-up model sized like the round 7 file (5.01 MB against the real 5.2 MB) is 12.47 MB with the new fields, so they add 7.46 MB and the estimate for the real file is 12.7 MB. This is an estimate. The real size is only known when Bader runs the add-in, the dialog prints it
- plot_relations.py (R57 to R61) with the CSV and the summary. Beyond the brief I added the slope along the short edges, the overlap area of every yard candidate, and levelElevation. On the real round 7 file in place (old shape, sketch outlines, no new fields, output in the scratch folder) it ran in 1.3 s on 337 plots and 337 schedule rows. Counts: Generic top equals FFL on 336 of 336 and El @ Entry on 0 of 336. Every plot has 2 to 6 yard floors that touch or overlap (2: 33, 3: 85, 4: 81, 5: 37, 6: 101), so none is assigned by touching alone. By the largest overlap with the Generic floor the best candidate is at least twice the next on 336 of 336 plots and 335 yards are the best candidate of exactly one plot. The ramp lies fully inside the Generic outline on 336 of 336 and shares 3 edges with it on 335 of those 336 plots (plot 2441 has no Generic floor). Ramps are 5.5 m deep and 8 to 16 m long. The type name slope equals the slope along the long edges on 26 of 327 ramps and along the short edges on 216 of 327
- Mutation check on plot_relations.py: edge tolerance down to 0.5 mm failed 3 of 35 tests, up to 50 mm failed 3, slope sum without the second long edge failed 4, slope sum as a difference failed 4. Code put back, hashes equal
- Gate: Python 201 passed (166 before), .NET 143 passed (109 before), build 0 errors. New tests: Python 35, .NET 34
- The copy step excludes SiteAdaptation_Revit2024.addin as well, an old copy of it sits in the build folder and would add a second manifest in the subfolder. steps/for-bader-round-9.md has the steps. The round 8 entry moved to steps/history/round-8.md

Remains:
- Bader runs steps/for-bader-round-9.md. Then the report runs on the new file and the numbers above are checked against the top face outlines. The yard rule is Bader's call

Known bugs:
- UNPROVEN in Revit: the bounding box of a link instance (taken to be in host space, the report counts boxes that hold the link origin to show it), GetTopFaces and the outline loops on shape edited slabs, the bottom range from the solid, spot references (they may be unavailable on all 987 spots, then one line says so), LinkedFileStatus on the real links
- The report on an ambiguous yard uses the union of the candidates, so its yard overlap and gap numbers are only a bound until the yard rule is set
- Still open from earlier rounds: plot 2441 has no Generic floor, 119 ramps with no corner on the road, 12 failing plots, many label faces, the stale names of plots 2589 and 2594

Next:
- After the Revit run, read the summary and decide the yard rule and what the ramp slope in the type name means

Your turn:
1. Run steps/for-bader-round-9.md, 11 steps, and give me the File size line and plot_relations_summary.md
2. Yard rule. Recommended: the yard of a plot is the touching yard with the largest overlap with its Generic floor. On the old file that is clear on 336 of 336 plots. The other way is to give the 337 yard floors the Plot parameter in the model
3. Ramp slope. Recommended: no change now. The type name slope matches the slope across the depth (short edges) on 216 of 327 and the long edges follow another gradient. Say which one the type name is meant to describe
4. The older open questions are in steps/history/round-8.md

## Round 8, 2026-10-23, ramp levels from the real corners, surface roles, floor count, one error line

Stub. The full entry is in steps/history/round-8.md.
- Ramp corner levels with the survey offset (R47), role proposal (R48), floor count per plot (R49) and one error line for floors with no Plot parameter (R50) were added and merged as PR 10. Gate then: Python 166 passed, .NET 109 passed
- Open for Bader: the Generic floor of plot 2441, the 119 ramps with no corner on the road, the stale names of plots 2589 and 2594, the numbered questions in steps/history/round-8.md. The yard plot question is answered by the round 9 measurement

## Round 7 Revit, 2026-10-22, ramp corners, spot elevations, plot numbers, link names

Stub. The full entry is in steps/history/round-7-revit.md.
- Floor corners and cornersSource, slope, spot elevations, plot numbers, the link name parse and the link contents default were added and merged as PR 9. The add-in ran in Revit on 22 Oct 23:12 UTC and the round 8 entry says what it showed. Gate then: Python 135 passed, .NET 105 passed
- The questions of that round are answered or moved: Prototype-FFL 298 lower (round 8), the 337 yard lines (R50). Still open: the stale names of plots 2589 and 2594, the sketch plane at 150 m

## Round 7 road, 2026-10-22, LandXML road surfaces and the ramp level test

Stub. The full entry is in steps/history/round-7-road.md.
- LandXML reader, surface lookup, road config, ramp road levels, gap bands and the road check were added and merged as PR 8. 7 surfaces, 337 ramps tested, only Top Surface-02 covers any corner (119 ramps with no covered corner). Gate then: Python 127 passed, .NET 77 passed
- Open for Bader: the finished road surface, the 119 ramps with no covered corner, the plot check, the numbered questions that are in steps/history/round-7-road.md

## Round 6, 2026-10-22, plot number shift, second source, no type check

Stub. The full entry is in steps/history/round-6.md.
- Shift config IMC 10 and IMD 11, Master Plan skip list, second source check, workbook survey and face reasons were added and merged as PR 7. Location check 734 of 746 joined plots pass, 12 fail. Gate then: Python 96 passed, .NET 77 passed
- Open for Bader: the 12 failing plots, the 17 many label faces, the numbered questions that are in steps/history/round-6.md

## Round 5, 2026-10-22, Revit link names, shared parameters and floors

Stub. The full entry is in steps/history/round-5-revit.md.
- Link names, shared parameters, floors and link_summary.py were added and merged as PR 6. The Revit API code has never run in Revit, so nothing new is confirmed until Bader runs it. Gate then: Python 70 passed, .NET 77 passed
- Open for Bader: rebuild and run the add-in, send prototypes.json, check three link names, Prototype-FFL units, foundation slabs, nested links

## Round 4, 2026-10-22, explode, label join, PDF rows, full run

Done:
- Branch feat/explode-join-rows. Revit code untouched (diff against main is empty). Main moved during the round: another session merged PR 4 (Revit links, walls, ramps, also called round 4) and took R16 and R17, so my rules are R18 to R26. Nothing real is in git. This log holds counts only
- Housekeeping: CLAUDE.md, steps/PROGRESS.md, steps/history/ (rounds 2 and 3), 3 merged remote branches deleted, main guard hook. The hook script passed 17 offline cases and pytest, but it is NOT live in this session (it started outside the repo, a dry run of git push to main went through)
- Full run on the real files, in place, outputs in sample_output/full_run: 27 of 27 ran, 1 min 47 s, slowest 26 s, no timeout, no failure. 13 DWG, 12 PDF, 2 workbooks. Not read: 145 DWG under 01_Output, 2 Excel lock files, the Master Plan folder has no .bak but holds a masterplan .xlsx I was not asked to read
- CAD explode: pieces now carry layer, handle, insert path, world coordinates, attributes and extended data. Entities before and after exploding: Building Footprint 4271 to 20458, Master Plan R06 45 to 140064, Phase 1 Road Layout 7049 to 211479, Typical Cross Sections 1548 to 129100, baseplan 15791 to 15771. Second method, AutoCAD DXF export read by ezdxf: before cells match 527 of 527, after cells 1022 of 1026, totals equal in 12 of 13 files. The 4 cells: 3 are ARC and CIRCLE that ezdxf names ELLIPSE (Road Layout), 1 is 2 POLYLINE in R06 that ACadSharp does not read (2 of 140066)
- Layer filter: config/cad_layers.json (keep, drop, plot line layers, plot label layers). Dropped and counted: 2705 pieces on Defpoints in Building Footprint, 61 in Road Layout, 26 in R06. pieces.json is up to 46 MB per drawing
- Xrefs: baseplan 3 of 3 found next to the file. 4 other xrefs are not found (one in each of the 3 Master Plan files and one in Typical Cross Sections), the run went on without them. Reader notes are grouped with counts. MULTILEADER: 18 exist in 2 drawings and none holds text, ezdxf agrees for the 15 on the baseplan
- Plot join, baseplan: MP_PLOT_LINE alone gave 777 faces, 390 plots, 375 faces with many labels. Adding MP_PLOT_SUBD gives 2991 faces, 2962 plots with one label each, 29 other faces (12 no label, 17 many labels), 18 labels outside every face, 0 duplicate numbers. Second method, shapely on the DXF: 2960 of my 2962 plots match by number and area, 2 not in the reference, 1 reference face not in mine
- PDF rows: 3346 rows from 12 sheets (round 3 counted 3306, it missed 3 rows per full block). PDFium text agrees on 3345, 0 missed. The 1 extra is a source typo, plot 2589 code 2 on sheet 009028 has N with no decimal point, kept as found and listed in odd. shapes file 2 to 3 MB to 74 KB on sheet 009003
- Excel: sheetRow on every row, coords_extra.json (notes, legend, formulas). Both workbooks read with MP_Phase 1_Zone21, 410 rows (sheet rows 40 to 449) and 337 rows. Raw sheet XML second method: 8290 of 8290 cells match, the row after each table has an empty plot cell
- Link table, join by plot number: 2962 CAD plots, 746 PDF plots, 747 Excel plots, 746 with all three. PDF unit type equals Excel unit on all 746. Location check fails on 746 of 746, see Known bugs 1. Coordinate space: PDF sheets overlap the CAD plots yes, 3319 of 3346 PDF points lie inside some CAD plot. 213 of 259 drawing pairs overlap
- Six random plots opened across CAD, PDF and Excel (1192, 1319, 1432, 1505, 2473, 2576). Each PDF row is in the PDFium text, each Excel plot cell is in the raw sheet, PDF type equals Excel unit, and each plot sits in the CAD plot numbered 10 or 11 lower
- Civil 3D roads: Road Surface C3D (180 MB) holds 105 model space entities and Rough Grading (216 MB) holds 5, one reader error each. ezdxf on AutoCAD's DXF export finds the same 105 and 5, so the surfaces and grading are Civil 3D objects outside model space, not read
- Gate: Python 60 passed, .NET 63 passed. New tests built on dummy data in code

Remains:
- Revit run on the real file, by Bader. Plot join only for the baseplan layer names. Everything under Known bugs

Known bugs:
- 1. NUMBER SHIFT: every PDF plot sits in the CAD plot whose label is lower by 11 (IMD sheets, 404 plots) or 10 (IMC sheets, 333 plots), a few edge plots differ by one more or less. 0 of 805 sheet plots sit in the plot of the same number, independent shapely faces agree. Nothing is corrected, all 746 are in conflicts.json
- CAD label type (DP4, VL4) is a plot type and PDF and Excel type (DT3, VD2) is a unit type, so they cannot be compared
- 4 PDF plots repeat a point code, 58 plots appear on 2 or more sheets, 1 Excel plot has no PDF rows
- Typical Cross Sections, CEC Building Footprint and Rough Grading do not share the site coordinate space with the PDF sheets
- Master Plan layers use other names (X_MP_Plots$0$MP_Plot), so no plot faces come from them
- Block that contains itself: the guard is in code and untested, ACadSharp overflows the stack when a test builds one
- Still open: one unclosed face and 28 plot_open pieces, arc crossings not noded, Revit not run

Next:
- Decide the numbering with Bader, then put the shift in a small config the link step reads
- Look at the 29 other faces and 18 stray labels, then placement only after the conflicts are understood

Your turn:
1. The baseplan labels and the schedules differ by 11 (IMD) or 10 (IMC). Which numbering is right, and where does the shift come from? Recommended: the PDF and Excel numbers are official, tell me the source and I add the shift as a config map
2. Keep MP_PLOT_SUBD as a plot line layer? Recommended: yes, without it 375 faces hold many plots
3. Is it right that the PDF points are unit corners that must lie inside the plot? Recommended: yes, the check stays at inside or within 1 m
4. Is there a table from plot type to unit type? Recommended: send it, until then no type check against CAD
5. Review config/cad_layers.json drop list (Defpoints, logo, title, border, viewport, legend). Recommended: keep
6. Civil 3D road files hold no readable entities. Recommended: export from Civil 3D by hand, the same as Revit
7. Sheet 009028 plot 2589 code 2 has N 2363719462 in the source. Recommended: tell the drawing owner, I keep it as found
8. Two rounds are called round 4 in this log. Recommended: leave it, say Round 4 CAD or Round 4 Revit
9. Revit still waits for you, steps are in the Revit round entry below

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

Detail: [steps/history/round-3.md](history/round-3.md).
Excel header row and stop rules fixed, El @ Entry and Height kept at 4 decimals. CAD plot geometry read from MP_PLOT_LINE (777 closed shapes, 28 open pieces). One more PDF sheet read and the layout held on all 12. Gate at the end of the round was Python 24 passed and .NET 28 passed.

## Round 2, 2026-10-22, readers run on real ROSHN Makkah files

Detail: [steps/history/round-2.md](history/round-2.md).
First run of the four readers on the real files. Excel, PDF and CAD ran, Revit did not. The list of mismatches found there drove round 3. Gate at the end of the round was Python 18 passed and .NET 22 passed.

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
