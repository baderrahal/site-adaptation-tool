# OLD-1a-revit-test, 12 Nov 2026

Lane revit-test on Bader's PC, branch task/OLD-1a-revit-test from origin/main 1f97b8e6. RAN means run in this session, with the file that holds the output. A file named without a drive lies under the work root C:\SA-work\SiteAdaptation, the files of this task in old-1a. Counts, names, sizes and times only, no coordinate and no level. No code was changed. Times are local, 10:08 to 10:12.

## Result

| Done when | Result | Proof |
|---|---|---|
| landxml under the work root holds the LandXML of the surfaces | 7 files, one per surface, 7 of 7 surfaces of the drawing | RAN, landxml, old-1a\Makkah Phase 1_Road Surface C3D\surfaces.csv |
| The table of surfaces with points and faces read back | below, 7 rows, each count equal by a second method | RAN python\landxml_reader.py, old-1a\readback_counts.json |
| Each name of siteBuild.roadSurfaces, there or missing | 6 of 6 there, 0 missing | config\project.json line 293 against the file names of landxml |
| The drawing of the input folder is unchanged by SHA-256 | equal, size and write time equal too | old-1a\source_before.json, old-1a\source_after.json |
| The copy under the work root is gone | gone, 0 drawings of that name under the work root | RAN |

## For Lead, read first

1. The probe builds and runs against Civil 3D 2026 on this PC. Both builds gave 0 warnings and 0 errors. The window ran 90 seconds, the probe 5 seconds of it.
2. CHOSEN by this lane, yours to refuse: the probe keeps only the triangles inside the Phase 1 border, which it reads from phase1_border.csv of the data book, and this PC holds no such file. Making it needs a copy of the boundary drawing, the schedules and the baseplan read (python\data_book_border.py lines 117 to 126), and the task allows one copy only. I wrote a border file that is a square of plus and minus 1,000,000,000 on both axes, no real coordinate, so the probe kept every triangle of every surface. Proof that nothing was cut: the faces of each file equal the triangle count Civil 3D itself gives for the surface, 7 of 7. So these files hold the whole surfaces, not the Phase 1 part the earlier export of the laptop held. To refuse it, delete landxml.
3. The export ran in the full Civil 3D window, python\c3d_window.py with the command C3DDATA, not in the core console of python\c3d_run.py. c3d_run.py holds no LandXML command, its four commands are C3DDUMP, C3DLEVELS, C3DBORDER and C3DLINES (c3d_run.py line 22), and it refuses a script with EXPORT (line 24). The LandXML is written by C3DDATA alone (dotnet\SiteAdaptation.C3DProbe\Civil\DataCommands.cs lines 573 to 578).
4. python\c3d_probe.json still names folders of Documents and of the laptop: probeRoot, dataBook.root, the window lock and landxmlDir (lines 8, 93, 185, 219). I did not use it and did not change it. I passed --config with a settings file under the work root, old-1a\c3d_probe_old1a.json, which holds only the keys c3d_window.py reads and no windowLock block. The lock was taken once, by python\window_lock.py around the command, from siteBuild.lock of config\project.json.
5. The builds went to old-1a\artifacts by --artifacts-path, so the repo holds no new bin or obj folder.
6. The probe writes into landxml of its own root. I moved the 7 files from old-1a\landxml to landxml of the work root, the place docs\truth.md names, with Move-Item, no file changed.
7. Civil 3D ended with exit code 3762504530, which is 0xE0434352, after the probe had written data.json with done true. The run record says done true, unchanged true, stopped null, error null. Why the exit code is not 0 is UNKNOWN, not looked into. No data.error.txt was written.
8. The names of siteBuild.roadSurfaces are the drawing name, two underscores and the surface name. That is the file name the probe gives, less .xml. The name inside each file is the surface name alone. I matched by file name. Which of the two the build asks for on this PC is UNKNOWN, not run here.
9. status in steps\tasks\OLD-1a-revit-test.md is still open. The task file is not in the allowed list of the task, so I did not set it.

## What was opened in Civil 3D

One window of Civil 3D 2026, acad.exe with the product switch C3D, on the copy old-1a\work\Roads\Makkah Phase 1_Road Surface C3D.dwg. The script was NETLOAD of the probe, C3DDATA, C3DQUIT. Nothing was saved, C3DQUIT closes with discard. No other drawing and no Revit was opened.

| Step | Result | Proof |
|---|---|---|
| Lock taken | 10:09:03, no window process, no lock, slot 1, 3.67 GB free | round_addin\logs\windows.jsonl, event lockTaken, round OLD-1a |
| Box "Annotation Scale - Large Number of Scales" | the watcher pressed No | old-1a\logs\20261112-100903_roadSurface.watch.json |
| Box "Security - Unsigned Executable File", naming the probe DLL | the watcher pressed Load Once, never Always Load | same file |
| Probe | done true, 7 surfaces kept, 5 seconds | old-1a\Makkah Phase 1_Road Surface C3D\data.json |
| Window | 90.0 seconds, lowest free memory 1.08 GB, the copy unchanged by SHA-256, no new file beside it | old-1a\logs\20261112-100903_roadSurface.run.json |
| Lock given back | 10:10:36, windowClosed with no process left, lockDeleted | round_addin\logs\windows.jsonl |

The driver: no click, key or shot by this lane. The watcher answered the two boxes of the process it started by an invoke on the button, no mouse and no keyboard.

## The build of the probe, measured first

| Command | Result |
|---|---|
| dotnet build dotnet\SiteAdaptation.C3DProbe\Civil\SiteAdaptation.C3DProbe.Civil.csproj -c Release --artifacts-path old-1a\artifacts | Build succeeded, 0 warnings, 0 errors, net8.0-windows, against C:\Program Files\Autodesk\AutoCAD 2026 |
| dotnet build dotnet\SiteAdaptation.C3DWatcher\SiteAdaptation.C3DWatcher.csproj -c Release --artifacts-path old-1a\artifacts | Build succeeded, 0 warnings, 0 errors |

RAN, with dotnet SDK 8.0.428, 9.0.313 and 10.0.400 on the PC. Python 3.12.10 of the .venv of the main repo folder.

## The surfaces

All 7 are TIN surfaces, none is a reference. Drawing points and Drawing triangles are what Civil 3D gives for the surface (surfaces.csv, columns points and triangles). Points and Faces are read back from the LandXML by python\landxml_reader.py. Every file is in meter, with 0 invisible faces, 0 bad faces and 0 bad points.

| Surface | Drawing points | Drawing triangles | Points read back | Faces read back | File size, bytes |
|---|---|---|---|---|---|
| Top Surface-02 | 325,960 | 584,130 | 325,959 | 584,130 | 32,817,494 |
| Top Surface-01 | 578,303 | 1,044,562 | 578,300 | 1,044,562 | 58,990,474 |
| Part 4 Top Surfaces | 123,970 | 224,235 | 123,962 | 224,235 | 12,095,165 |
| Compiled West_MM | 161,464 | 285,055 | 161,458 | 285,055 | 15,789,796 |
| Top Surface Part 2 East Of East | 40,130 | 72,377 | 40,130 | 72,377 | 3,813,800 |
| Missed from Paste | 2,063 | 3,595 | 2,063 | 3,595 | 176,198 |
| EXISTING_11.5 Sqkm | 4,681 | 8,682 | 4,681 | 8,682 | 405,563 |
| Total | 1,236,571 | 2,222,636 | 1,236,553 | 2,222,636 | 124,088,490 |

Faces equal the drawing triangles on 7 of 7. Points are fewer than the drawing points on 4 surfaces, by 1, 3, 8 and 6, 18 in all. The probe writes only the points a triangle uses and joins two points closer than 0.1 mm in plan (DataCommands.cs lines 569 and 592 to 602). Which of the two made each difference is UNKNOWN, not measured.

Each file is named Makkah Phase 1_Road Surface C3D__ and the surface name, with .xml.

## siteBuild.roadSurfaces

config\project.json line 293 names 6 surfaces, in this order. Each is there as a file of landxml of that name.

| Name in config | There | Points | Faces |
|---|---|---|---|
| Makkah Phase 1_Road Surface C3D__Top Surface-02 | yes | 325,959 | 584,130 |
| Makkah Phase 1_Road Surface C3D__Top Surface-01 | yes | 578,300 | 1,044,562 |
| Makkah Phase 1_Road Surface C3D__Part 4 Top Surfaces | yes | 123,962 | 224,235 |
| Makkah Phase 1_Road Surface C3D__Compiled West_MM | yes | 161,458 | 285,055 |
| Makkah Phase 1_Road Surface C3D__Top Surface Part 2 East Of East | yes | 40,130 | 72,377 |
| Makkah Phase 1_Road Surface C3D__Missed from Paste | yes | 2,063 | 3,595 |

Missing: none. Exported and not named in the config: EXISTING_11.5 Sqkm, 1 surface.

## Second check, by a different method

| Claim | First method | Second method | Result |
|---|---|---|---|
| Points and faces per file | python\landxml_reader.py, old-1a\readback_counts.json | a plain text scan in PowerShell that counts the lines starting with the P tag and the F tag, no reader code | equal on 7 of 7 files, both counts. 1 Surface tag per file |
| Faces per surface | the read back | the triangle count Civil 3D gives, surfaces.csv column triangles, taken by the probe from the surface itself | equal on 7 of 7 |
| The source drawing is unchanged | my SHA-256 before and after | the SHA-256 and size of Lead's list old-project\inputs-round0-after.json of 09:29 for this file | equal |
| The window closed and the lock is back | round_addin\logs\windows.jsonl | Get-Process finds no acad, accoreconsole, Revit or watcher, window.lock is not there | equal |

## The input folder

| Step | Result | Proof |
|---|---|---|
| The drawing before, 10:08 | 189,135,860 bytes, write time 2026-10-22 11:08:25 UTC, SHA-256 D4869B63931711F3B2D6A1904D87250D28C5343FAE21EF2FB564A235131705BE | old-1a\source_before.json |
| The copy, before the window | the same SHA-256 | RAN Get-FileHash |
| The copy, after the window, before its delete | the same SHA-256 | RAN, and unchanged true in the run record |
| The copy deleted | old-1a\work is gone, 0 files named like the drawing under the work root | RAN |
| The drawing after, 10:11 | the same size, write time and SHA-256 | old-1a\source_after.json |
| The whole folder, by name and size | 837 listed, 837 now, gone 0, added 0, size differs 0, 0 files written after 09:29 today | RAN against old-project\inputs-round0-after.json. Only the one drawing was hashed again, the other 836 by name, size and write time |

## Write places

| Command | Output |
|---|---|
| scripts\checks\write_places.py --save, before | PASS write places: listed 79 names before the tests |
| scripts\checks\write_places.py --check, after | PASS write places: nothing new on C:\, in Documents or in the repo outside the build folders |

git status of the worktree showed no file after the run, before this record.

## Left under the work root

- landxml: the 7 LandXML files, 124,088,490 bytes. They hold real coordinates and levels and stay out of git.
- old-1a\readback: the arrays of landxml_reader.py, one folder per file. old-1a\readback_counts.json, the counts.
- old-1a\Makkah Phase 1_Road Surface C3D: the tables of the probe, surfaces.csv, data.json, entities.csv, texts.csv, blocks.csv, layers.csv, civil.csv.
- old-1a\logs: the script, the watcher log, the run record.
- old-1a\artifacts: the two builds. old-1a\c3d_probe_old1a.json and old-1a\phase1_border.csv, the settings and the wide border of point 2.
- round_addin\logs\windows.jsonl: 3 new lines of round OLD-1a.

Nothing of this is needed again but landxml. The rest can be deleted once Lead has read it.

## Failed

No surface failed. The one thing not clean is the exit code of Civil 3D, point 7.
