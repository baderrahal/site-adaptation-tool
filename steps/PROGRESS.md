STATE 2026-10-24 17:14, Round C3D done and merged (PR 22), the probe plug-in waits on a trusted folder

# Progress

Live state of the tool. At most 60 lines. The round detail is in steps/log.md and steps/history/.

## State

- Round C3D (R150 to R159): the road drawings read only in Civil 3D 2026 on working copies. SECURELOAD keeps the probe plug-in out of NETLOAD, so plain LISP, ACadSharp and the LandXML did the reading. No corridor in any drawing, Road Layout holds 2 surfaces, 1 alignment, 3 profiles, 1 assembly, Rough Grading 10 surfaces. ROW is the plot line, sidewalk median 4.15 m. Top Surface-02 snapped within 5 mm gives 672 of 674 IMC ramp front corners, 539 within 5 mm, and covers the 119 uncovered ramps. Summary: steps/investigations/civil3d.md
- Round 14 Part B (R125 to R130): plan_from_cad.py writes 224 of 337 IMC plots with links and levels. Check 3 is R125. The Rev 6 ramp outline fits 321 of 325 past ramps. Part B read Top Surface-02 as stopping at the curb, round C3D measured its edge on the plot line within 2 mm (conflict, Bader's turn 1)
- Round 14 Part A (R120 to R124): links from CAD, rotation 337 of 337, mirror by drawn side 218 of 218, origin 322 of 325 within 10 mm
- Round 15b (R111 to R119): floors in plan.json and the Floors step of the placer, proven in Revit on IMC
- Round 15a (R80 to R89, R102 to R110): plan_from_past.py, the placer, the test host and the watcher, 337 of 337 links within 1 mm in Revit
- Rounds 1 to 13: readers, road, LandXML, profile, clusters, levels, road lookup, entry rule, block offsets (R1 to R101)
- Gate: Python 731 passed and 1 skipped, .NET 358 passed
- Real files stay in C:\02-Site Adaptation and C:\Users\bader\Documents\SiteAdaptation and are read in place. Nothing real is in git

## Done

- Round C3D: R150 to R159, PR 22. Python 708 to 731, .NET 347 to 358
- Round 14: Part A R120 to R124 (PR 20), Part B R125 to R130 (PR 21)
- Round 15b: R111 to R119, PR 19. Round 15a test: R102 to R110, PR 18. Round 13: R90 to R101, PR 17
- Rounds 1 to 12b: PRs 1 to 16

## Open bugs

- The probe plug-in has not read a drawing, so the surface names in the drawings, the Rough Grading surfaces and the assembly are UNKNOWN
- CadReader puts some pieces nested in anonymous blocks of the Road Layout at swapped coordinates
- The site-adaptation-tool-15a folder is empty but held open by another process, not deleted
- No villa mirror rule, so the CAD plan leaves the 107 villas out. No rule for CASE within a facing or for linkName
- levels.py never splits, so 25 FFLs of the split clusters differ from the schedule. 24 FFLs lie on a half millimetre
- 2338 is 10 mm off, 2543 and 2546 4 m, 2374 and 2391 have ramps 2 m narrower, all next to the cut list
- The past yards sit at the FFL except TH1, TH3 and VL6, so 288 yard tops differ from the plan
- Plots 2301 and 2302 are VD4 on the link and DP4 in the schedule
- The placer stops on an angle to true north other than 0
- Unproven in Revit: the parallel windows of R110, the round 9 code, plan_imc_cad.json

## Bader's turn

1. Answer the 7 questions of round C3D in steps/log.md, first the trusted folder for the probe
2. Answer the questions of Round 14 Part B and Part A, first the villa mirror
3. Answer the open questions of Round 15b, first the yard depth for the architect
4. Close the session held in site-adaptation-tool-15a so the empty folder can go

## Next three steps

1. Run the probe on the road surface, road layout and rough grading copies once the folder is trusted, and match the surfaces with the LandXML
2. Take the plot front level from the snapped road surface into levels.py and plan_from_cad.py, once Bader answers question 3 of round C3D
3. The villa mirror, then a test host run of plan_imc_cad.json on a work file
