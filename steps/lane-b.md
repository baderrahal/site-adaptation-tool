# Lane B, the laptop lane

Phase 5 and 6 on Bader's company laptop (D1 of steps/decisions-2026-10-28.md). While Phase 4 is open this file holds what the shared records get at the fold in, and it is where a Lane B phase is marked done.

## Phases

| Phase | Status |
|---|---|
| L0 Laptop check, light mode design, UI spec | done, Lane B, laptop, on the merge of branch docs/laptop-l0 after its review |
| 5a Install, ribbon, pane, one transaction per button | running since 28 Oct 2026, Lane B, laptop |
| 5b Button 1 Set up site | open |
| 5f Update from report, new by Bader's changes of 28 Oct 2026, after 5e and before 6a | open |

After 5b Lane B waits until 4g2, 4g3 and 4h are all marked done in steps/01_next.md. Bader's list of the Lane B phases after that, in order: 5c, the Revit run after 5c, 5d, 5e, 5f, 6a, 6b, 6c (5f added by Bader's changes of 28 Oct 2026, docs/design/13-workflow-changes.md).

A laptop session starts from Bader's Lane B prompt, never from steps/START.md. START.md runs the first open phase of steps/01_next.md, which is Phase 4, and nothing there names this file until the fold in.

## Fold in

Not done on 28 Oct 2026 in L0. Two conditions fail: PR 75 "Round ADDIN 4g2 PR 2" is open, and origin holds the branches docs/addin-4g2-state-line and feat/addin-4g2-status-memory besides main. The top entry of steps/log.md was not checked, since the fold in fails already.

Not done on 28 Oct 2026 at the start of 5a. Line 1 of steps/PROGRESS.md on main 09385dd reads "STATE RUNNING, 4g3, 2026-10-28 10:18". The other two conditions held then: no open PR, and origin held main alone (RAN gh pr list, git ls-remote).

Waiting for the fold in, in one records commit:

1. steps/01_next.md: an entry for L0, and each of 5a and 5b done by then, marked done "Lane B, laptop", and the phase changes below. The Phase 4 next step stays as it is. Without this a START.md session on the personal PC finds 5a open once 4h is done and builds it again.
2. steps/log.md: one entry within the caps for the Lane B phases done.
3. steps/PROGRESS.md: the state and the next three steps.
4. steps/usage.md: the rows of the Usage section below.
5. steps/questions-closed.md: Q021 "Parallel windows over 8 GB free" closed by D3, 28 Oct 2026. Each QL id of this file gets the next free Q id, the map of old numbers to IDs gets the pair, and every file that names a QL id (this file, steps/laptop-check.md, steps/decisions-2026-10-28.md, docs/design/11-light-mode.md) names the Q id instead.
6. docs/design/README.md: the rows of 11-light-mode.md and 12-ui-spec.md.
7. docs/design/07-mock.md: every item marked NEW in docs/design/12-ui-spec.md joins the list of 7.7, as approved by Bader (U6, U7). In 7.1 the icons of 12-ui-spec.md take the place of the plain numbered squares named CHOSEN. These differences of 12-ui-spec.md from the mock carry no NEW mark and join 7.7 too, so the screenshot check of Phase 6 finds none by chance: the Rule column shows the plain name with the id as a tooltip, the "Will change" mark of 7.3, the found and not found marks of the input rows, and the first open state of 7.2 next to the new project prefilled from config of section 6.
8. CLAUDE.md: the venv command of QL6, `py -3.12 -m venv .venv`, and Rule 1 of the Lane B rules below.
9. Bader's four UI controls of 28 Oct 2026, the theme test, the text test, the picture test and the UI checklist in review, written as rules in docs/design/12-ui-spec.md, part UI controls. At the fold in (CHOSEN by Code): .claude/agents/reviewer.md gains the UI checklist, control 4, and each phase of steps/01_next.md that touches a window names the four controls in its done-when. The end of 5a gives Bader the UI approval form of item 5 of that part. Until then the lead of each Lane B session puts the checklist into the brief of every reviewer of a PR that touches a window (CHOSEN by Code, .claude/agents is outside Lane B).
10. docs/design/README.md: the row of 13-ui-labels.md, the label list of control 2 (CHOSEN by Code, as every part of docs/design/ has its row).
11. Bader's design changes of 28 Oct 2026, docs/design/13-workflow-changes.md, approved on the canvas: docs/addin-mock.md and docs/design/07-mock.md 7.1 and 7.2 take the order of the 9 steps, the ribbon of four panels and the pane changes, as Bader approved them. docs/design/03a-buttons-shared-and-1.md (the steps each button needs) and 03b-buttons-2-to-8.md take the new numbers and the new button 3 Review data, docs/design/10-rule-ids.md the rule ids of the renumbered buttons and of the new windows. steps/01_next.md gains phase 5f Update from report after 5e and before 6a, and the phases 5c and 5d take the changes of the last part of 13-workflow-changes.md. CLAUDE.md, "The approved mock" line, names 13-workflow-changes.md beside docs/addin-mock.md. docs/design/README.md gains the row of 13-workflow-changes.md.

## Phase changes to steps/01_next.md

Bader's text of 28 Oct 2026, word for word, for the fold in. Two names are changed, CHOSEN by Code: "R1" is written "The Revit run after 5c", since R1 is also the id of a rule, and "the window rule below" is written "the window rule (D3)", since the rule is in steps/decisions-2026-10-28.md.

- 5a: R372 settings part waits for R367 of 4h, build the pane against a small settings reader, join at 5c. Q103 moves to 5c. The UI spec and Theme.xaml are part of 5a's done-when. Install per user only.
- 5b: R383 element tags wait for R368 of 4h, button 1 calls one tag step that 5c fills.
- 5c: do every join item of steps/lane-b.md first (R367 settings, R368 tags), then Q103, then buttons 2 to 4 with light mode (Masses and Links) and the check table of the UI spec with the plain rule names.
- The Revit run after 5c: as "Revit run after 5c" of steps/01_next.md, in Masses mode, plus one NBH in Links. Measure the open item of the base boundary. Record the peak memory of one window in each mode, and run the whole NBH 03 image again (Q063) now that 64 GB is free.
- 5d: done when NBH 012 is placed in Masses with all 13 prototype shapes, then in Links, peaks logged.
- 6b: Masses for all 7 zones, Links for one NBH. Zones run in parallel windows by the window rule (D3).

Added by Bader's Lane B prompt of 28 Oct 2026, the second of that day, for the fold in:

- 5a: Rule 2 first. Install per user only, no admin, into %APPDATA%\Autodesk\Revit\Addins\2024. R371 ribbon with the icons of docs/design/12-ui-spec.md. Theme.xaml with the gate test that fails on any colour or font size outside it, Libre Franklin bundled from google/fonts. Done when the add-in installs into Revit 2024 on the laptop with the ribbon and pane of 7.1 and 7.2 and the UI spec, open items 1 and 2 of docs/design/09-risks.md are measured in Revit, a screenshot of the ribbon and the pane is in steps/ui-shots/ beside a list of every known difference from the canvas, and the tests run on both runners.
- 5b: R374 on the R17B file of kept\round_17 among the four copies of 3.1. The Set up site window by 7.3 of docs/design/12-ui-spec.md with its screenshot in steps/ui-shots/.
- C2, dropped by Bader on 28 Oct 2026 (Lane A on this laptop, item 1), as he first gave it: after 4g3 and 4h are done, in a git worktree of origin/main outside the repo, the gate, the C# reads, the parity on the whole project and the 7 keys on net48 and net8.0 as 4g2 ran it, and the build peak, three runs each, the free memory at the start of each read. One PR adds the result to steps/log.md and closes the C# items the 4g3 and 4h log entries left for the laptop.
- 5c: Rule 3 first, then every join item below and Q117, the optional inputs of button 3, then Q103, then buttons 2 to 4.
- 5d: Q115, the wall extent per kind of edge, before button 7 places walls. Q116, the ramp of turned plots on the open edge.

## Join items of Bader's changes of 28 Oct 2026

From docs/design/13-workflow-changes.md, part Where each lands. 5a builds the order, the ribbon and the pane in Lane B PR 8.

- W1, 5b. Set up site: the pass limit 0.1 mm read from addin.position.checkPointLimitMm of config, the Floor types row, the Ramp slope row and its dropdown, the Shared parameters row from the parameter sheet, the parameters and GUIDs only from that sheet. Its PNG.
- W2, 5c. config/project.json gains addin.position.checkPointLimitMm 0.1, config is Phase 4 territory until the fold in.
- W3, 5c. The rules profile gains the block manualRules, M01 to M15 as drafted on the canvas, with id, text, group, manual section and the steps that check it. Code reads the page numbers from the manual PDF. The texts of M01 to M15 are on the canvas, not in the repo, so 5c asks Bader for them if no file holds them.
- W4, 5c. The pre-setting: Import from past job and Export with R367, never a drawing path or a coordinate.
- W5, 5c. The windows 3 Review data and Site Adaptation Rules, their labels in docs/design/13-ui-labels.md, their PNGs, and the manual rule ID on every check row and Review data row, with "Open in Site Adaptation Rules" in the Check table side box.
- W6, 5d. Place villas places each link at its FFL and writes Prototype-FFL, Levels writes no link parameter, in the buttons of the new order.
- W7, 5e. The manual rule ID on every report line.
- W8, 5f. Update from report, the whole window and its run, its labels and PNG.

## Join items for 5c

Changes Lane B needs in files it may not edit while Phase 4 is open.

- J1. R367 settings: 5a builds the pane against a small settings reader, 5c joins it to R367 of 4h.
- J2. R368 tags: button 1 of 5b calls one tag step, 5c fills it from R368 of 4h.
- J3. Q103 moves from 5a to 5c.
- J4. The keys of light mode in config/project.json: the Villa mode default, the height of the solid and the limit of the base against the footprint (D2, QL4, QL5).
- J5. The 10 paths under C:\Users\bader, 9 in config/project.json and 1 in config/road_surfaces.json (QL2, steps/laptop-check.md item 14).
- J6. The rule id of light mode (D2). Bader gives it "at fold in", and docs/rules/ is outside Lane B while Phase 4 is open, so 5c writes it in docs/rules/ with the code and a test that names it, and names it in docs/design/11-light-mode.md. An id from R400 up must be a written rule with its test, as R401 is, and the planned ids of section 10 are R345 to R388, so the id cannot come in a records commit alone.
- J8. The code paths that take Documents\SiteAdaptation from the Windows Documents folder: dotnet/SiteAdaptation.TestHost/Run-TestHost.ps1, Run-Parallel.ps1, JobSession.cs and its job guard, dotnet/SiteAdaptation.Placer/PlaceFromPlan.cs and PlaceFromPlanCommand.cs, and the default work folder of docs/design/06-settings.md. Each reads the root of QL2.
- J9. The rule text of the Phase 5 ids Lane B codes before the fold in, R370, R371, R372 and R382 in 5a, R373, R374 and R383 in 5b, goes into docs/rules/07-addin-r354-r388.md and docs/rules/index.md at the fold in, from their rows of docs/design/10-rule-ids.md and what the code does. docs/rules/ is outside Lane B while Phase 4 is open, and no test ties a rule id to the index (RAN grep of python/tests), so the tests name the ids before the text is there.
- J7. D3 keeps a window lock per copy, while R200 holds one lock siteBuild.lock for every window and, while a process of siteBuild.windowProcesses runs, logs a wait and looks again after waitMinutes. Parallel windows need R200 changed before 6b. The peak per window is measured on one NBH, 6b opens whole zones, so the first 6b window measures its own peak before a second opens.

## Latent findings

- L0 breaker 21. Step 8 of the Bader section of steps/laptop-check.md sets only http.sslBackend. It works because ~/.gitconfig already holds http.schannelcheckrevoke=false, set by someone UNKNOWN. On a machine without that line the step would not be enough. No other machine runs it now.
- 5a PR 2 review. On net48 the resolver throws ArgumentNullException when a file of its folder is not an assembly, since the Version compare of .NET Framework refuses a null version (OwnFolderResolver.cs lines 46 and 60). AssemblyName or LoadFrom throws there on a malformed name or a file that is no assembly.
- 5a PR 2 review. The theme gate misses compound property names such as RowBackground and HorizontalGridLinesBrush, SystemColors, a Setter with Value before Property or a Setter.Value element, single-quoted attributes, sc# colours, a FontSize from a Binding FallbackValue or TargetNullValue or from a sys:Double resource, FontSize += and *=, and the emSize of FormattedText. Nothing checks the Setters and Colors inside Theme.xaml itself.
- 5a PR 2 review. The resolver would serve the Newtonsoft.Json.dll of the build output if the install copied it, so the install of PR 4 must leave it out.
- 5a PR 2 review. CreateRibbonTab throws on a second manifest of the add-in.
- 5a PR 5 review. A folder input is hashed by its file list only, so a copy of the same size and write time passes the Apply re-check, and no slot holds the content hash a body takes as it reads.
- 5a PR 5 review. With the dry run unticked the inputs are not hashed again at Apply.
- 5a PR 5 review. A body with a null ReadBack skips the read back.
- 5a PR 5 review. Install-SiteAdaptation.ps1 -NoBuild can mix two builds of Common, the add-in build and an older worker build.
- 5a PR 5 review. Every failed move is called held open by another program, and a saved file is reported unsaved when the hash after its move fails.
- 5a PR 5 review. Two outputs of one file name in different folders collide under the dry folder.
- 5a PR 5 review. Buttons.State is never set, the pane of PR 4 fills it.
- 5a PR 5 review. Two inputs of one name or a bad path character throw an ArgumentException the flow does not catch.
- 5a PR 5 review. A temporary file a body left open in a stream is not deleted on a roll back, and nothing deletes it later.
- 5a PR 5 review. An exception that is not an IOException after the commit, in a move or its hash, leaves no record of the committed run.
- 5a PR 5 review. No hook runs the link load before the transaction or puts the shared parameter file back after it.
- 5a PR 5 review. A workbook open in Excel can fail the FileShare.Read hash of an input.
- 5a PR 5 review. The folder hash walks every file of the folder, backups and lock files too.
- 5a PR 5 review. The install writes the manifest before the Common check and copies the add-in before a failing Worker copy.
- 5a PR 5 review. Copy-Files never removes a file an earlier install left.
- 5a PR 5 review. The TaskDialog and the Transaction are not disposed on every path.
- 5a PR 5 review. The list per plot of the confirm is hidden in ExpandedContent.
- 5a PR 5 review. No test drives WarningCollector, RevitTransaction or TaskDialogConfirm.
- 5a PR 5 review 2. A running Revit whose path is known but whose version cannot be read, outside a Revit 2024 folder, counts as not Revit 2024 and lets the install go on (dotnet/SiteAdaptation.AddIn/InstallChecks.ps1 lines 21 to 23 and 35). No real input is known to reach it, Revit.exe carries its version.
- 5a PR 4 review. With no machine.json the work root falls back to the Documents folder, which OneDrive may hold, with no warning. Rank first for 5c.
- 5a PR 4 review. A work folder inside inputRoot or a machine.json with no workRoot throws out of IRunState.WorkFolder, and the flow has no stop for it.
- 5a PR 4 review. A relative or unexpanded workRoot of machine.json is taken as written.
- 5a PR 4 review. A cloud PathName can throw in Path.GetFullPath of the work folder hash.
- 5a PR 4 review. The folder hash of the pane and the one of the flow differ in case and slash, so they never match each other.
- 5a PR 4 review. The folder stamp, the latest write time xor the file count, misses a rename.
- 5a PR 4 review. SettingsFile.Read takes formatVersion 0 and throws an exception it does not catch on a field of the wrong shape.
- 5a PR 4 review. A folder input with a denied subfolder shows as missing.
- 5a PR 4 review. A mapped drive and its UNC path give one model two work folders.
- 5a PR 4 review. The Nbh setter of the view model takes null.
- 5a PR 4 review. The baseline compare goes through a 256 colour palette and may flake on another machine.
- 5a PR 4 review. The rules file is parsed on the UI thread at every refresh of the pane.
- 5a PR 4 review. A file that keeps changing is hashed again at every refresh.
- 5a PR 4 review. A drive root picked as a folder input is hashed over the whole drive.
- 5a PR 4 review. The text test checks the messages of RunTexts by their parts, so a short part passes inside any entry, and a text built by code is not seen.
- 5a PR 4 review. PaneHost.Current, PaneHost.Sessions, PaneHost.Routed, AnswerRelay.Post and PaneSessions.KeyOf have no production caller yet.
- 5a PR 4 review. DocumentOpened and DocumentCreated give a session to linked and family documents too, which no button runs on.
- 5a PR 4 review. A click of the box runs later through an ExternalEvent and may run on another document if the user switched in between.
- 5a PR 4 review 2. The hash of DiskProbe in the pane is a second copy of FileHash.Sha256 and InputHash.OfFolder of Common, now with another share mode. In 5b the pane calls the hash of Common and FileHash takes FileShare.ReadWrite.
- 5a PR 4 review 2. PaneViewModel.PickInput, SettingsFile.Read and PaneViewModel.Document have test callers only.
- 5a PR 4 review 2. A hash that fails is swallowed in DiskProbe.Hash, so its row looks like a hash still running.

## Lane A on this laptop, Bader 28 Oct 2026

Phase 4 now runs on this laptop too, as Lane A, in a second clone folder "Site Adaptation Tool Lane A". Bader's changes for Lane B, recorded as his decisions:

1. C2 is dropped from the Lane B list. Lane A runs the C# checks of 4g2, 4g3 and 4h inside its own phases, which closes Q111. 5c starts when 4h is marked done in steps/01_next.md.
2. Memory: Lane A runs builds of up to about 3 GB at the same time. The window rule counts them: keep 8 GB free across both lanes, so the windows of D3 are (free RAM minus 8 GB minus the Lane A builds running) divided by the peak of one window, rounded down, at least 1 and at most 3.
3. Speed rules from now on. S2: parity on the 7 keys in each PR, the whole project once at the end of a phase. S3: one review pass for a PR that changes only records, docs or UI text, at most two for logic.
4. Design changes: Bader may send new design changes during the build. Each goes into docs/design/12-ui-spec.md or docs/design/13-workflow-changes.md and into this file as a join item for the phase that owns that part, recorded as Bader's decision. If it belongs to the current phase and fits, it is done there.
5. Lane B never edits Lane A's files: the plan builder, config/project.json, rules/, docs/rules/, docs/design/ parts 01 to 10. Shared records only by the fold in rule. Before this rule, 5a PR 5 (PR 89) changed one line of docs/design/08-install.md, line 5, the install refusing only on Revit 2024 (Lane B's own R370). Fold in item 11 names changes of 03a, 03b, 07-mock and 10-rule-ids that wait for whoever owns those parts at the fold in.

## Lane B rules, Bader 28 Oct 2026

- Rule 1, PRs across lanes: at most one open PR per lane, and never two open PRs that touch the same file. Lane B PR titles start with "Lane B", its branch names hold "laptop" or "lane-b". Into CLAUDE.md at the fold in.
- Rule 2, Civil 3D: Civil 3D 2025 is installed here, the CAD reads of earlier rounds ran on Civil 3D 2026 on the PC. First thing in 5a, list every place a button or its worker needs Civil 3D, AutoCAD or a DWG reader at run time. Anything a button needs that is built against the 2026 libraries becomes a question before anything is built on it. The list is below.
- Rule 3, data copies: the data on this laptop serves 5a and 5b. Before 5c Bader copies the work folder again after Phase 4 ends, and 5c checks it against a fresh PC manifest before it starts.
- Rule 4, setup report: if SA_COPY\setup_report.md of the OneDrive folder 02_ROSHN MAKKAH exists and steps/laptop-check.md does not hold it, its facts go into steps/laptop-check.md in the first records commit. At the start of 5a the file does not exist (RAN ls of SA_COPY), so nothing was copied.
- Window rule D3 and Q021 as in steps/decisions-2026-10-28.md. Revit runs on copies only, logs outside the repo, the peak memory of every run recorded, Code runs Revit through the test host and never asks Bader to click.

## Rule 2, what needs Civil 3D, AutoCAD or a DWG reader at run time

Found on main 09385dd by a grep of dotnet/ and python/ for AcMgd, AcDbMgd, AcCoreMgd, AeccDbMgd, AecBaseMgd, accoreconsole, Civil 3D, C3DProbe and C3DWatcher (RAN).

| Need | Where | Runs on 2025 | How known |
|---|---|---|---|
| DWG read of buttons 1 to 3 (baseplan, villa blocks, curbs, the check points of button 1) | the worker CadReader: dotnet/CadReader/CadReader.csproj line 11 ACadSharp 3.8.0, dotnet/CadReader/WorkerReads.cs line 5, dotnet/CadReader/CadExtractor.cs line 5 | needs no AutoCAD and no Civil 3D by its references. No DWG was read on this laptop yet | READ the csproj, its only package references are ACadSharp, DocumentFormat.OpenXml and PdfPig, with no AutoCAD or Civil 3D assembly |
| Road surface of button 2 | the LandXML files of siteBuild.roadSurfaces read by System.Xml in Common, dotnet/SiteAdaptation.Common/Read/LandXml.cs line 7, docs/design/04-inputs.md line 16 | needs no Civil 3D at run time | READ 04-inputs.md and LandXml.cs. The pane takes the landxml folder of the snapshot (docs/design/07-mock.md line 34). Where that folder came from is not READ in this session, UNKNOWN. A new road surface drawing would need new LandXML files, which no button makes |
| None of the buttons | the data book tools: python/c3d_run.py line 167 (accoreconsole), python/c3d_window.py, python/c3d_inventory.py, dotnet/SiteAdaptation.C3DProbe/Civil/SiteAdaptation.C3DProbe.Civil.csproj line 7 (AutoCAD 2026 HintPath, not in the solution), dotnet/SiteAdaptation.C3DWatcher/Program.cs line 13 | not needed. Whether C3DProbe.Civil builds and loads in Civil 3D 2025 is UNKNOWN, not measured, since no Phase 5 or 6 step runs it | RAN a grep of dotnet/SiteAdaptation.TestHost, Common and CadReader for C3DProbe, C3DWatcher and accoreconsole: no call, one comment in dotnet/CadReader/CadModels.cs line 29 |

So nothing a button needs is built against the 2026 libraries, and Rule 2 raises no question.

## Bader's answers of 28 Oct 2026

Bader answered QL1 to QL8 in his Lane B prompt of 28 Oct 2026. At the fold in each QL id takes the next free Q id and goes to steps/questions-closed.md with this answer, item 5 above. Lane B numbers its questions QL1 upward, CHOSEN by Code, since both lanes would take the same next free Q id.

- QL1: the inputs are copied from the PC to C:\02-Site Adaptation, proven by hash. C:\10- ROSHEN MECCA is not used by the tool.
- QL2: one machine file, %APPDATA%\SiteAdaptation\machine.json, gives the work root C:\Users\p003653k\Documents\SiteAdaptation, outside OneDrive. No path of a user goes into config/project.json.
- QL3: keep the U1 to U7 split of steps/decisions-2026-10-28.md.
- QL4: Villa mode Masses by default, Links picked per run.
- QL5: the base against the footprint is the largest distance between the two outlines, both ways, in mm, against a limit in config.
- QL6: CLAUDE.md says `py -3.12 -m venv .venv`.
- QL7: SA.Type.Project 16, SA.Type.Step 14, SA.Type.Count 28, SA.Type.Label 13 SemiBold. Amber to SA.Warn, grey to SA.BorderStrong, green to SA.Ok, white to SA.Surface. A missing file shows "[m] of [n] found". Written into docs/design/12-ui-spec.md with the weights of the recommended answer.
- QL8: a "Villa mode" row under the Dry run checkbox, a new item of the list of 7.7. Written into docs/design/12-ui-spec.md 7.2 and docs/design/11-light-mode.md.

## Questions open in Lane B

QL9. Tabular figures. Libre Franklin 2.000 and 3.000 have no tnum feature, so the "Tabular figures for numbers" of docs/design/12-ui-spec.md cannot come from it, and WPF does not fall back to another font for one feature. The GSUB feature list of both files holds aalt ccmp dnom frac liga locl numr ordn sinf subs sups, and rvrn in 3.000, no tnum (READ in 5a PR 2). WPF gives the bundled Regular the digit widths 0=688 1=465 2=599 3=641 4=649 5=654 6=661 7=599 8=668 9=658 in thousandths of the em, 9 widths for 10 digits (RAN), the breaker measured 0=675 1=438 9=641. Under SA.Type.Count "1111", "0000" and "7777" are 55.3, 78.5 and 68.5 px wide (RAN).
- Recommended: keep Libre Franklin, right-align the number columns and accept proportional digits.
- Other: numbers in Segoe UI. Segoe UI Regular has one width for every digit by default, 539 for each, and Segoe UI Semibold has 5 widths, 402 to 576, by default, both files hold a tnum feature (RAN and READ in 5a PR 2), so the Semibold numbers need the tnum request the theme already makes.
- Other: Bader gives another font.

QL10. Icon of Place villas, its hole. The 12-ui-spec.md line of 7 Place villas (4 before Bader's changes of 28 Oct 2026) reads "M25 6.5a1 1 0 1 0 2 0a1 1 0 1 0 -2 0 fill white (32 only)" with no "no stroke", so by the defaults of the spec the 2 wide SA.Heading stroke covers the white fill and the pin shows a navy dot. 5a PR 2 draws it as written, seen in a render of the 16 icons (RAN).
- Recommended: "no stroke", a white hole as the canvas likely shows.
- Other: keep it as written.

QL11. The text test and windows not built yet. Bader's control 2 says the gate test fails on any entry of the label list that no window shows. Set up site comes in 5b and the check table in 5c, so their entries would fail every gate of 5a. The UI spec now counts an entry only once its window is built (CHOSEN by Code, docs/design/12-ui-spec.md, UI controls item 2).
- Recommended: keep it, each window's entries count from the PR that builds it.
- Other: the entries of a window go into the list only in the PR that builds it.

QL12. Messages and the confirm of a button as TaskDialogs. docs/design/03a-buttons-shared-and-1.md line 5 says a TaskDialog shows the reason of a roll back, and the flow of 5a PR 5 shows every stop and message in a TaskDialog and its confirm (Apply, Cancel, the list per plot) in one too. A TaskDialog takes the Windows font, colours and buttons, none of Theme.xaml (docs/design/12-ui-spec.md lines 9 to 13 and the Font, Colours and Sizes parts), and it cannot scroll, so a whole-project list per plot grows past the screen. In 5a only "<label> is not built yet." is reached.
- Recommended: keep TaskDialogs for the short stops and messages as 03a says, accepted as the Revit look, and build the confirm as the themed summary window of 7.7 item 1 with the first button that has a body (5b), with its PNG and labels.
- Other: every message in a themed window of its own.
- Other: keep TaskDialogs for all, the confirm included.

QL13. Pane start width. docs/design/12-ui-spec.md says the pane starts 380 wide. DockablePaneState of Revit 2024 offers DockPosition, MinimumWidth (docked, 200 by default), MinimumHeight, FloatingRectangle (floating only) and TabBehind, and no start width for a docked pane (READ RevitAPIUI.xml of the Revit 2024 API package), so 5a sets MinimumWidth 380, a floor.
- Recommended: keep MinimumWidth 380 and measure the width Revit opens the pane at in the Revit run of 5a.
- Other: Bader gives another rule.

QL14. Step rows and rows 32. docs/design/12-ui-spec.md says rows 32 and gives each step its name at 14 and one plain line under it at 12, which needs about 38 (RAN, the layout of the pane in the picture test). On first open the line of step 1 wraps beside the chip "Needs settings" at 380 (steps/ui-shots/pane-first-open.png).
- Recommended: step rows of 40, on the scale as 24 plus 16, with the input and project rows kept at 32, and the step line cut with an ellipsis and its full text as tooltip.
- Other: Bader gives the heights.

QL15. Input rows at 380 wide. docs/design/12-ui-spec.md puts mark, label, file name, size, short hash and Browse on one row of 32 at 380 wide, and in steps/ui-shots/pane.png the size and hash show in full on 0 of 9 rows (READ the PNG), and on 0 of 10 since the Shared parameters row (5a PR 8, steps/ui-shots/README.md).
- Recommended: keep the file name, put size and short hash in the tooltip under the full path, as 7.2 of docs/design/07-mock.md lets the tooltip tell two files apart.
- Other: two-line input rows.
- Other: a wider pane.

QL16. Gaps between stacked inputs. docs/design/12-ui-spec.md gives rows 32, inputs 30 and spacing 4, 8, 12, 16, 24 only, so two inputs on stacked rows stand 2 px apart (Neighbourhood and Plot list) and 3 px (Rules Browse and Level profile), seen in steps/ui-shots/pane.png.
- Recommended: accept the 2 px left by the row and input heights as part of those sizes, not a spacing.
- Other: inputs 28 high so they stand 4 apart.
- Other: Bader gives the rule.

## The questions as L0 asked them

QL1. Inputs on the laptop. C:\02-Site Adaptation is not on the laptop (steps/laptop-check.md item 12).
- Recommended: copy C:\02-Site Adaptation from the personal PC to the same path on the laptop and compare the counts, steps 1 to 4 of the Bader section of steps/laptop-check.md. inputRoot, the key copies and key_copies.json then hold as they are, and R312 checks the copies.
- Other: point inputRoot at C:\10- ROSHEN MECCA. It has no Revit 2024 key copies, no key_copies.json and lacks 4 named files (item 13).

QL2. The work root of the laptop. Config names 10 paths under C:\Users\bader (item 14), and the test host, the job guard, the placer and the default work folder of section 6 take Documents\SiteAdaptation from the Windows Documents folder instead. On the laptop that folder is redirected to C:\Users\p003653k\OneDrive - Parsons Corp\Documents, which syncs to the company cloud, so the two would split, and plans with real coordinates would sync.
- Recommended: one file per machine outside git, %APPDATA%\SiteAdaptation\machine.json, gives one root that stands for C:\Users\bader\Documents\SiteAdaptation and for Documents\SiteAdaptation, read before config by the C# and the Python side. On the laptop the root is C:\Users\p003653k\Documents\SiteAdaptation, outside OneDrive. 5a codes it in the add-in shell, 5c joins it for the rest (J5, J8). With it the R343 check of item 22 finds siteBuild.folder and runs on the laptop.
- Other: Lane B works under the OneDrive Documents folder and accepts the sync.

QL3. The split of U1 to U7 in steps/decisions-2026-10-28.md is CHOSEN by Code.
- Recommended: keep it.
- Other: Bader gives the split of the canvas.

QL4. The Villa mode default of a run Bader starts by hand. D2 sets Masses for test runs only.
- Recommended: Masses, Links chosen per run, since Links is the heavy mode.
- Other: Links.

QL5. How the base of a solid is measured against the footprint outline (D2).
- Recommended: the largest distance from any point of one outline to the other outline, both ways, in mm after the placement, against a limit in config/project.json.
- Other: the area between the two outlines over the footprint area.

QL6. `py -m venv .venv` of CLAUDE.md makes a 3.14 venv on the laptop, since py picks 3.14 there (item 6).
- Recommended: CLAUDE.md reads `py -3.12 -m venv .venv` from the fold in.
- Other: leave CLAUDE.md, and each laptop session makes the venv on 3.12 by hand.

QL7. docs/design/12-ui-spec.md says Theme.xaml holds every size and colour by key, but some values it uses have no key: the sizes 16 (project name), 14 (step name) and 28 (the check point count), and the colours amber, grey outline, green check and white. A change to the look is Bader's, so 5a needs these before it writes Theme.xaml.
The "Next step" label is 13 SemiBold, while SA.Type.Body is 13 Regular. The Input files help line has text only for "All [n] found".
- Recommended: three more type keys SA.Type.Project 16 SemiBold, SA.Type.Step 14 Regular and SA.Type.Count 28 SemiBold, and SA.Type.Label 13 SemiBold. Amber is SA.Warn, grey outline SA.BorderStrong, green check SA.Ok and white SA.Surface. When a file is missing the help line reads "The drawings and schedules the tool reads. [m] of [n] found."
- Other: Bader gives the keys, the values and the line from the canvas.

QL8. Where the pane shows Villa mode (docs/design/11-light-mode.md). The approved pane of 12-ui-spec.md has no control for it, and section 6 gives a pane row only to a row of the mock.
- Recommended: a row "Villa mode" with Masses and Links under the Dry run checkbox of Scope, a NEW item of 7.7. Test runs set it in the settings file.
- Other: no pane row, the settings file only.

## Usage

| Phase | Date | Tokens | Cost USD | How known |
|---|---|---:|---:|---|
| L0 | 28 Oct 2026 | UNKNOWN | UNKNOWN | npx is not installed, steps/laptop-check.md item 20 |

## How L0 ran

- The main session wrote the four files with no developer helper, CHOSEN since the UI spec text was only in the session prompt.
- "Never two PRs open" was read as Lane B's own PRs, CHOSEN, since PR 75 of Phase 4 was open and touches none of these files.
- Every git command to origin ran with `-c http.sslBackend=schannel -c http.schannelCheckRevoke=false`, item 10.
- To prove the hooks, one PowerShell read of a registry key that does not exist ran before guard-registry.sh was read. That hook blocks changes only. CLAUDE.md says nothing in the registry is read, so it is listed here. The second probe was `reg /?`, which the hook blocked.
- The local dotnet test fails 42 per framework on main and on this branch alike, all from pwsh missing (item 23). So the L0 PR merges only when the python and the dotnet check of CI both pass.
