---
name: task
description: A lane does one task from steps/tasks, opens a PR, runs the reviewer, merges and stops.
---

# Task

Argument: the task id. One task per fresh session.

1. Run `git fetch origin` and start a branch `task/<id>` from origin/main.
2. Read steps/tasks/<id>.md. If its lane is not this folder's name, stop and say so. Plan first, then continue without waiting.
3. Push your branch every 15 minutes while working. Build only inside `allowed`. The acceptance tests are the lead's, never change them. If one looks wrong, stop and say why.
4. Set `status: done` in the task file, which turns its acceptance tests from owed to hard. Run `pwsh scripts/quick-tests.ps1` and `python -m pytest tests/acceptance`. Show the output.
5. Check the result a second way that shares no code with what you built.
6. Open a ready PR with this body:

```
Task: <id>

## What changed

## Callers
Each new function, then who calls it.

## Proof
File and line, or command output. Tag anything not proven UNKNOWN. Write "not seen in Revit" where that is so.

## Source tags
New values and their tag: computed, rule, user pick, missing, past.
```

7. Write the diff, the PR body and the test output to files in your scratch folder and run the reviewer agent on them. For changes to build logic, Excel numbers, overrides or Revit model writes also run the breaker agent. Fix a real fault with a test, then review again once.
8. Run /merge, then stop. Do not start another task in this session.

A PR touching Revit add-in files needs a file in steps/revit-results/, or the label revit-later when the revit-test lane still owes the check. A fix on its third attempt stops and goes back to measuring.

At the end, put your final statement in the PR body and in steps/heads-up/<lane>.md, then push.
