# Site Adaptation Tool

A Revit 2024 add-in, C# on .NET Framework 4.8, that takes a project's site drawings, schedules and prototypes and builds the site model: levels, floors, ramps, villa links and walls, then checks it and reports. Python stays in the repo as the test checker only. The rules live in docs/rules/ and nowhere else, docs/rules/index.md finds a rule by id.

## Fast build, 6 to 8 Nov

Bader's order of 6 Nov, it overrides the lines below until it is deleted after 8 Nov.
- One reviewer pass per PR, fix REAL only, merge on green within the hour. No breaker, second review, S1, hold-out, parity or scoring per PR, no second check of the task skill. Job tests stays required.
- No records PR. Each PR carries its own top entry of steps/log.md under the seven headings.
- A task may ship in several PRs, one card or one function each, pushed at least every 2 hours. Its last PR sets done.
- No design work, the design is the 3-stage sketch of 4 Nov and the task text. Live plan steps/plan-fast-3days.md, asks between lanes steps/seam.md.

## The 3 stages and the rules

| Stage | Buttons | Result |
|---|---|---|
| 1 Project data | 1 Set up site, 2 Read data, 3 Review data | a reviewed plot table |
| 2 Build site | 4 Check, 5 Floors and ramps, 6 Levels, 7 Place villas, 8 Walls | the built model |
| 3 Review and deliver | 9 Report, Update from report | the Excel files and the delivered model |

Rules per project: each project holds its own rule set, chosen and edited in the add-in. A round names stage 1, 2, 3 or rules. The button map is CHOSEN by Code from the pane titles, Bader corrects it.

## Roles and lanes

Bader decides. Design is decided in chat and Code builds only what is approved. The role comes from the folder, never from a setting:

| Folder | Role | Does |
|---|---|---|
| main repo folder | lead | writes tasks and acceptance tests, merges setup, owns tests/acceptance, golden, holdout |
| worktree named revit-test | revit tester | the only lane that opens Revit |
| worktree named build-1 or build-2 | builder | one task at a time |
| anything else | builder | needs a task whose lane is that folder name |

Trust an answer only if it names proof you can check in the repo.

## How to work

- One task per fresh session. Round: /round. Task: /task. Then /merge, /report. Whole repo: /audit, /questions.
- Plan first, then continue without waiting.
- Claims need proof as a file and line or command output. Write UNKNOWN instead of guessing.
- Never mark a thing done without a second check by a different method.
- Never commit to main, a hook blocks it. Merge a green PR at once, never two waiting.
- Per PR, S1 and the hold-out. All five scenarios once at the end of a round, before its check. One review pass for a small change, a second only for logic, Excel numbers, overrides or Revit model writes. Run scripts/status.ps1 -Progress after each merge.
- Use `git push origin --delete` for a merged branch, never gh with --delete-branch.
- New question: next free Q id after checking origin/main, recommended answer first, in steps/questions-open.md. That file is near the 100 KB cap of R400, move or shorten text first.
- A fix on its third attempt stops and goes back to measuring.
- Push your branch every 15 minutes while working. Never leave two PRs waiting.
- Any steps for Bader are numbered, one action each, in the VS Code order: the folder, then Terminal then New Terminal, then each command on its own line. Long steps go in a file under steps/.
- The repo is public. Every review also checks the diff holds no real coordinate, level, plot data, client file contents, or path with a client name beyond what config already names.
- steps/PROGRESS.md and steps/STATUS.md are written by scripts/status.ps1, never by hand.
- The scenario and hold-out answers are read by the scoring scripts alone, a hook blocks every lane.
- Lead starts a lane with scripts/lanes/run_lane.py, a fresh session in the lane's folder. A message cannot clear a session.
- A PR body starts with the exact line `Task: <id>` or `Lead: <reason>`, nothing after the id. Every new function goes under Callers, private ones too.

Traps moved here from memory, 5 Nov 2026:

- Run gh merges from the lane's own folder, never chain gh pr merge after a watch, delete the remote branch on its own.
- Never run git worktree prune without Bader. Never build a lane's folder while a runner runs from it, a worker run takes a copy of its build.
- A question a config value cites stays open until Bader answers, or R401 fails. A new config value needs python/rule_status.py --write.
- A new Python file naming the mode rev6 must join the modes list of test_r62.
- TaskStop does not end the children of a background shell. Stop by your own process id, never a whole tree.
- A commit of a run that may stall takes `git -c gc.auto=0`. A status script stays under the tool's time limit, and is never piped into head.
- A person may be at this PC. A Revit driver sends a click, a key or a shot only with Revit in front, after 5 minutes idle and with no meeting window, and stops when another input comes. On 5 Nov one click landed in a live meeting.
- guard-main judges a command before it runs. Split a branch switch and a commit into two calls.

## The tool must work for any project

- No ROSHN data in code. Project values go in config and the rules profile. Known debt, the audit found past values in the shipped set, steps/backlog.md holds it.
- No hand editing of JSON by users, every setting has a window.
- Every value carries a source tag: computed, rule, user pick, missing, and past only in past job mode. A value tagged override or past needs an entry in docs/signoff.md.
- Rule ids are R###, question ids are Q###. Never renumbered or reused.
- A Rev 06 level profile (parsons2024Rev6, Q050) is the default of every new build. native2022 stays for reference.

## Past job mode

- The 7 Rev 6 site models and the override list belong to the past job. They are for compare, never a source for a new project (steps/truth-audit.md).
- Majority rule, Bader's standing rule since Round 17, for past job mode only until Bader says otherwise: code the logic most past plots follow, put each plot it misses in config/overrides/roshn-makkah.json, mark a rule weak under majority.strongShare. CHOSEN by Code: it no longer rules a new project, by the lessons below. Full text: steps/history/CLAUDE-until-setup-lanes.md.
- The area key is the Neighbourhood_ID, never an IM letter.

## Writing

Plain English. No em-dashes, no semicolons, no emoji, in every file, commit and PR, C# statements keep theirs. Do not use the words pipeline, backend, deploy, stack, schema. No generated-by footer and no co-authored-by line. A comment says why, never what the next line does. Say what happened, with the output if a step failed or was skipped.

## Real files

- Real drawings, sheets and workbooks stay where they are. Readers run on them in place and never write there.
- No link or junction into C:\02-Site Adaptation, C:\10- ROSHEN MECCA or an input root of config. Runs write only under the work root. A run that reads real inputs records name, size, write time and SHA-256 of each file before and after, and fails on any change.
- Never copy a real file into git, this repo is public. Never commit real coordinates or levels, logs hold counts, row numbers and distances only. sample_data/ and sample_output/ stay ignored.
- A file that fails or times out is recorded with its exact error and the run carries on. Never swap in a dummy file.
- Run the placer and add-in only on copies of a model. Never read or write the registry.

## Commands

```
py -3.12 -m venv .venv
.venv\Scripts\python -m pip install -r requirements.txt
.venv\Scripts\python -m pytest python
dotnet build dotnet\SiteAdaptation.sln -c Release
dotnet test dotnet\SiteAdaptation.sln -c Release --no-build
pwsh scripts\quick-tests.ps1
pwsh scripts\status.ps1
```

Unset SITE_ADAPTATION_INPUT before running these. The gate is green checks on the PR, job "tests" of .github/workflows/tests.yml.

## Repo map

| Path | Holds |
|---|---|
| .claude/ | agents, hooks, rules by file type, skills |
| .github/workflows/ | tests.yml the checks, test.yml the Linux legs of R346 |
| config/, rules/ | project.json, override list, parameter sheet, rules profile |
| docs/rules/ | the rules, index.md finds an id |
| docs/design/, docs/addin-mock.md | the add-in design, the approved mock |
| docs/lessons.md, docs/signoff.md | lessons, sign off of past and override values |
| docs/repo-map.md | the long map, grep it for a path |
| dotnet/SiteAdaptation.AddIn | the Revit add-in, ribbon, pane, windows |
| dotnet/SiteAdaptation.Common | logic with no Revit type |
| dotnet/CadReader | the worker, reads CAD, PDF, xlsx, writes Excel |
| dotnet/SiteAdaptation.Tests, UiShots, TestHost | gate tests, window renders, Revit test runs |
| python/ | readers, plan build and checks used as test checker |
| samples/new-project/ | a project with no past data, for the clean folder test |
| scripts/ | status.ps1, quick-tests.ps1, checks/ of tests.yml |
| steps/backlog.md | one backlog by stage |
| steps/tasks/, steps/revit-results/ | one file per task, one per Revit check |
| steps/truth-audit.md, steps/handover/ | what is true today, the old lanes' notes |
| steps/questions-open.md, questions-closed.md | the questions |
| steps/PROGRESS.md, log.md, latent.md, history/ | state, round log, findings no real input reaches, old rounds |
| tests/acceptance, golden, holdout | owned by the lead, hold-out answers read by no lane |

## Lessons, read first

The top 10 of docs/lessons.md.

1. Past job outputs are for compare only, never build from them.
2. Never tune and test on the same data.
3. Show the tool from an empty model with nothing pre-run.
4. Write UNKNOWN instead of guessing.
5. Every value carries its source.
6. A rule measured on past models is a past measure, not a manual rule.
7. Pane words must match what is built.
8. A change to floor outlines can make older models skip floors, test on an old model.
9. A locked screen stops Revit checks.
10. Runs with no one watching froze on Revit's Load Once prompt.
