# Data book, rounds DATA and DATA-2, 2026-10-24

The data book lives outside the repo in Documents\SiteAdaptation\data_book. Counts and distances only here, no coordinates or levels. The 15 sources and the 15 working copies hash the same before and after both rounds.

## Phase 1 border

All Phase Boundaries holds 8 rings. Layer Phase 1 Bound, labelled PHASE - 1, is a 9-vertex ring (open polyline whose ends meet) of 2,517,500 m2 and 6,971 m perimeter. It holds 747 of 747 face centroids of the IMC and IMD schedule plots, and every other ring holds 0. So it was taken by layer and label.

## How each drawing was read

- Round DATA: three tries of the full Civil 3D window were stopped by the 2 GB memory floor before NETLOAD (free 1.75, 1.72, 1.94 GB). All 14 drawings got CadReader stand-in tables.
- Round DATA-2: free memory 5.62 GB at the start and 6.16 GB after dotnet build-server shutdown, the floor stayed at 2 GB. The full window ran on the three drawings with Civil 3D objects. Lowest free memory during a run was 3.11 to 3.38 GB.
- Road Layout try 1: the probe DLL dialog came up, but the watcher clicked Do Not Load, because Civil 3D 2026 shows the file as a Name line and a Location line and the watcher looked for the full path in one text. The run was stopped after 170 s, copy unchanged. R161 now joins Name and Location (2 new tests). Try 2 and the other two drawings each needed one try.
- Times: Road Layout 40 s, Road Surface 25 s, Rough Grading 25 s. Civil 3D ended with 0xC0000005 (Road Layout) and 0xC0000409 (Rough Grading) after C3DQUIT, both after data.json was written. Road Surface ended with 0. The watcher answered one Annotation Scale dialog with No.
- Those three folders now hold reader C3DProbe tables only. The Road Layout entities table (27.7 MB) is split by layer as R164 says. The other 11 drawings keep their CadReader stand-in.

## What the full read found inside Phase 1

| Drawing | Entities, blocks, texts | Surfaces inside | Civil 3D objects |
|---|---|---|---|
| Road Layout | 208,363, 5,869, 688 | 2: Grading - North (1), New Existing (1) | 1 alignment and 1 assembly, both outside the border, so no profile rows. Site CEC with 0 parcels and 0 alignments |
| Road Surface | 42, 0, 0 | 7, the same names as the earlier LandXML | 1 point group (_All Points, 0 points) |
| Rough Grading | 17, 0, 0 | 9 of 10: Grading - North, Total Top of Roads Subgrade (-0.70), New Existing, Phase - 01 Grading, compiled CEC RG, CHR-4, CHR-5, CHR-9, CHR-10. Difference Between Ph-01 & NGL is a volume surface with no triangles | none |

18 surfaces were exported to data_book\landxml, triangles inside Phase 1 (R169). plots.csv got 98 columns, a left and right level for each of the 18 exports added, and none of the 62 old columns changed a cell. Part W then wired it by the method that scored best per source and added a method column per wired column, 110 columns. The scores and the changes are in steps/investigations/wiring.md.

## What each source adds inside Phase 1

| Source | Kept rows (entities, blocks, texts) | Adds |
|---|---|---|
| Baseplan | 3,706, 0, 3,148 | plot faces and labels, the only client plot type |
| Building Footprint | 20,453, 3,780, 6 | unit blocks with outlines, MP_Setback Line |
| Roads Road Layout | 208,363, 5,869, 688 (C3DProbe) | curb, ROW, lane marks, road names, 2 surfaces |
| Base Plans Road Layout | 8,389, 0, 2 | the same curb a second time, shy line |
| Cutsheet | 54, 0, 31 | sheet frame per plot (every plot) |
| CEC Footprint | 186,046 (split by layer), 1, 12 | CEC detail only |
| R06 | 119,761, 3,893, 80 | blocks on 63 to 68 percent of plots, the same types as the baseplan |
| R06_0000 and R06_0001 | 118,458, 2,267, 79 each | identical to each other, blocks on 9 to 12 percent of plots |
| V9 | 16,563, 586, 3,459 | reference only (Bader, DATA-2) |
| Road Surface | 42, 0, 0 (C3DProbe) | 7 surfaces |
| Rough Grading | 17, 0, 0 (C3DProbe) | 9 surfaces |
| Typical Cross Sections | 0 | sections lie outside the border, not plan geometry |
| Workbook | 9 sheets | area schedules, Phase1 sheet 2,701 villa and townhouse units, no plot rows |

## El @ Entry from the road and the ramp (R170)

The 747 schedule plots. Prediction: mean road level of the two front corners (snapped as in R157) plus slope times ramp depth (5.52 m, C10 5.50 m). Difference is predicted less schedule.

| Variant | Within 5 mm | Within 30 mm | Worse | No level | Median |
|---|---|---|---|---|---|
| Road only (round DATA) | 2 | 259 | 483 | 3 | 55 mm below |
| 0.5 percent | 217 | 171 | 356 | 3 | 28 mm below |
| 2 percent | 35 | 135 | 574 | 3 | 55 mm above |
| By typology (2 percent C10, VL4, VL6) | 221 | 193 | 330 | 3 | 11 mm below |

- By typology per neighbourhood: IMC 96, 92, 149. IMD 125, 101, 181, 3 no level.
- The 2 percent units stay low: C10 2, 6, 35 (2 no level), median 18 mm below. VL4 2, 7, 27, median 33 mm below. VL6 2, 22, 2.
- Rough Grading gives no El @ Entry: Phase - 01 Grading and Grading - North 0 within 30 mm of 747, median 1.32 m below. Total Top of Roads Subgrade (-0.70) 0 within 30 mm, 235 no level, median 0.59 m below. New Existing 2 or 3 within 30 mm. The Road Layout surfaces are 1.1 to 1.2 m below.

## Units, not plots (R171)

- Every one of the 2,578 unit faces holds exactly one unit. No face holds two.
- Footprint unit blocks inside Phase 1: 1,865. A villa block covers 1 face. A duplex block (DP2 149, DP4 291) covers 2 faces, one duplex unit each. A townhouse block (TH1 65, TH3 66) covers 4 faces, 2 TH and 2 DP ends.
- 81 blocks lie on no face (C10 20, VL2 16, VL3 8, VL4 14, DP2 6, DP4 9, TH1 4, TH3 4). Counted by the faces such a block usually covers they hold 120 units.
- Units: 2,698 (2,578 on faces and 120 on no face). Rev 6 2,704, gap 6. Phase1 sheet 2,701, gap 3.

| Phase1 sheet row | Footprint units | Sheet | Gap |
|---|---|---|---|
| Villa (C10) | 280 | 280 | 0 |
| Villas (VL2, VL3, VL4) | 1,014 | 1,017 | -3 |
| Duplex (DP2, DP4) | 1,142 | 1,142 | 0 |
| Townhouse (TH1, TH3) | 262 | 262 | 0 |

By Rev 6 kind (Rev 6 count of the round 12 record): C10 280 against 204 (+76), villa 1,014 against 1,076 (-62), duplex 1,142 against 958 (+184), townhouse 262 against 264 (-2), DP1/VD1 202 cannot be split by kind. The client type does not give the Rev 6 code: on the 747 schedule plots a VL4 plot holds DP2 24, DP4 28, VD2 29, VD4 27, VL4 22 and DT3 1, VD1 1. So only the sheet rows compare like with like.

## Width meaning (R172)

rules/roshn-makkah.json names the width plot_width_m in every typology and vl3_width, th1_width and vl4_width under plot. rules/README.md calls it the plot width of the manual, for reference only. Since every unit face holds one unit, that plot width is also the width of the plot of one unit. A duplex or townhouse block spans two or four plots, each at its own width.

## Answers

- Unit code source: no drawing or sheet gives a Rev 6 unit code for the 1,831 unit plots with no schedule. The footprint and R06 blocks name only client types, and none of the 9 codes that exist in Rev 6 and not as client types appears in any table.
- Entry level source: see R170 above. No surface gives El @ Entry directly. By typology on the snapped road is the best of the tested variants, 221 of 747 within 5 mm.
- IME: no text, layer or sheet of any source names IME, IMC or IMD. No source gives the IME boundary or its plots.

## Conflicts

- Plot type: footprint blocks agree with the baseplan type on all 2,332 unit plots that sit under a block of their own type. Since Part W the other 246 (DP ends of townhouse rows) are wired to the townhouse row block they sit under, which is the row block and no type conflict. R06 agrees the same way on 2,578, R06_0000 on 232.
- Width against Rev 6: DP2 403 of 408 and DP4 684 of 688 at 10 or 12 m. TH1 and TH3 all match. C10 58 and VL3 41 plots are wider than Rev 6. VL4 is left out, its width comes from CAD (Bader, DATA-2). VL2 has no Rev 6 width.
- Setback: the drawn setback line sits 5.00 m behind the front on 2,530 plots and 3.00 m on 40, never at the Rev 6 5.52 m (C10 5.50 m).
- Road Layout counts: the stand-in had 202,320 entities, 5,464 blocks and 1,537 texts, the full read 208,363, 5,869 and 688. Road Surface 39 against 42 entities, Rough Grading 107 against 17. The two readers count nested and proxy pieces another way, not resolved.
- Faces with many labels: 17 in the baseplan, their labels have no face of their own.
- V9 numbering is no longer a conflict, it is reference only (Bader, DATA-2). analysis.json keeps its counts under referenceV9.

## Second method (R173)

- Fronts (plain math on the CadReader curb and setback pieces): seed 14 width 20 of 20, width class 20 of 20, setback class 20 of 20. Seed 7 19, 20, 19 of 20. At 5.00 m the same. At 3.00 m seed 14 20, 20, 20 and seed 7 19, 19, 20. Both seed 7 misses are corner plots where the nearest-to-curb edge is a chamfer (plot 735, 7.07 m against 14.00 m), the main code takes the front by its length limits.
- El @ Entry (brute force level on the surface named in roadLevelSurface): 20 of 20 bands match at seeds 14 and 7 for all three variants.
- Export against the earlier LandXML of the same name: 0 corners differ by more than 1 mm on the 7 road surfaces (Top Surface-02 773 equal, Top Surface-01 934 equal, EXISTING 5,362 equal). 124 corners have a level from one file only, at the border cut of the export.
- R168 again: rows match data.json on 14 of 14 drawings, the three new probe folders included, plots.csv seed 14 and seed 7 20 of 20.

## Part L, the Zone 4 location plan

- File: C:\02-Site Adaptation\Roshn Makkah - Master Plan Rev.09\01101-PSN-ZZZ-XXX-DWG-TU-005000.pdf, copied with the two Rev 9 DLUS workbooks into data_book\work, all three hash the same before and after. A second PDF of the same number in 01_Output\1.0 General\PDF is another export (other hash, 33,321 words), and 01_Output\1.0 General\DWG holds a 6.9 MB DWG of the sheet. Neither was read.
- Checked against the chat: one page, Rotate 90, A1 (2,384 by 1,684 pt, "Original Sheet Size:A1"), vector text (40,300 characters, 2 images), title PHASE 1 - Zone 4, LOCATION PLAN, created 25 Mar 2024, revisions 00 Nov 2023 and 01 Jan 2024. The Revit model name 01101-PSN-IMG-ZZZ-PIM-TU-000001 is not in the PDF, no text and no stream holds PIM. There are three control points, not one, the one the chat quoted is among them.
- Text lines: 3,182 numbers, 2,704 client codes, 206 OS, 71 SS, 241 other land uses. Pairs 3,181, 41 codes with no number, 1 number with no code, 0 duplicate numbers. Unit numbers run 428 to 3147, all labels 1 to 3183.
- Fit on 337 IMC plots, 316 kept: residual median 0.082 m, p95 0.145 m, max 0.234 m, 1.764 m per point (1:5000). On 410 IMD plots with shift 11: median 0.095 m, p95 0.179 m. The three control point marks land 0.265 to 0.305 m from their stated coordinates.
- Number to face: IMC shift 10 on 337 of 337, IMD 11 on 410 of 410. By NBH of the sales sheet: 012 (IMC) 10, 013 (IMD) 11, 021 11 on 432 of 432, 02 10 on 323 of 324, 022 10 on 274 of 301, 03 -8 on 259 and -2 on 223 of 490, 01A -8 on 196 and 10 on 85 of 297. 360 plots break the modal shift of their NBH, so a fixed shift holds for 012, 013, 021 and 02 only.
- Codes: through the old to new table every one of 2,591 codes with a sales row equals Typology_Rev9, 0 differ. 113 coded plots have no sales row (81 of them under the Zone 4 hatch). The 16 new codes equal Landuse_Level_4 of PH_1-SDF on every code, 2,704 and 2,704. On the 747 schedule plots Typology_Rev9 differs from the schedule unit on 8: 6 TH1 are TH1-IM in Rev 9, and 2301 and 2302 are VD4 in Rev 9 (DP4 in the schedule, VD4 on the link).
- Zones: the sales sheet gives Zone 1 to NBH 021 and 022, Zone 2 to 012 and 013, Zone 3 to 02 and 03, Zone 4 to 01A. The plan hatches 253 of the 297 01A plots and no plot of another zone. The zone outlines are drawn only in the key plan of the title block (one magenta and one red outline), not on the plan. No text names IME, IMC, IMD or IMG, so nothing on the sheet tells whether 02 or 03 is IME.
- Wiring (R183): point inside agrees with the 747 schedule plots on 746, 1 ambiguous, 0 wrong. plots.csv 115 columns: 2,570 of 2,578 unit plots carry a plan number and 2,566 a Rev 9 typology, 1,819 of the 1,831 unit plots with no schedule unit now have Typology_Rev9.
- Second method (R184), fit on IMD: label spelled 20 of 20 at seeds 14 and 7, face 20 of 20, code 18 and 19 of 20 with 2 and 1 with no sales row.

## Data quality

0 duplicated labels. 22 open faces inside Phase 1. 18 labels outside any face (baseplan reader). 3 plots cross the border. 275 plots have no front edge (mostly non-residential and corner plots). 0 plots claimed by both schedules. R06_0000 and R06_0001 are the same drawing.
