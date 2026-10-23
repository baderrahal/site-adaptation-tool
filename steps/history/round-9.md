## Round 9, 2026-10-23, floor, ramp and link relations

Done:
- Branch round/floor-link-relations from main at 61bfccc. This session started in C:\Users\bader, outside the repo, so the main guard hook was NOT live. I never committed to main. No round branch was open and nothing was uncommitted. Four old local branches from rounds 1 to 4 are not merged (feat/readers, feat/revit-links-walls-ramps, fix/excel-header-cad-plots-pdf-second-sheet, test/readers-real-files), I left them alone. The untracked folder Claude outputs/ is not mine and stays out of the commit. R51 to R61 were free
- Revit reader, floors (R51 to R53): typeId, levelElevation, topElevationRange from GetTopFaces, bottomElevationRange from the downward faces of the solid, boundingBox, outline loops (an edge on two top faces is dropped, the rest is chained), workset, phaseCreated, designOption, parameterDetails and floorTypes once per type. level and heightOffset already existed. New geometry is in shared coordinates. No existing field changed, a test proves a floor without the new fields keeps exactly its old keys
- Revit reader, links and spots (R54, R55): link elementId, status, loaded, parameterDetails and for loaded links 8 box corners and transformShared. Nothing inside a link is read, a test checks the link state code never opens the linked document. Spot host with category and type. Every failure is one line per reason with the count and the first 5 ids, the element stays in the list. The add-in dialog now shows the file size
- Revit API checks: the build against the Revit 2024.3 assembly has 0 errors, so every member name exists. The XML docs shipped with the package confirm GetEdgesAsCurveLoops, GetTopFaces, the LinkedFileStatus values, DesignOption null in the main model, the built in id INVALID, and that spot references can be unavailable. They say nothing about the space of the bounding box of a link instance
- Size (R56): a made-up model sized like the round 7 file (5.01 MB against the real 5.2 MB) is 12.47 MB with the new fields, so they add 7.46 MB and the estimate for the real file is 12.7 MB. This is an estimate. The real size is only known when Bader runs the add-in, the dialog prints it
- plot_relations.py (R57 to R61) with the CSV and the summary. Beyond the brief I added the slope along the short edges, the overlap area of every yard candidate, and levelElevation. On the real round 7 file in place (old shape, sketch outlines, no new fields, output in the scratch folder) it ran in 1.3 s on 337 plots and 337 schedule rows. Counts: Generic top equals FFL on 336 of 336 and El @ Entry on 0 of 336. Every plot has 2 to 6 yard floors that touch or overlap (2: 33, 3: 85, 4: 81, 5: 37, 6: 101), so none is assigned by touching alone. By the largest overlap with the Generic floor the best candidate is at least twice the next on 336 of 336 plots and 335 yards are the best candidate of exactly one plot. The ramp lies fully inside the Generic outline on 336 of 336 and shares 3 edges with it on 335 of those 336 plots (plot 2441 has no Generic floor). Ramps are 5.5 m deep and 8 to 16 m long. The type name slope equals the slope along the long edges on 26 of 327 ramps and along the short edges on 216 of 327
- Mutation check on plot_relations.py: edge tolerance down to 0.5 mm failed 3 of 35 tests, up to 50 mm failed 3, slope sum without the second long edge failed 4, slope sum as a difference failed 4. Code put back, hashes equal
- Gate: Python 201 passed (166 before), .NET 143 passed (109 before), build 0 errors. New tests: Python 35, .NET 34
- The copy step excludes SiteAdaptation_Revit2024.addin as well, an old copy of it sits in the build folder and would add a second manifest in the subfolder. steps/for-bader-round-9.md has the steps. The round 8 entry moved to steps/history/round-8.md

Remains:
- Bader runs steps/for-bader-round-9.md. Then the report runs on the new file and the numbers above are checked against the top face outlines. The yard rule is Bader's call

Known bugs:
- UNPROVEN in Revit: the bounding box of a link instance (taken to be in host space, the report counts boxes that hold the link origin to show it), GetTopFaces and the outline loops on shape edited slabs, the bottom range from the solid, spot references (they may be unavailable on all 987 spots, then one line says so), LinkedFileStatus on the real links
- The report on an ambiguous yard uses the union of the candidates, so its yard overlap and gap numbers are only a bound until the yard rule is set
- Still open from earlier rounds: plot 2441 has no Generic floor, 119 ramps with no corner on the road, 12 failing plots, many label faces, the stale names of plots 2589 and 2594

Next:
- After the Revit run, read the summary and decide the yard rule and what the ramp slope in the type name means

Your turn:
1. Run steps/for-bader-round-9.md, 11 steps, and give me the File size line and plot_relations_summary.md
2. Yard rule. Recommended: the yard of a plot is the touching yard with the largest overlap with its Generic floor. On the old file that is clear on 336 of 336 plots. The other way is to give the 337 yard floors the Plot parameter in the model
3. Ramp slope. Recommended: no change now. The type name slope matches the slope across the depth (short edges) on 216 of 327 and the long edges follow another gradient. Say which one the type name is meant to describe
4. The older open questions are in steps/history/round-8.md
