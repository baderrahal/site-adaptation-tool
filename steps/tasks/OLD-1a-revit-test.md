---
id: OLD-1a-revit-test
lane: revit-test
stage: 2
status: done
allowed:
  - steps/revit-results/
  - steps/heads-up/revit-test.md
---

# Scope

Round 1 step a of steps/plan.md, Bader's order of 12 Nov 03:25. Read docs/truth.md first, its standing rules hold here.

The road surfaces as LandXML. The input folder holds no LandXML file on this PC, so no plot gets a road level until this is done.

1. Hash the drawing Roads\Makkah Phase 1_Road Surface C3D.dwg of the input folder, size, write time and SHA-256, before anything else.
2. Copy that one drawing to a folder under the work root. This copy is the one Bader's order names, it is deleted in step 6.
3. Open the copy with Civil 3D 2026, one window, through the window lock of python/window_lock.py, with the probe of dotnet/SiteAdaptation.C3DProbe and python/c3d_run.py if they build and run against Civil 3D 2026 on this PC. If they do not, write the exact error into the record and stop for Lead. Change no code.
4. Export every surface of the drawing to LandXML into landxml under the work root, one file per surface or one file in all, as the probe does.
5. Read each file back with python/landxml_reader.py. Count the surfaces, and per surface the points and the faces. Name each surface of siteBuild.roadSurfaces of config/project.json that is there, and each that is missing.
6. Delete the copy of the drawing. Hash the drawing of the input folder again, it must be unchanged.

The driver rule of .claude/rules/revit.md holds: a person may be at this PC. One Revit or Civil 3D window at a time. Keep the part under 110 minutes. A surface that fails is recorded with its exact error and the run carries on.

The record is steps/revit-results/OLD-1a-revit-test.md: counts and names of surfaces only, no coordinate, no level, the repo is public. Take no rule id and no question id.

# Done when

- landxml under the work root holds the LandXML of the surfaces, and the record holds the table of surfaces with points and faces read back.
- The record says for each name of siteBuild.roadSurfaces whether it is there.
- The drawing of the input folder is unchanged by SHA-256, and the copy under the work root is gone.
- The PR body starts with `Task: OLD-1a-revit-test` and says what was opened in Civil 3D.
