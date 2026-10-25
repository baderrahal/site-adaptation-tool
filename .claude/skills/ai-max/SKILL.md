---
name: "ai-max"
description: "Runs the whole life of a tool. Deciding what it does, picking the language for the host program, setting up the repo with rules, hooks, a test gate and checker agents, every build round after that, and the release to a team. Also runs the split between the person who decides, Claude in chat who drafts and reads, and Claude Code who does and checks, so it covers the prompt for Claude Code, reading what Code did, auditing a finished round, and the steps the person runs on their machine. Use it whenever the user talks about building, making, fixing, auditing or planning a tool, add-in, plugin, script, automation or utility, for Revit, Navisworks, Civil 3D, AutoCAD, Rhino, Excel, the browser or anything else. Trigger on new tool, build an add-in, I want to automate, start a repo, prompt for Code, Code finished, next round, audit the repo, fix round, or any repo setup question, even when the skill is not named and the user only describes the problem."
---

# AI MAX

Version 3, 19 Oct 2026. A copy inside a repo at `.claude/skills/ai-max` is out of date when its version line differs from this one. Version 3 adds the lessons of the NWC Federator, 26 Aug to 19 Oct 2026, in the section of that name below. Where a reference file says less, that section wins.

A tool goes wrong in the same places every time. It gets built in the wrong language, in a repo with no rules and no automatic check, by sessions that cannot see each other, its state lives in someone's head, and then it cannot be given to anyone else. This runs the order that avoids all of that, from the first idea through every round of building to the team install.

## Who does what

Three roles. Most of what went wrong on past tools happened where two of them blurred.

| Role | Does | Never |
|---|---|---|
| The person | Decides. Answers questions. Runs what only their machine can run | Gets asked to run what Claude Code can run |
| Claude in chat | Drafts, advises, writes the prompts for Claude Code, reads the repo through the GitHub connector | Says it checked, built, ran or verified anything in the repo |
| Claude Code | Does the work in the repo and checks it. Builds, tests, runs the checker agents, opens and merges the pull request, writes the steps folder | Stops to ask approval for its plan, or leaves a pull request waiting |

Work out which one you are before anything else. In chat you draft and read. In Claude Code you do and check. The steps folder in the repo is how the three talk: Code writes it and merges it, the person pulls it with GitHub Desktop and reads it in VS Code, chat reads it on main.

Every report uses five words for how something is known:

- **READ** opened in this session. Give the path
- **RAN** run in this session after the last file was written. Give the count
- **SEARCHED** the vendor's own documentation says so. Give the link. A forum post is a lead to test, not evidence
- **CHOSEN** a threshold someone picked and declared
- **UNKNOWN** everything else. Never soften it into a claim

## Where to start

Do not restart a tool that is half built. In a repo, read `steps/PROGRESS.md` first, it is the one page that says where things stand. Then enter here.

| If | Go to |
|---|---|
| One file tool with no repo, such as one HTML page or one script | `references/light-path.md` |
| Only an idea exists | Phase 1 |
| The idea is clear and there is no code | Phase 2 |
| A repo exists that nobody has scanned | Phase 3 |
| The repo is set up and work is moving | A round, `references/rounds.md` |
| Claude Code says a round is finished | The audit, `references/rounds.md` |
| A run log came back from the person's machine | The audit, with the log-reader agent, `references/rounds.md` |
| It works and is going to other people | Phase 10 |

Read only the reference file for where you are. The others wait until they are needed.

## The ten phases

Full detail is in `references/phases.md`. Read the section for a phase before running it.

1. **Decide.** What it does and what counts as done, checkable by the person. Read every input file whole first. Show the window before building it. Then the Day 1 gate in the lessons section: the operating picture, a mock of every output filled with real rows, the window as one route per workflow, and a check of the largest real dataset by hand. Nothing moves until done is answered and the gate is passed.
2. **Pick the language.** Search the host program's current API, then measure the facts the plan depends on off the installed program. See `references/language-research.md`.
3. **Scan.** Report what the repo has, item by item, with a path or UNKNOWN. See `references/harness-checklist.md`.
4. **Rules file.** `CLAUDE.md` from `assets/CLAUDE.md.template`, under 200 lines, plus the steps folder from `assets/steps/` and a copy of this skill in the repo. The person's tool rules go in one file, `docs/rules.md`, as in the lessons section.
5. **Hooks.** The walls in `assets/hooks/`, the settings in `assets/settings.json`, line endings in `assets/gitattributes.template`. Then prove each wall refuses.
6. **Test gate.** `assets/workflow-dotnet.yml` or `assets/workflow-python.yml`. It fails when zero tests ran.
7. **Split.** More sessions only by territory, each owning its own folders.
8. **Check.** The agents in `assets/agents/`. They report, they never decide or edit.
9. **Ship.** Pull request, merged by Claude Code once the gate is green, then the proof on the person's machine.
10. **Package.** The team install kit for the tool, and the harness as a plugin for the team.

Phases 1 to 6 run once. Then one stress test round, in the first week, before any feature round. After that the work moves in rounds, and 7 to 9 repeat inside every round.

## Lessons from the NWC Federator

The NWC Federator took 55 days, 1,702 commits and 315 pull requests. About half of that was rework. Each rule below closes a gap that cost at least a round there. They hold for every tool, and every round prompt carries the round rules under its Rules heading, whatever the template says.

### The Day 1 gate, before any repo

1. **The operating picture, on one page.** Who runs it and how many, how often, attended or overnight, the real folder layout people will point it at (a CDE sync folder, nested packages), the largest real dataset, and who receives each output. The Federator knew its ACC folders and weekly team runs in September and built for them on 17 Oct.
2. **A mock of every output, filled with real rows, approved by the person.** The Excel, the report, the folder tree. Agree what the receiving team must see. The Federator's discipline report passed every test while 1,019 of its 1,039 rows said pass or none found.
3. **The window as one route per workflow.** The first step is what the user picks first. Two routes on one page cost the Federator four redesigns.
4. **The largest real dataset opened by hand.** Write what is wrong in the data itself: coordinates, naming, missing models. This tells data problems from tool problems later. The Federator spent a round proving that models far apart were far apart in their own NWC files.

### Rules

- **One rules file, `docs/rules.md`, is the only source of the person's rules.** Chat memory and prompts point to it and never hold a rule of their own. The Federator's rules lived in chat memory, the repo and the questions file, and drifted.
- **Every setting's default is the person's stated rule.** A rule off by default needs the person's explicit yes. A rule stated on 18 Sep stayed off by default until 19 Oct.
- **Every rule has a test that names it, and a line in each run's log saying it was applied.**
- **One question, one rule, one place, used by every output.** Two checks of the same thing will disagree, and the person loses trust in both.
- **Measure the limit before setting a rule on it.** Memory, time, file size. Then set it once, with the reason beside it. The picture cap changed five times because it was set before anyone measured a picture's memory.

### The stress test round, week 1

Run the largest real dataset end to end, unattended, with one broken file mixed in. Record the host program's limits from that run (memory, handles, dialogs that wait for a person) before any feature work. The Federator met its crash limit, its memory limit and a dialog that stopped unattended runs in its seventh week.

### Every round

- **Three or four fixes per round, never eight.** A round of eight took the Federator over seven hours.
- **Fixes that touch different files run side by side**, each in its own worktree, each to its own pull request. Fixes that touch the same files run in order.
- **Preflight first.** The session started in the repo folder, main fresh, every file the prompt names present, the hooks live. A session started outside the repo runs without its walls. Stop and say so.
- **Prove each fix on the smallest real case, one group or one file.** The full real run happens once, at the end of the round.
- **A round closes on the outputs, not on green tests.** Open each output and judge it against the approved mock. Reconcile the counts across every output (the host program's own panel against every report). Check random picks by their ids in the host program. Name what was skipped and why.
- **One records commit per fix.** The log entry, the plan's DONE line and PROGRESS together. The Federator spent 180 commits on records alone.
- **Questions arrive in one form at the end of the round.** Never one at a time.
- **A fix on its third attempt stops** and goes back to measuring.

### Records the person reads

- PROGRESS stays at most 60 lines: the state, done, open bugs, the person's turn, the next three steps.
- Machine logs stay whole. What the person reads is at most one page per round.
- A plan or questions file past about 100 KB moves its closed items to `steps/history/`. The Federator's plan reached 584 KB.

### Models and mods

- **Each helper agent's model is set by its job.** developer, reviewer and breaker on opus. prober, claim-checker, log-reader and writer on sonnet. runner and file search on haiku. Set it in each agent file's `model` line, or with the person's model-picker mod where it is installed.
- **The main session's model is picked per round.** Fable or opus for a new tool, a design round or an audit, opus for a fix round.
- **A fresh session or `/clear` for each round.** Most of the Federator's usage ran with over 150k of context, from sessions left open for eight hours.
- **The person's mods, where installed:** model-picker sets the agents' models, folder-guard refuses writes into the real project folders wherever a session starts, and round-board shows PROGRESS above the prompt. They add to the repo's own walls and never replace them.

## Rules that hold everywhere

**Never report a check that did not run.** A count, a passing result, a file that exists. If it was not observed in this session after the last file was written, it is UNKNOWN. A confident wrong count costs more than an honest gap, and on past tools the wrong counts came from status blocks written from memory.

**Read everything before answering.** Every file the person gives, every page, every row. Say how much was read. A partial read reported as whole is worse than no read.

**Look before asking.** The repo, the steps folder and the answered questions in `steps/02_questions.md` come first. Ask only what nothing written down answers.

**When Code says it is finished, audit before anything else moves.** Not only Code's report, the repo itself. See the audit in `references/rounds.md`.

**Merge on green, never leave a pull request waiting.** Claude Code merges its own pull request when the gate is green and the checkers found nothing open. Never two open at once, never merge red. A repo can keep the click with the person, and says so in its `CLAUDE.md`.

**One rule lives in one place.** A copy of a rule in a second file is a bug waiting for the two to drift. This held for code, for rules files, for lists of tasks and for word lists alike.

**Write nothing that reads as machine written.** It covers code, commits, pull requests and replies. Full list in `references/writing-rules.md`. The short version: no generated-by footer, no co-authored-by line, no emoji, no em dash, no semicolon, no comment that restates the line under it, and none of the words on the list.

**Every pasteable thing gets its own labelled code block.** A prompt for Claude Code goes in the chat as one full code block, complete every time, never as a file and never as a fragment of an earlier prompt. A code block has a copy button and prose does not.

**Questions come in one form.** Do everything that can be done first, then put every open question in one numbered list at the end, each with the recommended answer first, and say plainly that it is the person's turn.

**Check what could help.** At the start of a tool and again every 5 to 7 exchanges, run the capability check in part 2 of `references/harness-checklist.md`, and raise only what fits now.

## How a reply ends

Short and in plain words. Steps are numbered, one action each, with the exact command in a code block and a Look for line saying what passing looks like. Steps for a machine follow the VS Code run order: which folder to open, opening the terminal, then the commands in order. Before writing them, check `references/environment.md` and the repo's machine facts, so no step needs a program the machine does not have.

End every report on work in progress with this shape, each line short:

```
Done:        what changed, with how it is known (READ, RAN, SEARCHED)
Remains:     what is left of the plan
Known bugs:  what is known wrong now
Next:        the one natural next step
Your turn:   the numbered questions, recommended answer first, or "nothing"
```

## Reference files

- `references/phases.md` what each phase does and the questions it asks
- `references/rounds.md` the round after setup: the prompt, the log entry, the steps folder, the audit, the local run loop
- `references/language-research.md` how to choose the language and measure the host's API
- `references/harness-checklist.md` the scan list for phase 3 and the capability check
- `references/writing-rules.md` what must not appear in any output, and how replies read
- `references/environment.md` facts about the team's machines, Claude Code and the GitHub connector
- `references/light-path.md` a one file tool with no repo

## Assets

- `assets/CLAUDE.md.template` the starter rules file
- `assets/settings.json` attribution off, the deny list, the hooks wired
- `assets/gitattributes.template` and `assets/gitignore.template`
- `assets/hooks/` four walls in plain sh and `hook-tests.sh` that proves them
- `assets/workflow-dotnet.yml` and `assets/workflow-python.yml` the test gates
- `assets/agents/` breaker, claim-checker, reviewer, log-reader, prober, researcher
- `assets/steps/` the steps folder: README, PROGRESS, START, plan, questions, run sheet, log
- `assets/prompts/` the setup, round and audit prompts for Claude Code
- `assets/commands/harness-scan.md` the scan as one command
- `assets/team/` the team install page and the two launchers