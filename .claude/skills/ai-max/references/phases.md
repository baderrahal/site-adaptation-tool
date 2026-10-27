# The ten phases

## Contents

1. Decide
2. Pick the language
3. Scan
4. Rules file
5. Hooks
6. Test gate
7. Split
8. Check
9. Ship
10. Package

Phases 1 to 6 run once per tool. After that the work moves in rounds, described in `rounds.md`, and phases 7 to 9 repeat inside every round.

---

## Phase 1. Decide

Two questions, and neither can be skipped. Most failed tools failed here, not in the code.

**What does it do?** One sentence. If it takes a paragraph, it is more than one tool.

**What counts as done?** Push for something the person can check themselves, with known numbers. "It works" is not an answer. These are:

- totals match a test file with known counts, and five real records match the source system
- one building end to end with no errors, every building unattended in under 45 minutes, and the counts in the report match the host program's own panel exactly

If the person resists, offer three candidate definitions that differ in kind, not in wording, with the recommended one first. Picking is faster than inventing.

### Before asking anything

Read every file the person supplied, whole. Every page of a PDF, every sheet of a workbook. Say how much was read. Look in the repo, if one exists, and in anything already answered. Only then ask.

### The questions form

Everything still open goes into one numbered form, sent once, with the recommended option first in each and a short answer format such as `1 B, 2 yes, 3 yes`. The person batches their answers, so a question asked alone in the middle of the work costs a full round trip.

Cover these, skipping any already answered:

- **Who runs it.** The author alone, or a team, and how many. A tool for a team cannot need a compiler, an admin step or a path that exists on one machine
- **Which host version they run.** The team's, not the author's. On one past tool the author ran Civil 3D 2025 and the team ran 2024, and the surface broke on their machines
- **How often.** Once, weekly or daily. A weekly run by a team justifies a different build than a one-off
- **What it reads and what it writes, and where the outputs go.** Who picks each folder
- **Whether it changes a model or files people made.** If it does, it shows what it will do and changes nothing until the person presses the button, and it reuses what a person made by hand rather than overwriting it
- **Where it is built.** Which machine, where the repo lives, public or private. A public repo holds no client file and no run log that names a machine or a person
- **Full path or light path.** One HTML page or one script with no repo goes to `light-path.md`
- **What nobody has written down.** When the rule for something is not written anywhere, such as which plots belong to one checklist, the person picks it in the window. The tool does not invent a rule for it
- **Version 1.** The smallest slice that meets done. Everything else is written down as a later version, so nothing is lost and nothing creeps in

### Show the window before building it

For any tool with a window, show how it will look before any code: a drawing in the chat, or a design the person can mark up. Changing a picture costs nothing and changing a built window costs a round.

The house look for the team's tools, unless the person says otherwise:

- Parsons font, Franklin Gothic, and the Parsons colours, navy `#081931`, cyan `#00AEE6`, green `#50BF34`. Square, professional, not small rounded boxes
- A label or note sits above, and its action sits directly below it. No sub notes under labels
- More detail sits behind a plus expander rather than on the face of the window
- A step at a time when there are several, with a step that cannot be worked on yet saying in one line why
- A new window follows the pattern of the tool's windows that already exist
- A tick box label of at most eight words and one grey line of at most twelve. No capitals for emphasis
- No code name and no framework message in a label. A failure dialog may carry the exception text
- Everything through the window. The person never types into a command line or answers a command line prompt

**Output of this phase:** the two sentences, the answered form, and the approved picture of the window. They go into the repo later, the sentences at the top of `CLAUDE.md` and the form as `steps/02_questions.md`.

---

## Phase 2. Pick the language

Read `language-research.md` and follow it. The host decides most of the answer, the current state of its API is searched rather than recalled, and every API fact the plan depends on is measured off the installed program before code calls it.

Do not skip this because the answer seems obvious. APIs change between versions, and a member that exists in 2026 can be missing in 2025.

**Output:** the language, one paragraph of reasoning, the two runners up and what each loses, anything the choice closes off, and the list of host facts still to measure.

---

## Phase 3. Scan

Read `harness-checklist.md` and work through part 1 item by item.

Report one line per item:

```
ITEM | PRESENT or ABSENT or UNKNOWN | path | one short fact
```

- A path for anything PRESENT. A claim without a path is UNKNOWN
- ABSENT means looked for and not there. A check that could not run is UNKNOWN
- No recommendations in this phase. Reporting and recommending in one breath is how a guess becomes a finding

An existing repo keeps the names it already uses. The steps folder, the log and the state files may be named differently from the templates, and that is fine. Map them, do not rename them.

If the repo does not exist yet, say so and go to phase 4 with everything ABSENT. The person creates it on GitHub with a README, so that `main` exists and is the default branch before Claude Code makes its first branch. A repo created empty ends up with Code's first branch as its default and no pull request can be opened, which cost three sessions on one past tool.

**Output:** the table, then one line naming which absent items matter for this tool and which do not apply.

---

## Phase 4. Rules file

Copy `assets/CLAUDE.md.template` and fill it in.

Keep `CLAUDE.md` under 200 lines and 10 KB. Past that, the rules for one folder go in `.claude/rules/<folder>.md` with a `paths` field at the top, so they load only when a file under that folder is touched. Each rules file stays under 200 lines too. One past repo let a rules file grow to 2,900 lines of specification, which loads every time anyone touches that folder. A specification goes in `docs/` and is read when needed. The reasons and measurements behind a rule go in `docs/history/`, so the rule itself stays one line.

What belongs in it:

- what the tool is and what done means
- who does what, and how a round works
- the host and target, read off a dated source
- where Code runs and where the tool runs, which are rarely the same machine, and the machine facts: how git is reached, where the repo lives, which host versions are installed
- where things are
- the exact build and test commands
- the rules every file keeps
- things that have gone wrong before, one line each

What does not belong: anything the code says clearly, general good practice, and a copy of a rule that lives somewhere else.

**The steps folder.** Copy `assets/steps/` into `steps/` and fill in `PROGRESS.md`. It is how the three roles talk. See `rounds.md` for what each file holds.

**The skill itself.** Copy this skill into `.claude/skills/ai-max/` so every Claude Code session on the repo has it, and keep the SessionStart line in `assets/settings.json` that points at it. When this skill's version line changes, the repo copy is refreshed in its own round.

**Output:** the files and their line counts.

---

## Phase 5. Hooks

A rule in `CLAUDE.md` is a request. A hook is a wall. Anything that must never happen goes here, because a request can be reasoned around and a wall cannot.

Start from `assets/hooks/`, `assets/settings.json` and `assets/gitattributes.template`.

| Wall | Fires on | Prevents |
|---|---|---|
| `refuse-protected-paths.sh` | file tools and commands | edits to evidence folders, to the walls themselves, and any command naming a live project folder |
| `refuse-git-on-main.sh` | commands | a commit on main, and a push that lands on main without a pull request |
| `require-log.sh` | commands | a commit that does not carry the log |
| `stop-gate.sh` | the session stopping | stopping while `steps/PROGRESS.md` reads OPEN, once per stop |

Why they are written the way they are, all measured on past repos:

- **Plain sh with builtins.** Starting a program costs about two seconds on the team's Windows machines, so a hook that pipes through python or grep on every call slows every call. Worse, a hook that parses with python3 where python3 is missing reads an empty command and lets everything through. A wall that fails open looks exactly like a wall that passed
- **LF line endings.** sh reads a carriage return as part of a word, so a Windows checkout dies on its first line with exit 2, which is the refusal code, and every call is refused. `.gitattributes` pins hooks to LF on every machine
- **The walls protect themselves.** A file tool cannot edit `.claude/hooks` or `.claude/settings`. A change to a wall is written somewhere else, proved with `hook-tests.sh`, and copied in with a command, and the log says so
- **Each header names what it cannot see.** These read the words of a call. They are walls against a mistake, not a sandbox

Settings in `assets/settings.json`:

- `attribution` off for commits and pull requests. Read each commit back off main anyway
- a deny list for force pushes, `--no-verify`, changing `core.hooksPath`, `reset --hard`, `clean` and an admin merge
- the hooks wired as `sh "${CLAUDE_PROJECT_DIR:-.}/.claude/hooks/<name>.sh"`, with PowerShell in the matchers, because a Windows session has a PowerShell tool beside Bash

**Prove every wall.** Run `sh .claude/hooks/hook-tests.sh` and read the count. Then, in the session, try each one for real: write a file under a protected folder with the file tool, and commit without the log. Never report a wall as working unless it was seen refusing. Settings, hooks and agent definitions load when a session starts, so a new wall bites from the next session, and the proof belongs to that session.

A scope on Bash in an agent's tools line, such as `Bash(git log:*)`, fails open. If an agent must not write, it gets no Bash at all.

**Output:** the files, what each prevents, the hook-tests count, and the refusals seen live.

---

## Phase 6. Test gate

Copy `assets/workflow-dotnet.yml` or `assets/workflow-python.yml` to `.github/workflows/tests.yml` and set the commands.

The gate runs the hook tests first, then builds, then tests, then reads the count out of the result file and fails when it is zero. One test in it holds the record size caps of the Usage section of `SKILL.md`, so a record that grows past its cap fails the pull request. A gate that runs zero tests reports green forever, and a folder named tests is not a test.

**Add-ins for a host program.** Split the code in two from the first day:

- `<Name>.Core` holds every rule a test can prove and no host type at all. A rule that can be tested lives here even when only the add-in reads it
- `<Name>.<Host>` holds the window and every call into the host, and nothing else

Core is what the gate tests. Whether the add-in project builds in the gate depends on the host. Where a package supplies the host's reference assemblies, such as the Nice3point packages for Revit, the gate builds the whole solution and catches a compile break. Where none exists, as for Navisworks, the add-in builds only on a machine with the host, so add text checks for the compile faults that have bitten before, such as a local declared twice or a type named without its namespace imported, each with a folder broken on purpose that the check must refuse.

Rules for tests, each paid for on a past tool:

- every check gets a test that breaks one thing and asserts the check names it. A test that only passes the good file proves nothing
- tests go through the real entry point, not only the function behind it. Three guards once shipped correct and unreachable
- a written file is read back off the disk as a file, never through the object that wrote it
- a path in a test is built, never typed, so tests pass on Linux and Windows alike
- a test skips only for a rule of the file system, with a line saying what it needs. A check that could not run is not a check that passed
- before trusting any guard, break the code it guards and watch it go red. A mutation test that has never failed is not a test. Hash the file before mutating and after restoring, and stop if they differ

A git pre-commit hook in `.githooks/` can run the tests before every commit. The person switches it on once per clone with `git config core.hooksPath .githooks`.

For a public repo, add a check that no committed file names a machine, a user folder or a licence id. Run logs carry all three.

**Output:** the file path, what triggers it, and the number of tests it ran on its first real run.

---

## Phase 7. Split

One session sets up. More sessions build, but only by territory.

**Territory.** Each task owns its own folders: its folder in Core, in the add-in and in tests. A record file lists the tasks, one per line, and both the rules and the hook read it, so there is one list and not two. A hook refuses a commit that touches two tasks' folders, or the shared folder together with a task's. The rules for it:

- a session edits only its own task's files. Reading anything is fine
- a change to the shared folder runs alone. The person stops the other sessions first, and it lands as its own pull request
- a task that needs another task's file asks the person. It does not make the change
- each task keeps its own page and log, such as `steps/<task>/PROGRESS.md`, because a status file every session writes conflicts on every merge

Two to four sessions is the useful range. Collisions grow by pairs, not by workers. From a terminal each one starts with `claude -w <name>` in its own worktree. In the app, start a new session per task and name its branch in the prompt.

**Code only the host can run.** Splitting by territory is fine, but every change on the host side is unproven until a run on the machine. Batch the host changes of several rounds into one machine cycle rather than paying a cycle per change, and see the cap on unproven changes in `rounds.md`.

**Output:** the tasks, their territories, who owns the shared folder, and the record file.

---

## Phase 8. Check

Agents read the work and try to break it. They are in `assets/agents/`:

- **breaker** finds the input that makes the change wrong, silent wrong numbers first
- **claim-checker** marks every claim in a log entry or pull request BACKED, UNBACKED or WRONG
- **reviewer** is the second reader of every change, never its author, and gives APPROVE or CHANGES NEEDED
- **log-reader** turns one run's log into findings, each quoting its line
- **prober** measures one host fact with a script and records it with its date
- **researcher** searches for current practice and the vendor's own word on a question, and returns sourced findings

Rules that came out of using them:

- checkers get Read, Grep and Glob only. They report, they never decide and never edit. An agent that can find a problem and quietly fix it is an agent whose findings nobody sees
- the lead gathers the evidence, the build output, the test counts, the changed files, the run folders, and hands it over. The agent judges evidence, it does not go looking for it
- verify a FAIL yourself before acting on it, and do the same for a PASS that rests on a number. One past run checked the verifier's arithmetic and took the critic's finding on trust, and the wrong finding went through
- agents read files when they are dispatched. If a source changes mid-run, start again from the first affected step rather than patching
- a checker that could not run, such as one stopped by a usage limit, means the work is not machine audited, and the log says so
- never put an agent on a required path. A commit gate that waits on an agent hangs the day the agent stalls
- breaker and reviewer mark each finding REAL or LATENT, and a pull request gets at most 2 review passes, as the Usage section of `SKILL.md` says

**Output:** what each agent found and what was done about each finding.

---

## Phase 9. Ship

Before the pull request:

- the working tree is clean
- the full test run happened after the last file was written, and the counts in the log come from it, before and after
- the checkers ran over the change and their findings are fixed or written down
- the log entry is at the top of `steps/log.md` and `steps/PROGRESS.md` is rewritten

The pull request body says what was proved here, with RAN counts, and what waits for the person's machine. Write it to a file and pass it with `--body-file`, and the commit message with `-F`.

Then Claude Code merges it, once the gate is green:

- never merge red, and never leave a pull request open or approved. Do not start a second while one is open
- on a squash merge, pass the title and the message on the merge call. GitHub builds the default squash message on its own server, where no hook runs, and has added a co-author line there. Read the merge commit back off main
- delete the branch. When the session cannot, because its proxy refuses branch deletes, name the branch in `PROGRESS.md` for the person to delete from the pull request page
- if red, do not merge. Fix it or report what failed

A repo can keep the merge with the person, for example where a merge is the last checkpoint before code reaches a machine that cannot easily undo it. It says so in `CLAUDE.md`. The default is merge on green.

**The proof on the machine.** Anything only the host can run is unproven until it runs there. Either write the person's run sheet, `steps/03_your_steps.md`, or run the local loop if Code works on that machine. Both are in `rounds.md`. A fix is done when a run on real files shows it.

**Output:** the pull request link, the test count and when it ran, the merge commit read back, and what waits for the machine.

---

## Phase 10. Package

Run this only when the tool is going to other people, and build the package only when the person says to.

**The team kit for an add-in.** Built from a clean checkout of main by a script, written outside the repo, never committed. It holds:

- the built add-in, stamped with the commit and the time it was built. The stamp shows in the window's title bar and in every log, and the version in the kit is read off it, never typed
- `assets/team/Install.cmd`, which starts Windows PowerShell for that one run with the policy bypassed, needs no admin, installs to the person's own profile, keeps the version it replaces, and refuses when started from inside the zip
- `assets/team/Put back the previous version.cmd`, which restores the kept version
- `assets/team/INSTALL-TEAM.txt`, plain steps written for someone who has never seen the tool, including what to send when something fails: the newest log and a screenshot of the whole window

Every install ends on one line that starts DONE, FAILED or REFUSED and says what to do. The install script runs `Unblock-File` first, because a DLL from a browser download fails to load with a message that never mentions the block. It copies the bundle's contents, refuses to finish if any reference is satisfied by neither the bundle, the framework nor the host, and refuses while the host program is running.

**A one file tool** is the file and a short page saying how to open it.

**Before it goes to the wider company**, ask whether it needs the IT security review, a review package and a verification report. One past tool went through it before release, and finding out late costs the release date.

**When the person asks for it**, a short note to the people who will use it and one to management, plain and high level, in the Parsons look.

**The harness for the team.** Wrap the skills, hooks, agents and commands into a plugin so the team installs them once rather than copying them into every repo. Before packaging check that nothing points at a path from one machine, nothing holds a credential, and the install steps were followed once from scratch by someone who did not build it. That last check is the one that finds the missing step.

**Output:** the kit, its version read off the stamp, and the install page.
