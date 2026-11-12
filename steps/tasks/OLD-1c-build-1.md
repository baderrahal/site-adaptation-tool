---
id: OLD-1c-build-1
lane: build-1
stage: 3
status: done
allowed:
  - python/compare_built.py
  - python/tests/test_compare_built.py
  - config/project.json
  - rules/
  - docs/rules/
  - scripts/checks/
  - steps/compare.md
  - steps/latent.md
---

# Scope

Round 1 step c of steps/plan.md. Read docs/truth.md first, its standing rules and its pass level hold here.

python/compare_built.py, a dev tool, with its test: a built model against the answer key, joined by plot number, the columns and the pass level of Bader's answer of 12 Nov.

The past side is answer-key under the work root, one folder per zone model, 7 of them: key_reads.json and links.csv, one row per villa link with plot, prototype, position, rotation, mirror and origin Z. Read the files to learn their columns, they are outside git and stay there. The neighbourhood of a zone model comes from config/area_codes.json.

The built side is the same kind of read of a built model, made by the read job of the test host, in a folder the script is given. No built model exists yet: the baseline run of 12 Nov built 0 of 9 neighbourhoods, steps/revit-results/OLD-1-revit-test.md. So the script is proven two ways:

1. It first counts each past model against itself, and that must give 100 percent in every column, 7 of 7. This proves the reader and the join.
2. The test, on made up files in the temporary folder of the test, with no real number: a built side with a plot missing, a position off by more than the limit, an FFL off, a rotation off, a mirror wrong, a count off by more than 5 percent, each one named by the row it fails. And one built side just inside every limit that passes.

Columns per neighbourhood: plots built of plots in the past model. Position within the limit, as a share and the largest difference in mm. FFL the same. Rotation within the limit. Mirror right. Floors, yards, walls and levels, built of past. Pass or fail of each column and of the neighbourhood.

The pass level, Bader 12 Nov: 95 percent of the plots of the past model built. Of the built plots 95 percent hold each of position within 5 mm, FFL within 5 mm, rotation within 0.1 degree, mirror right. Floors, yards, walls and levels within 5 percent of the past count. Yards are counted on IMC, IMD, IME and IMF only. IMA and IMB count for plots, position, rotation and mirror only. The limits are values of a new block compare of config/project.json, each with its source "Bader 12 Nov 2026", never a number in code.

Where a column cannot be filled from what the answer key holds, say FFL if the key holds no floor level per plot, the column says UNKNOWN with the reason, never a guess, and the PR names what a read would need to hold. Whether the 7 past models share one coordinate base is measured first, on the past models alone, and the script says so in its output.

The script writes steps/compare.md: counts, shares and differences in mm only, no coordinate, no level, no plot number. The baseline table of this PR: each past model against itself, and the built side as 0 of the plots of each of the 9 neighbourhoods, from the record of the baseline run. A second run on the same files gives the same bytes. A check in scripts/checks, with its test, fails when steps/compare.md holds a line that is not of the form the script writes.

Python only, no C# twin, no pane button, no window. The script writes nothing but steps/compare.md and, if it needs one, a file under the work root. Rule ids R835 to R839. Question ids Q652 to Q654. Take no other id. New config values need python/rule_status.py --write.

# Done when

- The past models against themselves give 100 percent in every column that can be filled, 7 of 7, with the output in the PR.
- The test passes, and each broken case is named by the test.
- steps/compare.md is written by the script, the same bytes on a second run, and the new check passes on it.
- Build and quick tests pass. Every new function has its caller in the PR.
- The PR body starts with `Task: OLD-1c-build-1`.
