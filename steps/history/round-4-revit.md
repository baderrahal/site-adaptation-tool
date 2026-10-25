# Round 4 Revit, full log entry

Moved here from steps/log.md in Round ADDIN.

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
