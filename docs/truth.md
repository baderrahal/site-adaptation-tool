# The truth

The one source of truth of the Site Adaptation Tool, from 12 Nov 2026. Where another file disagrees with this one, this one is right and the other is fixed or listed in steps/rules-vs-truth-12nov.md. A line marked "not built yet" is decided by Bader and waits for its round in steps/plan.md.

## What the tool does

A Revit 2024 add-in. It reads the site drawings, the schedules and the villa files of a project, and builds the site model in Revit: levels, floors, ramps, yards, villa links and walls. Then it checks the model and writes the Excel reports. The goal from 12 Nov: it builds every neighbourhood of the old project, Phase 1, from an empty model, and each result is measured against the past Revit models.

## The 3 cards

The pane shows 3 cards. They are the only way in. Each click runs the old numbered steps behind it, which stay as the engine.

| Card | Click | What it does |
|---|---|---|
| 1 Project data | Choose project folder | Takes the folder of the project, sets it as the input folder of this project, runs step 1 Set up site, then step 2 Read data on the folder |
| 1 Project data | 1 Set up site | Step 1 alone: units, position, Level 1 and Level 2, floor and wall types, shared parameters |
| 1 Project data | Fix items | Shown when the data check has rows. Opens the window where a person confirms or corrects what the tool read |
| 1 Project data | 3 Review data | Step 3: every plot against its sources, the team marks each one. Nothing changes in Revit |
| 2 Build site | Build | Makes the plan, shows the preview when Preview first is ticked, then runs steps 1 (if not done), 4 Check, 5 Floors and ramps, 6 Levels, 7 Place villas, 8 Walls in that order. A step that waits on an earlier step runs that one first. It asks once to use the manual template when the project has no rules |
| 2 Build site | Preview first, a tick | On by default. Shows what Build will do before it changes the model |
| 3 Review and deliver | Report | Step 9: writes the Excel report into the work folder of the model |
| 3 Review and deliver | Update from report | Reads the Excel a person edited and updates the model from it |
| 3 Review and deliver | Open rules | Shown when a rule has an issue. Opens the Rules window |

Proof: dotnet/SiteAdaptation.AddIn/Pane/StagesPage.xaml lines 146 to 213, StageCards.cs lines 264 to 301, BuildChain.cs line 14, App/PaneHost.cs lines 151 to 205 and 423 to 489.

Today an Advanced part under the cards still holds the whole old pane with one button per step (StagesPage.xaml lines 219 to 221, App/RibbonLayout.cs lines 20 to 39). Bader's decision of 12 Nov: Advanced no longer shows the 9 step buttons. Not built yet, round 1 step d.

## Settings

The gear at the head of the pane opens two rows: Project setup, Edit, the New project window, and Rules, Open, the Rules window. Every setting has a window, no one edits JSON by hand. The setup is saved inside the Revit model, so it is per project (Revit/SettingsEntity.cs lines 8 to 10).

| Setting | What it sets | Default |
|---|---|---|
| Start from | The settings and rules the project starts from | Empty project |
| Site drawing (CAD), Plot list (Excel), Villa levels (Excel), Road levels (civil file), Parameter sheet (Excel) | The input files | none, the folder read proposes them |
| Pick surfaces | Which road surfaces give the road levels | none picked |
| Report file name, 10 fields | The name of the report Excel | the values of config |
| Survey point, Plots found layer, Villa type sheet and column | What the scan proposed and a person confirmed | none |
| Before Build | Warn and build, or Stop the build, when a rule is not confirmed | Warn and build |
| Preview first | The preview before Build | on |

Still in Advanced today, leaving with it in round 1 step d unless a card needs them: Level profile, Scope (Whole project, Neighbourhood, Plot list), Dry run first, Villa mode (Masses, Links), Road topography, Pre-setting. Which of these move to a card or a window is CHOSEN by Code in that task and listed for Bader.

Decided on 12 Nov, not built yet, round 2: a tick "Join surfaces for the road toposolid" under Road levels, on by default. On: the surfaces the front corners took are joined into one toposolid, and the first picked surface wins where two overlap. Off: one surface, and card 3 lists the plots with no toposolid. Saved per project. Card 3 names the surfaces used.

The one setting per PC is the work root, in %APPDATA%\SiteAdaptation\machine.json, written by scripts/set-machine.ps1.

## How rules work

- Each project holds its own rule set, a copy made from the manual template rules/templates/manual.json when a person presses Use the manual template, or when Build offers it.
- The Rules window, title "Rules of this project", shows each rule as Rule, Value, Source and Status, with Confirm or Save on the row.
- Value: what the tool uses. Source: where it comes from, one of rule, default, computed, person entered, missing. Status: not confirmed, confirmed, or fixed.
- Every rule starts not confirmed. A person confirms it, or changes the value and saves, which sets the source to person entered and the status to confirmed, with who and when. Every change is written to rules_log.jsonl beside the rules.
- The rules of a project are editable per project and saved in project_rules.json under New projects of the work root. The shipped settings cannot be edited.
- Three rules are fixed and cannot be changed: the Saudi checks, no guessed value, every change logged.
- Decided on 12 Nov, not built yet, round 2: a manual rule a person changed shows "Changed from manual Rev 06" and the old value.
- The level rules of a new build are the Rev 06 profile, parsons2024Rev6 (Q050). native2022 stays for reference only.
- Every rule in docs/rules has its id, a plain name, its input, its output and its test. docs/rules/index.md finds a rule by id.

Proof: dotnet/SiteAdaptation.AddIn/RulesWindow, dotnet/SiteAdaptation.Common/ProjectRules/ProjectRulesFile.cs lines 19 to 44, RulesChange.cs lines 45 to 89, RulesSetup.cs lines 9 and 31.

## Where every file lives

| What | Where | Rule |
|---|---|---|
| The real inputs of the old project | C:\02-Site Adaptation | Read only, read in place, never copied. SHA-256 before and after every run |
| The villa files the build links | 00_Revit Models\Zone 02\ARC\Prototypes_2024 of the input folder, 13 Revit 2024 files | config/project.json pastJob.upgradedPrototypeFolder |
| Past outputs inside the input folder | 01_Output, Revit All Zones, Native Files, the site models of 00_Revit Models | Skipped by settings when the tool reads the folder. 01_Output is skipped today (dataCheck.skipFolders), the other three not yet, round 1 step b |
| Everything the tool, a script, a test or a lane writes | Under the work root, C:\SA-work\SiteAdaptation | Nothing on the drive root, nothing in Documents, nothing in the repo but code and records. scripts/checks/write_places.py fails a PR that breaks it |
| The work folder of a model | <work root>\<model name>_<8 letters> | Reads, plans, logs, the report Excel, earlier reports |
| The settings and rules of a project | <work root>\New projects\<model> | config, rules copy, project_rules.json, rules_log.jsonl |
| The answer key | <work root>\answer-key\<zone model> | What the 7 past models hold, for compare only, never in git |
| Road surfaces as LandXML | <work root>\landxml | Made in round 1 step a |
| Test host runs, lane logs, temporary files of tests | <work root>, its folders round_addin, lanes, tmp, checks | |
| The installed add-in | %APPDATA%\Autodesk\Revit\Addins\2024\SiteAdaptationAddIn | With the worker and the shipped settings and rules |
| Code and records | The repo | Public. No real coordinate, level, plot data or client file in it |

The work root is read from machine.json by dotnet/SiteAdaptation.Common/WorkRoot.cs. Its last folder must be named SiteAdaptation. On a PC with no machine file the code still falls back to Documents\SiteAdaptation (WorkRoot.cs lines 57 to 60), which disagrees with the rule above and is listed.

One Revit at a time, this PC has 16 GB. Revit opens only copies of a model, made under the work root.

## The folders of the repo

| Folder | Holds |
|---|---|
| dotnet/SiteAdaptation.AddIn | The Revit add-in: ribbon, pane, windows, the steps |
| dotnet/SiteAdaptation.Common | The logic with no Revit type |
| dotnet/CadReader | The worker program: reads CAD, PDF and Excel outside Revit, writes Excel |
| dotnet/SiteAdaptation.Tests, UiShots, TestHost | The tests, the window pictures, the Revit test runs |
| python | Dev tools only: readers, the compare, the checks. The user's PC runs no Python |
| config, rules | The settings and the rules profile, the manual template |
| docs/truth.md, docs/rules, docs/design | This file, the rules by id, the design |
| scripts, scripts/checks | Status, quick tests, the checks of the gate |
| steps | The plan, the tasks, the Revit results, the log. steps/history holds what is over |
| tests | Acceptance, golden and hold-out tests, owned by the lead |

Round 1 step d removes what the audit found dead and labels what stays, this table is written again then.

## The pass level

Bader, 12 Nov 03:25. A neighbourhood passes when:

- 95 percent of the plots of its past model are built.
- Of the built plots, 95 percent hold each of: villa position within 5 mm, FFL within 5 mm, rotation within 0.1 degree, mirror right.
- Floors, yards, walls and levels are each within 5 percent of the past count.
- Yards are counted on IMC, IMD, IME and IMF only. IMA and IMB count for plots, position, rotation and mirror only.

The compare is python/compare_built.py, round 1 step c. It writes steps/compare.md, counts only. A fix is done only at the pass level on its neighbourhood and on one it was not tuned on.

## Standing rules of every round

1. C:\02-Site Adaptation is read only, SHA-256 before and after every run. The tool reads it in place, with the past output folders skipped by settings. No copy of it is ever made.
2. Every file the tool, a script, a test or a lane writes goes under C:\SA-work\SiteAdaptation.
3. No vibe code. Add only what the task lists. Every function has a caller named in the PR. Every rule has its id, plain name, input, output and test. Where a task is silent, take the smallest version, mark it CHOSEN, go on.
4. One Revit at a time.

## Bader's decisions

| Date | Decision |
|---|---|
| 24 Oct | The yard floor stands 0.10 m under FFL, a team decision, the manual gives no level |
| 12 Nov | No more demo. All work on Bader's PC. The goal is the whole old project, measured against the past models |
| 12 Nov 03:25 | The copies under the work root are deleted once proven not needed, steps/storage-12nov.md |
| 12 Nov 03:25 | The plan is right, with its first four tasks as one round, OLD-1 |
| 12 Nov 03:25 | The pass level above |
| 12 Nov 03:25 | Yard floor at FFL less 0.10 m, as a rule of the project the team can change in the Rules window, never a fixed value, source "Team decision 24 Oct, the manual gives no level". Q225 is answered |
| 12 Nov 03:25 | The road surfaces are joined for the toposolid, as an option in Settings, on by default. Q600 is closed |
| 12 Nov 03:25 | No new project work: the new project path and roads from 3D solids are dropped. The installer waits until Bader asks. Whether the repo goes private waits |
| 12 Nov 03:25 | Round 1 step d: the 3 cards are the only way in. Past job mode and its override list, light mode, the data book reads and the Python copies of C# logic are removed |
