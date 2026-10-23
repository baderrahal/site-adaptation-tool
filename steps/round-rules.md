# Steps for Bader, round 10 (project rules profile)

This round adds the ROSHN Makkah rules as data and a loader for them. Nothing runs in Revit. Steps 1 and 2 get the code, steps 3 to 5 open VS Code and install, steps 6 and 7 run the Python tests, steps 8 and 9 run the .NET tests, step 10 reads the rules.

1. Open GitHub Desktop, choose the repository site-adaptation-tool, make sure the branch is main, click Fetch origin.
   Look for: the button now reads Pull origin.

2. Click Pull origin.
   Look for: the history list shows the merge of the round 10 pull request at the top and the button reads Fetch origin again.

3. Open VS Code, choose File, Open Folder, pick C:\Users\bader\Documents\GitHub\site-adaptation-tool, then choose Terminal, New Terminal.
   Look for: the terminal prompt ends with site-adaptation-tool.

4. In the terminal run:
   `.venv\Scripts\python -m pip install -r requirements.txt`
   Look for: no red error text. Lines that start with Requirement already satisfied are normal.

5. In the Explorer panel on the left open the folder rules.
   Look for: two files, README.md and roshn-makkah.json. If the folder is missing, the pull in step 2 did not finish.

6. In the terminal run the new test:
   `.venv\Scripts\python -m pytest python\tests\test_project_profile.py -v`
   Look for: test_r62_profile_file_loads_with_cluster_q01_of_four_units_and_front_max_two PASSED and 1 passed.

7. In the terminal run all Python tests:
   `.venv\Scripts\python -m pytest python`
   Look for: 202 passed and no failed.

8. In the terminal run the .NET build:
   `dotnet build dotnet\SiteAdaptation.sln -c Release`
   Look for: Build succeeded and 0 Error(s).

9. In the terminal run the .NET tests:
   `dotnet test dotnet\SiteAdaptation.sln -c Release --no-build`
   Look for: a line that starts with Passed! and reads Failed: 0, Passed: 143.

10. In the Explorer panel open rules\README.md.
    Look for: a list of ten checks. Read them against your own notes and tell me if one is worded wrong.

## When the KEO riser height arrives

1. Open rules\roshn-makkah.json, find the line `"riser_height": null` in the steps block.
2. Replace null with the KEO value in mm, a plain number.
   Look for: the line reads `"riser_height": 165` or whatever the KEO value is. Run step 7 again and it still shows 202 passed.

## What remains

- The ten checks in rules/README.md are not coded. This round is data and loader only.
- There is no project settings file yet, so nothing picks the profile by name. The loader takes a path.
- riser_height is null, so check 5 cannot compute steps.
- The Revit run of round 9 (steps/for-bader-round-9.md) is still waiting for you.

## Known bugs

- cluster_split_positions is [2, 3] but D02 and D04 have two units, so position 3 does not exist in them. The file does not say if a position is a unit number or the gap after a unit. Check 3 cannot be coded until you say.
- Check 8 compares a VL4 plot with the standard plot width. The profile holds vl3_width and th1_width and no standard width for VL4.
- The profile is in mm and the Revit file prototypes.json is in metres (R4). A check that joins them must convert, and none does yet.
- full_run.py holds the drawing file name ROSHN Makkah Baseplan.dwg. It is a file name and not a rule value, so I left it.

## What comes next

1. You answer the three questions in the top entry of steps/log.md.
2. Code checks 1 and 2 first. They need only the unit sequence of each cluster in the schedule workbook, so no Revit run.
3. Check 5 waits for the KEO riser height.
