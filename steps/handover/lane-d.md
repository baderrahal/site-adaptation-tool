# Handover, Lane D, 4 Nov 2026

Tags: RAN a command this session, READ a file on main at 13356fe, CHOSEN my opinion, UNKNOWN no proof found now.

## What I built

- Scanner, PR 114, 118, R420 to R427: dotnet/CadReader/CadScan.cs, ScanSources.cs, Common/Scan/ScanLogic.cs. READ docs/repo-map.md:44
- One parameter sheet, PR 126, R460 to R465: Common/SetUp/ParameterSheet.cs:42, ParameterBook.cs. READ docs/rules/index.md:468-473
- Test host design values, Links mode, PR 129, 134, R466 to R468. READ docs/rules/index.md:474-476
- Each zone against its key, PR 137, 138, R469. READ docs/rules/index.md:477, steps/log.md:948, 984
- Road surfaces in order, PR 145, R490 to R493: Common/Plan/DesignBasis.cs:221, 240. READ docs/rules/index.md:478-481
- Mass base as a wall ring, PR 153, R500 to R504: Common/PlaceVillas/WallRing.cs:59, WallRingBuilder.cs:241. READ docs/rules/index.md:482-486
- Sheet shown, parameters on links only, PR 165, R506 to R508. READ docs/rules/index.md:487-489
- Mass cut at party line, PR 169, R530. READ docs/rules/13-parameters-r460-up.md:37
- Road toposolid, PR 180, R570 to R575: Common/Floors/RoadTopography.cs:113, FloorsRun.cs:220, 284, Flow/KnownWarnings.cs:83, config/project.json:202. READ docs/rules/index.md:491-496
- All PRs named above and PR 181 merged. RAN gh pr view on each

## Half done

- No branch, no open PR. RAN gh pr list --state open gives 0. Worktrees Lane D TOPO and agent-ad19500003cae4104, agent-afa7793be2038fb39, agent-ad2cf97c56dab7c70, agent-afbf0e7d49fc5c62f: 0 dirty, 0 ahead of origin/main. RAN git status, git log
- Lock free, no Revit process. RAN window_lock.py --status gives "lock": false, processes empty, Get-Process Revit empty
- The tick of R574 is in the xaml (AddIn/Pane/SitePanePage.xaml:227, Lane C PR 183). Seen in Revit: UNKNOWN
- A press of the merged TOPO code on SA-dryrun3nov-done.rvt with its floors is not run, it stops at step 5. READ gh pr view 180, "A press of the merged code"
- Id blocks left free: R576 to R579, Q478, Q479. READ steps/questions-open.md has Q470 to Q477, docs/rules/index.md:496 ends at R575. CHOSEN to name them, no block list found in the repo

## Known bugs and traps

- A model with floors built before RAMP stops at step 5, every outline reads changed. See it: press 5 on SA-dryrun3nov-done.rvt. Proof: FloorsRun.cs:471-476, Q439 steps/questions-open.md:793
- Test a floor press on C:\SA-demo\SA-dryrun3nov.rvt, same model with no floors. READ steps/log.md, TOPO entry at line 53, Checked
- Road edges miss the 5 mm target: 263 of 1,008 points on NBH 012, 80 plots at corners all with override. See it: toposolid_012.json in the job output. READ gh pr view 180, Q472 steps/questions-open.md:797, Q475 line 809
- The solid is a tall block from Level 1 and spreads outside the NBH by the hull. READ Q474 line 805, Q473 line 801
- A real press shows only a count on the row, the toposolid lines reach no window or file. Read toposolid_<NBH>.json. READ Q476 line 813
- DeleteWarning quiets the box only, Revit lists the 783 again after commit. READ AddIn/Revit/WarningCollector.cs:38, Q476 line 813
- A plot list press resets steps 3 to 9, the scope key changed. READ Common/Steps/StepStates.cs:159, Q477 line 817
- Masses overlap or lose walls on plots outside NBH 012 and no run line names them. READ Q470 line 741, Q471 line 745
- Four config values read UNKNOWN in the R401 block, the mark in the note does not reach them. READ rules/roshn-makkah.json:757-760
- A new config value needs python/rule_status.py --write or the gate fails. A new rule goes after the last in its rule file or the index test fails. READ gh pr view 180, Gate, python/rule_status.py:9
- C:\SA-demo\config\project.json is outside git and holds floors.roadTopography, rampJoint, plotPull. Its hash change reruns step rows. A plain copy of the repo config breaks inputRoot. Backups in round_addin\logs\p_topo\backup_config, backup_config2. READ gh pr view 180, Writes outside the repo. inputRoot break: UNKNOWN in the repo, from my notes
- Parity of the past job: p_mass\par\parity.sh runs rev6 only unless read_roshn is present, use p_mass\roshn_par.sh. Both files exist, RAN find. The behaviour: UNKNOWN in the repo, from my notes
- Unset SITE_ADAPTATION_INPUT before a gate. The variable is read at python/input_guard.py:17. That it breaks a gate: UNKNOWN in the repo, from my notes
- gh pr merge --delete-branch once cost a worktree its git metadata. UNKNOWN in the repo, from my notes
- Do not run git worktree prune without Bader. Bader's order this round, no line in the repo: UNKNOWN

## What I wish I had known at the start

- A match with the past job is no proof of the tool. READ steps/truth-audit.md
- Another lane's merge can change the model a round was told to test on, RAMP did that to TOPO. READ gh pr view 180, "The second copy is not the model the round named"
- Toposolid.Create raises one overlap warning per floor, 783 on NBH 012, found only in Revit, no offline test could show it. READ gh pr view 180, Limits
- A round needs the size of the input measured first: 95,177 surface points against a limit of 10,000. READ gh pr view 180, Other results

## What is missing

- A run line or file for what a real press did, Q476, and for each mass that does not fit, Q470. READ lines 813, 741
- A warning count in the check table that says which warnings were taken. CHOSEN, nothing found by grep of Q476 beyond the 783
- A rule that a round's test model is rechecked against the config of main before the round starts. CHOSEN, from the Q439 case
- Cleanup of worktrees: RAN git worktree list gives 55 rows, most of old helpers. No owner for removal
- A shorter way for Bader to answer: RAN grep gives 197 questions in steps/questions-open.md. CHOSEN

## Lessons

1. Prove in Revit early. R575 and the 52 s box came only from the Revit check, after the offline gate was green. READ gh pr view 180, table
2. Measure with a second method that shares no code. My own walk over Revit's triangles agreed at most 0.05 mm with the report and showed the misses were the input, not the tool. READ steps/log.md, TOPO entry, Checked
3. Write the limit in the PR when the check differs from the order. The named model did not run and the PR says so. READ gh pr view 180
4. A fix found late gets less checking, say it. R575 had no second review. READ steps/log.md:65, gh pr view 180, Gate
5. Keep job state outside the repo in one folder per round, the Revit copies and backups stayed findable. RAN ls round_addin\logs\p_topo

## Questions only I know about, recommended answer first

- Remove the 5 unused worktrees named under Half done? Yes, by Bader's own command, they hold nothing. RAN above
- Fix the four UNKNOWN values of R401 by giving each its own CHOSEN sentence in floors.roadTopography.note? Yes, in the next round that touches the note, Q473 can carry them. READ rules/roshn-makkah.json:757-760
- Run TOPO on the named model once Q439 is answered? Yes, one press on a copy of SA-dryrun3nov-done.rvt, it is the only press with old floors and new code. READ gh pr view 180
- Second review of R575? Yes, reviewer alone, it writes into a model and has had none. READ steps/log.md:65
- Keep the p_topo copies, the topo_check backups? Delete after Q439, they are copies only. RAN ls, total size UNKNOWN

Open by Bader for this lane: Q390, Q392, Q393, Q470 to Q477, steps/questions-open.md.
