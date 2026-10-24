# Data book, rounds DATA to DATA-5, 2026-10-24 to 2026-10-25

The data book lives outside the repo in Documents\SiteAdaptation\data_book. Counts and distances only here, no coordinates or levels. The 15 sources and the 15 working copies hash the same before and after rounds DATA and DATA-2, the 10 sources and copies of DATA-3 the same before and after DATA-3.

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
- Checked against the chat: one page, Rotate 90, A1 (2,384 by 1,684 pt, "Original Sheet Size:A1"), vector text (40,300 characters, 2 images), title PHASE 1 - Zone 4, LOCATION PLAN, created 25 Mar 2024, revisions 00 Nov 2023 and 01 Jan 2024. Corrected in round DATA-3: the title block does hold it. Its characters read "Plot Layout: 01101-PSN-IMG-ZZZ-PIM-TU-000001" and "CAD Ref: 2.1 Site Adapatation\Zone 4 \4.1.10 BIM Models\01101-PSN-IMG-ZZZ-PIM-TU-000001", PIM and IMG twice each. The DATA-2 search missed them. There are three control points, not one, the one the chat quoted is among them.
- Text lines: 3,182 numbers, 2,704 client codes, 206 OS, 71 SS, 241 other land uses. Pairs 3,181, 41 codes with no number, 1 number with no code, 0 duplicate numbers. Unit numbers run 428 to 3147, all labels 1 to 3183.
- Fit on 337 IMC plots, 316 kept: residual median 0.082 m, p95 0.145 m, max 0.234 m, 1.764 m per point (1:5000). On 410 IMD plots with shift 11: median 0.095 m, p95 0.179 m. The three control point marks land 0.265 to 0.305 m from their stated coordinates.
- Number to face: IMC shift 10 on 337 of 337, IMD 11 on 410 of 410. By NBH of the sales sheet: 012 (IMC) 10, 013 (IMD) 11, 021 11 on 432 of 432, 02 10 on 323 of 324, 022 10 on 274 of 301, 03 -8 on 259 and -2 on 223 of 490, 01A -8 on 196 and 10 on 85 of 297. 360 plots break the modal shift of their NBH, so a fixed shift holds for 012, 013, 021 and 02 only.
- Codes: through the old to new table every one of 2,591 codes with a sales row equals Typology_Rev9, 0 differ. 113 coded plots have no sales row (81 of them under the Zone 4 hatch). The 16 new codes equal Landuse_Level_4 of PH_1-SDF on every code, 2,704 and 2,704. On the 747 schedule plots Typology_Rev9 differs from the schedule unit on 8: 6 TH1 are TH1-IM in Rev 9, and 2301 and 2302 are VD4 in Rev 9 (DP4 in the schedule, VD4 on the link).
- Zones: the sales sheet gives Zone 1 to NBH 021 and 022, Zone 2 to 012 and 013, Zone 3 to 02 and 03, Zone 4 to 01A. The plan hatches 253 of the 297 01A plots and no plot of another zone. The zone outlines are drawn only in the key plan of the title block (one magenta and one red outline), not on the plan. No text names IME, IMC, IMD or IMG, so nothing on the sheet tells whether 02 or 03 is IME.
- Wiring (R183): point inside agrees with the 747 schedule plots on 746, 1 ambiguous, 0 wrong. plots.csv 115 columns: 2,570 of 2,578 unit plots carry a plan number and 2,566 a Rev 9 typology, 1,819 of the 1,831 unit plots with no schedule unit now have Typology_Rev9.
- Second method (R184), fit on IMD: label spelled 20 of 20 at seeds 14 and 7, face 20 of 20, code 18 and 19 of 20 with 2 and 1 with no sales row.

## Data quality

0 duplicated labels. 22 open faces inside Phase 1. 18 labels outside any face (baseplan reader). 3 plots cross the border. 275 plots have no front edge (mostly non-residential and corner plots). 0 plots claimed by both schedules. R06_0000 and R06_0001 are the same drawing.

## Round DATA-3, the Master Plan Rev 9 folder, El @ Entry by terms, the units on no plot and the location plan DWG

Sources read from copies in data_book\work: the Rev 9 DWG and its PDF plot, the two Rev 9 workbooks, the 01_Output location plan DWG and PDF, the Typical Cross Sections drawing, and the VL4 and VL6 prototypes (copied, not opened). All 10 sources and copies hash the same before and after. The 01_Output PDF was read once in place before its copy was made, then only from the copy.

### Part R, the two workbooks (R185 to R188)

- Both by the same author on 2024-06-25, the accommodation workbook saved 06:30 and TBD 08:42, 31 sheets each. Only the accommodation workbook holds JN Sales Data_Base_Rev09, Sheet1 and PH_1-SubPhase_ID (8.4.1). Only TBD holds Time Line, PH_1-Phase & Zone_ID (8.4.3) and Phase 1 Data (ROSHN)_2023-11-13.
- TBD is newer. Its Time Line has one row, Rev 9 on 2024-06-24: 3 utility plots deleted (112, 84, 3165), phase boundary revised, approved Amana plot numbers added. Its PH_1-DLUS lacks exactly those 3 plots and carries Amanah_PID on all 3,194 of its plots, where the accommodation workbook (3,197 plots) has it on none. Neighbourhood_ID changes on 474 plots by case only (01A to 01a, 01B to 01b). PH_1-SDF: 3 plots only in the accommodation workbook, 5 only in TBD. PH_1-DLUS-(Without Formulas), PH_2 to PH_4-SDF, the code table and Phase-02 to Phase-04 are equal cell for cell.
- Setbacks (R186), 2,897 DLUS plots joined, 2,559 unit plots: Front 5.50 m on 1,521 (villas and duplexes), 7.00 m on 1,031 (every TH type), none, 0.00 or 6.00 on 7. The drawn line of plots.csv (5.00 m on 2,514, 3.00 m on 34) equals the DLUS front on 0 of 2,552. Against Rev 6: 1,521 equal (5.50 against 5.52 and 5.50), 1,035 differ (the 7.00 m townhouses). Sides are written "0 / 2" (C10 "0 / 4.5", TH 0). Rear 5.00 m duplex, 3.50 m villa, 3.00 m C10, 7.00 m TH.
- Widths (R187), 2,555 unit plots with a sales row: Land Width equals the CAD width on 2,493 and Land Length the CAD depth on 2,538. C10: 61 plots wider than Rev 6 in CAD, Land Width equals CAD on 59, Typology_Rev9 C10 47 and VL6 14, the code width 16 m on all. VL3: 43 plots between the two Rev 6 widths (12.5 to 14 m), Land Width equals CAD on 42, Typology_Rev9 VL4 16, VD4 15, DP4 9, DP2 3, none VL3.
- Code table (R188): 18 rows, 16 old and 16 new codes, DP1 and DP4 give two new codes by form, DP2 and DP4-0 give none. In rules/roshn-makkah.json as typology_codes, 18 of 18 rows equal the sheet. Of the 20 codes the location plan prints (2,704 labels), 9 are in the table as written and 11 by their base, 0 outside it. Typology_Rev9 holds 15 codes, all new codes of the table.

### Part R, the Rev 9 drawing (R189 to R191)

- The DWG copy holds 641 classes, 308 of Civil 3D, with 3,480 AECC_PARCEL and 3,263 AECC_PARCEL_SEGMENT. ACadSharp drops them, so CadReader now writes the class list. Inside Phase 1: 226 of 469 layers hold entities, 8,938 texts, 2,687 unit code texts (old codes, TH3-0 500, DP4 455, DP2-0 350 and so on) and 787 cluster codes (T02, T01, D05 and so on) on MP_BLDG_ANNO_TYPENAME, a second copy of 3,467 on the Cabin Only layer, 171 number texts (stair numbers only), no setback layer, boundary layers Phasing-01 and Phasing-04. The plot line layer MP_Plot lies outside Phase 1 (795 faces, 0 inside).
- The full Civil 3D read for the parcels did not run. Civil 3D showed "Annotation Scale - Large Number of Scales" and the own-only watcher left it, as rule 3 of the round says. The run was stopped after 100 s, the copy unchanged, lowest free memory 2.82 GB.
- The PDF plot (title MP-SK-01-03-002-Phase-01(BLDG), CAD file MP-SK-01-03-006_R9.0.dwg, M9.0 25-JUN-24, note PLOTS AS PER CMP (REV-06) APPROVAL): 3,190 numbers, 2,704 new codes, 3,171 pairs. Fit on 2,874 location plan numbers, 2,502 kept, median 0.031 m. Wiring: pointInside 746 agree, 1 ambiguous, 0 wrong of the 747 schedule plots (chosen), nearest 744, 3, 0. DWG unit texts by pointInside: 2,491 agree with the location plan code by base, 21 ambiguous, 21 wrong.
- Faces: the plot strokes of the PDF close 3,086 faces. A second fit on the face centroids moves them 0.264 m and 0.08 m (median residual 0.010 m). Unit plots: 2,481 same, 58 shape differs (median Hausdorff 2.0 m), 39 with no Rev 9 face, all Hausdorff median 0.03 m. Numbers: 2,552 same, 3 differ (one number off), 6 with two labels, 13 with no plan number, 4 with no label. Codes: 2,551 same as Typology_Rev9, 5 differ (4 OS on faces with two plan numbers, plot 1770 DP2 against VD4). DWG unit text: 2,495 same, 21 differ (18 TH1 on TH1-IM plots). 115 unit plots differ in shape, number or code.
- Nothing in the DWG texts, layers or the PDF names IME, IMC, IMD, IMG, NBH or a zone, so Rev 9 does not tell NBH 02 from 03.

### Part E, El @ Entry (R192, R193)

- Cross sections, read whole: 13 titles A to L, 50 curbs with a sidewalk. Every curb is 0.12 by 0.20 m. The up-stand reads 0.070 m on most (local road 8 of 14, the others 0.020 and 0.150). The sidewalk carries a 2.0 percent text, widths 1.65 to 13.1 m by section (local road H and I 6.05 m). The rules profile holds no curb or sidewalk value.
- 747 schedule plots, within 5 mm, 30 mm, worse, no level: road at curb plus all terms 40, 156, 550, 1. Without up-stand 99, 289, 358, 1. Without sidewalk 39, 274, 433, 1. Without ramp 52, 270, 424, 1. The R170 corner variant 221, 193, 330, 3, still the best. By neighbourhood the best curb variant (no up-stand) IMC 38, IMD 61 within 5 mm. Curb plus up-stand plus sidewalk lands a median 64 mm above the road surface at the plot line, 9 of 743 within 30 mm.

### Part U, the units on no plot (R194)

- The 81 blocks (120 units) all lie in one unlabelled other face of the baseplan, the nearest labelled face a median 99 m away, the nearest unit face 188 m. By the TBD DLUS all are Zone 4: NBH 01b 72 blocks (109 units), NBH 01a 9 blocks (11 units). Both the location plan and the Rev 9 PDF have a plot label inside every block. Only 9 blocks have a sales row, the sales sheet holds no 01b plot.

### Part V, the VL6 hand

- Not run. Every Revit start shows the security dialogs of two other add-ins before ours, and rule 3 of the round lets the watcher click no dialog but its own. The CAD frame of R123 puts the VL4 link origin at the end front corner and the VL6 origin at the start corner, a hint and not the check. Steps for Bader: steps/bader-data-3.md.

### Part L2, the location plan DWG (R196)

- The 01_Output DWG is a Revit export (millimetres, created and saved 2024-03-26 02:15, logo images named after 01101-PSN-IMC-ZZZ-PIM-TU-000001). 3,182 plot texts, 3,181 numbers also in the PDF with the same code on every one, plot 17 only in the DWG (its land use holds a bar the PDF pattern does not read). DWG to page fit median 0.051 pt.
- The Rev.09 PDF was created 2024-03-25 17:08 and modified 2024-03-26 15:44, it holds the Plot Layout and CAD Ref lines and four ZONE notes. The 01_Output PDF was created 2024-03-25 17:15 and holds neither. So the Rev.09 PDF is the latest, the DWG its source with the same labels.

### Second method (R198)

- Seeds 14 and 7, 20 plots each: DLUS front, drawn front and class 20 of 20. Land Width, Length, Typology_Rev9 and width class 20 of 20. Rev 9 face overlap, class and number 20 of 20. The best El @ Entry variant (corner by typology) 20 of 20.

## Round DATA-4, the Rev 9 parcels, the VL6 hand and where El @ Entry came from

Every DWG, XML and workbook under the source root and the two prototypes, 167 files, were copied into data_book\work with their layout before they were opened (146 new copies). All 167 sources hash the same before and after and all 167 copies equal their source. Four windows, one at a time under the shared lock, no wait: Civil 3D twice on the Rev 9 copy (65 s and 45 s, lowest free memory 3.10 and 3.31 GB), Revit twice (VL6 1.0 min, VL4 0.55 min, 5.88 and 5.70 GB free at the lock). The Civil 3D watcher answered Annotation Scale - Large Number of Scales with No and the probe dialog with Load Once. In each Revit start the watcher answered Do Not Load for RCRC Green and RCRC Green Audit and Load Once for the three DLLs of this tool, inside our own process only. The registry was not read or written, the install ran with -SkipSigningReport.

### Part P, the Rev 9 parcels (R243, R244)

- The 3,480 parcels of the class list sit in the bound block MP-Base, so no site of the drawing holds them. The probe now reads parcels met inside a block. Inside Phase 1: 3,276 parcels, 3,266 with a number. Name equals Number on all 3,266, no number twice. StyleName holds the new code or the land use (DT3 500, DT1 298, DP2 244, VD4 229, DP4 226, C10 204, OS 203, VL4 201 and so on, 85 xx), Description the unit code.
- Numbers: 3,160 of the 3,266 are location plan numbers, 21 location plan numbers have no parcel. 3,159 are Rev 9 PDF numbers, 12 PDF numbers have no parcel. 90 parcel numbers are in neither: 85 xx (numbers over 4,000), 4 ROW, 1 hotel.
- Faces: the parcels coincide with the baseplan faces, median Hausdorff 0.000 m. Unit plots: 2,481 same, 65 shape differs, 32 with no parcel face. Numbers 2,554 same, 5 differ. Codes 2,554 same as Typology_Rev9, 6 differ (VD4 to OS 2, DP4 to OS 1, VD2 to OS 1, DP2 to OS 1, DP2 to VD4 1).
- The 115 differing unit plots of DATA-3 taken again: all 97 shape differences are real, the parcels hold them too, and the PDF faces equal the parcels at a median overlap over union of 0.997. Of the 3 number differences 2 are real (plots 1759 and 2000) and 1 came from the PDF read (488). All 5 code differences are real. The 21 DWG unit text differences (18 TH1 on TH1-IM plots) stay a matter of the DWG texts.

### Part V, the VL6 hand (R245, R246)

- The VL6 and VL4 test copies were read in Revit 2024 by a read job, detached with worksets discarded and closed without saving. VL6 holds 82 rooms (built on the VL3 rooms, with unplaced C10 rooms beside them) and 57 doors, VL4 37 rooms and 68 doors. Many elements repeat at one place, design options, and are taken once.
- In the link axes, by the plot frame of R123: VL4 plot 12 m, its gate at 0.325 of the front from the start corner, on the left seen from the road. VL6 plot 16 m, its gate at 0.631, on the right. The VL6 main doors sit at 0.618 and 0.517, its driver's room at 0.941, on the right with the gate. VL4 has no driver's room, its main doors sit at 0.903 (front) and 0.342 (side).
- So VL6 is drawn the other hand from VL4. All four prototype files hash the same after the reads.

### Part X, where El @ Entry came from (R242, R247 to R249)

- The two past schedules: in all 747 rows El @ Entry and FFL are typed values, Height is the formula FFL less El @ Entry (747 of 747), SSL a table formula FFL less 0.075. No external link. The note of both sheets says "FFL levels determined based on the plot entrance spot elevation information provided in the Infrastructure IFC Package", and El @ Entry is "the elevation at the top of the ramp infront of the Villa's entrance".
- No IFC file and no LandXML is under the source root. One IFC sits outside it in Downloads (1TB02012-012D03-UNA-00-XX-MOD-UT-00010.ifc), not opened.
- Surfaces on this machine: of 160 DWGs only the three road drawings hold Civil 3D surfaces: Road Layout 2 (saved 2023-03-06), Road Surface C3D 11 (saved 2023-03-06), Rough Grading 10 (saved 2023-02-12). No older version exists. The 01_Output grading plans are Revit exports of the past model with its own spot elevation texts.
- The 747 schedule plots on every surface version, within 5 mm, 30 mm, worse, no level: the snapped road level 221, 193, 330, 3 (IMC 96, 92, 149, 0, IMD 125, 101, 181, 3), median 10 mm low, and its shape fit after the median is worse (61 within 5 mm), so no constant datum hides behind it. Top Surface-01 alone (IMD) 58, 42, 87 and Top Surface-02 alone (IMC) 47, 43, 65 where they have a level. Every other surface sits 0.59 to 1.51 m low with 0 within 5 mm.
- The 1.4 key on NBH 022, 294 plots: by its El @ Entry parameter the road level gives 3 within 5 mm, median 178 mm low, shape fit 37. By its ramp corners less the datum, 3 within 5 mm, median 212 mm low, but 164 within 5 mm once the 212 mm is taken off. So the 1.4 ramps follow the shape of today's Top Surface-02 a steady 0.21 m higher.
- Most likely: the IMC and IMD schedules took their levels from today's road surfaces through the plot entrance spots of the Infrastructure IFC package, the 1.4 job from the same road shape 0.21 m higher, an older or raised version that is not on this machine.

### Second method (R250)

- Parcels: seeds 14 and 7, 20 unit plots each, parcel, location plan number and class 20 of 20. The VL6 hand from the reader rows: the gate shares of both prototypes and the verdict agree. The best surface version (snapped road level): bands 20 of 20 at both seeds by brute force.

## Round DATA-5, the audit of DATA-4, the missing xrefs, the reader counts, the area key, the IFC and the coverage

All 576 files under the source root and the IFC in Downloads hash the same before and after the round. The 170 copies of the data book and the new IFC copy (work\Downloads) equal their source after. Five Civil 3D windows, one at a time under the shared lock (round DATA-5), only in our own process: the first stopped at the 2 GB floor after 16 s (free 4.24 GB at the start, 1.65 GB lowest, copy unchanged, its two lock files removed), then Road Surface 30 s, Road Layout 25 s, Rough Grading 25 s, the Base Plans road layout 30 s, lowest free 2.05 to 3.00 GB. Road Layout and Rough Grading ended with 0xC0000005 and 0xC0000409 after C3DQUIT, after the DXF was written. The watcher answered Annotation Scale - Large Number of Scales with No once and the probe dialog with Load Once four times. The registry was not read or written.

### Part Q, the audit of DATA-4 (R251)

- The 115 rows of parcel_redo.csv: 97 plots hold a shape, number or code verdict (87 shape only, 4 shape and code, 1 shape, number and code, 1 shape and number, 1 shape and the PDF number, 3 shape and DWG text) and 18 hold only the DWG unit text (16 TH1, 2 TH3). DATA-4 added the verdicts up, 97 shape, 3 number (2 real, 1 PDF read) and 5 code, to 105, but every number and code verdict sits on a plot that also differs in shape. So 97 plots, not 105, and the other 18, not 10, are the DWG text plots.
- The sixth parcel code difference is plot 2302 (a VL4 face, Typology_Rev9 DP2). Its face holds two location plan numbers (2312 and 216), so the PDF compare had no code to compare (noLabel) and only a shape verdict was set. The parcel that matches the face is 216, an OS parcel. Four of the five real code differences are the same case, an OS parcel on a face with two plan numbers. Only plot 1759 (DP2 to VD4, one plan number) is a code change on its own.
- Recount: 29 of 30 counts the DATA-4 records give equal the tables. The one that does not is the 105 above. Counted from parcels.csv alone 5 of them fall short (shape 62 against 65, no face 35 against 32, numbers 2,550 and 4 against 2,554 and 5, codes 2,551 against 2,554), since 8 unit plots share their parcel with another face and parcels.csv names one face per parcel. rev9_parcels.py now also writes parcel_matches.csv, one row per face, the three old outputs byte equal. Seeds 14 and 7: 20 of 20 recounted plots equal their stored classes.

### Part X, the four missing xrefs (R252)

| Xref | Asked by | Path asked | Inserts in model space | Found |
|---|---|---|---|---|
| Makkah Phase 1_Road Layout_new | R06 | .\Makkah Phase 1_Road Layout_new.dwg | 1, at the origin, scale 1 | no |
| the same | R06_0000 | the same | 0 | no |
| the same | R06_0001 | the same | 0 | no |
| XR-TITLE BLOCK | Typical Cross Sections | XR-TITLE BLOCK.dwg | 0 | no |

- Searched by file name under the source root (Native Files and 01_Output included), Downloads and the data book work folder, 15 drawings read, 7 xrefs, the 3 of the baseplan found. Nearest names of the road xref: the two road layouts on disk. Downloads holds a ZONE 1 CAD Title Block.dwg, another name, not opened.
- What they hold inside Phase 1 cannot be read without the files. The road xref sits at the origin at scale 1, so it keeps its own coordinates. R06 was saved 2022-12-29 22:59. Both road layouts on disk were created 2021-11-27 04:30 like R06 and saved later (Base Plans 2023-01-31 01:49, Roads 2023-03-06 17:29), so the _new layout R06 asked for is older than both. The title block xref has no model space insert and holds no plan data.
- R06_0000 and R06_0001 carry a header save of 2026-10-22, the day the folder came to this machine (file time 14:29 +03, header time 23:29 as CadReader reads it, the two do not agree). They look like local saves of R06, not client files.

### Part C, the reader counts and the two road layouts (R253 to R255)

Third count: DXFOUT of each copy in our own window, read as plain group code pairs, every insert expanded through its blocks. Rows per type with one row per entity, cut parts apart, LWPOLYLINE and POLYLINE as one type:

| Road Layout type | DXF | Probe | CadReader |
|---|---|---|---|
| LINE | 165,529 | 165,529 | 163,289 |
| POLYLINE | 16,829 | 16,831 | 15,600 |
| ARC | 14,655 | 14,761 | 14,213 |
| HATCH | 6,554 | 6,534 | 6,091 |
| SPLINE | 1,267 | 1,267 | 1,203 |
| CIRCLE | 1,101 | 1,101 | 1,002 |
| ELLIPSE | 902 | 902 | 902 |
| TEXT, MTEXT | 487, 198 | 487, 201 | in texts.csv |
| DIMENSION, WIPEOUT, LEADER | 439, 281, 9 | 439, 281, 9 | none |
| AECC_TIN_SURFACE | not written | 1 | none |
| Inserts top and nested | 824 and 5,045 | 824 and 5,045 | 5,464 in one list |
| Attributes | 879 | none | 852 as ATTRIB texts |

- Road Layout 208,363 against 202,320: the probe writes TEXT, MTEXT, DIMENSION, WIPEOUT, LEADER and the surface as entity rows (1,418), CadReader writes none of them. CadReader also misses 4,625 geometry rows, and the two make the whole gap of 6,043: 3,423 under the one insert of MP_CEC (the CEC footprint bound in, its insert point outside Phase 1), 1,083 under 34 dynamic ARROW blocks, 128 under 4 External Road ARROW blocks. Blocks 5,869 against 5,464: the probe counts 824 top and 5,045 nested inserts, CadReader keeps 5,464 with no parent. Texts 688 against 1,537: CadReader counts 852 attribute values as texts (vehicle tracking tags such as atenv and atelem on layer 0), the probe writes none.
- Road Surface 42 against 39: both hold the same 30 polylines, cut at the border into 7 extra parts by the probe and 9 by CadReader, and the probe adds 1 dimension and 4 surface rows. Rough Grading 17 against 107: both hold the same 5 polylines, CadReader writes 102 parts of the cut and the probe 6, and the probe adds 6 surface rows.
- The probe count is right. The DXF agrees with it on every type but 106 arcs (the DXF judges an arc by its centre), 20 hatches and 3 MTEXT at the border. The probe loses only the 879 attribute values, which are no plot or road data. DXFOUT writes no Civil 3D object at all (the Road Layout classes list 2 surfaces, 1 alignment, 3 profiles, 1 parcel and 1 assembly with no record), so only the probe reads them.

The two road layouts, facts for Bader to ask which is current:

| | Roads Road Layout | Base Plans Makkah_Road Layout |
|---|---|---|
| Created | 2021-11-27 04:30 | 2021-11-27 04:30 |
| Last saved (header) | 2023-03-06 17:29 | 2023-01-31 01:49 |
| Size, model space entities | 82 MB, 7,049 | 1.5 MB, 9,554 |
| Paper space, title block, revision text | none, none, none | none, none, none |
| Model space texts | 406 (road names, ROW names) | 8 (control points, bus stops) |
| Curb points with no other curb within 1 m | 13,335 of 92,346 (14.4 percent), 954 stretches, 13 over 50 m, longest 288 m | 14,859 of 92,250 (16.1 percent), 1,007 stretches, 17 over 50 m, longest 328 m |
| Where, by a 3 by 3 grid of Phase 1 | middle 3,163, W 2,884, NE 2,771, N 1,983, E 1,632 | E 3,322, middle 3,004, NE 2,765, W 2,592, N 1,204, S 1,075 |

- On the plan image (data book data5\road_layouts\curb_difference.svg) the Roads curb parts from the other at nearly every junction, on the corner returns, and along the arterial edges of the north and east border. The Base Plans curb parts as short breaks mid block on the local streets, small closed shapes and a stepped edge of bays along the south border, with one straight stretch across it. So the two differ in junction corners and curb breaks, not by a shift.

### Part A, the area key (R256, R257)

| Zone and NBH (TBD PH_1-DLUS) | Plots |
|---|---|
| Zone 1 021, 022 | 475, 340 |
| Zone 2 012, 013 | 380, 467 |
| Zone 3 02, 03 | 357, 540 |
| Zone 4 01A | 323 |
| Zone 5 010, 011 | 40, 12 |

- 2,934 of 2,956 plots get one key (2,904 by the location plan number, 30 by the Rev 9 parcel), 0 with two values. 22 have no DLUS row: 11 public parking (plan numbers 3167 to 3177), 8 SS, 1 OS, 1 buffer, and the DP4 face 2000 whose plan number 3178 is past the DLUS. Unit plots 2,577 of 2,578. Against the sales sheet 2,580 same, 0 differ, 354 with no sales row (OS 190, SS 40, then other land uses, 4 VL4). No baseplan face lies in 01B, its 120 units sit on no face.
- IM codes, file names and texts only, no Revit file opened:

| Code | File names | Texts | Plots of its files by NBH |
|---|---|---|---|
| IMA | 4, Native Files 1.1 | none | not read |
| IMB | 4, Native Files 1.2 | none | not read |
| IMC | 221 (00_Revit Models Zone 02, 01_Output, Native Files 1.3) | 1 (the past model record) | IMC schedule 337 in 012, IMC past model 337 in 012 |
| IMD | 198 (00_Revit Models Zone 02, 01_Output, Native Files 1.4) | 15 (the 1.4 model record and paths) | IMD schedule 410 in 013, 1.4 model 336 in 012 and 296 in 022, 1 no key |
| IMG | 0 | 2, title block of the Rev.09 location plan, Zone 4 | no file |
| IME, IMF and any other | 0 | 0 | none |

- In Native Files the letter follows the folder, 1.1 IMA, 1.2 IMB, 1.3 IMC, 1.4 IMD, numbered 000001 to 000004, and the 1.4 model paths read 4. Phase 1.4 and Site Adaptation\Phase 4. The one of them read (1.4, IMD) holds 012 and 022, not 013. So an IM letter may name a delivery package rather than a neighbourhood. PH_1-SubPhase_ID of the accommodation workbook names sub-phases 1A (Zones 1 to 3), 1B (Zones 3 and 4) and 1C (Zone 5), no IM letter.
- config/area_codes.json, proposed: IMC 012 and IMD 013 measured (with the 1.4 model listed as a conflict), IMG Zone 4 by text, IMA 021, IMB 022, IME 02 and IMF 03 guessed from the letter order. The table agrees with the IMC and IMD schedules and the IMC model and lists the 1.4 model as its conflict.
- IME: nothing on this machine names it. If the letters go two to a zone in order, as C and D are 012 and 013, IME would be 02 and IMF 03. That is a guess from letter order only.

### Part I, the IFC in Downloads (R258)

- Found in Downloads\New folder, not in Downloads itself, beside a DWG, an NWC and an SDW of the same number. Copied to work\Downloads, source and copy hash the same before and after.
- Header: FILE_NAME of 2023-08-06 17:47:21, author LEGION, no organisation, written by Autodesk Civil 3D 2023 through the EXPRESS Data Manager, schema IFC4X1, view CoordinationView_V2.0 for Architecture. IfcProject 1TB02012-012D03-UNA-00-XX-MOD-UT-00010, IfcSite with no name and latitude and longitude 0, the projected CRS empty and the map conversion all zero.
- 181,190 records: 43,289 points, 40,665 faces, 342 building element proxies (BUILDINGELEMENTPROXY(n), all Wall Construction), 32 alignments with 2D horizontal segments only, 188 property sets of AMA_ fields (project and program 1TB02012, contract 012D03, originator UNA, discipline UT, phase DD, level GF, 01).
- 0 of 42,539 points lie inside Phase 1 and their box does not meet the box of the border, their heights do not meet the Phase 1 road levels. No spot, entrance, plot, surface or level entity, the only level word is AMA_Level (GF, 01).
- It is not the Infrastructure IFC package the schedule notes name: another project and client numbering (1TB02012 and AMA_ fields against 01101 and 00101-PSN), utility walls, no site position, nothing inside Phase 1 and nothing at the plot front. Its sibling folders in Downloads are named Four Seas Resort and IFCs Dated 27-07-23 Four seasons. Nothing was wired from it.

### Part T, the coverage (R259)

| File | Reader | Space | Kept inside Phase 1 | Civil 3D in the file | Civil 3D read | Not read and why |
|---|---|---|---|---|---|---|
| Roads/Road Layout | C3DProbe | site | entities 208,363, blocks 5,869, texts 688, surfaces 2 | 2 surfaces, 1 alignment, 3 profiles, 1 assembly, 1 parcel | 2 surfaces, alignment and assembly outside | 879 attribute values (vehicle tracking tags). 1 parcel in a block, the R243 run not repeated |
| Roads/Road Surface C3D | C3DProbe | site | entities 42, surfaces 7 | 11 surfaces, 154 alignments with label groups | 7 surfaces, 1 point group of 0 points | 154 alignments the probe does not reach (no siteless alignment, no site, none in model space or its 7 inserts) |
| Roads/Rough Grading | C3DProbe | site | entities 17, surfaces 9 | 10 surfaces | 9 surfaces | the volume surface has no triangles |
| Roads/Typical Cross Sections | CadReader | own | 0 by design | none | none | read whole for curbs and sidewalks (R192), xref XR-TITLE BLOCK missing |
| Base Plans/CEC Building Footprint | CadReader | site | entities 186,046, blocks 1, texts 12 | 1 parcel | none | the parcel, the probe never ran on it |
| Base Plans/All Phase Boundaries | CadReader | site | entities 12, texts 1 | none | none | nothing known |
| Base Plans/Building Footprint | CadReader | site | entities 20,453, blocks 3,780, texts 6 | none | none | 81 unit blocks on no face (01b, 01a) |
| Base Plans/Cutsheet | CadReader | site | entities 54, texts 31 | none | none | nothing known, two old lock files beside its copy |
| Base Plans/Road Layout | CadReader | site | entities 8,389, texts 2 | none | none | which layout is current is open |
| Base Plans/Baseplan | CadReader | site | entities 3,706, texts 3,148 | none | none | 15 MULTILEADER with no text, no face for 01b |
| Master Plan/LUS Rev06 workbook | openpyxl (R165) | none | 9 sheets | none | none | no plot rows |
| Master Plan/R06 | CadReader | site | entities 119,761, blocks 3,893, texts 80 | 5 parcels | none | xref Road Layout_new missing, the 5 parcels |
| Master Plan/R06_0000 | CadReader | site | entities 118,458, blocks 2,267, texts 79 | 1 parcel | none | the same xref, the parcel, saved on this machine |
| Master Plan/R06_0001 | CadReader | site | the same as R06_0000 | 1 parcel | none | identical to R06_0000, skipped by decision C |
| Master Plan/Amana V9 | CadReader | site | entities 16,563, blocks 586, texts 3,459 | none | none | reference only |
| Rev.09/location plan PDF | pdfplumber (R179) | page fitted to site | 3,181 labels | none | none | 1 number with no face, 41 codes with no number |
| Rev.09/Rev 9 DWG | C3DProbe | site | entities 252,741, blocks 16,065, texts 9,274 | 3,480 parcels | 3,276 parcels inside | the Cabin Only text copies not used, MP_Plot outside |
| Rev.09/Rev 9 PDF | pdfplumber (R189) | page fitted to site | 3,190 numbers | none | none | 6 faces with two labels |
| Rev.09/accommodation workbook | openpyxl (R185) | none | sales sheet 2,591 plots | none | none | no 01b sales row |
| Rev.09/TBD workbook | openpyxl (R185, R256) | none | PH_1-DLUS 3,161 plots | none | none | 13 plan numbers past its last plot |

Which source is the truth for what:

| What | Source | Check |
|---|---|---|
| Plot faces | Baseplan faces | Rev 9 parcels 2,481 of 2,578 unit plots the same, median Hausdorff 0.000 m, 20 of 20 at both seeds (R250, R260) |
| Unit blocks | Building Footprint blocks | type agrees on 2,332 of 2,332, R06 on 2,578, units 2,698 against the sheet 2,701 |
| Coordinates | Site metres of the drawings, the Phase 1 ring | 747 of 747 schedule plots inside, location plan fit median 0.082 m |
| Curb and ROW | Roads Road Layout (probe) | 79,011 of 92,346 Roads curb points within 1 m of the Base Plans curb, which is current is open |
| Road level at the plot front | Top Surface-02 and -01, snapped | 221 of 747 within 5 mm, bands 20 of 20 by brute force (R173, R250) |
| Area key | TBD PH_1-DLUS Zone and Neighbourhood_ID | sales sheet 2,580 of 2,580 the same, 20 of 20 and 19 of 20 plus 1 with no row at seeds 14 and 7 (R260) |

Data no file on this machine holds, to ask the client for:
- IME: no file, text or path names it, and none of IMF or another letter. Which neighbourhood each IM letter is, or that the letters name packages.
- 01b: no baseplan face (120 units on no face) and no sales row (113 plan plots with no sales row, 81 under the Zone 4 hatch).
- The El @ Entry rule or the Infrastructure IFC package with the plot entrance spots. The IFC in Downloads is another project.
- The Zone 4 IMG past job 01101-PSN-IMG-ZZZ-PIM-TU-000001, named only by the location plan.
- Which road layout is current, neither carries a revision text. The xref Makkah Phase 1_Road Layout_new of R06 and XR-TITLE BLOCK of the cross sections.
- The ramp slope (architect), the road version the 1.4 job used.

### Second method (R260)

- Area key from the DLUS xlsx XML: seed 14 20 of 20, seed 7 19 of 20 and 1 plot correctly with no DLUS row. Parcels by ray cast on the raw civil.csv lines: parcel, plan number and class 20 of 20 at both seeds. Curb distance by brute force over every segment: far or near 20 of 20 at both seeds.
- DXF line scan against the parser: 13, 4 and 8 types equal, BLOCK records 1,109, 80 and 67 equal. IFC types by one regular expression per line: 49 of 49 equal. IM file names by pathlib: IMA 4, IMB 4, IMC 221, IMD 198, all equal.
- The check found a fault in itself: the line scan counted the EOF record when ENTITIES was the last section, fixed before the run on the real files.
