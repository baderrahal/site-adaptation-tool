# Progress

Live state of the tool. At most 60 lines. The round detail is in steps/log.md and steps/history/.

## State

- Round 15a (plan.json from the past IMC job and the placer step 1) is pull request 15. python/plan_from_past.py builds plan_imc_past.json outside the repo (R80 to R82). dotnet/SiteAdaptation.Placer is the Revit command Place from plan (R83 to R88), dry run by default, one transaction for the real run, its logic in Common and tested on fakes. python/plan_compare.py compares a plan with the placed model (R89). The reader got two fixes inside R54 and R55. Nothing of it has run in Revit yet.
- Past plan: 337 of 337 plots written, yard missing on all 337 (R58 gives ambiguous), Generic missing on 1. Position proof 0 mm on 94 links, FFL against link origin Z 337 of 337, second method 150 of 150.
- Round 12 (villa blocks, row split, road fronts, levels, master plan V9) is merged as PR 14. Round 12b runs in parallel on the Python side. Rounds 11, 10 and 9 are PRs 13, 12 and 11. The round 9 Revit code has NOT run in Revit yet.
- Real baseplan: 330 rows are 548 clusters, M02 143, M04 282, M09 61, Q02 62. Level proof on the past schedules: FFL 633 of 747, steps 744 of 747.
- Top Surface-02 is the finished road for the ramps (Bader decided).
- Readers: CAD (C#, ACadSharp), PDF, Excel, link table, second source check, face reasons, LandXML, surface lookup, ramp levels, surface roles, road check, cluster analysis, levels, plan builder, plan compare and full run (Python).
- Gate: Python 366 passed, .NET 205 passed.
- Real files stay in C:\02-Site Adaptation and C:\Users\bader\Documents\SiteAdaptation and are read in place. Nothing real is in git.

## Done

- Round 15a: R80 to R89, the placer add-in, the plan builder and compare, reader fixes in R54 and R55. Python 323 to 366, .NET 155 to 205.
- Round 12: R67 to R72, the corrected Rev 6 profile, drawing roles in config/project.json.
- Round 11: R63 to R66. Round 10: profile and loader (R62). Round 9: R51 to R61.
- Round 8: R47 to R50. Rounds 1 to 7: readers, road, LandXML, main guard hook. Revit round 7 ran in Revit on 22 Oct.

## Open bugs

- R58 cannot assign a yard on the past job, all 337 are ambiguous. Yard floors have no Plot parameter.
- RRE_ZZ_SP_Prototype-FFL differs from the schedule FFL on 258 plots, up to 32 mm. Plots 2301 and 2302 are VD4 on the link and DP4 in the schedule.
- The placer stops on an angle to true north other than 0. Both add-ins share one SiteAdaptation.Common.dll in Revit.
- Unproven in Revit: the whole placer, the link box and spot host fixes, the round 9 code.
- The only drawing with unit blocks is R06.dwg and the roles keep it out of the run. VL2 has no profile entry. Q02 against M10.
- V9 numbers are a new sequence and the baseplan role stays until Bader decides.
- The main guard hook only loads in a session that starts inside the repo.

## Bader's turn

1. Run steps/round-15a.md on a copy of the IMC model and answer the questions of the Round 15a entry in steps/log.md, first the yard rule.
2. The questions of the Round 12 entry, first baseplan or V9, then steps/round-12.md.

## Next three steps

1. Fix what the Revit run of round 15a shows, then place the floors from plan.json once the yard rule is set.
2. Fill plan.json from CAD and LandXML with the same format, so the placer does not change.
3. Code check 3 (one FFL per cluster) and wire levels.py to the official plot numbers (12b and 13).
