# Site Adaptation Tool

Data readers for the ROSHN Makkah site adaptation work, and the placer, which reads plan.json and places the villa links.
The rules live in docs/rules.md and nowhere else. This file points to them and adds the working agreement.

## Roles

- Bader decides. Every open question goes to Bader by its fixed ID with the recommended answer first.
- Chat drafts the prompts and reads what Code did.
- Code does the work and checks it. Code never marks a thing done without a second check by a different method.

## Start of every session

1. Confirm the folder is the repo root, the branch is not main, and `git fetch` shows main level with origin.
2. Read steps/PROGRESS.md, then the top entry of steps/log.md.
3. Open a new branch for the round. Never commit to main. A hook blocks commits and pushes to main.

## Round ADDIN

Part 1 is the add-in. Part 2, the output sheets, waits for Bader's check of Part 1. The plan and the findings are in steps/investigations/round-addin.md. Rule numbers R300 to R399.

- Option B (Bader): the add-in does everything inside Revit 2024 in C#, .NET Framework 4.8. No Python on the user's PC. Python stays in the repo only as the test checker.
- Q050 (Bader): parsons2024Rev6 is the level profile of every new build, Rev 6 prevails. native2022 stays for reference and is never the default.
- Answer keys: the 7 Rev 6 Parsons site models in C:\02-Site Adaptation\Revit All Zones, IMA and IMB Zone 1, IMC and IMD Zone 2, IME and IMF Zone 3, IMG Zone 4. They are the main reference for every rule. The Native 1.1 to 1.4 reads are a second reference only, for plots none of the 7 hold, and never for unit codes. Their Revit 2024 copies sit in Revit All Zones\2024, listed in answerKeys of config/project.json and checked or made by python/key_copies.py against key_copies.json beside them (R312). The IMC copy comes from the Zone 02\ARC original (Q065).
- The area key is the Neighbourhood_ID, never an IM letter. The Parsons and the Native job use the IM letters for other areas.
- The approved mock lives word for word in docs/addin-mock.md. It sets the content and the order of the ribbon, the dockable pane, the Set up site dialog, the check table and the two Excel files.

How the round runs:

- Do not come back to Bader until Part 1 is built.
- The main session plans, orchestrates and merges. One developer helper per PR.
- No approval stop: a PR is merged when gh pr checks passes and the checkers found nothing open.
- The breaker and the claim-checker read each PR before it merges. Each finding is fixed with a test.
- Never two PRs open. At most four changes per PR.
- A fix on its third attempt stops, goes back to measuring and becomes a question.
- New question IDs start at Q062.
- Every fact in a report is tagged READ with a path, RAN with a count, SEARCHED with a link, CHOSEN or UNKNOWN.
- .claude/agents holds developer, reviewer and breaker on opus, prober, claim-checker and log-reader on sonnet, runner on haiku. .claude/skills/ai-max holds the AI MAX skill, version 3.

## Repo map

The rule behind each path is in docs/rules.md under the id given here.

- python/ holds the PDF, Excel and LandXML readers, the link table and summary, the second source check, the face reasons, the surface lookup and roles, the ramp levels, the road check, plot_relations.py, plan_from_past.py, plan_compare.py and the full run script, plus tests in python/tests.
- config/project.json names the rules profile and the drawings and holds the settings of each round (R63). No project name goes in a Python file. config/plot_numbers.json holds the plot number shift of each neighbourhood. config/second_source.json names the plot layers of the master plan drawing named by drawings.masterplan. config/road_surfaces.json gives each LandXML surface its role and names the ramp types and the input files. config/cad_layers.json is the keep and drop list of CAD layers, Bader edits it.
- rules/ holds one project profile JSON per project and a README with the checks not coded yet. python/project_profile.py loads a profile by path (R62). New project rule values go there and not in code.
- dotnet/CadReader reads DWG and DXF with ACadSharp and writes blocks.json (R68), the DWG class list and the header dates. dotnet/SiteAdaptation.Common holds JSON output and unit helpers.
- dotnet/RevitPrototypeReader is the Revit 2024 reader add-in. Do not change it unless the round says so.
- dotnet/SiteAdaptation.Placer is the Place from plan add-in (R83 to R88). Its math lives in SiteAdaptation.Common behind IPlacerTarget, its Floors step in FloorPlacer behind IFloorTarget (R113 to R116). dotnet/SiteAdaptation.TestHost runs the placer and the reader in Revit with no clicks, only for a job of its run script (R103, R104). dotnet/SiteAdaptation.DialogWatcher answers the add-in security boxes with Do Not Load (R108). python/placer_check.py checks a test host run (R105).
- python/plan_from_past.py builds plan.json from a past job, prototypes.json and the schedule named by pastJob (R80, R81). The plan goes outside the repo. python/plan_compare.py compares a plan with the placed model (R89). python/yard_rule.py (R111), floors in config/project.json (R112), python/scenario_files.py (R118), placer_check.py --floors (R119).
- Rounds 11 to 13: python/cad_analysis.py writes analysis.json (R64, R69, R70, R71, R74), python/rules_checks.py (R65, R66, check_one_ffl R125), python/levels.py (R67, check 5) and python/levels_proof.py, python/block_scan.py (R73), python/block_proof.py (R76), python/link_proof.py (R77), python/road_lookup.py (R91), python/entry_levels.py (R93, R94), python/entry_proof.py (R95), python/block_offsets.py (R97 to R99), config/prototype_offsets.json and python/prototype_offsets.py (R100), python/round13_check.py (R101).
- Round 14: python/cad_links.py writes plotFrame into config/prototype_offsets.json (R120 to R123), python/cad_links_check.py (R124), python/plan_from_cad.py (R126, R128, --landxml-dir R137), python/ramp_outline.py (R127), plan_compare.py --against-plan (R129, R138), python/cad_plan_check.py (R130), python/cad_rules.py (R131 to R133), python/cad_rules_check.py (R134), python/floor_fit.py into config/floor_outlines.json (R135, R136), python/cad_floor_check.py (R139).
- Civil 3D and the data book: python/c3d_window.py and dotnet/SiteAdaptation.C3DWatcher (R160, R161), python/data_book*.py outside the repo (R163 to R168), data_book_retest.py (R170 to R172), data_book_retest_check.py (R173), python/wiring.py and data_book.py (R174 to R177), python/wiring_check.py (R178), python/location_plan.py (R179 to R183), python/location_plan_check.py (R184).
- Round DATA-3 (R185 to R198, R195 unused): python/rev9_workbooks.py (R185 to R187), typology_codes in rules/roshn-makkah.json (R188), python/rev9_drawing.py (R189 to R191), python/cross_sections.py (R192), python/entry_terms.py (R193), python/stray_units.py (R194), python/location_dwg.py (R196), python/c3d_window.py under the lock (R197), python/data3_check.py (R198).
- Round Z4 (R200 to R218): python/window_lock.py (R200), python/round_files.py (R201, R202, R218), python/rvt_info.py reads BasicFileInfo from the bytes (R203), the siteUpgrade, read, newSite and image jobs run by Run-TestHost.ps1 -RoundSpec with SiteSpec and SiteCoordinates in Common (R204, R205, R211 to R214), python/key_cover.py (R206, R207), python/site_spec.py (R211, R212), python/plan_from_book.py (R208 to R210), python/z4_compare.py (R215, R216), python/z4_check.py (R217). siteBuild in config/project.json holds the round settings.
- Round 16 (R220 to R236): PlacementRotation in Common (R220), python/level_cross.py (R221 to R223), python/corner_front.py (R224 to R226), python/footprint_check.py (R227 to R229), plan_from_book.py for a whole NBH (R230, R236), python/place_codes.py (R231), the image job with links (R234), python/z16_check.py (R235).
- Round DATA-4 (R240 to R250): the Civil 3D watcher by rule 3 (R240), CadReader --header-only (R241), python/surface_versions.py (R242), python/rev9_parcels.py (R243, R244), the read job with handCheck.folders (R245), python/villa_hand.py (R246), python/past_formulas.py (R247), python/surface_entry.py (R248, R249), python/data4_check.py (R250).
- Round DATA-5 (R251 to R260): python/data4_audit.py and parcel_matches.csv (R251), python/xref_search.py (R252), c3d_window.py --dxf-folder and python/dxf_count.py (R253, R254), python/road_layouts.py (R255), python/area_key.py (R256), python/im_codes.py and config/area_codes.json (R257), python/ifc_header.py (R258), python/coverage.py (R259), python/data5_check.py (R260).
- Round 17 (R261 to R282, R260 is DATA-5's): placer_check.py --split (R261 to R263), plan_from_past.py --names (R264), rvt_info.py (R265, R266), python/past_all.py and past_all.csv (R267 to R269), python/entry_gaps.py (R270), python/majority.py writes config/overrides/roshn-makkah.json (R271 to R276), levelProfiles and hands in rules/roshn-makkah.json (R277), python/plan_rules.py and plan_from_book.py --level-profile --overrides (R278 to R280), python/r17_check.py (R281), python/hand_compare.py with hands.oldFiles (R282).
- Round ADDIN (R300 to R399): levelProfiles.default parsons2024Rev6 (R277) with overrides of its own job (R280), drawings.referenceOnly and the villa block guard project_profile.read_villa_blocks (R300), config/area_codes.json by job read by python/im_codes.py (R301), testHost.imageStepMinutes and imageMarginMinutes for the image job (R302), R303 withdrawn (Q064), dotnet/Directory.Build.targets with the short intermediate name of the startup config so a clone under a deep folder builds (R304), dotnet/SiteAdaptation.TestHost/WindowSamples.ps1 samples every test host window (R310), the site facts of the reader built by SiteFacts in Common (R311), python/key_copies.py with answerKeys (R312), prototypeSets and dotnet/SiteAdaptation.TestHost/PrototypeSets.ps1 for the STR prototypes in Prototypes_2024\STR (R313), the spot value, type and elevation base of the reader (R320), floors.keyTypes in config/project.json for the FFL Generic floors of IME and IMF (R321), python/key_reads.py counts each key read (R322) and python/key_reads_check.py checks it by the floor top under each spot and the per plot rule (R323), plan_from_past.py --answer-key --names --key writes the plan of each key with keyPlans.entryHeightM (R325), python/key_join.py joins each key plot to its face by area share and past_all.py writes past_all.csv with source, kind and heightSource, pastAll.scheduleKeys and majority.kindLeaveOut keep the SE rows out of the level rules (R326), python/entry_spots.py compares the spots on a plot's own floors with El @ Entry by entrySpots in config (R327) and python/key_plans_check.py checks them a second way (R328), python/override_source.py gives each override its job in source and its model apart, a build applies parsons only, majority.overrideKeep keeps native and the level fields of 3c (R330), python/key_rules.py measures the coordinates, link frame, hands, ramps and floors, walls and parameters of the Parsons keys into keyRules of levelProfiles.parsons2024Rev6 (R331 to R335), python/key_rules_check.py checks them a second way (R336), plan_from_book.py builds a parsons2024Rev6 plot by keyRules and python/build_proof.py holds that build against every key plot (R337), python/key_rules_block.py writes keyRules from majority.keyRulesBlock in config (R338), python/key_levels.py measures the levels of the keys as a build gives them at majority.levelPassMm with the bands of levelBandsMm, a miss with no value goes to the cannot list of the override file (R339), the build places the levels by keyRules and build_proof.py gives the holds by rule and by override apart (R340), key_rules_check.py --landxml checks them by its own text route (R341), docs/licences.md lists every library with its licence, its load check and what Revit 2024 ships (R342), docs/addin-design.md is the design Phases 4 and 5 build, with the parity tolerances in its section 5, the Phase 5 open list in its section 9 and the schedule fit as the numbering of every build (R343), python/tests/test_licences.py and test_addin_design.py hold them.
- Round ADDIN Phase 4 (R345 to R388): Common on netstandard2.0, CadReader the worker and the tests on net48 and net8.0 with a Linux leg in CI (R346), the worker run in Common/Worker run by CadReader.exe --request, inputs read through WorkerContext with free memory checked first, outputs written with RunStamp and renamed only after every check (R347), python/parity.py with plan_from_book.py --steps compares `step_<step>_<side>.json` by the tolerances of addin.parity in config, python/face_rings.py and FaceRings refuse a ring that is not simple (R345), common.fixed and Rounding.Fixed are the one rounding rule, majority.overrideRounding names the rounding each level profile's overrides were measured on (R385), override_source and plan_rules keep use and skip and a hand edit one by one (R386, R330), python/cad_units.py gives majority.py and plan_from_book.py the CadReader outputs cadReads names through the units check and tableUnits gives the units of the data book tables (R388), python/location_plan.py --check control fits the control points by the marks of their shape and their similarity, then the refine, as a check of the schedule fit that numbers nothing (R387, Q081), a front corner takes a road level only from a road surface that covers it within siteBuild.snapM through c3d_connect.road_level, a plot with none is listed in noRoadLevel with siteBuild.roadFolder (R344, Q073, Q082). The worker read command of button 2 (R348 to R350): CadReader WorkerReads reads the drawings (reference only and not Meters refused), the road surfaces by LandXmlReader and TinSurface with the road level of R344, the DLUS and sales by XlsxSheet as openpyxl reads them, through SheetRows, ReadSteps and ExactDouble of Common, python/read_steps.py writes the Python side, parity.workbook_inputs refuses a workbook config does not name, addin.workerPeakMiB.commands holds the whole read peak checked with the floor (R347, Q083), c3d_run.cad_reads gives the probe settings their cadReads folders (R388).
- Round ADDIN 4d (R351 to R353, Q084): CadReader PdfPage and LocationPlan of Common with python/face_index.py and FaceIndex, a face past the cell range on the wide list (R351), python/plot_table.py and PlotTable with addin.plotTable (R352), python/position_file.py and ProjectPositionFile with the command position, CadReader --exact and plots_exact.json, the read job step keyPoints with KeyPointSteps and KeyPointsFile, each key read held to its key and to keyRules.site, a face numbered by the client code rows of labels.csv, a point failed on a vertex merged over the limit, four residuals open as Q088 (R353), python/key_parameters.py into addin.sharedParameters with each file held to its key and hand entries refused until Q087 (R353), WorkerMemory.ReadOrder and Release with addin.workerPeakMiB.commands.read (Q084), data_book.build takes the analysis.json of dataBook.join.analysis in python/c3d_probe.json (R166).
- Round ADDIN 4e (R354 to R357, Q089 to Q094): the build steps clusters, fronts, hands and linkFrame of plan_from_book.py --steps and of the worker command build, BuildSteps, SiteClusters with HoldAnalysis, BuildFace, BuildRules, Fronts with the box tree of RoadLines, Hands, LinkFrames, PyMath and PyJson in Common/Plan with StepValue of StepFile, plan_from_book.hold_analysis, hand_settings and number_settings, plan_rules.check_link_fits and BuildRules.CheckLinkFits, CsvRows strict UTF-8 as Python csv reads, a scope that numbers no plot refused by plan_from_book.scope_plots and PlotScope, parity.area_tolerance with AddinConfig.ParityTolerances, parity.listed_inputs over the prototype folder of pastJob, the snapshot analysis.json an input held to the reads it was made from (Q089), the prototype folder residuals open as Q090, the JSON reads of the worker as Q091, the settings and keyRules values a build takes unchecked as Q092, the CSV reads that still differ as Q093 and the scope and tolerance refusals that still differ as Q094, python/tests/test_build_steps.py and BuildStepTests.cs.
- docs/rules.md holds the rules with ids R1 and up. docs/addin-mock.md holds the approved mock of Round ADDIN.
- steps/log.md holds the round log. steps/PROGRESS.md holds the live state. steps/history/ holds old rounds, steps/investigations/ the findings of a round, steps/questions-closed.md the closed questions.

## Rules in short

The full text is docs/rules.md. Ids are the contract, a test names the id it proves.

- R1 Read only. A reader never changes the file it opens.
- R2 Numbers are rounded to 3 decimals, El @ Entry and Height keep 4.
- R3 Small output. Only the fields the rules list.
- R9 Excel reads from the header row with Plot No. and stops at the first row with no plot number.
- R10 No invented data. Tests use small dummy files built in code.
- R11 Zero tests is a failure.
- R12 Every rule has a test whose name carries the rule id.
- R16 and R17 cover the Revit links, walls, floors and ramps. R27 covers the link summary. R18 to R26 cover the main guard, explode, layer filter, texts and xrefs, plot labels, PDF rows, the link table, the full run and the harness files.
- R28 to R33 cover the plot number shift, the Master Plan skip list, the dropped plot type check, the second source check, the workbook survey and the face reasons.
- R34 to R39 cover the floor corners, the corner source and slope, the spot elevations, the floor and wall plot numbers, the link name parse and the link contents default.
- R40 to R46 cover the LandXML reader, invisible faces, the surface lookup, the surface roles, the ramp road levels, the gap bands and the road stages with their second check.
- R47 to R50 cover the ramp corner levels, the role proposal, the floor count per plot and the one error line for floors with no Plot parameter.
- R51 to R56 cover the shared coordinates, the floor measurements, the parameter details and floor types, the link state, the spot host and the output size. R57 to R61 cover the plot relation report, the yard and shared edges, the ramp geometry, the gaps and link box, and the summary.
- R62 covers the project profile in rules/ and its loader. R63 covers the project settings. R64 covers the cluster derivation. R65 and R66 are checks 1 and 2.
- R90 is the block as a cluster hint. R91 and R92 cover the road level lookup and the ramp test with it. R93 to R96 cover the entry level measurement, rule, proof and its run through the level rule. R97 to R101 cover the block offsets, the fit and test, the entrance point, the measured offsets file and the second methods.
- R67 is the level rule, check 5. R68 to R72 cover the block data, the unit code per plot (the Excel unit only), the row split, the front by road edge and the xref lookup. R73 to R77 cover the block scan, the Excel unit, the clusters with no Rev 6 match, the second block match and the block against the link.
- R80 to R82 cover plan.json, the plan from the past job and the shared position proof. R83 to R88 cover the placer: coordinates check, link placement, parameters, dry run and one transaction, report and skip, install. R89 compares the plan with the placed model.
- R102 to R110 cover the placer filter, the test host guard, its steps, the placer check, the registry hook, the link instance name, the security dialogs, the prototype upgrade job and the parallel windows.
- R135 to R139 cover the floor outlines in the link frame, the ramp type and front level, the floors of the CAD plan, their compare and their second method.
- R131 to R134 cover the villa hand test, the CASE second letter test, the FFL split test and their second method.
- R125 to R130 cover check 3 (one FFL per cluster), the levels of the CAD plan, the ramp outline test, the plan from CAD, the plan against plan compare and their second method.
- R120 to R124 cover the plot cut list, the corner test and origin offset, the rotation from the front edge, the mirror by the drawn side with the CASE count, and their second method.
- R160 to R168 cover the full window runner, its dialog watcher, the data command, the Phase 1 border, the stand-in tables, the workbook sheets, plots.csv, the analysis and its second method. R169 to R173 cover the export of every surface, El @ Entry from the road and the ramp, the units per face, the width keys and their second method. R174 to R178 cover the wiring methods, their score and pick, the weak spots and drawing to drawing checks, the chosen wiring and its second method. R179 to R184 cover the location plan text, label pairs, page to site fit, number to face and code, zones and wiring, and their second method.
- R111 to R119 cover the yard rule, the floors in the plan, the floor step and its stops, the plot value and skip with the ramp corners, the floor geometry, the floor run and report, the floor jobs of the test host, the work file life cycle and the floor check.
- R251 to R260 cover the DATA-4 audit with the parcel matches, the xref search, the DXF of a copy and its count, the readers against that count, the two road layouts, the area key, the IM codes and their table, the IFC header, the coverage table and their second method.
- R240 to R250 cover the Civil 3D dialogs by rule 3, the CadReader header, the surface survey, the parcels of a drawing and their compare, the rooms and doors of a read, the villa hand, the past schedule formulas, El @ Entry by surface version for the schedules and the past key and their second method.
- R185 to R198 cover the Rev 9 workbook diff, the DLUS setbacks, the land widths, the code table, the Rev 9 drawing read, its PDF labels and wiring, its faces against the baseplan, the cross sections in full, El @ Entry by its terms, the unit blocks on no plot, the location plan DWG, Civil 3D under the shared lock and their second method.
- R200 to R218 cover the window lock, the snapshot, the originals, file info from bytes, the site upgrade, the read, the answer key and its cover, the prototype choice, the plan from the data book with estimated levels and ramps, the site coordinates, the new site file and its name, the plan view image, the compare in bands before and after Revit, its second method and the clean up.
- R220 to R236 cover the reader rotation, the levels of two past jobs, the road of the ramp fronts, the gap per unit, the corner road edges, the corner front rules and their use in the plan, the house outline, the footprint test and its key band, the plan of a whole neighbourhood, one run per prototype code, the links of an image, the second method and the clusters from the footprint blocks.
- R261 to R282 cover the split placer check with the footprint and the Native dialect, the names that are no plot, the shortened TransmissionData and the storage times, the coverage, the model inventory and past_all.csv, the El @ Entry gaps between keys, the majority rule with its context, decisions and override list, the level profiles, the rules and overrides in a plan from CAD, the second method and the hands against a Native key.
- R300 to R304 cover the villa blocks from the Building Footprint with R06_0000 for reference only, the area codes by job, the image step limit from the config, the withdrawn watcher through the job (Q064) and the short intermediate name of the startup config so a clone under a deep folder builds. R310 to R313 cover the memory samples of every test host window, the site facts of the reader, the key copies by code with their JSON manifest and the prototype sets of the upgrade job. R320 to R323 cover the spot value of the reader, the floor types per key, the counts of each key read and their second method. R325 to R328 cover the plan of each key, the join by area share with past_all.csv and the area codes, the spots at the plot entrance and their second method. R330 to R338 cover the override source and keep, the rules measured on the Parsons keys, their second method, a build by keyRules with its proof and the writer of keyRules. R339 to R341 cover the levels of the keys, the build of the levels with its proof and their second method. R342 and R343 cover the licences and the add-in design, the mock sentences word for word and no control byte in steps files. R345 to R388 are planned in docs/addin-design.md (R385 to R388 the Python changes for parity), Phase 6 takes R389 to R399. R345 to R347 and R385 to R388 cover the parity harness, the projects, the worker run, the one rounding rule, use and skip, the control point fit as a check of the schedule fit and the input units. R344 covers the road level of a front corner from a covering road surface only. R348 to R350 cover the DWG, LandXML, DLUS and sales reads of button 2. R351 to R353 cover the location plan of button 2, the plot table, and the position file with its check points against the keys and the shared parameter GUIDs of the keys. R354 to R357 cover the clusters, the front with the road levels at its corners, the link frame and the hand of a plot in a build, steps 4 to 7 of the parity harness.

## Real files

- Real drawings, sheets and workbooks stay where they are. Readers run on them in place.
- Never copy a real file into git. sample_data/ and sample_output/ are ignored, keep them ignored.
- Never commit real coordinates or levels. Logs and reports hold counts, row numbers and distances only.
- Full run outputs go to sample_output/full_run/<file name>/.
- A file that fails or times out is recorded with its exact error and the run carries on. Never swap in a dummy file.
- The Revit reader and the placer are run by Bader by hand. The placer runs only on a copy of a model.

## Commands

```
py -m venv .venv
.venv\Scripts\python -m pip install -r requirements.txt
.venv\Scripts\python -m pytest python
dotnet build dotnet\SiteAdaptation.sln -c Release
dotnet test dotnet\SiteAdaptation.sln -c Release --no-build
```

The gate is both test commands green. Run it locally before every pull request.
If `py` is missing, call python.exe from the Python 3.12 install folder.

## Writing rules

These apply to every file, commit message, pull request text and report.

- No generated-by footer and no co-authored-by line.
- No emoji.
- No em dash.
- No semicolon in prose, comments, Markdown, JSON, YAML or Python. C# statements keep the semicolon the language needs.
- No comment that restates the line under it. A comment says why.
- Keep names and titles short.
- Say what happened. If a test failed or a step was skipped, say so with the output.

## Pull requests

- One branch per round, named type/short-topic.
- Title states the round and the topic.
- Run the gate, push, open the pull request, merge it once the gate is green. Do not wait for approval when the round says so.
- Merge only after gh pr checks <number> --watch shows the python and the dotnet check both passed, never while one is red or still running.
- After a merge, delete the branch on the remote and pull main.

## Log shape

The top entry of steps/log.md fits on one page and has these headings in this order.

1. Goal
2. Done
3. Checked
4. Remains
5. Known bugs
6. Next
7. Your turn

Your turn is one list of every open question for Bader by fixed ID, recommended answer first.
steps/PROGRESS.md is 60 lines at most and holds state, done, open bugs, Bader's turn and the next three steps.

## Questions

- Every question for Bader has a fixed ID, Q001 upward. Never renumber an ID and never reuse one. A new question takes the next free ID.
- Bader's turn in steps/PROGRESS.md and in the top entry of steps/log.md list the open questions by ID with the same text in both.
- A closed question moves to steps/questions-closed.md with its answer and the date, and keeps its ID. That file also maps old round numbers to IDs.
- Steps files and reports name a question by its ID, never by a list position.

## Majority rule

Bader's standing rule since Round 17, for every round.

- For every decision (ramp type, flat ramp, hand, front edge, levels, steps, yard, parameters) find the logic most plots of the past models follow and code it.
- Do not stop on odd plots. Each plot the coded rule does not reproduce goes into config/overrides/roshn-makkah.json with the plot, the field, the value the past job used, the source model and the reason if known.
- The tool applies the rule, then the override list. Bader edits the override list by hand.
- A rule is strong when it holds on at least the share set in config (majority.strongShare in config/project.json, 0.90 to start). If no logic reaches it, code the best one, mark it weak in Bader's turn and keep going.
- No project value in code. Rules and thresholds go in config and the rules profile.

## Checking work

- Check each output a second way, with a different tool or method than the reader uses.
- Report counts of matches and mismatches.
- Open a handful of random items by id and compare them across sources.
- Never resolve a conflict silently. Write it to a conflicts list.

## Hooks

.claude/settings.json holds a hook that stops git commit on main and git push to main.
.claude/hooks/guard-registry.sh blocks every registry change (R106). Nothing in the registry is read or written.
A hook only loads when a session starts in the repo. A session that started elsewhere has no hooks, say so in the log.
