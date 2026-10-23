# Round 10 detail

Moved from steps/log.md in round 11. The run sheet of that round is steps/history/round-10-steps.md.

## Round 10, 2026-10-24, ROSHN Makkah rules profile and loader

Done:
- Branch round/roshn-profile from main at 5b4414e. This session started in C:\Users\bader, outside the repo, so the main guard hook was NOT live. I never committed to main. The untracked folder Claude outputs/ is not mine and stays out of the commit. R62 was free
- rules/roshn-makkah.json holds the content as Bader gave it, keys unchanged, 13 top level keys, riser_height null. A script confirmed it parses and holds no semicolon and no em dash
- python/project_profile.py: load_profile(path) and profile.get("clusters.Q01"). A missing file, bad JSON, a file that is not one object and a missing key each raise ProfileError with the file name. A key that holds null returns None. The loader and the test logic hold no project key and no project value. A search of the Python and C# source outside the tests for the typology codes, cluster ids and profile key names found only older lines: DP4 in a comment and the Generic 150mm type name in link_summary.py, and drawing file names with ROSHN and Makkah in full_run.py. They are not profile values and I left them
- One test, test_r62_profile_file_loads_with_cluster_q01_of_four_units_and_front_max_two. It checks Q01 has 4 units, front_max is 2.0, a missing key raises with its name in the message and a null in a temp file reads as None. The missing file, bad JSON and not-an-object paths have no test. Mutation check, run after the review fixes below: Q01 cut to 3 units failed it, front_max 2.5 failed it, a loader that returns None for a missing key failed it, and both files were put back and compared equal
- rules/README.md with the ten checks, R62 in docs/rules.md, two lines in CLAUDE.md, steps/round-rules.md with 13 steps, the round 9 detail moved to steps/history/round-9.md, PROGRESS.md rewritten
- A fresh reviewer agent read the pull request diff (it ran nothing). I fixed what held up: the README and R62 said the tool already loads the profile and no code holds a value, now they say what is true, check 5 is back to the wording Bader gave, checks 4 and 5 name their full keys because front_min and front_max exist twice, the steps are one action each with git commands, the invented riser example is gone, the loader reads a file with a byte order mark and wraps read errors, the error match is tested, and the log no longer claims what the repo cannot show about checks 1 and 2
- Gate after the fixes: Python 202 passed (201 before), .NET 143 passed (143 before), build 0 errors

Remains:
- The ten checks are not coded. There is no project settings file, so nothing picks the profile by name and the loader takes a path
- riser_height is null until the KEO value is supplied, so check 5 cannot compute steps
- Bader's Revit run of round 9 (steps/for-bader-round-9.md) is still waiting

Known bugs:
- The values are as pasted. The source report 380159-SAPL-ROSHN-DSN-RPT-000252 is not in the repo, so I did not check them against it
- cluster_split_positions is [2, 3] but D02 and D04 have two units. The file does not say what a position counts, so check 3 cannot be coded
- The profile has vl3_width and th1_width and no standard width for VL4, so check 8 has nothing to compare against
- The profile is in mm and prototypes.json is in metres (R4). No check joins them yet, the first one must convert
- The ten checks sit in rules/README.md as asked, while docs/rules.md says it is the only source of rules. R62 points to the README for now
- A duplicate key in the JSON silently takes the last value, the loader does not warn

Next:
- After the answers below, find where the cluster of a plot and the order of its units come from, then code checks 1 and 2. UNKNOWN whether the schedule workbook has it, the columns the rules name hold no cluster

Your turn:
1. Split positions. Recommended: position n is the wall between unit n and unit n+1, counted from 1. Then Q01 and Q02 can split between units 2 and 3 or between 3 and 4, and D02 and D04 cannot split. Say if the manual means something else
2. VL4 standard plot width. Recommended: give me the width in mm from the manual and I add it as plot.vl4_width. Without it check 8 cannot run
3. Project settings. Recommended: a file config/project.json with one line, profile, that names rules/roshn-makkah.json, read by the full run and by every check. Say yes or no
4. Where the ten checks live. Recommended: keep them in rules/README.md until each one is coded, then move it into docs/rules.md as R63 to R72 together with its test
5. Cluster membership. Recommended: tell me which file or sheet says which plots form a cluster and in what order. Without it checks 1 and 2 have no input
6. Run steps/round-rules.md, 13 steps, and tell me if a Look for line did not match
7. The older open questions, the yard rule and the ramp slope, are in steps/history/round-9.md
