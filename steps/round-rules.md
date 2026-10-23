# Steps for Bader, round 10 (project rules profile)

This round adds the ROSHN Makkah rules as data and a loader for them. Nothing runs in Revit. Steps 1 to 4 open the folder and the terminal, steps 5 and 6 pull main, step 7 installs, steps 8 to 13 check the new files and run the tests.

1. Open VS Code.
   Look for: the VS Code window opens.

2. Choose File, then Open Folder.
   Look for: a folder picker opens.

3. Pick C:\Users\bader\Documents\GitHub\site-adaptation-tool and click Select Folder.
   Look for: the Explorer panel on the left shows CLAUDE.md, python and steps.

4. Choose Terminal, then New Terminal.
   Look for: a prompt that contains site-adaptation-tool before the > sign.

5. In the terminal run:
   `git switch main`
   Look for: Already on 'main' or Switched to branch 'main'. If git says local changes would be overwritten, stop and tell me.

6. In the terminal run:
   `git pull`
   Look for: Fast-forward and a list of files that includes rules/roshn-makkah.json. If it says Already up to date, the pull was done before and that is fine.

7. In the terminal run:
   `.venv\Scripts\python -m pip install -r requirements.txt`
   Look for: no red error text. Lines that start with Requirement already satisfied are normal.

8. In the Explorer panel open the folder rules.
   Look for: two files, README.md and roshn-makkah.json. If the folder is missing, step 6 did not pull the round.

9. In the terminal run the new test:
   `.venv\Scripts\python -m pytest python\tests\test_project_profile.py -v`
   Look for: test_r62_profile_file_loads_with_cluster_q01_of_four_units_and_front_max_two PASSED and 1 passed.

10. In the terminal run all Python tests:
    `.venv\Scripts\python -m pytest python`
    Look for: 202 passed and no failed. A machine without Git Bash also shows some skipped, which is fine.

11. In the terminal run the .NET build:
    `dotnet build dotnet\SiteAdaptation.sln -c Release`
    Look for: Build succeeded and 0 Error(s).

12. In the terminal run the .NET tests:
    `dotnet test dotnet\SiteAdaptation.sln -c Release --no-build`
    Look for: a line that starts with Passed! and has 0 after Failed: and 143 after Passed:.

13. In the Explorer panel open rules\README.md.
    Look for: ten numbered checks. Send me the number of any check that is worded wrong.

## When the KEO riser height arrives

1. In the Explorer panel open rules\roshn-makkah.json.
   Look for: the file opens and the first line holds "project".

2. Press Ctrl+F and type riser_height.
   Look for: one match, the line `"riser_height": null,` inside the steps block.

3. Replace the word null with the KEO value in mm, a plain number.
   Look for: the line reads "riser_height": then your number and a comma.

4. Press Ctrl+S.
   Look for: the dot on the file tab goes away.

5. In the terminal run step 10 again.
   Look for: the same pass count as before and no failed.

## What remains

- The ten checks in rules/README.md are not coded. This round is data and loader only.
- There is no project settings file yet, so nothing picks the profile by name. The loader takes a path.
- riser_height is null, so check 5 cannot compute steps.
- The repo does not show where the cluster of a plot comes from, so checks 1 and 2 have no data source yet.
- The Revit run of round 9 (steps/for-bader-round-9.md) is still waiting for you.

## Known bugs

- cluster_split_positions is [2, 3] but D02 and D04 have two units, so position 3 does not exist in them. The file does not say if a position is a unit number or the gap after a unit. Check 3 cannot be coded until you say.
- Check 8 compares a VL4 plot with the standard plot width. The profile holds vl3_width and th1_width and no standard width for VL4.
- front_min and front_max exist twice in the profile, under slopes_percent and under steps. The README names the full key for checks 4 and 5.
- The profile is in mm and the Revit file prototypes.json is in metres (R4). A check that joins them must convert, and none does yet.
- The values are as pasted. The source report is not in the repo, so they were not checked against it.
- Older rule constants are still in code, for example the Generic floor type name in python/link_summary.py, and full_run.py holds drawing file names with ROSHN and Makkah. They are not profile values and I left them.

## What comes next

1. You answer the open questions in the top entry of steps/log.md.
2. Find where cluster membership and plot adjacency come from, then code checks 1 and 2.
3. Check 5 waits for the KEO riser height.
