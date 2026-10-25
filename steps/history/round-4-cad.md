# Round 4 CAD, full log entry

Moved here from steps/log.md in Round ADDIN.

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
