# Part 1 check, short form

For Bader at 16:00, before the dry run at 17:00 on NBH 012. One action per step.

## 1. Close Revit

1. Close every Revit 2024 window. Save or discard as it asks, nothing of the dry run is open yet.

## 2. Open the repo in VS Code

2. Open VS Code.
3. File, Open Folder, pick C:\Users\<user>\OneDrive - Parsons Corp\Documents\GitHub\site-adaptation-tool, then Select Folder.
4. Terminal, New Terminal. A PowerShell terminal opens at the bottom, in that folder.

## 3. Run each line on its own

Wait for the prompt to come back after each line.

```
git switch main
```

Prints: Switched to branch 'main'.

```
git pull
```

Prints: a list of changed files, or Already up to date.

```
dotnet build dotnet\SiteAdaptation.sln -c Release
```

Prints: Build succeeded and 0 Error(s), after a minute or two.

```
C:\Users\<user>\Tools\pwsh\pwsh.exe -File dotnet\SiteAdaptation.AddIn\Install-SiteAdaptation.ps1 -NoBuild
```

Prints: no red line, and ends with two lines, Manifest ... SiteAdaptationAddIn_Revit2024.addin names ..., then one SiteAdaptation.Common.dll in 5 folders of the tool.

## If a line does not print that

| It says | Do this |
|---|---|
| Revit 2024 is running. Close it before the install. | Close every Revit 2024 window, also check Task Manager for Revit.exe, then run the install line again. Nothing was installed. |
| The Common DLLs differ, close the program that holds it and run the install again | Close the program it names, or Revit, then run the install line again. |
| git switch main stops on local changes | Stop and tell Lane C. Do not discard them, they are a lane's work. |
| The build shows an error | Stop and tell Lane C, with the last lines. |

Revit 2023 and 2025 may stay open, they load no add-in of this folder.

## 4. The dry run

The click order is in steps/dryrun-3nov.md, Lane B writes it by 15:45. The kit it uses is in steps/dryrun-3nov-kit.md.

When Revit asks about an unsigned add-in on first start, press Always Load.

## The long check

The long Part 1 check, page by page with the known faults, stays for later: steps/part1-check-long.md.
