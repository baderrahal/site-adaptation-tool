# Scan of the second input folder, 7 Nov 2026

Bader's order of 14:50, F15: the neighbourhoods of S1 not used for tuning hold no villa type. C:\10- ROSHEN MECCA was read as a project for the first time, read only, about 25 minutes. Counts and names only. The raw findings stay in the work root, folder roshen-scan, findings.md, ledger.jsonl and final.json.

## How it was read

- 560 files were marked by name, size, write time and SHA-256 before they were opened, and hashed again at the end: 0 changed (roshen-scan final.json).
- Workbooks by openpyxl in read only mode. Drawings by the CadReader of the repo's own build, copied into the work root and run from there. No Revit, no AutoCAD. Two .xls drawing registers were not read, xlrd is not installed.

## 1. The folder

2,727 files, 21.4 GiB: 5 top folders and 2 PDF files at the top. 0 xml files.

| Top folder | dwg | rvt | xls, xlsx, xlsm | pdf | other |
|---|---|---|---|---|---|
| 2022 | 525, 0.25 GiB | 77, 3.51 GiB | 7 | 573, 0.75 GiB | 282, 0.73 GiB |
| 20230306, a hand over to a consultant | 12, 0.56 GiB | 0 | 1 | 0 | 0 |
| 2024.03.27, the renumbering script | 355, 0.71 GiB | 101, 6.20 GiB | 15 | 409, 0.91 GiB | 357, 7.66 GiB |
| 2025.06.26, a master plan for the municipality | 1, 0.03 GiB | 0 | 0 | 2, 0.02 GiB | 4, 0.02 GiB |
| Master Plan Rev.09 | 1, 0.02 GiB | 0 | 2, 0.02 GiB | 1, 0.01 GiB | 0 |

## 2. Files that give a villa type per plot, untuned neighbourhoods

The tuned list is steps/truth-audit.md section 6: 012, 013, 021, 022, 02, 03, 01A.

- Workbooks: only sheet PH_1-DLUS of the two Rev 9 DLUS workbooks, columns Neighbourhood_ID, Typology and PLOT_NO. Both workbooks give the same counts. These two workbooks are the ones S1 already holds.

| Neighbourhood | Rows in PH_1-DLUS | Typology equal to a prototype code |
|---|---|---|
| 01B | 128 | 105 |
| 010 | 33 | 0 |
| 011 | 24 | 0 |
| 023 | 7 | 0 |

  010, 011 and 023 hold open space, plazas, multi-family, mixed use and services only. The sales sheet, the typology column the confirmed read takes today, holds the 7 tuned neighbourhoods only. PH_2 to PH_4 hold TBD.
- Drawings: inserts whose block name holds a prototype code, 1,342 in Makkah Building Footprint.dwg and 4,574 in MP-SK-01-03-006 Rev 9, two xref copies. Joined to a neighbourhood through the baseplan plot shapes: 0 for 01B, 011 and 023. 010 gets a few, the DLUS contradicts them.
- Site models: 21, all of the 7 tuned neighbourhoods. None names 010, 01B, 011 or 023. No schedule export of a model. The villa types inside an rvt stay UNKNOWN, reading them needs Revit.

## 3. The pick

01B has the most plots with a villa type, 105, from PH_1-DLUS. Lead's check on S1, the plot table of run 1 of FAST-3 against PH_1-DLUS by plot number:

- Of the 128 rows of 01B, 2 join a plot of the table with an outline, 12 join a plot listed apart with no outline, 114 join no plot. None of those 14 carries a typology equal to a prototype code.
- So the S1 baseplan does not draw the villa plots of 01B, and 01B cannot be built on S1 with villa links. A baseplan that draws them was not found: the Rev 9 drawing holds 309 coded inserts that fall in no plot of the S1 baseplan, their neighbourhood UNKNOWN.

Lead's read only check of 010 on S1, the same day: the baseplan labels give a prototype code for 11 of its 16 plots, the footprint blocks for 9, 7 of them equal to the label, while PH_1-DLUS calls 12 of the 16 multi-family or mixed use and 4 open space. The sales sheet has no row of 010.

## 4. C:\02-Site Adaptation against the manifest

528 of the 837 inputs rows of SA_COPY\manifest.csv are there, each equal by bytes and SHA-256, 0 differ, no file outside the manifest. All 309 rows of SA_COPY\need.txt of 28 Oct are still missing:

| Folder | Missing |
|---|---|
| 00_Revit Models\Zone 02\ARC\Prototypes_2024 | 273 |
| 00_Revit Models\Zone 02\ARC, the backup folder of the IMC TU model | 31 |
| 00_Revit Models\Zone 02\ARC, the IMC TU model | 1 |
| Master Plan, the two R06 drawings | 2 |
| 01_Output\1.11 References, two Excel lock files | 2 |

## Still UNKNOWN

- The villa types inside any rvt.
- The neighbourhood of the 309 coded inserts of the Rev 9 drawing that fall in no baseplan plot.
- Whether the baseplan plot labels use the Rev 9 plot numbers.
- On PH_1-DLUS-(Without Formulas) the column headed Neighbourhood ID holds phase names, not checked further.
