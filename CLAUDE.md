# Site Adaptation Tool

Data readers for the ROSHN Makkah site adaptation work, the placer and the Revit add-in.
The rules live in docs/rules/ (docs/rules/index.md finds a rule by id) and nowhere else. This file points to them and adds the working agreement.

## Roles

- Bader decides. Every open question goes to Bader by its fixed ID with the recommended answer first.
- Chat drafts the prompts and reads what Code did.
- Code does the work and checks it. Code never marks a thing done without a second check by a different method.

## Start of every session

1. Confirm the folder is the repo root, the branch is not main, and `git fetch` shows main level with origin. A detached HEAD equal to origin/main counts as level, for the Lane B worktree.
2. Read steps/PROGRESS.md, and in a phase session its open phase of steps/01_next.md. The top entry of steps/log.md only when PROGRESS leaves a point open.
3. Open a new branch for the round. Never commit to main. A hook blocks commits and pushes to main.

## Round ADDIN

Part 1 is the add-in. Part 2, the output sheets, waits for Bader's check of Part 1. Findings of 0a to 4e: steps/history/round-addin/, from 4f: steps/investigations/round-addin.md. Rules R300 to R399. Decisions: steps/decisions-2026-10-28.md, D1 to D19.

- Option B (Bader): the add-in does everything inside Revit 2024 in C#, .NET Framework 4.8. Python stays in the repo only as the test checker.
- Q050 (Bader): parsons2024Rev6 is the level profile of every new build, Rev 6 prevails. native2022 stays for reference and is never the default.
- Answer keys: the 7 Rev 6 Parsons site models in C:\02-Site Adaptation\Revit All Zones, IMA and IMB Zone 1, IMC and IMD Zone 2, IME and IMF Zone 3, IMG Zone 4, the main reference for every rule. The Native 1.1 to 1.4 reads are a second reference only, for plots none of the 7 hold, never for unit codes. Their Revit 2024 copies: answerKeys of config/project.json, R312, the IMC copy from the Zone 02\ARC original (Q065).
- dotnet/RevitPrototypeReader reads the answer keys, do not change it unless the round says so.
- The area key is the Neighbourhood_ID, never an IM letter.
- The approved mock is docs/addin-mock.md word for word, Bader's changes of docs/design/13-workflow-changes.md prevail. It sets the ribbon, the pane, Set up site, the check table and the two Excel files.

## Lanes (D10, D17, D18)

- Two lanes in one repo. Lane A runs in this folder, Lane B in a detached worktree at the sibling folder "Site Adaptation Tool Lane B WT". Each phase of steps/01_next.md names its lane and what it starts when.
- tools/run-lane.ps1 -Lane A or B runs a lane, one phase session after another, from steps/START.md (Lane A) or steps/START-B.md (Lane B). steps/STOP-A or steps/STOP-B stops it.
- A session ends with exactly one last line: PHASE DONE <id>, BLOCKED <reason>, QUESTION <ids> only when a question blocks all work of the phase, or LANE DONE.
- At most one open PR per lane, never two open PRs on one file. Lane B PR titles start with "Lane B", its branches hold "laptop" or "lane-b".
- Line 1 of steps/PROGRESS.md: "STATE RUNNING, <phase>, <date time>" from the first code PR of a phase, STATE WAITING from its last. On STATE RUNNING on origin/main the other lane leaves the shared records alone.

## How a phase runs

- One phase per session (Q096). The main session plans, orchestrates and merges. No approval stop: a PR merges when gh pr checks passes and the checkers found nothing open.
- At most 8 changes per PR. A phase aims for one code PR and one end records PR (D14). Two helpers may work at once in separate worktrees on separate files, one PR.
- Helpers (D16): developer (opus) for build logic, plan or Excel numbers and the override list. developer-light (sonnet) for UI, install, records, docs and tests-only PRs. runner for Revit runs, prober for one Revit fact. A helper at its turn cap reports and stops, what it did not reach is UNKNOWN.
- Reviews (D12): the breaker reads a PR only when it changes build logic, numbers in a plan or an Excel, or the override list, at most 2 passes (Q097). Every other PR gets the reviewer, with the UI checklist where a window changes, one pass. The claim-checker reads only the claims of the log entry of the end records PR.
- A REAL finding is fixed with a test, a LATENT one (no real input reaches it, Q095) goes to steps/latent.md. Parity and review on real inputs only. What is left after the passes becomes a question.
- Revit probe first (D11): before a test host run, probe each Revit API behaviour it needs that no run measured. A third attempt at a failed run is a probe of the cause, the phase stops only if that finds none.
- Any other fix on its third attempt stops, goes back to measuring and becomes a question.
- A new question takes the next free ID, Q148 now. A question that does not block goes to steps/questions-open.md and the phase carries on.
- Every fact in a report is tagged READ with a path, RAN with a count, SEARCHED with a link, CHOSEN or UNKNOWN.

## Records

- docs/repo-map.md the repo map, grep it for a path or a rule id. docs/design/ the add-in design, its README names each part.
- steps/PROGRESS.md the live state, steps/log.md the round log, steps/01_next.md the plan to finish Part 1, steps/lane-b.md the Lane B rules and join items.
- steps/questions-open.md, steps/questions-closed.md and steps/waiting-on-others.md the questions, steps/latent.md the findings no real input reaches, steps/history/ the old rounds.

## Reading budget

- At start: CLAUDE.md, steps/PROGRESS.md and the phase entry of steps/01_next.md. An agent reads what its file lists. The rest by grep or line range, never a whole file over 30 KB.
- The caps of R400, held by test_r400_record_sizes: CLAUDE.md at most 10 KB, steps/PROGRESS.md at most 60 lines and 6 KB with no line over 200 characters, the top entry of steps/log.md at most 4 KB, no file of docs/ or steps/ outside steps/history/ over 100 KB.

## Real files

- Real drawings, sheets and workbooks stay where they are. Readers run on them in place and never write there.
- No link, junction or symbolic link points into C:\02-Site Adaptation, C:\10- ROSHEN MECCA or any input root of config. Runs write only under the work root (D19). A run that reads real inputs records name, size, write time and SHA-256 of each file it opens there before and after, and fails on any change.
- Never copy a real file into git. sample_data/ and sample_output/ stay ignored. Never commit real coordinates or levels, logs hold counts, row numbers and distances only.
- Full run outputs go to sample_output/full_run/<file name>/. A file that fails is recorded with its exact error and the run carries on. Never swap in a dummy file.
- Code runs the reader, the placer and the add-in on copies through the test host. Bader runs them by hand only for his check of Part 1.

## Commands

```
py -3.12 -m venv .venv
.venv\Scripts\python -m pip install -r requirements.txt
.venv\Scripts\python -m pytest python
dotnet build dotnet\SiteAdaptation.sln -c Release
dotnet test dotnet\SiteAdaptation.sln -c Release --no-build
```

The gate is both test commands green. Run it locally before every pull request. If `py` is missing, call python.exe from the Python 3.12 install folder.

## Writing rules

These apply to every file, commit message, pull request text and report.

- No generated-by footer and no co-authored-by line. No emoji. No em dash.
- No semicolon in prose, comments, Markdown, JSON, YAML, Python or scripts. C# statements keep the semicolon the language needs.
- No comment that restates the line under it. A comment says why. Keep names and titles short.
- Say what happened. If a test failed or a step was skipped, say so with the output.

## Pull requests

- One branch per PR, type/short-topic, the title states the round and topic. Merge once gh pr checks <number> --watch shows python and dotnet passed, then delete the remote branch and pull main.

## Log shape

The top entry of steps/log.md fits on one page with the headings Goal, Done, Checked, Remains, Known bugs, Next, Your turn. steps/PROGRESS.md holds state, done, open bugs, Bader's turn and the next three steps.
Your turn and Bader's turn list only the questions opened or closed in the phase, by ID and title, and one line with the open count and steps/questions-open.md (D13). Until python/rule_status.py reads questions-open.md (4h-1), Bader's turn holds every open ID.

## Questions

- Each question has a fixed ID, Q001 upward, never renumbered or reused, and is named by its ID only. steps/questions-open.md holds its full text, recommended answer first, title at most 8 words.
- A closed question moves to steps/questions-closed.md with its answer and date, which also maps old numbers and the QL ids. An answer that waits for someone else closes with the interim answer and goes to steps/waiting-on-others.md.

## Majority rule

Bader's standing rule since Round 17, for every round.

- For every decision (ramp type, flat ramp, hand, front edge, levels, steps, yard, parameters) find the logic most plots of the past models follow and code it.
- Each plot the coded rule does not reproduce goes into config/overrides/roshn-makkah.json with the plot, the field, the past value, the source model and the reason if known.
- The tool applies the rule, then the override list. Bader edits the override list by hand.
- A rule is strong when it holds on at least majority.strongShare of config/project.json. If no logic reaches it, code the best one, mark it weak in Bader's turn and keep going.
- No project value in code. Rules and thresholds go in config and the rules profile.

## Checking work

- Check each output a second way, by another method than the reader. Report counts of matches and mismatches, open a few random items by id across sources, write every conflict to a conflicts list.

## Hooks

.claude/hooks: guard-main.sh stops commit and push on main, guard-registry.sh every registry change (R106), guard-inputs.sh links and writes into an input root (D19).
A hook only loads when a session starts in the repo. A session that started elsewhere has no hooks, say so in the log.
