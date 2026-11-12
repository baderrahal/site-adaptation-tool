# The lanes, until 12 Nov 2026 16:55

History. From Bader's order of 12 Nov 16:55 Cowork writes all tool code and Claude Code builds, tests, reports, commits and merges, docs/truth.md Who does what. Below are the lines of CLAUDE.md and .claude/rules/revit.md as they stood before, byte for byte, and what the skills /round and /task were for. The builder lanes build-1 and build-2 take no new task. Their worktrees stay, never run git worktree prune without Bader.

## CLAUDE.md, as it stood

## Roles and lanes

Bader decides. Design is decided in chat and Code builds only what is approved. The role comes from the folder, never from a setting:

| Folder | Role | Does |
|---|---|---|
| main repo folder | lead | writes tasks and acceptance tests, merges setup, owns tests/acceptance, golden, holdout |
| worktree named revit-test | revit tester | the only lane that opens Revit |
| worktree named build-1 or build-2 | builder | one task at a time |
| anything else | builder | needs a task whose lane is that folder name |

Trust an answer only if it names proof you can check in the repo.

From How to work and the traps:

- One task per fresh session. Round: /round. Task: /task. Then /merge, /report. Whole repo: /audit, /questions.
- Lead starts a lane with scripts/lanes/run_lane.py, a fresh session in the lane's folder. A message cannot clear a session.
- Run gh merges from the lane's own folder, never chain gh pr merge after a watch, delete the remote branch on its own.
- Never run git worktree prune without Bader. Never build a lane's folder while a runner runs from it, a worker run takes a copy of its build.

## .claude/rules/revit.md, as it stood

- Only the revit-test lane opens Revit. A hook blocks every other lane. For the fast build, until 8 Nov, the lanes .claude/revit-lanes.txt names also open it for their own check before the PR, Bader's order of 7 Nov 10:05.

## The skills

- /round, Lead only: turned a round goal into acceptance tests and one task file per lane in steps/tasks.
- /task: a lane did one task of steps/tasks in its own worktree, inside the allowed list of the task, opened a PR, ran the reviewer and stopped.
- scripts/lanes/run_lane.py started a fresh session in a lane's folder, its log under lanes of the work root.

## The last tasks of the lanes

- build-2: OLD-1d3-build-2, the step rows of Advanced and the numbered ribbon buttons, finished on its branch.
- build-1: OLD-1d4-build-1, stopped part way on 12 Nov 16:55, branch task/OLD-1d4-build-1 commit 6ebc4c33, not reviewed, not merged. The task went to Cowork.
- revit-test: OLD-1e1-revit-test, held on 12 Nov 16:55, its record merged as it stood.
