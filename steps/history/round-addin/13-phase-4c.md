## Phase 4 part c, PR 4c logic/addin-4c

Branch logic/addin-4c from main at 9c25ec8, equal to origin/main after git fetch, and rebased on b35d26e after PR 52 merged, with no conflict (RAN). R348, R349 and R350 are new, R347 and R388 changed, R349 follows R344 of PR 52. Four changes: the read settings moved into config/project.json (c33f39e), R349 the LandXML read and the road level snap in Common (4005006, adf77a6), R348 to R350 the read command of the worker and the read steps of the parity (adcbdfe, 68ab560), and the time limit of a read from the measured button 2 (fcf5f0e), then the rules (bd5e2ed) and the records. The phase ran in a helper agent started by the main session, which paused it for PR 52 at 70bc691 and resumed it. No Revit or Civil 3D window was opened and the registry was not read or written. Real values stay in Documents\SiteAdaptation\round_addin\parity, this file holds counts, sizes and plot numbers only.

### Originals and inputs (R201, R202)

- 60 originals, the 99 inputs, the 20 extras of 4b and 14 reads of 4c, the three drawings in C:\02-Site Adaptation, the two Rev 9 workbooks, the 7 Road Surface C3D LandXML files of the snapshot, plots.csv and labels.csv, hashed before the first edit and after the last run, 60, 99, 20 and 14 equal (RAN, round_addin\logs\p4c_hash.sh, originals_p4c_before.txt and _after.txt and the others). The worker read copies of the three drawings in round_addin\parity\read4c\inputs, equal to their sources by SHA-256. The override list and the rules profile not changed by 4c (protected_p4c_after.txt, equal to those of PR 52).

### The read settings in config/project.json (change 4 of the brief)

- dataBook.join curb, row and footprint, rev9 workbooks, dlus and sales, data5.areaKey dlus and sales and locationPlan.sales moved from python/c3d_probe.json into dataBook of config/project.json. c3d_run.load_config lays them over the probe settings and refuses a key both hold, plan_from_book.py, majority.py, corner_front.py and key_rules_check.py read the sales sheet from config. The merged probe settings equal those of main on 487 of 487 values (RAN, p4c_settings_move.txt). addin.workbookRoles names the input of each workbook, tbd dlus and accommodation sales. The rest of locationPlan moves with R351 in 4d.
- Tests first (RAN, p4c_settings_first.txt): 4 of 5 failed, the keys still in the probe, no lay_over, the direct reads of c3d_probe.json.

### R349, the LandXML read and the road level snap

- LandXmlReader and TinSurface of Common, landxml_reader.read_landxml, surface_lookup.Surface and c3d_connect.snap_z and road_level step for step, the unit refused as R388 holds it (READ, the code). After the rebase the read holds the snap against siteBuild.snapMaxM as plan_rules.road_snap of R344 does, before any surface is read, and python/read_steps.py takes the snap through road_snap.
- Tests first (RAN, p4c_r349_first_csharp.txt, compile errors on both targets on the missing types, p4c_r344_snap_first.txt and p4c_r344_snap_first_csharp.txt, 1 Python and 6 C# tests failed on the snap check missing).

### R348 to R350, the read command and the read steps

- WorkerReads of CadReader, the command read: the drawings by the CadExtractor, refused for reference only or a header InsUnits not Meters, read_<role>.json at R2, the road surfaces of siteBuild.roadSurfaces in their order, the workbooks by DocumentFormat.OpenXml 3.5.1 (XlsxSheet) as openpyxl reads them, SheetRows and ReadSteps of Common, python/read_steps.py the Python side. A build of the step faces takes the worker read as baseplanRead and refuses a baseplan output not in Meters (READ, the code).
- Tests first (RAN, p4c_read_steps_first.txt, the Python tests stopped at collection on the missing module, p4c_read_steps_first_csharp.txt, 324 compile errors on the missing types). Then the Python and C# tests of 4c passed on net48 and net8.0.
- Found by the first parity run (RAN, p4c_parity_read.txt at fb6f8c3): blocks failed on 2,081 outlines, every one a mirrored block, each the reverse of the other side. The reads of Round 12 were written at 03:04 on 2026-10-24, before 4ac1428 turned the outline of a flipped insert back to counter clockwise at 04:16 (R68). A fresh CadReader read of the three drawings equals the Round 12 reads by SHA-256 but for the layers.json headers, the footprint blocks.json (2,081 outlines reversed, all else equal), the road blocks.json (2,441 reversed and 3 others) and 8 road pieces on layers no build reads, 7 lines one unit of the third decimal apart, 0.001 m, and 1 hatch whose loops differ (RAN, p4c_cadreads.txt). So cadReads.root names sample_output/round4c/full since 4c. The measure on both reads gave majority.json and the override list equal by SHA-256 (RAN, p4c_dry12.txt and p4c_dry4c.txt at fb6f8c3, before the rebase, and again at 429831e, after the rebase and the fixes of the review, majority.json 742a3ea4 and the list d971b39f on both, p4cfix_dry12.txt and p4cfix_dry4c.txt), the build proof at fcf5f0e on the reads of 4c 0 unexplained on the 7 keys, holds 47,569, and the 7 plans equal those of the proof of PR 52 by SHA-256 (RAN, p4c_proof2.txt, p4c_proof2_plans_equal.txt).
- Also found: sales failed 6 cells on net48 alone. double.Parse of net48 read 2264.253152 one unit of the last bit off the nearest double and its format R wrote the double of 2264.2531520000002 as 2264.253152 (RAN, a probe in the scratchpad, parse48). ExactDouble of Common reads the nearest double and writes every double of an output so it reads back the same. surfaces failed on the bounds, which landxml_summary.json holds at 3 decimals, read_steps takes them from the points of the read now. faces from the worker read was refused by parity.py since one role named two files, the worker read is baseplanRead now.

### The parity of the reads (RAN, p4c_parity_read2.py and .txt at adf77a6, round_addin\parity\read4c2)

| Step | Items | Fields equal on each runtime | One unit, failures |
|---|---|---|---|
| labels | 2,962 labels | 11,848 | 0, 0 |
| blocks | 3,780 blocks | 26,460 | 0, 0 |
| curbs | 2,533 pieces | 12,665 | 0, 0 |
| rows | 917 pieces | 4,585 | 0, 0 |
| setbacks | 2,699 pieces | 13,495 | 0, 0 |
| faces from the worker read | 2,704 unit plots | 13,520 | 0, 0 |
| surfaces | 6 surfaces | 84 | 0, 0 |
| roadLevels | 10,307 vertices of 2,557 plots, 6,509 on a road surface, 3,798 on none | 30,921 | 0, 0 |
| areaKey | 3,161 plots | 25,288 | 0, 0 |
| dlusSetbacks | 3,182 rows | 22,274 | 0, 0 |
| sales | 2,591 plots | 5,182 | 0, 0 |

The 30 inputs of the run equal by SHA-256 before and after, the worker answer listing each file equal before and after, 0 date and 0 duration cells in the three sheets read.

- Second method (RAN, p4c_parity_second.ps1 and .txt), PowerShell 7 alone, never parity.py: per step one key set on the three files, and every written field of 20 draws with replacement at seed 14 and 20 at seed 7 equal on Python, net48 and net8.0, 0 differ on every step.
- Load check (RAN, p4c_load_check.txt): a net48 host holding the System.Memory and the other assemblies of Revit 2024 and the DocumentFormat.OpenXml 2.12.3 of Dynamo, no redirect, loaded a library of DocumentFormat.OpenXml 3.5.1 by LoadFrom and wrote and read an xlsx, 3.5.1.0 beside 2.12.3.0. docs/licences.md holds DocumentFormat.OpenXml 3.5.1, its Framework 3.5.1 and System.IO.Packaging 8.0.1 of the net8.0 leg, MIT by the nuspec of each in the cache (READ).

### The read of button 2, its time and its memory (R347)

- A whole read in the worker, the three drawings, the six road surfaces and the two workbooks, no step, three runs per runtime (RAN, p4c_button2_time.txt at adf77a6): net48 20.2, 21.2 and 21.0 s, peak private 3,069, 3,595 and 3,575 MiB, net8.0 16.9, 16.8 and 17.0 s, peak 3,891, 3,843 and 3,578 MiB. addin.workerTimeLimitS.read is 300 s, about 14 times the longest, 600 s before (CHOSEN).
- The peak said plainly: R347 holds the free memory against the peak of each large input alone, the Road Layout 2,612 MiB plus the floor of 2.0 GiB, 4,660 MiB, while a whole read peaks 457 to 983 MiB higher on net48 and 966 to 1,279 MiB on net8.0, so a read started with 4,660 MiB free could end near 1 GiB under the floor. Each run here started with 5,442 to 5,571 MiB free and none went under 1,964 MiB free after. Q083 asked, closed by the review of PR 53 (below).

### Not done in this PR

- The location plan, the plot table and the rest of locationPlan (4d). The read writes no triangles of the road surfaces, a build of 4f reads them again or 4d writes them with the plot table. The read of a road surface takes the first surface of each file, as plan_from_book.load_roads does. CLAUDE.md is not changed, the lines it should gain are in the report of the PR.
- Kept as known limits, CHOSEN: a P or F of NaN or infinity is a bad point in C# where Python float() keeps it, a cell of a duration style keeps its number in C# where openpyxl gives a timedelta, which read_steps.py refuses since the review of PR 53, so no step is written on either side of such a sheet, none of them in the files read (counts above). A Unicode digit of a plot text, a digit by char.IsDigit here, is no digit on either side since that review.

### Questions

- Q083 new: the memory of a whole read of button 2, closed in the review of PR 53. Q084 new in that review: the free memory button 2 needs. The same text in steps/PROGRESS.md and the top entry of steps/log.md, the full text in steps/questions-closed.md.

Gate: at the head of the PR, in the PR body and round_addin\logs\p4c_gate_*.txt.

### Review of PR 53, the breaker and the claim-checker

The breaker's probes in the scratchpad of the session (gen.py, cs\Program.cs, probe\cells_py.json, m12, m4c) found nine places where the C# went apart from openpyxl or the Python readers with no failure or the memory check fell short, and the claim-checker five records. Fixed in 6a0caf1 and 429831e, each with a failing test first (RAN, round_addin\logs\p4cfix_first_python.txt, 9 of 10 Python tests failed, the tenth the Python half of a C# case, and p4cfix_first_csharp.txt, of the tests that compiled 27 of 31 failed on net48 and 3 of 31 on net8.0, and 6 of the 10 exponent cases on net8.0, the rest of ReadEdgeTests.cs did not compile on the missing types). Tests: dotnet/SiteAdaptation.Tests/ReadEdgeTests.cs and python/tests/test_read_edges.py. No Revit or Civil 3D window was opened.

1. A cell of type d: XlsxSheet.FromIso8601 is openpyxl from_ISO8601, a date, a time or a datetime by what the text holds (CellTimeKind), a duration refused on both sides, read_steps.jsonable refusing a timedelta. Test R350_a_date_cell_of_type_d_reads_as_openpyxl_from_iso8601_gives_it on both sides.
2. A date in a key column: SheetRows.Rows keeps the cells, ToJson writes them, so ByPlot and ReadSteps.SetbackKey take the text Python str() gives, 2024-03-05 00:00:00. Test R350_a_date_in_a_key_column_gives_the_text_python_str_gives.
3. Digits: the ten ASCII digits on both sides, area_key.zone_text by [0-9] and by_plot by isascii, SheetRows.IsAsciiDigits (CHOSEN, net48 knows fewer Unicode digits than Python 3.12). Test R350_a_zone_or_plot_digit_is_an_ascii_digit_on_both_sides.
4. repr: ExactDouble.Shortest on the exact value, 0 of 199,961 of the breaker's doubles differ from Python repr on net48 and on net8.0, 817 on net48 before (RAN, fix4c\reprlist in the scratchpad, repr48_before.txt and repr48_after.txt). SheetRows.PyRound reads its large quotient by ExactDouble too. Tests R350_repr_of_a_double_is_the_shortest_text_that_reads_back_as_python_repr_gives, 27 doubles, and R350_repr_of_random_doubles_reads_back_as_the_same_double, 20,000 draws, its digits equal to the format R on net8.0.
5. A dimension out of date: a value past the stated dimension refused on both sides, rev9_workbooks.sheet_values and XlsxSheet, a table of no row refused by the worker read with exit 2 and by read_steps.py. The made up sheet of ReadStepTests whose E1 held 9 past A1:D5 holds a cell of no value there now. Tests R350_a_sheet_with_a_value_past_its_stated_dimension_is_refused, R350_the_worker_read_refuses_a_table_of_no_row and test_r350_read_steps_refuses_a_table_of_no_row.
6. Memory, Q083: addin.workerPeakMiB.commands.read with its reason, held with the floor before the first read and as the rest of it before each read (WorkerMemory.CommandRefusal, WorkerContext). 3,900 MiB first, the net8.0 peak of 4c rounded up, then 4,100 since a whole read after the fixes peaked at 4,008 MiB on net48. Test R347_q083_free_memory_is_held_against_the_whole_read_before_the_first_read_and_against_its_rest_before_each_read and test_r347_q083_the_config_names_the_peak_of_a_whole_read_with_its_reason. Q083 closed in steps/questions-closed.md.
7. An exponent past the doubles: ExactDouble.Parse holds the place of the first digit against 309 and -324 before any BigInteger work, inf or 0.0 as Python float gives. Test R350_a_number_text_of_any_exponent_reads_at_once_as_python_float_gives, 10 texts, each under 1 s.
8. One workbook key per setting: dataBook.locationPlan.sales.workbook and the locationPlan.dlus of the probe settings name accommodation, location_plan.py and location_plan_check.py take the file of dataBook.rev9.workbooks, a setting of no key refused on both sides, the sales rows take their own, parity.workbook_inputs refuses a workbook input other than the file config names. Tests R350_the_sales_rows_take_the_workbook_their_own_setting_names, test_r350_every_workbook_setting_names_a_key_of_the_rev9_workbooks, test_r350_read_steps_takes_the_sales_rows_from_the_workbook_their_setting_names and test_r350_parity_refuses_a_workbook_input_other_than_the_file_config_names.
9. The probe paths: c3d_run.cad_reads takes connect.baseplanPlots and the first fallback folder from cadReads, the probe holds no baseplanPlots, connect.landxmlDir and the last fallback stay with their reason in connect.cadReadsNote. Test test_r388_the_probe_settings_take_the_baseplan_and_the_fallback_reads_through_cad_reads.

Reruns (RAN):

- Read parity at 429831e (p4cfix_parity_read.py and .txt, round_addin\parity\read4cfix): every one of the 11 steps on net48 and net8.0 with the items and fields of the table above, 0 one unit cases, 0 failures, 31 inputs equal before and after. Both sides took a copy of config with siteBuild.minFreeGB 0.5, every other value equal, since the PC had 4,758 MiB free and config asks 6,148. Before it, a run at 429831e with the floor 1.0 was refused with exit 3 on both runtimes at 4.52 and 4.51 GiB free, which deleted the C# step files of the run at 6a0caf1 (p4cfix_parity_read_429831e_refused.txt), and that run at 6a0caf1 with the floor 1.0 held on every step (p4cfix_parity_read_6a0caf1.txt). Second method by PowerShell 7 alone, 20 draws with replacement at seeds 14 and 7 per step, 0 differ on 11 steps (p4cfix_parity_second.ps1 and .txt).
- The measure at 429831e on the reads of Round 12 and of 4c, see above, the hashes the breaker got at 0a58496.
- Button 2 at 6a0caf1 with the floor 2.0 (p4cfix_button2_time.txt): net48 45.3 to 46.7 s, peak 3,931 to 4,008 MiB, net8.0 31.8 to 32.3 s, peak 3,576 to 3,584 MiB. The CadReader of 0a58496 from a worktree in the scratchpad and the head in turn on one config copy (p4cfix_time_compare.py, _floor2.txt and _floor1.txt): 0a58496 on net48 20.9 to 47.0 s, peak 2,752 to 3,899 MiB, on net8.0 21.5 to 30.6 s, the head 21.2 and 41.6 s on net48, peak 3,585 and 3,935, so the time and the peak move with the state of the PC and not with the fixes. 6 of the 8 runs of the head there were refused with exit 3, 3 with the floor 2.0 and 3 with 1.0, one by the rest of the peak 20.8 s into its read, the PC 4.4 to 6.6 GiB free (Q084).
- The breaker's workbook probe\cells.xlsx: by_plot of C# and Python equal on 7 of 7 plots, stale.xlsx refused by both.
- The load check of 4c: the six files of its host equal their Revit 2024 and Dynamo sources by SHA-256, 6 of 6 (p4cfix_load_hashes.txt), docs/licences.md says so.
- Originals and inputs hashed before the first edit and after the last run, 60, 99, 32 and 14 equal, the override list and the rules profile not changed (p4cfix_hash.sh, the extras with the reads of round4c added).

Records: the measure rerun with its head, the five files of licences.md hashed, the sentence on a parity at 3.29 GiB free dropped since no log held it, 20 draws with replacement, the 8 road pieces as 7 lines and 1 hatch, Q083 closed, the Linux leg of 0a58496 skips 54 (READ, the log of its CI run).

Not done: a cell of a duration style still keeps its number in C# (above). CLAUDE.md is not changed, the lines it should gain are in the report of the review.

Gate: at the head of the PR, in the PR body and round_addin\logs\p4cfix_gate_*.txt.

