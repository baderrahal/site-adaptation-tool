# revit-test heads-up, PC-1-revit-test

12 Nov 2026, 03:05. The PR is open and not merged, Lead merges. Detail and proof in steps/revit-results/PC-1-revit-test.md.

## Final statement

- Made: prototypes_2024 under the work root, 13 villa files of format 2024 by rvt_info.py, 13 of 13 runs with log result ok, 39 minutes. Against Prototypes_2024 of the input folder 11 of 13 are equal in size.
- Made: answer-key under the work root, 7 folders, each with the read of its key, key_reads.json and links.csv. 7 of 7 reads with log result ok.
- Counts per key, plots as links with a plot number, villa links, floors, walls, levels: IMC 337, 338, 1,010, 926, 2. IMD 410, 411, 1,230, 1,138, 2. IME 324, 325, 972, 894, 2. IMF 490, 491, 1,470, 1,317, 2. IMG 297, 298, 594, 859, 2. IMA 432, 433, 761, 2,323, 2. IMB 301, 302, 773, 1,515, 2. Floors by type are in the record.
- Not made: the plan of each key and past_all.csv. plan_from_past.py stopped 7 of 7 on `not found: ...\sample_output\round12\full\ROSHN Makkah Baseplan.dwg\plots.json`, past_all.py on `FileNotFoundError ... data_book\location_plan\labels.csv`.
- Input folder: 837 files, gone 0, added 0, changed 0 by SHA-256, by two methods.
- For Lead to accept or refuse: Run-TestHost.ps1 -Upgrade stopped on the gone plan_imc_past.json, which it reads for a file name only. I wrote a stand-in with the 13 file names of the prototype folder and nothing else, ran the 13 upgrades and deleted it. To refuse it, delete prototypes_2024.
- The installed add-in is now the build of this worktree, same commit, the test host install refused the mixed Common DLLs.
- The driver: no click, key or shot by this lane. The watcher pressed Load Once on the 4 boxes of this tool in each of the 20 windows, never Always Load.
