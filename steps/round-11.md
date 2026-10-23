# Steps for Bader, round 11 (project settings, clusters, checks 1 and 2)

This round adds config/project.json, the cluster step python/cad_analysis.py and the first two coded checks. Nothing runs in Revit. Do it after pull request 13 is merged, the top entry of steps/log.md says when. Steps 1 to 4 open the folder and the terminal, steps 5 and 6 switch to main and pull it, step 7 installs, steps 8 to 11 run the tests, steps 12 and 13 run the cluster step on the real baseplan, steps 14 to 20 read analysis.json, steps 21 and 22 check the two small file changes.

1. Open VS Code.
   Look for: the VS Code window opens.

2. Choose File, then Open Folder.
   Look for: a folder picker opens.

3. Pick C:\Users\bader\Documents\GitHub\site-adaptation-tool and click Select Folder.
   Look for: the Explorer panel on the left shows CLAUDE.md, config, python and steps.

4. Choose Terminal, then New Terminal.
   Look for: a prompt that contains site-adaptation-tool before the > sign.

5. In the terminal run:
   `git switch main`
   Look for: Already on 'main' or Switched to branch 'main'. If git says local changes would be overwritten, stop and tell me.

6. In the terminal run:
   `git pull`
   Look for: Fast-forward and a list of files that includes python/cad_analysis.py and config/project.json. If it says Already up to date, the pull was done before and that is fine.

7. In the terminal run:
   `.venv\Scripts\python -m pip install -r requirements.txt`
   Look for: no red error text. Lines that start with Requirement already satisfied are normal.

8. In the terminal run the new tests:
   `.venv\Scripts\python -m pytest python\tests\test_cad_analysis.py python\tests\test_rules_checks.py python\tests\test_project_profile.py -v`
   Look for: 24 passed and no failed. The names start with test_r64, test_r65, test_r66 and test_r62.

9. In the terminal run all Python tests:
   `.venv\Scripts\python -m pytest python`
   Look for: 229 passed and no failed. A machine without Git Bash also shows some skipped, which is fine.

10. In the terminal run the .NET build:
    `dotnet build dotnet\SiteAdaptation.sln -c Release`
    Look for: Build succeeded and 0 Error(s).

11. In the terminal run the .NET tests:
    `dotnet test dotnet\SiteAdaptation.sln -c Release --no-build`
    Look for: a line that starts with Passed! and has 0 after Failed: and 143 after Passed:. No C# code changed this round, so the count is the same as before.

12. In the terminal read the baseplan with the CAD reader. The drawing is only read:
    `dotnet\CadReader\bin\Release\net48\CadReader.exe "C:\02-Site Adaptation\Base Plans\ROSHN Makkah Baseplan.dwg" sample_output\round11\baseplan --layers config\cad_layers.json`
    Look for: the line layers 48, pieces 12458, plots 2962, other faces 29, texts 3318.

13. In the terminal run the cluster step:
    `.venv\Scripts\python python\cad_analysis.py sample_output\round11\analysis --cad sample_output\round11\baseplan`
    Look for: the line clusters 330 {"D02": 85, "D04": 98, "Q01": 4, "Q02": 2, "unknown": 141}, issues 577.

14. In the Explorer panel open sample_output, then round11, then analysis, then analysis.json.
    Look for: the file opens and the first line under the opening bracket is "source".

15. Press Ctrl+F and type `"clusters": [`
    Look for: one match. The lines under it are clusters, each with code, plots, typologies, mirrored, orderSure and row.

16. In the search box type `"code": "Q01"` and look at the first match.
    Look for: 4 matches in all. Each cluster has 4 plot numbers in plots and the typologies DP2, TH1, TH1, DP2. The plot numbers are the baseplan labels, 10 or 11 below the official numbers.

17. In the search box type `"code": "Q02"`.
    Look for: 2 matches, each with the typologies DP4, TH3, TH3, DP4.

18. In the search box type `"code": "unknown"`.
    Look for: 141 matches. Open two of them and read plots and typologies. Send me the first plot number of any cluster you think is wrong.

19. In the search box type `"mirrored"` and look at any cluster.
    Look for: the values null, null, and so on. Every unit is null because the CAD output has no block scale yet.

20. In the search box type `"issues": [`
    Look for: one match. Each issue has rule, plots and text. R66 issues name the plots and typologies of an unknown cluster. There are 141 of them and 436 R65 issues.

21. In the Explorer panel open rules\roshn-makkah.json, press Ctrl+F and type `cluster_split_meaning`.
    Look for: one match, and the line reads "wall after unit n, counted from 1, 4-unit clusters only". A second search for `vl4_width` finds one match with the value "derive".

22. In the Explorer panel open config\project.json.
    Look for: profile, drawings and analysis. No Python file holds the project name any more.

## What remains

- The villa blocks are not matched to plots, so every mirror flag is null. The reasons are under Known bugs.
- Check 3 and the checks 4 to 10 are not coded. They stay in rules/README.md.
- The settings I chose and the front rule are in the Your turn list of the top log entry.
- The full run (python\full_run.py) has the new analysis stage and its command ran on the real baseplan output. A full run of all 26 files was not repeated this round.
- The Revit run of round 9 (steps/for-bader-round-9.md) is still waiting for you.
- riser_height is still null.

## Known bugs

- The baseplan drawing holds no villa block per plot. The blocks are in Makkah Building Footprint.dwg: 491 Villa_VL4, 291 Duplex_DP4, 280 Villa_C10, 275 Villa_VL3, 248 Villa_VL2, 149 Duplex_DP2, 66 Townhouse_TH3 and 65 Townhouse_TH1. Their insert points sit on plot edges, the duplex counts are about one per pair of plots, and the townhouse blocks do not touch their own plots. The CAD reader writes no block scale. So no honest match to a plot exists yet.
- 141 clusters are unknown. 44 have four units, 35 of them DP4 DP4 DP4 DP4 and 9 of them DP2 DP2 DP2 DP2. 97 are rows of 6 to 12 units. They may be listed clusters standing side by side. The tool calls them unknown, as the rule says.
- Of the 436 R65 issues, 88 sit in the four unit unknown rows and 348 in the longer rows. They are right under the rule and add noise until the unknown rows are explained.
- The order along the road is not sure for 95 of 330 clusters, because no end or both ends of the row touch other plots. 55 of them are listed clusters. The tool then keeps the chain and starts at the lower plot number.
- Plot numbers in analysis.json are the baseplan labels. The official number is the label plus the shift in config/plot_numbers.json.
- A plot whose typology is not in the profile is in no cluster and is not reported.
- The front rule says plots back onto other plots and front onto a road. It agrees with the Revit link axes on 76 of 76 units it decides, but UNKNOWN for the 95 rows it leaves open.
- The settings in config/project.json are my choices: tolerance 0.01 m, party wall 0.75 of the long side, end share 0.25.

## What comes next

1. You answer the open questions in the top entry of steps/log.md.
2. Explain the 141 unknown clusters, then match villa blocks to plots once the block scale is read.
3. Check 5 waits for the KEO riser height. Check 3 can be coded now that the split positions are defined.
