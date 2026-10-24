STATE 2026-10-24 16:40, Round 14 done, Part A is PR 20 (merged), Part B is PR 21 and merges only after both checks pass

# Progress

Live state of the tool. At most 60 lines. The round detail is in steps/log.md and steps/history/.

## State

- Round 14 Part B (R125 to R130): plan_from_cad.py writes plan_imc_cad.json outside the repo with links and levels, no floors. 224 of 337 IMC plots written, 113 have no mirror rule (107 villas, 6 of the cut list). Against the past plan on 218 plots: rotation, mirror and file equal 218, insert within 10 mm 217, FFL within 10 mm 193. Check 3 is R125 (one FFL per cluster, splits at inner walls of 4-unit clusters counted from either end). The Rev 6 ramp outline fits 321 of 325 past ramps within 10 mm, and Top Surface-02 stops at the curb, so the sidewalk is not covered
- Round 14 Part A (R120 to R124): cad_links.py measures the past links in the frame of their face. Front edge from 7.99 to below 18.75 m nearest RD-CURB-LINE, rotation 337 of 337. Mirror by drawn side (DP left, DT and TH right), 218 of 218. Origin a front corner offset per typology in plotFrame of config/prototype_offsets.json, 322 of 325 within 10 mm. 12 plots do not follow the baseplan cut (R120)
- Round 15b (R111 to R119): floors in plan.json and the Floors step of the placer, proven in Revit on IMC
- Round 15a (R80 to R89, R102 to R110): plan_from_past.py, the placer, the test host and the watcher, 337 of 337 links within 1 mm in Revit
- Round 13 (R90 to R101): clusters from the schedule units (225), road lookup, entry rule, block offsets
- Rounds 1 to 12b: readers, road, LandXML, profile, clusters, levels (R1 to R77)
- Gate: Python 708 passed and 1 skipped, .NET 347 passed
- Real files stay in C:\02-Site Adaptation and C:\Users\bader\Documents\SiteAdaptation and are read in place. Nothing real is in git

## Done

- Round 14: Part A R120 to R124 (PR 20), Part B R125 to R130 (PR 21). Python 680 to 708. Merge rule in CLAUDE.md
- Round 15b: R111 to R119, PR 19. Round 15a test: R102 to R110, PR 18. Round 13: R90 to R101, PR 17
- Rounds 1 to 12b: PRs 1 to 16

## Open bugs

- The site-adaptation-tool-15a folder is empty but held open by another process, not deleted
- No villa mirror rule, so the CAD plan leaves the 107 villas out. No rule for CASE within a facing or for linkName
- levels.py never splits, so 25 FFLs of the split clusters differ from the schedule. 24 FFLs lie on a half millimetre
- 2338 is 10 mm off, 2543 and 2546 4 m, 2374 and 2391 have ramps 2 m narrower, all next to the cut list
- The past yards sit at the FFL except TH1, TH3 and VL6, so 288 yard tops differ from the plan
- Plots 2301 and 2302 are VD4 on the link and DP4 in the schedule
- The placer stops on an angle to true north other than 0
- Unproven in Revit: the parallel windows of R110, the round 9 code, plan_imc_cad.json

## Bader's turn

1. Answer the questions of Round 14 Part B and Part A in steps/log.md, first the villa mirror
2. Answer the open questions of Round 15b, first the yard depth for the architect
3. Close the session held in site-adaptation-tool-15a so the empty folder can go

## Next three steps

1. The villa mirror once Bader answers, then floors in the CAD plan from the ramp outline and the faces
2. A test host run of plan_imc_cad.json on a work file and placer_check against the answer key
3. The combined Revit test with the parallel windows once more than 8 GB is free
