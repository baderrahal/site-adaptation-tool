# Round log

Newest entry first.

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
- 01101-PSN-IMC-ZZZ-DWG-TU-009018.pdf, MISMATCH: two labels carry U+FFFD in place of curly quote marks in the sheet notes. The text reads as a replacement character plus the word, in the render it is a quoted title
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
