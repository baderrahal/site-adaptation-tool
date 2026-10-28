# Lane B, the laptop lane

Phase 5 and 6 on Bader's company laptop (D1 of steps/decisions-2026-10-28.md). While Phase 4 is open this file holds what the shared records get at the fold in, and it is where a Lane B phase is marked done.

## Phases

| Phase | Status |
|---|---|
| L0 Laptop check, light mode design, UI spec | done, Lane B, laptop, on the merge of branch docs/laptop-l0 after its review |
| 5a Install, ribbon, pane, one transaction per button | running since 28 Oct 2026, Lane B, laptop |
| 5b Button 1 Set up site | open |

After 5b Lane B waits until 4g2, 4g3 and 4h are all marked done in steps/01_next.md. Bader's list of the Lane B phases after that, in order: C2 the C# check of 4g3 and 4h, 5c, the Revit run after 5c, 5d, 5e, 6a, 6b, 6c.

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
- C2, after 4g3 and 4h are done: in a git worktree of origin/main outside the repo, the gate, the C# reads, the parity on the whole project and the 7 keys on net48 and net8.0 as 4g2 ran it, and the build peak, three runs each, the free memory at the start of each read. One PR adds the result to steps/log.md and closes the C# items the 4g3 and 4h log entries left for the laptop.
- 5c: Rule 3 first, then every join item below and Q117, the optional inputs of button 3, then Q103, then buttons 2 to 4.
- 5d: Q115, the wall extent per kind of edge, before button 7 places walls. Q116, the ramp of turned plots on the open edge.

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
| DWG read of buttons 1 to 3 (baseplan, villa blocks, curbs, the check points of button 1) | the worker CadReader: dotnet/CadReader/CadReader.csproj line 11 ACadSharp 3.8.0, dotnet/CadReader/WorkerReads.cs line 5, CadExtractor.cs line 5 | needs no AutoCAD and no Civil 3D, ACadSharp is a managed DWG reader | READ the csproj, its only package references are ACadSharp, DocumentFormat.OpenXml and PdfPig |
| Road surface of button 2 | the LandXML files of siteBuild.roadSurfaces read by System.Xml in Common, dotnet/SiteAdaptation.Common/Read/TinSurface.cs, docs/design/04-inputs.md line 16 | needs no Civil 3D at run time | READ 04-inputs.md. The LandXML snapshot was exported once on the PC, a new road surface drawing would need a new export, which no button does |
| None of the buttons | the data book tools: python/c3d_run.py line 167 (accoreconsole), python/c3d_window.py, python/c3d_inventory.py, dotnet/SiteAdaptation.C3DProbe/Civil/SiteAdaptation.C3DProbe.Civil.csproj line 7 (AutoCAD 2026 HintPath, not in the solution), dotnet/SiteAdaptation.C3DWatcher/Program.cs line 13 | not needed. Whether C3DProbe.Civil builds and loads in Civil 3D 2025 is UNKNOWN, not measured, since no Phase 5 or 6 step runs it | RAN a grep of dotnet/SiteAdaptation.TestHost, Common and CadReader for C3DProbe, C3DWatcher and accoreconsole: no call, one comment in CadModels.cs line 29 |

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

The questions as L0 asked them:

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
