STATE 2026-10-24 15:47, Round 14 Part A merged (PR 20, main 497855c), Part B open on feat/cad-levels-14b

# Progress

Live state of the tool. At most 60 lines. The round detail is in steps/log.md and steps/history/.

## State

- Round 14 Part A (R120 to R124): python/cad_links.py measures the past links in the frame of their baseplan face. Front edge from 7.99 m up to below 18.75 m nearest RD-CURB-LINE, link Y axis into the plot, rotation 337 of 337. Mirror by the drawn side of each typology (DP left, DT and TH right), 218 of 218 cluster plots. Origin a fixed offset from one front corner per typology, within 10 mm on 322 of 325. The frame is plotFrame in config/prototype_offsets.json. 12 plots do not follow the baseplan cut (R120) and are listed apart. No rule yet for the villa mirror or the CASE letter within a facing
- Round 15b (R111 to R119): plan.json holds Generic, yard and ramp floors, the placer has a Floors step, proven in Revit on IMC, 1009 floors equal to the past model
- Round 15a (R80 to R89, R102 to R110): plan_from_past.py, the placer, the test host and the watcher, 337 of 337 links within 1 mm in Revit
- Round 13 (R90 to R101): clusters from the schedule units (225), road lookup Top Surface-02 first, entry rule, block offsets
- Rounds 1 to 12b: readers, road, LandXML, profile, clusters, levels (R1 to R77)
- Gate: Python 694 passed and 1 skipped, .NET 347 passed
- Real files stay in C:\02-Site Adaptation and C:\Users\bader\Documents\SiteAdaptation and are read in place. Nothing real is in git

## Done

- Round 14 Part A: R120 to R124, Python 680 to 694, PR 20. Merge rule in CLAUDE.md (gh pr checks --watch, both pass)
- Round 15b: R111 to R119, PR 19. Round 15a test: R102 to R110, PR 18. Round 13: R90 to R101, PR 17
- Rounds 1 to 12b: PRs 1 to 16

## Open bugs

- The site-adaptation-tool-15a folder is empty but held open by another process, not deleted
- Plots 2543 and 2546 lie 4 m from the fitted origin, next to the 2544 2545 cut. 2338 is 11 mm off
- The villa mirror has no rule, VL6 breaks the side neighbour pattern
- The past yards sit at the FFL except TH1, TH3 and VL6, so 288 yard tops differ from the plan
- RRE_ZZ_SP_Prototype-FFL differs from the schedule FFL on 258 plots. Plots 2301 and 2302 are VD4 on the link and DP4 in the schedule
- The placer stops on an angle to true north other than 0
- Unproven in Revit: the parallel windows of R110, the round 9 code
- The main guard hook only loads in a session that starts inside the repo

## Bader's turn

1. Answer the questions of Round 14 Part A in steps/log.md, first the villa mirror
2. Answer the open questions of Round 15b, first the yard depth for the architect
3. Close the session held in site-adaptation-tool-15a so the empty folder can go

## Next three steps

1. Part B: check 3, levels with El @ Entry, ramp outline test, plan_from_cad.py and the plan compare
2. The villa mirror once Bader answers, then the CAD plan for the plots with no past job
3. The combined Revit test with the parallel windows once more than 8 GB is free
