# Round 16, steps for Bader

Open the folder C:\Users\bader\Documents\GitHub\site-adaptation-tool in VS Code, then a PowerShell terminal (Terminal, New Terminal). Run each command in order.

## 1. Bring main up to date

```
git switch main
git pull
```

## 2. Look at the two images

They show the same area of NBH 012 (IMC), 613 by 608 m. The first is the new IMC file built from CAD only, the second the IMC key. Both have their prototype links loaded.

```
Invoke-Item "C:\Users\bader\Documents\SiteAdaptation\round_16\images\testhost_I16_plan - Floor Plan - testhost I16 area.png"
Invoke-Item "C:\Users\bader\Documents\SiteAdaptation\round_16\images\testhost_IMK_plan - Floor Plan - testhost IMK area.png"
```

Or open the folder C:\Users\bader\Documents\SiteAdaptation\round_16\images in VS Code (File, Open Folder) and click each PNG. The villas and the duplex plots in no cluster are empty in the new file on purpose (questions 3 and 46).

## 3. Optional, open the new IMC file in Revit 2024

Work on a copy so the round file stays as it was, and only when the other session runs no window.

```
Test-Path C:\Users\bader\Documents\SiteAdaptation\window.lock
Copy-Item "C:\Users\bader\Documents\SiteAdaptation\round_16\work\01101-PSN-IMC-TEST-NEW.rvt" "C:\Users\bader\Documents\SiteAdaptation\round_16\work\01101-PSN-IMC-LOOK.rvt"
& "C:\Program Files\Autodesk\Revit 2024\Revit.exe" "C:\Users\bader\Documents\SiteAdaptation\round_16\work\01101-PSN-IMC-LOOK.rvt"
```

The first command must print False.

## 4. Optional, run the round checks again

```
.venv\Scripts\python python\corner_front.py --key "imc=C:\Users\bader\Documents\SiteAdaptation\plan_imc_past.json" --key "key14=C:\Users\bader\Documents\SiteAdaptation\round_z4\plan_z4_past.json" --labels C:\Users\bader\Documents\SiteAdaptation\round_16\snapshot\location_plan\labels.csv --baseplan C:\Users\bader\Documents\SiteAdaptation\round_16\snapshot\cad\baseplan\plots.json --roads C:\Users\bader\Documents\SiteAdaptation\round_16\snapshot\cad\roads --analysis C:\Users\bader\Documents\SiteAdaptation\round_16\snapshot\cad\analysis\analysis.json --sales "C:\Users\bader\Documents\SiteAdaptation\round_16\snapshot\rev9\PSN-ZZZ-XXX-SCH-TM-000201- DLUS Schedule_(Rev9)_Accomodation_Schedule.xlsx" --out C:\Users\bader\Documents\SiteAdaptation\round_16\corner_front.json
```

## 5. Answer the questions

The new questions of Your turn in steps/PROGRESS.md are at the end of the list, each with the recommended answer first.

## 6. When you no longer need the round files

Delete the look copy of step 3 and, once the last question is answered, the folders round_z4\work and round_16\work under C:\Users\bader\Documents\SiteAdaptation.

```
Remove-Item "C:\Users\bader\Documents\SiteAdaptation\round_16\work\01101-PSN-IMC-LOOK.rvt"
```
