# Questions, fixed IDs and the closed list

Every question for Bader has a fixed ID, Q001 upward. An ID is never reused or renumbered. Open questions are listed by ID in Bader's turn of steps/PROGRESS.md and in the top entry of steps/log.md, with the same text in both. A closed question moves here with its answer and the date, and keeps its ID.

Round 17 gave the IDs. Each open question of the DATA-4 list kept its number as its ID, so Q001 is the old 1 and Q043 the old 43. The DATA-4 list had no 14 and no 34, those two were closed in DATA-4 and are below. Round DATA-5 ran beside Round 17, closed 43 and added 44 to 49, which keep those numbers as Q044 to Q049. Round 17 added Q050 to Q060.

## Closed

| ID | Question | Answer | Closed |
|---|---|---|---|
| Q014 | VL6 hand: is VL6 drawn the other hand from VL4 | Yes. Round DATA-4 read both prototypes in Revit (R245, R246): the VL6 gate and driver's room sit on the right seen from the road, the VL4 gate on the left | 2026-10-25, DATA-4 |
| Q028 | Zone 4: the 1.1 and 1.2 models hold 234 of the 297 plots of 01A, use them as its past key, still send the IMG job if it exists | Replaced by the answer keys (Bader): the IMG model is in C:\02-Site Adaptation\Revit All Zones, and the 7 Rev 6 Parsons site models there (IMA and IMB Zone 1, IMC and IMD Zone 2, IME and IMF Zone 3, IMG Zone 4) are the main reference for every rule. The Native 1.1 to 1.4 reads are a second reference only, for plots none of the 7 hold, and never for unit codes | 2026-10-25, ADDIN |
| Q034 | Rule 3 of the dialog watcher: where may it click | Inside the Revit or Civil 3D process this session started only, matched by process id. Load Once for this tool's DLLs, Do Not Load for any other add-in, and for an information dialog the button that leaves the file as it is. Never a window of another process. Bader restated it for Round 17 | 2026-10-25, DATA-4 and Round 17 |
| Q043 | The IFC in Downloads, 1TB02012-012D03-UNA-00-XX-MOD-UT-00010.ifc: is it the Infrastructure IFC package of the schedule notes | No. Round DATA-5 read its header from a copy (R258): it is another project, so it was not copied under C:\02-Site Adaptation | 2026-10-25, DATA-5 |
| Q046 | A probe pass for the unread alignments and parcels | Dropped (Bader): the add-in does not need the 154 alignments | 2026-10-25, ADDIN |
| Q050 | Level profile: no profile is the default, which one does every new build use | parsons2024Rev6 for every new build, Rev 6 prevails (Bader). native2022 stays in the repo for reference and is never the default | 2026-10-25, ADDIN |

## Old numbers to IDs

The steps files of three rounds named questions by the number they had in that round. This table gives the ID for each. A dash means the round had no such question. Closed before IDs means the question was settled before Round 17 and has no ID.

| Topic | Z4 | DATA-3 | 16 | ID |
|---|---|---|---|---|
| El @ Entry rule | 1 | 1 | 1 | Q001 |
| Ramp type | 2 | 2 | 2 | Q002 |
| Villa mirror | 3 | 3 | 3 | Q003 |
| Setback | 4 | 4 | 4 | Q004 |
| Plot front level | 5 | 5 | 5 | Q005 |
| Back corners | 6 | 6 | 6 | Q006 |
| Generic floor | 7 | 7 | 7 | Q007 |
| Round 16 test host run | 8 | 8 | - | closed before IDs, round 16 ran it |
| Units | 9 | 9 | 8 | Q008 |
| Unit codes with no schedule | 10 | 10 | 9 | Q009 |
| Widths | 11 | 11 | 10 | Q010 |
| IME | 12 | 12 | 11 | Q011 |
| FFL split | 13 | 13 | 12 | Q012 |
| CASE | 14 | 14 | 13 | Q013 |
| VL6 hand | 15 | 15 | 14 | Q014, closed |
| Yard depth | 16 | 16 | 15 | Q015 |
| Plots 2311 and 2312 | 17 | 17 | 16 | Q016 |
| Plots 2301 and 2302 | 18 | 18 | 17 | Q017 |
| The 58 past ramps above the surface | 19 | 19 | 18 | Q018 |
| RRE_ZZ_SP_Prototype-FFL | 20 | 20 | 19 | Q019 |
| Angle to true north | 21 | 21 | 20 | Q020 |
| Parallel windows | 22 | 22 | 21 | Q021 |
| TEST copies in Zone 02\ARC | 23 | 23 | 22 | Q022 |
| The empty site-adaptation-tool-15a folder | 24 | 24 | 23 | Q023 |
| Curb and ROW distance | 25 | 25 | 24 | Q024 |
| The 13 plots the PDF check refuses | 26 | 26 | 25 | Q025 |
| The two curbs | 27 | 27 | 26 | Q026 |
| Two DP4 labels in one face | 28 | 28 | 27 | Q027 |
| Zone 4 IMG past job | 29 and 33 | 29 and 33 | 28 | Q028 |
| Official numbers | 30 | 30 | 29 | Q029 |
| Plan plots with no sales row | 31 | 31 | 30 | Q030 |
| Location plan versions | 32 | - | - | closed before IDs, DATA-3 found the Rev.09 PDF the latest (R196) |
| Rev 9 plot faces | - | 32 | 31 | Q031 |
| The 1.4 key datum | 34 | 34 | 32 | Q032 |
| The DT3 plot alone on a VL3 face | 38 | 38 | 33 | Q033 |
| Rule 3 of the watcher | 40 | 40 | 34 | Q034, closed |
| round_z4\work and round_16\work | 41 | 41 | 35 | Q035 |
| Rev 9 workbooks | - | 42 | 36 | Q036 |
| Corner front | 37 | 37 | 37 | Q037 |
| Which road the 1.4 job used | 35 | 35 | 38 | Q038 |
| The 8 old prototypes of 20221206 - Links | 36 | 36 | - | closed before IDs, round 16 proved links by the footprint, Round 17 asks again as a new ID if the hands need them |
| Reader fix of Z4 | 39 | 39 | - | closed before IDs, round 16 kept it in PlacementRotation (R220) |
| Footprint test in every placer check | - | - | 39 | Q039 |
| Clusters from the footprint blocks | - | - | 40 | Q040 |
| Rows 2290 to 2293 and 2296 to 2299 | - | - | 41 | Q041 |
| Duplex plots in no cluster | - | - | 42, written 46 in bader-16.md before the merge renumbered the list | Q042 |
| The IFC in Downloads | - | - | - | Q043, from DATA-4, closed in DATA-5 |
