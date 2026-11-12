---
id: OLD-1e1-revit-test
lane: revit-test
stage: 2
status: open
allowed:
  - steps/revit-results/
  - steps/heads-up/revit-test.md
  - steps/compare.md
---

# Scope

Round 1 of steps/plan.md, a first run of step e, made as soon as part 1 of step d is merged, so the next stop of the Build is known early. The run of step e after parts 3 and 4 is its own task. Read docs/truth.md first, its standing rules hold here.

The baseline of 12 Nov built 0 of 9 neighbourhoods on one cause, the data book. PR 305 took that cause away (R840). Whether the Build then builds was not seen in Revit.

1. Build Release in this worktree from origin/main, install the add-in and the test host for Revit 2024.
2. Hash the input folder C:\02-Site Adaptation before the first run, name, size, write time and SHA-256.
3. Start from a copy of the model that holds card 1 of the baseline, old-1b\OLD1-card1.rvt under the work root, with its work folder, if the tool takes it. If it does not, card 1 again from an empty model copy as in OLD-1b, and say which.
4. Card 2 Build on neighbourhood 01A first. Record the time per step and the counts: plots read, plots built, floors by type, levels, links, walls, issues. If it stops, its exact words, the code line that writes them, and stop the Builds there: one stop that is not of a neighbourhood stops all 9, as on 12 Nov.
5. If 01A builds: the read of the built model, the same read that made answer-key (steps/revit-results/PC-1-revit-test.md), into built\01A under the work root: links.csv with plot, originX, originY, originZ, rotationDeg, mirrored in metres and degrees, and key_reads.json as python/key_reads.py writes it. Then `python python/compare_built.py` with the built folder, and the row of 01A in the record. steps/compare.md is written by the script alone.
6. Then the next neighbourhoods in this order while the session lasts: 012, 013, 02, 03, 021, 022, then 010 and 01B. One per part, each part under 110 minutes, the model saved at the end of each part, the read and the compare after each. A neighbourhood that stops: its exact words, then the next.
7. Card 3 Report at the end, on the last model.
8. Hash the input folder again, the run fails on any change.

One Revit at a time, through python/window_lock.py, this PC has 16 GB. The driver rule of .claude/rules/revit.md holds, a person may be at this PC. Change no code and edit no JSON by hand. A fault is a line for Lead. Push the record after each part.

The record is steps/revit-results/OLD-1e1-revit-test.md. Counts, times and differences in mm only, no coordinate, no level, no plot number, the repo is public. Take no rule id and no question id.

# Done when

- The record holds 01A built with its counts and its compare row, or stopped with its exact words and the code line.
- Each further neighbourhood that was run has its row.
- The input folder is unchanged by SHA-256.
- The PR body starts with `Task: OLD-1e1-revit-test` and says what was opened in Revit.
