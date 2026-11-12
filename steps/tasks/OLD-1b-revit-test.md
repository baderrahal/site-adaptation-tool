---
id: OLD-1b-revit-test
lane: revit-test
stage: 2
status: open
allowed:
  - steps/revit-results/
  - steps/heads-up/revit-test.md
---

# Scope

Round 1 step b of steps/plan.md, the baseline run. It starts when OLD-1p-build-1 is merged. Read docs/truth.md first, its standing rules hold here.

The 3 cards on the whole old project, from an empty model copy under the work root, reading C:\02-Site Adaptation in place with the past output folders skipped by settings. No copy of the input folder and no scenario folder. The road surfaces are the 7 LandXML files in landxml of the work root, made by OLD-1a.

1. Build Release in this worktree from origin/main, install the add-in and the test host for Revit 2024.
2. Hash the input folder before the first run: name, size, write time and SHA-256.
3. Card 1 once: Choose project folder on the input folder, then what the card asks. Record what the tool read: the neighbourhoods it names, the plots, the villa files, the road surfaces it found and where, and every row of Fix items. If the tool does not find the road surfaces under the work root by a setting a person can reach in a window, write the exact words into the record and stop this part for Lead. Change no code and edit no JSON by hand.
4. Card 2 per neighbourhood, one neighbourhood per part, each part under 110 minutes, the model saved at the end of each part. A neighbourhood that stops: its exact words in the record, then the next.
5. Card 3 Report at the end.
6. Hash the input folder again, the run fails on any change.

One Revit at a time, through python/window_lock.py. The driver rule of .claude/rules/revit.md holds, a person may be at this PC. Not known yet: how many neighbourhoods card 1 names and whether one fits in 110 minutes here. The first part measures both, and the record says so before the second part starts. Push the record after each part.

The record is steps/revit-results/OLD-1-revit-test.md: one row per neighbourhood with the time per step and the counts of plots read, plots built, floors by type, levels, links, walls and issues. Counts only, no coordinate, no level, no plot data, the repo is public. The built models stay under the work root for the compare of step c. Take no rule id and no question id. A fault goes into the record as a line for Lead.

# Done when

- The record holds one row per neighbourhood, built or stopped with its exact words, and the Report ran on the last model.
- The input folder is unchanged by SHA-256.
- The PR body starts with `Task: OLD-1b-revit-test` and says what was opened in Revit.
