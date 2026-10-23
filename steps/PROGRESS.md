# Progress

Live state of the tool. At most 60 lines. The round detail is in steps/log.md and steps/history/.

## State

- Round 10 (ROSHN Makkah rules profile and loader) done on branch round/roshn-profile. rules/roshn-makkah.json holds the rule values as data, python/project_profile.py loads it by path (R62), rules/README.md lists the ten checks. None of the ten is coded and no project settings file exists yet. riser_height is null until the KEO value is supplied.
- Round 9 (floor, ramp and link relations) is merged as PR 11. The Revit reader gained floor measurements, parameter details, link state and spot hosts, and python/plot_relations.py measures how the ramp, Generic floor, yard floor and link of each plot relate. Measurement only, no placement logic. The new Revit code has NOT run in Revit yet, Bader runs steps/for-bader-round-9.md.
- Round 8 (ramp levels from the real corners, surface roles, floor count per plot, one error line) is merged.
- The Revit round 7 HAS run in Revit (Bader, 22 Oct 23:12 UTC, 5.2 MB). It showed 1010 floors, 337 ramp floors all read with the shape editor (4 corners each), 987 spot elevations, 337 error lines (all yard floors with no Plot parameter) and no slope parameter on any floor. Plot 2441 has no Generic 150mm floor in it, Bader deleted it by mistake and will restore it.
- Revit round 5 (link names, floors) ran on 22 Oct before: 338 links (289 not loaded), 926 walls, 1011 floors, 0 ramps.
- Top Surface-02 is the finished road for the ramps (Bader decided, role road in config/road_surfaces.json). The other five design surfaces are not confirmed.
- Readers: CAD (C#, ACadSharp), PDF (pdfplumber), Excel (openpyxl), link table, second source check, face reasons, LandXML reader, surface lookup, ramp levels, surface roles, road check and full run script (Python, shapely, numpy).
- Gate: Python 202 passed, .NET 143 passed.
- Real files stay in C:\02-Site Adaptation and C:\Users\bader\Documents\SiteAdaptation and are read in place. Nothing real is in git.
- The full run covered 11 DWG, 12 PDF, 2 schedule workbooks and the masterplan workbook, 26 of 26 ran, then 3 stages. The LandXML file runs as 2 more stages (6 s and 3 s). Outputs are in sample_output/full_run.

## Done

- Round 10: rules/roshn-makkah.json, the loader, one test named R62 (Q01 has 4 units, front_max is 2.0, a missing key raises), rules/README.md, R62 in docs/rules.md and steps/round-rules.md. Python went from 201 to 202 passed, .NET stayed at 143.
- Round 9: floor measurements, parameter details, floor types, link state and spot hosts in the Revit reader (R51 to R56) and plot_relations.py with the CSV and summary (R57 to R61). The made-up model estimate for the new file is 12.7 MB. On the old real file the report ran on 337 plots: Generic top equals FFL on 336 and El @ Entry on 0, every plot has 2 to 6 yard candidates, the largest overlap with the Generic floor picks one on 336 of 336, the type name slope matches the short edges on 216 of 327 ramps.
- Round 8: ramp_levels.py reads the corners with the survey offset (R47). 414 corners are covered by Top Surface-02, 338 within 30 mm. Role proposal per surface (R48), floor count per plot (R49, one finding: plot 2441 has no Generic floor), one error line per floor type with no Plot parameter (R50).
- Revit round 7: floors get corners and cornersSource, slope info, plotNumber and unit, sketchBoundary, spotElevations, link rawName with the name parsed from RRE_ZZ_SP_RVT-Name, link contents default none, rules R34 to R39. Prototype-FFL is lower than the Excel on 298 plots, equal on 39, higher on 0 (the earlier number 282 was wrong).
- Round 7: LandXML reader (7 surfaces), lookup with grid index, config/road_surfaces.json, ramp test on 337 ramps, gap bands, second check with shapely and raw bytes, rules R40 to R46.
- Round 1 to 4: four readers, explode, layer filter, plot labels, PDF rows, link table, full run, main guard hook.
- Round 6: plot number shift config (IMC 10, IMD 11), Master Plan skip list, CAD type check dropped, second source check, masterplan workbook survey, face reasons.
- Location check with the shift: 734 of 746 joined plots pass, 0 need another shift.

## Open bugs

- 12 plots fail the location check, unit corners 1.4 to 20 m outside the plot, no shift fits.
- Sheet 009027 plot 2549 code 5 and sheet 009028 plot 2589 code 2 hold bad northing values in the source.
- Baseplan: 17 faces with many labels (15 consecutive runs) and 12 faces with no label are not fixed. The 18 stray labels are road and buffer labels.
- R06_0000 against the baseplan: 334 faces only in the baseplan, 1100 only in R06_0000, 32 matched faces differ in area by more than 1 percent.
- The masterplan workbook has no per plot rows, so the numbering test could not run on it.
- Rough grading has no export yet. The road surface comes from the LandXML export.
- 119 ramps have no covered corner on Top Surface-02 and 934 corners have no cover. A corner that does not match the road is a design choice.
- Yard floors have no Plot parameter, so the floor count cannot check the yard per plot. Spots carry no value or target.
- Still unproven in Revit: the sketch plane at 150 m, the floor slope parameter and arrow on a sloped floor, spots inside links.
- Unproven, round 9 code: the bounding box of a link instance (assumed in host space), top face outlines of shape edited slabs, the bottom range from the solid, spot references, the real file size (estimate 12.7 MB).
- One unclosed face, 28 plot_open pieces, arc crossings not noded. Plots 2589 and 2594 carry a stale link Name.
- Rules profile: the meaning of cluster_split_positions is not stated, there is no standard VL4 plot width, the profile is in mm and prototypes.json in metres, and the values are not checked against the source report.
- The main guard hook only loads in a session that starts inside the repo.

## Bader's turn

1. Restore the Generic 150mm floor of plot 2441, then run the 11 steps in steps/for-bader-round-9.md (build, copy, reload links, Read Prototypes, run the report) and give me the File size line and plot_relations_summary.md.
2. Answer the numbered questions in the top entry of steps/log.md (split positions, VL4 width, project settings, where the ten checks live), then run steps/round-rules.md. The yard rule and ramp slope questions are in steps/history/round-9.md.

## Next three steps

1. Read plot_relations_summary.md from the new file and check it against the top face outlines, then decide the yard rule and what the ramp slope in the type name means.
2. Run link_summary.py on the new prototypes.json, the floor count should show 0 findings.
3. Find where cluster membership comes from, then code rules checks 1 and 2 once the round 10 questions are answered. Placement work only after the conflicts list and the Revit result are understood.
