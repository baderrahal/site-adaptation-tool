---
paths:
  - "dotnet/SiteAdaptation.AddIn/**"
  - "dotnet/SiteAdaptation.Placer/**"
  - "dotnet/RevitPrototypeReader/**"
  - "dotnet/SiteAdaptation.TestHost/**"
---

# Revit add-in files

- Only the revit-test lane opens Revit. A hook blocks every other lane. For the fast build, until 8 Nov, the lanes .claude/revit-lanes.txt names also open it for their own check before the PR, Bader's order of 7 Nov 10:05.
- Use the shared lock file, siteBuild.lock of config/project.json, through python/window_lock.py.
- At most 3 Revit or Civil 3D windows on the machine. No RAM floor rule, the laptop has enough memory.
- A PR that touches these files holds a file in steps/revit-results/, or the label revit-later if the check is still owed.
- Run on copies of a model only. Read the result file, never the exit code. A locked screen stops a window press, and an unwatched run can freeze on Revit's Load Once prompt.
- The driver: a click, a key or a shot only with Revit in front, after 5 minutes idle and with no meeting window, and it stops when another input comes. On 5 Nov one click landed in a live meeting. When Revit shows the box "Security - Unsigned Add-In" for Site Adaptation, the driver presses Always Load itself, once, and writes the time in steps/revit-results. This is the one box it may press on sight, Bader's order of 6 Nov 23:37. Every other box keeps the driver rule, and a box of any other maker stays untouched. Measured on 6 Nov: a new build of the DLL brings the box back, so Always Load does not survive an install. Bader's order of 7 Nov 00:16: the Teams window "Meeting compact view" stays open on purpose, Teams keeps the laptop awake, so the driver does not count it as a meeting window. Revit in front, 5 minutes idle and the stop on other input stay.
- Write "not seen in Revit" in the PR for anything a test proved and Revit did not.
