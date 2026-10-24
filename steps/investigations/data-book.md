# Data book, round DATA, 2026-10-24

The data book lives outside the repo in Documents\SiteAdaptation\data_book. Counts only here, no coordinates or levels. The 15 sources hash the same before and after the round.

## Phase 1 border

All Phase Boundaries holds 8 rings. Layer Phase 1 Bound, labelled PHASE - 1, is a 9-vertex ring (open polyline whose ends meet) of 2,517,500 m2 and 6,971 m perimeter. It holds 747 of 747 face centroids of the IMC and IMD schedule plots, and every other ring holds 0. So it was taken by layer and label.

## How each drawing was read

- The full Civil 3D window (Bader's decision) was tried three times on the smallest copy. Each time the runner stopped it within 16 s because free memory fell under the 2 GB floor of the round (1.75, 1.72, 1.94 GB), before NETLOAD, so no dialog came up and the watcher clicked nothing. Free memory before the start was 3.8 to 4.5 GB, held by other programs and sessions. Three failures, so the step stopped and went to Your turn.
- The vlax fallback needs the same full window, so it was not tried.
- Stand-in for all 14 drawings: CadReader (ACadSharp) inside Phase 1, every row marked reader CadReader. Civil 3D objects (the 7 road surfaces, 10 Rough Grading surfaces, the Road Layout alignment, profiles and assembly, the CEC parcel) are not in these tables. The road surface drawing gets surfaces.csv from its LandXML.

## What each source adds inside Phase 1

| Source | Kept rows (entities, blocks, texts) | Adds |
|---|---|---|
| Baseplan | 3,706, 0, 3,148 | plot faces and labels, the only client plot type |
| Building Footprint | 20,453, 3,780, 6 | unit blocks (type agrees with the baseplan on 2,332 unit plots), MP_Setback Line |
| Roads Road Layout | 202,320 (split by layer), 5,464, 1,537 | curb, ROW, lane marks, road names |
| Base Plans Road Layout | 8,389, 0, 2 | the same curb a second time, shy line |
| Cutsheet | 54, 0, 31 | sheet frame per plot (every plot) |
| CEC Footprint | 186,046 (split by layer), 1, 12 | CEC detail only |
| R06 | 119,761, 3,893, 80 | blocks on 63 to 68 percent of plots, the same types as the baseplan |
| R06_0000 and R06_0001 | 118,458, 2,267, 79 each | identical to each other, blocks on 9 to 12 percent of plots |
| V9 | 16,563, 586, 3,459 | its own plot numbering, on 86 to 99 percent of plots |
| Road Surface | 39, 0, 0, 7 surfaces | surfaces only (from the LandXML) |
| Rough Grading | 107, 0, 0 | its 10 surfaces need Civil 3D |
| Typical Cross Sections | 0 | sections lie outside the border, not plan geometry |
| Workbook | 9 sheets | area schedules, Phase1 sheet 2,701 villa and townhouse units, no plot rows |

## plots.csv

2,956 baseplan plots inside Phase 1 (6 dropped as outside), IMC 337, IMD 410, rest 2,209. 2,578 unit plots (C10, DP2, DP4, TH1, TH3, VL2, VL3, VL4) against 2,704 units of Rev 6 (126 fewer) and 2,701 in the Phase1 sheet. 48 columns plus a left and right level for each of the 7 surfaces, 62 in all.

Coverage (IMC, IMD, rest): official number and Rev 6 unit 100, 100, 0 percent. Footprint block 90, 90, 78. V9 number 99, 100, 86. Curb, ROW and sidewalk 100, 100, 88. Road level at both corners 100, 100, 86. Past FFL and El @ Entry 100, 100, 0.

## Answers

- Item 4, unit code source: no drawing or sheet gives a Rev 6 unit code for the 1,831 unit plots with no schedule. The footprint and R06 blocks name only client types (Villa_VL4, Duplex_DP2), and none of the 9 codes that exist in Rev 6 and not as client types (DP1, DP3, DT1, DT3, TH1-IM, VD1, VD2, VD4, VL6) appears in any table. TH1-IM is named nowhere.
- Item 5, entry level source: the road surface snapped at the two front corners against El @ Entry of the 747 schedule plots: within 5 mm 2, within 30 mm 259, worse 483, no level 3, median 55 mm below. No surface gives El @ Entry directly. The ramp front corners match the snapped road surface (539 of 672 within 5 mm, round C3D). Rough Grading is UNKNOWN until Civil 3D can read it.
- Item 6, IME: no text, layer or sheet of any source names IME, IMC or IMD. No source gives the IME boundary or its plots.

## Conflicts

- V9 numbers differ from the baseplan label on 2,633 plots with 1,164 different offsets, and never equal the official number. V9 is a separate numbering. No R06 drawing has plot number texts on the faces.
- Plot type: footprint blocks agree with the baseplan type on all 2,332 unit plots that have one. R06 agrees on 1,773, R06_0000 on 232, with no disagreement.
- Width against Rev 6: DP2 403 of 408 and DP4 684 of 688 at 10 or 12 m. TH1 and TH3 all match. C10 58, VL3 41 and VL4 70 plots are wider than Rev 6. VL2 has no Rev 6 width.
- Setback: the drawn setback line sits 5.00 m behind the front on 2,530 plots and 3.00 m on 40, never at the Rev 6 5.52 m (C10 5.50 m).
- Faces with many labels: 17 in the baseplan, their labels have no face of their own.

## Data quality

0 duplicated labels. 22 open faces inside Phase 1. 18 labels outside any face (baseplan reader). 3 plots cross the border. 275 plots have no front edge (mostly non-residential and corner plots). 0 plots claimed by both schedules. R06_0000 and R06_0001 are the same drawing.
