# Steps for Bader, round 12b (villa blocks from Makkah Building Footprint, unit codes into the clusters)

This round reads the unit blocks from Makkah Building Footprint.dwg, gives every plot a unit code from its block or from the schedule, builds the clusters from that code, flags the Q02 clusters, rounds the FFL to the mm and adds three check scripts. Nothing runs in Revit. Do it after the pull request of this round is merged, the top entry of steps/log.md says when. Steps 1 to 4 open the folder and the terminal, steps 5 and 6 switch to main and pull it, step 7 installs, steps 8 to 10 run the tests, steps 11 to 15 read the drawings and the schedules, steps 16 and 17 run the block scan, steps 18 to 21 run the cluster step and read analysis.json, steps 22 to 24 run the three checks, steps 25 and 26 run the level proof, steps 27 and 28 read the two config files.

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
   Look for: Fast-forward and a list of files that includes python/block_scan.py, python/block_proof.py, python/link_proof.py and steps/round-12b.md.

7. In the terminal run:
   `.venv\Scripts\python -m pip install -r requirements.txt`
   Look for: no red error text. Lines that start with Requirement already satisfied are normal.

8. In the terminal run all Python tests:
   `.venv\Scripts\python -m pytest python`
   Look for: 468 passed and no failed, and a line that starts with R67 proof: in the middle of the dots, FFL matches 633 of 747, steps matches 745 of 747. A machine without the schedules shows one skipped, and without Git Bash a few more, which is fine.

9. In the terminal run the .NET build:
   `dotnet build dotnet\SiteAdaptation.sln -c Release`
   Look for: Build succeeded and 0 Error(s).

10. In the terminal run the .NET tests:
    `dotnet test dotnet\SiteAdaptation.sln -c Release --no-build`
    Look for: a line that starts with Passed! and has 0 after Failed: and 205 after Passed:. Nothing in .NET changed in this round, the 205 includes round 15a.

11. In the terminal read the baseplan with the CAD reader. The drawing is only read:
    `dotnet\CadReader\bin\Release\net48\CadReader.exe "C:\02-Site Adaptation\Base Plans\ROSHN Makkah Baseplan.dwg" sample_output\round12b\baseplan --layers config\cad_layers.json`
    Look for: the line layers 48, pieces 12458, plots 2962, other faces 29, texts 3318 and under it blocks 20, mirrored 0.

12. In the terminal read the building footprint drawing, the new villaBlocks file:
    `dotnet\CadReader\bin\Release\net48\CadReader.exe "C:\02-Site Adaptation\Base Plans\Makkah Building Footprint.dwg" sample_output\round12b\footprint --layers config\cad_layers.json`
    Look for: the line layers 19, pieces 21527, plots 0, other faces 0, texts 6 and under it blocks 3780, mirrored 2081. plots 0 is expected, this drawing holds the unit blocks and no plot faces.

13. In the terminal read the road layout drawing, which gives the road edges for the front of a row:
    `dotnet\CadReader\bin\Release\net48\CadReader.exe "C:\02-Site Adaptation\Roads\Makkah Phase 1_Road Layout.dwg" sample_output\round12b\roads --layers config\cad_layers.json`
    Look for: the line layers 242, pieces 216017, plots 0, other faces 0, texts 1705 and under it blocks 6157, mirrored 2158.

14. In the terminal read the first schedule workbook. It is only read:
    `.venv\Scripts\python python\excel_reader.py "C:\02-Site Adaptation\01_Output\1.11 References\00101-PSN-IMC-XXX-SCH-TU-090000.xlsx" sample_output\round12b\IMC --sheet "MP_Phase 1_Zone21"`
    Look for: a headers line that includes Plot No. and Unit, and the line rows 337.

15. In the terminal read the second schedule workbook:
    `.venv\Scripts\python python\excel_reader.py "C:\02-Site Adaptation\01_Output\1.11 References\00101-PSN-IMD-XXX-SCH-TU-090000.xlsx" sample_output\round12b\IMD --sheet "MP_Phase 1_Zone21"`
    Look for: a headers line and the line rows 410.

16. In the terminal run the block scan. It matches nothing, it only counts:
    `.venv\Scripts\python python\block_scan.py sample_output\round12b\scan --cad sample_output\round12b\baseplan --blocks sample_output\round12b\footprint`
    Look for: the first line inserts 3780, names 19, names by kind {'unit': 7, 'old': 1, 'none': 11}, then one line for each of the 19 names with the biggest first (MP-Parking_6mx2.5m 1806, Villa_VL4 491, Duplex_DP4 291), and two last lines. The first of them holds {'0': 84, '1': 3148, '2': 425, '4': 123}, so no block covers 3 plots, 425 cover 2 and 123 cover 4. The second holds 'inBestPlot': 3115, 'inOtherPlot': 413, 'inNoPlot': 168, 'noPlotHoldsPart': 84.

17. In the Explorer panel open sample_output, then round12b, then scan, then block_scan.json. Press Ctrl+F and type `"kind": "unit"`
    Look for: 7 matches. Then `"kind": "old"` gives 1 match (Villa_VL2) and `"kind": "none"` gives 11. Every name with a code has "endsWithCode": true, so config/block_codes.json is not needed.

18. In the terminal run the cluster step with the footprint blocks and the two schedules:
    `.venv\Scripts\python python\cad_analysis.py sample_output\round12b\analysis --cad sample_output\round12b\baseplan --blocks sample_output\round12b\footprint --roads sample_output\round12b\roads --coords sample_output\round12b\IMC\coords.json sample_output\round12b\IMD\coords.json`
    Look for: four lines. The first is clusters 535 {"M02": 153, "M04": 293, "unknown": 89}, issues 426. The second starts with blocks and holds "villaBlocks": 1617, "plotsOneBlock": 2346, "plotsNoBlock": 232, "plotsTwoBlocks": 0, "tiedBlocks": 36, "blocksOnNoPlot": 65, "mirrored": 503, "notMirrored": 501, "mirrorNull": 1342. The third starts with units and holds "bySource": {"block": 2346, "excel": 73, "none": 159}, "plotsWithBoth": 674, "blockUnitAgrees": 138, "blockUnitDiffers": 536. The fourth is fronts {"neighbour": 240, "road edge": 91, "none": 7}.

19. In the Explorer panel open sample_output, then round12b, then analysis, then analysis.json. Press Ctrl+F and type `"unitSource": "block"`
    Look for: 2346 matches. Then `"unitSource": "excel"` gives 73 and `"unitSource": null` gives 159. The 232 plots without a block are all label type VL2.

20. In the search box type `"code": "unknown"`
    Look for: 89 matches. In every one of them typologies is a run of TH1 or TH3 with 4, 8 or 12 units. This is the finding of the round: a townhouse block names the whole cluster, DP2 TH1 TH1 DP2 or DP4 TH3 TH3 DP4, and gives TH1 or TH3 to all four plots, so no listed cluster fits. Then search `"code": "Q02"` and look for 0 matches, and `"rule": "R75"` for 0 matches.

21. In the search box type `covers 4 plots`
    Look for: 492 matches, 123 blocks with 4 plots each. Then `covers 2 plots` gives 850. These plots have "mirrored": null, since one flag cannot say which unit of the block is mirrored. Then search `"cadType"` and look for 2578 matches, one record for each plot. Each record has cadType, the client plot type from the baseplan label, and unit, the unit code. The two are equal except on 246 plots, the end duplex plots of the 4 plot blocks. Then search `"blockUnit"` and look for 536 matches. Each is a plot where the block and the schedule give different units. The block wins and the pair is listed here under conflicts.

22. In the terminal run the second method for the block to plot match. It uses plain polygon clipping and no geometry library:
    `.venv\Scripts\python python\block_proof.py sample_output\round12b\block_proof --cad sample_output\round12b\baseplan --blocks sample_output\round12b\footprint`
    Look for: blocks 1617: agree 1595, owner flips on a near tie 22, differ 0, not comparable 0 and plots 2962: agree 2962, differ 0, faces crossing themselves 0, with a block 2346.

23. In the terminal run the mirror flag and rotation against the 338 Revit links. Both files are only read:
    `.venv\Scripts\python python\link_proof.py sample_output\round12b\link_proof --analysis sample_output\round12b\analysis\analysis.json --prototypes C:\Users\bader\Documents\SiteAdaptation\prototypes.json --coords sample_output\round12b\IMC\coords.json sample_output\round12b\IMD\coords.json`
    Look for: links 338, with a block 303, with a mirror flag 114 (same 28, opposite 86) and the line link turned from the block by degrees: {"0.0": 134, "90.0": 15, "180.0": 134, "270.0": 19, "329.9": 1}. The flags are mostly opposite and the rotation is the same or 180 degrees away, so the two cannot be compared as they stand.

24. In the terminal run the cluster step again with the schedules alone, no blocks, for comparison:
    `.venv\Scripts\python python\cad_analysis.py sample_output\round12b\analysis_excel_only --cad sample_output\round12b\baseplan --roads sample_output\round12b\roads --coords sample_output\round12b\IMC\coords.json sample_output\round12b\IMD\coords.json`
    Look for: clusters 225 {"M01": 5, "M02": 30, "M04": 32, "M05": 50, "M06": 71, "M07": 19, "M08": 6, "M11": 12}, issues 1831. These are real Rev 6 clusters, with no unknown. The 1831 issues are the plots that have no unit code from the schedule.

25. In the terminal run the level proof on the two past schedules, grouped by the sheet FFL. They are only read:
    `.venv\Scripts\python python\levels_proof.py sample_output\round12b\proof`
    Look for: the first line FFL matches 633 of 747, steps matches 745 of 747, both 633, SSL 633, count rule alone 747. The fourth line is sheet FFL against the computed FFL rounded down: to the cm matches 90 of 747 (fixes 10, breaks 553), to 5 mm matches 168 of 747 (fixes 24, breaks 489). The fifth is sheet grouping misses by cause: FFL {"base height": 4, "rounding": 110}, steps {"base height": 1, "rounding": 1}.

26. In the terminal run the proof again with the CAD clusters of step 18:
    `.venv\Scripts\python python\levels_proof.py sample_output\round12b\proof_cad --analysis sample_output\round12b\analysis\analysis.json`
    Look for: the first five lines are the same as in step 25. Then CAD clusters: FFL matches 548 of 747, steps matches 649 of 747, clusters with more than one sheet FFL: 2 units 8 of 151, 4 units 3 of 9, 8 units 14 of 14, and CAD cluster misses by cause: FFL {"wrong cluster": 145, "rounding": 54}, steps {"wrong cluster": 97, "rounding": 1}.

27. In the Explorer panel open config\project.json.
    Look for: under drawings the villaBlocks name Makkah Building Footprint.dwg (not R06_0000), and under analysis the two new lists unitPlotTypes and noRev6Match ["Q02"].

28. In the Explorer panel open rules\roshn-makkah.json and press Ctrl+F and type `"Q02"`
    Look for: 1 match, under clusters with the units DP4, TH3, TH3, DP4. There is no Q02 under cluster_aliases any more, which now holds D02, D04 and Q01 only.

## What remains

- Check 3 (one FFL per cluster), checks 4 and 6 to 10. The pair rule on blocks. The Revit run of round 9 (steps/for-bader-round-9.md) is still waiting for you.
- levels.py is not wired to analysis.json. A plot of the schedule enters the CAD run through the shift of R28.

## Known bugs

- The block names carry the client plot type and a block names a whole cluster. The unit code from a block equals the baseplan label type on 2100 of 2346 plots, and equals the Excel unit on 138 of 674. The townhouse blocks turn 123 old M09 and Q02 clusters into unknown. Question 1 in the log.
- 159 plots labelled VL2 have no unit code. The schedule gives units to 73 other VL2 plots, DP4, DP2, VD4, VD2, VL4 and VL6.
- The plot at the edge of a neighbourhood can sit one label off the shift (R28). The Excel unit uses the config shift only.
- The mirror flag of a block is not the mirrored flag of its Revit link by plain comparison, 28 same and 86 opposite.
- The sheet FFL is not the computed FFL rounded down to the cm or to 5 mm. The 110 rounding misses of 1 to 5 mm stay unexplained.
