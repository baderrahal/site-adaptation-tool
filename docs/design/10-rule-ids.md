## 10. Planned rule ids, conflicts and questions

Planned rules, taken from R345 to R388, each written into docs/rules.md with its test in the PR that builds it. R385 to R388 are the Python changes the parity needs (section 5), so Phase 6 takes R389 to R399. Each PR holds at most four changes:

| Id | Rule | PR |
|---|---|---|
| R345 | The parity harness: --steps of plan_from_book.py, the step files of the worker, python/parity.py with the tolerances of section 5 by kind of field, the one unit cases listed | 4a |
| R346 | Common on netstandard2.0, the worker on net48 and net8.0, Newtonsoft.Json 13.0.1, the test project on net48 and net8.0 with a Linux leg | 4a |
| R347 | The worker run: the request and the answer with the document they belong to, the free memory against the measured peak plus the floor, the exit codes, the kill at the time limit, the work files by a temporary name and a rename, Revit not waiting but for the level Excel (section 2) | 4a |
| R385 | One rounding rule in Python and C#: the 15 significant digit decimal of the value read with its exponent (NumberStyles.Float), half away from zero, minus zero as 0, and R2 changed to name it | 4a |
| R386 | The override list keeps use and skip through a majority run, and a Python build leaves an entry marked use rule and a skip plot | 4b |
| R387 | The control point fit of the location plan, the marks by their shape, the start from their similarity, refined on the faces with one label, as a check of the schedule fit of R181 in location_plan.py: it numbers nothing and lists apart every face it numbers otherwise with why | after Q081 |
| R388 | The Python reference refuses a LandXML with no linear unit or another than meter and a drawing whose InsUnits is not Meters | 4b |
| R348 | DWG reads: faces, labels, blocks, curbs, ROW and setbacks, a drawing not in Meters refused | 4c |
| R349 | LandXML surfaces and the road level snap, a LandXML with no unit or not in meter refused | 4c |
| R350 | DLUS and sales reads | 4c |
| R351 | The location plan: text lines, pairs, the page to site fit by the schedule fit of R181, which numbers every build, number to face, and the control point fit of R387 as its check, the marks the curves of the mark shape, the start their similarity, the refine on the faces with one label, listing apart the faces it numbers otherwise with why, as the Python does | 4d |
| R352 | The plot table, numbered by the schedule fit of R351 | 4d |
| R353 | The project position file written from a key read by the worker, the check points and the shared parameter ids of the keys | 4d |
| R354 | Footprint and analysis clusters | 4e |
| R355 | Front edge and corner front | 4e |
| R356 | Link frame | 4e |
| R357 | Hand rules, the kind, pair kind and unit class with them | 4e |
| R358 | Ramp, generic and yard outlines and the ramp type | 4f |
| R359 | Levels, the row partners and the road at front with them, the road at front and El @ Entry from one unrounded mean | 4f |
| R360 | Overrides, the use and skip of R386 with them | 4f |
| R361 | Link parameters and the link name | 4f |
| R362 | Walls and python/plan_walls.py | 4g |
| R363 | Issues and the check rows, python/check_rows.py, Past in the form of the list by check and field | 4g |
| R364 | The level Excel, written to its temporary name inside the transaction of button 6 and moved after the commit, its run id, the road at front | 4g |
| R365 | The report Excel, one build per report, the counts made apart, Levels by the run id of button 6, the plan summary | 4g |
| R366 | Scope with the plot list syntax and its stops, the step state with the hash of every input and of the folders by their lists and every output on disk by its hash | 4h |
| R367 | The settings JSON kept in the project, written only by a real run | 4h |
| R368 | Element tags, the match of the found elements to the plan, the hand change, the copy and the schema versions | 4h |
| R369 | Second method of Phase 4, seeds 14 and 7 | 4h |
| R370 | Install script, manifest, one Common | 5a |
| R371 | The ribbon | 5a |
| R372 | The pane bound to the active document, a worker answer routed to its own document, the work folder by the file name and the hash of its path, the settings saved with the project | 5a |
| R382 | One transaction per button, the steps outside it, the confirm before any write, the dry folder, the roll back with the reason, the stop | 5a |
| R373 | Button 1 on an empty project, the order of its steps, the units by Document.SetUnits, the level by Level.Elevation on its stated base, the shared parameters by GUID with the stop on a name of another GUID, the shared parameter file of the run, the badge by what button 1 sets | 5b |
| R374 | Button 1 on an existing site file, its dialog with no tick and no position save | 5b |
| R383 | Element tags in Revit, schema names of letters, digits and underscore, the library and memory line of each run | 5b |
| R375 | Button 2 Read data | 5c |
| R376 | Button 3 Check, the check table, Use and Save overrides of the rows changed in the session only | 5c |
| R377 | Button 4 Place villas, link types by full path, the load and reload step before the transaction, a changed prototype file reloaded, the move in place of a tagged link | 5c |
| R378 | Button 5 Floors and ramps | 5d |
| R379 | Button 6 Levels, RRE_ZZ_SP_Prototype-FFL written | 5d |
| R380 | Button 7 Walls | 5d |
| R381 | Button 8 Report | 5e |
| R384 | The test host jobs of the buttons and the screenshots | 5e |

Conflicts, none resolved silently:

1. R85 never writes RRE_ZZ_SP_Prototype-FFL and R80 keeps it out of parameters, while keyRules writes it by fflDownToTheCm, 0.994 on 1,791 TU plots, and the plan of 3c holds it (READ). By the majority rule button 6 writes it, and R379 changes R85 and R80 in the same change.
2. R80 lists 9 link parameters and the newSite job of R212 bound Plot and 9, while the keys hold 10 on their links, RRE_ZZ_SP_Prototype-FFL with them (READ, the RC and RD reads). Button 1 binds 11.
3. R181 fits the location plan to the site on the plots of the IMC schedule, and keyRules and the overrides, 12,421 after PR 52, were measured on that numbering, while the first draft of this design numbered a new project by the three control points the sheet prints. The schedule belongs to the project, so the schedule fit numbers every build on both sides (R351, R352), and the control point fit of R387 is only a check that lists the faces it numbers otherwise (section 5). The IMC fit put the control points 0.265 to 0.305 m from their stated points (READ, locationPlan.json).
4. R211 puts the internal origin at the plot centre of the NBH, the keys share the Parsons position. Under Q068 button 1 sets the Parsons position from the file config names.
5. R212 makes a new shared parameter file, so new GUIDs, the keys hold the client's. Phase 4d read the GUIDs from the key copies into config so button 1 writes the same ones (R353), 10 of 11, Plot a project parameter in the keys (Q085). Until then the GUIDs of a new file differ from the keys and a schedule over both files does not join them.
6. R87 skips a plot already placed, button 4 moves a link it tagged when the plan moved it. R377 keeps the skip for every untagged link.
7. Place from plan runs links and floors as two steps of one button (R116), the mock makes them buttons 4 and 5, one transaction each, as R116 already gives each step its own.
8. The garden walls have no measured rule (R335 counts them), so button 7 places boundary walls only and lists the garden walls as not placed, while Q071 asks for the wall top rule.
9. shapely, a requirement of the Python checker, bundles GEOS under LGPLv2.1 (Q076).
10. R2 rounds to 3 decimals, and Python round() and C# Math.Round give another result on a tie such as 12.3455 (RAN, rounding_probe_pr49.txt). R385 gives both one rule on the decimal value and changes R2 in the same change, the Python outputs move by one unit of their last decimal on a tie (Q078). The level builders kept round() in 4a, since with R385 there 350 level items of the build proof lost their key, and take R385 since 4b with the level overrides measured again on it, 0 unexplained on every key (Q080).
11. majority.write_overrides writes only note, overrides and cannot, and plan_rules.load_overrides ignores use (READ), so Skip and Rule of the check table would be lost or not honoured. R386 changes both before step 12 is compared, done in 4b.
12. TypesByFile of Placer.cs reuses a link type by its file name (READ, l.296 to 331), so two files of one name in two folders would share a type. R377 matches by the full path.
13. SiteSteps.cs refuses a shared parameter file that is there already (READ, l.42 to 44), so a run that rolled back after writing it would block the next. Button 1 names its file by the run id and deletes it after (R373).
14. R212 binds the parameters from a new shared parameter file, so a site file it made holds them under the names of config with other GUIDs, and R85, R87 and R114 find a parameter by name (READ, RevitPlacerTarget.cs l.50 and l.166, RevitFloorTarget.cs l.66 and l.199). Button 1 stops on such a file with the list and binds nothing, and every tool parameter is read and written by its GUID (R373, R377, R378).
15. R40 reads a LandXML with no Units element and gives its linearUnit as None (READ, landxml_reader.py l.34 to 46). R388 refuses it, and R349 does on the C# side.

Questions opened by this design, the full text in steps/questions-closed.md:

- Q076. The Python checker packages: shapely is BSD-3-Clause and its wheels bundle GEOS under LGPLv2.1, numpy, pillow and pypdfium2 hold parts under other permissive licences, none ships with the add-in: keep them for the checker (recommended), or replace shapely.
- Q077. The worker: button 2 and the plan of button 3 run in the add-in's own worker process, CadReader.exe in C#, installed with the add-in and started and ended by the button, since the Road Layout read alone took 2,571 to 2,612 MiB and PdfPig needs a System.Memory Revit 2024 does not hold: accept (recommended), or read inside Revit with the resolver.
- Q078. Rounding: one rule for Python and C#, the 15 significant digit decimal of the value rounded half away from zero (R385), so 12.3455 gives 12.346 in both, and the Python outputs move by one unit of their last decimal on a tie: accept (recommended), or round half to even in both.

The design follows the recommended answers of Q068 (the Parsons position), Q017 (VD4 by Rev 9), Q057 (no ramp front fall), Q071 (the base, the line and the open front of the boundary walls, the top weak until the architect answers) and Q078 (half away from zero), and the state until an answer of Q066 (the Zone 1 yard of Zones 2 and 3). Q073 is answered (Bader, 2026-10-26): the Roads folder was read in full and holds no road level at a front corner no road surface covers, so such a plot gets no level and is listed with why (R344), none in NBH 012, and step 9 carries the list into the C#. Q082 asks for the road model of the 33 plots of the keys with none, an answer changes data, not code.
