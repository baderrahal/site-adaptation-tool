# Round ADDIN, 2026-10-25, Part 1, the add-in for Revit 2024

Counts and distances only. This file holds the plan of the round and gathers its findings, phase by phase.

## Plan

Written 2026-10-25 before the first edit. The round prompt, word for word, is round-addin-prompt.md in the scratchpad of the main session, outside the repo. Part 2 (sheets) is not started in this round.

### Preflight, done before this plan

- Repo root C:\Users\bader\Documents\GitHub\site-adaptation-tool, branch main, main equal to origin/main at ff212e5 after git fetch (RAN).
- Hooks live: guard-main.sh refused a dry run commit on main and guard-registry.sh refused a harmless echo with the word reg (RAN, 2 of 2 refused).
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
7. Close Q035, Q044 (structure), Q045, Q046, Q047, Q048, Q049, Q050, Q060, Q061 in steps/questions-closed.md with answers.

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
- plan_<code>_past.json per model, plan_imc_past.json never overwritten, compared.
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
