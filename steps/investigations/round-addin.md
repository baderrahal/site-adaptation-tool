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
- Tests: 9 in test_round_addin.py and the 2 R277 tests of test_round17.py, the one that said there is no default now tests a profile with no default (RAN, 11 passed).

### R300, Q045: villa blocks from the Building Footprint

- SEARCHED the repo (every .py, .json, .cs and .ps1) for R06_0000 and R06_0001: no reader takes them for villa blocks. drawings.villaBlocks is Makkah Building Footprint.dwg since round 12b, and the data book wires footprintBlock from the role buildingFootprint (READ, config/project.json, python/c3d_probe.json). R06_0000 is read as reference only: the r06_0000Block and r06_0000Number columns of the data book (dataBook.join.masterBlocks) and the R06 to R06_0000 compare of R176 in wiring.py. Those stay.
- One stale text: the skip reason of R06_0001 said R06_0000 is read for the villa blocks. Corrected.
- New: drawings.referenceOnly lists both copies. project_profile.refuse_reference_only refuses a drawing or a blocks.json of that list in cad_analysis.read_blocks (so block_scan, block_offsets, block_proof, round13_check), footprint_check.footprint_blocks (footprint_check, plan_from_book and majority --footprint-blocks), the analysis stage of full_run and the footprint role of data_book and wiring. 3 tests (RAN, 3 passed). The dotnet code reads no R06 drawing (SEARCHED).

### R301, Q044: config/area_codes.json by job

- One table parsons and one native. native from the Round 17 coverage of R267: IMA 1.1 with 02 (Zone 3) 309 and 01A (Zone 4) 84, IMB 1.2 with 03 431 and 01A 150, IMC 1.3 with 021 399 and 013 382, IMD 1.4 with 012 336 and 022 294 (READ, steps/investigations/round-17.md). parsons holds the IMC key 337 in 012 (model), the IMC schedule 337 in 012 and the IMD schedule 410 in 013, marked schedule (READ, data-book.md lines 267 and 268).
- Gone: the guessed rows IMA 021, IMB 022, IME 02, IMF 03, and the IMG row, which held text only and no count. Phase 2 fills parsons from the 7 models.
- im_codes.py: load_table refuses a row with a status, no positive count, no kind of model or schedule, no file, no measured note, an IM code twice in a job, or a job missing. Each source names its job in dataBook.data5.imCodes.sources and is held against the row of its own job: agrees, differs, noRow or noJob. R257 points to R301. 16 tests of R301 and 2 of R257 (RAN, all passed).
- Second method: the 8 native rows parsed again from the coverage table text of round-17.md by a regular expression, 8 of 8 equal, 0 on one side only, and the of-count in each measured note right on 8 of 8 (RAN).

### R302, Q061: the image step limit and the NBH 03 image

- testHost.imageStepMinutes 30 in config/project.json. Run-TestHost.ps1 writes it into an image job as imageMinutes, gives the job a total of 27 minutes plus it and refuses -TimeoutMinutes under it plus 30. The guard refuses an image job with no imageMinutes or one outside 0 to MaxImageMinutes (120, a bound against a mistyped job), and imageMinutes on any other job, and lets an image job total run to 29 minutes plus its image minutes. The image step takes TestHostGuard.ImageStepMinutes(job), the constant 10 is gone. 8 .NET tests in RoundAddinTests.cs and 1 Python test (RAN).
- Build and install: dotnet build 0 errors and 1 old warning, Install-TestHost.ps1 -NoBuild -SkipSigningReport under PowerShell 7.6.7, the three manifests kept, Common the same in the reader, the placer, the watcher and the build (RAN). The script skips the CodeSigning read with that switch (READ, Install-TestHost.ps1 lines 88 to 91).
- Windows, one at a time under the lock of R200 with round ADDIN-0b, Revit 2024.3. The watcher answered 5 boxes in each in our own Revit only, Do Not Load for RCRC Green and RCRC Green Audit, Load Once for our three (RAN, round_17\logs\windows.jsonl and run_I2x_image.txt):

| Code | Area of NBH 03 | Free GB at the lock | Open s | Image s | Result | Peak MB |
|---|---|---|---|---|---|---|
| I20 | whole, 735 by 733 m | 5.46 | 84.3 | 1,805.2 | timeout at 30 minutes | 1,692 |
| I21 | south west quarter | 8.12 | 84.1 | 55.3 | ok, 128 links and 232 floors in the view | 1,995 |
| I22 | south east quarter | 7.84 | 74.8 | 53.7 | ok, 118 links, 217 floors | 1,810 |
| I23 | north west quarter | 8.92 | 76.3 | 56.4 | ok, 131 links, 241 floors | 1,995 |
| I24 | north east quarter | 8.80 | 77.3 | 51.9 | ok, 102 links, 182 floors | 1,816 |

- A first I20 start stopped at reading its spec, a JSON escape fault in the spec file I wrote, before any Revit started (RAN, run_I20_image_specfault.txt). It is not counted as an image try.
- The whole area failed once at 30 minutes, with no dialog in Revit and no other window seen by the watcher (READ, testhost_I20_watcher.json). Each quarter takes under a minute, all four under 4 minutes, so the whole area is not slow by its size, and why it hangs is UNKNOWN (Q063). The quarters were CHOSEN as the second try, since a repeat of a run that hit its limit tests nothing new. The quarter views count 479 links and 872 floors, over the 404 and 808 of the file, since plots on a quarter edge show twice (RAN). Opened I21 and I23: houses, floors and grid tags drawn, cut at the quarter edge. The export fits a wider extent than the crop (two symbols far to the right in I21), so in I21 the houses fill a third of the width (READ, the PNG files).
- The NBH 03 file was unchanged by hash after every run (RAN, Unchanged True in all 5 run logs).

### Q047, Q048: the IFC copy and the lock files

- Deleted data_book\work\Downloads\1TB02012-012D03-UNA-00-XX-MOD-UT-00010.ifc, 10,497,165 bytes, equal by SHA-256 and size to Downloads\New folder\1TB02012-012D03-UNA-00-XX-MOD-UT-00010.ifc, which stays (RAN). The empty Downloads folder of data_book\work stays. dataBook.data5.ifc.copy still names the copy, so a run of ifc_header.py needs the copy made again.
- Deleted Makkah Phase 1 Cutsheet - 500.dwl (61 bytes) and .dwl2 (211 bytes) beside the Cutsheet copy in data_book\work\Base Plans: AutoCAD lock records of user bader on this machine, 24 Oct 17:57, no AutoCAD process running (READ, RAN).

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
- dataBook.surfaceEntry.key.settings of python/c3d_probe.json pointed at round_z4\work\project_z4_past.json, now at kept\round_z4, and the path resolves and loads (RAN). siteBuild of config/project.json still names round_17 and its newFile, which now sits in kept\round_17. Phase 1 sets siteBuild again.

### Q049: attribute values of the probe

- DataCommands.cs line 198 of the probe reads an attribute reference only when it is not Invisible, and only such an attribute reaches the attr_ columns of blocks.csv (lines 200, 201, 475 to 483) and an ATTRIB row of texts.csv (line 202) (READ). The line dates from 601129f, 24 Oct 18:02, and the Road Layout tables were written at 18:45 that day (READ, git blame and the file times).
- The Road Layout tables of the probe hold 0 ATTRIB rows and 0 attr_ columns, while the probe met all 824 top and 5,045 nested inserts (RAN, and DATA-5). So the 879 vehicle tracking values are left out already, no code changed.
- The DATA-5 line that the probe writes no attribute value holds for Road Layout only: the probe table of the Rev 9 drawing MP-SK-01-03-006_R9.0 holds 2,139 ATTRIB rows and 16 attr_ columns, room names, finish numbers and levels of the bound building footprint, and rev9_drawing.survey (R189) reads every text row (RAN, READ). The 11 CadReader tables of the data book hold none (RAN). Q062 asks whether those stay.

### The remote branch feat/revit-links-walls-ramps

- merge-base --is-ancestor of its tip 5fbf27a with origin/main: no, 1 commit not in main (RAN). It was squash merged as PR 4, 0a0d4c3, which is in main and holds the same tree as the tip, an empty diff (RAN). File by file: 12 files, 2 equal main, 10 changed later on main, 0 neither (RAN). Deleted with git push origin --delete, git branch -r shows origin/main only (RAN).
- It was deleted before the image existed, where the prompt put it after. It touches nothing the image needs.

### Gate

- Python 1024 passed and 1 skipped, .NET build 0 errors and 1 warning (xUnit2013, older than this round), .NET 429 passed (RAN).

### New questions

- Q062: the Rev 9 attribute values of the probe.
- Q063: the whole NBH 03 image runs past 30 minutes while each quarter takes under a minute.
