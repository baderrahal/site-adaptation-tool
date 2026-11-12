# The plan from 12 Nov 2026

The one live plan. Bader's order of 12 Nov: the tool builds the whole old project, every neighbourhood of Phase 1, from an empty model, and each result is measured against the past Revit models the team built. Phase 3 waits. steps/plan-fast-3days.md is the old plan of 6 to 8 Nov, kept as a record.

Nothing of this plan starts until Bader answers steps/bader-form-12nov.md. The order below is the recommended one, item 1 of the form. Each task becomes a file in steps/tasks by /round when its turn comes, one open task per lane.

Where it starts from, steps/audit-12nov.md and steps/old-project-pc.md: one neighbourhood, 01A, was built from an empty model on the laptop on 8 Nov, 263 of 278 plots, with 0 yard floors and the mirror value missing on 211 plots. Nothing built was ever held against a past model in Revit. This PC has 16 GB, one Revit at a time, and no speed number of the laptop holds here.

Words: the answer key is what the past models of Revit All Zones hold, read into answer-key under the work root by task PC-1-revit-test. A compare number is a count of the compare table of task OLD-2, never a coordinate or a level.

## Order

| # | Task | Lane | Starts when |
|---|---|---|---|
| 0 | PC-2 the road surfaces as LandXML again | revit-test | Bader answers the form |
| 1 | OLD-1 the full old project run | revit-test | PC-1-revit-test and PC-2 are done |
| 2 | OLD-2 the compare | build-1 | With OLD-1, it needs only the answer key and one built model |
| 3 | OLD-3 to OLD-9 the fixes, worst first by the compare | build-1, build-2, build-3 | The first compare table is in steps/compare.md |
| 4 | NEW-1 the new project path, then ROAD-1, then INST-1 | build-2, build-3, build-1 | The old project meets the pass level of the form |

## PC-2, the road surfaces as LandXML again

- Lane: revit-test, it holds the one window of Revit or Civil 3D.
- What: the input folder holds no LandXML file on this PC (steps/old-project-pc.md section 1). The 18 files the runs of 6 to 8 Nov took their road levels from were exports of the road drawings into the old work root, which is gone. Export them again with Civil 3D 2026 through the probe of dotnet/SiteAdaptation.C3DProbe and python/c3d_run.py, on copies of the 4 drawings of Roads under the work root, and put the files into S1 of the work root with a new manifest.
- Files: steps/revit-results/PC-2-revit-test.md. No code, unless the probe does not build against Civil 3D 2026 on this PC, then that fix alone.
- Rule ids: none.
- Done when: the count of surfaces exported is in the record, each named in siteBuild.roadSurfaces of config/project.json is there, and the 4 drawings are unchanged by SHA-256.
- Compare number: none. Without it OLD-1 builds no plot.

## OLD-1, the full old project run

- Lane: revit-test.
- What: every neighbourhood of Phase 1 built from an empty model copy. Card 1 once for the whole folder, on scenario S1 of the work root, which holds no past output and no past model. Then card 2 per neighbourhood, one neighbourhood per part, each part under 110 minutes, the model saved at the end of each part. Report at the end. A neighbourhood that stops is recorded with its exact words and the run carries on with the next.
- Files: steps/revit-results/OLD-1-revit-test.md, steps/heads-up/revit-test.md. No code.
- Rule ids: none.
- Done when: the record holds one row per neighbourhood with its time per step and its counts of plots read, plots built, floors by type, levels, links, walls and issues, and the Report ran on the last model. The input folder is unchanged by SHA-256.
- Compare number it must reach: none, it is the first measure. Its numbers fill the Built column of steps/compare.md.
- Not known yet: how many neighbourhoods card 1 names, and whether one neighbourhood fits in 110 minutes on this PC. The first part measures both and the record says so before the second part starts.

## OLD-2, the compare

- Lane: build-1.
- What: one script that reads a built model, through the read job of the test host that already reads a past model, and the answer key, and writes one row per neighbourhood and element. Counts only.
- The join: a built plot and a past plot are the same plot when their plot number is the same, the past side from links.csv of answer-key. The past models hold 2,591 villa links with a plot number in 7 models (steps/old-project-pc.md section 4). Whether the 7 models share one coordinate base is the first thing the script measures, on the past models alone.
- Columns: plots built of plots in the past model. Villa position difference in mm, as the share within the pass level and the largest. Rotation right. Mirror right. FFL difference in mm, share within the pass level and the largest. Floors by type, built of past. Yard floors, built of past. Walls, built of past. Levels, built of past.
- Files: python/compare_built.py and its test under python/tests, a C# twin only if a pane button is to show it, which this task does not ask. steps/compare.md is written by the script, never by hand. The neighbourhood of a past model comes from config/area_codes.json, the pass levels from a new block compare of config/project.json.
- Rule ids: R830 to R839. Question ids: Q650 to Q654.
- Done when: the script runs on the 01A model of OLD-1 and on a made up pair of files in the test, steps/compare.md holds the table, and a second run on the same files gives the same bytes. No real coordinate or level in git, a check of scripts/checks proves it on the file.
- Yard floors are counted only against IMC, IMD, IME and IMF. IMG holds none, and IMA and IMB, the structural models, hold no yard and no ramp floor and share one set of 733 generic floors, so their floors and walls are left out of the pass level and shown apart.
- Compare number it must reach: none of its own. It must count the past model against itself as all right, 100 percent in every column, which proves the reader and the join.

## OLD-3 to OLD-9, the fixes, worst first

The order is by the numbers of 8 Nov and is set again by the first compare table. Each fix is rerun on its neighbourhood by revit-test and compared again, and its task is done only when its compare number is reached on 01A and on one neighbourhood it was not tuned on.

| Task | Lane | What | Files | Rule ids | Done when, the compare number |
|---|---|---|---|---|---|
| OLD-3 yard floors | build-1 | 0 yard floors placed so far. R410 holds the back yard floor off under Rev 06 because the manual gives no level, Q225. Item 3 of the form | dotnet/SiteAdaptation.Common/Floors, the rules profile, python twin, tests | R840 to R844 | Yard floors built of past at the pass level of the form, and no generic or ramp floor lost |
| OLD-4 mirror direction | build-3 | The plan lacks the mirror value on 211 of 278 plots of 01A. The hand of each prototype has no source | dotnet/SiteAdaptation.Common/Plan, Read, the confirm window, python twin, tests | R845 to R849 | Mirror right at the pass level of the form, counted against the past links |
| OLD-5 CASE and foundation type | build-3, after OLD-4 | They follow an assumed hand on about 201 plots. No run counted the values on the links in Revit | dotnet/SiteAdaptation.Common/Plan, the parameter step, tests | R850 to R854 | Each shared link parameter equal to the past link on the plots whose mirror is right, at the pass level |
| OLD-6 plots with no road level | build-2 | 13 of 278 plots of 01A have no road level, 15 are not built. Q600, two road surfaces joined, item 4 of the form | dotnet/SiteAdaptation.Common/Roads, Floors, config/road_surfaces.json, tests | R855 to R859 | Plots built of plots in the past model at the pass level. FFL within the pass level on the plots that had no road level |
| OLD-7 F35 to F39 | build-1 | The pane faults that stop or mislead a Build: prototypes row, grey Review data, reuse box words, step 7 box line, scope on reopen | dotnet/SiteAdaptation.AddIn/Pane, App, tests | R860 to R864 | OLD-1 runs a neighbourhood with no extra press and no stop of F35. Not a compare number |
| OLD-8 F18 to F21 | build-2 | Advanced run 2 reads the past job folder, a read has no Stop, Cancel loses the read, step 1 Done over reopen | dotnet/SiteAdaptation.AddIn/Commands, Pane, the worker, tests | R865 to R869 | A read stops within 10 s of Stop and keeps what it read. Seen in Revit. Not a compare number |
| OLD-9 F23 | build-1 | A scope change undoes step 2 with no line | dotnet/SiteAdaptation.AddIn/Pane, tests | R870 to R871 | The pane says so in one line. Seen in Revit |

Also by the audit, small, each a line of the task of its lane: the rule lines R689 to R691 written, ClusterUnconfirmed wired or removed, the i of the card heads. By the run of PC-1: Run-TestHost.ps1 -Upgrade takes the file names from the prototype folder and not from plan_imc_past.json (line 493), and the first Release build of a fresh worktree failed once on a baml file and passed on the second try, cause UNKNOWN.

## After the old project compares right

| Task | Lane | What | Files | Rule ids | Done when |
|---|---|---|---|---|---|
| NEW-1 the new project path | build-2 | F40, F41, F42 on the Phase 3 folder: the Baseplan from the folder, Use CAD finds the plot outlines, an origin of another UTM zone | dotnet/SiteAdaptation.AddIn/Commands, Common/Read, SiteSpec, tests | R872 to R879 | Card 1 passes on the Phase 3 folder with no typed position, seen in Revit |
| ROAD-1 roads from 3D solids | build-3 | Road levels where a project has 3D solids and no Land XML | Common/Roads, the worker, tests | R880 to R889 | Road levels for the plots of a folder with no Land XML, held against the old project's Land XML levels on the same plots at the FFL pass level |
| INST-1 an installer the team runs | build-1 | One file a team member runs with no VS Code and no repo, signed, F11 | dotnet/SiteAdaptation.AddIn install, scripts/sign, docs/design/08-install.md | R890 to R894 | A second Windows user of this PC installs and builds one neighbourhood with no box of an unsigned add-in |

## Rules of the plan

- One Revit at a time. The input folder is read only, SHA-256 before and after every run.
- Never tune and test on the same data: every fix is counted on 01A and on one neighbourhood it was not tuned on.
- A compare number is reached only in steps/compare.md, written by the script from a model built from an empty copy.
- A fix on its third attempt stops and goes back to measuring.
