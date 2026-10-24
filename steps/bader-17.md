# Round 17, steps for Bader

Open the folder C:\Users\bader\Documents\GitHub\site-adaptation-tool in VS Code (File, Open Folder), then a PowerShell terminal (Terminal, New Terminal). Run each command in order.

## 1. Bring main up to date

```
git switch main
git pull
```

## 2. Look at the four images

Each pair shows the same area. The first pair is NBH 012: the new IMC file built from CAD with the coded rules and the overrides, then the Parsons IMC key. The second pair is NBH 03: the new file built by the native2022 profile, then the 1.2 Native model (floors only, its old prototype files are not on this machine).

```
Invoke-Item "C:\Users\bader\Documents\SiteAdaptation\round_17\images\testhost_I18_plan - Floor Plan - testhost I18 area.png"
Invoke-Item "C:\Users\bader\Documents\SiteAdaptation\round_17\images\testhost_IMK_plan - Floor Plan - testhost IMK area.png"
Invoke-Item "C:\Users\bader\Documents\SiteAdaptation\round_17\images\testhost_I19_plan - Floor Plan - testhost I19 area.png"
Invoke-Item "C:\Users\bader\Documents\SiteAdaptation\round_17\images\testhost_U12_plan - Floor Plan - testhost U12 area.png"
```

Or open the folder C:\Users\bader\Documents\SiteAdaptation\round_17\images in VS Code and click each PNG.

## 3. Open and edit the override list

The list is config/overrides/roshn-makkah.json, one override per line. In VS Code press Ctrl+P, type overrides, and open it. Ctrl+F finds a plot, for example `"plot": "2291"`.

Each line holds the plot (the official number of the location plan), the field, the value the past job used, the source model and the reason. A level holds its gap to the coded rule in metres, never a real level. A build applies only the lines whose source is the model it follows, for NBH 012 the Parsons key 01101-PSN-IMC-ZZZ-PIM-TU-000001.rvt.

- To drop an override, delete its line and the comma before it if it was the last line.
- To change one, edit its value: true or false for mirrored, 0.5, 2 or flat for rampType, the gap in metres for a level.
- Run the check below after an edit, it must print ok.

```
.venv\Scripts\python -c "import json; json.load(open('config/overrides/roshn-makkah.json', encoding='utf-8')); print('ok')"
```

A new run of python/majority.py writes the list again from the past models, so keep your edits in a commit of their own and tell Chat which lines you changed.

## 4. Optional, open the new IMC file in Revit 2024

Work on a copy, and only when no other session runs a window. The first command must print False.

```
Test-Path C:\Users\bader\Documents\SiteAdaptation\window.lock
Copy-Item "C:\Users\bader\Documents\SiteAdaptation\round_17\work\01101-PSN-IMC-R17B-TEST-NEW.rvt" "C:\Users\bader\Documents\SiteAdaptation\round_17\work\01101-PSN-IMC-R17-LOOK.rvt"
& "C:\Program Files\Autodesk\Revit 2024\Revit.exe" "C:\Users\bader\Documents\SiteAdaptation\round_17\work\01101-PSN-IMC-R17-LOOK.rvt"
```

## 5. Optional, run the majority rule again

It reads past_all.csv of the round folder and writes the report outside the repo and the override list in config/overrides. It takes under a minute.

```
cd python
..\.venv\Scripts\python majority.py C:\Users\bader\Documents\SiteAdaptation\round_17\past_all.csv --plots-csv C:\Users\bader\Documents\SiteAdaptation\round_17\snapshot\plots.csv --baseplan C:\Users\bader\Documents\SiteAdaptation\round_17\snapshot\cad\baseplan\plots.json --roads C:\Users\bader\Documents\SiteAdaptation\round_17\snapshot\cad\roads --analysis C:\Users\bader\Documents\SiteAdaptation\round_17\snapshot\cad\analysis\analysis.json --footprint-blocks C:\Users\bader\Documents\SiteAdaptation\round_17\snapshot\cad\footprint\blocks.json --sales "C:\Users\bader\Documents\SiteAdaptation\round_17\snapshot\rev9\PSN-ZZZ-XXX-SCH-TM-000201- DLUS Schedule_(Rev9)_Accomodation_Schedule.xlsx" --model u11 01101-PSN-IMA-ZZZ-PIM-TU-000001.rvt --model u12 01101-PSN-IMB-ZZZ-PIM-TU-000002.rvt --model u13 01101-PSN-IMC-ZZZ-PIM-TU-000003.rvt --model u14 01101-PSN-IMD-ZZZ-PIM-TU-000004.rvt --model imc 01101-PSN-IMC-ZZZ-PIM-TU-000001.rvt --out C:\Users\bader\Documents\SiteAdaptation\round_17\part_2\majority.json --overrides ..\config\overrides\roshn-makkah.json
cd ..
```

## 6. Answer the questions

Bader's turn in steps/PROGRESS.md lists the open questions by fixed ID, the level profile choice Q050 first. steps/questions-closed.md holds the closed ones and maps old numbers to IDs.

## 7. When you no longer need the round files

Delete the look copy of step 4. The round keeps the four 2024 key copies, the two new files, the Parsons IMC key copy and the images until Q035 and Q060 are answered.

```
Remove-Item "C:\Users\bader\Documents\SiteAdaptation\round_17\work\01101-PSN-IMC-R17-LOOK.rvt"
```
