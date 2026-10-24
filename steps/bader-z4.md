# Round Z4, steps for Bader

Open the folder C:\Users\bader\Documents\GitHub\site-adaptation-tool in VS Code, then a PowerShell terminal (Terminal, New Terminal). Run each command in order.

## 1. Bring main up to date

```
git switch main
git pull
```

## 2. Look at the two images

They show the same area, 509 by 527 m. The first is the new file built from CAD (61 DT3 houses and their floors). The second is the past model (its floors only, its house files are not on this machine).

```
Invoke-Item "C:\Users\bader\Documents\SiteAdaptation\round_z4\images\testhost_Z4N_plan - Floor Plan - testhost Z4N area.png"
Invoke-Item "C:\Users\bader\Documents\SiteAdaptation\round_z4\images\testhost_K24_plan - Floor Plan - testhost K24 area.png"
```

Or open the folder C:\Users\bader\Documents\SiteAdaptation\round_z4\images in VS Code (File, Open Folder) and click each PNG. Look at plots 2252 and 2253, 2021 and 2022, 2031 and 2032: there the past ramp sits on the other edge of the plot (Q037).

## 3. Optional, open the new file in Revit 2024

Work on a copy so the round file stays as it was.

```
Copy-Item "C:\Users\bader\Documents\SiteAdaptation\round_z4\work\01101-PSN-Z4-TEST-NEW.rvt" "C:\Users\bader\Documents\SiteAdaptation\round_z4\work\01101-PSN-Z4-LOOK.rvt"
& "C:\Program Files\Autodesk\Revit 2024\Revit.exe" "C:\Users\bader\Documents\SiteAdaptation\round_z4\work\01101-PSN-Z4-LOOK.rvt"
```

Do not open Revit while the other session runs a window. Check first that this prints False:

```
Test-Path C:\Users\bader\Documents\SiteAdaptation\window.lock
```

## 4. Optional, run the compare again

```
.venv\Scripts\python python\z4_compare.py C:\Users\bader\Documents\SiteAdaptation\round_z4\cover.json --key C:\Users\bader\Documents\SiteAdaptation\round_z4\plan_z4_past.json --key-settings C:\Users\bader\Documents\SiteAdaptation\round_z4\work\project_z4_past.json --key-model C:\Users\bader\Documents\SiteAdaptation\round_z4\prototypes_z4_past.json --model C:\Users\bader\Documents\SiteAdaptation\round_z4\prototypes_z4_new.json --out C:\Users\bader\Documents\SiteAdaptation\round_z4\compare_new_model.json
```

## 5. Answer the questions

The questions this round added are Q028, Q032, Q033, Q034, Q035, Q037 and Q038 by fixed ID, each with the recommended answer first in Bader's turn of steps/PROGRESS.md. steps/questions-closed.md maps the old numbers 33 to 41 to these IDs.

## 6. When you no longer need the round files

Delete the look copy of step 3 and, once Q035 is answered, the folder C:\Users\bader\Documents\SiteAdaptation\round_z4\work.

```
Remove-Item "C:\Users\bader\Documents\SiteAdaptation\round_z4\work\01101-PSN-Z4-LOOK.rvt"
```
