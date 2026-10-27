## Phase 3 part 2, PR 3b docs/addin-design

Branch docs/addin-design from main at a201c19, equal to origin/main after git fetch (RAN). Rules R342 and R343 are new, R344 is not used. The phase ran in a helper agent started by the main session, which runs the checkers and merges. No Revit or Civil 3D window was opened, the lock was not taken, the registry was not read or written. This PR writes the design and the licences, no add-in code. Values of coordinates and levels stay in Documents\SiteAdaptation\round_addin\design, this file holds counts, sizes, versions and plot numbers only.

### What the PR holds

| Change | Files | Rule, test |
|---|---|---|
| 1 | docs/licences.md, every .NET package of the 9 projects, the 4 planned ones and every Python package, with the licence, where it ships and how it was checked, the load check, what Revit 2024 ships, the libraries not chosen, the conflicts | R342, python/tests/test_licences.py, 6 tests |
| 2 | docs/addin-design.md, the design of Part 1 in 10 sections, before any add-in code | R343, python/tests/test_addin_design.py, 6 tests |
| 3 | The records: this section, the top entry of steps/log.md, steps/PROGRESS.md, Q072, Q076 and Q077 in steps/questions-closed.md | |

Outside the repo: round_addin\design\mock_filled.md (section 7 with the real coordinates and levels), round_addin\design\work (plan_012.json, mock_values.py and .json, mock_filled.py, inputs_hashes.txt), round_addin\logs\p3b_*.txt.

### The libraries (R342)

- Licences READ from the .nuspec of the 24 packages of the obj/project.assets.json files under dotnet in the local NuGet cache, 23 of the 9 projects of the sln and Microsoft.NETFramework.ReferenceAssemblies.net461 of SiteAdaptation.C3DProbe.Civil outside it, which the first count took for one of the 9 and the table lacked until the review, and from the licence file inside the package where the nuspec gives a file or a URL (Nice3point, System.Collections.Immutable, System.Reflection.Metadata), SEARCHED on nuget.org for PdfPig 0.1.16, DocumentFormat.OpenXml 3.5.1 and its Framework 3.5.1 and for the URLs of Microsoft.NETFramework.ReferenceAssemblies and xunit.abstractions. All 28 .NET packages are MIT or Apache-2.0 (READ).
- Load check (RAN, a scratchpad project outside the repo): a net48 host holding System.Memory 4.0.1.1, System.Buffers 4.0.3.0, System.Numerics.Vectors 4.1.4.0, System.Runtime.CompilerServices.Unsafe 4.0.4.1, Newtonsoft.Json 13.0.1 and DocumentFormat.OpenXml 2.16.0 with no redirect, then an add-in loaded by LoadFrom. With no resolver ACadSharp 3.8.0, DocumentFormat.OpenXml 3.5.1 and Newtonsoft.Json ran (the host copy, file 13.0.1.25517, served), PdfPig 0.1.16 failed with FileNotFoundException for System.Memory 4.0.2.0. A first resolver, which served only a request whose requesting assembly lies in the add-in folder, did not help, the request came with no requesting assembly (seen in the session, its output not saved, so it is not a RAN of this file). The second, which also serves a request with no requester when the file of the add-in folder has the same or a higher version, let all four run. The same code as a net48 exe got a generated config redirecting System.Memory and System.Buffers to 4.0.5.0 and ran with no resolver.
- Revit 2024 (READ, C:\Program Files\Autodesk\Revit 2024, assembly versions by GetAssemblyName): NewtonSoft.Json.dll 13.0.0.0 (file 13.0.1.25517) with the redirect 0.0.0.0-13.0.0.0 in Revit.exe.config, System.Memory 4.0.1.1, System.Buffers 4.0.3.0, System.Numerics.Vectors 4.1.4.0, System.Runtime.CompilerServices.Unsafe 4.0.4.1 with no redirect, AddIns\DynamoForRevit\DocumentFormat.OpenXml.dll 2.12.3.0, itextsharp 4.1.6.0. No ACadSharp, PdfPig or other OpenXml under Revit 2024 or the two add-in folders.
- Python (READ, METADATA of the 19 packages installed in .venv): every requirement and the 13 packages they pull in, 18 rows, pip left out as the installer. shapely 2.2.0 bundles GEOS under LGPLv2.1 (Q076).

### The design (R343)

- Memory of the readers (RAN, CadReader.exe of the build at a201c19 on copies in the scratchpad, each source equal by Get-FileHash before and after, each copy equal to its source, the copies deleted after): Makkah Phase 1_Road Layout.dwg, 82,061,457 bytes, 20.2 s, peak private 2,576 MiB, 216,017 pieces, 6,157 blocks. ROSHN Makkah Baseplan.dwg, 1.3 s, 99 MiB, 2,962 plots. Makkah Building Footprint.dwg, 1.0 s, 80 MiB, 3,780 blocks. 6 GiB free of 15.78 at the start. Its output was not saved. The review saved a rerun at 67c1fa0 (RAN, round_addin\logs\cadreader_peaks_pr49.txt): Road Layout 2,571 MiB polled and by the OS peak in 21.2 s, baseplan 100 MiB in 1.4 s, footprint 77 MiB in 1.3 s, InsUnits Meters on all three, each source equal by hash before and after. The claim-checker's rerun gave 2,612 MiB in 24.8 s, 99 MiB in 1.2 s and 77 MiB in 0.9 s. This and the PdfPig failure make the worker of the design, Q077 asks Bader.
- Common for netstandard2.0 (RAN): the 31 source files of dotnet/SiteAdaptation.Common copied to the scratchpad with a csproj for netstandard2.0 and Newtonsoft.Json 13.0.1 built with 0 errors and 0 warnings.
- The build of NBH 012 by the parsons rules (RAN, plan_from_book.py nbh=012 with the inputs of the build route of 3c and the committed override list, into round_addin\design\work\plan_012.json, its run time not logged): 337 plots, 336 written, 2312 not written (no unit, it shares the face of 2311, Q016), 5 written with no yard (no fitted outline for VD4, VD2 and VL6 on plots 10.0 to 13.0 m wide: 2378, 2387, 2441, 2544, 2545), 91 clusters with 337 plots, 214 in a cluster, front rule nearestCurb 227 and orientation 110, Top Surface-02 on 672 of 672 front corners, floors generic 336, yard 331, ramp 336, the overrides applied and left as in the build of the key cover of 3c. The 28 inputs it read equal by sha256sum before and after (round_addin\design\work\inputs_hashes.txt).
- The filled mock (RAN, mock_values.py on the plan, the override list, majority.json and key_join.json of 3c): 1,247 check rows, Plot cut 12, Floor 38, Hand 12, Unit code 3, Position 4, Level 817, Parameter 361, 2 plots follow every rule (2616, 2624), level rows 336, 24 of them Source Rule. Second count by PowerShell ConvertFrom-Json, another tool on the same files: 13 of 13 values equal, 0 differ (round_addin\logs\p3b_mock_second.txt).
- Data book of NBH 012 (RAN, the snapshot plots.csv and labels.csv): 380 faces, 337 unit plots, 348 with a footprint block, 345 with a road level, 337 client code labels, Zone 2. The Phase 1 location plan: 2,704 client code labels, 2,591 in the 7 NBH, 113 with no sales row.
- The mock values of section 7 come from: the plan of NBH 012, the override list, majority.json and key_join.json of the 3c review, site_report_i18.json and site_imc_b.json of the Round 17 IMC new site file (the check points, 5 of 5 at 0.0 mm), the RC and RD reads of 2a (units, levels, types, the 10 link parameters), key_reads.json (wall types), plan_012.json alone for every number of the report page since the review (Counts agree: Excel links 336, floors 1,003, levels 336, Revit open until Phase 6), and keyRules of the rules profile (Rules applied). The Round 17 rebuild, prototypes_imc_r17b.json against plan_imc_r17b.json with 999 floors, and place_codes_b.txt (13 runs, 7.96 min) filled the first draft and are named only as another build now.

### Tests first

- R342 (RAN, round_addin\logs\p3b_r342_first.txt): with docs/licences.md moved aside 5 of 6 tests failed on FileNotFoundError, the sixth tests the readers on made up files. With a row set to Newtonsoft.Json 13.0.1, PdfPig set to LGPL-2.1 and shapely set to MIT, 4 failed for those rows. Then 6 passed.
- R343 (RAN, p3b_r343_first.txt): with docs/addin-design.md moved aside 4 of 6 failed on FileNotFoundError. A made up easting in place of one [coordinate] failed the coordinate test, and every "Counts agree across outputs" renamed failed the label test. Export to Excel taken out of its first line did not fail it, since the label is found again further on in 7.4, so the order test holds each label once in section 7 and a label written twice can hide one place. Then 6 passed.

### Not done in this PR

- No add-in code, no Revit window, nothing placed. The counts of buttons 4 to 6 in 7.6 and the Excel column of the Counts agree box are those of the plan a real run would place, the Revit column waits for Phase 6, which places the parsons build.
- R344 is not used. CLAUDE.md is not changed, the lines it should gain are in the report of the PR.
- Wall width and the shared parameter GUIDs of the keys are not read (4b). The floor type thickness is read: the Default Thickness of every floor type is in the floorTypes of each key read, the 5 types of the IMD read among them (READ, testhost_RD_prototypes.json, round_addin\logs\key_level_bases_pr49.txt). Whether Revit 2024 moves the survey point and the base point as the keys hold them, a project from no template, the WPF pane and the screenshots are UNKNOWN until Phase 5.

### Questions

- Q076 new: the Python checker packages, shapely bundles GEOS under LGPLv2.1.
- Q077 new: the worker process beside Revit for the reads and the build.
- Q072 has its full text in steps/questions-closed.md now.

### Gate

- On the working tree of this PR at a201c19, every file of the three commits in place: Python 1544 passed and 1 skipped, .NET build 0 errors and the 1 old warning (xUnit2013), .NET 522 passed (RAN, round_addin\logs\p3b_gate_python.txt, p3b_gate_build.txt, p3b_gate_test.txt). Main before this PR at the gate of PR 48. In this PR 12 Python tests, 6 of R342 and 6 of R343, and no .NET test.

### Review of PR 49, the breaker and the claim-checker

The breaker and the claim-checker read the design and the records. Every item is settled in docs/addin-design.md in new commits on docs/addin-design, no amend, no add-in code, no Revit or Civil 3D window, CLAUDE.md unchanged. Real values stay in round_addin, this file holds counts, sizes, names and plot numbers.

| Item | Where the design settles it |
|---|---|
| 1 Rounding | section 5, One rounding rule: R385 planned, the 15 significant digit decimal half away from zero in both languages, rounded fields compared for equality, Q078 |
| 2 Past | 3.3 Use, the table What Past writes by check and field: a gap to the rule for levels and positions, not offered on Unit code, Plot cut, outlines and the ramp front fall, an existing site file only when it is an answer key |
| 3 Skip | 3.3 and section 5: R386 planned, write_overrides keeps skip, load_overrides honours use rule |
| 4 Counts agree | 3.8 Checks and 7.6: Revit read at report time against the Placed and Levels rows and the plan, one build for every number of the page |
| 5 Badge | 3.1 and 7.3: by the Settings entity, a shared parameter of config, a position or a moved survey or base point, never by types |
| 6 Repeat | section 3, Element tags, A second run, Schema versions |
| 7 Link types | 3.4 Link types and Link types to load, 7.7 item 2 |
| 8 Step hash | section 3, Steps: every config file of the step, the position file, the folders by their lists, content hashed at the read |
| 9 Worker outputs | section 3, Work files, and section 2, The worker run |
| 10 Two PDFs of one name | 3.2 and 7.2: size and short hash beside the name, the path on hover |
| 11 Input units | 3.2 Checks: LandXML not meter and InsUnits not Meters refused, R348, R349, R388 |
| 12 Scope | section 3, Scope: the plot list syntax and its stops, button 1 on the whole project |
| 13 Units | 3.1 order step 1: Document.SetUnits |
| 14 Pane per document, Diff, Road at front | 7.2 How, 7.3 How, 7.5 |
| 15 Schema names | section 3, Element tags, section 6 Where |
| 16 Transform direction | 3.1 Checks |
| 17 Survey point, order, level datum | 3.1 order steps 2 and 3, the measures of Phase 5b |
| 18 Shared parameter file | 3.1 Shared parameters |
| 19 Work folder name | section 6 Work folder, settled in the last pass below as its item 19 |
| 20 Worker memory | section 2, The worker run, R347 |
| 21 Dry run of 3 | section 3, Nothing changes until the user confirms, 3.3 Dry run, 3.4 Inputs |
| 22 Settings and work folder | section 6 When and Work folder, 3.1 Dry run |
| 23 The checkbox | section 3, Nothing changes until the user confirms, One transaction per button |
| 24 Parity inputs | section 5, the inputs of the reference and steps 7, 10 and 15 |
| 25 Location plan fit | section 5, The location plan fit: R387 planned, the Python reference fits by the control points |
| 26 Mock | 3.1 Existing site file, 7.3 Coordinate system, section 4 Villa blocks the setbacks, 7.4 title, 7.6 five columns, section 6 What, 7.7 |
| 27 The coordinate check | python/tests/test_addin_design.py, R343 |

Facts of the review:

- Rounding (RAN, round_addin\logs\rounding_probe_pr49.txt, a probe in the scratchpad on net48 and net8.0 and Python 3.12.10): at 3 decimals Python round() gives 12.345, 15.123, 1.0, 2.046 and 7.062 for 12.3455, 15.1235, 1.0005, 2.0465 and 7.0625, C# Math.Round AwayFromZero and the 15 digit decimal half away from zero give 12.346, 15.124, 1.001, 2.047 and 7.063 on both runtimes, ToEven gives 1, 2.046 and 7.062 on three of them.
- CadReader at 67c1fa0 on copies (RAN, cadreader_peaks_pr49.txt): Road Layout 2,571 MiB in 21.2 s, baseplan 100 MiB in 1.4 s, footprint 77 MiB in 1.3 s, peak polled every 50 ms and by the OS equal within 3 MiB, InsUnits Meters on all three, each source equal by hash before and after, free memory 3.93 GiB at its lowest during the Road Layout read.
- Hashing (RAN, hash_times_pr49.txt): the 13 prototype files of Prototypes_2024, 1,027 MiB, 1.16 s by content with the files in the cache and 0.014 s by the list of names, sizes and write times, the 18 LandXML files of the snapshot, 226 MiB, 0.27 s and 0.001 s. Prototypes_2024 holds 273 files of 2,377 MiB with its backups and STR folder.
- Key levels (RAN, key_level_bases_pr49.txt): on all 7 reads Level 1 and Level 2 have Elevation Base Survey Point and Elevation equal to ProjectElevation within 0.0001 m. The Default Thickness of every floor type is read, 3 to 5 types per key, Generic 150mm 0.10 m on IMC and 0.15 m on IMD.
- Inputs: 42 LandXML reports under Documents\SiteAdaptation give linearUnit meter (READ). Two files are named 01101-PSN-ZZZ-XXX-DWG-TU-005000.pdf, 3,056,293 bytes in Roshn Makkah - Master Plan Rev.09, the one the data book reads, and 2,395,117 bytes in 01_Output\1.0 General\PDF (READ, sha256sum).
- Packages (RAN, the assets files): 23 in the 9 projects of the sln, Microsoft.NETFramework.ReferenceAssemblies.net461 of SiteAdaptation.C3DProbe.Civil outside it, 28 rows with the 4 planned, the net461 row added. xunit.abstractions takes lib/net35 on net48.
- frontCorner: the build of NBH 012 gave the 110 plots with two or more road edges the edge the sales Orientation names and the other 227 the nearest curb (RAN, summary.frontRule of plan_012.json), so Rules applied lists frontCorner frontRule 0.978.
- The filled mock outside the repo was written again with the Diff at two decimals, 5 lines changed (mock_filled.py, the earlier file kept as work\mock_filled_before_pr49.md).

Tests first (RAN, round_addin\logs\pr49_tests_first.txt): the revised test_addin_design.py on the design of 67c1fa0 failed 5 of 13, the ties, the schema names, the Past table, the six columns and the planned range. On the revised design the leak test failed with one real level of the plan planted at 2 decimals, and the leak check found 2 of 2 planted values among 10,652 real values of the outputs (2,608 levels and 8,036 coordinates), 0 before. test_licences.py with the assets scan failed on the net461 package before its row. Then both files passed, 13 and 8 tests.

Python changes planned, each before the C# step compared with it: R385 rounding (4a), R386 Use and Skip in the override list (4b), R387 the control point fit of the location plan (4b), R388 input units (4b). Phase 4 runs in 8 PRs of at most four changes, 4a to 4h, Phase 5 in 5a to 5e, Phase 6 takes R389 to R399.

Questions: Q078 new, rounding, recommended accept. Q077 holds the three runs of the Road Layout read.

Gate: at the head of the PR, in the PR body and round_addin\logs\pr49_gate_*.txt.

### Last pass on PR 49

The lead read the revised design and named 19 more items, 15 silent in the design, 3 in the records and 1 on the work folder. Each is settled in docs/addin-design.md in new commits on docs/addin-design, no amend, no add-in code, no Revit or Civil 3D window, CLAUDE.md unchanged.

| Item | Where the design settles it |
|---|---|
| 1 Counts agree, Levels | 3.6 Outputs and Checks, 3.8 Checks: Revit counts the plots the last run of 6 that committed set and read back, by the UniqueIds of its run log, Excel the Levels rows of the Excel whose Settings sheet carries that run id, another run id an issue |
| 2 Files after the commit | section 3 One transaction per button, Steps and Work files, 3.1 order step 6, 3.6, 3.8: written to the temporary name inside the transaction, a failure rolls back, moved after the commit, a failed move leaves the step not Done with the open file named |
| 3 Parameter of one name | 3.1 Shared parameters, one per name: a name of config with another GUID stops button 1 with the list, every read and write by Element.get_Parameter(Guid), conflict 14 |
| 4 Save overrides | 3.3 Use and 7.4: only the rows changed in the session, byHand on those alone, the 112 left rows of NBH 012 untouched |
| 5 Worker and document | section 2 Request, Answer and Revit does not wait, 7.2 How: the path and session id of the document in the request and the answer, the result to that document, dropped with an issue once it closed |
| 6 Numbering | section 5 The location plan fit, R351, R352, R387, conflict 3: the schedule fit of R181 numbers every build, the control point fit a check listing the faces it numbers otherwise |
| 7 Road at front | 7.5 and step 10: the road at front and El @ Entry from one unrounded mean, each rounded once by R385 |
| 8 Changed prototype file | 3.4 Link types to load and Second run, 7.7 item 2: a loaded type whose file hash changed is reloaded after a confirm, before the transaction |
| 9 Check point Revit column | 3.1 Checks, 7.3 How: in a dry run the value Apply would give from the position it would set, after Apply the value Revit reports, never Revit against itself |
| 10 Exponent text | section 5 rule step 2: NumberStyles.Float, the C# float noise test in the plan of 4a |
| 11 Tolerances | section 5 Parity tolerances: 0.000001 m, m2 and degree before rounding, one unit apart only on a straddle within it and listed, no geometry exact |
| 12 Level and clip | 3.1 order steps 2 and 3: Level.Elevation on the stated base, ProjectElevation read only, BasePoint.Clipped set, the open point dropped |
| 13 Badge on the IMD copy | 3.1 the badge: the signs that copy holds, the Settings entity on the 5b project after a run, the R17B copy for the GUIDs |
| 14 LandXML unit | 3.2 Checks, R349, R388, conflict 15: no unit or not meter refused |
| 15 Past grey | 3.3 and 7.7 item 4: every row of a site file that is no answer key, every row of a plot no key holds |
| 16 Phase 6 ids | the plan of this file: Phase 6 R389 to R399 |
| 17 Control byte | steps/questions-closed.md Q078: the carriage return made a backslash r again, no other control byte in the steps files, row 19 of the review table above |
| 18 Mock sentences | 7.2, 7.3, 7.4, 7.5, 7.6: every sentence of the mock word for word with its punctuation, the two file sentences in a text fence |
| 19 Work folder | section 6 Work folder: the file name and the first 8 hex digits of the SHA-256 of the full path, a document with no path asks for a save first, 7.2 the name "not saved" |

Facts of the last pass:

- Tolerances (RAN, round_addin\logs\pr49_last_tolerances.txt, the probes in pr49_last_probes, made up coordinates only): on 20,000 four sided faces the shoelace area in reversed order differed from shapely by up to 3.8E-04 m2 and 1,483 areas differed after R385 at 3 decimals, a plain centroid loop on raw coordinates by up to 1.56 m, 19,956 differing. About the first vertex the area held within 9.1E-13 m2 and the centroid within 1.4E-09 m, 0 differing. Two orders of a point sum 5.8E-11 m, two level formulas in a triangle 3.0E-12 m, the affine fit about the means against numpy lstsq 4.4E-07 m over 200 fits.
- Exponent text (RAN, pr49_last_float_parse.txt): decimal.Parse with InvariantCulture threw FormatException on 5 of 5 G15 texts with an exponent on net48 and net8.0, NumberStyles.Float read all 5, 1E+300 threw OverflowException.
- Badge signs of the 7 key reads (RAN, booleans only, no value): each read holds a project position other than zero and a survey point and a project base point off the internal origin, the RD read holds the parameters of config on its links by name.
- Code (READ): RevitPlacerTarget.cs l.50 and l.166 and RevitFloorTarget.cs l.66 and l.199 find parameters by LookupParameter, landxml_reader.py l.34 to 46 keeps linearUnit None when the Units element is missing, plan_from_book.rule_levels writes rampFrontCorners by common.r3 per corner while plan_rules.entry_of takes the unrounded mean, override_source.merge refuses a run that drops a byHand entry, keyRules.site names no level base.
- Control bytes (RAN, a scan of every tracked file under steps): 1 byte, the carriage return at byte 22,780 of steps/questions-closed.md, in the path of the rounding probe of Q078.

Tests first (RAN, round_addin\logs\pr49_last_tests_first.txt): 9 new tests of R343 in test_addin_design.py, 8 failed on the design and records of f634985: the mock sentences (6 not word for word), the exponent text, the tolerances, the numbering of R351, the control byte, the review numbers (19 missing), the Phase 6 ids and the open list. The ninth, the sentence check finding a colon made a period, passed as a check of the helper. Then all passed.

No new question. Gate: at the head of the PR, in the PR body and round_addin\logs\pr49_last_gate_*.txt.

