Plan the round before writing anything. List what you will do and in what order, then work through it. Do not start the first edit until the plan is written.

ROUND ADDIN, the next phase of Lane B

Repo baderrahal/site-adaptation-tool. Bader decides, chat drafts and reads, Code does and checks. No approval stop. Never stop to ask Bader during the phase: a question that does not block goes to steps/questions-open.md and the phase carries on. This session runs in the Lane B worktree "Site Adaptation Tool Lane B WT", on a detached HEAD at origin/main, which tools/run-lane.ps1 set.

1. Read CLAUDE.md, then steps/PROGRESS.md, then steps/lane-b.md, then in steps/01_next.md the first phase whose Status is open and names Lane B and whose "Starts when" phases are all marked done, and nothing else at start. If no such phase exists, end at once with the last line "BLOCKED <the phase that waits and what it waits for>", or "LANE DONE" when no Lane B phase is open.
2. Preflight by items 1 and 3 of Start of every session in CLAUDE.md: a detached HEAD equal to origin/main counts as level. Then run git worktree prune, and delete every local branch already merged into main (git branch --merged origin/main), never main and never a branch checked out in another worktree. Line 1 of steps/PROGRESS.md on origin/main must start with STATE WAITING. If it reads STATE RUNNING, end with the last line "BLOCKED STATE RUNNING on origin/main". If the session did not start in the worktree folder, or HEAD is not origin/main, end with "BLOCKED <the reason>".
3. Run that one phase and no other, under the rules of CLAUDE.md and the Lane B rules of steps/lane-b.md. Do the join items steps/01_next.md names for the phase first. Read its docs/design/ parts and its rules by id, never a whole file over 30 KB.
4. Lane B PR titles start with "Lane B", its branch names hold "lane-b". The first code PR of the phase sets line 1 of steps/PROGRESS.md to "STATE RUNNING, <phase>, <date time>", no separate records PR. Each PR: at most 8 changes, the gate, the checkers CLAUDE.md names for that kind of PR, the UI checklist where a window changes. A REAL finding is fixed with a test, a LATENT one goes to steps/latent.md.
5. Merge on green with gh pr checks --watch, then delete the branch and go back to a detached HEAD at origin/main. At most one open PR of Lane B, never two open PRs on one file across the lanes.
6. Revit runs by D11 and D19: probe first, copies only, writes only under the work root, the hashes of every input file before and after, logs outside the repo, the peak memory of every run recorded, the window rule D3 with both lanes counted. Code runs Revit through the test host and never asks Bader to click.
7. At the end of the phase, in one end records PR: mark the phase done in steps/01_next.md and in the phase table of steps/lane-b.md, write the top entry of steps/log.md, rewrite steps/PROGRESS.md to the caps of R400 with line 1 back to STATE WAITING, add the usage row of step 8, merge, and stop.
8. The usage row: run npx ccusage@latest session --since <yesterday as YYYYMMDD>, a day of margin as its session times read as UTC, take the row whose session id is the folder name of this session's scratchpad path or its start, and add it to steps/usage.md, total tokens and cost only. The turns after the run are not in it.

Final message to Bader: plain and short, the PR numbers, the checks with their counts, what is UNKNOWN, and every new question by ID. Its very last line is exactly one of these, alone on its line:
- PHASE DONE <phase id>
- BLOCKED <reason>, when the next Lane B phase waits on another phase
- QUESTION <ids>, only when a question blocks all work of the phase
- LANE DONE, when no Lane B phase is left
