# CHECK-2NOV, parts of steps/bader-fix-1nov.md, run through the real windows

INTERIM, 17:15 on 2 Nov. Parts 11 and 13 still run, part 12 not written. Fails below are as recorded by the helper, not yet verified by Lane B, so a fail here may turn out a fault of the sheet or of the script.

Build under check: add-in dbda940 (code of 24bb1a8), so PR 139 is not in this check. Parts 14 to 16 of the sheet need PR 139 and are not run here.

| Part | Result so far | Open rows |
|---|---|---|
| 1 | Setup done: config refreshed, stray export deleted (Q308, Q309) | |
| 2 | PASS, 5 rows | |
| 3 | FAIL, 4 pass, 1 fail, 1 not reached | Check window has no field column, filter or rampJoint word. Ramps unchanged not reached |
| 4 | FAIL, 7 pass, 3 fail, 1 not reached | No mirror row in Properties. Element id empty in Placed. Second run after a config edit stops at step 1. DT3 instance height not reached |
| 5 | PASS, 6 rows | |
| 6 | PASS, 4 rows | |
| 7 | FAIL, 2 pass, 1 fail | Floor list holds 8 types beyond the 5 of config |
| 8 | PASS, 5 rows | |
| 9 | PASS, 5 rows, 1 not reached | Saves not reached |
| 10 | FAIL, 6 pass, 1 fail | Step 4 in the same session fails, in a new Revit session it passes |
| 11 | Running | Revit crashed at 16:23, about 16 s after OK on Browse For Folder of the Prototypes row, restarted on a copy at 16:53 |
| 12 | Not written | |
| 13 | Not run | |
