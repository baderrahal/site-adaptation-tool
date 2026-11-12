# revit-test heads-up, OLD-1a-revit-test

12 Nov 2026, 10:25. The PR is open and not merged, Lead merges. Detail and proof in steps/revit-results/OLD-1a-revit-test.md.

## Final statement

- Made: landxml under the work root, 7 LandXML files, one per surface of Roads\Makkah Phase 1_Road Surface C3D.dwg, from one window of Civil 3D 2026 under the window lock, 90 seconds.
- Points and faces read back by landxml_reader.py: Top Surface-02 325,959 and 584,130. Top Surface-01 578,300 and 1,044,562. Part 4 Top Surfaces 123,962 and 224,235. Compiled West_MM 161,458 and 285,055. Top Surface Part 2 East Of East 40,130 and 72,377. Missed from Paste 2,063 and 3,595. EXISTING_11.5 Sqkm 4,681 and 8,682.
- siteBuild.roadSurfaces: 6 of 6 names there, 0 missing. EXISTING_11.5 Sqkm is exported and not named in the config.
- The probe builds and runs against Civil 3D 2026 on this PC, 0 warnings and 0 errors. No code was changed.
- For Lead to accept or refuse: this PC holds no phase1_border.csv, which the probe needs. I gave it a square far wider than any drawing, so the files hold the whole surfaces, not the Phase 1 part. To refuse it, delete landxml.
- Not clean: Civil 3D ended with exit code 3762504530 after the probe wrote done true. Why is UNKNOWN.
- python/c3d_probe.json still names folders of Documents and of the laptop. Not used and not changed, the run took a settings file under the work root.
- Input folder: the drawing equal by SHA-256, size and write time before and after. 837 files, gone 0, added 0, size differs 0 by name and size. The copy is deleted.
- The driver: no click, key or shot by this lane. The watcher pressed No on one annotation scale box and Load Once on the security box of the probe, never Always Load.
- status of the task file is still open, the file is not in the allowed list of the task.
