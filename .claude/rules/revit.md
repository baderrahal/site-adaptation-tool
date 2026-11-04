---
paths:
  - "dotnet/SiteAdaptation.AddIn/**"
  - "dotnet/SiteAdaptation.Placer/**"
  - "dotnet/RevitPrototypeReader/**"
  - "dotnet/SiteAdaptation.TestHost/**"
---

# Revit add-in files

- Only the revit-test lane opens Revit. A hook blocks every other lane.
- Use the shared lock file, siteBuild.lock of config/project.json, through python/window_lock.py.
- At most 3 Revit or Civil 3D windows on the machine. No RAM floor rule, the laptop has enough memory.
- A PR that touches these files holds a file in steps/revit-results/, or the label revit-later if the check is still owed.
- Run on copies of a model only. Read the result file, never the exit code. A locked screen stops a window press, and an unwatched run can freeze on Revit's Load Once prompt.
- Write "not seen in Revit" in the PR for anything a test proved and Revit did not.
