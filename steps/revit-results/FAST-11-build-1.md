# FAST-11-build-1, Revit check

8 Nov 2026, build-1.

Revit was not opened by this lane. The task allows a check on a copy of the 01A model before 06:00. CHOSEN by Code: no Revit run. The done when is a test on the outputs of a read, and the plan change sits in the worker, which runs outside Revit. Run 2 of revit-test at 06:00 on 01A is the first time Revit sees it, so the PR carries the label revit-later.

## What was run instead, outside Revit

- The worker CadReader.exe of this branch, net48 Release, on the request of the real step 4 Check of short check 2 (check_4_20261107-233358-654 of FAST3-short2-01A in the work root), its outputs sent to a new folder fast11-build-1 under the work root. Nothing of the short check folder was written.
- 30 input files hashed before and after each of the 2 reruns, SHA-256, size and write time: 0 changed, 0 gone, 0 new. inputs_before_after.json of each rerun folder in the work root.
- Counts read by a Python script from the plots and missing lists of plan_01A.json, sharing no code with the C#:

| plan_01A.json | written | not written | left out by the key that stops it | built with a value missing | no road level |
|---|---|---|---|---|---|
| short check 2, 7 Nov | 62 | 216 | mirrored 211, unit 2, transform 2, levels.ffl 1 | 0 | 11 |
| rerun 2 of this branch | 265 | 13 | levels.ffl 9, transform 2, unit 2 | 203, the hand | 11 |

- The mirrored value of the 265 written plots: source missing and not mirrored 203, computed 62. The parameters CASE and Prototype Foundation Type no longer lack on any plot, as the rule tables give them for the hand the plot is built with.

## Not seen in Revit

- The 265 links, floors and ramps placed by steps 5 to 8.
- The card 2 line and the card 3 line in the pane. A net48 test renders the pane view model on a made up work folder.
