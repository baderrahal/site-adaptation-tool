# Rounds

After setup the work moves in rounds. One round is one goal, one branch, one pull request, merged, and a log entry that says what it proved.

## Contents

1. One round
2. The prompt for Claude Code
3. The steps folder
4. The log entry
5. The person's run sheet
6. The audit
7. Reading the repo from chat
8. Things that stop a round
9. The local run loop
10. Keeping files short

---

## 1. One round

1. The person picks the goal, from `steps/01_next.md` or a new one
2. Claude in chat writes the full prompt from `assets/prompts/round.md`
3. Claude Code, in a fresh session, plans, branches, works, runs the full tests after the last edit, runs the checkers, writes the log entry, rewrites `steps/PROGRESS.md`, opens the pull request, merges it once the gate is green, reads the merge back off main, adds its row to `steps/usage.md`, and stops at the end of its phase
4. Claude in chat audits the round and tells the person: done, remains, known bugs, next, and their turn

No approval stop anywhere in it. The person decides at the start and at the end, never in the middle.

One finding per pull request, except small cosmetic fixes, which can share one.

**The first round on a tool that already exists** changes no code. It reads the whole repo and writes `00_analysis.md`, numbering what it finds as bugs B1, logic problems L1 and missing pieces M1, then the plan in `01_next.md` and every question in `02_questions.md`. The person answers the questions in one pass, and the fixes start in the round after.

---

## 2. The prompt for Claude Code

Every prompt opens with this line, word for word:

```
Plan the round before writing anything. List what you will do and in what order, then work through it. Do not start the first edit until the plan is written.
```

Then, in this order. `assets/prompts/round.md` has the blank:

- **The round's title.** ROUND N, a few words
- **Context.** The repo, main's commit if known, what the last log entry says, and who does what in one line
- **Goal.** The outcomes as bullets, each one observable
- **Read first, in order.** `CLAUDE.md`, `.claude/rules`, `steps/PROGRESS.md`, then the files the round needs, by path, and never a whole file over 30 KB
- **Do this.** Numbered, one action each
- **Out of scope.** What not to touch
- **Checks that must pass.** The exact commands
- **Rules.** Branch, one pull request, tests after the last edit, breaker and claim-checker, findings REAL or LATENT, at most 2 review passes, merge on green with the squash message passed, the log entry, `PROGRESS.md` rewritten, the run sheet when the host side changed, a question goes in `steps/02_questions.md` and the work carries on, never report a check that did not run, the writing rules, the usage row, and stop at the end of the phase
- **Final message to the person.** Plain and short: what is done, what is open, what is UNKNOWN, and anything that needs their hand

The prompt is complete every time. Never a fragment the person has to assemble from an earlier message, and never a file to download. It goes in the chat as one code block.

When a round is large, say so and split it in the prompt, fixes 1 to 4 first and 5 to 8 second. Code should never quietly do half.

---

## 3. The steps folder

Written by Claude Code, merged to main, pulled by the person with GitHub Desktop and read in VS Code, read by chat through the connector. It is written for a phone screen: short lines, one action per step, every command in a code block. Templates are in `assets/steps/`.

| File | Holds | Rule |
|---|---|---|
| `PROGRESS.md` | the one page every session and every reader starts from | inside the caps of the Usage section of `SKILL.md`, rewritten never appended |
| `START.md` | the prompt the person pastes into a new Claude Code session | under 20 lines |
| `00_analysis.md` | what the code does, what is wrong, what is missing | history once the plan exists |
| `01_next.md` | the plan, items numbered F1, F2, each with a DONE line when done | the order at the top |
| `02_questions.md` | every question for the person, numbered once, the answer under each | never renumbered |
| `03_your_steps.md` | what the person does at their machine | rewritten each round, at most 30 steps |
| `log.md` | one entry per round, newest at the top | top entry at most 4 KB, older entries move to `history/` past about 100 KB |
| `latent.md` | findings no real input reaches, kept with no fix | moved back as REAL when real data reaches one |
| `usage.md` | one row per session, its tokens and cost | added after each phase merges |
| `logs/` | run logs the person sends back | evidence, never edited, a hook refuses it |

`PROGRESS.md` starts with one line, `STATE OPEN`, `STATE WAITING`, or `STATE CLOSED`, then a comma and the date and time of the rewrite. Then, each short:

- **Now** what is in work and what merged since the last rewrite
- **Next** the next three steps, in order
- **Waiting for the person** question numbers and runs on their machine, or none
- **Blockers and known bugs** at most five lines
- **Where the history is** the log, the plan, the questions

Rewrite it after every merge, every run, every answer from the person and before any stop. OPEN means the session is working on its own. WAITING means it needs the person, and the page says for what. The stop gate in `assets/hooks/stop-gate.sh` sends a session back once while the page reads OPEN, so a session cannot end with the page stale and nobody told.

A repo that already uses other names keeps them. One past repo kept a state file per task and a log per task. What matters is one short page read first, and one log newest on top.

---

## 4. The log entry

At the top of `steps/log.md`, in this shape:

```
## Round N, short title, yyyy-mm-dd
Goal:         one sentence
Done:         what changed, by file or folder
Checked:      each command run and what it showed, test counts before and after
UNKNOWN:      what could not be checked, and why
Remains:      what was planned and left
Known bugs:   what is known wrong now
Next:         the one natural next step
Your turn:    question numbers and runs on the machine, or nothing
```

A number goes in only if it came from a run made after the last file was written. A finding that stays open goes in the plan or the tracker, not only in a log line where nobody finds it again. The `require-log.sh` wall refuses a commit that does not carry the log.

---

## 5. The person's run sheet

`steps/03_your_steps.md` is what the person does on the machine that has the host program. Rewritten each round, never appended, because a run sheet nobody finishes tests nothing.

- **At most 30 steps.** Rank by what breaks the tool if it is wrong, not by what is easiest to check. What does not fit goes under "Not covered by this pass" at the end, so nothing is lost
- **Get the code first.** In GitHub Desktop: pick the repo, check the branch is main, press Fetch origin, then Pull origin. Use the app the person actually has, which `CLAUDE.md` names under machine facts. Never a git command where git is not on the PATH
- **Then the VS Code run order.** Open Folder on the repo, New Terminal, then each command in its own code block
- **One action per step**, then a line starting `Look for:` with the exact words or count that means it passed
- **End with what to send back.** The run's log into `steps/logs/` through GitHub Desktop, and a screenshot when a window is wrong

**The cap on unproven changes.** Every change on the host side is unproven until a run. When three rounds have landed host changes with no run on the machine, the next round writes the run sheet and no code. One past tool reached 28 fixes that nobody had seen in the host.

---

## 6. The audit

When Claude Code says a round is finished, nothing else moves until the audit. It checks the repo, not only Code's report.

**In Claude Code**, as an audit round from `assets/prompts/audit.md`:

- run the full tests and the build, and keep the counts
- breaker over every file the round changed, reviewer over the change
- claim-checker over the round's log entry and pull request body against the repo
- recount every number in the entry, read every file it names, read the merge commit back off main
- add the result to the log, and fix or file every WRONG and UNBACKED claim

**In chat**, through the GitHub connector:

1. Read `steps/PROGRESS.md` and the top entry of the log on main, and the sizes of the start files and the newest row of `steps/usage.md` against the caps
2. Read the merge commit with its file stats, to see what actually changed
3. Read the changed files that matter
4. Compare every claim in the entry with what was read
5. Report each as READ with its path, or UNKNOWN when it needs a run. Chat cannot see GitHub Actions results, so a test count from Code is RAN by Code and UNKNOWN to chat
6. Then the next prompt, and the person's turn

Never write that chat checked, verified or confirmed something in the repo. It read it.

---

## 7. Reading the repo from chat

Facts about the GitHub connector, from past rounds:

- a private repo returns not found until the Claude GitHub app has been given that repo in GitHub settings, Installed GitHub Apps. It is access, not a wrong path
- read a merge commit with its file stats to see what a round changed
- a large file can pass the connector's inline limit. Fetch the raw file instead
- the connector cannot read GitHub Actions results
- a file over 30 KB is read in parts or searched, never pulled whole into a reply
- the pull request list can report merged false on a merged pull request. Read merged_at
- a public repo can also be cloned read only into the chat's own workspace, which is quicker for many files. It is still reading, not checking

---

## 8. Things that stop a round

**Three strikes.** When the same finding survives three fix attempts, stop. Write what was tried and what each run showed, and put the question to the person.

**Anything that starts on its own.** After opening a pull request a session may schedule its own check-in, and each check-in schedules the next. Name anything recurring in the same turn it starts, and remove it unless the person asked for it. A merge landing in the middle of a proving run destroys what the run proves.

**A wall in the way.** Never work around a hook with `--no-verify`, a different shell or a different tool. If the wall is wrong, write the change elsewhere, prove it with `hook-tests.sh`, copy it in with a command, and name it in the log.

**A usage limit.** Agents stop when the account limit is reached. Nothing after that point was machine audited, and the log says so.

---

## 9. The local run loop

When Claude Code runs on the machine that has the host program, it can prove its own fixes: build, install, run the tool on a copy of real files, read what the run did, fix one finding per branch and pull request, and prove each fix with another run. A run by the person on their real folder stays the final proof.

Guards that never bend:

- live project folders and CDE sync folders, such as the ACC Desktop Connector folder, are read only. A script copies what the loop needs, finds the folder itself, and no command ever names it. The paths wall refuses the rest
- never close a host program the loop did not start. Read every process id first and close only your own, even when the run fails
- copy the installed add-in aside before installing over it, once per session
- run evidence goes into `steps/runs/<set>/<run>/`, copied by a command and never edited. A file over 20 MB is never committed, it stays outside the repo and the log names it with its size
- a run log leaves the machine only after the machine name, the user folder and any licence id are masked

Roles the lead can add for the loop, beside the five checkers: a runner that builds, installs, runs and collects, a developer that takes one finding to one pull request with the failing test first, and a writer that keeps the install page and the run sheet true to the code.

---

## 10. Keeping files short

The person reads on a phone. On past tools the run sheet grew past 1,700 lines, one log past 600 KB and one state file past 2,800 lines, and each stopped being read. Every session pays for the same files on every turn.

- the byte caps of every record are in the Usage section of `SKILL.md`, held by one test in the gate
- the run sheet at most 30 steps, rewritten
- every `.claude/rules` file under 200 lines
- counts on the page are made by a script from the tracker where one exists, never typed
