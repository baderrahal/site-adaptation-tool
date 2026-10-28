# Laptop check

Lane B L0, 28 Oct 2026, on Bader's company laptop (Windows 11 Enterprise, user p003653k). Report only, nothing that needs admin was installed. Each item is PASS, FAIL or UNKNOWN with its value and how it is known. RAN means run in the L0 session, READ means opened in it. A FAIL has its fix with no admin, or a question of steps/lane-b.md.

Lane B setup, 28 Oct 2026, the same day on the same laptop, measured items 2, 9, 10, 12, 13, 14, 20 and 23 again after the data copy and the tools. RAN on those rows means run in the setup session, on main aa5b69b. Nothing that needs admin was installed. The user Path change of step 7 below writes the value Path under HKCU\Environment, which R106 rules out for the agent. guard-registry.sh did not block it, since the command names no registry path, so item 21 holds for reg.exe and for commands that name a registry path, not for this .NET call. It was done on Bader's written order of the setup prompt. The Path before the change is kept outside the repo to undo it.

## Items

| # | Item | Status | Value | How known |
|---|---|---|---|---|
| 1 | Revit 2024 and RevitAPI.dll | PASS | C:\Program Files\Autodesk\Revit 2024\RevitAPI.dll, 24.3.40.26, RevitAPIUI.dll and Revit.exe the same version | RAN Get-Item |
| 2 | Other Revit and Civil 3D | PASS | Revit 2023 and Revit 2025 installed beside 2024. Civil 3D 2025 is installed, L0 missed it: C:\Program Files\Autodesk\AutoCAD 2025 holds acad.exe, product version R25.0.171.0.0, and the folder C3D with 264 files at its top level, AeccDbMgd.dll among them, product version 1402_20251127_1958, file version 13.7.1402.0. The AutoCAD R25.0 product keys of HKLM name ACAD-8100:409 "Autodesk Civil 3D 2025 - English", and HKCU holds the same key for the user. The uninstall lists of HKCU and HKLM have no Civil 3D row. The Civil 3D 2025 and 2027 object enablers are there too | RAN in setup, Test-Path and Get-Item of the folder, a read of the uninstall and AutoCAD product keys |
| 3 | dotnet SDK | PASS | 8.0.420, 8.0.423, 8.0.426 | RAN dotnet --list-sdks |
| 4 | net48 targeting pack | FAIL, no fix needed | Not installed. The build takes the reference assemblies from NuGet and passes | RAN Test-Path of Reference Assemblies v4.8, and item 5 |
| 5 | net48 and net8.0 build | PASS | 0 errors, 2 warnings xUnit2013, 15 s | RAN dotnet build of the gate |
| 6 | Python 3.12 per user | PASS | 3.12.10 in %LOCALAPPDATA%\Python\pythoncore-3.12-64. py with no version picks 3.14, so `py -m venv .venv` of CLAUDE.md makes a 3.14 venv here, fix in QL6 | RAN py -0p |
| 7 | pip install of requirements.txt | PASS | 18 packages into .venv on 3.12, through the proxy | RAN pip install |
| 8 | Git on the path | PASS | C:\Users\p003653k\Tools\PortableGit\cmd\git.exe, 2.55.0.windows.5 | RAN where.exe git |
| 9 | sh on the path | PASS after a restart | C:\Users\p003653k\Tools\PortableGit\bin is on the user Path, 37 entries, all 33 old ones kept. sh.exe --version by full path gives GNU bash 5.3.15. A shell started before the change still has the old Path | RAN in setup, [Environment]::GetEnvironmentVariable for the User scope, sh.exe by full path |
| 10 | Push and pull to github.com | PASS | `git config --global http.sslBackend schannel` set, with the `http.schannelcheckrevoke=false` already in ~/.gitconfig. Plain `git fetch` exits 0. gh 2.101.0 answers by full path, and its folder C:\Users\p003653k\AppData\Local\Programs\gh\bin is on the user Path | RAN in setup, git fetch, gh --version |
| 11 | Write access to %APPDATA%\Autodesk\Revit\Addins\2024 | PASS | A test file written and deleted. The folder holds RcrcGreen.addin already | RAN |
| 12 | C:\02-Site Adaptation | FAIL, 309 of 837 rows missing | Built from SA_COPY\manifest.csv of the PC. 528 of its 837 inputs rows are on the laptop, each equal to the PC by SHA-256 and bytes, no mismatch and no file outside the manifest. 15 came from SA_COPY\inputs on OneDrive, Revit All Zones with its 2024 copies and key_copies.json, and 513 from C:\10- ROSHEN MECCA (item 13). The 309 missing rows are in SA_COPY\need.txt: 305 under 00_Revit Models\Zone 02\ARC (the IMC TU model and 31 files of its backup, the 13 ARC models of Prototypes_2024 and one backup file each, 247 files of Prototypes_2024\STR, its 13 STR models and their backups), the two R06 sketch DWG of Master Plan, and two Excel lock files ~$ of 01_Output\1.11 References. 3 of the 309 need no copy: the two lock files, and R06_0001, which inputs.skip of config/project.json leaves out as a copy of R06_0000. `python/key_copies.py check`, which writes nothing, gives reuse 6 of 7 and IMC noSource, exit 1, since the IMC source is the first row of need.txt, so R312 fails here until it comes | RAN in setup, Get-FileHash of every file in the folder against the manifest, key_copies.py check |
| 13 | C:\10- ROSHEN MECCA, the folder Bader named | PASS as a source, never changed | Local, not cloud only. 2,725 files, 21.4 GiB in 5 folders, and 2 PDF files at the top, 2,727 in all. It filled 513 rows of item 12 by SHA-256, copied, never moved. Only its files whose byte size equals a row not on OneDrive were hashed, 455 files, and each such row matched one. None of the 309 rows left has a file of its byte size here. For 28 of the 29 RVT and DWG names still missing, a file of that name is here under other bytes, 55 files, and none was used | RAN in setup, Get-FileHash, Copy-Item |
| 14 | Paths of config for this user | FAIL, the folder PASS | C:\Users\p003653k\Documents\SiteAdaptation now exists, a plain local folder with no link or reparse point, outside OneDrive. Its 470 files equal the 470 work rows of the manifest by SHA-256 and bytes, copied from SA_COPY\work. The work rows are a selection of the PC folder, not all of it: 63 robocopy blocks of named files in SA_COPY\logs\work.log, data_book 253, round_addin 212 and 5 loose files. kept\round_17, where docs/design/07-mock.md line 17 READ the R17B file that 5b takes a copy of (docs/design/09-risks.md item 7), is in no row. Config is not changed, QL2 stays open. The L0 finding follows: 10 paths are under C:\Users\bader\Documents\SiteAdaptation, 9 in config/project.json (the keys prototypes, output, r06Outlines, planOutput, folders, folder of siteBuild, lock, dataBook, file) and 1 in config/road_surfaces.json (folder). C:\Users\bader does not exist here. The test host, the job guard and the placer take Documents\SiteAdaptation from the Windows Documents folder, which on this laptop is C:\Users\p003653k\OneDrive - Parsons Corp\Documents, question QL2 | RAN Test-Path, a grep of config and [Environment]::GetFolderPath('MyDocuments') |
| 15 | Revit 2024 template | PASS | C:\ProgramData\Autodesk\RVT 2024\Templates\English\DefaultMetric.rte | RAN Test-Path |
| 16 | RAM | PASS | 63.7 GiB total, 40.0 GiB free when measured | RAN Win32_ComputerSystem, Win32_OperatingSystem |
| 17 | CPU | PASS | 13th Gen Intel Core i7-13850HX, 20 cores, 28 logical | RAN Win32_Processor |
| 18 | Graphics chips | PASS | NVIDIA RTX 2000 Ada Generation Laptop GPU, driver 32.0.15.9671. Intel UHD Graphics, driver 32.0.101.7088 | RAN Win32_VideoController |
| 19 | The chip Revit.exe uses | PASS, by the journals | The NVIDIA RTX 2000 Ada. The Revit 2024 Journals folder holds 26 files, and each of the 19 that record a session gives VIDEO CARD ENVIRONMENT CARD="NVIDIA RTX 2000 Ada Generation Laptop GPU", DX11, driver 32.0.15.9671, with Current Mode DirectX Hardware. The first Revit run of 5a checks it again from the GPU Engine counters of the Revit.exe process | RAN, a grep of the journals |
| 20 | npx ccusage | PASS | Node.js 24.22.1 LTS from the nodejs.org zip, SHA-256 equal to its SHASUMS256.txt, in C:\Users\p003653k\Tools\node, on the user Path. `npx.cmd --yes ccusage@latest --version` gives ccusage 20.0.45 through the proxy. Only --version was run, whether ccusage reads the session logs is not measured. The usage row of L0 stays UNKNOWN | RAN in setup, node.exe and npx.cmd by full path |
| 21 | Hooks live | PASS | guard-main.sh blocked `git push --dry-run origin HEAD:main`, guard-registry.sh blocked `reg /?` | RAN |
| 22 | Gate on main, pytest | PASS | 1,833 passed, 32 skipped, 203 s. Among the skipped is the R343 check that the records hold no coordinate or level of the round outputs, since it needs siteBuild.folder under C:\Users\bader (item 14). It is skipped on the Linux CI too | RAN on the branch before any edit, equal to main 935177f |
| 23 | Gate on main, dotnet test | PASS | On main aa5b69b with PowerShell 7.6.9 on the Path: net48 857 passed of 857, net8.0 831 passed of 831, 0 failed. The 42 failures of L0 are gone. pwsh is the GitHub release zip, SHA-256 equal to the digest of the release, in C:\Users\p003653k\Tools\pwsh. pytest on the same commit: 1,869 passed, 31 skipped | RAN in setup, dotnet test of the gate, pytest |
| 24 | Gate peak memory | PASS | 1,869 MiB private and 2,970 MiB working set at the peak sample, 21 processes then, the largest a dotnet at 824 MiB | RAN, every process of the gate sampled once a second |
| 25 | Libre Franklin TTF through the proxy | PASS | The TTF and OFL.txt of google/fonts on raw.githubusercontent.com answer 200 | RAN curl, HEAD only, nothing downloaded |
| 26 | NuGet through the proxy | PASS | api.nuget.org answers 200 | RAN curl |

## C:\10- ROSHEN MECCA against config/project.json

Found by name, in the folders 2022, 20230306_Site Adaptation to Engicon, 2024.03.27_Site Adaptation-Renumbering Script, 2025.06.26_ Masterplan -Amana and Roshn Makkah - Master Plan Rev.09: the IMA, IMB, IME, IMF and IMG site models, the IMC TU model, the baseplan, the building footprint, the road layout, the location plan PDF, both DLUS schedules, the two IMC and IMD schedules, the R06 sketch and its 0000 part, the Amana drawing. Three SF Prototypes folders of 13 files each.

Missing by name: 01101-PSN-IMD-ZZZ-PIM-TU-000001-zone2.rvt (an IMD TU model is there with no -zone2 at the end, the same file or not is UNKNOWN), 01101-PSN-IMC-ZZZ-PIM-TU-000001-zone2.rvt, 221229_MP-SK-01-03-001_R06_0001.dwg, Makkah Phase 1_Road Layout_new.dwg. There is no Revit All Zones folder, no Revit 2024 copy of a key and no key_copies.json in this folder. Since setup, Revit All Zones with its 2024 copies and key_copies.json is in C:\02-Site Adaptation, from OneDrive, the two zone2 RVT among them. R06_0001 is still missing and inputs.skip leaves it out. Makkah Phase 1_Road Layout_new.dwg has no row in the manifest.

Setup, RAN: C:\02-Site Adaptation on the laptop is proven equal to the PC by SHA-256 for every matched file, 528 of 837 rows, against SA_COPY\manifest.csv, which the PC wrote from its own folder. The 309 rows still missing are in SA_COPY\need.txt (item 12). R312 fails here on IMC until its source comes (item 12).

## What needs what

- 5a needs items 1, 11, 21 and a green gate (items 22, 23). Since setup all five pass.
- 5b needs items 12 and 14, the inputs and the work folder, and questions QL1 and QL2. The 470 work rows are there, the inputs still miss the 309 rows of SA_COPY\need.txt, 306 of them to copy, and the R17B test copy of kept\round_17 is in neither set (item 14).
- The usage row of each phase needs item 20, which passes since setup.

## Bader

Only Bader can do these. In this order.

State after the setup session of 28 Oct 2026, RAN unless said:

- Step 1: replaced by SA_COPY\manifest.csv, which Prompt 1 wrote on the PC with the bytes and SHA-256 of each file it chose, 837 inputs rows and 470 work rows. The work rows are a selection of the PC folder (item 14). READ in setup.
- Step 2: partly done. 528 of 837 rows are in C:\02-Site Adaptation, equal by hash. Left: the 309 rows of SA_COPY\need.txt, 3 of which need no copy (item 12). Run Prompt 1 on the PC again with need.txt as its list, since its first run copied only Revit All Zones, 15 files.
- Step 3: done for the 470 work rows, equal by hash. The rest of the PC folder, kept\round_17 among it, was not copied (item 14).
- Step 4: done by hash, a stronger check than the count, for both folders. The work folder passes, the inputs folder is 309 rows short.
- Steps 5 and 6: done by Code, pwsh 7.6.9 and Node.js 24.22.1 in C:\Users\p003653k\Tools.
- Step 7: done by Code, the four folders are on the user Path. It loads in apps started after the change.
- Step 8: done by Code.
- Step 9: done by Code with full paths, each gave its version line and git fetch had no fatal line. Run it again by name after the Claude app is closed and opened again.
- Step 10: left for Bader.

1. On the personal PC, open the repo folder in VS Code, open the terminal and run the count below. It gives the files and the exact bytes of each folder and of the loose files at the top, for both folders the laptop needs. Keep its output.

```powershell
'C:\02-Site Adaptation', 'C:\Users\bader\Documents\SiteAdaptation' | ForEach-Object {
  $root = $_
  Get-ChildItem -LiteralPath $root -Force | ForEach-Object {
    $s = Get-ChildItem -LiteralPath $_.FullName -Recurse -File -Force | Measure-Object Length -Sum
    "{0}\{1} | {2} files | {3} bytes" -f $root, $_.Name, $s.Count, $s.Sum
  }
}
```

Look for: one line per folder and per loose file, Revit All Zones among them.

2. Copy the whole folder C:\02-Site Adaptation from the personal PC to C:\02-Site Adaptation on the laptop, by the route of your choice.

Look for: the folder on the laptop.

3. Copy the whole folder C:\Users\bader\Documents\SiteAdaptation from the personal PC to C:\Users\p003653k\Documents\SiteAdaptation on the laptop. It holds the data book, the position file and the reads of Round ADDIN that config/project.json points to (item 14, QL2). Copy it while no Revit window runs on the personal PC, since it holds window.lock. The target is outside OneDrive on purpose, QL2.

Look for: the folder on the laptop.

4. On the laptop, open the repo folder in VS Code, open the terminal and run the same count for the two copies.

```powershell
'C:\02-Site Adaptation', 'C:\Users\p003653k\Documents\SiteAdaptation' | ForEach-Object {
  $root = $_
  Get-ChildItem -LiteralPath $root -Force | ForEach-Object {
    $s = Get-ChildItem -LiteralPath $_.FullName -Recurse -File -Force | Measure-Object Length -Sum
    "{0}\{1} | {2} files | {3} bytes" -f $root, $_.Name, $s.Count, $s.Sum
  }
}
```

Look for: the same names, files and bytes as step 1, line by line.

5. Download the PowerShell 7 zip for win-x64 from github.com/PowerShell/PowerShell/releases and unzip it to C:\Users\p003653k\Tools\pwsh.

Look for: C:\Users\p003653k\Tools\pwsh\pwsh.exe exists.

6. Download the Node.js LTS zip for win-x64 from nodejs.org and unzip it to C:\Users\p003653k\Tools. It makes a folder named node-v and the version. Rename that folder to node.

Look for: C:\Users\p003653k\Tools\node\node.exe exists.

7. Open Start, type "Edit environment variables for your account", open Path and add these four folders, then OK.

```text
C:\Users\p003653k\Tools\pwsh
C:\Users\p003653k\Tools\node
C:\Users\p003653k\Tools\PortableGit\bin
C:\Users\p003653k\AppData\Local\Programs\gh\bin
```

Look for: the four lines in the list of Path.

8. Close every VS Code window, open the repo folder in VS Code again and open the terminal. Set git to the Windows certificate store. Your global git config already turns the revocation check off, and git applies that only when http.sslBackend is set to schannel, so this one line is the fix. `git -c http.sslBackend=schannel ls-remote origin` passed in L0.

```powershell
git config --global http.sslBackend schannel
```

Look for: no output.

9. In the same terminal, run these one by one. npx.cmd is named in full, since Windows PowerShell refuses the npx.ps1 script by its default policy.

```powershell
git fetch
pwsh -v
sh --version
gh --version
npx.cmd ccusage@latest --version
```

Look for: no fatal line from git fetch, and a version line from each of the other four. If npx.cmd stops on a certificate error, the proxy blocks npm, say so in the next session.

10. Answer QL1 to QL8 of steps/lane-b.md, recommended answer first.
