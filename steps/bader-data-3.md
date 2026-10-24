# Round DATA-3, steps for Bader

Open the folder C:\Users\bader\Documents\GitHub\site-adaptation-tool in VS Code, then a PowerShell terminal (Terminal, New Terminal). Run each command in order.

## 1. Bring main up to date

```
git switch main
git pull
```

## 2. Optional, run the reports of the round again

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

## 3. Answer the questions

The questions of this round are carried by fixed ID in Bader's turn of steps/PROGRESS.md, and steps/questions-closed.md maps the DATA-3 numbers to the IDs. Round DATA-4 answered the dialog rule and the VL6 hand, so the Revit look by hand is no longer needed.
