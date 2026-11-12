# Storage of the work root, 12 Nov 2026

Round 0, item 1 of Bader's order of 12 Nov 03:25: delete the copies, after proving nothing in them is needed. Counts, names and sizes only.

## How it was measured

- The input folder C:\02-Site Adaptation was hashed again before any delete: 837 files, 8,874,086,637 bytes, against the record made after the work of the morning, old-project\inputs-after.json, gone 0, added 0, changed 0 (old-project\inputs-round0-before.json of the work root).
- Every file of scenarios and of prototypes_2024 of the work root was hashed, 1,629 files, 27,553,158,585 bytes, and sorted: equal by SHA-256 to a file of the input folder, a file of a Revit backup folder, a manifest of a copy, or none of these (old-project\classify.json of the work root).
- A file was deleted only when its SHA-256 is the SHA-256 of a file of the input folder in the hash of that same hour, and its size on disk was still the size hashed.

## The sort

| Folder | Equal to an input file | Revit backup folder | Manifest | None of these |
|---|---|---|---|---|
| scenarios\S1 | 345 files, 6,194,464,711 bytes | 0 | 1 | 0 |
| scenarios\S2 | 343 files, 6,184,929,941 bytes | 0 | 1 | 0 |
| scenarios\S3 | 327 files, 5,467,674,554 bytes | 0 | 1 | 18 files, 1,345,801,072 bytes |
| scenarios\S4 | 345 files, 6,194,464,711 bytes | 0 | 1 | 0 |
| prototypes_2024 | 39 files, 2,252,406 bytes | 195 files, 1,086,133,862 bytes | 0 | 13 files, 1,077,145,600 bytes |

## Deleted

- 1,360 files of S1, S2, S3 and S4, each equal by SHA-256 to a file of the input folder, 24,041,533,917 bytes.
- The manifests of S1, S2 and S4, 3 files, 218,718 bytes. The folders they list are gone.
- In all 1,363 files, 24,041,752,635 bytes. The folders S1, S2 and S4 are gone, S3 holds only the 18 files below.

## Kept, and why

The 18 files of S3 that are none of these. They are what the scenario script made for S3: the drawings saved as DXF and three workbooks written again. No file of the input folder equals them, so by the order they stay. S3_manifest.json stays beside them, it holds their SHA-256.

| File of scenarios\S3 | Bytes |
|---|---|
| Base Plans\CEC Building Footprint.dxf | 61,111,860 |
| Base Plans\Makkah All Phase Boundaries.dxf | 233,847 |
| Base Plans\Makkah Building Footprint.dxf | 3,412,727 |
| Base Plans\Makkah Phase 1 Cutsheet - 500.dxf | 130,335 |
| Base Plans\Makkah_Road Layout.dxf | 5,174,795 |
| Base Plans\ROSHN Makkah Baseplan.dxf | 5,681,561 |
| Master Plan\211222_ROSHN Makkah_LUS_Masterplan Rev06.xlsx | 114,602 |
| Master Plan\221229_MP-SK-01-03-001_R06.dxf | 146,393,177 |
| Master Plan\221229_MP-SK-01-03-001_R06_0000.dxf | 162,720,153 |
| Master Plan\221229_MP-SK-01-03-001_R06_0001.dxf | 162,720,153 |
| Master Plan\25.06.2025 Phase 1 Amana_V9.dxf | 206,640,178 |
| Roads\Makkah Phase 1_Road Layout.dxf | 255,847,134 |
| Roads\Makkah Phase 1_Road Surface C3D.dxf | 22,576,549 |
| Roads\Makkah Phase 1_Rough Grading.dxf | 93,202,469 |
| Roads\Makkah Phase 1_Typical Cross Sections.dxf | 68,263,473 |
| Roshn Makkah - Master Plan Rev.09\MP-SK-01-03-006_R9.0.dxf | 135,532,344 |
| Roshn Makkah - Master Plan Rev.09\PSN-ZZZ-XXX-SCH-TM-000201- DLUS Schedule_(Rev9)_Accomodation_Schedule.xlsx | 8,169,132 |
| Roshn Makkah - Master Plan Rev.09\TBD-PSN-ZZZ-XXX-SCH-TM-000201- DLUS Schedule_Rev9.0.xlsx | 7,876,583 |

The 13 villa files of prototypes_2024 that revit-test saved as Revit 2024 files on 12 Nov. None equals a file of the input folder by SHA-256, a Revit save never gives the same bytes twice, so by the order they stay.

| File of prototypes_2024 | Bytes | Against the Revit 2024 copy of the input folder |
|---|---|---|
| RRE-WP-NSP-KEO-ARC-C10-ZZ-MOD-1500.rvt | 85,377,024 | same size |
| RRE-WP-NSP-KEO-ARC-DP1-ZZ-MOD-2100.rvt | 86,732,800 | same size |
| RRE-WP-NSP-KEO-ARC-DP2-ZZ-MOD-2200.rvt | 79,659,008 | same size |
| RRE-WP-NSP-KEO-ARC-DP3-ZZ-MOD-2300.rvt | 81,924,096 | same size |
| RRE-WP-NSP-KEO-ARC-DP4-ZZ-MOD-2400.rvt | 75,452,416 | 4,096 bytes larger |
| RRE-WP-NSP-KEO-ARC-DT1-ZZ-MOD-3100.rvt | 107,859,968 | same size |
| RRE-WP-NSP-KEO-ARC-DT3-ZZ-MOD-3300.rvt | 115,576,832 | same size |
| RRE-WP-NSP-KEO-ARC-TH1-ZZ-MOD-3100.rvt | 69,414,912 | same size |
| RRE-WP-NSP-KEO-ARC-TH3-ZZ-MOD-3300.rvt | 70,275,072 | same size |
| RRE-WP-NSP-KEO-ARC-VD2-ZZ-MOD-2200.rvt | 71,053,312 | same size |
| RRE-WP-NSP-KEO-ARC-VD4-ZZ-MOD-2400.rvt | 72,921,088 | same size |
| RRE-WP-NSP-KEO-ARC-VL4-ZZ-MOD-1400.rvt | 76,902,400 | 4,096 bytes smaller |
| RRE-WP-NSP-KEO-ARC-VL6-ZZ-MOD-1300.rvt | 83,996,672 | same size |

The 13 folders named <file>_backup beside them, 234 files, 1,088,386,268 bytes, also stay. CHOSEN by Lead: they are not old saves of a file, they are the worksharing folder Revit keeps beside a central file, one .rws file and its .dat files each, so they belong to the 13 files above and go when those go. 39 of their small .dat files equal a file of the input folder by chance of content.

## The 2 villa files that differ by 4 KB

DP4 and VL4. No step needs the new one. Proof: the build takes its prototype folder from the input root with pastJob.upgradedPrototypeFolder (dotnet/SiteAdaptation.Common/Plan/BuildSteps.cs lines 288 to 312), and config/project.json line 44 names 00_Revit Models\Zone 02\ARC\Prototypes_2024 there. The folder prototypes_2024 of the work root is named only as the place Run-TestHost.ps1 -Upgrade writes to (TestHostPaths.ps1 line 170), no step reads it unless a run spec names it by hand. So the input folder copy is used, for all 13 files. Whether the content of the two pairs differs was not measured, the files were not opened.

So the 13 new files and their folders, 2,165,531,868 bytes, are needed by no step. They are kept only because no hash proves them equal to an input file. Bader's word deletes them.

## Sizes

| | Before | After |
|---|---|---|
| The work root | 27,645,405,299 bytes | 3,603,847,286 bytes |
| scenarios | 25,387,626,717 bytes | 1,345,874,082 bytes |
| prototypes_2024 | 2,165,531,868 bytes | 2,165,531,868 bytes |
| Free on drive C | 58,129,059,840 bytes | 82,173,153,280 bytes |

Measured once before the deletes and once right after. The work root shrank by 194,622 bytes less than was deleted, the hash lists and the list of the new check were written in between. Later runs add logs and temporary files, so the numbers move.

## C:\sa-test-none

- What made it: scripts/checks/test_set_machine.py. One of its refused roots was a folder on the drive root, and while Lead had the script broken on purpose on 12 Nov 01:51, to prove the test fails, the script made that folder.
- The fix, in this PR: every refused root of the test now stands under the folder of the test, and the test fails if a refused root leaves a folder behind. scripts/quick-tests.ps1 sends the temporary files of both test sets under tmp of the work root.
- Not deleted. The folder is still there, it holds one empty folder named SiteAdaptation. A safety check of the tool Lead runs in refuses to remove a folder on the drive root, twice, and Lead may not go around it. Bader's turn: delete C:\sa-test-none.

## The new check

scripts/checks/write_places.py, with its test. Before the tests it lists the names on the drive roots, in Documents and in the repo outside git. After the tests it lists them again and fails the PR on every new name that is not the work root or a build folder (bin, obj, .venv, __pycache__, .pytest_cache, .vs, TestResults). It runs as the first and the last step of job tests and inside the quick tests. CHOSEN by Lead: it sees a new name, it does not see a file added inside a folder that was already there on the drive root or in Documents.

## The second tidy, 12 Nov 17:44, Bader's order of 16:55

Deleted from the work root, by Bader's order:

| Folder | Files | Bytes | What it held |
|---|---|---|---|
| prototypes_2024 | 247 | 2,165,531,868 | The 13 villa files, 1,077,145,600 bytes, and their 13 backup folders. No step needs them, the build links the villa files of the input folder |
| scenarios | 19 | 1,345,874,082 | S3: 15 DXF saves, 1,329,640,755 bytes, 3 workbooks, 16,160,317 bytes, and S3_manifest.json, 73,010 bytes |

Moved, not deleted: 78 files named testhost_<code>_upgrade_<kind>, 320,872 bytes, from the work root itself into testhost\upgrade-12nov of the work root. The order counted 84. On disk were 78, 6 kinds for each of the 13 villa codes, RAN ls.

Free on C before 79,559,798,784 bytes, after 83,071,795,200. The work root after: 755,935,974 bytes. Its folders: Bridge, New projects, OLD1-site_347b04be, answer-key, checks, compare, dev-manifest-stamps, landxml, lanes, old-1a, old-1b, old-1e1, old-project, round_addin, testhost, tmp. No file lies in the work root itself.

Not done: the test host still writes testhost_<name>_<kind> files into the work root itself (dotnet/SiteAdaptation.TestHost/Run-TestHost.ps1 lines 121 to 129, JobSession.cs lines 44 and 65). That change is tool code, which Cowork writes since 12 Nov 16:55. The brief is Bridge\cowork-brief-testhost-folder.md under the work root.
