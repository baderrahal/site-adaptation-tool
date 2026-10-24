# Steps for Bader, round 12 (blocks, row split, road fronts, levels, master plan V9)

This round adds the block file of the CAD reader, the block to plot match, the row split, the front of a row from the road edges, the level rule with its proof on the past schedules, the corrected Rev 6 profile and the new master plan roles with the V9 drawing. Nothing runs in Revit. Do it after the pull request of this round is merged, the top entry of steps/log.md says when. Steps 1 to 4 open the folder and the terminal, steps 5 and 6 switch to main and pull it, step 7 installs, steps 8 to 10 run the tests, steps 11 to 13 run the proof on the past schedules, steps 14 to 19 read the drawings, steps 20 to 22 run the cluster step and the second source check, steps 23 to 29 read analysis.json, steps 30 to 32 check the profile and the two config files.

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
   Look for: Fast-forward and a list of files that includes python/levels.py, python/levels_proof.py and dotnet/CadReader/Exploder.cs. If it says Already up to date, the pull was done before and that is fine.

7. In the terminal run:
   `.venv\Scripts\python -m pip install -r requirements.txt`
   Look for: no red error text. Lines that start with Requirement already satisfied are normal.

8. In the terminal run all Python tests:
   `.venv\Scripts\python -m pytest python`
   Look for: 288 passed and no failed, and a line that starts with R67 proof: in the middle of the dots. A machine without the schedules shows 287 passed and 1 skipped, and without Git Bash a few more skipped, which is fine.

9. In the terminal run the .NET build:
   `dotnet build dotnet\SiteAdaptation.sln -c Release`
   Look for: Build succeeded and 0 Error(s).

10. In the terminal run the .NET tests:
    `dotnet test dotnet\SiteAdaptation.sln -c Release --no-build`
    Look for: a line that starts with Passed! and has 0 after Failed: and 148 after Passed:.

11. In the terminal run the proof test on the two past schedules. They are only read:
    `.venv\Scripts\python -m pytest python\tests\test_levels_proof.py`
    Look for: 11 passed and the line R67 proof: FFL matches 633 of 747, steps matches 744 of 747, both 632, SSL 633, count rule alone 747. The first number is how many plots the level rule reproduces.

12. In the terminal run the proof as a script, to get the list of every mismatch:
    `.venv\Scripts\python python\levels_proof.py sample_output\round12\proof`
    Look for: the first line is the same as in step 11, the second line starts with base height source: past job schedules, 747 plots, and the third line is FFL mismatches 114, steps mismatches 3, largest FFL difference 0.1 m. Then come 114 lines that start with FFL plot, each with the unit, the sheet FFL, the computed FFL and the difference, and 3 lines that start with steps plot.

13. In the Explorer panel open sample_output, then round12, then proof, then levels_proof.json. Press Ctrl+F and type `"difference": 0.1`
    Look for: 5 matches. Four are plots 2560 to 2563 under fflMismatches and the fifth is plot 2563 again under stepsMismatches. This is one DP3 TH3 TH3 DP3 cluster where the sheet uses a base height of 100 and the profile says 200.

14. In the terminal read the baseplan with the CAD reader. The drawing is only read:
    `dotnet\CadReader\bin\Release\net48\CadReader.exe "C:\02-Site Adaptation\Base Plans\ROSHN Makkah Baseplan.dwg" sample_output\round12\baseplan --layers config\cad_layers.json`
    Look for: the line layers 48, pieces 12458, plots 2962, other faces 29, texts 3318 and under it blocks 20, mirrored 0.

15. In the terminal read the new master plan, the V9 drawing:
    `dotnet\CadReader\bin\Release\net48\CadReader.exe "C:\02-Site Adaptation\Master Plan\25.06.2025 Phase 1 Amana_V9.dwg" sample_output\round12\v9 --layers config\cad_layers.json`
    Look for: the line layers 43, pieces 22193, plots 0, other faces 0, texts 3470 and under it blocks 595, mirrored 345. plots 0 is expected. The V9 plot outlines are polylines on layers with other names, the V9 scan in the top entry of steps/log.md says why.

16. In the Explorer panel open sample_output, then round12, then v9, then layers.json. Press Ctrl+F and type `Plots No.`
    Look for: 1 match, and a few lines under it "entities": 164 and "after": 2781. The 2781 texts are the plot numbers of V9, 163 in model space and 2618 inside one block.

17. In the terminal read the drawing R06_0000, the one the config names for the unit blocks:
    `dotnet\CadReader\bin\Release\net48\CadReader.exe "C:\02-Site Adaptation\Master Plan\221229_MP-SK-01-03-001_R06_0000.dwg" sample_output\round12\r06_0000 --layers config\cad_layers.json`
    Look for: the line blocks 2287, mirrored 1093.

18. In the terminal read the road layout drawing:
    `dotnet\CadReader\bin\Release\net48\CadReader.exe "C:\02-Site Adaptation\Roads\Makkah Phase 1_Road Layout.dwg" sample_output\round12\roads --layers config\cad_layers.json`
    Look for: the line layers 242, pieces 216017, plots 0, other faces 0, texts 1705 and under it blocks 6157, mirrored 2158.

19. In the terminal read the previous master plan R06. The full run does not read it, this is only for the comparison in step 21:
    `dotnet\CadReader\bin\Release\net48\CadReader.exe "C:\02-Site Adaptation\Master Plan\221229_MP-SK-01-03-001_R06.dwg" sample_output\round12\r06 --layers config\cad_layers.json`
    Look for: the line blocks 4639, mirrored 2151.

20. In the terminal run the cluster step with the blocks of R06_0000, as config\project.json says:
    `.venv\Scripts\python python\cad_analysis.py sample_output\round12\analysis_r06_0000 --cad sample_output\round12\baseplan --blocks sample_output\round12\r06_0000 --roads sample_output\round12\roads`
    Look for: three lines. The first is clusters 548 {"M02": 143, "M04": 282, "M09": 61, "Q02": 62}, issues 2346. The second starts with blocks and holds "villaBlocks": 0, because R06_0000 has only Villa_VL2 blocks and VL2 is not in the manual set. The third is fronts {"neighbour": 235, "road edge": 88, "none": 7}.

21. In the terminal run the cluster step with the blocks of R06 instead, as a comparison:
    `.venv\Scripts\python python\cad_analysis.py sample_output\round12\analysis_r06 --cad sample_output\round12\baseplan --blocks sample_output\round12\r06 --roads sample_output\round12\roads`
    Look for: the first line clusters 548 {"M02": 143, "M04": 281, "M09": 61, "Q02": 62, "unknown": 1}, issues 795. The second line holds "villaBlocks": 1620, "plotsOneBlock": 1554, "plotsNoBlock": 793, "tiedBlocks": 203. The third line is the same fronts line as in step 20.

22. In the terminal run the second source check of V9 against the baseplan:
    `.venv\Scripts\python python\second_source.py sample_output\round12\second --cad sample_output\round12\baseplan --second sample_output\round12\v9`
    Look for: the line matched 1771, only baseplan 1220, only second 197, area differs 114. The check builds fewer V9 faces than there are plots, see Known bugs.

23. In the Explorer panel open sample_output, then round12, then analysis_r06, then analysis.json.
    Look for: the file opens and the first line under the opening bracket is "source".

24. Press Ctrl+F and type `"code": "M09"`
    Look for: 61 matches. Each cluster has 4 plot numbers in plots and the typologies DP2, TH1, TH1, DP2. The plot numbers are the baseplan labels, 10 or 11 below the official numbers. Then search `"code": "Q02"` and look for 62 matches with the typologies DP4, TH3, TH3, DP4.

25. In the search box type `"code": "unknown"`
    Look for: 1 match, plots 438 and 437 with the typologies DP4, DP4. Under block you see Villa_VL4 and Duplex_DP4, so a villa block landed on a duplex plot. This is the only unknown left, 141 before this round.

26. In the search box type `"mirrored"` and look at the first match inside a cluster that is not the unknown one.
    Look for: values true, false or null. In a pair or a cluster exactly one unit is not null, because one block covers the whole pair or cluster. Searching `"mirrored": true` gives 753 matches, one for each mirrored plot.

27. In the search box type `"frontRule": "road edge"`
    Look for: 138 matches. These are clusters, 88 rows. Each cluster also has rowId and rowUnits, so the pieces of one row share both. Then search `"frontRule": "none"` and look for 14 matches, 7 rows.

28. In the search box type `"orderSure": false`
    Look for: 14 matches. They are the same 14 clusters as the none rule. The order of every other cluster is sure.

29. In the search box type `"blocks": 0`
    Look for: 793 matches. These are plots of a listed typology that got no block, mostly the second plot of a duplex pair and the three other plots of a townhouse cluster, because one block covers them all.

30. In the Explorer panel open rules\roshn-makkah.json, press Ctrl+F and type `"M10"`
    Look for: 2 matches, the cluster DP2, TH3, TH3, DP2 and the alias Q02 that points at it. Then search `"Q02"`: the cluster has DP4 ends. That difference is question 4 in the log.

31. In the Explorer panel open config\project.json.
    Look for: under drawings the roles baseplan, masterplan (the V9 file), previousMasterplan (R06.dwg), villaBlocks (R06_0000.dwg), roadLayout, xrefRoles, and a skip list with one entry, R06_0001. There is no secondDefault any more.

32. In the Explorer panel open config\second_source.json.
    Look for: no file line, and three layers whose names have Arabic text after the English name: residential plots outlines, residential and commercial mixed use, commercial.

## What remains

- Checks 3, 4 and 6 to 10 are not coded. They stay in rules/README.md. Check 5 is R67.
- levels.py is not wired to analysis.json. The plot numbers there are baseplan labels and the schedules use the official numbers, so the shift of R28 comes first.
- A full run with the PDF and Excel readers was not repeated. The DWG files and the stages ran on the real files.
- The Revit run of round 9 (steps/for-bader-round-9.md) is still waiting for you.
- riser_height is still null. No code reads the rev6 block of the profile.

## Known bugs

- The only drawing with unit blocks is R06.dwg, 1868 of them, and the roles keep it out of the run. R06_0000 has 248 block inserts and all are Villa_VL2, and VL2 is not in the manual set, so the run finds no unit block. V9 has none.
- A duplex block covers 2 plots and a townhouse block covers 4. One block has one mirror flag, so the flags cannot say original and mirror inside a pair. 203 blocks tie and go to the lowest plot number.
- Q02 has DP4 ends and M10 has DP2 ends. The manual list has no DP4 end cluster, so 62 rows are written Q02. VL2 has 232 plots and no profile entry.
- The second source check builds faces from closed polylines and from loose lines apart. On V9 it finds 1968 faces where noding the lines of the three layers together gives about 2600, so its V9 counts are low.
- The sheet FFL sits 1 to 5 mm below the rule in 53 of 473 clusters and 100 mm below in plots 2560 to 2563. The FFL is not rounded to the mm before the height, which is why plot 1526 differs.
- The road edge front is weak where both ends of a row lie within about 1 m of a curb. It agreed with the Revit link axes on 8 of 9 rows.
- The steps flag under 1 or over 3 cannot fire with this count rule.
- Plot numbers in analysis.json are the baseplan labels. The V9 numbers are another sequence and are not used anywhere.
- One xref is nowhere in the input folders, XR-TITLE BLOCK.dwg of the Typical Cross Sections. It is listed in full_run_summary.json under issues.

## What comes next

1. You answer the questions in the top entry of steps/log.md, first the baseplan or V9, the unit block source, VL2 and Q02 against M10.
2. I join the official plot numbers to analysis.json and feed levels.py from it.
3. I code check 3, one FFL per cluster, with the split positions of the profile.
