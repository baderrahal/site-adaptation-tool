---
name: round
description: Lead only. Turns a short round goal into acceptance tests and one task file per lane.
---

# Round

Run this in the main repo folder only. Argument: the round goal in one line.

1. Read steps/backlog.md and steps/truth-audit.md for the items the goal touches. Plan first, then continue without waiting.
2. Split the goal into one task per lane, the lanes being build-1, build-2 and revit-test. Two open tasks must not share a folder.
3. Write the acceptance tests in tests/acceptance/ first, each with `@pytest.mark.task("<id>")`. While its task is open a test is owed and may fail, conftest.py marks it xfail so it can merge under the required check. Run them with `--runxfail` to show they fail today for the right reason. They run on GitHub, so their inputs are made up, never a real file. Open a PR that holds them, then run /merge. Lanes do not start until they are on main.
4. Write one file per task in steps/tasks/<id>.md in this shape, on a branch, and merge it the same way:

```
---
id: R1-example
lane: build-1
stage: 1
status: open
allowed:
  - dotnet/SiteAdaptation.Common/
  - python/
---

# Scope
What to build, and what is out of scope.

# Acceptance tests
tests/acceptance/test_example.py

# Done when
A check by a second method, with what it must show.

# Revit check needed
What the revit-test lane must see, or none.

At the end, put your final statement in the PR body and in steps/heads-up/<lane>.md, then push.
```

`stage` is 1, 2, 3 or rules. `allowed` lists folders from the repo root, ending in a slash, or single files. The Edit and Write hook and the folder rules check read it from main, so a lane cannot widen it, and the task must be merged before the lane starts. A shell write passes the hook, the check catches it on the PR.

5. Tell Bader the one line each lane needs: `/task <id>`.

Design is decided in chat. If the goal needs a design nobody approved, stop and ask Bader by question id.
