# Site Adaptation Tool

Data readers for the ROSHN Makkah site adaptation work, and the placer, which reads plan.json and places the villa links.
The rules live in docs/rules.md and nowhere else. This file points to them and adds the working agreement.

## Roles

- Bader decides. Every open question goes to Bader as a numbered list with the recommended answer first.
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
- R111 to R119 cover the yard rule, the floors in the plan, the floor step and its stops, the plot value and skip with the ramp corners, the floor geometry, the floor run and report, the floor jobs of the test host, the work file life cycle and the floor check.

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

Your turn is one numbered list of every open question for Bader, recommended answer first.
steps/PROGRESS.md is 60 lines at most and holds state, done, open bugs, Bader's turn and the next three steps.

## Checking work

- Check each output a second way, with a different tool or method than the reader uses.
- Report counts of matches and mismatches.
- Open a handful of random items by id and compare them across sources.
- Never resolve a conflict silently. Write it to a conflicts list.

## Hooks

.claude/settings.json holds a hook that stops git commit on main and git push to main.
.claude/hooks/guard-registry.sh blocks every registry change (R106). Nothing in the registry is read or written.
A hook only loads when a session starts in the repo. A session that started elsewhere has no hooks, say so in the log.
