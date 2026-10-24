# Steps for Bader, round 15a

This round adds plan.json, the plan builder, the placer add-in Place from plan and two fixes in the reader. Do it after the pull request of this round is merged, the top entry of steps/log.md says when. Each step is one action.

Steps 1 to 3 open the repo, steps 4 to 10 build and install (a), steps 11 and 12 make the copy (b), steps 13 and 14 remove the KEO links (c), steps 15 and 16 are the dry run (d), steps 17 and 18 the real run (e), steps 19 to 25 the reader on the copy (f), step 26 is your answer (g).

## Open the repo

1. Open GitHub Desktop, choose the repository site-adaptation-tool, make sure the branch is main, click Fetch origin.
   Look for: the button now reads Pull origin.

2. Click Pull origin.
   Look for: the history list shows the merge of pull request 15 near the top and the button reads Fetch origin again.

3. Open VS Code, choose File, Open Folder, pick C:\Users\bader\Documents\GitHub\site-adaptation-tool, then choose Terminal, New Terminal.
   Look for: the terminal prompt ends with site-adaptation-tool.

## a. Build and install the placer and the fixed reader

4. In the terminal run:
   `dotnet build dotnet\SiteAdaptation.sln -c Release`
   Look for: Build succeeded and 0 Error(s).

5. Close Revit completely. Then copy the fixed reader, the same command as in round 9:
   `Copy-Item -Path "dotnet\RevitPrototypeReader\bin\Release\net48\*" -Destination "C:\Users\bader\AppData\Roaming\Autodesk\Revit\Addins\2024\SiteAdaptation\" -Exclude "SiteAdaptation.addin","SiteAdaptation_Revit2024.addin" -Recurse -Force`
   Look for: this command shows today's date and time:
   `(Get-Item "C:\Users\bader\AppData\Roaming\Autodesk\Revit\Addins\2024\SiteAdaptation\RevitPrototypeReader.dll").LastWriteTime`

6. Make the folder of the placer:
   `New-Item -ItemType Directory -Force "C:\Users\bader\AppData\Roaming\Autodesk\Revit\Addins\2024\SiteAdaptationPlacer"`
   Look for: a line with SiteAdaptationPlacer under Name.

7. Copy the placer into it:
   `Copy-Item -Path "dotnet\SiteAdaptation.Placer\bin\Release\net48\*" -Destination "C:\Users\bader\AppData\Roaming\Autodesk\Revit\Addins\2024\SiteAdaptationPlacer\" -Exclude "*.addin" -Recurse -Force`
   Look for: this command shows today's date and time:
   `(Get-Item "C:\Users\bader\AppData\Roaming\Autodesk\Revit\Addins\2024\SiteAdaptationPlacer\SiteAdaptation.Placer.dll").LastWriteTime`

8. Check that both add-ins carry the same SiteAdaptation.Common.dll, because Revit loads only one of them:
   `(Get-FileHash "C:\Users\bader\AppData\Roaming\Autodesk\Revit\Addins\2024\SiteAdaptation\SiteAdaptation.Common.dll").Hash -eq (Get-FileHash "C:\Users\bader\AppData\Roaming\Autodesk\Revit\Addins\2024\SiteAdaptationPlacer\SiteAdaptation.Common.dll").Hash`
   Look for: True.

9. Copy the manifest of the placer next to the one of the reader. Do not touch SiteAdaptation_Revit2024.addin:
   `Copy-Item "dotnet\SiteAdaptation.Placer\SiteAdaptationPlacer_Revit2024.addin" "C:\Users\bader\AppData\Roaming\Autodesk\Revit\Addins\2024\"`
   Look for: this command prints True:
   `Test-Path "C:\Users\bader\AppData\Roaming\Autodesk\Revit\Addins\2024\SiteAdaptationPlacer_Revit2024.addin"`

10. Build the plan from the past job. It reads prototypes_imc_past.json, the copy of the round 9 reader output that I made, and the IMC schedule. Close the schedule in Excel first:
    `.venv\Scripts\python python\plan_from_past.py`
    Look for: plots written 337 of 337, missing a value 337, then position proof on 94 links, largest difference 0 mm, then C:\Users\bader\Documents\SiteAdaptation\plan_imc_past.json.

## b. Make a copy of the IMC model

11. Open Revit 2024. Choose File, Open, Project, go to C:\02-Site Adaptation\00_Revit Models\Zone 02\ARC, click 01101-PSN-IMC-ZZZ-PIM-TU-000001.rvt once, tick Detach from Central, click Open, then choose Detach and preserve worksets.
    Look for: the model opens and the title bar says Detached. Nothing is written to the original.

12. Choose File, Save As, Project, keep the folder C:\02-Site Adaptation\00_Revit Models\Zone 02\ARC, type the name 01101-PSN-IMC-ZZZ-PIM-TU-000001_placer-test.rvt, click Save.
    Look for: the title bar shows 01101-PSN-IMC-ZZZ-PIM-TU-000001_placer-test. Every step below runs on this copy. Never run the placer on the original.

## c. Remove the 13 KEO prototype links in the copy

13. Choose Manage, Manage Links, the Revit tab. Click the first row whose name starts with RRE-WP-NSP-KEO-ARC, hold Ctrl and click the other KEO rows until 13 rows are selected, click Remove, answer Yes.
    Look for: only 01101-PSN-ZZZ-XXX-LAN-ZZ-001000.rvt is left on the Revit tab.

14. Click OK and open the site plan view.
    Look for: no villa is left on the plots, the floors, ramps and spot elevations are still there. The survey point is not touched by this step.

## d. Dry run

15. Choose Add-Ins, External Tools, Place from plan. Pick C:\Users\bader\Documents\SiteAdaptation\plan_imc_past.json, then choose Dry run.
    Look for: Dry run, nothing was changed. Plots in plan 337, would place 337, skipped 0, parameters written 0, issues 0.

16. Open C:\Users\bader\Documents\SiteAdaptation\placer_report.json in VS Code.
    Look for: "stopped": false, and under coordinates "match": true. If it says stopped, copy the failure line to chat and stop here.

## e. Real run

17. Choose Add-Ins, External Tools, Place from plan again, pick the same plan, then choose Place for real. It loads 13 links and places 337, so it can take some minutes.
    Look for: Real run. Plots in plan 337, placed 337, skipped 0, parameters written 3033, issues 0. If it says Rolled back, copy that line to chat and stop here.

18. Press Ctrl+S to save the copy.
    Look for: the save ends with no error box.

## f. Read the copy with all links loaded, in the same Revit session

19. Choose Manage, Manage Links, the Revit tab, click the first row, hold Shift and click the last row, click Reload, wait until it stops, click OK.
    Look for: the Status column reads Loaded on all 14 rows.

20. Choose Add-Ins, External Tools, Read Prototypes, then The open document.
    Look for: a final box that lists 338 link(s) and a File size line under 20 MB. Write the number down.

21. Compare the plan with the placed copy:
    `.venv\Scripts\python python\plan_compare.py "C:\Users\bader\Documents\SiteAdaptation\plan_imc_past.json" "C:\Users\bader\Documents\SiteAdaptation\prototypes.json"`
    Look for: plots 337, full match 337 and every count under mismatches 0. The same command on the round 9 file gave 337 of 337, so any mismatch comes from the placement.

22. Count where the link boxes came from:
    `.venv\Scripts\python -c "print(__import__('collections').Counter(l.get('boxSource') for l in __import__('json').load(open(r'C:\Users\bader\Documents\SiteAdaptation\prototypes.json', encoding='utf-8'))['documents'][0]['links']))"`
    Look for: a Counter with linkExtents or api and no None, the counts add up to 338.

23. Count where the spot hosts came from:
    `.venv\Scripts\python -c "print(__import__('collections').Counter(s.get('hostSource') for s in __import__('json').load(open(r'C:\Users\bader\Documents\SiteAdaptation\prototypes.json', encoding='utf-8'))['documents'][0]['spotElevations']))"`
    Look for: a Counter with dependents or references and no None, the counts add up to 987.

24. Print the error lines of the run:
    `.venv\Scripts\python -c "print('\n'.join(__import__('json').load(open(r'C:\Users\bader\Documents\SiteAdaptation\prototypes.json', encoding='utf-8'))['documents'][0]['errors']))"`
    Look for: no line that starts with spotElevations host and no line with bounding box. The line about the yard floors with no Plot parameter is expected.

25. Count the error lines on the links:
    `.venv\Scripts\python -c "print(__import__('collections').Counter(e for l in __import__('json').load(open(r'C:\Users\bader\Documents\SiteAdaptation\prototypes.json', encoding='utf-8'))['documents'][0]['links'] for e in l.get('errors', [])))"`
    Look for: no line with bounding box and no Link is not loaded. Parameter RRE_ZZ_SP_Prototype-FFL is missing with 338 is expected, 337 because the placer never writes that parameter and 1 for the landscape link. The four other Parameter lines with 1 each are the landscape link too. The round 9 file gave Link is not loaded 244 and bounding box: the API gives none 94.

## g. Your answer

26. Tell chat which Look for line did not match, and paste what you saw instead. Paste the lines of steps 21 to 25 as they are.
