## Round 5, 2026-10-22, Revit link names, shared parameters and floors

Done:
- Branch round/link-names-floors. Changed dotnet/RevitPrototypeReader, dotnet/SiteAdaptation.Common, the tests, docs/rules.md (R16 and R17 rewritten, R27 new), README.md and CLAUDE.md. Not touched: dotnet/CadReader, python/excel_reader.py, python/pdf_reader.py
- THIS CANNOT BE PROVEN AGAINST THE REAL FILE. There was no Revit here. The Revit API code (RevitModelSource.cs) compiles against the Revit 2024 API package and has never run. The tests prove the reader logic and the JSON shape on test doubles. Bader must rebuild, reinstall and run it in Revit to confirm anything about the real model
- Item 1, link data. Every link entry, loaded or not, now has name, parameters (every instance parameter, text included), plotNumber, prototype, ffl, steps, case and nameParts. A missing or blank value is written as an explicit null and a line is added to that entry's errors. Name with fewer or more than 5 parts also adds a line. file, transform and errors are as before. A link whose transform, name or one parameter fails to read now stays in the list with a problem line, before it was dropped
- Item 2, small output. New top level linkedFiles, one entry per file name, read from the first loaded link of that file. Each link keeps contentsRef. Links inside a file now sit under its linkedFiles entry and not under every link. Flag --link-contents none|once, default once. An add-in has no command line, so the flag is read from reader-options.txt next to the DLL, or from ReaderOptions.Parse in tests. Dummy case, 90 links to 3 files with 61 instances each: 237879 characters with once, 4473767 characters with the contents copied into every link, 19 times smaller
- Item 3, floors. New floors array after walls: type name, level, heightOffset, topElevation, thickness, area, boundary, arcs and all instance parameters. boundary is the outer loop (largest area) as vertices, arcs hold edge index, centre, radius and ccw. Only floors in the host are read. Walls and ramps unchanged
- Item 4, python/link_summary.py with 10 tests on dummy json built in code. It loads with an object hook that drops the repeated contents, so the 223 MB file loads in 2 seconds
- Real file run, old shape, 223 MB: 338 links, 289 not loaded, 14 distinct files, 167 mirrored links, 5 distinct rotation values. Second method, grep on the raw text: 651 not loaded lines and 700 transforms, which is 289 + 362 and 338 + 362, so 362 nested link entries and all of them not loaded. The script counts host links only
- NOT COMPUTED on the real file, the old shape has no such fields: links per prototype parameter, mirrored per prototype, CASE against mirrored and against rotation, Prototype-FFL against origin Z, repeated plot numbers and the plot name check
- Mutation check: making the reader read every link made the two read once tests fail, then it was put back
- Gate: build 0 errors, .NET 77 passed (63 before), Python 70 passed (60 before)

Remains:
- The Revit run on the real model, by Bader. Nothing new is confirmed until then

Known bugs:
- UNPROVEN Revit API behaviour: RevitLinkInstance.Name may not give the text shown as Name in Properties. Floor.SketchId and Sketch.Profile for the loops. The arc sweep from Arc.Normal. Level elevation plus offset as the floor top. Thickness and area parameters
- A floor with no sketch gets no boundary and a line in errors. Foundation slabs are Floor elements too and are included
- Floor coordinates are internal, like every other point, not survey coordinates
- link_summary reads ffl as metres and only as a plain number. It counts host links, not the 362 nested ones. In the new shape nested entries sit under linkedFiles
- Added beyond the brief: nested link entries are read once per file, a plot name check in the summary, linked file counts in the Revit dialog, per parameter read failures on a link kept as problem lines
- Links are now sorted by file name and then name
- Still open and untouched: the items under Round 4 and earlier

Next:
- Bader runs the add-in and sends back prototypes.json, then the shape and values are checked against the model, link by link
- Run link_summary.py on that file and read the CASE, mirror and rotation tables

Your turn:
1. Rebuild the add-in, copy the new files into the Revit Addins folder as before, and run it on the attached site model, with the links loaded if possible, and send back prototypes.json
2. Does name on a link entry match the Name shown in Revit for the same link? Recommended: check three links and tell me
3. Is Prototype-FFL in metres? Recommended: yes, tell me if it is millimetres and I change the summary
4. Keep foundation slabs in floors? Recommended: keep, filter later by type name
5. Count nested links in the summary? Recommended: no, they are all unloaded in the old run
