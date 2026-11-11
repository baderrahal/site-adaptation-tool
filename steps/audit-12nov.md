# Audit, 12 Nov 2026

Lead, on Bader's PC, main 0c31771 of 8 Nov. Read only, nothing deleted. Three read only agents searched, Lead checked their counts against the files named. Line numbers are of main 0c31771. No Revit run was made for this audit, every Revit number is of 6 to 8 Nov on the laptop.

Counts at a glance:

| What | Count |
|---|---|
| Dead parts, nothing in product code calls them | 42: 6 C# methods, 9 Python functions, 22 Python files, 5 scripts |
| Parts in the product library only the test host or the renders call | 31 |
| Folders flagged | 13 |
| Rules in the index | 567, 472 with a test that names the id, 95 without |
| Rule ids used in code with no rule line | 3: R689, R690, R691 |
| Buttons | 35: 34 reach the code they name, 1 reaches nothing |
| Faults F1 to F42 | 26 fixed, 13 open, 2 explained, 1 UNKNOWN |
| Fixed faults seen in Revit after the fix | 21 of 26, 2 partly, 3 not seen |
| Backlog lines | 264: 245 keep, 11 done, 8 no longer apply |
| Open questions with full text in questions-open.md | 101: 98 keep, 1 done, 2 no longer apply |

## 1. Code

### 1.1 Dead code

How it was searched:
- The repo's own caller check, scripts/checks/caller_check.py, judges only the diff of a pull request. On main it prints `NOT RUN caller check: not a pull request`.
- A scan of the whole repo by name, kept outside the repo, on the idea of count_uses (scripts/checks/caller_check.py:75): strings and comments taken out, XAML read as it is, a test is no caller. 7,918 declarations in dotnet, python, scripts and .claude, 72 with no use by name in product code. Each of the 72 was sorted by hand and the ones below checked again with `git grep -n -w`.
- Limits: the scan matches names and is no compiler. A dead method that shares its name with a live one is not found, so the true count is likely higher, by how much is UNKNOWN. A call by reflection reads as no call. Dead code reached only from dead code was not traced.

| # | Where | Why |
|---|---|---|
| 1 | dotnet/SiteAdaptation.AddIn/CheckTable/CheckTableSession.cs:66 PickDoorLevel | Only tests call it. The window sets the value through CheckTableViewModel.cs:196 |
| 2 | dotnet/SiteAdaptation.AddIn/Commands/Buttons.cs:17 NotBuiltText | One test file names it. Left from phase 5a, the comment at Buttons.cs:10 is stale |
| 3 | dotnet/SiteAdaptation.C3DProbe/TinBorder.cs:49 VertexAt | No caller and no test |
| 4 | dotnet/SiteAdaptation.Common/Read/LocationPlan.cs:410 Classify | Outside tests only its own line |
| 5 | dotnet/SiteAdaptation.Common/Review/ReviewFile.cs:129 ClusterUnconfirmed | One test file calls it. Its comment (R442) says buttons 5 to 8 skip these plots. Whether another function does that skip is UNKNOWN. It may be a missing wire, not only dead code |
| 6 | dotnet/SiteAdaptation.Common/TestHostJob.cs:164 JobPath | Called from 4 test files only |
| 7 | python/key_rules_check.py:856 cluster_side | No caller, no test |
| 8 | python/plan_walls.py:508 ffl_of | No caller, no test |
| 9 | python/road_lookup.py:79 default_config_path | No caller, no test |
| 10 | python/wiring_check.py:105 ring_distance | No caller, no test |
| 11 | python/override_source.py:292 list_path | Tests only |
| 12 | python/prototype_offsets.py:58 group_index | Tests only |
| 13 | python/record_parts.py:41 cap_breaches | Tests only |
| 14 | python/surface_lookup.py:133 lookup_z and :138 lookup_many | Tests only, 2 functions |
| 15 | tools/run-lane.ps1 | The old lane A and B runner of 29 Oct. scripts/lanes/run_lane.py is the runner now |
| 16 | scripts/sign/Measure-ColdStart.ps1 | Named by no file outside steps and docs |
| 17 | scripts/new-project-intake.ps1 | Named only by itself outside steps and docs |
| 18 | scripts/records/split_record.py | Named only by itself and one history file |
| 19 | scripts/scenarios/build_second_check.py | Imported only by python/tests/test_site_build_scores.py:8 |
| 20 | python/split_proof.py | No importer and no test |
| 21 | dotnet/SiteAdaptation.RevitTests, 2 files | Not in dotnet/SiteAdaptation.sln, so the gate never builds it. Built only by scripts/sign/Run-RevitTests.ps1, which nothing names |
| 22 | dotnet/SiteAdaptation.C3DProbe/Civil | Not in the solution, its own line 3 says it is built by hand where Civil 3D is installed. Explained, not dead |
| 23 | dotnet/RevitPrototypeReader/ExportPrototypesCommand.cs:15 | A command from before the one add-in. Named only by its own manifest, whose Assembly is a placeholder path. The project lives only through the test host (SiteAdaptation.TestHost.csproj:11) |
| 24 | dotnet/SiteAdaptation.Placer/PlaceFromPlanCommand.cs:15 | The same. The add-in does not reference the placer, only the test host does (SiteAdaptation.TestHost.csproj:10) |

Python files that only their own test and docs name, 21, each a check run by hand in a past round: block_proof, block_scan, c3d_inventory, c3d_window, cad_floor_check, cad_rules_check, data4_audit, data_book_border, data_book_excel, data_book_readme, hand_compare, key_reads_check, link_proof, phase4_check, r17_check, road_check, scan_check, xref_search, z16_check, z4_check, zone_compare, all python/<name>.py. With split_proof.py that is 22. Four more are named outside tests only in a C# comment as the second route and nothing runs them: new_project_check, report_excel_check, site_spec, update_check.

Test run support that ships inside the product library, 31 parts with one product line each, their own declaration: in dotnet/SiteAdaptation.Common the files AddinRun.cs, AddinRunGate.cs, ButtonShots.cs, FreezeWindows.cs, RevitListener.cs, SiteSpec.cs, TestHostFiles.cs, TestHostJob.cs, TestHostRun.cs and WorkRoot.cs:91, plus PaneViewModel.cs:454 SetSteps and PrototypeReading.cs:47. Those files hold 3,672 lines. Recommendation: move them to a library only the test host loads.

### 1.2 Folder structure

Tracked files by `git ls-files`, 1,450 on main.

| Folder | Files | Holds | Flag |
|---|---|---|---|
| root | 5 | CLAUDE.md, README.md, requirements.txt, .gitignore, .gitattributes | |
| .claude | 76 | agents 9, hooks 11, rules 5, skills 48 of which ai-max 42, 3 loose | .claude/scheduled_tasks.lock is a tracked lock file of 24 Oct. Recommend untrack and ignore |
| .github/workflows | 2 | tests.yml the gate, test.yml the Linux legs | |
| config | 12 | project.json, layer, plot, offset, road and rule value files, the parameter sheet | |
| config/overrides | 1 | the override list of the past job | |
| docs | 5 | addin-mock, lessons, licences, repo-map, signoff | |
| docs/design | 26 | the add-in design | |
| docs/rules | 33 | 32 rule parts and index.md | |
| dotnet | 3 | the solution and the two Directory.Build files | |
| dotnet/CadReader | 21 | the worker | |
| dotnet/RevitPrototypeReader | 7 | the old prototype export command | Lives only through the test host. Recommend fold what the test host needs into it |
| dotnet/SiteAdaptation.AddIn | 156 | the add-in | |
| dotnet/SiteAdaptation.C3DProbe | 10 | the Civil 3D probe, a second project under Civil outside the solution | |
| dotnet/SiteAdaptation.C3DWatcher | 2 | closes Civil 3D boxes for the probe | |
| dotnet/SiteAdaptation.Common | 244 | logic with no Revit type, and the test run support of 1.1 | See 1.1 |
| dotnet/SiteAdaptation.DialogWatcher | 2 | closes Revit boxes for test runs | |
| dotnet/SiteAdaptation.Placer | 7 | the old placer command | As RevitPrototypeReader |
| dotnet/SiteAdaptation.RevitTests | 2 | one test in real Revit | Not in the solution. Recommend add it or write why not |
| dotnet/SiteAdaptation.TestHost | 27 | Revit test runs | |
| dotnet/SiteAdaptation.Tests | 221 | gate tests | |
| dotnet/SiteAdaptation.UiShots | 10 | window renders | |
| python | 146 | 145 .py and c3d_probe.json, all flat | 22 files only their test names. Recommend a subfolder for the checks of past rounds |
| python/tests | 142 | pytest files | |
| rules | 4 | the rules profile and two templates | Three places carry rules by name: rules, docs/rules, config/rule_values.json. Recommend rename rules to say profile |
| samples/new-project | 6 | a made up project for the clean folder test | |
| scripts | 4 | status.ps1, quick-tests.ps1, quick-set.txt, new-project-intake.ps1 | |
| scripts/checks | 18 | the checks of tests.yml and their own tests | |
| scripts/lanes | 1 | run_lane.py | |
| scripts/records | 1 | split_record.py | Unused, 1.1 |
| scripts/scenarios | 11 | scenario build and scoring | |
| scripts/sign | 4 | signing and the RevitTests runner | |
| steps | 49 | state, log, questions, plans, and old round sheets | round-12.md, round-12b.md, round-13.md, round-15a.md, bader-16.md, bader-17.md, bader-z4.md, for-bader-round-9.md, START.md, START-B.md, 01_next.md, lane-b.md are old rounds. Recommend move to steps/history |
| steps/handover | 4 | notes of the old lanes a to d | Three places hold lane notes: handover, heads-up, HEADSUP.md. Recommend one |
| steps/heads-up | 5 | one note per lane | As above |
| steps/history | 65 | old rounds | |
| steps/investigations | 22 | 20 notes and 2 scripts | Code under steps. Recommend move the two scripts to scripts or history |
| steps/revit-results | 11 | one file per Revit check | |
| steps/runs | 10 | records of one old round | Recommend history |
| steps/tasks | 28 | one file per task | |
| steps/ui-shots | 38 | 35 png, 2 md, 1 .gitkeep | |
| tests/acceptance | 7 | the lead's acceptance tests | |
| tests/golden | 1 | README.md only | The Golden step of tests.yml runs on a folder with no test. Recommend add a test or drop the step |
| tests/holdout | 1 | not read | |
| tools | 1 | run-lane.ps1 | Superseded by scripts/lanes |

### 1.3 Rules

- `scripts/checks/rules_docs.py` on 12 Nov: `PASS rules docs: 567 rules, 95 new or changed meet the format, 472 old rules still to rewrite`.
- docs/rules/index.md holds 567 ids, R1 to R823, none twice. The 32 parts hold the same 567.
- Labels on the rule line: Input on 213, Output on 212, Test on 116, all three on 115, none of the three on 353. So 452 of 567 rules do not yet carry id, input, output and test in the format.
- A test names the id of 472 rules (353 in the .NET tests, 227 in python/tests, 5 in scripts/checks). 95 rules are named by no test. A mention is not proof the test checks the rule, and a range in text counts only its two ends, so some of the 95 may be covered.
- 516 of the 567 are named in product code. 26 are named in neither code nor tests.

Rule ids no test names, by part:
- 01-reader-to-round-15a.md, 41: R18, R23 to R33, R40 to R49, R57 to R62, R64, R70 to R74, R76, R77, R81, R82, R89, R105, R106
- 02-round-13-to-data-2.md, 27: R91 to R93, R95 to R99, R101, R111, R112, R119 to R121, R123, R124, R129 to R139
- 03-round-z4-to-17.md, 18: R203, R209, R222, R225, R228, R261 to R263, R266 to R270, R273, R274, R276, R279, R282
- 04-addin-r300-r327.md, 1: R303
- 07-addin-r354-r388.md, 3: R12 to R14
- 10-addin-r407-up.md, 1: R478
- 11-scanner-r420-up.md, 2: R422, R423
- 12-lane-c-r430-up.md, 1: R449
- 13-parameters-r460-up.md, 1: R463

A real gap: R689, R690 and R691 are used in dotnet/SiteAdaptation.AddIn/App/PaneHost.cs, Pane/StageCards.cs, Pane/StageResults.cs, Pane/StageResultsText.cs:3 and dotnet/SiteAdaptation.Tests/FAST1/R689_StageResultsTests.cs, and docs/rules/18-fast-pane-r680-up.md holds R680 to R688 only. docs/rules/27-fast11-r800-up.md:7 quotes the words of R691, a rule with no line of its own.

### 1.4 Buttons

The manifest names only the application class. App.BuildRibbon (dotnet/SiteAdaptation.AddIn/App/App.cs:54) makes one push button per row of RibbonLayout.Panels. A numbered button goes command Execute, Buttons.Run (Commands/Buttons.cs:22), the private Run (:86), Buttons.Body (:137 to :170). Paths below are under dotnet/SiteAdaptation.AddIn.

Ribbon, 12 buttons, all reach the code they name:

| Title | Created | Handler | Reaches |
|---|---|---|---|
| 1 Set up site | App/RibbonLayout.cs:20 | Commands/ButtonCommands.cs:12 | SetUpSiteBody.For, Commands/SetUpSiteBody.cs:19 |
| 2 Read data | RibbonLayout.cs:21 | ButtonCommands.cs:21 | ReadDataBody.For, Commands/ReadDataBody.cs:46 |
| 3 Review data | RibbonLayout.cs:22 | ButtonCommands.cs:30 | ReviewDataBody.For, ReviewData/ReviewDataBody.cs:26 |
| 4 Check | RibbonLayout.cs:23 | ButtonCommands.cs:39 | CheckTableBody.For, CheckTable/CheckTableBody.cs:40 |
| Site Adaptation Rules | RibbonLayout.cs:27 | ButtonCommands.cs:48 | Buttons.Rules, Buttons.cs:55, PaneHost.OpenRuleValues, App/PaneHost.cs:347 |
| 5 Floors and ramps | RibbonLayout.cs:31 | ButtonCommands.cs:57 | FloorsBody.For, Floors/FloorsBody.cs:16 |
| 6 Levels | RibbonLayout.cs:32 | ButtonCommands.cs:66 | LevelsBody.For, Floors/FloorsBody.cs:28 |
| 7 Place villas | RibbonLayout.cs:33 | ButtonCommands.cs:75 | PlaceVillasBody.For, PlaceVillas/PlaceVillasBody.cs:27 |
| 8 Walls | RibbonLayout.cs:34 | ButtonCommands.cs:84 | WallsBody.For, BoundaryWalls/WallsBody.cs:15 |
| 9 Report | RibbonLayout.cs:38 | ButtonCommands.cs:93 | ReportBody.For, Commands/ReportBody.cs:18 |
| Update from report | RibbonLayout.cs:39 | ButtonCommands.cs:102 | UpdateFromReportBody.For, Commands/UpdateFromReportBody.cs:16 |
| Show pane | RibbonLayout.cs:43 | ButtonCommands.cs:112 | Buttons.ShowPane, Buttons.cs:43, PaneHost.ShowByHand, PaneHost.cs:633 |

The pane in 3 stages, 12 buttons (Pane/StagesPage.xaml). A step run goes PaneViewModel.RequestRun (Pane/PaneViewModel.cs:804), RunButtonHandler.Execute (PaneHost.cs:687), Buttons.Run, the same entry as the ribbon.

| Title | Created | Handler | Reaches | Verdict |
|---|---|---|---|---|
| Settings, the gear | StagesPage.xaml:98 | Pane/StageCards.cs:269 | opens the settings | right |
| Edit, Project setup | StagesPage.xaml:115 | PaneViewModel.cs:73 | PaneHost.OpenNewProject, PaneHost.cs:327 | right |
| Open, rules | StagesPage.xaml:124 | StageCards.cs:270 | PaneHost.OpenRules, PaneHost.cs:282 | right, see note 1 |
| 1 Set up site | StagesPage.xaml:146 | StageCards.cs:272 | RequestRun(1), SetUpSiteBody.For | right |
| Choose project folder | StagesPage.xaml:147 | StageCards.cs:264, :423 | ProjectFolderPick.Write, then RequestRun of step 1 or 2 at StageCards.cs:464 | right |
| Fix items | StagesPage.xaml:151 | StageCards.cs:268 | PaneHost.FixItems, PaneHost.cs:175 | right |
| 3 Review data | StagesPage.xaml:152 | StageCards.cs:277 | RequestRun(3), ReviewDataBody.For | right |
| Build | StagesPage.xaml:171 | StageCards.cs:265, :478 | PaneHost.cs:448 to 468, SiteBuildFlow.Run, then BuildChain steps 1, 4, 5, 6, 7, 8 (Pane/BuildChain.cs:14) | right |
| Open rules, card 3 | StagesPage.xaml:189 | StageCards.cs:270 | PaneHost.OpenRules | right, see note 1 |
| Report | StagesPage.xaml:212 | StageCards.cs:266 | RequestRun(9), ReportBody.For | right |
| Update from report | StagesPage.xaml:213 | StageCards.cs:271 | PaneHost.cs:485, UpdateFromReportBody.For | right |
| i, on each card head | StagesPage.xaml:36 | none, a ToolTip and no Command or Click | nothing on a click | reaches nothing. Whether a tooltip alone is meant is UNKNOWN |

Advanced, the pane of version 1, 11 buttons (Pane/SitePanePage.xaml), all reach the code they name: the step rows 1 to 9 (:93, PaneViewModel.cs:1042), i of a step row (:111, HelpBox.Show), Browse (:54), Pick surfaces (:64, PaneViewModel.cs:320), the box button (:251, PaneViewModel.cs:850), Use a saved project (:256, PreSetting.Import), How it works (:310, PaneHost.cs:123), Edit or Open of the settings row (:278), Change of the rules row (:297), New project (:311), Export (:338, PaneViewModel.cs:902).

Notes:
1. Two rules windows. The ribbon button Site Adaptation Rules opens the rule values window (PaneHost.cs:350). The pane buttons Open and Open rules open the project rules table (PaneHost.cs:289). Each reaches a rules window, not the same one. For Bader to say if that is meant.
2. The stage cards have no button of their own for 2 Read data, 4 Check and 5 to 8. Step 2 runs through Choose project folder and steps 4 to 8 through Build. Alone they run only from Advanced or the ribbon. The stage table of CLAUDE.md lists them as buttons.
3. No IExternalCommand class of the add-in is left without a button.

## 2. Faults F1 to F42

All 42 are defined in steps/seam.md. R3 is steps/revit-results/FAST-3-revit-test.md. Every PR named was checked as merged with `gh pr list --state all`.

| Id | What it is | Status | Seen in Revit | Proof |
|---|---|---|---|---|
| F1 | Choose project folder stopped on needs step 1 | fixed, PR 257, PR 260 | yes, R3:192 | seam.md:28 |
| F2 | Set up site did not take the Baseplan of the folder | fixed on S1, PR 260. Back on the new project as F40 | yes, R3:192 | seam.md:29 |
| F3 | Card 1 had no Review data | fixed, PR 257 | yes, R3:138 | seam.md:30 |
| F4 | First Build of a fresh Revit session stopped with R405 | fixed, PR 254 | not seen in Revit as a clean case | seam.md:50 |
| F5 | Build read only the Advanced rows, Prototypes not chosen | fixed, PR 256. The same words came back as F35 | yes, R3:192 to 200 | seam.md:32 |
| F6 | Build ran Read data twice more | fixed, PR 267 | yes, R3:199 | seam.md:34 |
| F7 | Setup lost when the model is not saved | fixed as a plain line, PR 257 | not seen in Revit | docs/rules/23-fast7-r760-up.md:10 |
| F8 | Empty project still held past job values | fixed, PR 273, leftovers at seam.md:99 | yes, R3:242 | seam.md:36 |
| F9 | Small pane words | fixed, PR 257 | yes for three of four, grey Report not seen | seam.md:37 |
| F10 | Data check said Shared parameters Missing | fixed, PR 254, PR 256 | yes, R3:153 | seam.md:38 |
| F11 | The Always Load box of the unsigned add-in comes back | open, task SETUP-sign-2 waits | yes, as a fault, 5 starts | seam.md:39 |
| F12 | A stopped Build did not say why | fixed, PR 252, PR 256 | partly, the words seen at R3:391 | seam.md:40 |
| F13 | Review Close with no Save stopped the Build | fixed, PR 267 | yes | seam.md:41 |
| F14 | Card 2 shows real coordinates | explained, fine for the user, shots in git blanked | yes | seam.md:42 |
| F15 | Untuned neighbourhoods hold no villa type | explained, decided away, runs use 01A marked tuned | yes, R3:63 | seam.md:44 |
| F16 | Card 1 read did not write what Check needs | fixed, PR 256 | yes, R3:156 | seam.md:46 |
| F17 | How it works taught the nine steps | fixed, PR 257 | not seen in Revit | seam.md:47 |
| F18 | Run 2 of Advanced read the past job folder | open, no PR names it | yes, as a fault, R3:108 | seam.md:59 |
| F19 | A running read has no Stop or Cancel | open | yes, as a fault, R3:108 | seam.md:60 |
| F20 | Cancel after a long read loses the whole read | open | yes, as a fault, R3:108 | seam.md:61 |
| F21 | Step 1 Done lost over save and reopen | UNKNOWN, not seen again at R3:288 and R3:394, no PR names it | yes, R3:114 | seam.md:62 |
| F22 | Two full reads before a neighbourhood can be named | fixed, PR 272 then PR 284 | yes, R3:386 | seam.md:63 |
| F23 | A scope change undoes step 2 with no line | open | yes, as a fault, R3:117 | seam.md:64 |
| F24 | Card 1 order undid its own step 1 | fixed, PR 264 | yes, R3:192 | seam.md:69 |
| F25 | Build looped on Check | fixed, PR 268 | yes, R3:200 | seam.md:71 |
| F26 | Check read plots from the past job data book | fixed, PR 269 | yes by effect, R3:200 | seam.md:72 |
| F27 | No yard floor, no road toposolid, a floors overlap box | toposolid and box fixed, PR 269. The yard is explained by R410, seam.md:79 | yes, R3:201, R3:215 | seam.md:73 |
| F28 | Review, Check and Floors gave three plot counts | fixed, PR 269, PR 277 | yes, R3:200, R3:264 | seam.md:74 |
| F29 | The plan wrote only 62 of 278 plots of 01A | fixed, PR 277 | yes, 263 of 278, R3:269 | seam.md:104 |
| F30 | Card 3 counted no road level on the whole project | fixed, PR 277 | yes, R3:281 | seam.md:105 |
| F31 | Revit 2021 villa files stopped step 7 on an upgrade box | fixed, PR 278 | yes, R3:262, R3:295 | seam.md:106 |
| F32 | A walls overlap box stopped step 8 | fixed, PR 281 | yes, R3:399 | seam.md:111 |
| F33 | Reopen stopped 13 times on the upgrade box | fixed, PR 283 | yes, R3:398 | seam.md:112 |
| F34 | Card 3 counted issues of the whole read | fixed, PR 281 | yes, R3:399 | seam.md:113 |
| F35 | Prototypes row Not chosen after one read, Build stops | open, cause UNKNOWN | yes, as a fault, R3:391 | seam.md:116 |
| F36 | After Confirm, Review data stays grey until one more press | open | yes, R3:387 | seam.md:117 |
| F37 | The reuse box says a line untrue of a reuse | open | yes | seam.md:118 |
| F38 | The step 7 box lacks the line on the 2024 copies | open | yes, R3:392 | seam.md:119 |
| F39 | Scope goes back to whole project on reopen | open | yes, R3:388 | seam.md:120 |
| F40 | New project: Set up site gets no Baseplan from the folder | open | yes, R3:445 | seam.md:122 |
| F41 | New project: Use CAD finds no plot outline | open, cause not measured | yes, R3:446 | seam.md:123 |
| F42 | A typed origin outside one UTM zone is refused | open | yes, R3:447 | seam.md:124 |

Counts: fixed 26, open 13 (F11, F18, F19, F20, F23, F35 to F42), explained 2 (F14, F15), UNKNOWN 1 (F21). Of the 26 fixed, 21 were seen in Revit after the fix, 2 partly (F9, F12), 3 not seen (F4, F7, F17).

## 3. Remaining features

The numbers are of neighbourhood 01A of folder S1, which was used for tuning, unless said. All of 6 to 8 Nov, on the laptop. Phase 3 is the second project, a folder the tool had never seen.

### 3.1 BIM features

| Feature | Where it stands | Last measured number | What is left |
|---|---|---|---|
| Site coordinates | partly | S1: 5 of 5 check points pass from the CAD drawing, 8 Nov (R3:244). Phase 3: 0 check points, a typed position used (R3:445) | F40, F41, F42 |
| Plot read | works on S1 | 2,618 of 2,964 numbered plots are villa plots, 278 in 01A, one read 454 s, 8 Nov (R3:385). Scenario S1: 1,873 of 1,891 plot numbers right, 6 Nov (steps/scenarios.md:9) | A file open in Civil 3D makes the read not whole |
| Villa type | partly, a person confirms a column | After Confirm a type for 2,570 of 2,964 plots, 32 with no villa file, 8 Nov (R3:249). Scenario S1: 233 of 1,891 right with no confirm (steps/scenarios.md:9), 1,859 with it (steps/scenarios.md:53) | An area with no type column gets no villa (F15) |
| Road levels | partly | 13 of 278 plots of 01A have no road level, 8 Nov (R3:279) | The person picks the surfaces. No Land XML, no plot built |
| Road toposolid | partly | 1 made from one surface, 433 warnings taken, 8 Nov (R3:344) | 01A needs two surfaces and the tool takes one, Q600. No one checked its shape |
| Floors, ramps, yards | floors and ramps work, yards not built in the new mode | 526 floors on 263 plots: 263 generic, 263 ramp, 0 yard, 62 s, 8 Nov (R3:343) | 0 yard floors in every run of 6 to 8 Nov. It is rule R410, the Rev 06 set holds the back yard floor off, Q225 open (seam.md:79) |
| Levels | works on 01A | 526 levels at the plan on 263 plots, 0 moved, 8 Nov (R3:345) | 15 plots listed with no level (R3:260) |
| Villa links, mirror, rotation | links work, mirror mostly missing, rotation UNKNOWN | 263 links of 13 villa files, 96 s, 8 Nov (R3:346). The plan lacks the mirror value on 211 of 278 plots (R3:279). Scenario S1: mirror right on 735 of 1,891 with no confirm (steps/scenarios.md:9) and on 448 of 1,891 with a confirmed profile (steps/scenarios.md:53) | A source for the hand of each prototype. Whether rotation is right is in no record |
| Walls | partly | 582 walls on 263 plots, 6.5 s, 8 Nov (R3:347) | 584 issues of step 8 in run 2 (R3:288), a run before the last one. No person looked at the walls |
| Shared parameters on links | partly, not counted in Revit | The sheet reads 10 parameters each with a GUID (R3:246). CASE and Foundation Type missing on 2 of 278 plots on 8 Nov, 213 on 7 Nov (R3:279, R3:222) | They follow an assumed hand on about 201 plots, so they may be wrong. No run counted the values on the links in Revit |
| Report | works, run once | 1 Excel in 23 s, 13 sheets, 278 plot rows, 526 floor rows, 582 wall rows, 8 Nov (R3:289) | Not run on the last copy |
| Update from report | partly | Opened with an unedited Excel: 0 changes found, 8 Nov (R3:290) | An edited Excel was never tested |

### 3.2 Other features

| Feature | Where it stands | Last measured number | What is left |
|---|---|---|---|
| Pane | partly | 3 cards ran to their end on 01A from an empty model, 8 Nov (R3:315) | F18, F23, F35 to F39 |
| Speed | measured on the laptop only | 8 Nov: read 454 s, Build 4.2 min, step 7 96 s, card 1 16.9 min (R3:333 to 374) | Not measured on this PC, which has a quarter of the memory. Whole project time UNKNOWN, Q414 |
| Stop and cancel | partly | A stopped Build names step and reason (R3:391) | A running read cannot be stopped (F19). Cancel after the read loses it (F20) |
| Install for the team | not built for a team | Installed by a script on one PC. 0 of 34 files signed (steps/revit-results/SETUP-sign.md:40). On 12 Nov the script installed on this PC, steps/pc-setup.md | Signing (F11, SETUP-sign-2), an installer with no VS Code, Q379 |
| New project path | partly, card 1 only | Phase 3, 8 Nov: read 189 s, 24 files read, 0 plots built (R3:426 to 472) | F40 to F42, F35, F36. Set aside by the order of 12 Nov |

Notes: 15 of 278 plots of 01A are not built, 11 with no floor level, 2 with no outline or front, 2 with no unit (R3:264). No number above was held against a past model in Revit, that compare is task 2 of steps/plan.md.

## 4. Backlog and questions sorted

Each line item of steps/backlog.md and each full text block of steps/questions-open.md got keep, done or no longer applies. Where done could not be proven the verdict is keep.

| File | Keep | Done | No longer applies | Total |
|---|---|---|---|---|
| steps/backlog.md | 245 | 11 | 8 | 264 |
| steps/questions-open.md, full text blocks | 98, of which 11 a config value cites | 1 | 2 | 101 |

Moved with this PR, whole lines, each with its old line number and why:
- 19 backlog lines to steps/history/backlog-moved-12nov.md. Done: lines 7, 57, 77, 78, 91, 160, 187, 251, 252, 257, 258. No longer apply: 193, 227, 232, 239, 240, 247, 249, 250, all about the demo, the laptop or an order of work the order of 12 Nov replaced.
- 3 question blocks to steps/history/questions-moved-12nov.md, each with a new row in steps/questions-closed.md: Q336 done by R706 in PR 235, Q441 and Q454 no longer apply.
- Line 242 of the backlog, the R310 samples, is proven done by PR 242 but stays: its limit was measured on the laptop and wants a new measure on this PC.

Kept on purpose:
- 19 backlog lines mirror a question a config value cites, R401 keeps those open until Bader answers: Q150, Q159, Q164, Q225, Q228, Q236, Q249, Q257, Q258, Q269, Q277, Q326, Q327, Q329, Q351, Q415, Q450, Q470, Q473.
- Ten old pane and window draft questions have no answer on record and may be dropped by Bader in one go: Q284, Q285, Q286, Q287, Q291, Q293, Q370, Q405, Q407, Q409.

Found and not changed:
- The index of steps/questions-open.md, lines 9 to 22, still names 17 ids that steps/questions-closed.md closed: Q148, Q149, Q156, Q190, Q214 to Q216, Q218 to Q221, Q331, Q333, Q336, Q338, Q391, Q394. Q441 and Q454 were not looked for there. They sit inside long lines, so they were not cut by line. Line 22 is a broken copy of part of line 20.
- 14 open questions have no backlog line: Q495, Q497, Q498, Q501, Q502, Q503, Q505 to Q508, Q513 to Q516.
- 109 more open questions have their full text in steps/history/questions-open-older.md and were not read one by one.

## 5. Repo upkeep

Nothing was deleted. A recommendation each.

| Item | Found | Recommendation |
|---|---|---|
| Old worktrees | None on this PC. The 55 of the backlog were on the laptop. `git worktree list` shows the main folder and the 4 lanes made on 12 Nov | Nothing to do. Never run git worktree prune without Bader |
| Remote branches | 12 old branches on origin beside main and the branch of this round. 10 are merged into main. 2 are not: lead/demo-live-p3 of the closed PR 291 and records/6c-3nov-c-stop | Delete the 10 merged ones with `git push origin --delete`, after Bader says yes. Read the other 2 first |
| Old task files | 6 tasks of the fast build stood open on main | Closed as not finished in the setup PR of 12 Nov, each with a line why |
| Records near the size cap | The cap of R400 is 102,400 bytes. steps/log.md 100,584 and steps/questions-closed.md 100,659 before this round. steps/questions-open.md 54,732, in no danger | Split log.md before the next entry, done in the plan PR of this round. Split questions-closed.md at the next closed question |
| Tests that fail at random | PaneTests R372, the hash off the UI thread, failed once on net48 in the gate of PR 292 on 12 Nov and passed on net8.0 in the same run, it waits 10 s on a thread (dotnet/SiteAdaptation.Tests/PaneTests.cs:822), a rerun was needed. The worker time limit tests of R347 fail under load and are left out of the required job (scripts/quick-set.txt:2, .github/workflows/tests.yml:40). R104 FreezeGuardTests failed once, cause not measured. The R310 samples take their limit from a measure made on the laptop (config/project.json, cimTestTimeoutSeconds) | Measure the CIM times again on this PC. Give the R347 tests a memory and time measure on this PC before they go back into the gate |
| The public repo and client names | The repo is PUBLIC. 157 tracked files name the client project, 3,170 lines: 105 files under steps and docs, 6 under config and rules, 30 test files, 9 product C# files. 193 files name the company. Two manifests hold a path under a Windows user folder (dotnet/SiteAdaptation.Placer/SiteAdaptationPlacer_Revit2024.addin:5, dotnet/SiteAdaptation.TestHost/SiteAdaptationTestHost_Revit2024.addin:5) | Make the repo private, item 5 of steps/bader-form-12nov.md. The names are also in the git history, so a clean up of files alone would not take them away. The 9 product C# files are the known debt of CLAUDE.md, no project data in code |
| A tracked lock file | .claude/scheduled_tasks.lock, of 24 Oct | Untrack and ignore |
| Stale state pages | steps/PROGRESS.md was written on 6 Nov. steps/STATUS.md is not written on this PC | Run scripts/status.ps1 -Progress after the merges of this round |
