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

- python/ holds the PDF reader, the Excel reader, the link table, the link summary, the second source check, the face reasons, the LandXML reader, the surface lookup, the ramp levels, the surface roles, the road check, the plot relation report (plot_relations.py), the plan builder (plan_from_past.py) and the full run script, plus tests in python/tests.
- config/plot_numbers.json holds the plot number shift of each neighbourhood. config/second_source.json names the plot layers of the master plan drawing, which drawings.masterplan in config/project.json names.
- dotnet/CadReader reads DWG and DXF with ACadSharp. It also writes blocks.json, one entry per block insert with scale, rotation and a world outline (R68).
- dotnet/RevitPrototypeReader is the Revit 2024 add-in. Do not change it unless the round says so.
- dotnet/SiteAdaptation.Placer is the second Revit 2024 add-in, Place from plan (R83 to R88). Its math and decisions live in SiteAdaptation.Common behind IPlacerTarget, the Revit side only carries them out.
- python/plan_from_past.py builds plan.json (R80) from a past job, prototypes.json and the schedule, named by pastJob in config/project.json (R81). The plan goes outside the repo.
- dotnet/SiteAdaptation.Common holds JSON output and unit helpers.
- config/project.json names the rules profile and the drawings, and holds the CAD analysis settings (R63). No project name goes in a Python file.
- rules/ holds one project profile JSON per project and a README with the checks not coded yet. python/project_profile.py loads a profile by path (R62). New project rule values go there and not in code.
- python/cad_analysis.py derives the clusters of duplex and townhouse plots from CAD adjacency and writes analysis.json (R64). It matches unit blocks to plots (R69), cuts long rows into listed clusters (R70) and takes the front of a row from the road edges (R71). python/rules_checks.py holds the coded checks (R65, R66).
- python/levels.py gives the FFL, SSL and steps of a cluster (R67, check 5). python/levels_proof.py compares it with the two past schedules in the input folder and copies nothing.
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
- R67 is the level rule, check 5. R68 to R72 cover the block data, the block to plot match, the row split, the front by road edge and the xref lookup.
- R80 to R82 cover plan.json, the plan from the past job and the shared position proof. R83 to R88 cover the placer: coordinates check, link placement, parameters, dry run and one transaction, report and skip, install.

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
A hook only loads when a session starts in the repo. A session that started elsewhere has no hooks, say so in the log.
