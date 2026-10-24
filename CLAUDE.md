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

## Repo map

- python/ holds the PDF reader, the Excel reader, the link table, the link summary, the second source check, the face reasons, the LandXML reader, the surface lookup, the ramp levels, the surface roles, the road check, the plot relation report (plot_relations.py), the plan builder (plan_from_past.py), the plan compare (plan_compare.py) and the full run script, plus tests in python/tests.
- config/plot_numbers.json holds the plot number shift of each neighbourhood. config/second_source.json names the plot layers of the master plan drawing, which drawings.masterplan in config/project.json names.
- dotnet/CadReader reads DWG and DXF with ACadSharp. It also writes blocks.json, one entry per block insert with scale, rotation and a world outline (R68).
- dotnet/RevitPrototypeReader is the Revit 2024 add-in. Do not change it unless the round says so.
- dotnet/SiteAdaptation.Placer is the Place from plan add-in. dotnet/SiteAdaptation.TestHost runs the placer and the reader in Revit with no clicks, only for a job of its run script (R103, R104). dotnet/SiteAdaptation.DialogWatcher answers the add-in security boxes with Do Not Load (R108). python/placer_check.py checks a test host run (R105).
- dotnet/SiteAdaptation.Placer is the second Revit 2024 add-in, Place from plan (R83 to R88). Its math and decisions live in SiteAdaptation.Common behind IPlacerTarget, the Revit side only carries them out. Its second step, Floors, lives in FloorPlacer behind IFloorTarget (R113 to R116).
- python/yard_rule.py gives each yard floor its plot by baseplan share (R111). config/project.json floors names the floor types, levels and Plot parameter (R112). python/scenario_files.py makes the test master and the work files of the floor scenarios and deletes them (R118). placer_check.py --floors checks a floor scenario (R119).
- python/plan_from_past.py builds plan.json (R80) from a past job, prototypes.json and the schedule, named by pastJob in config/project.json (R81). The plan goes outside the repo. python/plan_compare.py compares a plan with the model the placer built (R89).
- dotnet/SiteAdaptation.Common holds JSON output and unit helpers.
- config/project.json names the rules profile and the drawings, and holds the CAD analysis settings (R63). No project name goes in a Python file.
- rules/ holds one project profile JSON per project and a README with the checks not coded yet. python/project_profile.py loads a profile by path (R62). New project rule values go there and not in code.
- python/cad_analysis.py derives the clusters of duplex and townhouse plots from CAD adjacency and writes analysis.json (R64). It gives each plot a unit code from its block or its schedule row (R69, R74), cuts long rows into listed clusters (R70) and takes the front of a row from the road edges (R71). python/rules_checks.py holds the coded checks (R65, R66).
- python/road_lookup.py asks the LandXML surfaces in a fixed order and the first that covers a point answers (R91). python/entry_levels.py holds the entry rule measured on the past job (R93, R94) and python/entry_proof.py measures it and proves it on the 747 schedule plots (R95). python/block_offsets.py measures where the link and the entrance of a unit sit in its footprint block (R97 to R99). config/prototype_offsets.json holds those measured values and python/prototype_offsets.py reads and writes it (R100). python/round13_check.py checks the lookup and the offsets a second way (R101).
- python/cad_links.py measures the past links in the frame of their baseplan face: the cut list, the corner test and origin offset, the rotation from the front edge, the mirror by the drawn side and the CASE count (R120 to R123), and writes plotFrame into config/prototype_offsets.json. python/cad_links_check.py checks it a second way with json and math only (R124).
- python/plan_from_cad.py builds plan.json from CAD and the schedule with links and levels (R126, R128), python/ramp_outline.py tests the Rev 6 ramp outline on the past ramps (R127), plan_compare.py --against-plan compares two plans (R129) and python/cad_plan_check.py checks them a second way (R130). check_one_ffl in python/rules_checks.py is check 3 (R125).
- python/cad_rules.py tests the villa hand, the CASE second letter and the FFL split on the past job (R131 to R133), python/cad_rules_check.py checks them a second way (R134).
- python/floor_fit.py measures the Generic and yard outlines in the link frame, the ramp type bands and the ramp front level into config/floor_outlines.json (R135, R136). plan_from_cad.py --landxml-dir writes the floors (R137), plan_compare.py --against-plan compares them (R138) and python/cad_floor_check.py checks them a second way (R139).
- python/levels.py gives the FFL, SSL and steps of a cluster (R67, check 5). python/levels_proof.py compares it with the two past schedules in the input folder and copies nothing.
- python/block_scan.py counts the block inserts of the footprint drawing against the baseplan plots (R73). python/block_proof.py checks the block to plot match a second way with no geometry library (R76). python/link_proof.py compares the block mirror flag and rotation with the Revit links (R77).
- python/c3d_window.py and dotnet/SiteAdaptation.C3DWatcher run the full Civil 3D read of a data book copy (R160, R161). python/data_book*.py build and check the data book of Phase 1 outside the repo (R163 to R168), data_book_retest.py tests El @ Entry, the units per face and the width keys on it (R170 to R172) and data_book_retest_check.py checks them a second way (R173). python/wiring.py scores the ways each source wires to the plots and data_book.py wires with the chosen one (R174 to R177), python/wiring_check.py checks it a second way (R178). python/location_plan.py reads the Zone 4 location plan, fits it to the site and ties every plot number and code to its face and to the Rev 9 sales sheet (R179 to R183), python/location_plan_check.py checks it a second way (R184).
- Round DATA-5 (R251 to R260): python/data4_audit.py audits DATA-4 and rev9_parcels.py writes parcel_matches.csv (R251), python/xref_search.py looks for every xref by name (R252), c3d_window.py --dxf-folder writes a DXF of a copy and python/dxf_count.py counts it against both readers (R253, R254), python/road_layouts.py gives the facts of the two road layouts (R255), python/area_key.py gives every plot Zone and NBH from the DLUS (R256), python/im_codes.py finds the IM codes and config/area_codes.json names them (R257), python/ifc_header.py reads an IFC copy as text (R258), python/coverage.py writes the coverage table (R259) and python/data5_check.py checks them a second way (R260).
- Round DATA-4 (R240 to R250): the Civil 3D watcher answers by rule 3 and never purges (R240), CadReader --header-only gives dates and classes (R241), python/surface_versions.py lists every DWG and XML with the surfaces it holds (R242), the probe reads parcels inside blocks and python/rev9_parcels.py compares them (R243, R244), a read job of the test host writes the rooms and doors and may read a test copy of handCheck.folders (R245), python/villa_hand.py gives the hand of a prototype (R246), python/past_formulas.py reads the formulas of the past schedules (R247), python/surface_entry.py tests El @ Entry by surface version for the schedules and the past key (R248, R249) and python/data4_check.py checks them a second way (R250).
- Round DATA-3 (R185 to R198, R195 unused): python/rev9_workbooks.py inventories and diffs the two Rev 9 workbooks and joins the DLUS setbacks and the sales widths by plot (R185 to R187), typology_codes in rules/roshn-makkah.json holds the old to new code table (R188), python/rev9_drawing.py reads the Rev 9 DWG and its PDF plot against the baseplan (R189 to R191), python/cross_sections.py measures the curbs and sidewalks of the cross sections (R192), python/entry_terms.py tests El @ Entry by its terms (R193), python/stray_units.py places the unit blocks on no plot (R194), python/location_dwg.py compares the location plan DWG with the PDF (R196), python/c3d_window.py runs Civil 3D under the R200 lock with the own-only watcher (R197) and python/data3_check.py checks them a second way (R198). CadReader writes the DWG class list and the header dates.
- Round Z4 (R200 to R218): python/window_lock.py runs every window under the shared lock (R200), python/round_files.py checks the snapshot and the originals and cleans the work folder (R201, R202, R218), python/rvt_info.py reads BasicFileInfo and the links of a .rvt from its bytes (R203). The test host has siteUpgrade, read, newSite and image jobs (R204, R205, R212 to R214) run by Run-TestHost.ps1 -RoundSpec, with SiteSpec and SiteCoordinates in Common (R211). python/key_cover.py finds what a past model holds and picks the NBH and prototype (R206, R207), python/site_spec.py writes the site file (R211, R212), python/plan_from_book.py builds the plan from the data book only (R208 to R210), python/z4_compare.py compares in bands (R215, R216) and python/z4_check.py checks it a second way (R217). siteBuild in config/project.json holds the round settings.
- Round 16 (R220 to R236): PlacementRotation in Common holds the rotation choice of the reader (R220). python/level_cross.py compares the levels of two past jobs and fits ramp fronts to the surfaces (R221 to R223). python/corner_front.py tests the ramp front rules of corner plots, orientation of the sales sheet is kept and plan_from_book.py uses it (R224 to R226). python/footprint_check.py checks placed links against the footprint blocks (R227 to R229). plan_from_book.py builds a whole NBH with clusters from the footprint blocks (R230, R236), python/place_codes.py places one prototype code per run (R231), the image job may load the links (R234), python/z16_check.py checks the round a second way (R235).
- Round 17 (R261 to R282, R260 is DATA-5's): placer_check.py --split checks plan position and level apart, the footprint as an edge only third check, and reads a key of the Native dialect (R261 to R263). plan_from_past.py --names maps a link whose Name is no plot by the house frame of its file (R264). rvt_info.py reads a shortened TransmissionData and the storage times (R265, R266). python/past_all.py writes the coverage, the model inventory and past_all.csv, one row per plot per past key (R267 to R269). python/entry_gaps.py checks the join of large El @ Entry gaps between keys (R270). python/majority.py runs the majority rule per decision and writes config/overrides/roshn-makkah.json (R271 to R276). levelProfiles and hands in rules/roshn-makkah.json hold the coded rules, no level profile is the default (R277). python/plan_rules.py and plan_from_book.py --level-profile --overrides apply them (R278 to R280). python/r17_check.py checks the round a second way (R281), python/hand_compare.py compares hands with a Native key by hands.oldFiles (R282). Questions have fixed IDs, steps/questions-closed.md holds the closed ones.
- config/road_surfaces.json gives each LandXML surface its role, ground, road or design, and names the ramp types and the input files.
- config/cad_layers.json is the keep and drop list of CAD layers. Bader edits it.
- docs/rules.md holds the rules with ids R1 and up.
- steps/log.md holds the round log. steps/PROGRESS.md holds the live state. steps/history/ holds old rounds.

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

1. Done
2. Remains
3. Known bugs
4. Next
5. Your turn

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
