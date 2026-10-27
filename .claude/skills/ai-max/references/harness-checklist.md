# Scan checklist and capability check

Part 1 is the scan for phase 3. Part 2 is the capability check, run at the start of a tool and again every 5 to 7 exchanges.

---

## Part 1. The scan

Work through every item. Do not stop early because the first few came back absent.

Report each as one line:

```
ITEM | PRESENT or ABSENT or UNKNOWN | path | one short fact
```

| # | Item | What to look for | Why it matters |
|---|---|---|---|
| 1 | Rules file | `CLAUDE.md`, its line count and its bytes | Over 200 lines or 10 KB costs context on every message |
| 2 | Scoped rules | `.claude/rules/`, each file's `paths` field and line count | A rules file loads whenever its folder is touched, so a long one costs as much as a long `CLAUDE.md` |
| 3 | Skills | `.claude/skills/`, names, and the version line of any `ai-max` copy | A stale copy runs an older workflow |
| 4 | Agents | `.claude/agents/`, each one's model and tools | A checker holding Bash or Write can change what it checks |
| 5 | Commands | `.claude/commands/`, names | Saved prompts, one word instead of a paste |
| 6 | Hooks | `.claude/settings.json` and `.claude/hooks/`, every event wired, and the shell each hook needs | The only real enforcement. A hook needing a program the machine lacks fails open |
| 7 | Settings | `attribution` off, the deny list | Without them a commit can carry a co-author line and a push can skip the hooks |
| 8 | Line endings | `.gitattributes`, LF pinned for hooks | A Windows checkout with CRLF jams sh hooks shut |
| 9 | Test gate | `.github/workflows/`, file names and triggers | Whether tests run without being asked |
| 10 | Tests | the test folder, how tests run, how many exist, whether the gate fails on zero | A gate with no tests reports green forever |
| 11 | Host split | a Core project with no host reference, and tests over it | Decides what can be proved away from the host |
| 12 | Language | what the source files actually are, the target framework | Decides the gate and whether splitting by territory applies |
| 13 | Steps folder | `steps/PROGRESS.md` and its STATE line, the log and the date of its newest entry | How the person and chat learn where things stand |
| 14 | Connections | `.mcp.json` or configured servers | Live reads instead of manual exports |
| 15 | Editor tasks | `.vscode/tasks.json`, `.vscode/settings.json` | Often stale, often holds a path from one machine |
| 16 | Ignore file | `.gitignore` | Whether build output, secrets, models and client files are kept out |
| 17 | Public repo hygiene | visibility, client files, run logs naming a machine, a user folder or a licence id | A public repo publishes all of it |
| 18 | Source on one machine | code the person mentions that is not in git, such as a zip or a folder on their desktop | Code cannot fix what it cannot see, and must not write a second copy from scratch |
| 19 | Pull requests | how many open, any approved and not merged, any older than main | Stacked pull requests and work left on branches |
| 20 | Branch state | current branch, distance from main, branches already merged | What a pull of main would actually contain |
| 21 | Record sizes | bytes of `PROGRESS.md` and its longest line, the top log entry, and the largest file in `docs/` and `steps/` outside history | A session pays for these on every turn |
| 22 | Usage record | `steps/usage.md` and its newest row | Whether cost is measured or guessed |

## Rules for the scan

**A path or it is UNKNOWN.** PRESENT without a path is a guess wearing a finding's clothes.

**ABSENT means looked for and not found.** If the check could not run, that is UNKNOWN. Treating the two as the same is how a missing thing gets recorded as a settled fact.

**No recommendations here.** Phase 3 reports. Phases 4 to 6 propose. Mixing them means the reader cannot tell what was seen from what was inferred.

**Item 20 before 21 and 22, and say it plainly.** If work exists only on a branch, anyone pulling main does not have it. This catches a tool that looks finished but is not reachable by the person who has to run it. One past repo held 80 unmerged commits while the person ran an old copy of main.

**From chat, through the connector,** items that need a command, such as which hooks actually fire or where `core.hooksPath` points, are UNKNOWN. Say so rather than inferring from the files.

## After the table

One line: which absent items matter most for this tool, and which do not apply. A one file tool with no repo needs almost none of this, and saying so is more useful than listing twenty absences.

`assets/commands/harness-scan.md` holds this scan as one command for a repo.

---

## Part 2. The capability check

At the start of a tool, and again every 5 to 7 exchanges, look for help that is not being used. Raise only what fits the work in front of you now, one line each, so a gap is found while the tool is still being shaped rather than after it is built.

| Capability | Fits when |
|---|---|
| A hook | a rule in `CLAUDE.md` has been broken twice |
| The test gate | tests exist but only run when someone remembers |
| Path rules in `.claude/rules/` | `CLAUDE.md` is near 200 lines |
| A project skill or command | the same procedure has been pasted twice |
| Checker agents | a round is about to merge with nothing reading it but its author |
| A second session by territory | two tasks in one repo are waiting on each other |
| Agent teams, sessions that message each other and share a task list | the work needs sessions to coordinate, not just stay apart |
| A plugin | other people need the same skills, hooks and agents |
| A connector | the tool reads data the person exports by hand today. Autodesk cloud routes need an APS app, see `environment.md` |
| A scheduled task | something should run weekly or report on its own |
| A model per agent | search work can run on a faster model and review on a stronger one |
| A model call inside the tool | the tool compares text today but the job needs judgement |
| The record size test | a record file is near its cap, or the repo has none |
| A usage row per phase | cost is guessed, or one phase seemed to run long |

Check that a capability is actually available in the person's setup before recommending it. If it is not, say so in one line.
