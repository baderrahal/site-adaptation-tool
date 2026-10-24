STATE 2026-10-24 18:20, Round 14 and round C3D merged (PR 20 to 23), Round 14c Part A open on feat/cad-rules-14c

# Progress

Live state of the tool. At most 60 lines. The round detail is in steps/log.md and steps/history/.

## State

- Round 14c Part A (R131 to R134): cad_rules.py tests the villa mirror, the CASE second letter and the FFL split. None holds on every plot outside the 16 cut plots, so none is coded. Villas: one hand per row 33 of 33, blind side to the neighbour 62 of 63, VL6 runs the other hand. CASE: the hand explains 331 of 337. Split: the height rule reproduces 0 of 24 schedule splits
- 16 cut plots (R120 and cadLinks.cutNeighbours) are counted apart everywhere, plotFrame measured with all 16 apart
- Round C3D (R150 to R159, PR 22): the road drawings read only in Civil 3D 2026. ROW is the plot line, sidewalk median 4.15 m. Top Surface-02 snapped within 5 mm gives 672 of 674 IMC ramp front corners. Its edge lies on the plot line within 2 mm, which conflicts with the curb reading of R127. Summary: steps/investigations/civil3d.md
- Round 14 (R120 to R130, PR 20 and 21): links from CAD in the frame of the baseplan face, rotation 337 of 337, mirror by drawn side 218 of 218, origin offset per typology in plotFrame. plan_from_cad.py writes plan_imc_cad.json with links and levels for 224 of 337 plots, check 3 is R125, the Rev 6 ramp outline fits 321 of 325 past ramps
- Round 15b (R111 to R119): floors in plan.json and the Floors step of the placer, proven in Revit on IMC. The yard sits at FFL minus 0.100 on every unit (Bader), asked of the architect
- Round 15a (R80 to R89, R102 to R110): plan_from_past.py, the placer, the test host and the watcher, 337 of 337 links within 1 mm in Revit
- Rounds 1 to 13: readers, road, LandXML, profile, clusters, levels, road lookup, entry rule, block offsets (R1 to R101)
- Gate: Python 740 passed and 1 skipped, .NET 358 passed
- Real files stay in C:\02-Site Adaptation and C:\Users\bader\Documents\SiteAdaptation and are read in place. Nothing real is in git

## Done

- Round 14c Part A: R131 to R134. Round C3D: R150 to R159, PR 22 and 23
- Round 14: Part A R120 to R124 (PR 20), Part B R125 to R130 (PR 21). Round 15b: R111 to R119, PR 19
- Rounds 1 to 15a: PRs 1 to 18

## Open bugs

- No villa mirror rule, the CAD plan leaves the 107 villas out. No CASE or linkName rule
- levels.py never splits, so 25 FFLs of split clusters differ from the schedule. 24 FFLs lie on a half millimetre
- The probe plug-in has not read a drawing, the surface names in the drawings are UNKNOWN. CadReader puts some nested Road Layout pieces at swapped coordinates
- The site-adaptation-tool-15a folder is empty but held open by another process
- 2338 is 10 mm off the fitted origin. Plots 2301 and 2302 are VD4 on the link and DP4 in the schedule
- The placer stops on an angle to true north other than 0
- Unproven in Revit: the parallel windows of R110, the round 9 code, plan_imc_cad.json

## Bader's turn

1. Answer the questions of Round 14c Part A, first the villa mirror
2. Answer the 7 questions of round C3D, first the trusted folder for the probe and where Top Surface-02 ends
3. Ask the architect why M07 and M08 split at the middle wall and M11 never
4. Close the session held in site-adaptation-tool-15a so the empty folder can go

## Next three steps

1. Round 14c Part B: Generic, yard and ramp floors in the CAD plan and their compare
2. The villa mirror and the front level from the snapped road surface once Bader answers
3. A test host run of plan_imc_cad.json on a work file (Round 16)
