Plan the round before writing anything. List what you will do and in what order, then work through it. Do not start the first edit until the plan is written.

ROUND ADDIN, the next phase of Lane A

Repo baderrahal/site-adaptation-tool. Bader decides, chat drafts and reads, Code does and checks. No approval stop. Never stop to ask Bader during the phase: a question that does not block goes to steps/questions-open.md and the phase carries on.

1. Read CLAUDE.md, then steps/PROGRESS.md, then in steps/01_next.md the first phase whose Status is open and names Lane A and whose "Starts when" phases are all marked done, and nothing else at start. If no such phase exists, end at once with the last line "BLOCKED <the phase that waits and what it waits for>", or "LANE DONE" when no Lane A phase is open.
2. Preflight by items 1 and 3 of Start of every session in CLAUDE.md. Then run git worktree prune, and delete every local branch already merged into main (git branch --merged origin/main), never main and never the current branch. Line 1 of steps/PROGRESS.md must start with STATE WAITING. If it reads STATE RUNNING, end with the last line "BLOCKED STATE RUNNING on origin/main". If the session did not start in the repo folder, or main is not level with origin, end with "BLOCKED <the reason>".
3. Run that one phase and no other, under the rules of CLAUDE.md. Read its docs/design/ parts and its rules by id, never a whole file over 30 KB.
4. The first code PR of the phase sets line 1 of steps/PROGRESS.md to "STATE RUNNING, <phase>, <date time>", no separate records PR. Each PR: at most 8 changes, the gate, the checkers CLAUDE.md names for that kind of PR. A REAL finding is fixed with a test, a LATENT one goes to steps/latent.md.
5. Merge on green with gh pr checks --watch, then delete the branch and pull main. At most one open PR of Lane A.
6. At the end of the phase, in one end records PR: mark the phase done in steps/01_next.md, write the top entry of steps/log.md, rewrite steps/PROGRESS.md to the caps of R400 with line 1 back to STATE WAITING, add the usage row of step 7, merge, and stop.
7. The usage row: run npx ccusage@latest session --since <yesterday as YYYYMMDD>, a day of margin as its session times read as UTC, take the row whose session id is the folder name of this session's scratchpad path or its start, and add it to steps/usage.md, total tokens and cost only. The turns after the run are not in it.

Final message to Bader: plain and short, the PR numbers, the checks with their counts, what is UNKNOWN, and every new question by ID. Its very last line is exactly one of these, alone on its line:
- PHASE DONE <phase id>
- BLOCKED <reason>, when the next Lane A phase waits on another phase
- QUESTION <ids>, only when a question blocks all work of the phase
- LANE DONE, when no Lane A phase is left
