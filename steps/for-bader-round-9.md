# Steps for Bader, round 9

Run these in order after the round 9 pull request is merged. Each step is one action. Steps 1 to 3 are the repo, steps 4 and 5 install the add-in, steps 6 to 9 run Revit, steps 10 and 11 run the report.

1. Open GitHub Desktop, choose the repository site-adaptation-tool, make sure the branch is main, click Fetch origin.
   Look for: the button now reads Pull origin.

2. Click Pull origin.
   Look for: the history list shows the merge of the round 9 pull request at the top and the button reads Fetch origin again.

3. Open VS Code, choose File, Open Folder, pick C:\Users\bader\Documents\GitHub\site-adaptation-tool, then choose Terminal, New Terminal.
   Look for: the terminal prompt ends with site-adaptation-tool.

4. In the terminal run:
   `dotnet build dotnet\SiteAdaptation.sln -c Release`
   Look for: Build succeeded and 0 Error(s).

5. Close Revit completely. Then in the terminal run:
   `Copy-Item -Path "dotnet\RevitPrototypeReader\bin\Release\net48\*" -Destination "C:\Users\bader\AppData\Roaming\Autodesk\Revit\Addins\2024\SiteAdaptation\" -Exclude "SiteAdaptation.addin","SiteAdaptation_Revit2024.addin" -Recurse -Force`
   The command leaves out both manifest files on purpose. Do not touch C:\Users\bader\AppData\Roaming\Autodesk\Revit\Addins\2024\SiteAdaptation_Revit2024.addin, its Assembly line has the real path.
   Look for: this command shows today's date and time:
   `(Get-Item "C:\Users\bader\AppData\Roaming\Autodesk\Revit\Addins\2024\SiteAdaptation\RevitPrototypeReader.dll").LastWriteTime`

6. Open Revit 2024 and open the IMC site model.
   Look for: the model opens and the Add-Ins tab shows External Tools.

7. In Revit choose Manage, Manage Links, click the first Revit link, hold Shift and click the last one so that all are selected, click Reload, wait until it stops.
   Look for: the Status column reads Loaded on every row. Tell me how many rows do not.

8. Choose Add-Ins, External Tools, Read Prototypes, then choose The open document.
   Look for: a final box that lists floors, links and spot elevations, then a line File size with a number of MB and the words the target is under 20 MB. Write the number down.

9. Close the box. Open the folder C:\Users\bader\Documents\SiteAdaptation in File Explorer.
   Look for: prototypes.json with today's date and a size in MB that matches the box.

10. In the VS Code terminal run:
    `.venv\Scripts\python python\plot_relations.py "C:\Users\bader\Documents\SiteAdaptation\prototypes.json" --excel "C:\02-Site Adaptation\01_Output\1.11 References\00101-PSN-IMC-XXX-SCH-TU-090000.xlsx"`
    Look for: three lines, the number of plots and two file paths. If it says permission denied on the workbook, close the workbook in Excel and run it again.

11. Open the folder C:\Users\bader\Documents\SiteAdaptation in File Explorer.
    Look for: plot_relations.csv and plot_relations_summary.md with today's date.

After step 11 give me the File size line from step 8 and the file plot_relations_summary.md.
