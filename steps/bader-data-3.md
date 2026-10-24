# Round DATA-3, steps for Bader

Open the folder C:\Users\bader\Documents\GitHub\site-adaptation-tool in VS Code, then a PowerShell terminal (Terminal, New Terminal). Run each command in order.

## 1. Bring main up to date

```
git switch main
git pull
```

## 2. The VL6 hand in Revit 2024 (question 15)

The round could not open Revit, so this one is by hand. Use the copies in the data book, never the files of Prototypes_2024.

First check that no other window runs. Both lines must print False:

```
Test-Path C:\Users\bader\Documents\SiteAdaptation\window.lock
[bool](Get-Process Revit, acad, accoreconsole -ErrorAction SilentlyContinue)
```

Open the VL6 copy:

```
& "C:\Program Files\Autodesk\Revit 2024\Revit.exe" "C:\Users\bader\Documents\SiteAdaptation\data_book\work\00_Revit Models\Zone 02\ARC\Prototypes_2024\RRE-WP-NSP-KEO-ARC-VL6-ZZ-MOD-1300.rvt"
```

In Revit:

1. Open the ground floor plan.
2. Type VV, open the Site group of the Model Categories tab, tick Internal Origin and Project Base Point, press OK.
3. Look from the road side of the house. Write down whether the main entrance door and the driver's house sit to the left or to the right of the internal origin.
4. Close the file and answer No when Revit asks to save.

Do the same with the VL4 copy:

```
& "C:\Program Files\Autodesk\Revit 2024\Revit.exe" "C:\Users\bader\Documents\SiteAdaptation\data_book\work\00_Revit Models\Zone 02\ARC\Prototypes_2024\RRE-WP-NSP-KEO-ARC-VL4-ZZ-MOD-1400.rvt"
```

Then check that both copies are unchanged. Each line must print True:

```
(Get-FileHash "C:\Users\bader\Documents\SiteAdaptation\data_book\work\00_Revit Models\Zone 02\ARC\Prototypes_2024\RRE-WP-NSP-KEO-ARC-VL6-ZZ-MOD-1300.rvt").Hash -eq (Get-FileHash "C:\02-Site Adaptation\00_Revit Models\Zone 02\ARC\Prototypes_2024\RRE-WP-NSP-KEO-ARC-VL6-ZZ-MOD-1300.rvt").Hash
(Get-FileHash "C:\Users\bader\Documents\SiteAdaptation\data_book\work\00_Revit Models\Zone 02\ARC\Prototypes_2024\RRE-WP-NSP-KEO-ARC-VL4-ZZ-MOD-1400.rvt").Hash -eq (Get-FileHash "C:\02-Site Adaptation\00_Revit Models\Zone 02\ARC\Prototypes_2024\RRE-WP-NSP-KEO-ARC-VL4-ZZ-MOD-1400.rvt").Hash
```

If VL6 has the entrance and the driver's house on the other side from VL4, the answer to question 15 is yes.

## 3. Optional, run the reports of the round again

They write only into C:\Users\bader\Documents\SiteAdaptation\data_book. Each takes under 4 minutes.

```
cd python
..\.venv\Scripts\python rev9_workbooks.py
..\.venv\Scripts\python rev9_drawing.py
..\.venv\Scripts\python cross_sections.py
..\.venv\Scripts\python entry_terms.py
..\.venv\Scripts\python stray_units.py
..\.venv\Scripts\python location_dwg.py
..\.venv\Scripts\python data3_check.py
cd ..
```

The counts are in the data book folders rev9, cross_sections, entry_terms, stray_units, location_dwg and in logs\data3_check.json.

## 4. Answer the questions

Questions 1, 4, 9, 11, 12, 15, 29, 31, 32, 40 and 42 of Bader's turn in steps/PROGRESS.md are new or changed in this round, each with the recommended answer first. Question 40 decides whether the next round may answer the dialogs of its own windows, which the Rev 9 parcel read and the VL6 check need.
