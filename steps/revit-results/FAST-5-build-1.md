# FAST-5-build-1, Revit check

7 Nov 2026, build-1.

## Opened in Revit

Nothing yet. Revit was not opened by this lane for PR 1 of FAST-5. No revit-wanted file was written, no window lock taken.

## Checked outside Revit

- The real worker CadReader.exe of this branch, built Release net48 and copied to the work root folder fast5-build-1, ran the read for the build on the folder S1 with the config copy of run 1 (project mode rev6). The request was made by a Python script with its own file search by the names of config, sharing no code with FolderRoles or ReadInputs.ForBuild.
- check1, with the data book border: refused, "the plot table needs the location plan, the footprint blocks and the area key of the same request". Fault found, fixed in ReadInputs.ForBuild, which now leaves the border out, and the test R740_the_read_for_the_build_needs_no_schedule... holds a border in its data book.
- check2, with no border: status done, 0 issues, 59 s, outputs read_baseplan.json, read_villaBlocks.json, read_curbs.json, read_workbooks.json, read_roadSurfaces.json and 10 step files, 15 input files marked by the worker. Counts: curbs pieces kept 3450, villaBlocks pieces kept 2699, road surfaces 6, three sheets of 3195 to 3203 rows. baseplan pieces kept 0, as in the past old read of 3 Nov (param_check_fcdd6ddd), which counted 0 too.

## Owed

- Choose project folder, then Build, on an empty model copy of 01A, to the end: floors, levels, villa links and walls placed with no stop at Check. Not seen in Revit. Lead's plan of 10:05 gives it to revit-test as the short check once FAST-5 and FAST-6 are on main, else PR 2 of FAST-5.
