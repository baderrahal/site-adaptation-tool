# Steps for Bader, round 13 (unit codes, road levels, entry level per plot, block to link offset)

This round makes the Excel unit the only unit code, reads a footprint block as a cluster hint, asks the LandXML surfaces in a fixed order for the road level, measures El @ Entry on the past job and codes one rule for it, and measures where the link and the entrance of a unit sit in its footprint block. Nothing runs in Revit. Do it after the pull request of this round is merged, the top entry of steps/log.md says when. Folder to open in VS Code: C:\Users\bader\Documents\GitHub\site-adaptation-tool. Steps 1 to 4 open the folder and the terminal, steps 5 and 6 switch to main and pull it, step 7 installs, steps 8 to 10 run the tests, steps 11 to 13 run the cluster step, steps 14 and 15 the road lookup, steps 16 to 18 the entry level, steps 19 and 20 the block offsets, steps 21 and 22 the second methods, steps 23 to 25 read the config files.

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
   Look for: Fast-forward and a list of files that includes python/road_lookup.py, python/entry_levels.py, python/entry_proof.py, python/block_offsets.py, config/prototype_offsets.json and steps/round-13.md.

7. In the terminal run:
   `.venv\Scripts\python -m pip install -r requirements.txt`
   Look for: no red error text. Lines that start with Requirement already satisfied are normal.

8. In the terminal run all Python tests:
   `.venv\Scripts\python -m pytest python`
   Look for: 565 passed and no failed. A machine without the schedules shows one skipped, and without Git Bash a few more, which is fine.

9. In the terminal run the .NET build:
   `dotnet build dotnet\SiteAdaptation.sln -c Release`
   Look for: Build succeeded and 0 Error(s).

10. In the terminal run the .NET tests:
    `dotnet test dotnet\SiteAdaptation.sln -c Release --no-build`
    Look for: a line that starts with Passed! and has 0 after Failed: and 205 after Passed:. Nothing in .NET changed in this round.

11. In the terminal run the cluster step. The inputs are the reader outputs of rounds 12 and 12b, which stay in sample_output:
    `.venv\Scripts\python python\cad_analysis.py sample_output\round13\analysis --cad sample_output\round12\baseplan --blocks sample_output\round12b\footprint --roads sample_output\round12\roads --coords sample_output\round12b\IMC\coords.json sample_output\round12b\IMD\coords.json`
    Look for: the first line starts with clusters 225 {"M01": 5, "M02": 30, "M04": 32, "M05": 50, "M06": 71, "M07": 19, "M08": 6, "M11": 12}, issues 1. Round 12b gave 535 clusters with 89 unknown and no Q02 cluster is written now either.

12. In the terminal read the lines the cluster step printed below the clusters line, they start with units and hints.
    Look for: in the units line bySource excel 747 and none 1831, and in the hints line byStatus equal 157, differs 10 and no schedule 381, codeAgrees 0 and codeDiffers 157. That is 157 block groups that equal one schedule cluster of 225, and on none of them does the client type of the block give the Rev 6 cluster code.

13. In VS Code open sample_output\round13\analysis\analysis.json and search for "conflicts"
    Look for: a list of 10 entries, each a block that names other plots than the schedule cluster. They are listed and nothing settles them. Tell me which side is right.

14. In the terminal run the ramp test with the lookup:
    `.venv\Scripts\python python\ramp_levels.py sample_output\round13\road --prototypes "C:\Users\bader\Documents\SiteAdaptation\prototypes_imc_past.json" --landxml-dir sample_output\full_run\landxml`
    Look for: ramps 337, covered corners {"0": 119, "1": 39, "2": 162, "3": 17, "4": 0}.

15. In VS Code open sample_output\round13\road\road_levels_summary.json and search for "lookup"
    Look for: orderSource file order, road first, the order Top Surface-02 first, answeredBy Top Surface-02 414 and 0 for the other five, noFirstSurfaceCorner 119 and gainedFromLaterSurfaces 0. No later surface covers any of the 119 ramps.

16. In the terminal measure El @ Entry on the past job. This does not write the config:
    `.venv\Scripts\python python\entry_proof.py measure sample_output\round13\entry --cad sample_output\round12\baseplan --roads sample_output\round12\roads --landxml-dir sample_output\full_run\landxml`
    Look for: plots 337, ramp rule with n 337, median -0.0015, within5mm 205, within10mm 238, within30mm 319, maxAbs 0.1007, and road offset 0.14.

17. In the terminal prove the entry rule on the 747 schedule plots:
    `.venv\Scripts\python python\entry_proof.py proof sample_output\round13\entry --cad sample_output\round12\baseplan --roads sample_output\round12\roads --landxml-dir sample_output\full_run\landxml`
    Look for: two lines. The IMC line has plots 337, road level 336, none 1 and bands within5mm 23, within10mm 41, within30mm 160, worse 176. The IMD line has plots 410, road level 410, none 0 and bands 20, 36, 201, 209.

18. In the terminal run the level proof with the computed entry levels:
    `.venv\Scripts\python python\levels_proof.py sample_output\round13\levels --analysis sample_output\round13\analysis\analysis.json --entries sample_output\round13\entry\entry_proof.json`
    Look for: a line CAD clusters: FFL matches 600 of 747, steps matches 705 of 747 and a line computed entry levels: FFL matches 6 of 747, steps matches 638 of 747. The line after it lists the misses by cause and entry level is the largest.

19. In the terminal measure the block offsets. This does not write the config:
    `.venv\Scripts\python python\block_offsets.py sample_output\round13\offsets --cad sample_output\round12\baseplan --blocks sample_output\round12b\footprint`
    Look for: links 337, measured 302, left out {"noBlockOrTwo": 35} and in the summary groups 15, tooFew 0, testPlots 149.

20. In VS Code open sample_output\round13\offsets\block_offsets.json and search for "variants"
    Look for: Villa_VL4, Villa_C10 and Villa_VL3 hold two variants each, an original and a mirrored unit, with different offsets. This is why one offset per group misses the plots of the second variant.

21. In the terminal run the second method for the road lookup, 10 random points with seed 13:
    `.venv\Scripts\python python\round13_check.py road sample_output\round13\check --landxml-dir sample_output\full_run\landxml --points sample_output\round13\entry\entry_proof.json`
    Look for: road: seed 13, 10 drawn, agree 10, differ 0.

22. In the terminal run the second method for the offsets, 10 random plots with seed 13:
    `.venv\Scripts\python python\round13_check.py offsets sample_output\round13\check --offsets-report sample_output\round13\offsets\block_offsets.json --blocks sample_output\round12b\footprint`
    Look for: offsets: seed 13, 10 drawn, agree 10, differ 0.

23. In VS Code open config\prototype_offsets.json
    Look for: a note that starts Measured from the IMC past job, entry with roadOffsetM 0.14, and 15 groups. Each group has splitSeed 13, fitError and testError. This file was written by the scripts and nothing reads it by hand.

24. In VS Code open config\road_surfaces.json
    Look for: no order key on any surface. The lookup therefore uses the file order with Top Surface-02 first. To set your own order add "order": 1, "order": 2 and so on to every surface except EXISTING_11.5 Sqkm, with different numbers. Then rerun step 14 and step 15 and the order line shows order in config.

25. Tell me the answers to the numbered questions in the top entry of steps/log.md.
    Look for: the list under Your turn, recommended answer first.
