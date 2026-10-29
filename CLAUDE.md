# Site Adaptation Tool

Data readers for the ROSHN Makkah site adaptation work, and the placer, which reads plan.json and places the villa links.
The rules live in docs/rules/ (docs/rules/index.md finds a rule by id) and nowhere else. This file points to them and adds the working agreement.

## Roles

- Bader decides. Every open question goes to Bader by its fixed ID with the recommended answer first.
- Chat drafts the prompts and reads what Code did.
- Code does the work and checks it. Code never marks a thing done without a second check by a different method.

## Start of every session

1. Confirm the folder is the repo root, the branch is not main, and `git fetch` shows main level with origin.
2. Read steps/PROGRESS.md, and in a phase session its open phase of steps/01_next.md. The top entry of steps/log.md only when PROGRESS leaves a point open.
3. Open a new branch for the round. Never commit to main. A hook blocks commits and pushes to main.

## Round ADDIN

Part 1 is the add-in. Part 2, the output sheets, waits for Bader's check of Part 1. The plan and the findings of 0a to 4e are in steps/history/round-addin/, those from 4f in steps/investigations/round-addin.md. Rule numbers R300 to R399.

- Option B (Bader): the add-in does everything inside Revit 2024 in C#, .NET Framework 4.8. No Python on the user's PC. Python stays in the repo only as the test checker.
- Q050 (Bader): parsons2024Rev6 is the level profile of every new build, Rev 6 prevails. native2022 stays for reference and is never the default.
- Answer keys: the 7 Rev 6 Parsons site models in C:\02-Site Adaptation\Revit All Zones, IMA and IMB Zone 1, IMC and IMD Zone 2, IME and IMF Zone 3, IMG Zone 4. They are the main reference for every rule. The Native 1.1 to 1.4 reads are a second reference only, for plots none of the 7 hold, and never for unit codes. Their Revit 2024 copies sit in Revit All Zones\2024, listed in answerKeys of config/project.json and checked or made by python/key_copies.py against key_copies.json beside them (R312). The IMC copy comes from the Zone 02\ARC original (Q065).
- dotnet/RevitPrototypeReader reads the answer keys, do not change it unless the round says so.
- The area key is the Neighbourhood_ID, never an IM letter. The Parsons and the Native job use the IM letters for other areas.
- The approved mock lives word for word in docs/addin-mock.md. It sets the content and the order of the ribbon, the dockable pane, the Set up site dialog, the check table and the two Excel files.

How the round runs:

- One phase per session (Q096). Bader pastes steps/START.md into a fresh session, which runs the next open phase of steps/01_next.md. At the end of the phase it merges, rewrites steps/PROGRESS.md and stops.
- The main session plans, orchestrates and merges. One developer helper per PR.
- No approval stop: a PR is merged when gh pr checks passes and the checkers found nothing open.
- The breaker and the claim-checker read each PR before it merges. Each REAL finding is fixed with a test.
- Parity and review on real inputs only (Q095). The reviewer and the breaker mark each finding REAL, a real input reaches it, or LATENT. A LATENT finding goes to steps/latent.md with no fix.
- At most 2 review passes per PR (Q097). What is left becomes a new question in steps/questions-open.md and the work carries on.
- Never two PRs open. At most four changes per PR.
- A fix on its third attempt stops, goes back to measuring and becomes a question.
- Line 1 of steps/PROGRESS.md is the state of the records (Bader, 2026-10-28). A session sets it to "STATE RUNNING, <phase>, <date time>" in the first PR of its phase and back to a line that starts with STATE WAITING in its last. Lane B (steps/lane-b.md) reads it on origin/main after git fetch and on STATE RUNNING leaves the shared records alone.
- A new question takes the next free ID, Q126 now.
- Every fact in a report is tagged READ with a path, RAN with a count, SEARCHED with a link, CHOSEN or UNKNOWN.
- .claude/agents holds developer, reviewer and breaker on opus, prober, claim-checker and log-reader on sonnet, runner on haiku. .claude/skills/ai-max holds the AI MAX skill, version 4.

## Records

- docs/repo-map.md: the repo map and the rules in short, moved from here in Round REC. Grep it for a path or a rule id.
- docs/rules/ holds the rules, docs/rules/index.md names the part of each id. docs/design/ holds the add-in design, its README names each part. docs/addin-mock.md holds the approved mock.
- steps/PROGRESS.md holds the live state, steps/log.md the round log, steps/01_next.md the plan to finish Part 1 and steps/START.md the prompt of a phase session.
- steps/questions-open.md and steps/questions-closed.md hold the questions, steps/latent.md the findings no real input reaches, steps/history/ the old rounds.

## Reading budget

- At start a session reads CLAUDE.md and steps/PROGRESS.md, and for a phase its entry of steps/01_next.md. An agent reads what its file in .claude/agents lists.
- Everything else is read by grep or a line range: a rule by its id through docs/rules/index.md, a question by its id in steps/questions-open.md, the docs/design/ part of the step.
- Never read a whole file over 30 KB.
- The caps of R400, held by test_r400_record_sizes: CLAUDE.md at most 10 KB, steps/PROGRESS.md at most 60 lines and 6 KB with no line over 200 characters, the top entry of steps/log.md at most 4 KB, and no file of docs/ or steps/ outside steps/history/ over 100 KB.

## Real files

- Real drawings, sheets and workbooks stay where they are. Readers run on them in place.
- Never copy a real file into git. sample_data/ and sample_output/ are ignored, keep them ignored.
- Never commit real coordinates or levels. Logs and reports hold counts, row numbers and distances only.
- Full run outputs go to sample_output/full_run/<file name>/.
- A file that fails or times out is recorded with its exact error and the run carries on. Never swap in a dummy file.
- Code runs the reader, the placer and the add-in on copies through the test host, Bader runs them by hand only for his check of Part 1. The placer runs only on a copy of a model.

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
- Merge only after gh pr checks <number> --watch shows the python and the dotnet check both passed, never while one is red or still running.
- After a merge, delete the branch on the remote and pull main.

## Log shape

The top entry of steps/log.md fits on one page and has these headings in this order.

1. Goal
2. Done
3. Checked
4. Remains
5. Known bugs
6. Next
7. Your turn

Your turn lists every open question by fixed ID and title, as Bader's turn of steps/PROGRESS.md does. The full text is in steps/questions-open.md.
steps/PROGRESS.md is 60 lines at most and holds state, done, open bugs, Bader's turn and the next three steps.

## Questions

- Every question for Bader has a fixed ID, Q001 upward. Never renumber an ID and never reuse one. A new question takes the next free ID.
- steps/questions-open.md holds the full text of every open question with its recommended answer first. Bader's turn in steps/PROGRESS.md and Your turn in the top entry of steps/log.md list them by ID and a title of at most 8 words, the same in both.
- A closed question moves to steps/questions-closed.md with its answer and the date, and keeps its ID. That file also maps old round numbers to IDs.
- Steps files and reports name a question by its ID, never by a list position.

## Majority rule

Bader's standing rule since Round 17, for every round.

- For every decision (ramp type, flat ramp, hand, front edge, levels, steps, yard, parameters) find the logic most plots of the past models follow and code it.
- Do not stop on odd plots. Each plot the coded rule does not reproduce goes into config/overrides/roshn-makkah.json with the plot, the field, the value the past job used, the source model and the reason if known.
- The tool applies the rule, then the override list. Bader edits the override list by hand.
- A rule is strong when it holds on at least the share set in config (majority.strongShare in config/project.json, 0.90 to start). If no logic reaches it, code the best one, mark it weak in Bader's turn and keep going.
- No project value in code. Rules and thresholds go in config and the rules profile.

## Checking work

- Check each output a second way, with a different tool or method than the reader uses.
- Report counts of matches and mismatches.
- Open a handful of random items by id and compare them across sources.
- Never resolve a conflict silently. Write it to a conflicts list.

## Hooks

.claude/settings.json holds a hook that stops git commit on main and git push to main.
.claude/hooks/guard-registry.sh blocks every registry change (R106). Nothing in the registry is read or written.
A hook only loads when a session starts in the repo. A session that started elsewhere has no hooks, say so in the log.
