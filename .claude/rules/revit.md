---
paths:
  - "dotnet/SiteAdaptation.AddIn/**"
  - "dotnet/SiteAdaptation.Placer/**"
  - "dotnet/RevitPrototypeReader/**"
  - "dotnet/SiteAdaptation.TestHost/**"
---

# Revit add-in files

- Revit is started only from the worktree revit-test, a hook blocks every other folder, and since 12 Nov 16:55 only on Bader's order. Cowork writes these files, Claude Code never edits them, a fault goes in the Bridge report (docs/truth.md, Who does what).
- Use the shared lock file, siteBuild.lock of config/project.json, through python/window_lock.py.
- One Revit at a time on Bader's PC, the work machine since 12 Nov: it has 16 GB, under revitSlotsMinTotalGB of config/project.json, so python/window_lock.py gives one slot. One Revit or Civil 3D window at a time, Bader's rule of 12 Nov. The Revit part of a Bridge task runs with the PC to itself: no other lane, no Navisworks, Cowork idle, the free memory written before each press.
- A PR that touches these files holds a file in steps/revit-results/, or the label revit-later if the check is still owed.
- Run on copies of a model only. Read the result file, never the exit code. A locked screen stops a window press, and an unwatched run can freeze on Revit's Load Once prompt.
- The driver: a click, a key or a shot only with Revit in front, after 5 minutes idle and with no meeting window, and it stops when another input comes. On 5 Nov one click landed in a live meeting. When Revit shows the box "Security - Unsigned Add-In" for Site Adaptation, the driver presses Always Load itself, once, and writes the time in steps/revit-results. This is the one box it may press on sight, Bader's order of 6 Nov 23:37. Every other box keeps the driver rule, and a box of any other maker stays untouched. Measured on 6 Nov: a new build of the DLL brings the box back, so Always Load does not survive an install. Bader's order of 7 Nov 00:16: the Teams window "Meeting compact view" stays open on purpose, Teams keeps the laptop awake, so the driver does not count it as a meeting window. Since 7 Nov 17:00, by those words "ignore the teams", the same keep awake meeting in its full Teams view does not count either. Revit in front, 5 minutes idle and the stop on other input stay.
- Write "not seen in Revit" in the PR for anything a test proved and Revit did not.
