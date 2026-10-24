# Civil 3D investigation, round C3D, 2026-10-24

Read only, on working copies in Documents\SiteAdaptation\c3d_probe\work. The 10 sources hash the same before and after the round. Counts and distances only, no coordinates or levels.

## How it was read

- Civil 3D 2026 is installed in C:\Program Files\Autodesk\AutoCAD 2026 (acad.exe R25.1.74, accoreconsole.exe 25.1.74, AeccDbMgd.dll 13.8.280 in C3D, AecBaseMgd.dll 8.8.58 in ACA). Free memory at the start 4.87 of 15.78 GB, so the core console only, never the full window.
- No acad.exe or accoreconsole.exe was running at the start, so the Civil 3D session the prompt names was not open on this machine. Every process the round started has ended.
- The probe plug-in (R150) builds and is ready, but NETLOAD in the core console refuses every assembly outside TRUSTEDPATHS while SECURELOAD is 1. A three line test plug-in fails the same way and the /al bundle switch loads nothing. SECURELOAD was not changed.
- What ran instead: plain AutoLISP typed at the core console prompt (R153), which needs no plug-in, for the class and layer of every entity. ACadSharp (CadReader, no layer filter) for the plain AutoCAD content (R154). The LandXML export for the surface triangles (R155).

## What each drawing holds

| Drawing | Civil 3D objects | Plain content |
|---|---|---|
| Roads\Road Surface C3D | 7 TIN surfaces on layer 0, nothing else Civil | 92 polylines, 7 inserts, 6 dimensions |
| Roads\Road Layout | 2 TIN surfaces (C-TOPO), 1 alignment (C-ROAD), 3 profiles (C-ROAD-PROF), 1 assembly | RD-CURB-LINE 2548, ROW 916, RD-SHY 327, lane marks, road names, a bound CEC block |
| Roads\Rough Grading | 10 TIN surfaces (C-TOPO) | contour annotation, one TIN boundary 3D polyline |
| Roads\Typical Cross Sections | none | section drawings (kerb, pave, dimensions), not plan geometry, one missing title block xref |
| Base Plans\Baseplan | none | plot faces and labels, MP_ROW 133, xrefs CEC footprint, Base Plans road layout and building footprint |
| Base Plans\Makkah_Road Layout | none | RD-CRB-LN 4228, RD-SHY-LN 758, parking and centre lines |
| Base Plans\Building Footprint | none | 3780 unit blocks (Villa, Duplex, Townhouse, parking), MP_Setback Line 2699 |
| Base Plans\CEC Building Footprint | 1 parcel | 186,192 plain entities |
| Base Plans\All Phase Boundaries | none | 8 phase boundaries |
| Base Plans\Cutsheet - 500 | none | 44 sheet frames |

No drawing holds a corridor, a feature line, a grading, a COGO point or a pipe network. So no sidewalk link and no back of sidewalk point code exists. The only level texts are relative FFL notes inside the CEC blocks. All 10 drawings are in metres. The Base Plans road layout curb lies on the Roads curb (same 4.15 m median from the plot fronts), so the two road layouts line up.

## Surfaces

- The LandXML holds 7 surfaces in EPSG 32637, the drawing 7 AECC_TIN_SURFACE. The names in the drawing need the probe.
- Top Surface-01 and Top Surface-02 do not overlap (0 of 169,234 points of a 2 m grid in their shared box). They abut: 768 border vertices of Top Surface-02 touch Top Surface-01, level difference median 0, worst 2 mm.

## What connects to what, on the 337 IMC plots

- The ROW line of the Roads road layout lies on the plot front of all 337 plots (median 0 m). The plot line is the back of the sidewalk. The sidewalk runs from the curb to the ROW line, median 4.15 m (2.50 to 4.92 m).
- All 337 past ramps run from the plot line (within 2 mm) 5.5 m into the plot.
- The edge of Top Surface-02 runs along the plot line. Of the 674 front corners, 403 lie on it and the other 271 lie within 2 mm outside its last triangle (median 0.2 mm). That tiny gap is why round 13 found 119 ramps with no corner on any surface.
- One line: Top Surface-02 reaches the plot line on IMC, and Top Surface-01 does the same on IMD. Neither stops at the curb.

## Best source for the plot front level

| Source at the ramp front corners (674) | given | within 5 mm | within 30 mm | worse |
|---|---|---|---|---|
| Top Surface-02 snapped to its edge within 5 mm | 672 | 539 | 41 | 92 |
| Top Surface-02 where the corner is | 382 | 305 | 26 | 51 |
| Top Surface-02 at the nearest curb point | 672 | 6 | 34 | 632 |
| EXISTING (ground) | 674 | 0 | 0 | 674 |
| 3D lines within 5 m | 80 | 0 | 0 | 80 |

- The snapped Top Surface-02 gives all 119 uncovered ramps a level. The 92 worse corners sit on 58 plots in runs (2314 to 2330, 2348 to 2352, 2381 to 2390, 2598 to 2612 and others), where the past ramp sits a median 97 mm above the surface.
- Against El @ Entry, the mean of the two snapped corners is a median 55 mm lower, within 30 mm on 121 of 335 plots, so El @ Entry is not the plot line level.
- Coverage of plot fronts with both corners: IMC 337 of 337 (Top Surface-02), IMD 99.3 percent of 410 (Top Surface-01), the other 1,831 unit plots of the baseplan 98.6 percent from any design or road surface. IME has no schedule, no shift and no boundary on this machine.

## Plots 2311 and 2312

The Building Footprint drawing puts two mirrored Villa_VL4 blocks on baseplan faces 2301 and 2302 and nothing on OS 326. So it shows the baseplan cut (VL4, VL4, OS), not the DP2 pair of the past model.
