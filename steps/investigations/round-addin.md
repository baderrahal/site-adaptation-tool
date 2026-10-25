# Round ADDIN, 2026-10-25, Part 1, the add-in for Revit 2024

Counts and distances only. This file holds the plan of the round and gathers its findings, phase by phase.

## Plan

Written 2026-10-25 before the first edit. The round prompt, word for word, is round-addin-prompt.md in the scratchpad of the main session, outside the repo. Part 2 (sheets) is not started in this round.

### Preflight, done before this plan

- Repo root C:\Users\bader\Documents\GitHub\site-adaptation-tool, branch main, main equal to origin/main at ff212e5 after git fetch (RAN).
- Hooks live: guard-main.sh refused a dry run commit on main and guard-registry.sh refused a harmless echo with the word reg (RAN by the main session, 2 of 2 refused). Their output, word for word:
  - guard-main.sh, on git commit --dry-run --allow-empty -m hooktest on main: "Blocked by .claude/hooks/guard-main.sh: git commit on main in C:\Users\bader\Documents\GitHub\site-adaptation-tool. Work on a branch and merge by pull request."
  - guard-registry.sh, on echo hook test reg probe: "Blocked by .claude/hooks/guard-registry.sh: reg.exe. Nothing in the registry is changed by this tool, Bader answers it by hand."
- Worktree site-adaptation-tool-17: not on disk, git worktree list shows main only (RAN). Nothing to remove.
- Files named by the prompt: all present (READ by listing) but one. 01101-PSN-IMC-ZZZ-PIM-TU-000001_TEST-MASTER.rvt is no longer in 00_Revit Models\Zone 02\ARC, where testhost_MST_log.json puts it (READ). BasicFileInfo of Revit All Zones\01101-PSN-IMC-ZZZ-PIM-TU-000001-zone2.rvt gives lastSavePath ...\Zone 02\ARC\01101-PSN-IMC-ZZZ-PIM-TU-000001_TEST-MASTER.rvt, not workshared, 48,742,400 bytes (RAN rvt_info.py on it). So the master was moved into Revit All Zones and renamed. It is not a missing input, it is the answer to Phase 1 step 2: the IMC key is the original in Zone 02\ARC (hash fef2bfee... in round_17\logs\originals_after.txt, READ) and a new question records it. No hash of the master was ever written, so the hash compare of step 2 is replaced by this BasicFileInfo proof and a link and floor count in Phase 1.
- AI MAX version 3, 19 Oct 2026, is installed at C:\Users\bader\.claude\skills\synced\6ef2e1cc-...\ai-max (READ). Its assets hold breaker, claim-checker, log-reader, prober, researcher and reviewer. developer and runner have no file there, rounds.md line 186 describes both, so Phase 0 writes those two from that text.
- Free disk 56 GB on C (RAN). Revit All Zones\2024 does not exist yet (Phase 1 makes it).
- "Claude outputs/" is untracked in the repo root and is not this round's. It is left alone and never added.

### How the round runs

- The main session plans, starts one helper per PR, runs the checkers and merges. It edits no file in the repo.
- Per PR: a developer agent on opus (fresh context) reads CLAUDE.md, steps/PROGRESS.md, this plan and, from Phase 4 on, docs/addin-design.md. It branches from fresh main, makes at most four changes, proves each on the smallest real case, runs the gate, writes the records, pushes and opens the PR. It does not merge.
- Then the breaker (opus) and the claim-checker (sonnet) read the PR in parallel. Each finding goes back to the developer to fix with a test. Third attempt at one fix stops and becomes a question.
- Then gh pr checks --watch, merge, delete the remote branch, pull main. Never two PRs open.
- If the agent files of .claude/agents are not picked up by this session after Phase 0, each helper runs as general-purpose on the same model with the instruction to read and follow its agent file first. The log says so.
- Records per PR: steps/investigations/round-addin.md (counts, how each is known), the top entry of steps/log.md rewritten for the round (one page), steps/PROGRESS.md (60 lines at most). Commits and PRs carry no attribution line (R13).
- Rule numbers by phase, so no two PRs take the same id: Phase 0 R300 to R309, Phase 1 R310 to R319, Phase 2 R320 to R329, Phase 3 R330 to R344, Phase 4 R345 to R369, Phase 5 R370 to R384, Phase 6 R385 to R399. New questions take the next free ID from Q062.
- Every phase hashes the sources it reads before and after (the 7 Parsons models, the Zone 02\ARC originals, Prototypes_2024, the data book snapshot it uses) with round_files.py.
- Revit and Civil 3D windows: one at a time under the shared lock of R200, 2 GB floor, watcher by rule 3 in our own process only, no registry.

### Order of work

#### Phase 0, harness and open fixes (2 PRs)

PR 0a chore/addin-harness
1. Copy the skill into .claude/skills/ai-max. Write .claude/agents: developer, reviewer, breaker (opus), prober, claim-checker, log-reader (sonnet), runner (haiku). Read each.
2. Move closed entries of steps/log.md to steps/history/ until it is under 100 KB.
3. CLAUDE.md under 200 lines with option B, Q050, the answer keys, the mock, the two parts and how this round runs. docs/addin-mock.md word for word. PROGRESS rewritten. This plan saved as steps/investigations/round-addin.md.

PR 0b fix/addin-open-fixes
4. Q045: no villa block read from R06_0000, with a test. Q044: config/area_codes.json by job (parsons empty until Phase 2, native from Round 17), guessed rows gone, readers and tests updated. Q049: no attribute values in the Civil 3D read. Q061: image step 30 minutes in config, then the NBH 03 image.
5. Outside the repo: delete the IFC copy (Q047) and the two Cutsheet lock files (Q048). Move the kept files of round_z4\work, round_16\work and round_17\work (2024 key copies U11, U12, U13, K24 and the files TEST-NEW of Z4, IMC 16, IMC R17B, IMB R17) into Documents\SiteAdaptation\kept, then delete the three work folders (Q035, Q060). Each file listed with its size. The NBH 03 image is made before its file moves.
6. Delete the remote branch feat/revit-links-walls-ramps after git shows every commit of it in main.
7. Close Q035, Q044 (structure), Q045, Q047, Q048, Q049, Q060, Q061 in steps/questions-closed.md with answers. Q028, Q046 and Q050 were closed by 0a.
8. R277: parsons2024Rev6 as the default level profile of a new build in the rule text and the code, with a test (Q050).

#### Phase 1, the 7 Parsons models (1 or 2 PRs)

- Hash the 7 files and the Zone 02\ARC originals. BasicFileInfo from bytes for each.
- IMC: record the moved master (above), take Zone 02\ARC\01101-PSN-IMC-ZZZ-PIM-TU-000001.rvt as the IMC key, new question.
- Save 2024 copies into Revit All Zones\2024, one window at a time, with the existing siteUpgrade job (R204) or its extension, check Format 2024 by bytes, reuse a copy whose source hash matches.
- Links of each model against Prototypes_2024, upgrade a missing one only where its 2021 file is on the machine.
- SE models of IMA and IMB against the TU models.
- Coordinates of each model: survey point, project base point, shared site, angle to true north, levels, units, and whether all zones share one survey point.
- Measured: free memory, open and save time per model, peak memory of one window, each limit set from them with the reason.

#### Phase 2, read the 7 keys (2 PRs)

- Reader extended only where it lacks a field: walls with base, top and location, spots with hosts, survey and project points, levels, the 9 RRE parameters and CASE. Read each 2024 copy.
- `plan_<code>_past.json` per model, plan_imc_past.json never overwritten, compared.
- Join to plots.csv by location, then official number, Zone and NBH, Parsons rows in past_all.csv with a source column, the plots on no face or two listed.
- Parsons table of config/area_codes.json with counts. Q011 closed if IME names its NBH.
- Spot elevations at the plot entrance against El @ Entry (Q001).
- Plan files keep what Part 2 needs: plot numbers, setting out points, levels, wall lengths.

#### Phase 3, rules on the Parsons keys and the design (2 PRs)

PR 3a: every Round 17 rule re-measured per model and over all 7, the Native share beside it, odd plots to the override list with source. Levels to the mm with the cause per plot, share at 1 mm and 5 mm, the pass limit in config. Strong rules under parsons2024Rev6 in rules/roshn-makkah.json. A source field on every override, parsons only applied by default, counts per source. Second method at seeds 14 and 7.

PR 3b: docs/addin-design.md before any add-in code. Folder structure, the 8 buttons with inputs, outputs, checks and rule numbers, the data path from input files to Revit, where Part 2 attaches, each mock screen and file filled with real IMC rows (in the design only as counts and layout if a value is a real coordinate or level, since none goes in git). Libraries checked from package metadata into docs/licences.md.

#### Phase 4, the logic in C# (about 4 PRs)

- 4a: the projects of the design (logic with no Revit reference, Revit add-in, tests), the parity harness (C# writes each step's output for the data book, Python compares field by field with the tolerance in the test), coordinates from CAD, plot faces, labels and official numbers.
- 4b: footprint clusters, front edge and corner front, link frame, hand rules.
- 4c: ramp and floor outlines, road level snap from LandXML, levels under parsons2024Rev6.
- 4d: walls, overrides, issues, the level Excel and the report Excel writers.
- A step is done only when parity holds on every plot of the data book. Real data stays outside git, so the parity run is local and its counts go in the log, and each step also has tests on small dummy data built in code that run on CI.

#### Phase 5, the Revit add-in (about 4 PRs)

- 5a: ribbon tab and panels, dockable pane, settings saved with the project, install script (pwsh 7) and .addin manifest for %APPDATA%\Autodesk\Revit\Addins\2024.
- 5b: button 1 Set up site with its dialog, on an empty project (metric template and None with imperial units) and on an existing site file.
- 5c: buttons 2, 3 and 4 with the check table.
- 5d: buttons 5, 6, 7 and 8. One transaction per button, rolled back on any error with the reason shown, dry run first, scope by project, NBH or plot list, no duplicates on a second run, a missing step or input named and the button stops.

#### Phase 6, end to end on all zones (about 3 PRs)

- 6a: one NBH from a new project through all 8 buttons in the test host, fixes.
- 6b: each of the 7 models from DefaultMetric, one zone again from None, button 1 on the IMD 2024 copy changing nothing, compare tables per zone with the worst 10, Round 17 IMC beside the new IMC, 10 random plots per zone by id.
- 6c: the mock check by screenshots of the ribbon, pane, dialog (new and existing) and check table, both Excel files opened and compared item by item, an image of every new file, clean up into Documents\SiteAdaptation\round_addin. Then steps/bader-addin.md, the README section, the final log entry, the Your turn list, questions closed, and the PROGRESS PR.

### Risks known now and how each is handled

- Agent files written mid session may not load in this session: fallback above.
- Screenshots of the Revit UI unattended: taken by the test host of our own Revit window only (no other process), or the step becomes a question.
- A project with no template: NewProjectDocument with UnitSystem Imperial stands for it in the test host, so button 1 is proven to switch units to metric.
- Libraries inside Revit (ACadSharp, an xlsx writer, a PDF reader): checked for licence from metadata and for load conflicts with the assemblies Revit ships, on the smallest case first.
- The SE models of Zone 1 may hold no villa links: then Zone 1 rules fall back to the Native reads for those plots and the gap is a question.
- Real coordinates and levels never go into git: the design shows them as counts or in files outside the repo.

## Phase 0b, PR fix/addin-open-fixes

Branch fix/addin-open-fixes from main at 865a80e, equal to origin/main after git fetch (RAN). Rules R300 to R302 are new, R277 and R257 changed. The session started in the repo, so both hooks were live: guard-registry.sh refused two commands whose text held its trigger word, a grep pattern and a heredoc of this section, and nothing in the registry was touched (RAN, 2 refusals).

### Originals (R201, R202)

- 40 files hashed with round_files.sha256 before the first edit and after the last window: the 16 Native Files models, the 7 Revit All Zones models, the 2 Zone 02\ARC models (IMC and IMD TU), the 13 files of Prototypes_2024 and the 2 schedules of round_17\logs\originals_list.txt. Lists in Documents\SiteAdaptation\round_addin\logs\originals_0b_before.txt and originals_0b_after.txt. 40 of 40 equal, 0 changed or gone (RAN). The 32 of them in round_17\logs\originals_after.txt equal those hashes too, 32 of 32 (RAN).

### R277, Q050: parsons2024Rev6 for every new build

- rules/roshn-makkah.json levelProfiles.default is parsons2024Rev6. plan_rules.profile_name takes the named profile, else the default, and refuses a default that is missing or names no profile, note or default (CHOSEN, Bader's value lives in the rules profile, not in code). plan_from_book.py builds the rules on every run and prints the profile it applied, named or the default. Before, a build with no --level-profile took the estimate of R209 (READ, plan_from_book.py main).
- Tests: 10 in test_round_addin.py and the 2 R277 tests of test_round17.py, the one that said there is no default now tests a profile with no default, 12 collected by pytest -k r277 at the end of pass 3 (RAN) (corrected in review pass 3).

### R300, Q045: villa blocks from the Building Footprint

- SEARCHED the repo (every .py, .json, .cs and .ps1) for R06_0000 and R06_0001: no reader takes them for villa blocks since Round 12b. Until Round 12b drawings.villaBlocks was R06_0000, whose 248 inserts are all Villa_VL2 (READ, git show 6e46893^:config/project.json, steps/history/round-12.md lines 9 and 52). Since 12b it is Makkah Building Footprint.dwg, and the data book wires footprintBlock from the role buildingFootprint (READ, config/project.json, python/c3d_probe.json). R06_0000 is read as reference only: the r06_0000Block and r06_0000Number columns of the data book (dataBook.join.masterBlocks) and the R06 to R06_0000 compare of R176 in wiring.py. Those stay.
- One stale text: the skip reason of R06_0001 said R06_0000 is read for the villa blocks. Corrected.
- New: drawings.referenceOnly lists both copies. project_profile.refuse_reference_only refuses a drawing or a blocks.json of that list in cad_analysis.read_blocks (so block_scan, block_offsets, block_proof, round13_check), footprint_check.footprint_blocks (footprint_check, plan_from_book and majority --footprint-blocks), the analysis stage of full_run and the footprint role of data_book and wiring. 3 tests (RAN, 3 passed). The dotnet code reads no R06 drawing (SEARCHED).

### R301, Q044: config/area_codes.json by job

- One table parsons and one native. native from the Round 17 coverage of R267: IMA 1.1 with 02 (Zone 3) 309 and 01A (Zone 4) 84, IMB 1.2 with 03 431 and 01A 150, IMC 1.3 with 021 399 and 013 382, IMD 1.4 with 012 336 and 022 294 (READ, steps/investigations/round-17.md). parsons holds the IMC key 337 in 012 (model), the IMC schedule 337 in 012 and the IMD schedule 410 in 013, marked schedule (READ, data-book.md lines 267 and 268).
- Gone: the guessed rows IMA 021, IMB 022, IME 02, IMF 03, and the IMG row, which held text only and no count. Phase 2 fills parsons from the 7 models.
- im_codes.py: load_table refuses a row with a status, no positive count, no kind of model or schedule, no file, no measured note, an IM code twice in a job, or a job missing. Each source names its job in dataBook.data5.imCodes.sources and is held against the row of its own job: agrees, differs, noRow or noJob. R257 points to R301. 16 tests of R301 and 2 of R257 (RAN, all passed).
- Second method: the 8 native rows parsed again from the coverage table text of round-17.md by a regular expression, 8 of 8 equal, 0 on one side only, and the of-count in each measured note right on 8 of 8 (RAN).

### R302, Q061: the image step limit and the NBH 03 image

- testHost.imageStepMinutes 30 and imageMarginMinutes 10 in config/project.json. Run-TestHost.ps1 writes both into an image job as imageMinutes and imageMarginMinutes, so the image job is 40 minutes and its window 43 when -TimeoutMinutes is not given, and it refuses a -TimeoutMinutes under 43 (corrected in review pass 2). The guard refuses an image job with no imageMinutes or one outside 0 to MaxImageMinutes (120, a bound against a mistyped job), and imageMinutes on any other job, and lets an image job total run to 29 minutes plus its image minutes. The image step takes TestHostGuard.ImageStepMinutes(job), the constant 10 is gone. 8 .NET test methods in RoundAddinTests.cs and 2 Python tests (RAN) (corrected in review pass 3).
- Build and install: dotnet build 0 errors and 1 old warning, Install-TestHost.ps1 -NoBuild -SkipSigningReport under PowerShell 7.6.7, the three manifests kept. Common the same in the reader, the placer, the watcher and the build is backed by two saved installs, each for one build only (RAN). install_0b_pass2.txt holds for the build of the R303 watcher run in I28, now withdrawn. install_0b_pass3.txt holds for the build of d22655e, the watcher equal to main, run in I29: the script printed True and Get-FileHash gives one SHA-256 for the three installed Common DLLs and the build one, and one for the installed and the built watcher (corrected in review pass 3). The installs of the 03:38 and 05:05 builds left no saved output, so they back nothing (corrected in review pass 2). The script skips the CodeSigning read with that switch (READ, Install-TestHost.ps1 lines 88 to 91).
- Windows, one at a time under the lock of R200 with round ADDIN-0b, Revit 2024.3. The watcher answered 5 boxes in each in our own Revit only, Do Not Load for RCRC Green and RCRC Green Audit, Load Once for our three (RAN, round_17\logs\windows.jsonl and run_I2x_image.txt):

| Code | Area of NBH 03 | Free GB at the lock | Open s | Image s | Result | Peak working set MB printed by Run-TestHost.ps1 |
|---|---|---|---|---|---|---|
| I20 | whole, 735 by 733 m | 5.46 | 84.3 | 1,805.2 | timeout at 30 minutes | 1,692 |
| I21 | south west quarter | 8.12 | 84.1 | 55.3 | ok, 128 links and 232 floors in the view | 1,995 |
| I22 | south east quarter | 7.84 | 74.8 | 53.7 | ok, 118 links, 217 floors | 1,810 |
| I23 | north west quarter | 8.92 | 76.3 | 56.4 | ok, 131 links, 241 floors | 1,995 |
| I24 | north east quarter | 8.80 | 77.3 | 51.9 | ok, 102 links, 182 floors | 1,816 |

- A first I20 start stopped at reading its spec, a JSON escape fault in the spec file I wrote, before any Revit started (RAN, run_I20_image_specfault.txt). It is not counted as an image try.
- The whole area failed once at 30 minutes with no dialog in the test host log. The I20 watcher report runs from 00:40:42 until Revit exited at 01:12:55 UTC with only the three Revit frame windows and the five security boxes, while the I21 to I24 watchers ended at the first log step after about 20 s (RAN, testhost_I2x_watcher.json). The I20 report backs no statement about windows during the image step. On I25 the watcher process had 0.671875 s of CPU at 05:18:02 and again at 05:19:02 local while Revit did not respond (RAN, saved word for word in round_addin\logs\evidence_I25.txt). That it was held in a UI Automation call is read from its code of then, whose only waiting calls were UI Automation calls, and was not measured (UNKNOWN), and whether the I20 watcher was held the same way is UNKNOWN (corrected in review pass 2). A quarter window takes 2.62 to 2.84 min in windows.jsonl over the 7 quarter runs I21 to I24 and I26 to I28, and only the image step of a quarter is 51.9 to 57.7 s (READ, windows.jsonl and testhost_I2x_log.json) (corrected in review pass 2). So the whole area is not slow by its size, and why it hangs is UNKNOWN (Q063). The quarters were CHOSEN as the second try, since a repeat of a run that hit its limit tests nothing new. The quarter views count 479 links and 872 floors, over the 404 and 808 of the file, since plots on a quarter edge show twice (RAN). Opened I21 and I23: houses, floors and grid tags drawn, cut at the quarter edge. The export fits a wider extent than the crop (two symbols far to the right in I21), so in I21 the houses fill about a fifth of the width (READ, the PNG files).
- The NBH 03 file was unchanged by hash after every run (RAN, Unchanged True in all 5 run logs).

### Q047, Q048: the IFC copy and the lock files

- Deleted data_book\work\Downloads\1TB02012-012D03-UNA-00-XX-MOD-UT-00010.ifc, 10,497,165 bytes, equal by SHA-256 and size to Downloads\New folder\1TB02012-012D03-UNA-00-XX-MOD-UT-00010.ifc, which stays (RAN). The empty Downloads folder of data_book\work stays. dataBook.data5.ifc.copy still names the copy, so a run of ifc_header.py needs the copy made again.
- Deleted Makkah Phase 1 Cutsheet - 500.dwl (61 bytes) and .dwl2 (211 bytes) beside the Cutsheet copy in data_book\work\Base Plans. Backed: the Civil 3D window of 24 Oct 17:56:55 on that copy, stopped by the 2 GB floor after 15.7 s, lists both as its new files (READ, data_book\logs\20261024-175655_cutsheet.run.json). That they name user bader and that no AutoCAD process ran at the delete were READ before the delete with no saved output (corrected in review pass 2).

### Q035, Q060: the kept files and the three work folders

- Kept by the Kept lines (READ): round-z4.md line 80, round-16.md line 83, round-17.md line 147. Round 17 lists the site specs and reports but no parameter file and no key settings, so those went with its folder.
- Copied into Documents\SiteAdaptation\kept, each copy hashed against its source before any delete, then the three work folders deleted whole (RAN, round_addin\logs\keep_and_clean.json). Second check with PowerShell Get-FileHash on the kept copies: 21 of 21 equal, 0 differ (RAN).

| Round | File | Bytes | Fate |
|---|---|---|---|
| z4 | 01101-PSN-IMD-ZZZ-PIM-TU-000004_TEST-K24.rvt | 74,301,440 | kept\round_z4 |
| z4 | 01101-PSN-Z4-TEST-NEW.rvt | 6,594,560 | kept\round_z4 |
| z4 | project_z4_past.json, site_parameters.txt, site_report_z4n.json, site_z4.json | 11,141, 2,114, 1,944, 1,402 | kept\round_z4 |
| 16 | 01101-PSN-IMC-TEST-NEW.rvt | 8,380,416 | kept\round_16 |
| 16 | site_imc.json, site_parameters.txt, site_report_i16.json | 1,440, 2,114, 1,942 | kept\round_16 |
| 17 | 01101-PSN-IMA-ZZZ-PIM-TU-000001_TEST-U11.rvt | 24,485,888 | kept\round_17 |
| 17 | 01101-PSN-IMB-ZZZ-PIM-TU-000002_TEST-U12.rvt | 77,070,336 | kept\round_17 |
| 17 | 01101-PSN-IMC-ZZZ-PIM-TU-000003_TEST-U13.rvt | 76,161,024 | kept\round_17 |
| 17 | 01101-PSN-IMC-R17B-TEST-NEW.rvt, 01101-PSN-IMB-R17-TEST-NEW.rvt | 9,891,840, 8,278,016 | kept\round_17 |
| 17 | site_03_c.json, site_imc.json, site_imc_b.json, site_report_i17.json, site_report_i18.json, site_report_i19.json | 1,357, 1,355, 1,357, 1,946, 1,947, 1,946 | kept\round_17 |
| 17 | project_u11_past.json, project_u12_past.json, project_u13_past.json | 14,198 each | deleted |
| 17 | site_parameters.txt, site_parameters_b.txt, site_parameters_c.txt | 2,114 each | deleted |

- Totals: 27 files, 21 kept with 285,195,525 bytes, 6 deleted with 48,936 bytes. round_z4\work, round_16\work and round_17\work are gone (RAN). The images, logs and plans outside the work folders stay.
- dataBook.surfaceEntry.key.settings of python/c3d_probe.json pointed at round_z4\work\project_z4_past.json, now at kept\round_z4, and the path resolves and loads (RAN). siteBuild.newFile of config/project.json named the file now in kept\round_17. It is null since the review fixes, and a job that needs it refuses with the setting named. siteBuild.folder still names round_17, Phase 1 sets siteBuild again.

### Q049: attribute values of the probe

- DataCommands.cs line 198 of the probe reads an attribute reference only when it is not Invisible, and only such an attribute reaches the attr_ columns of blocks.csv (lines 200, 201, 475 to 483) and an ATTRIB row of texts.csv (line 202) (READ). The line dates from 601129f, 24 Oct 18:02, and the Road Layout tables were written at 18:45 that day (READ, git blame and the file times).
- The Road Layout tables of the probe hold 0 ATTRIB rows and 0 attr_ columns, while the probe met all 824 top and 5,045 nested inserts (RAN, and DATA-5). So the probe writes none of the 879 values, no code changed. Why it writes none is UNKNOWN: line 198 is the only attribute filter of the probe, but that the 879 are invisible was not read from the drawing.
- The DATA-5 line that the probe writes no attribute value holds for Road Layout only: the probe table of the Rev 9 drawing MP-SK-01-03-006_R9.0 holds 2,139 ATTRIB rows and 16 attr_ columns, room names, finish numbers and levels of the bound building footprint, and rev9_drawing.survey (R189) reads every text row (RAN, READ). The 11 CadReader tables of the data book hold none (RAN). Q062 asks whether those stay.

### The remote branch feat/revit-links-walls-ramps

- merge-base --is-ancestor of its tip 5fbf27a with origin/main: no, 1 commit not in main (RAN). It was squash merged as PR 4, 0a0d4c3, which is in main and holds the same tree as the tip, an empty diff (RAN). File by file: 12 files, 2 equal main, 10 changed later on main, 0 neither (RAN). Deleted with git push origin --delete, git branch -r shows origin/main only (RAN).
- It was deleted before the image existed, where the prompt put it after. It touches nothing the image needs.

### Fixes after the breaker and the claim-checker (new commits on PR 42)

- R280 (item 1): an override applies only when the job of its source model is the job of the level profile. The job of a source is the group of majority.groups that holds the keys its overrides name, and each level profile names its job in rules/roshn-makkah.json. A mismatch is refused with both named. The 5 source models of the real override list map to one job each, 4 native and the IMC key parsons (RAN). At the end of pass 3, 11 R280 tests are collected, 9 in test_round_addin.py, 8 of them through plan_from_book.main, and 2 in test_round17.py (RAN) (corrected in review pass 3).
- R277, R279 (item 2): the rule text says a build that names no profile applies the default profile, the hand tables, the TH1-IM fit and only the overrides of its job. A test through main sees the default name and the hand tables reach build.
- R300 (items 3 to 5): a reference drawing matches by its stem whatever the case, CAD extension or copy suffix, every villa block reader takes the settings its caller loaded, and data_book_retest, stray_units, c3d_connect, wiring_check and z16_check read unit blocks through project_profile.read_villa_blocks. A test lists every Python file that reads a blocks.json or blocks.csv and is no villa block reader, 8 files with their reasons. 33 R300 tests collected by pytest -k r300, at 83583e3 and at the end of pass 3 (RAN) (corrected in review pass 3).
- R301 (items 6, 7): a guessed status on an area is refused, a source with no keyed plot gets noKeyedPlot, a source agrees only on zone and neighbourhood together, the job names come from majority.groups, measured_count names its rule.
- Item 8: dataBook.data5.ifc.retired names Q047, ifc_header.py and data5_check.py skip the IFC with that reason. data5_check.py then stopped earlier, at the CadReader reads of the two road layouts the DATA-5 clean up deleted. Made again with xref_search.read_copy from the data book copies, which stayed unchanged by hash: Roads layout 27.2 s and 53,179,790 bytes, Base Plans layout 0.7 s and 1,779,522 bytes (RAN). data5_check.py then ran to its end, exit 0 (RAN): IFC skipped by Q047, area 20 of 20 and 19 plus 1 noDlusRow, parcels 20 of 20 at both seeds, curb 20 of 20 at both seeds, DXF no DXF for 3 roles (deleted by DATA-5), file names equal for IMC (221) and one more each for IMA (5 to 4), IMB (5 to 4), IMD (199 to 198), IME, IMF and IMG (1 to 0). Recounted without the Revit All Zones folder: IMA, IMB and IMD equal DATA-5, IME, IMF and IMG 0, and IMC 220, so each extra name is the one model of its code in that folder (RAN). The folder was made at 00:53:11 on 25 Oct, after im_codes.json of DATA-5 (00:09:39). Only IMA, IMB, IME, IMF and IMG were saved there at 00:53, IMD carries a save of 22 Oct 14:07 and arrived at 00:53:58, and IMC carries 24 Oct 08:58 and is the IMC master moved in from Zone 02\ARC (see the preflight), so its count stays (READ, file times) (corrected in review pass 2). siteBuild.newFile is null and newSite and linksAndFloors refuse with the setting named, round_files clean says so when work is gone.
- R302 (item 9): testHost.imageMarginMinutes 10 (CHOSEN, several times the open and the close below), a plain image run takes a window of 43 minutes and a job total of 40, the session deadline is TestHostGuard.TotalMinutesOf. The NBH 03 file opened in 74.5 to 84.3 s over the 8 runs I20 to I27 and 76.0 s in I28, and closed in 2.6 to 3.0 s over the 6 runs of them that closed and 3.8 s in I28 (READ, testhost_I2x_log.json). These measures moved here from the note of config/project.json, which keeps only the reason (corrected in review pass 2). I25 ran with no -TimeoutMinutes (READ, its command in round_addin\logs\evidence_I25.txt), and the script did not stop Revit at the old 30 minutes: run 32.1 min, stopReason null (READ, testhost_I25_run.json) (corrected in review pass 2).
- R303 (item 10), first attempt, withdrawn in review pass 3 with the whole of R303 (see below): Run-TestHost.ps1 ran the watcher with --watch-job for the window limit, the job phase listed windows by Win32 and still read and clicked a dialog by UI Automation. On I25 the watcher was held through the image step, on I26 the start phase from the open to the end of the image step, on I27 it watched to the end (RAN). The second breaker and claim-checker pass named what was left in it: UI Automation calls on the main loop for a dialog that was not hung, a main frame whose file title holds Security counted as a dialog, a silent click entry, and a Revit the worker stopped could read as Revit exited. Replaced by the second attempt below (corrected in review pass 2).

### The whole area retry, I25

- One retry of the whole NBH 03 area on a work copy of kept\round_17\01101-PSN-IMB-R17-TEST-NEW.rvt in a round_17\work made for it, equal to the kept file by hash before and after, under the lock with 6.96 GB free (RAN). Open 82.1 s, image step timeout at 1,807.9 s, run 32.1 min, the copy unchanged (RAN).
- Measured from outside while it ran (RAN, round_17\logs\revit_samples_I25.txt, 39 samples every 30 s from 05:19:14 to 05:38:19 and a last line at 05:38:49 that Revit was gone): Revit not responding on 39 of 39, private memory 10,732 to 10,733 MB and working set 4,909 to 4,913 MB, CPU from 667 to 1,439 s, about 0.7 of one core, free memory 2.16 to 2.77 GB (corrected in review pass 2). Like for like by the same measures but not the same means (corrected in review pass 3): the I25 sampler took a sample every 30 s with no watcher column (script round_17\logs\sampler_I25.ps1, word for word from evidence_I25.txt), the I28 sampler every 5 s, waited for Revit to start and added the CPU of the watcher as a last column (script round_17\logs\sampler_I28.ps1). On the quarter I28 (RAN, round_17\logs\revit_samples_I28.txt, 28 samples): private memory up to 6,377 MB and working set up to 5,663 MB, free memory down to 1.10 GB, under the 2 GB floor of R200, which the lock checks only before a window. The 1.8 to 2.0 GB of the quarter table are another measure, the peak working set Run-TestHost.ps1 prints, which read 1,982 MB on I28 while the sampler saw 5,663 MB (corrected in review pass 2).
- A Win32 list of every window of that Revit at 05:19:35, read only (RAN, saved word for word in round_addin\logs\evidence_I25.txt): two visible windows, the main frame and the GUID Monitor window, both hung, no dialog and no message box, 28 hidden windows. That list is of I25 only, at one moment (corrected in review pass 2).
- So in I25 the whole area export was busy and showed no dialog at 05:19:35, and paged under memory pressure. By private memory the whole area held 10.7 GB and a quarter rises to 6.4 GB, why the whole area stays busy past 30 minutes is UNKNOWN (corrected in review pass 2). Q063 stays open with this.
- I26 and I27 were checks of the watcher on the south west quarter, not image tries, 2.8 and 2.6 min each, their images equal in size to I21. The work copy and round_17\work were deleted after I27 (RAN).
- Run-TestHost.ps1 printed a peak memory of 1,884 MB for I25 and 1,982 MB for I28 while the samples show a 4.9 and a 5.7 GB working set, why the script reads it lower is UNKNOWN. A [Math]::Max of an int and a long over 2 GiB in pwsh 7 gives the right long, so that is not it (RAN).

### R303, second attempt: the job phase only lists windows, withdrawn in review pass 3

- Withdrawn: the third review pass found two new faults in the start phase worker. A worker already running when the first log step lands still made UI Automation calls and could log a job dialog as a security dialog, and an ElementNotEnabledException from a disabled button faulted the worker, gave exit code 4 and left Revit unwatched through the start. A fix would be the third attempt, which stops by the round rule. The watcher, SecurityDialogs.cs, the R108 click test and the watcher parts of Run-TestHost.ps1 are back to main, R303 is marked withdrawn in docs/rules.md and its 9 tests are gone, Q064 asks Bader. The bullets below are the record of the attempt, only the memory and time measures of I28 are still used (corrected in review pass 3).
- Code (CHOSEN, the shape the lead set): after the first log step the watcher lists the top windows of the Revit process the test host started by EnumWindows filtered by GetWindowThreadProcessId, and logs title, class, visible and time once per window and state in jobWindows, never among the dialogs. In the job phase it makes no UI Automation call, reads no button and clicks nothing. The start phase answers by rule 3 as before. When the watch ends the main loop waits up to 90 s for the start phase worker and its result wins, a faulted worker is logged with its exception and gives exit code 4, every report write has one retry. Run-TestHost.ps1 reads the watcher handle so its exit code is kept, counts security dialogs from the start phase only and prints the stopped dialog only on exit code 3. 9 R303 tests and 1 R108 test changed (RAN, all pass).
- Proof on the smallest real case, I28, the south west quarter of NBH 03 from a work copy of kept\round_17\01101-PSN-IMB-R17-TEST-NEW.rvt, equal by SHA-256 before and after the run, under the lock with 7.29 GB free, Revit 2024.3 (RAN, round_17\logs\run_I28_image.txt, testhost_I28_log.json, testhost_I28_watcher.json): open 76.0 s, image 56.6 s with 128 links and 232 floors in the view, the counts of I21 whose image step took 55.3 s (corrected in review pass 3), close 3.8 s, window 2.83 min, log result ok, watcher result Revit exited and exit code 0 read by the script.
- Summary counts: 5 security dialogs, all of the start phase (03:23:07 to 03:23:11 UTC, Do Not Load for RCRC Green and RCRC Green Audit, Load Once for our three), equal in the watcher report, the run log and the printed line. 43 job windows in the report and in watcher.jobWindows of the log, from 03:23:17.1 at the first log step to 03:25:38.6, 0.5 s before the watcher wrote Revit exited at 03:25:39.1. 4 of them were logged during the image step and 2 after it, and 4 during the open between samples that both showed Revit not responding (06:23:22 to 06:23:47 local). The watcher CPU was 0.875 to 0.953 s over those samples (corrected in review pass 3) and rose from 1.078 to 1.156 s over the image step. Revit did not respond only during the open, it responded on every sample of the image step (corrected in review pass 3). So in I28 the main loop kept listing while Revit was busy and while it did not respond, and ended 0.5 s after the last window. No stoppedOn, nothing clicked after the first step (RAN).
- A repeat by the same Win32 calls, not a second method (corrected in review pass 3): my own list of every window of that Revit at 03:24:43 UTC, 10 s into the image step, read only (RAN, round_17\logs\windows_I28_win32.txt, script round_17\logs\list_windows_I28.ps1): 29 windows, 29 matched in jobWindows by class, visible and title among the entries up to that time, 0 unmatched.
- The work copy and round_17\work deleted after the run (RAN). Not done: the whole area was not run again, as the lead set.

### Review pass 3: the watcher of main again, the deep clone build, data5_check

- I29, the south west quarter of NBH 03 from a work copy of kept\round_17\01101-PSN-IMB-R17-TEST-NEW.rvt, equal by SHA-256 before and after, under the lock with 6.95 GB free, the watcher built from the source of main (RAN, round_17\logs\run_I29_image.txt, testhost_I29_log.json, testhost_I29_watcher.json): open 78.0 s, image 56.3 s with 128 links and 232 floors in the view, close 3.3 s, window 2.90 min, log result ok, exit code 0. The watcher answered 5 security dialogs in the start phase from 03:54:20 to 03:54:26 UTC, Do Not Load for RCRC Green and RCRC Green Audit and Load Once for our three, and ended at 03:54:32 when the test host wrote its first log step, as on main, with 5 dialogs in securityDialogs of the log. Free memory was 0.55 GB 49 s into the image step, with Revit at 5,269 MB private memory, and a read a few seconds later gave Revit 6,700 MB private, both read once in this session by Get-CimInstance and Get-Process and not saved to a file. Run-TestHost.ps1 printed a peak of 1,907 MB. The work copy and round_17\work deleted after the run (RAN).
- A clean build: a fresh worktree of main and of 83583e3 in the scratchpad failed with MSB3030 on SiteAdaptation.DialogWatcher.exe.withSupportedRuntime.config and SiteAdaptation.C3DWatcher.exe.withSupportedRuntime.config under the local SDK 10.0.400, so main had it too. The file was on disk, its path was 271 characters, over the 260 the copy step of MSBuild checks, and a fresh worktree of main at a short path built with 0 errors (RAN). The SDK 10.0.400 and 8.0.426 targets that write the file are equal (RAN, diff). R304: dotnet/Directory.Build.targets names it runtime.config. A fresh worktree of the branch in the scratchpad then built with 0 errors, both exe.config files equal byte for byte to the short path build of main (RAN). CI builds with SDK 8 at a short path and passed before and after.
- data5_check.py after the pass 3 fixes (RAN, round_addin\logs\data5_check_pass3.json): exit 0 in 4.4 s, the same counts as in pass 1: area 20 of 20 at seed 14 and 19 plus 1 noDlusRow at seed 7, parcels and curb 20 of 20 at both seeds, no DXF for 3 roles, the IFC skipped by Q047, file names IMC 221 equal and one more each for IMA, IMB, IMD, IME, IMF and IMG. It calls none of the code pass 3 changed, its names part reads im_codes.json of DATA-5 (READ, data5_check.py line 187).

### Gate

- Before the review: Python 1024 passed and 1 skipped, .NET build 0 errors and 1 warning (xUnit2013, older than this round), .NET 429 passed (RAN).
- After the review fixes: see the top entry of steps/log.md.
- Originals after all windows of the review fixes: 40 of 40 equal to the hashes before the round (RAN, originals_0b_after_review.txt).
- Review pass 2: Python 1074 passed and 1 skipped, .NET build 0 errors and the same 1 old warning, .NET 441 passed (RAN).
- Review pass 3: Python 1097 passed and 1 skipped, .NET build in place 0 errors and the same 1 old warning, .NET 433 passed, a fresh worktree of acdca67 in the scratchpad built with 0 errors and its tests passed (RAN). The 9 R303 tests are gone and 1 R304 test is new. R12 needs no test for a rule marked Withdrawn. Originals 40 of 40 equal before and after I29 (RAN, originals_0b_pass3_before.txt and originals_0b_pass3_after.txt). The local branch feat/revit-links-walls-ramps, tip 5fbf27a with the tree of 0a0d4c3 in main, is deleted (RAN). Originals 40 of 40 equal before and after I28, and the before list equal to the hashes before the round (RAN, originals_0b_pass2_before.txt and originals_0b_pass2_after.txt).

### New questions

- Q062: the Rev 9 attribute values of the probe.
- Q063: the whole NBH 03 image runs past 30 minutes while the image step of each quarter takes under a minute (corrected in review pass 2).
- Q064: the watcher through the job, R303, withdrawn after two attempts (review pass 3).

## Phase 1, PR data/addin-parsons-models

Branch data/addin-parsons-models from main at cd26ab3, equal to origin/main after git fetch (RAN). Rules R310 and R311 are new, R203, R204 and R110 changed. The phase ran in a helper agent started by the main session, which runs the checkers and merges. Values of coordinates and levels stay in Documents\SiteAdaptation\round_addin\logs and in the testhost_K*_prototypes.json reads, this file holds counts, distances and differences only.

### Originals (R201, R202)

- The 40 files of Phase 0b hashed with sha256sum before the first window: 40 of 40 equal to originals_0b_pass3_after.txt (RAN, round_addin\logs\originals_p1_before.txt). After the last window 40 of 40 equal to the list before, and Get-FileHash gives the same 40 of 40 (RAN, originals_p1_after.txt).
- The IMD file of Revit All Zones and 00_Revit Models\Zone 02\ARC\01101-PSN-IMD-ZZZ-PIM-TU-000001.rvt are one file: the same SHA-256 (93607568...) by sha256sum and by Get-FileHash, the same size 60,846,080 (RAN).
- No window opened an original. Each source was copied into round_addin\work by a file copy and only the copy was opened. That each copy equalled its source by Get-FileHash before its window was checked by a script of the session whose output was not kept, so it is UNKNOWN (corrected in review). What is kept and backs part of it: the line Unchanged True of each run_K*_upgrade.txt, 7 of 7, which says the work copy had the same hash, time and size after its window as before it, the source size the sourceInfo step logged, equal to the size of its original 7 of 7, and sources.txt, whose 7 source hashes equal originals_p1_before.txt 7 of 7 (RAN, read again in the review). The work copies are deleted, so they cannot be hashed again. The link openings the test host logged point into the work folder, so the 2021 prototypes of Zone 02\ARC\Prototypes were not opened either (READ, testhost_KC_upgrade_log.json).

### BasicFileInfo from the bytes (R203)

- The SE models gave format 2021Build: 20200715_1022(x64)Last Save Path: ... as one value and no build and no last save path. Revit writes Username, Build and Last Save Path of a BIM 360 cloud model right after the value before them on one line (READ, the BasicFileInfo text of the IMA model). R203 now ends a value at the next key on its line. On the 96 .rvt files under C:\02-Site Adaptation the old and the new parse give the same keys for 90 and differ for 6, the 6 BIM 360 models (the two SE models of Revit All Zones and the PP and SE models of IMC and IMD in Zone 02), each now with its format, build, last save path and Worksharing Central (RAN). 2 tests.
- Second method: the sourceInfo step of each site upgrade reads BasicFileInfo in Revit. Format and workshared equal the bytes on 7 of 7 (RAN).

| Model | Bytes | Format | Build | Workshared | Central model |
|---|---|---|---|---|---|
| IMA SE zone1 | 58,335,232 | 2021 | 20200715_1022 | yes | BIM 360://ROSHN MAKKAH VO#2 |
| IMB SE zone1 | 52,125,696 | 2021 | 20200715_1022 | yes | BIM 360://ROSHN MAKKAH VO#2 |
| IMC TU zone2 of Revit All Zones | 48,742,400 | 2024 | 20250918_1515 | no | none, last save ...\Zone 02\ARC\..._TEST-MASTER.rvt |
| IMC TU of Zone 02\ARC, the key | 52,494,336 | 2024 | 20250918_1515 | yes | C:\02-Site Adaptation\00_Revit Models\Zone 02\ARC |
| IMD TU zone2, equal to Zone 02\ARC | 60,846,080 | 2021 | 20230907_1515 | yes | \\mainsrv\...\REV 8.2\WORKSPACE\6. Zone 2 |
| IME TU zone3 | 54,366,208 | 2021 | 20230907_1515 | yes | \\mainsrv\...\WORKSPACE\7. Zone 3 |
| IMF TU zone3 | 73,572,352 | 2021 | 20230907_1515 | yes | \\mainsrv\...\WORKSPACE\7. Zone 3 |
| IMG TU zone4 | 44,122,112 | 2021 | 20230907_1515 | yes | \\mainsrv\...\WORKSPACE\8. Zone 4 |

(RAN, round_addin\logs\rvt_info_p1_fixed.json.) The storage times give the last save of the SE and TU models on 2024-03-26, of the Zone 02\ARC IMC on 2026-10-22 and of the zone2 IMC on 2026-10-24 (RAN, R266).

### IMC: the zone2 file is the master of the floor scenarios

- BasicFileInfo of the zone2 file names ...\Zone 02\ARC\01101-PSN-IMC-ZZZ-PIM-TU-000001_TEST-MASTER.rvt as its last save path, not workshared (RAN). No hash of the master was ever written (READ, the preflight), so it is compared with the original by a read of each, KM of a test copy of the zone2 file and KC of the 2024 copy of the original (RAN):

| | zone2 file (KM) | Zone 02\ARC original (KC) |
|---|---|---|
| Link types, link instances | 14, 338 | 14, 338 |
| Links in the TransmissionData | 14 Unloaded | 4 Loaded, 1 Not Found, 9 Unloaded |
| Floors | 0 | 1,010 |
| Walls, placed family instances | 926, 2,311 | 926, 2,311 |
| Spot elevations | 1 | 987 |

- testhost_MST_log.json of the master job lists 14 links set to unloaded, 1,010 floors deleted (Flat Ramps 10, Generic 150mm 336, Ramps 0.5% 280, Ramps 2% 47, yard 337) and its model ...\_TEST-MASTER.rvt (READ). So the zone2 file is that master moved and renamed, and the IMC key is the original of Zone 02\ARC. Q065 asks Bader. The zone2 file stays as it is (CHOSEN, the brief).

### The Revit 2024 copies (R204)

- Revit All Zones\2024 did not exist, so no copy could be reused (READ). Each copy was made by the siteUpgrade job: a file copy of the source into round_addin\work, opened detached with its worksets discarded, saved as <name>_TEST-<code>.rvt next to it, read, then moved to Revit All Zones\2024 under the name of its model in Revit All Zones, never over a file (RAN, by a script in the scratchpad of the session, not kept). The IMC copy is of the Zone 02\ARC original, already Format 2024, so its window only opened it detached and saved it (RAN, open 9.5 s), and it takes the zone2 name, so the folder holds one file per name of Revit All Zones (CHOSEN). R204 now lets the round spec name the source in the work folder, so the 7 sources needed no change of siteBuild.answerKey per window. 1 test.
- Revit All Zones\2024\sources.txt holds per copy its SHA-256, the source path and the source SHA-256, so a later round reuses a copy whose source hash still matches (RAN). sha256sum of the 7 copies equals sources.txt on 7 of 7 (RAN).
- Format 2024 by the bytes 7 of 7 and by the targetInfo step in Revit 7 of 7, none workshared (RAN, rvt_info_p1_2024.json). Sizes: IMA 53,272,576, IMB 47,509,504, IMC 52,387,840, IMD 55,836,672, IME 51,302,400, IMF 71,122,944, IMG 40,722,432 bytes (RAN).
- The SE upgrades showed one task dialog each, TaskDialog_Views_Related_To_Analytical_Changed, closed by the test host, and logged 1,171 (IMA) and 837 (IMB) warnings that analytical elements became invalid in the upgrade, deleted as warnings by R104 (RAN, testhost_KA_upgrade_log.json, testhost_KB_upgrade_log.json). The TU upgrades had no dialog and no failure line (RAN).

### Links and prototypes

- TU models (IMC, IMD, IME, IMF, IMG and the zone2 file): 14 Revit links each in the TransmissionData bytes and in the read, the file names equal on 6 of 6 (RAN). 13 are the KEO ARC prototypes, all 13 in 00_Revit Models\Zone 02\ARC\Prototypes_2024 (RAN). The 14th is the landscape model 01101-PSN-ZZZ-XXX-LAN-ZZ-001000.rvt, not on this machine (SEARCHED C:\02-Site Adaptation, Documents and Downloads by name). It is no prototype and the add-in does not need it (CHOSEN). The TU link paths are P:\22-05 ROSHN Makkah\REV 8.2\WORKSPACE\4. Villa Prototypes\20240209\ for IMD to IMG and relative Prototypes\ for the IMC original (READ). No link loaded in any window (RAN, loaded 0 in every read).
- SE models: the bytes list 1 Revit link, the TU model of the zone (01101-PSN-IMA-ZZZ-PIM-TU-000001.rvt, 01101-PSN-IMB-ZZZ-PIM-TU-000001.rvt from a user's Downloads), while Revit reads 14 link types, that TU model and 13 KEO STR prototypes RRE-WP-NSP-KEO-STR-<code>-ZZ-MOD-<n>.rvt linked from Autodesk Docs://ENGICON/ROSHN MAKKAH VO#2/Project Files/WIP/Prototypes/STRUC (RAN, the link openings and the read). The cloud links are not in the TransmissionData XML, so R203 does not see them (known bug). No Rev 6 Parsons TU model of Zone 1 is on this machine (SEARCHED by name). Native Files holds 1.1\01101-PSN-IMA-ZZZ-PIM-TU-000001.rvt and 1.2\01101-PSN-IMB-ZZZ-PIM-TU-000002.rvt, the TU models of the 2022 Native job (READ, originals_p1_list.txt) (corrected in review). The 1.1 file has the name the IMA SE model links but is another model: central path \\MAINSRV\...\Site Adaptation\Phase 1\..., last saved 2022-12-11 (RAN, rvt_info_p1_protos.json). Bader's rule takes a Native model only for plots none of the 7 Parsons models hold, and never for unit codes, while IMA and IMB hold the Zone 1 plots (432 and 301 links) with no level, ramp or yard. So taking the levels, ramps and yards the SE models lack from the Native 1.1 and 1.2 reads is CHOSEN by the lead, not his rule (corrected in review pass 2), and Q066 asks Bader for it with the Parsons TU models as the recommended answer.
- The 13 STR prototypes are not in Prototypes_2024, which holds the 13 ARC files. Their 2021 files are on this machine in 00_Revit Models\Zone 02\STR\Prototypes, 13 of 13 (SEARCHED). Not upgraded: the add-in places the ARC prototypes, a link is read with no loaded file, and the upgrade job takes its source only from pastJob.prototypeFolder, the ARC folder, so it would be a code change (CHOSEN). Q067 asks Bader.

### What the SE models of IMA and IMB hold against the TU models

| | IMA SE | IMB SE | IMC | IMD | IME | IMF | IMG |
|---|---|---|---|---|---|---|---|
| Link instances | 433 | 302 | 338 | 411 | 325 | 491 | 298 |
| Links with a plot number | 432 | 301 | 337 | 410 | 324 | 490 | 297 |
| RRE and CASE parameters on them | 5 | 5 | 10 | 10 | 10 | 10 | 10 |
| Link Names with bars | 0 | 0 | 338 | 410 | 324 | 490 | 297 |
| Generic floors (type) | 733 (Generic 150mm) | 733 (Generic 150mm) | 336 (Generic 150mm) | 410 (Generic 150mm) | 324 (FFL) | 490 (FFL) | 297 (Generic 150mm) |
| Yard floors | 0 | 0 | 337 | 410 | 324 | 490 | 0 |
| Ramp floors 0.5%, 2%, flat | 0 | 0 | 280, 47, 10 | 350, 55, 5 | 248, 76, 0 | 409, 75, 6 | 229, 68, 0 |
| Structural slab and footing floors | 28 | 40 | 0 | 0 | 0 | 0 | 0 |
| Structural Foundations | 1,199 | 877 | 0 | 0 | 0 | 0 | 0 |
| Walls | 2,323 | 1,515 | 926 | 1,138 | 894 | 1,317 | 859 |
| Spot elevations | 822 | 587 | 987 | 1,256 | 938 | 1,415 | 866 |

(RAN, testhost_K*_prototypes.json.) In IMC the landscape link has a Name with bars too, so 338 of 338 links, in IMD to IMG it has none (RAN, rawName of each link, corrected in review). The 5 parameters of the SE links are RRE_ZZ_SP_Plot-Number, RRE_ZZ_SP_Prototype, RRE_ZZ_SP_Prototype-Foundation-Type, RRE_ZZ_SP_RVT-Name and CASE, so no FFL, steps, facade or HVAC (RAN). Structural Foundations is the one model category the SE models hold and the TU models do not, and the TU models hold none the SE models lack, imported drawings left out (RAN). The two SE models hold the same 733 Generic floors, corners equal to the cm on 733 of 733, each with a plot number, 432 of them the link plots of IMA and 301 of IMB, no plot shared (RAN). So a Zone 1 SE model gives link position, hand, plot number, prototype and CASE but no level, ramp or yard. Q066 asks for the Rev 6 Parsons TU models of Zone 1 (recommended). Until then the lead chose the Native 1.1 and 1.2 reads for the levels, ramps and yards of Zone 1 (CHOSEN, not the rule of Bader, corrected in review pass 2).
- Second method: each count of the reader against the category count of R311 in the same read, links against RVT Links, walls against Walls, floors against Floors with the 28 and 40 structural slab floors, which Revit files under Structural Foundations, matched by type name, spots against Spot Elevations: 32 of 32 equal over the 8 reads (RAN).
- Second source: 20 random plot links of the IMC 2024 copy at seed 14 and 20 at seed 7 against prototypes_original_imc.json, the read of the same original in Round 15a: file, shared origin within 1 mm, rotation, mirror and Name equal on 20 of 20 at both seeds, worst origin gap 0 mm, the same 337 plots on both sides (RAN).

### Coordinates of each model (R311)

- R311 adds to the reader the active shared site and every site, the place name, the project position of the internal origin, the levels with both API elevations and their base, the units and the elements per category. The Revit side only reads, SiteFacts.Build in Common converts and sorts. 6 tests. Proved first on the IMG copy, then on all 8 reads (RAN).
- All 7 models and the zone2 file hold the same survey point and project base point, in shared and in internal coordinates, the same project position, angle to true north 0, one shared site named Internal, place Boston, MA (the Revit default), the levels Level 1 and Level 2 with Elevation Base Survey Point, Level 2 4.0 m over Level 1, each level equal to the mm across the 8, units metric in meters, accuracy 0.01 on the TU models and 0.001 on the SE models (RAN, round_addin\logs\p1_compare.json, every gap 0.0 mm against the IMC key). So all zones share one survey point and one angle.
- Second method: the project position read by GetProjectPosition, put through the angle, gives the shared point of the survey point and of the base point from their internal points within 0.0 mm on 8 of 8 reads (RAN). The survey point of the IMC 2024 copy equals the one of plan_imc_past.json and of prototypes_original_imc.json, 0.0 mm in shared and in internal coordinates (RAN).
- Against config and the data book: the shared coordinates of the Parsons models are those of CAD, the frame of the data book, since the Round 17 placement from CAD put 285 of 336 IMC links within 1 mm of the key and every Z within 5 mm (READ, round-17.md). The new site files of R211 use angle 0 and the same elevation of the internal origin, but put the internal origin at the plot centre of their NBH rounded to 100 m: 547.5 m from the Parsons origin for the four site specs of Rounds 16 and 17 and 173.1 m for the Z4 spec (RAN, the site specs of kept\round_16, kept\round_17 and kept\round_z4). Button 1 must reproduce one project position, one survey point and base point, angle 0, the site Internal, Level 1 and Level 2 and metric units in meters for every zone. Q068 asks whether button 1 sets the Parsons project position in place of R211.

### Memory and time (R310)

- Units (corrected in review): the script, the outside samplers and window_lock.py divide byte counts by 1MB and 1GB of PowerShell or by 1024 to the third, so every memory value of this round was MiB or GiB, while the text wrote MB and GB and divided MiB by 1000 for GB (READ, WindowSamples.ps1 before the review, round_17\logs\sampler_I28.ps1, window_lock.free_gb). This section names them MiB and GiB. The Phase 0b sections above keep their old labels: their MB are MiB, their GB of free memory are GiB, and the 6.4 GB of a quarter and 10.7 GB of the whole area are 6,377 MiB (6.23 GiB) and 10,732 MiB (10.48 GiB).
- The cause of the frozen peak of Phase 0b: PowerShell 7.6.7 binds [Math]::Max to Max(Int32, Int32) when its first argument is an Int32, and Run-TestHost.ps1 started with $peak = 0. Past 2 GiB the call threw, the empty catch around it hid the throw, and the peak froze at the last value under 2 GiB (RAN, a child process of 2.6 GiB working set: Get-Process gave 2,654 MiB, the script variable stayed an Int32 and every call threw). So the old print could not pass the Int32 limit of 2,048 MiB. Of the 126 peakMb values of the earlier runs in Documents\SiteAdaptation and testhost_old, 101 are under 1,493 MiB, the lowest 697, and the 25 of the large windows sat at 1,493 to 1,995 MiB, under the limit, while an outside sampler saw 5,663 MiB working set in I28 (RAN, the run.json files, corrected in review).
- R310: Run-TestHost.ps1 samples its Revit every testHost.sampleSeconds, 5, private memory, working set, the peaks Windows keeps, responding and free memory, writes each sample at once to testhost_<name>_samples.jsonl, keeps a failed sample with its reason, and prints and writes the peaks into run.json. A first run showed that the samples miss the close of Revit (on three reads the outside sampler saw 35 to 65 MiB more private memory than the peak of the script to its last sample), so the peaks Windows kept to the exit are read after it through the process handle with K32GetProcessMemoryInfo. Proved on a read of IMF (KF2) and of IMG (KG2): peak private to the exit 1,606 and 1,095 MiB against 1,571 and 895 MiB sampled and 1,302 and 892 MiB by the outside sampler (RAN). KF2 ended at 07:47:59 local, before 9ca9a38 (07:51:54, the counters read as words) and f89cc3a (08:06:47, the outputs of a site upgrade moved aside) changed the scripts, so KF2 proves the scripts of dfd5e10. KG2 ended at 07:51:48, 6 s before 9ca9a38 was committed, and the edit of that commit may predate the start of KG2 at 07:51:19, so KG2 proves no one version of the scripts (READ, git log and windows.jsonl, corrected in review pass 2). Only KF2 and the later KG3 of the review, on the final scripts of pass 1, prove them, see below.
- The run moved the older outputs of a name <code>_upgrade aside only under -Upgrade, so the samples file of a site upgrade would have grown across two runs of one code. Now every run named <code>_upgrade moves its own, 1 test (RAN). Each site upgrade code of this phase ran once, so no samples file holds two runs (RAN, windows.jsonl).
- Second method: the sampler of I28 (round_17\logs\sampler_I28.ps1, Get-Process by name every 5 s) ran beside every window. Its largest private memory is at most the peak of the script in 14 of 17 windows, the 3 others are reads of the first runs, before the exit peaks, where the outside sampler caught the close (RAN).
- 17 windows, one at a time under the lock with round ADDIN-1, 0 waits, 6.26 to 7.34 GiB free at the lock, exit code 0 and no Revit left after each, 0.48 to 1.45 min each, 12.87 min in all (RAN, round_addin\logs\windows.jsonl). The watcher answered 5 security dialogs in each, in our own Revit only: Do Not Load for RCRC Green and RCRC Green Audit, Load Once for our three, 85 in all (RAN). The registry was not read or written. Install-TestHost.ps1 -SkipSigningReport under PowerShell 7.6.7 installed the build of 36b5125 with one Common DLL in the reader, the placer and the watcher (RAN, round_addin\logs\install_p1.txt). The later commits of the phase changed only the run script and the tests, nothing installed.
- Peak private memory of the 17 windows: 871 to 2,275 MiB (0.85 to 2.22 GiB), least free 3.82 GiB (RAN, the memory block of each run.json, corrected in review: 0.87 to 2.28 GB before was MiB divided by 1000). Only KF2 and KG2 have the peak through the exit (throughExit true), the other 15 the peak up to their last sample, since they ran before the exit read: KG2 shows 200 MiB between the two, 1,095 to the exit against 895 sampled (RAN).

| Model | Open s (upgrade) | Save s | Peak private MiB of the upgrade window, script and outside | Read: open s, read s | Peak private MiB of the read, script and outside | Least free GiB |
|---|---|---|---|---|---|---|
| IMA SE | 26.4 | 6.1 | 1,681 and 1,628 | 2.3, 5.2 | 883 and 948 | 4.55 |
| IMB SE | 24.1 | 5.3 | 1,569 and 1,497 | 2.1, 4.2 | 872 and 907 | 4.69 |
| IMC | 9.5 | 3.9 | 1,370 and 1,072 | 1.7, 5.8 | 1,194 and 882 | 4.77 |
| IMD | 37.7 | 6.2 | 1,798 and 1,681 | 2.2, 8.6 | 1,309 and 1,078 | 4.01 |
| IME | 34.1 | 6.7 | 1,679 and 1,556 | 1.9, 5.4 | 896 and 944 | 4.11 |
| IMF | 52.9 | 9.3 | 2,275 and 2,177 | 4.2, 9.6 | 1,592 and 1,591 | 3.82 |
| IMG | 30.0 | 4.5 | 1,461 and 1,404 | 1.9, 4.0 | 871 and 869 | 4.57 |
| zone2 IMC | none | none | none | 1.6, 3.0 | 1,086 and 869 | 5.34 |

- The open of an upgrade includes the upgrade from 2021, the IMC original is already 2024 and opened in 9.5 s (RAN). The peak working set of the largest window, the IMF upgrade, was 2,470 MiB (RAN). For scale, an image of a quarter of NBH 03 took 6,377 MiB (6.23 GiB) private and the whole area 10,732 MiB (10.48 GiB) in Phase 0b (READ, this file).
- Not responding (added in review): Revit did not respond on 7 of the 121 samples of the 17 windows, KF upgrade 2, KF2 2, KF read 1, KD read 1 and KG upgrade 1, and those 5 windows ended ok with exit code 0 (RAN, testhost_K*_samples.jsonl). Private memory at those samples: 908 to 1,720 MiB (0.89 to 1.68 GiB) on the 6 taken during the read step or the close right after it (KD, KF, KF2) and the open of the upgrade (KF upgrade), and 34 MiB on the KG upgrade sample, 11 s after Revit started and before its job (RAN, each sample against the steps of its log). Working set at those samples 90 to 1,889 MiB.
- What that means for Q063 and Q064: not responding comes with a busy main thread in ordinary windows at under 1.7 GiB private and goes away, so on its own it marks neither a hang nor memory pressure. I25 differs in kind, not responding on 39 of 39 samples over 19 minutes at 10,732 MiB (10.48 GiB) private with paging. A stop on not responding would have stopped 5 windows that ended ok here, which backs Q064 (the watcher stops at the first log step, the step limits stop a job), and Q063 keeps memory as the suspect of the whole area image on the I25 measures, not on the flag.

### Limits, each with its reason

- The free memory floor at the lock stays siteBuild.minFreeGB 2.0, in GiB as window_lock.free_gb reads it (CHOSEN, the round sets it). The largest window of this phase took 2,275 MiB (2.22 GiB) private and free memory fell from 6.26 GiB at its lock to 3.82 GiB, so every window here started with room for itself (RAN).
- The floor is watched during the job, not only at the lock, by R310: every sample counts free memory against the floor and the run prints the samples under it. It does not stop Revit (CHOSEN): the only windows that went under the floor, I28 at 1.10 GiB and I29 at 0.55 GiB, ended ok, the one hang, I25, stayed at 2.16 to 2.77 GiB free so a stop at the floor would not have caught it, its image step limit did, and the lock keeps a second window from starting meanwhile (READ, Phase 0b above). No window of this phase went under the floor, 0 of 121 samples (RAN). So no stop rule and no further change.
- The step limits of the test host stay as they are: open 25 min (siteUpgrade) and 20 min (read), save 10 min, read 15 min, job 27 min, window 30 min (CHOSEN). The largest measures are 52.9 s, 9.3 s, 4.2 s, 9.6 s and 1.45 min, so each limit is over 15 times its measure, and a limit nearer the measure would stop a slower machine and gain nothing (RAN).
- The sample interval 5 s (CHOSEN, as the I28 sampler): a read window of 30 s gets 4 to 7 samples, the exit peaks cover the rest. The checks of the wait loop keep their own 5 s since the review, so a change of sampleSeconds moves no check.

### Fixes after the breaker and the claim-checker (new commits on PR 43)

- R311 (item 1): a part that did not read is written as null with its error in site.notRead by part name, and each part sets its values only once all of them read, so no levels is [] and levels not read is null with notRead.levels. The test that held a part not read as an empty list now holds the opposite. 1 test.
- R311 (item 2): the angle to true north is rounded to 6 decimals first and then brought to 0 up to under 360, so -1e-16 rad and anything within the rounding of a full turn give 0, never 360 or negative zero. It is written at 6 decimals by its own converter, not at the 3 of R2: 1 mm across 5 km is 0.0000115 degrees and 0.001 degree turns a point 5 km away by 87 mm. lengthAccuracy is written as Revit gives it, 0.0001 stays 0.0001. R2 and R311 say so. 2 tests, one a theory of 8 angles.
- R311 (item 3): units.system stays Document.DisplayUnitSystem, the system the project was made in, and units.lengthSystem beside it gives METRIC or IMPERIAL by the length unit of the project units now, GetUnits and the FormatOptions of SpecTypeId.Length, among the ten length units of Revit 2024, five metric and five imperial (the names READ from UnitTypeId in RevitAPI.xml, which of them serve length CHOSEN by their names). In the test a project made metric and set to feet later reads METRIC and IMPERIAL. 1 theory of 12 cases.
- R310 (item 4): every memory field and printed line is in MiB and GiB and named so, peakMb of run.json keeps its name for Run-Parallel.ps1 and the earlier runs. The exit peaks give the peak also with no sample. Free memory and the child processes come from one CIM call each, a failed call leaves its own fields null with its reason and the memory of Revit stays, and the peaks count the samples with no free memory apart. The processes Revit started, and the ones they started, are counted in each sample with their names, private memory and working set, see KG3 below. 3 tests.
- R310 (item 5): the wait loop runs two clocks, the deadline, watcher, refusal, no log and start screen checks every 5 s as before R310, and a sample every sampleSeconds. A test runs two minutes of the loop on a clock that moves by the wait it is given: 24 checks at sample intervals of 60, 5 and 1 s, with 2, 24 and 120 samples. 1 test, and the script test holds the checks behind the check clock.
- R204 (item 6): the run script writes workFolder, siteBuild.folder\work, into a site upgrade job and the guard refuses a job with none, one that is not a plain full path to a folder named work, and a source beside it, in a folder inside it, in another folder named work or in Revit All Zones. workFolder on another job is refused. 2 tests on refused paths, one a theory of 4.
- R301 (item 7): dataBook.data5.imCodes.copyFolders of python/c3d_probe.json names Revit All Zones/2024 with its reason, and im_codes.py and data5_check.py count the files there apart as copyFileNames. On the real source root, read only (RAN): the source names give IMA 5, IMB 5, IMC 221, IMD 199, IME 1, IMF 1 and IMG 1, the counts of review pass 3 of Phase 0b, and the copies 1 each, 7 in all. The old walk gave one more each. data5_check.check_names, the second method by the parts of each path, gives the same 7 sources and 7 copies against im_codes.json of DATA-5, which holds no copies yet. 4 tests.
- Tests first (READ, from the run of that session, whose output was not kept, corrected in review pass 2): against the old code the new R311 tests did not build (no AngleDecimals, AngleDegrees, NotRead, LengthSystem), the R310, R204 and R110 tests failed 14 of 58 with the R311 code put back, and the R301 tests failed 8 of 8. The clock test looped on the old functions, since an unknown command does not stop pwsh, so the test helper now stops on any error and the loop has a cap. Then 58 of 58 and the R301 tests passed.
- The records (items 8 to 16): the path Revit All Zones\2024 in PROGRESS lost its backslash to a control character U+0082 on lines 1 and 10, fixed, and no other steps file holds a control character (RAN, every file under steps). The other corrections are marked in this section.

### Fixes after the second breaker and claim-checker pass (new commits on PR 43)

- R310 (item 1): each of the two CIM calls of a sample has -OperationTimeoutSec of testHost.cimTimeoutSeconds, 2 (CHOSEN: the two calls took 42 to 109 ms here, RAN, and two at the limit fit in the 5 s of the checks). A timed out call is written as not read, its fields null with the reason, and the sample is still written. The wait loop looks at the deadline right after the wait, before its clocks and any sample. Whether the limit holds on a local CIM call: a Win32_Directory query that runs past 10 s ended at 2.02 s with a limit of 2 and at 10.02 s with a limit of 10, Timed out (RAN). 2 tests, one with a CIM call that runs to its limit, one on the order of the loop, and the 4 older sample tests pass the limit.
- R310 (item 2): the rule states the scope. The child figures count the processes this window started, a helper an earlier window started is not counted, free memory covers the whole machine and is what the floor reads. In Phase 1 the LocalDB of error1.log started at 07:29:57 and ended at 07:56:37, and the 17 windows ran from 07:29:36 to 07:51:49 (READ, error1.log and windows.jsonl), so it started in the first window and ran on through the 16 after it. memory.helpersAtStart of run.json lists the processes of testHost.helperProcesses (RevitWorker, sqlservr, CefSharp.BrowserSubprocess, AdskLicensingAgent, the names of KG3 but conhost) already running at the window start, by name, id, start time and private MiB from Get-Process, and run.json is written at depth 5 so the list is kept in full. 1 test with a helper of 100 MiB that runs before the window. Second method: the list of Get-Process against Win32_Process for pwsh, svchost and explorer, 97 of 97 ids with private memory equal to the MiB, and the free memory of a sample within 22 MiB of the Available Bytes counter (RAN).
- R301 (item 3): copyFolders names the manifest of each folder, sources.txt. A file right in the copy folder counts apart only when the manifest names it as a copy with its source, any other file there or in a folder inside it stays in fileNames and is flagged as notInManifest. Folders, manifests and copy names match in any case. A blank around a part, a manifest that is no plain file name, nested folders, no manifest on disk, a manifest line with no copy and source under the root, a source inside the folder (listed too high) and a folder not under the root are refused, in im_codes.py and in data5_check.py, which reads the manifest its own way. On the real source root, read only: sources IMA 5, IMB 5, IMC 221, IMD 199, IME 1, IMF 1, IMG 1, copies 1 each, 0 flagged, the second method equal on 7 of 7 sources and 7 of 7 copies (RAN, round_addin\logs\review2_r301_real.txt). 10 tests new or changed.
- R311 (item 4): SitePart in Common names each part by its site field, so notRead holds projectPosition and never project position, and the reader guards each part by it. activeSite and placeName are written as null like sites when the sites part did not read. 1 test, and the older test of a part not read uses the new key.
- Tests first (RAN, outputs in round_addin\logs\review2_first_build.txt, review2_first_tests.txt and review2_first_r301.txt): the R311 test did not build with no SitePart (CS0103), then with SitePart alone 7 of 41 Parsons tests failed, the 4 sample tests on the missing -TimeoutSeconds, the helper test on the missing Get-RunningHelpers, the script test on no testHost.cimTimeoutSeconds and the R311 test on no activeSite. The R301 tests failed 17 of 68, most first at the new manifest field.
- Older than this PR, recorded as a known bug (item 5): RevitExtractor.cs line 133 writes the top level angleToTrueNorthDeg at the 3 decimals of R2 and not brought into 0 to 360, and plan_from_past.py line 665 and plan_from_cad.py line 294 copy it into plans at 3 decimals (READ). Placer.cs of Common, lines 155 to 161, refuses a plan angle more than 0.001 degree off the document and any angle but 0, and the 7 models are at 0, so nothing is placed wrong today. Phase 4 moves the coordinates to C# on the site facts of R311.
- The records (items 6 to 11): the top log entry is counted as test_r26 counts it, KF2 alone proves the scripts of dfd5e10, the tests first of pass 1 are READ, the LocalDB keeps only what its error.log backs and its parent is UNKNOWN, and Q066 says the Native stand-in for Zone 1 is the choice of the lead.

### KG3, the proving run of the review

- One read of the IMG 2024 copy through a work copy, the final WindowSamples.ps1 and Run-TestHost.ps1 and the build of the review installed by Install-TestHost.ps1 -NoBuild -SkipSigningReport, one Common DLL in the reader, the placer and the watcher (RAN, round_addin\logs\install_p1_review.txt). The copy 01101-PSN-IMG-ZZZ-PIM-TU-000001-zone4_TEST-KG3.rvt equalled its source by Get-FileHash and sha256sum before the window and after it, and was deleted after (RAN, copy_KG3.txt).
- Under the lock with round ADDIN-1, 6.57 GiB free at the lock, the watcher answered the 5 security dialogs in our own Revit only, Do Not Load for RCRC Green and RCRC Green Audit, Load Once for our three. Open 2.3 s, read 4.1 s with 298 links and 866 spots, close 0.3 s, log result ok, exit code 0, window 0.65 min (RAN, run_KG3_read.txt, windows.jsonl).
- Memory: 7 samples 5 s apart, peak private 1,102 MiB and peak working set 1,285 MiB to the exit, sampled 931 and 1,090 MiB, least free 4.51 GiB, 0 samples under the floor, 0 not responding, 0 failed (RAN, testhost_KG3_run.json). The outside sampler of the review (round_addin\logs\sampler_children_KG3.ps1, Get-Process by id every 5 s) saw Revit at most 865 MiB private, under the peak of the script (RAN, sampler_KG3_read.txt).
- Child processes: Revit started 10 in this window, RevitWorker (411 MiB private at most), the LocalDB sqlservr of the Revit Steel Connections add-in (547 MiB, instance SteelConnections2024v15, its parent UNKNOWN since the sampler finds descendants and never prints a parent), 5 CefSharp.BrowserSubprocess (23 to 69 MiB each), 2 AdskLicensingAgent (16 MiB each) and a conhost. Together up to 1,207 MiB private and 1,286 MiB working set in one sample, more than the 931 MiB of Revit, and Revit and them 2,120 MiB private at once (RAN, the samples). The outside sampler counted the same 10 by name and up to 1,201 MiB private and 1,276 MiB working set, within 6 MiB private and 10 MiB working set of the script at other moments (RAN). So they are included in every sample (CHOSEN): they have no exit peak, their peak is the largest sample. The sqlservr process outlived Revit: it ended at 09:01:51 local by its own time out, 4 min 57 s after the end of the window at 08:56:54.6 (READ, the error.log of the instance and endUtc of testhost_KG3_run.json). No log of its memory after the exit is kept, since the samplers stop with Revit (corrected in review pass 2). windows.jsonl gives 6.04 GiB free at the lock release against 6.57 GiB at the take (RAN). In Phase 1 one LocalDB instance started at 07:29:57 in the first window and ended at 07:56:37 by the same time out, so it ran through all 17 windows and was the child of the first one only (READ, error1.log of the instance). For Q021 and Q063: a window costs Revit plus about 1.2 GiB of these, and the LocalDB stays about 5 minutes after the last window, none of which the earlier peaks counted.
- Site facts: no part failed, so no notRead, levels Level 1 and Level 2, angleDeg 0.0, units system METRIC, length meters, lengthSystem METRIC, lengthAccuracy 0.01 (RAN, testhost_KG3_prototypes.json). Second method against the KG2 read of the same model before the review: activeSite, sites, placeName, projectPosition, levels and categories equal, units equal but for the new lengthSystem, links 298 equal (RAN).
- Item 5 in the window: both clocks have 5 s here, so the window shows the samples 5 s apart and every check working, the run ended by the log as before. The test above shows the clocks apart.
- Originals: the 40 files of Phase 0b and the 7 copies of Revit All Zones\2024, 47, hashed with sha256sum before the window and after it, 47 of 47 equal, and by Get-FileHash after, 47 of 47 equal (RAN, originals_p1r_before.txt, originals_p1r_after.txt, originals_p1r_after_filehash.txt). The 40 equal originals_p1_after.txt and the 7 equal sha256_2024_copies.txt (RAN).

### Not done in this PR

- The 13 STR prototypes are not upgraded (Q067).
- The cloud links of the SE models are not read from the bytes (known bug of R203).
- No Rev 6 Parsons TU model of Zone 1 is on this machine (Q066).
- No Revit window ran for review pass 2, so helpersAtStart, the CIM limit and the deadline first are proved by tests only. The angle of the top level stays as it is (item 5).
- Part 2 of Phase 1, in the next PR: the 2024 key copies made by code in git, the IMC copy name, the STR prototype upgrade and CLAUDE.md with R310 and R311.
- The failure path of a site part is proved by tests only, every part read in KG3.

### Gate

- Before the review: Python 1099 passed and 1 skipped, .NET build 0 errors and the 1 old warning (xUnit2013), .NET 445 passed (RAN).
- After the review fixes and after review pass 2: see the top entry of steps/log.md.

### New questions

- Q065: the IMC key is the Zone 02\ARC original, the zone2 file is the master of the floor scenarios.
- Q066: the Rev 6 Parsons TU models of Zone 1 (recommended), or the levels, ramps and yards of Zone 1 from the Native 1.1 and 1.2 reads, the stand-in the lead chose.
- Q067: the STR prototypes of the SE models.
- Q068: the project position button 1 sets.

## Phase 1 part 2, PR 44 data/addin-key-copies

Branch data/addin-key-copies from main at d3b3ed9 after PR 43, equal to origin/main after git fetch (RAN). Rules R312 and R313 are new, R301 and R109 changed. The phase ran in a helper agent started by the main session, which runs the checkers and merges. The hooks were live: guard-registry.sh refused a harmless echo with its trigger word (RAN, 1 refusal). Q067 closes by Bader's instruction, relayed in the brief of this PR.

### Originals (R201, R202)

- 60 files hashed with sha256sum before the first edit: the 40 originals of Phase 0b, the 7 copies of Revit All Zones\2024 and the 13 Revit 2021 files of Zone 02\STR\Prototypes (RAN, round_addin\logs\originals_p1b_list.txt and originals_p1b_before.txt). The first 47 equal originals_p1r_after.txt of Phase 1, 47 of 47 (RAN).
- After the last window: 60 of 60 equal by sha256sum, and by Get-FileHash of PowerShell against the list before, same 60, differ 0 (RAN, originals_p1b_after.txt and originals_p1b_after_filehash_summary.txt). So no window and no step wrote to a 2021 STR file, an original or a 2024 key copy.

### R312, the key copies by code

- answerKeys of config/project.json lists the 7 keys: code, zone, kind TU or SE, the source under inputRoot and the copy name, the name of each model in Revit All Zones. IMC is made from 00_Revit Models\Zone 02\ARC\01101-PSN-IMC-ZZZ-PIM-TU-000001.rvt and keeps the zone2 name, the other 6 from their model in Revit All Zones (CHOSEN, the brief and Q065). The block also names the copy folder, the manifest key_copies.json, the format 2024 and the job prefix K.
- python/key_copies.py check reuses a copy only when the manifest names it with the code and the source of the config, the SHA-256 of that source today, the SHA-256 of the copy today and the format 2024 in the entry and in the bytes (R203). Else make, noSource or remake with its reasons, each with the request for a siteUpgrade job: the source to copy into the work folder, the spec with the job code (KA to KG) and the name of the copy it makes. A manifest entry no key names is listed as noKey. check writes nothing. record moved a copy a siteUpgrade job made into the copy folder under its copy name only when no file of that name was there, after the work copy of the source equalled the source by hash, and wrote its entry. record is withdrawn in the review after two attempts (Q069, see Review fixes). import writes the manifest once from sources.txt of Phase 1. A source that can not be written passes every step.
- Tests first (RAN): the new test file stopped at collection with ModuleNotFoundError for key_copies, then 32 passed and the 2 tests of the real config failed with KeyError answerKeys until the config held it, and the copy folder test until python/c3d_probe.json named key_copies.json. Two later fixes, each with its test seen failing first: a copy that is no compound file raised CfbError and stopped the check, now it has no format and is remade, and an entry of another code was reused, now it is otherCode. 36 R312 tests at the end, on dummy compound files built in code (RAN).
- On the 7 real copies, read only but the manifest (RAN, round_addin\logs\key_copies_check_before_import.txt, key_copies_import.txt, key_copies_check.txt): before the import 0 of 7 reuse, 7 remake notInManifest, since no manifest was there. import wrote key_copies.json from the 7 lines of sources.txt with the hashes of today. Then check: reuse 7 of 7, exit 0. No log was kept of a check after the two fixes, so that claim is withdrawn. At the final head of PR 44, after the review fixes, check gives reuse 7 of 7, 0 notListed, exit 0 (RAN, round_addin\logs\p1b_final_key_copies_check.txt, with the head it ran on).
- How each is known, per copy: the source path of the manifest equals the source of the config (READ, both files), the SHA-256 of the source today equals the manifest and the SHA-256 of the copy today equals the manifest (RAN, key_copies.py by hashlib), the format 2024 from the bytes (RAN, rvt_info.py). Second method (RAN, round_addin\logs\key_copies_second.ps1 and key_copies_second.txt): Get-FileHash of PowerShell on the 7 copies and the 7 sources against the manifest read by ConvertFrom-Json, and the format and size of each copy from the targetInfo step Revit logged when it made it (testhost_K*_upgrade_log.json of Phase 1): 42 of 42 checks match, 0 mismatch.
- sources.txt (2,197 bytes) moved out of the copy folder to round_addin\logs\sources_p1.txt. No hash of it was logged before the move, so it is UNKNOWN whether the file moved unchanged. Its 7 lines equal the manifest and sha256_2024_copies.txt of Phase 1: per line the copy in the manifest, its hash, its source and the source hash in the manifest and its hash in the sha256sum list, 35 of 35 equal, 0 differ (RAN, round_addin\logs\p1b_review_sources_lines.py and .txt). No reader reads it now: im_codes.py and data5_check.py read key_copies.json, and only key_copies.py import reads a sources.txt, once, when no manifest is there.

### R301, the copy folder reads the JSON manifest

- dataBook.data5.imCodes.copyFolders names key_copies.json. im_codes.py reads it with key_copies.read_manifest, data5_check.py with json.loads and its own checks of each entry. A manifest that is no JSON list of copies, or an entry with no copy, source and hashes or with a source under no plain path, is refused in both.
- Tests first (RAN): with the tests switched to the JSON manifest and the readers not yet, 8 of 73 R301 tests of test_round_addin.py failed, at the old line parse and the old manifest name. Then all passed.
- On the real source root, read only (RAN, round_addin\logs\p1b_r301_real.py and p1b_r301_real.txt): sources IMA 5, IMB 5, IMC 221, IMD 199, IME 1, IMF 1, IMG 1, copies 1 each, 0 flagged, the second method equal on 7 of 7 sources and 7 of 7 copies. The same counts as review pass 2 of Phase 1 with sources.txt.

### R313, the STR prototypes (Q067)

- prototypeSets.STR of config/project.json: source 00_Revit Models\Zone 02\STR\Prototypes, target 00_Revit Models\Zone 02\ARC\Prototypes_2024\STR, discipline STR, codePrefix S (CHOSEN, the brief: a folder of its own so the ARC prototypes the placer picks by code stay alone). Run-TestHost.ps1 -Upgrade -PrototypeSet STR -Code <code> takes the one file of that code from the set folder with no plan, runs the job as S and the code and writes discipline and codePrefix into it. The steps of the upgrade job (R109) did not change. The 13 2021 files are workshared, Format 2021, each with 1 Revit link, unloaded (RAN, rvt_info.py, round_addin\logs\rvt_info_str_2021.json).
- First window, STH1 (RAN, round_addin\logs\run_STH1_upgrade.txt): the test host refused the job, since the guard of R103 knew only RRE-WP-NSP-KEO-ARC-<code>-ZZ-MOD-<n>.rvt and took STH1 as the code. The run script stopped Revit after 0.7 min, the source unchanged, nothing written but the empty folder Prototypes_2024\STR that -NewUpgradeFolder made. The claimed job file testhost_job_STH1_upgrade.claimed-24524-639285105181452908.json was moved from Documents\SiteAdaptation to round_addin\logs in the review, equal by sha256sum before and after (RAN, p1b_review_claimed_move.txt). Its refusal, testhost_job_STH1_upgrade.refused.json, stays in Documents\SiteAdaptation as R110 keeps it.
- The fix, the second attempt: TestHostJob carries discipline and codePrefix, the guard takes them on an upgrade job only, both or none, the discipline three capitals other than ARC, the job code the prefix and a prototype code, and the file RRE-WP-NSP-KEO-<discipline>-<that code>-ZZ-MOD-<n>.rvt. A job with neither keeps the ARC check. Tests first (RAN): the 13 set tests failed at first, at the missing PrototypeSets.ps1, the missing line of the run script and the missing config, and later the guard tests did not build with no Discipline (CS1061). 18 R313 test cases at the end (RAN).
- Install-TestHost.ps1 -NoBuild -SkipSigningReport under PowerShell 7 installed the build of the guard, one Common DLL in the reader, the placer and the watcher (RAN, round_addin\logs\install_p1b_guard.txt).
- Proof on one prototype first, STH1 again (RAN, run_STH1_upgrade_2.txt): open 184.3 s detached with worksets kept, save 6.4 s as a new central, close 0.4 s, targetInfo format 2024 workshared, 33,140,736 bytes, window 3.65 min, source unchanged by hash, size and time. Format 2024 by the bytes too (RAN, rvt_info_STH1.json). Then the other 12, one window at a time, by a loop that stops at the first window not ok (RAN, str_loop.txt, run_S<code>_upgrade.txt).
- 14 windows under the lock with round ADDIN-1b, the refused one included, 0 waits, 4.39 to 6.65 GiB free at the lock, the run script ended with exit code 0 and no Revit was left after each, 52.0 min in all by the lock (RAN, windows.jsonl). The window minutes of the table below come from the run.json of each window, 2.49 to 5.05, while windows.jsonl gives 2.51 to 5.07 for the same 13 windows, since the lock times the whole run script (RAN, round_addin\logs\p1b_review_minutes.py and .txt). Revit exited with exit code 0 in the 13 upgrades, the run script stopped it in the refused one (RAN, testhost_S<code>_upgrade_run.json). The watcher answered 5 security dialogs in each, in our own Revit only, Do Not Load for RCRC Green and RCRC Green Audit, Load Once for our three (RAN, testhost_S<code>_upgrade_watcher.json). The registry was not read or written. In Revit each upgrade showed one task dialog, TaskDialog_Views_Related_To_Analytical_Changed, closed by the test host as for the SE models in Phase 1, and logged 2 to 18 warning lines, 106 in all: 53 openings that partly cut their host, 30 analytical elements invalid after the upgrade, 21 elements joined that do not intersect and 2 family errors (RAN, testhost_S<code>_upgrade_log.json).

| Code | Open s | Save s | Window min | Peak private MiB | Peak working set MiB | Least free GiB | Not responding samples | Warning lines | Bytes |
|---|---|---|---|---|---|---|---|---|---|
| C10 | 110.2 | 11.7 | 2.49 | 2,020 | 2,136 | 2.46 | 3 | 4 | 56,188,928 |
| TH1 | 184.3 | 6.4 | 3.65 | 1,389 | 1,649 | 2.37 | 0 | 6 | 33,140,736 |
| VD2 | 260.2 | 13.5 | 5.05 | 2,052 | 2,360 | 2.70 | 2 | 4 | 71,909,376 |
| DP1 | 196.6 | 11.7 | 4.29 | 2,046 | 2,334 | 2.85 | 3 | 10 | 61,091,840 |
| DP2 | 233.8 | 10.7 | 4.55 | 2,047 | 2,350 | 3.65 | 2 | 6 | 69,459,968 |
| DP3 | 208.9 | 11.0 | 4.15 | 1,999 | 2,294 | 4.06 | 2 | 3 | 62,906,368 |
| DP4 | 223.6 | 11.1 | 4.38 | 2,048 | 2,358 | 3.79 | 2 | 15 | 68,169,728 |
| DT1 | 165.1 | 5.4 | 3.32 | 1,387 | 1,643 | 4.39 | 0 | 4 | 34,672,640 |
| DT3 | 195.0 | 6.4 | 3.83 | 1,435 | 1,700 | 4.35 | 0 | 17 | 38,879,232 |
| TH3 | 196.9 | 5.6 | 3.84 | 1,416 | 1,686 | 4.53 | 0 | 18 | 35,938,304 |
| VD4 | 248.0 | 11.0 | 4.81 | 2,053 | 2,348 | 3.61 | 2 | 15 | 68,648,960 |
| VL4 | 147.6 | 9.7 | 3.09 | 1,821 | 2,092 | 3.72 | 2 | 2 | 46,997,504 |
| VL6 | 178.1 | 9.9 | 3.62 | 1,941 | 2,210 | 3.63 | 2 | 2 | 52,465,664 |

- (RAN, round_addin\logs\str_check.py and str_check.txt, the memory block of each testhost_S<code>_upgrade_run.json.) Peaks are to the exit of Revit, throughExit true on 13 of 13, 29 to 60 samples a window, 0 samples under the 2 GiB floor and 0 failed. Revit and the processes it started held up to 2,021 to 2,704 MiB private together in one sample. Not responding came in 9 of 13 windows on 2 or 3 samples each, in 8 during the open step and in DP1 while Revit started, before the job, and each window ended ok, as in Phase 1 (RAN, the samples against the steps of each log).
- Second method (RAN, str_check.txt): the format of each new file from its bytes by rvt_info.py against the targetInfo step Revit logged, its size against the file, the log result ok and exit code 0, 65 of 65 checks match, 0 mismatch, that is 13 files times 5 checks: format 2024 by the bytes, format 2024 by the targetInfo of Revit, size equal to the file, log result ok and exit code 0. The 13 new file names equal the 13 STR link files the SE models of IMA and IMB opened in Phase 1 (READ, the linkOpenings of testhost_KA_upgrade_log.json and testhost_KB_upgrade_log.json, 13 of 13). Prototypes_2024\STR holds the 13 files and the 13 _backup folders Revit writes beside a new central (RAN). Their SHA-256 are in round_addin\logs\sha256_str_2024.txt for a later round.
- A LocalDB of Steel Connections (sqlservr 21440) started in the refused first window and ran on as a helper of the later windows, 570 MiB private at the start of the second STH1 (RAN, run_STH1_upgrade_2.txt). It was still running after the last window (RAN, tasklist).

### Review fixes

The breaker and the claim-checker read PR 44. Each code fix came with a test that names its rule, seen failing first for the right reason (RAN), then passing.

- R312 record, withdrawn after two attempts (Q069). First attempt: record filed a made copy only because the work copy hashed like the source today, so a _TEST-KA.rvt an earlier job left was filed under a later source. Second attempt, commit 60e0b04: record took the run.json of the job and its log and held the start of the run against the latest of the creation and write time of the work copy and the made copy against the run. The second breaker pass fooled it twice. A work copy refreshed by Copy-Item -Force keeps its old creation time and takes the write time of the source, so it still reads as written before the run (RAN on dummy files, round_addin\logs\p1b_review_copy_force.ps1 and .txt: creation kept True, write time of the source True, before the run True). And only the write time of the made copy tied it to the run, so a copy of a parallel window copied onto the made name passes (READ, the finding of the breaker). A third fix stops by the round rule. Commit c15d5e3 removes record, its run proof and their tests, check says that no step of key_copies.py files a made copy and points to Q069 (test_r312_record_is_withdrawn_so_check_asks_for_a_remake_by_hand..., seen failing first), and a remake is done by hand. The race of shutil.move over a file and two records at once, items 3 and 5 of the pass, went with record. The manifest is still written by a temporary file and a rename, now tested through import.
- R312 names: the copy name and the source file name must hold the code of their own key at codeInName, ^[0-9]{5}-PSN-{code}-, and no code of another key, so a swap is refused. import holds every entry to the rules of the manifest reader and refuses the whole import with every reason (no format, a source with a drive, a copy twice). check lists every .rvt in the copy folder no key and no entry names as notListed, exit not 0. Every function names its rule in its docstring, by a test.
- R312 and R313 places: paths compare by the place Windows opens, trailing dots and blanks off, either slash, any case, so Revit All Zones\2024.\x.rvt is refused as a source in the copy folder and a set target Prototypes_2024. as the upgraded ARC folder. key_copies.py does it in code, held against GetFullPathNameW by a Windows only test (14 cases, skipped on the Linux runner), PrototypeSets.ps1 by Windows itself. A set target that is its source, the past ARC prototype folder, a folder above either or inside either, or the upgraded ARC folder or a folder above it is refused. A folder inside the upgraded ARC folder stays allowed, since the real STR target is one and nothing reads folders inside it (CHOSEN).
- Config: answerKeys holds codePrefix IM, zonePattern, kinds TU and SE and codeInName, prototypeNames holds the prefix RRE-WP-NSP-KEO-, the plan discipline ARC and the suffix -ZZ-MOD-. The run script writes prototypeNames into every job that names a prototype file and the guard in Common takes them from the job. The real config gives the 7 keys and KA to KG as before (test), and the same 13 STR files and 13 ARC plan files as the literal names, 13 of 13 each (RAN, round_addin\logs\p1b_review_names_real.ps1 and .txt).
- Real case of the run proof before its withdrawal: the work copies of Phase 1 are gone, so its start against the work copy could not be held on them. The other parts held on the 7 Phase 1 runs and copies, 35 of 35 (RAN at f05c2e0, round_addin\logs\p1b_review_run_proof.py and .txt, which imports functions c15d5e3 removed).

### Not done in this PR

- No copy was made again, all 7 key copies reuse. record is withdrawn (Q069), so no step of the tool files a remade copy.
- The 13 STR files were not read with the reader, since the add-in places the ARC prototypes (Q067 asked to upgrade them only).
- Install-TestHost.ps1 was not run after the guard took prototypeNames, no Revit window was needed. The installed test host still holds the guard of the first fix and ignores prototypeNames, so the next window reinstalls first.

### Gate

- At the final head: Python 1201 passed and 1 skipped, .NET build 0 errors and the 1 old warning (xUnit2013), .NET 516 passed (RAN, round_addin\logs\p1b_final_gate_python.txt, p1b_final_gate_build.txt, p1b_final_gate_test.txt, each with the head it ran on). Main before this PR: 1122 and 1 skipped, 479. In this PR: 74 R312 test cases, 14 of them on Windows only and skipped on the Linux runner, 5 R301 test cases and 37 R313 test cases (RAN, pytest --collect-only and dotnet test --filter PrototypeSetTests).
- GitHub on the first push of PR 44: the python check failed on 1 test, test_r312_a_copy_whose_source_is_not_the_one_of_the_config_is_refused. Its helper hashed the source by the path written in another case, which the Linux runner does not find (FileNotFoundError), while key_copies.py hashes the source of the config. Fixed in the test, the next run passed python and dotnet (RAN, gh pr checks 44).

### Questions

- Q067 closed: the 13 STR prototypes are upgraded into Prototypes_2024\STR (Bader's instruction).
- Q069 new: record of a remade key copy, withdrawn after two attempts. Recommended: when a key source changes, the test host writes the SHA-256 of the work copy at the job start and of the made copy at its end into the run log, and record files a copy only when the work copy hash equals the source hash of the manifest request and the made copy hash on disk equals the logged one. Until Q069 is answered a remade copy is filed by editing its manifest entry by hand, since import refuses once a manifest exists.
- Known bug, recorded only: the GetFullPathNameW mirror of key_copies.py keeps a last part NUL, nul. or "NUL. " as a name where Windows opens the NUL device, reachable only by answerKeys.folder of the config, and PrototypeSets.ps1 throws on it.

## Phase 2 part 1, PR 45 data/addin-key-reads

Branch data/addin-key-reads from main at eedf3c5, equal to origin/main after git fetch (RAN). Rules R320 to R323 are new, R36 and R112 changed, R324 is not used. The phase ran in a helper agent started by the main session, which runs the checkers and merges. Values of coordinates and levels stay in Documents\SiteAdaptation\round_addin\reads, this file holds counts, distances and differences only.

### Originals (R201, R202)

- The 60 files of Phase 1b (the 40 originals, the 7 key copies, the 13 Revit 2021 STR files) hashed with sha256sum before the first edit: 60 of 60 equal originals_p1b_after.txt (RAN, round_addin\logs\originals_p2a_before.txt). After the last window: 60 of 60 equal by sha256sum and by Get-FileHash of PowerShell, differ 0 (RAN, originals_p2a_after.txt, originals_p2a_after_filehash.txt).
- key_copies.py check gave reuse 7 of 7 and exit 0 before the first read and after the last (RAN, p2a_key_copies_check_before.txt, p2a_key_copies_check_after.txt).

### What the reader wrote before this PR, field by field

Read in the 7 Phase 1 reads testhost_K*_prototypes.json and in RevitModelSource.cs (READ):
- Links: file, transform (origin, rotation, mirror and the three axes), rawName, nameParts, every instance parameter with the 9 RRE parameters and CASE among them, and the named plotNumber, prototype, ffl, steps and case. Present, unchanged. transformShared is written for a loaded link only (R54) and no link loads in a read, so the plan builders add the survey offset to the transform origin (R81).
- Floors: type, sketch, corners with their levels (R34), outline in shared coordinates (R52), level and top. Present, unchanged.
- Walls: type, location line ends, length, height (Unconnected Height, on 926 of 926 IMC walls, those up to Level 2 included), and in parameters Base Constraint, Base Offset, Top Constraint, Top Offset and Location Line as the integer 0 to 5 Revit holds. Present, unchanged. The location line stays a code and the wall width, a type parameter, is not read, since step 1 does not ask for it (CHOSEN, Phase 3 adds it if the wall rule needs the faces).
- Spot elevations: elementId, origin, view and the host with category and type (R55). Present. value was null on every spot of the 7 Phase 1 reads, no spot holds the key (READ). The reader asked SPOT_ELEV_TOP_VALUE and SPOT_ELEV_BOT_VALUE, Top Coordinate and Bottom Coordinate in RevitAPI.xml of Revit 2024, parameters of a spot coordinate, while a spot elevation shows Single/Upper Value (READ). The spot type and its elevation base were not read. Fixed by R320.
- Survey point, project base point and angle (R4), and the site facts of R311 with the project position, levels, units and categories. Present, unchanged.
- The Generic floors of IME and IMF are of type FFL, outside floors.types (known bug of Phase 1). Fixed by R321.

### R320, the spot value

- Tests first (RAN, round_addin\logs\p2a_r320_first_build.txt): the 4 R320 tests did not build, 20 errors: CS0103 (5) and CS0246 (5) for SpotValues and RawSpotValue, CS1061 (9) for the new SpotRow fields and CS0117 (1, KeyReadTests.cs line 75, SpotRow has no Parameters). Then the 4 and the R36, R55 and R56 tests passed, 15 (RAN).
- On IMC first (RC): value on 987 of 987 spots, type Target (Project), elevation base Survey Point and Display Elevations Actual (Selected) Elevation on 987, no Lower Value on any (RAN). value against the origin z plus the survey offset of R51: 0 mm on 987 of 987 (RAN). The survey offset is 0 in z in all 7 keys, so that compare proves feet to metres only, and the review replaced it by the floor top under each spot (see Review fixes).
- The TU keys IMD to IMG: value on every spot (RAN). The SE keys IMA and IMB: value on 0 of 822 and 0 of 587. Their spots are of type Target (Survey), show Bottom Elevation, sit in Structural Foundation Plan views and are hosted by STR link instances, which no read loads (RAN). The read gives no value and their parameters list no Single/Upper Value, so whether the parameter is missing or empty there is UNKNOWN, the error line of R320 added in the review says which on the next read. Whether they read with the STR links loaded is UNKNOWN. They mark footings, not plot entrances, so 2b does not need them (CHOSEN).
- The largest read is IMF at 15.16 MiB (15,896,045 bytes) against 13.90 MiB (14,571,841 bytes) in Phase 1, under the 20 MB (19.07 MiB) of R56 (RAN, the file sizes). The review corrected 15.16 MB against 14.57 MB, which mixed MiB and MB, and renamed fileMb of key_reads.json to fileMib.

### R321, the floor types of each key

- floors.keyTypes of config/project.json: IME and IMF read their Generic floors by FFL and the yard and ramp kinds by floors.types, the types their reads hold (CHOSEN per model, the brief allowed per job or per model, READ in the reads). plan_from_past.py --key passes the code and the plan keeps those types in floorSettings, since the placer and the check know a floor kind by floorSettings (CHOSEN).
- Tests first (RAN, round_addin\logs\p2a_r321_first_tests.txt): 12 failed, TypeError since load_floors took no key, DID NOT RAISE on 8 refused shapes and SystemExit 2 on --key. Then 12 passed (RAN).
- On the reads: Generic floors 324 of IME and 490 of IMF, 0 of each by floors.types alone (RAN, key_reads.json).

### The 7 reads (R322)

- Install-TestHost.ps1 -NoBuild -SkipSigningReport under PowerShell 7.6.7 installed the build of 7b82696: one SHA-256 for the Common DLL of the reader, the placer, the watcher and the two builds, 5 of 5, and the reader DLL equal to its build (RAN, round_addin\logs\install_p2a.txt).
- Each read: p2a_read_key.ps1 in round_addin\logs hashed the key copy against the manifest, copied it into round_addin\work as <copy>_TEST-R<letter>.rvt, ran the read job of the test host under the lock of R200 with round ADDIN-2a, moved the outputs into round_addin\reads\R<letter>, hashed the work copy again and deleted it. IMC first, then the other 6 by p2a_read_loop.ps1, which stops at the first read not ok (RAN, p2a_read_loop.txt). Each work copy equalled the key copy before and after its window, 7 of 7, and each was deleted (RAN, copy_R*.json).
- 7 windows, one at a time, 0 waits, 2.10 to 2.94 GiB free at the lock, exit code 0, log result ok, 0 dialogs in Revit and 0 failure lines in each (RAN, windows.jsonl, run_R*_read.txt). The watcher answered 5 security dialogs in each in our own Revit only, Do Not Load for RCRC Green and RCRC Green Audit, Load Once for our three (RAN). The registry was not read or written.

| Key | Job | Links, with a plot | Floors generic, yard, ramp, of no kind | Walls | Spots, with a value | Levels | Window min | Open s, read s | Peak private MiB, working set MiB | Least free GiB, samples under 2 GiB |
|---|---|---|---|---|---|---|---|---|---|---|
| IMC | RC | 338, 337 | 336, 337, 337, 0 | 926 | 987, 987 | 2 | 1.22 | 3.3, 10.3 | 1,200, 1,355 | 0.53, 9 |
| IMD | RD | 411, 410 | 410, 410, 410, 0 | 1,138 | 1,256, 1,256 | 2 | 0.72 | 2.8, 11.8 | 1,282, 1,470 | 0.72, 8 |
| IME | RE | 325, 324 | 324, 324, 324, 0 | 894 | 938, 938 | 2 | 0.59 | 2.1, 7.1 | 1,192, 1,377 | 0.72, 7 |
| IMF | RF | 491, 490 | 490, 490, 490, 0 | 1,317 | 1,415, 1,415 | 2 | 0.74 | 4.4, 12.8 | 1,584, 1,777 | 0.65, 7 |
| IMG | RG | 298, 297 | 297, 0, 297, 0 | 859 | 866, 866 | 2 | 0.54 | 2.0, 4.5 | 1,086, 1,268 | 1.42, 6 |
| IMA | RA | 433, 432 | 733 (the 733 of IMB), 0, 0, 28 | 2,323 | 822, 0 | 2 | 0.59 | 2.6, 6.3 | 1,215, 1,388 | 0.99, 6 |
| IMB | RB | 302, 301 | 733 (the 733 of IMA), 0, 0, 40 | 1,515 | 587, 0 | 2 | 0.55 | 2.4, 5.0 | 1,130, 1,299 | 1.16, 5 |

(RAN, round_addin\reads\key_reads.json, logs\p2a_key_reads.txt.) Every count equals the Phase 1 table above for the same model. Walls run from 859 (IMG) to 2,323 (IMA). IMA and IMB hold the same 733 Generic floors, the same element id and the same whole row in both reads, 432 on IMA plots and 301 on IMB plots, so a total over the 7 keys is 2,590 Generic floors, not 3,323 (RAN, sharedWith of key_reads.json, added in the review). The window minutes are those of run.json, the lock gives 0.57 to 1.26 for the same windows (RAN, windows.jsonl). Peaks are to the exit of Revit, throughExit on 7 of 7. Every link file count is 14. The levels are Level 1 and Level 2 in all 7.

- Floors by type: IMC Generic 150mm 336, yard 337, Ramps 0.5% 280, Ramps 2% 47, Flat Ramps 10. IMD 410, 410, 350, 55, 5. IME FFL 324, yard 324, Ramps 0.5% 248, Ramps 2% 76. IMF FFL 490, yard 490, 409, 75, 6. IMG Generic 150mm 297, Ramps 0.5% 229, Ramps 2% 68, no yard. IMA and IMB Generic 150mm 733 each, and 28 and 40 structural floors of three KEO_ST types (RAN).
- Walls by type: the TU keys hold BW (898, 1,108, 882, 1,304, 849) and Generic Wall-150mm_ Garden walls in 1 to 5 type names (28, 30, 12, 13, 10). The SE keys hold SBW and SBW 2 (2,275 and 1,451) and 48 and 64 KEO_ST walls (RAN).
- Spots by host: on the TU keys the ramps hold most. IMC Ramps 0.5% 827, Ramps 2% 129, Flat Ramps 28, Generic 2, no host 1. IMD 1,032, 171, 15, Generic 34, yard 2, no host 2. IME 726, 208, no host 4. IMF 1,175, 220, Flat 18, FFL 2. IMG 666, 200. On IMA and IMB every spot is on an STR link instance, 13 files each (RAN).
- What the SE keys lack, per field (RAN): the links carry 5 of the 10 parameters, Number-of-Steps, Façade-Color, Façade-Design-Option, Prototype-HVAC-Schedule-Type and Prototype-FFL are on none of the 432 and 301 plot links. Floors: no yard and no ramp, the 733 Generic floors and the structural floors only. Walls: SBW types, no BW. Spots: no value, footings only. Levels: Level 1 and Level 2 like the TU keys. They hold link position, hand, plot number, prototype and CASE. Q066 stays open.
- Memory: free memory at the lock was 2.10 to 2.94 GiB, lower than the 6.26 to 7.34 GiB of Phase 1 (RAN, windows.jsonl). Why is UNKNOWN: no listing of the other programs was saved, so the cause given before, other programs holding more memory, is withdrawn. Revit started 9 to 11 child processes in these windows, 11 in RC, 10 in RD, 9 in the 5 others (RAN, childProcessesMax of each run.json). It fell to 0.53 to 1.42 GiB during each window, under the 2 GiB floor in 5 to 9 samples of each. R310 logs the floor in every sample and stops nothing on it (READ, R310), and every window ended ok. Revit did not respond on 6, 3 and 3 samples of RC, RD and RF, all ended ok as in Phase 1 (RAN). The LocalDB sqlservr of the first window was a helper of the 6 after it, 492 to 555 MiB private at their starts (RAN).

### Second method (R323)

As first written, key_reads_check.py (RAN at 7b82696 with the uncommitted script, round_addin\logs\p2a_key_reads_check.txt):
- Categories of R311 in the same read: links against RVT Links, walls against Walls, spots against Spot Elevations and the floors whose Category parameter is Floors against Floors, 28 of 28 equal over the 7 keys. The 28 and 40 structural floors of IMA and IMB are under Structural Foundations, beside its 1,199 and 877 elements.
- The bytes of each key copy by rvt_info.py: the TU keys name the same 14 link files as the read, 14 of 14 on 5 of 5. The SE keys name 1 in their bytes, the 13 STR cloud links are only in the read, the known bug of R203.
- The Phase 1 read of the same key copy by element id: the same link ids on 7 of 7, 280 of 280 links equal at seeds 14 and 7, 30 of 30 floor types and 28 of 28 wall types with the same ids. The review names this the repeat read: the same bytes read by the same kind of code, it shows the reads repeat and is no second method.
- The spot value against the origin z plus the survey offset, 5,462 of 5,462 TU spots within 1 mm. The review withdrew it as a second method, the offset is 0 in z in all 7 keys, so it proves feet to metres only.

The routes of the review are under Review fixes.

### Review fixes (new commits on PR 45)

The breaker and the claim-checker read PR 45. Each code fix came with a test that names its rule, run first and seen failing for the right reason with the output saved, then passing (RAN, round_addin\logs\p2a_review_item*_first.txt and _after.txt, each with its head). No Revit window was opened in the review, so no file was hashed for one.

- Item 1, 2d415ec (R322, R323): nothing tied a read job to its key, so --key IME=RF --key IMF=RE filed the counts of IMF under IME. key_reads.py and key_reads_check.py now refuse a read whose model file does not hold the code of its key at answerKeys.codeInName or holds the code of another key, and a key or a job given twice (plan_from_past.check_key_file). Tests test_r322_a_read_is_counted_only_under_the_key_its_model_file_names_by_code_in_name, test_r322_a_key_or_a_job_given_twice_is_refused (3 cases), test_r323_a_read_is_checked_only_under_the_key_its_model_file_names..., 5 failed first with DID NOT RAISE. The 7 reads and the 7 Phase 1 reads each name their own code (RAN).
- Item 10, a0dcc7e (R322, R323): a run with one key wrote the json with that key alone. Both scripts now merge their keys into the file in place and keep the others, and refuse a file there that is no report. A link with no file is no file and names in another case are one. Tests test_r322_a_run_merges..., test_r323_a_run_merges..., test_r322_a_link_with_no_file..., 3 failed first.
- Items 9 and 5, 57bc10d (R322): the SE spots come by the dependents route of R55 with the link instance as the host, so the in a link branch never fired and 822 spots split into 13 kinds by link type. A host that is a link instance of the read is now in a link, with inLinkByFile beside it: IMA 822 and IMB 587 in a link, 13 link files each (RAN). sharedWith names per other key the rows both reads hold with the same id and the same whole row: IMA and IMB share 733 floors and nothing else, the 79 spot ids and 3 wall ids that meet between keys are other rows (RAN, key_reads.json). Tests test_r322_spots_on_a_link_instance..., test_r322_rows_two_keys_both_hold..., 2 failed first.
- Items 3, 4 and 8, 691faa2 (R323): two real second methods and the repeat read named as such. The floor top under each spot: a spot on a floor of the read is put in shared coordinates by the survey offset and its value held against the top of that floor at the spot, the level along the nearest outline edge of R52 within 5 mm in plan, else the plane through the corners of R34 when they lie on it within 1 mm. The per plot rule of the TU keys: one link, one generic floor, one yard and one ramp per unit plot, each element counted by its own plot parameter, a yard with no Plot parameter by the generic floor whose shape holds it, also by floors.types alone for IME and IMF. A read with no survey point gives the spot route as not read, it crashed with KeyError before (RAN on the committed code, p2a_review_item3_item4_item8_first.txt). Tests on made up floors test_r323_the_spot_value_is_held_against_the_top_of_its_floor..., test_r323_a_plane_needs_three_corners..., test_r323_a_read_with_no_survey_point..., test_r323_each_unit_plot_holds_one_link..., and the main test, 5 failed first.
- Item 7, 0867714 (R321): plan_from_past.py --key refuses a read whose model file holds no code of the key or the code of another, and a plot whose floors are of no kind says their type is not in floors.types, or floors.keyTypes of the key, not that no floor names the plot. On the IMC read --key IME is refused and --key IMC writes 337 of 337 plots into the scratchpad, and the IME read by floors.types alone gives the new reason on 324 plots (RAN, p2a_review_item7_real.txt). Tests test_r321_a_floor_of_no_kind_on_a_plot..., test_r321_plan_from_past_refuses_a_key..., 2 failed first, and the R112 test of a type of no kind takes the new reason.
- Item 6, 83b3713 (R320): a spot with no Single/Upper Value or no Elevation Base gave null and no error line. RevitModelSource now keeps the reason, no parameter, no value, no number or the Revit error, and ModelReader in Common writes one line per reason with the count and the first 5 ids, starting spotElevations value or spotElevations elevation base. A Lower Value that does not read gives no line. Tests R320_a_spot_with_no_value_or_no_elevation_base_is_one_error_line... on fakes through ModelReader, and R320_the_reader_gives_the_reason..., which did not build first, 6 errors CS0117 (RAN, p2a_review_item6_first_build.txt). Not proven in Revit: the 7 reads were made before it and no window was opened.
- Item 2, the spot datum, no code change. elevationBase is spot.DimensionType.get_Parameter(BuiltInParameter.SPOT_ELEV_BASE).AsValueString(), a parameter of the spot type, never of the instance (READ, RevitModelSource.cs). RevitAPI.xml of Revit 2024 labels SPOT_ELEV_BASE Elevation Base (READ), the label table of Revit 2024, en-US\GeneratedRes.dll, holds Elevation Base among the spot labels beside Display Elevations and Bottom Coordinate, and no file of the Revit 2024 install holds the text Elevation Origin, while 5 hold Elevation Base (SEARCHED, p2a_review_spot_datum.txt). So the reader reads the right parameter, the base of the type, and Survey Point is its value on every spot of the 7 reads. The name Target (Project) is a type name only: 5,452 TU spots carry it and 10 of IMD carry FFL LEVEL, all with base Survey Point, the SE spots are Target (Survey) (RAN, p2a_review_spot_datum.txt). The values agree: the survey offset is 0 in z and the base point sits on the survey point in all 7 keys, and the values equal the floor tops in shared elevation on 5,445 of 5,455 TU spots (below), which a base of the project base point would not give, since that point sits off the internal origin in z (RAN, p2a_review_spot_datum.txt). A base relative to Level 1, at elevation 0, would give the same values, so the values alone do not tell Survey Point from Relative, the type parameter does. No Revit window was needed (CHOSEN).

Floor top under each spot (RAN at 83b3713, round_addin\reads\key_reads_check.json, logs\p2a_review_key_reads_check.txt):

| Key | Compared | Within 1 mm | Within 5 mm | Share 1 mm | Off, all at the floor bottom | Not on a floor of the read |
|---|---|---|---|---|---|---|
| IMC | 986 | 986 | 986 | 1.000 | 0 | 1 |
| IMD | 1,254 | 1,251 | 1,251 | 0.998 | 3: Generic 150mm 2 at -150 mm, Flat Ramps 1 at -100 mm | 2 |
| IME | 934 | 934 | 934 | 1.000 | 0, worst 0.7 mm | 4 |
| IMF | 1,415 | 1,411 | 1,411 | 0.997 | 4: FFL 2 at -150 mm, Ramps 0.5% and Ramps 2% 1 each at -100 mm | 0 |
| IMG | 866 | 863 | 863 | 0.997 | 3: Ramps 0.5% 1 and Ramps 2% 2 at -100 mm | 0 |
| IMA, IMB | 0 | | | | no value on 822 and 587 | |

- Per host kind at 1 mm: Ramps 0.5% and Ramps 2% 0.990 to 1.000 on every TU key, Flat Ramps 1.000 on IMC and IMF and 14 of 15 (0.933) on IMD, Generic 150mm 2 of 2 on IMC and 32 of 34 (0.941) on IMD, FFL 0 of 2 on IMF, the yard 2 of 2 on IMD. The 10 spots off sit on an outline edge and their gap is minus the thickness of their floor within 1 mm, so they show its bottom, not its top. Over the TU keys 5,445 of 5,455 within 1 mm and within 5 mm, the 7 others have no host or one that is no floor of the read. The edge route gave 5,425 and the plane 30, the inside spots of IMD. Three IMG ramps have corners off one plane by more than 1 mm, up to 27.5 mm, and their spots sit on corners, so the edge route takes them (RAN, round_addin\logs\p2a_review_spot_datum.py and .txt).

Per plot rule (RAN, same files): IMC 336 of 337 plots hold, plot 2441 has no generic floor and its yard sits in no generic floor. IMD 410 of 410, IME 324 of 324 and IMF 490 of 490, each yard by the generic floor that holds it since no yard has a Plot parameter. By floors.types alone IME and IMF hold on 0 of 324 and 0 of 490, no generic floor and FFL of no kind on every plot, so FFL is the generic floor of each unit plot there, the second method of R321. IMG 0 of 297, all failing for no yard, IMG holds no yard floor. IMA and IMB 0 of 733, all failing for no yard and no ramp, and 301 and 432 plots of the shared Generic floors have no link in that model. Each key has 1 link with no plot number.

Second way of the two new routes (RAN, round_addin\logs\p2a_review_second.py and .txt, numpy and shapely, other inputs): the nearest corner within 5 mm else a numpy least squares plane, in internal coordinates, gives the same count within 1 mm and the same off ids on 7 of 7 keys. The link plot from the plotNumber field, the floor plot from the text of the Plot parameter and the yard by the corner polygon of the generic floor that holds the centroid of the yard corners give the same plots and the same breaking plots on 7 of 7 keys.

Records (items 11 to 20): walls 859 to 2,323, MiB for the read sizes, the build errors with CS0117, the memory cause UNKNOWN, 9 to 11 Revit child processes, the questions as one short sentence each, up to 7 to a line, with Q011, Q038 and Q063 put back whole in their short form, and the head of every gate. The R322 and R323 tests seen failing first before the review (ModuleNotFoundError) have no saved output, so that claim stays READ with no saved output. The review tests have their failing first output saved.

### Second breaker pass (new commits on PR 45)

A second breaker pass read PR 45. Each code fix has a test that names its rule, run first and seen failing for the right reason with the output saved, then passing (RAN, round_addin\logs\p2a_breaker2_item*_first.txt and _after.txt, each with its head). No Revit window was opened.

- Item 1, f8e54df (R321, R322, R323): check_key_file took a read by the start of its model file name, so 01101-PSN-IMC-ZZZ-PIM-TU-000001_TEST-WORK-S1, a floor scenario file the placer changed, _TEST-MASTER and the 2021 original passed as IMC, and a work copy of RD passed under the job RC. A read is now of a key only when its model is the copy name of that key in answerKeys without .rvt, then _TEST- and the job, a job code of the test host, in any case and with or without .rvt. The job of a repeat read or of plan_from_past.py --key is the one the test host names its file by, testhost_<job>_prototypes.json. Tests test_r322_a_read_is_counted_only_when_its_model_is_the_work_copy... (7 names), test_r322_a_job_is_a_job_code..., test_r321_the_work_copy_of_the_key_copy..., test_r321_the_real_keys_take_the_work_copy..., which builds the names from config/project.json, test_r323_a_read_and_its_repeat_read..., and the plan_from_past.py --key test with the 7 names: 12 failed first, DID NOT RAISE on 10, TypeError on 2 since check_key_file took no job, and the --key test went on past the check to the baseplan (RAN, p2a_breaker2_item1_first.txt).
- On the real reads (RAN, p2a_breaker2_item1_real.txt): the 7 reads RA to RG are accepted, and the Phase 1 reads KA, KB, KD, KE, KF2 and KG3. KC, the Phase 1 read of the IMC copy, is refused: its model is 01101-PSN-IMC-ZZZ-PIM-TU-000001_TEST-KC, the name the siteUpgrade job gave the copy before it was filed as ...-zone2.rvt (R204). So the rerun gives IMC no repeat read. KM, a read of a test copy of the zone2 file, the floor scenario master (Q065), is accepted by name, since that file has the name of the IMC copy. A name can not tell them apart, only the hash of the copy can, and check_key_file reads no hash (UNKNOWN whether the lead wants a hash tie, copy_<job>.json holds the hashes of each 2a read).
- Item 2, no code (R323): the per plot rule counts by plot value only and never checks where an element sits, so two neighbouring plots with their plot values swapped both hold, and a yard with no Plot parameter takes the plot of the generic floor that holds it, so it can never disagree with that floor. R323 says so, and the brief of 2b asks for the place check of the links, Generic floors and ramps against their face by the join (CHOSEN, the join is 2b).
- Item 3, no code (R323): on the 7 keys the shared and the internal elevation are the same, the survey point z equal both ways and the project position elevation 0, so the floor top route can not tell a spot value given as a shared elevation from one given as an internal one (RAN, p2a_breaker2_item3_datum.txt, booleans only). The survey offset is 0 in z only, it is not 0 in plan on any key, so the lines above that said the survey offset is 0 in all 7 keys now say in z (RAN, same file).
- Item 4, cd59792 (R323): the plane route never tested that the spot lies in its floor. A spot more than 5 mm from every outline edge must now lie in the outline loops by the even-odd rule, a hole is outside, else in the sketch outline, and a spot outside is outsideItsFloor. Test test_r323_a_spot_outside_the_outline..., failed first with 3 compared for 1.
- Item 5, c53a65c (R323): a spot with no host was notOnAFloorOfTheRead, it is now noHost. Tests test_r323_a_spot_with_no_host... and the floor top test, failed first.
- Item 6, 9c0c17c (R323): worstMm was 0 when nothing was compared, it is now null. Test test_r323_the_worst_gap_is_null..., failed first with 0.0.
- Item 8, 5e090e1 (R323): a repeat read that is the new read itself, the same file by any path or a read of the same job as a copy of it, is refused. Test test_r323_a_repeat_read_that_is_the_new_read_itself..., failed first with DID NOT RAISE.
- The rerun at 5e090e1 (RAN, p2a_breaker2_key_reads_5e090e1.txt and .json, p2a_breaker2_key_reads_check_5e090e1.txt and .json): the 7 job folders copied into the scratchpad and both scripts run there, the reads folder hashed before and after, 51 of 51 files equal. key_reads.json equal to the one of the reads folder on every path. key_reads_check.json differs on 9 paths and no other (p2a_breaker2_compare_5e090e1.txt): the 7 spots with no host are noHost, 1, 2 and 4 on IMC, IMD and IME, the same as no host of key_reads.json by its own code, the worst gap of IMA and IMB is null, and IMC has no repeat read. No spot is outside its floor, the 30 of the plane route lie in theirs. With KC given, the check stops on the IMC read with exit code 1 (p2a_breaker2_key_reads_check_KC_5e090e1.txt).

### Gate

- At 3dcacaa: Python 1223 passed and 1 skipped, .NET build 0 errors and the 1 old warning (xUnit2013), .NET 520 passed (RAN, round_addin\logs\p2a_gate_python.txt, p2a_gate_build.txt, p2a_gate_test.txt, each with the head it ran on). Main before this PR: 1201 and 1 skipped, 516. In this PR: 22 Python tests of R321 to R323 and 4 .NET tests of R320.
- At b17fcd6, after the review fixes and their first records: Python 1238 passed and 1 skipped, .NET build 0 errors and the 1 old warning (xUnit2013), .NET 522 passed (RAN, round_addin\logs\p2a_review_gate_python_b17fcd6.txt, p2a_review_gate_build_b17fcd6.txt, p2a_review_gate_test_b17fcd6.txt, each with its head). The review added 15 Python tests and 2 .NET tests and replaced one. The gate at the final head, after this line, is in the PR body with its head.
- The top log entry is 52 lines and 7,328 characters by the method of test_r26 (at most 70 lines and 9,000 characters), counted on the log of the final commit of the review, which this line does not change (RAN).

### Not done in this PR

- R324 is not used, the PR holds four changes.
- The wall location line stays Revit's code and the wall width is not read (see above).
- The SE spot values, which need the STR links loaded.
- CLAUDE.md was not changed by a helper. The main session added python/key_reads.py, python/key_reads_check.py, floors.keyTypes and R320 to R323 to it in 27fa56f (READ, git log).
- A hash tie of a read to its key copy, see item 1 of the second breaker pass. The place check of the per plot rule is PR 2b.
- The plans, the join, the area codes and the spots at the plot entrance are PR 2b.

### Questions

- None new and none closed. The older open questions are shortened to one short sentence each, up to 7 to a line, in steps/PROGRESS.md and the top entry of steps/log.md, with their full text kept under Open questions, full text in steps/questions-closed.md.

## Phase 2 part 2, PR 46 data/addin-key-plans

Branch data/addin-key-plans from main at 104c1f3, equal to origin/main after git fetch (RAN). Rules R325 to R328 are new, R329 is not used, the PR holds four changes. R325 changes how R206 and R264 map a link, R326 adds the source and kind columns to R269 and fills the parsons table of R301. The phase ran in a helper agent started by the main session, which runs the checkers and merges. No Revit or Civil 3D window was opened, the reads of 2a are the input, so the lock was not taken and the registry was not read or written. Values of coordinates and levels stay in Documents\SiteAdaptation, this file holds counts, plot numbers, ids and differences only.

The breaker and the claim checker read the first push (head 5ecd49b). Their 19 items were fixed in new commits bc5a743 to the record commit, each code fix with its test seen failing first, and every output of 2b was made again. The numbers below are those of the rerun, the first run is kept only where it says so.

### Originals and inputs (R201, R202)

- First run: the 60 files of 2a (the 40 originals, the 7 key copies, the 13 Revit 2021 STR files) and 61 inputs (the 51 files of the 7 reads, round_17\past_all.csv, plots.csv, labels.csv and the baseplan plots.json of the round_17 snapshot, plan_imc_past.json, prototypes_imc_past.json, the three Native plans and footprint_imc.json) were equal by hash before and after (RAN, round_addin\logs\originals_p2b_before.txt, originals_p2b_after.txt, inputs_p2b_before.txt, inputs_p2b_after.txt).
- Review run: the same 60 and 61 hashed before the first fix, equal to the after files of the first run, and again after the last run (RAN, originals_p2b_fix_before.txt and _after.txt, inputs_p2b_fix_before.txt and _after.txt, the counts are in the PR body). So no read of 2a, no key copy, round_17\past_all.csv and plan_imc_past.json were written. config/overrides/roshn-makkah.json was hashed before and after the majority run of the review, equal (RAN, p2b_fix_overrides_hash_before.txt).

### What the review changed (R325 to R328)

| Item | Change | Test, seen failing first in round_addin\logs |
|---|---|---|
| 1 | A generic floor of a key joins the one baseplan face that holds cadLinks.cutShareMin (0.9, R120) of its area, in the plan and in the join, never the face its representative point falls on. Below that the plot is cut and listed with every face, number and share. IMB 1995, IME 2634, IMF 702 and IMG 480 and 481 sat 0.50 on two faces and were filed by chance | test_r325_a_generic_floor_joins_a_face_only_by_an_area_share..., test_r325_main_joins_by_the_cut_share..., test_r326_a_generic_floor_joins_the_face_that_holds_cut_share_min..., p2b_fix_r325_first.txt, p2b_fix_r326_join_first.txt |
| 2 | Two key plots on one official number are twoPlots, listed by number, and neither counts as joined or in the area row | test_r326_two_key_plots_on_one_official_number..., p2b_fix_r326_join_first.txt |
| 3 | An ELl@Entry value that puts the flat floor top outside keyPlans.entryHeightM (0.0 to 1.5 m) over it is refused with the height it gives and never used, and the plan has no height for it. The spot counts leave it out too | test_r325_an_el_at_entry_parameter_whose_height_is_outside_the_band..., test_r327_a_key_value_whose_floor_top_is_outside..., p2b_fix_r325_first.txt, p2b_fix_r327_band_first.txt |
| 4, 14 | A spot counts for a plot only when its host floor names that plot, and the spots are grouped by that plot first | test_r327_a_spot_on_a_floor_of_another_plot..., p2b_fix_r327_first.txt |
| 5 | past_all.csv has a kind column from answerKeys (TU or SE), majority.kindLeaveOut keeps the SE rows out of the ramp slope, El @ Entry, ramp front and fall, height, steps and yard decisions | test_r326_the_majority_rule_leaves_the_rows_of_a_kind_out..., test_r326_a_majority_run_over_the_real_past_all..., p2b_fix_r326_past_all_first.txt |
| 6 | El @ Entry of a key plan has the source keyParameter, the plan of the past job keeps pastJob for its schedule | test_r325_a_key_plan_labels_el_at_entry_by_the_key_parameter..., p2b_fix_r325_first.txt |
| 7 | --schedule-entry gives El @ Entry of a schedule only to the key pastAll.scheduleKeys names for its pastSource, and heightSource says what the height was worked out from | test_r326_the_schedule_gives_el_at_entry_only_to_the_key_of_its_own_job..., p2b_fix_r326_past_all_first.txt |
| 8 | entry_spots.py finds the schedule by the official number of the join (--join), the second method by the official number of its own join | test_r327_el_at_entry_of_the_schedule_is_found_by_the_official_number..., p2b_fix_r327_first.txt |
| 9 | The second method draws its samples from every plot of the read, ties a link by the face its house centre stands on, and checks walls per plot, height, steps, modelPlot, source, kind, entrySource and heightSource, and the counts of join, area row, plan and past_all.csv | the R328 tests of test_key_plans_check.py, p2b_fix_r328_first.txt |
| 10 | number_routes takes the whole number after the last dash, so VD2-0481 and TH1-481 both end in 481 | test_r325_the_number_route_takes_the_whole_number..., p2b_fix_r325_first.txt |
| 11 | An answer key plan is never written over pastJob.output, with or without --key, and with --key never under the name or over the plan of another key | test_r325_an_answer_key_plan_with_no_out..., test_r325_a_key_plan_is_never_written_under_the_name_of_another_key..., p2b_fix_r325_first.txt |
| 12 | key_join.json and entry_spots.json keep the inputs and settings of each key, the second method reads them per key | test_r326_each_key_of_the_join_keeps_the_inputs..., test_r327_each_key_keeps_the_settings..., p2b_fix_r327_first.txt |
| 13 | The second method reads floors.plotParameter, the spot bands come from entrySpots.bandsM | test_r328_the_sample_is_drawn_from_every_plot..., the R327 settings tests |

- keyPlans.entryHeightM 0.0 to 1.5 m (CHOSEN, measured): on the 7 reads every generic floor top stands 0.05 to 0.8 m over its ELl@Entry but 21, IMC 10 and IMD 4 whose value is 0, IMF 6 whose value is 150.0 and IMD 1217, which stand 14.8 m to 174 m off it (RAN, scratchpad heights_probe.py).
- The R321 test of a key plan now names its plan plan_imx.json, as item 11 asks of a key plan. The R301 test pins the new parsons counts. The join on every plot of the second method was added after its tests and has no test of its own.

### R325, the plans of the answer keys

- Before 2b: plan_from_past.py --answer-key --names (R206) mapped a link by its Name when that is a floor Plot value, else by its frame (R264). The 1,858 plot links of the 5 TU keys have a Name with bars and no floor Plot value, so no TU link mapped (READ, the reads).
- Now a link maps first by its RRE_ZZ_SP_Plot-Number, then by its Name, then by its frame, keeps its own unit and loses its plot number when nothing maps it. With --key the plan holds El @ Entry of the ELl@Entry parameter within the band, the steps of the link, the height computed, modelPlot, the code of the key as neighbourhood and walls with their plots (CHOSEN, the brief).
- The 7 plans of the rerun, IMC first (RAN, round_addin\logs\p2b_fix_plan_<code>.txt), in Documents\SiteAdaptation: plan_imc_key_past.json, since plan_imc_past.json is kept, and plan_imd_past.json to plan_img_past.json, plan_ima_past.json and plan_imb_past.json, each over the plan the first run of 2b wrote.

| Key | Plots written | Links by parameter | Links not mapped | Generic floors cut | El @ Entry refused | Floors with no official number | Walls | Walls on no plot |
|---|---|---|---|---|---|---|---|---|
| IMC | 324 | 324 | 14 | 12 | 10 | 25 | 926 | 25 |
| IMD | 403 | 403 | 8 | 7 | 5 | 14 | 1,138 | 10 |
| IME | 320 | 320 | 5 | 4 | 0 | 8 | 894 | 10 |
| IMF | 476 | 476 | 15 | 14 | 6 | 28 | 1,317 | 30 |
| IMG | 268 | 268 | 30 | 19, and 10 on no face | 0 | 58 | 859 | 68 |
| IMA | 420 of 710 rows | 420 | 13 | 23 | 0 | 23 | 2,323 | 49 |
| IMB | 290 of 710 rows | 290 | 12 | 23 | 0 | 23 | 1,515 | 47 |

(RAN.) The rows of IMA and IMB are the 710 of the 733 shared Generic floors one face holds 0.9 of, a plot with no link of that key is listed as missing its link. No link was loaded in a read, so the position proof of R82 has 0 links. A wall goes to the plots whose generic floor holds the middle of its location line or has an edge within 0.3 m of it, a wall of a cut plot goes to none.

- The new IMC plan against plan_imc_past.json, leaf by leaf (RAN, round_addin\logs\p2b_plan_fields.py, p2b_fix_plan_fields_imc.txt and .json): 324 plots in both, 13 only in the old plan, the 12 cut plots 2311, 2312, 2375 to 2378, 2387 to 2390, 2544, 2545, which are the cut list of R120, and 2441 with no generic floor. Equal on 324 of 324: case, insert, rotation, mirror, unit, prototype file, link Name, the 9 parameters, steps, FFL and the generic, ramp and yard floors whole. Different: El @ Entry, the height that follows it, SSL only in the old plan (the key holds none) and modelPlot only in the new. The source of El @ Entry is keyParameter against pastJob and of the height computed against pastJob on 314 plots.
- El @ Entry of the key against the schedule (RAN, p2b_fix_imc_entry_gaps.txt): 10 plots hold 0 and are refused (2583 to 2588, 2595 to 2598). Of the 314 others 26 are equal, 175 within 1 mm (the parameter holds 3 decimals, the schedule 4), 13 over 1 mm up to 30 mm, of them 2623 at exactly 30 mm and 2561 11 mm over the schedule, and 100 over 30 mm, 0.0301 to 0.113 m under the schedule, 10th percentile 0.040, median 0.070, 90th percentile 0.080. Why is UNKNOWN (Q070).
- R129, plan_compare.py --against-plan on a copy in round_addin\plans_compare (RAN, p2b_fix_plan_compare_r129.txt): 324 plots, 320 compared, it leaves out 2374, 2391, 2543 and 2546, the plots of plotFrame.apart still in the plan. Its bands as they are: FFL, insert in plan and Z, mirror, prototype file, rotation, steps and unit equal on 320, the generic, ramp and yard outlines within 1 mm on 320 and their tops and corners within 5 mm, SSL missing on 320, El @ Entry equal 25, within 1 mm 172, worse than 10 mm 113, missing 10, the height equal 26, within 1 mm 171, worse 113, missing 10. In the first run R129 left out 15 plots of plotFrame.apart (2311, 2374 to 2378, 2387 to 2391, 2543 to 2546) and compared 320 of 335.
- What Part 2 needs for sheets, kept in each plan: the official number and the number of the model (modelPlot), the link origin in shared coordinates, the generic and yard outlines with arcs and the four ramp corners with their levels, the levels FFL, El @ Entry, height and steps (no SSL, the keys hold none), the link file, rotation, mirror, unit, CASE and the 9 parameters, and walls: id, type, length, height, base and top constraint and offset, location line, the ends of its location line and its plots (CHOSEN). The wall faces and width are not read (R17).

### R326, the join, the place check, the area codes and past_all.csv

- Join by area share (RAN, round_addin\join\key_join.json, logs\p2b_fix_key_join.txt, p2b_fix_join_lists.txt), IMC first:

| Key | Link plots | Joined, all on their own number | Cut | Other, by why | NBH, zone |
|---|---|---|---|---|---|
| IMC | 337 | 324 | 12, the cut list of R120 | noGenericFloor 2441 | 012, Zone 2 |
| IMD | 410 | 403 | 7 (1278, 1279, 1400 to 1402, 1486, 1487) | none | 013, Zone 2 |
| IME | 324 | 320 | 4 (2634, 2635, 2656, 2657) | none | 02, Zone 3 |
| IMF | 490 | 476 | 14 (702, 703, 859 to 864, 901 to 906) | none | 03, Zone 3 |
| IMG | 297 | 268 | 18 (476 to 484, 550, 551, 2954 to 2960) | noFace 431 to 440, 474 (440 since breaker 3) | 01A, Zone 4 |
| IMA | 432 | 420 | 12 (1588, 1589, 1611, 1612, 1764 to 1771) | none | 021, Zone 1 |
| IMB | 301 | 290 | 11 (1995 to 1997, 2006, 2007, 2265 to 2270) | none | 022, Zone 1 |

- 2,501 of the 2,591 link plots join, 55 fewer than the first run. Every plot the first run joined to another number (IME 2634, IMF 702, IMG 481, IMB 1995) and every plot it left on a face of no or two plan labels is cut now, and no joined plot has another number, no number holds two key plots (twoPlots 0) and no plot stands on two faces. IMG 440 touches face 206 by 0.000127 of its area, under keyPlans.minShare since breaker 3, so it is on no face, as the grid of R328 finds it. The zone and NBH of the location plan equal the area key of plots.csv on every joined plot.
- Place check (RAN, same file, p2b_fix_place_swaps.txt):

| Key | Links match, other | Generic floors match, cut | Ramps match, other |
|---|---|---|---|
| IMC | 335, 2 (2312 on 2311, one on a face of no label) | 324, 12 | 325, 12 cut |
| IMD | 410, 0 | 403, 7 | 402, 8 cut |
| IME | 322, 2 | 320, 4 | 320, 4 cut |
| IMF | 480, 10 | 476, 14 | 476, 8 cut and 6 noNumber |
| IMG | 281, 16 (11 noFace, 481 on 480) | 268, 18 and 11 noFace | 267, 19 cut and 11 noFace |
| IMA | 431, 1 (1769 on 1770) | 710 of the 733 shared, 23 | none |
| IMB | 296, 5 | the same 733 floors as IMA | none |

- No two plots of any key swap places (RAN). The 3 links of another number are all of cut plots.
- Area codes: the parsons table of config/area_codes.json written again from key_join.json by round_addin\logs\p2b_area_codes.py and checked by im_codes.load_table (RAN, p2b_fix_area_codes.txt): IMA 021 Zone 1 420, IMB 022 Zone 1 290, IMC 012 Zone 2 324 beside its schedule 337, IMD 013 Zone 2 403 beside its schedule 410, IME 02 Zone 3 320, IMF 03 Zone 3 476, IMG 01A Zone 4 268. Q011 stays closed, IME is NBH 02 of Zone 3 by its plots.
- past_all.csv in round_addin (RAN, p2b_fix_past_all.txt): 4,896 rows, native 2,395 (u11 394, u12 585, u13 783, u14 633), parsons 2,501 (imc 324, imd 403, ime 320, imf 476, img 268, ima 420, imb 290), by kind TU 1,791 and SE 710, the native rows of no kind. The native rows equal round_17\past_all.csv cell for cell on every column of that table, 2,395 of 2,395 in order, their kind and heightSource empty (RAN, p2b_fix_past_all_native_compare.txt). The 324 imc rows equal the Round 17 imc rows on every column of that table, the 13 others are the 12 cut plots and 2441 (RAN, p2b_fix_past_all_imc_compare.txt). El @ Entry comes from the schedule for imc 324 and imd 403, from the floor parameter for ime 320, imf 470, img 268, ima 420 and imb 290, and imf 6 are refused. heightSource is floorTop less floorParameter on every parsons row with a height of ime to imb, and schedule less schedule on imc and imd since breaker 3, which takes FFL, El @ Entry and the height as one schedule pair.
- The counts agree: on each key the join, the area row, the plan and past_all.csv hold the same plots (RAN, the second method below). In the first run IMG read 283 in the join and the area codes and 281 in its plan and past_all.csv.
- Majority run over the new past_all.csv (RAN, round_addin\majority\majority.json, logs\p2b_fix_majority.txt and p2b_fix_majority_kinds.txt, the override list to round_addin\majority, never config/overrides): the parsons decisions of ramp slope, El @ Entry, ramp front and fall, height, steps and yard take 1,785 to 1,791 TU rows and no SE row. Hand, generic floor and the foundation type and CASE tests keep the 710 SE rows, whose links hold those values. The rules themselves are for Phase 3a.

### R327, the spots at the plot entrance

- radiiM 0.5 m and 6.0 m and cornerM 0.05 m (CHOSEN in the first run, measured on IMC: the spots sit on the ramp corners and the corners of the entrance edge lie 5.0 m from its middle at the median, no spot lies at the middle). bandsM 0.001 and 0.005 m (CHOSEN, the rounding of R2 and majority.levelHoldMm).
- Per key (RAN, round_addin\spots\entry_spots.json, logs\p2b_fix_entry_spots.txt and p2b_fix_entry_spots_shares.txt), only the spots of the floors of the plot, the schedule by official number:

| Key, El @ Entry of | Plots | A spot within 0.5 m | A spot within 6 m | Within 1 mm, share | Within 5 mm, share | Corner pair: plots with both, mean within 1 mm, within 5 mm, share |
|---|---|---|---|---|---|---|
| IMC, key | 326 | 0 | 237 | 2, 0.006 | 12, 0.037 | 261, 29, 86, 0.330 |
| IMC, schedule | 324 | 0 | 236 | 2, 0.006 | 5, 0.015 | 260, 37, 153, 0.588 |
| IMD, key | 405 | 0 | 296 | 5, 0.012 | 15, 0.037 | 325, 56, 119, 0.366 |
| IMD, schedule | 403 | 0 | 292 | 2, 0.005 | 10, 0.025 | 323, 65, 216, 0.669 |
| IME, key | 324 | 0 | 213 | 7, 0.022 | 17, 0.052 | 249, 51, 90, 0.361 |
| IMF, key | 484 | 0 | 343 | 1, 0.002 | 9, 0.019 | 380, 37, 124, 0.326 |
| IMG, key | 296 | 0 | 215 | 5, 0.017 | 16, 0.054 | 237, 60, 84, 0.354 |

- Key values refused by the band: IMC 10, IMD 5, IMF 6. Spots on a host that is no floor of a plot: IMC 1, IMD 4, IME 4. IMG has one ramp of no entrance edge.
- IMA and IMB: no ramp and no spot value (822 and 587 spots on STR links), nothing to compare. So 5 keys were compared, not 7, and on none of the 5 does a spot at the plot entrance give El @ Entry: at most 0.054 of the plots have a spot of their own floors within 6 m and within 5 mm of it, and the mean of the two entrance corner spots holds within 5 mm on 0.326 to 0.669 of the plots with both. Q001 does not close.

### R328, the second method

- The first run sampled from the lists of the first method and tied links by the plot parameter, the review replaced both (item 9). The first run of the new check found one plot where the two joins differ that no sample held, so the join is now held on every plot as well.
- Rerun on all 7 keys (RAN, round_addin\check\key_plans_check.json, logs\p2b_fix_key_plans_check.txt), samples from every plot of the read:

Each cell gives the items compared at seed 14 and at seed 7, and every one of them matched.

| Key | Counts of join, area row, plan, past_all | Join on every plot, matched | Plans | Join | Place | Spots | past_all rows of the sampled plan plots | Spot counts on every plot |
|---|---|---|---|---|---|---|---|---|
| IMC | 324 each | 337 of 337 | 20, 20 | 20, 20 | 60, 60 | 20, 20 | 20, 20 | equal |
| IMD | 403 each | 410 of 410 | 20, 20 | 20, 20 | 60, 60 | 20, 20 | 20, 19 | equal |
| IME | 320 each | 324 of 324 | 20, 20 | 20, 20 | 60, 60 | 20, 20 | 20, 20 | equal |
| IMF | 476 each | 490 of 490 | 20, 20 | 20, 20 | 60, 60 | 20, 20 | 20, 19 | equal |
| IMG | 268 each | 297 of 297 | 20, 20 | 20, 20 | 60, 60 | 20, 20 | 19, 15 | equal |
| IMA | 420 each | 432 of 432 | 20, 20 | 20, 20 | 32, 35 | 20, 20 | 12, 14 | equal |
| IMB | 290 each | 301 of 301 | 20, 20 | 20, 20 | 28, 25 | 20, 20 | 8, 4 | equal |

- Every compared item matches. Before breaker 3 IMG 440 was cut by the first method on its touch of face 206 and noFace by the grid, keyPlans.minShare made both noFace. The past_all rows sampled are the sampled plots that are in the plan. The area row of each key equals the join counted by the area key of the official number.

### Known limit, recorded only

- Since PR 45 a read is tied to its key by the exact name <copy stem>_TEST-<job>, but the zone2 master of Revit All Zones and the IMC 2024 copy share one stem, so a read of a work copy made from the master passes as IMC by its name. Only a hash tie would part them. Q065, whose recommended answer replaces the zone2 file by a copy of the Zone 02\ARC original, removes the cause.

### Not done in this PR

- R329 is not used, the PR holds four changes and the review fixes stay inside them.
- CLAUDE.md was not changed by the helper: R325 to R328 and python/key_join.py, python/entry_spots.py and python/key_plans_check.py are for the main session to name there.
- The wall faces and width are not read, a wall goes to its plots by the middle of its location line. The footprint residual of R229 is in the imc rows only. SSL is in no key.
- No Revit window was opened.

### Tests first

- First run: most tests were run first and seen failing for the right reason. The R326 and R327 files failed at collection only, ModuleNotFoundError for key_join and entry_spots, then their real settings tests failed until the config held the new blocks (RAN, p2b_r325_first.txt to p2b_r328_first.txt).
- Review: each fix had its tests run first and seen failing, the outputs in p2b_fix_r325_first.txt, p2b_fix_r326_join_first.txt, p2b_fix_r326_past_all_first.txt, p2b_fix_r327_first.txt, p2b_fix_r327_band_first.txt and p2b_fix_r328_first.txt. The test of the real past_all.csv failed until the rerun wrote the kind column.

### Gate

- See the PR body, with the head it ran on.

### Questions

- Q011 stays closed, its answer now says 320 of the 324 IME plots join NBH 02 of Zone 3 by area share, the 4 others are cut.
- Q001 stays open: no entrance spot gives El @ Entry on the 5 TU keys compared, IMA and IMB have no ramp and no spot value.
- Q070 (new in 2b): the El @ Entry of IME, IMF, IMG, IMA and IMB comes only from the ELl@Entry parameter of the generic floor, which is off the IMC schedule by over 30 mm on 100 of the 314 IMC plots both hold, and 0 on 10 more, refused.
- No older question was shortened in this PR, PR 45 shortened them.

### Breaker pass 3

A third breaker pass read the PR at ebd69d2 and found 8 items. Each is fixed in a new commit, 8b7c004 to a47cdba, no amend, each with its test run first and seen failing (RAN, round_addin\logs\p2b_b3_item1_first.txt to p2b_b3_item8_first.txt, the head in the first line). The outputs that change were made again at a47cdba as files of this round, never over plan_imc_past.json, a read of 2a, round_17\past_all.csv or config/overrides. No Revit window was opened.

| Item | Rule | Change | Test |
|---|---|---|---|
| 1 | R326, R328 | A key with a schedule row takes FFL, El @ Entry and the height as one pair of the schedule, pastFfl and pastEntry of plots.csv, with heightSource schedule less schedule, never the key floor top with the schedule El @ Entry. A schedule row with El @ Entry and no FFL is refused. Each plot where the floor top of the key plan or its El @ Entry is off the pair by more than majority.levelHoldMm goes to past_all_conflicts.csv beside the table with both values. The second method checks FFL and the height as well | test_r326_a_key_with_a_schedule_takes_ffl_el_at_entry_and_the_height_as_one_pair..., test_r326_the_conflicts_list_is_written_beside_the_table..., test_r328_source_kind_el_at_entry... |
| 2 | R326 | Every kind is held against answerKeys.kinds as written there, case folded only when majority.kindFoldCase is true, every row of a group of majority.kindGroups (parsons) must carry one, in past_all.py before it writes and in majority.py when it reads, and majority.kindLeaveOut may name only kinds of answerKeys.kinds | test_r326_a_row_of_a_kind_group_with_no_kind..., test_r326_the_kind_of_an_answer_key_must_be_one_of..., test_r326_the_table_refuses_kept_parsons_rows..., test_r326_majority_refuses_a_kind... |
| 3 | R270 | entry_gaps.py holds the kinds as R326 and leaves out a row whose kind has entry in majority.kindLeaveOut, counted in leftOut | test_r270_a_row_of_a_kind_majority_kind_leave_out_keeps_out... |
| 4 | R325, R327, R328 | One flat top tolerance, keyPlans.flatTopM, in the plan, the spots and the second method, and the spots refuse a key value of a floor with no flat top as the plan does | test_r327_one_flat_top_tolerance..., test_r325_the_flat_top_of_a_generic_floor..., test_r325_the_flat_top_tolerance_must_be..., test_r328_the_flat_top_is_held... |
| 5 | R120, R325 | A generic floor whose face of cutShareMin holds no plan label or two is listed in cutPlots as noNumber or manyNumbers with that face | test_r325_a_generic_floor_whose_face_has_no_plan_label_or_two... |
| 6 | R325, R326, R328 | A share under keyPlans.minShare is left out by the plan, the join and the grid, so a floor that only touches a face is noFace by both methods | test_r326_a_generic_floor_that_only_touches..., test_r325_a_generic_floor_that_only_touches..., test_r325_the_minimum_share_must..., test_r328_a_grid_share_under_the_minimum_share... |
| 7 | R328 | The grid is laid in the axes of the longest edge of the floor and a floor with a share within 0.05 of cutShareMin by the coarse grid or the join of R326 is measured again on 200 by 200, one row 0.005 of the floor | test_r328_a_floor_whose_share_lies_near_cut_share_min... (0.89, 0.894, 0.906, 0.91 at 0 and 30.1 degrees), test_r328_a_floor_far_from_cut_share_min... |
| 8 | R326, R327 | key_join.py and entry_spots.py keep a key of the earlier file only when it holds the settings of this run, else the run is refused and the file left as it is | test_r326_each_key_of_the_join_keeps_the_inputs_and_the_share..., test_r327_each_key_keeps_the_settings... |

- Item 7 before the fix (RAN, p2b_b3_item7_first.txt): the 50 grid over the box read 0.894 as 0.900 and joined it, at 0 degrees. At 30.1 degrees the box grid read the four shares right, it is the edge parallel strip that a box grid can not resolve.
- Values: majority.levelHoldMm, 5 mm, is the band of the conflicts list, the band a level rule holds within (CHOSEN). keyPlans.flatTopM 0.0005 m (CHOSEN): every generic floor top of the 7 reads is exactly flat, its max less its min 0 on all 3,323 (RAN, scratchpad probe_b3.py). keyPlans.minShare 0.001 (CHOSEN, measured): the largest share of a floor that only touches a face is 0.000557, the ramp of IMG 440, its generic floor 0.000127, and no share of the 7 reads lies between 0.000557 and 0.0011 (RAN, scratchpad probe_b3b.py). majority.kindGroups parsons and majority.kindFoldCase false (CHOSEN). The refine band 0.05 and the resolution 0.005 are constants of the second method (CHOSEN, the coarse grid is off by up to 0.02).
- Plans (RAN, p2b_b3_plan_<code>.txt, p2b_b3_plan_compare.txt): the 7 plans written again, equal to the review plans on every plot and on the missing list. Only the IMG summary moved, DP4-440 from cut to noFace, cut 18 and noFace 11. Item 5 lists no floor on the real keys, no generic floor stands on a face of no or two plan labels.
- Join (RAN, p2b_b3_key_join.txt): IMG cut 18, noFace 11 (431 to 440, 474), its generic floors 268 match, 18 cut, 11 noFace, its ramps 267, 19 cut, 11 noFace. Every other count as in the review, 2,501 of 2,591 joined.
- past_all.csv (RAN, p2b_b3_past_all.txt, p2b_b3_past_all_compare.txt): 4,896 rows, the same keys and plots in order. Cells that moved: ffl and height of imd 1382 and 1436, the height 0.2527 to 0.0997 and 0.1499 to 0.0999, and heightSource of the 324 imc and 403 imd rows to schedule less schedule. The imc rows equal the Round 17 imc rows on every column of that table, 324 of 324, the native rows 2,395 of 2,395.
- Conflicts list (RAN, round_addin\past_all_conflicts.csv): 260 rows, imc ffl 0, imc entry 113, imd ffl 2 (1382, the schedule FFL 0.153 m under the floor top, 1436 0.050 m), imd entry 145. The key El @ Entry stands under the IMD schedule on all 145, 0.0099 to 0.0805 m, median 0.0495, and over 30 mm on 115 of the 398 plots that hold both. Why is UNKNOWN (Q070).
- Second way of item 1 (RAN, p2b_b3_conflicts_check.py and .txt, json and csv only): the same 0, 113, 2 and 145 plots, and all 324 imc and 403 imd rows hold the schedule pair.
- Spots (RAN, p2b_b3_entry_spots.txt): every count as in the review, the settings of each key now hold flatTopM.
- Second method (RAN, p2b_b3_key_plans_check.txt): every compared item matches on all 7 keys at seeds 14 and 7, the join of every plot 2,591 of 2,591 with IMG 297 of 297, and the past_all rows now with FFL and height. No floor of the join had a share within 0.05 of 0.9 by either method, so every one was measured on the 50 grid. The run took 42 s, 23 s before.
- Majority (RAN, p2b_b3_majority.txt, p2b_b3_majority_compare.txt, to round_addin\majority): parsons height misses 420 to 419 and steps 23 to 22, 1382 left both, parsons yard 0 to 2 misses, 1382 and 1436, whose yard top follows the key floor top while the FFL is now the schedule, the share 0.999 still strong. Overrides 7,597 to 7,595 in round_addin\majority\overrides_check.json. No SE row in the ramp, level, steps or yard decisions.
- El @ Entry gaps (RAN, p2b_b3_entry_gaps.txt to round_addin\gaps, p2b_b3_entry_gaps_before_fix.txt): on the new table 710 SE rows are left out and no pair holds ima or imb. The entry_gaps.py of ebd69d2 on the same table paired ima with u13 on 390 plots and imb with u14 on 289.
- Item 8 on a copy of the real join (RAN, p2b_b3_item8_real.txt): IMC alone at cutShareMin 0.8 exits with code 2 and names the 6 other keys, the hash of the copy unchanged.
- Hashes (RAN): 60 of 60 originals and 61 of 61 inputs equal before and after by sha256sum (originals_p2b_b3_before.txt, _after.txt, inputs_p2b_b3_before.txt, _after.txt), 60 of 60 by Get-FileHash (originals_p2b_b3_gethash.txt), plan_imc_past.json, round_17\past_all.csv and config/overrides/roshn-makkah.json equal (p2b_b3_protected_before.txt, _after.txt).
- The commands of the review were rebuilt and run at ebd69d2 first. Plans, join, spots, check and past_all.csv came out byte for byte equal to the review outputs (RAN, scratchpad), so the rerun runs what the review ran.
- Not done: the yard decision keeps FFL less the yard top, it does not take the key floor top on a plot with a schedule pair. Q070 holds the IMD numbers now. No new question.
