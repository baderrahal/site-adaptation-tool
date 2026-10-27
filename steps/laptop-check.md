# Laptop check

Lane B L0, 28 Oct 2026, on Bader's company laptop (Windows 11 Enterprise, user p003653k). Report only, nothing that needs admin was installed. Each item is PASS, FAIL or UNKNOWN with its value and how it is known. RAN means run in the L0 session, READ means opened in it. A FAIL has its fix with no admin, or a question of steps/lane-b.md.

## Items

| # | Item | Status | Value | How known |
|---|---|---|---|---|
| 1 | Revit 2024 and RevitAPI.dll | PASS | C:\Program Files\Autodesk\Revit 2024\RevitAPI.dll, 24.3.40.26, RevitAPIUI.dll and Revit.exe the same version | RAN Get-Item |
| 2 | Other Revit and Civil 3D | PASS | Revit 2023 and Revit 2025 installed beside 2024, AutoCAD 2025. No Civil 3D, only the Civil 3D 2025 and 2027 object enablers | RAN, the folders of C:\Program Files\Autodesk |
| 3 | dotnet SDK | PASS | 8.0.420, 8.0.423, 8.0.426 | RAN dotnet --list-sdks |
| 4 | net48 targeting pack | FAIL, no fix needed | Not installed. The build takes the reference assemblies from NuGet and passes | RAN Test-Path of Reference Assemblies v4.8, and item 5 |
| 5 | net48 and net8.0 build | PASS | 0 errors, 2 warnings xUnit2013, 15 s | RAN dotnet build of the gate |
| 6 | Python 3.12 per user | PASS | 3.12.10 in %LOCALAPPDATA%\Python\pythoncore-3.12-64. py with no version picks 3.14, so `py -m venv .venv` of CLAUDE.md makes a 3.14 venv here, fix in QL6 | RAN py -0p |
| 7 | pip install of requirements.txt | PASS | 18 packages into .venv on 3.12, through the proxy | RAN pip install |
| 8 | Git on the path | PASS | C:\Users\p003653k\Tools\PortableGit\cmd\git.exe, 2.55.0.windows.5 | RAN where.exe git |
| 9 | sh on the path | FAIL | sh and bash not on the path. Both are in PortableGit\bin. The hooks still fire, Claude Code finds Git Bash itself | RAN where.exe, and the two hook probes of item 21 |
| 10 | Push and pull to github.com | FAIL with plain git, PASS with the fix | `git fetch` fails, schannel CRYPT_E_NO_REVOCATION_CHECK. It passes with `-c http.sslBackend=schannel -c http.schannelCheckRevoke=false`. gh 2.101.0 is logged in as baderrahal with push rights but is not on the path | RAN git fetch, git ls-remote, gh api |
| 11 | Write access to %APPDATA%\Autodesk\Revit\Addins\2024 | PASS | A test file written and deleted. The folder holds RcrcGreen.addin already | RAN |
| 12 | C:\02-Site Adaptation | FAIL | Not on this laptop. config/project.json names it as inputRoot | RAN Test-Path |
| 13 | C:\10- ROSHEN MECCA, the folder Bader named | FAIL as inputRoot | Local, not cloud only. 2,725 files, 21.4 GiB in 5 folders and 2 PDF files. 17 of the 21 file names config/project.json gives are there in another layout, 4 are missing, see below | RAN |
| 14 | Paths of config/project.json for this user | FAIL | Five paths are under C:\Users\bader\Documents\SiteAdaptation: the work folder, the window lock, the data book and two plan outputs. C:\Users\bader does not exist here, question QL2 | RAN Test-Path |
| 15 | Revit 2024 template | PASS | C:\ProgramData\Autodesk\RVT 2024\Templates\English\DefaultMetric.rte | RAN Test-Path |
| 16 | RAM | PASS | 63.7 GiB total, 40.0 GiB free when measured | RAN Win32_ComputerSystem, Win32_OperatingSystem |
| 17 | CPU | PASS | 13th Gen Intel Core i7-13850HX, 20 cores, 28 logical | RAN Win32_Processor |
| 18 | Graphics chips | PASS | NVIDIA RTX 2000 Ada Generation Laptop GPU, driver 32.0.15.9671. Intel UHD Graphics, driver 32.0.101.7088 | RAN Win32_VideoController |
| 19 | The chip Revit.exe uses | UNKNOWN | The 11 Revit 2024 journals say Current Mode DirectX Hardware and name no card. The per app choice is in the registry, which this tool never reads. Measured at the first Revit run of 5a from the GPU Engine counters of the Revit.exe process | RAN, the journals |
| 20 | npx ccusage | FAIL | Node and npx not installed, so the usage row of L0 is UNKNOWN | RAN Get-Command |
| 21 | Hooks live | PASS | guard-main.sh blocked `git push --dry-run origin HEAD:main`, guard-registry.sh blocked `reg /?` | RAN |
| 22 | Gate on main, pytest | PASS | 1,833 passed, 32 skipped, 203 s | RAN on the branch before any edit, equal to main 935177f |
| 23 | Gate on main, dotnet test | FAIL | net48 784 passed, 42 failed of 826. net8.0 758 passed, 42 failed of 800. Every failure is the same: PrototypeSetTests and ParsonsModelTests start pwsh, and PowerShell 7 is not installed | RAN dotnet test |
| 24 | Gate peak memory | PASS | 1,869 MiB private, 2,970 MiB working set, over 21 processes, the largest one dotnet at 824 MiB | RAN, every process of the gate sampled once a second |
| 25 | Libre Franklin TTF through the proxy | PASS | The TTF and OFL.txt of google/fonts on raw.githubusercontent.com answer 200 | RAN curl, HEAD only, nothing downloaded |
| 26 | NuGet through the proxy | PASS | api.nuget.org answers 200 | RAN curl |

## C:\10- ROSHEN MECCA against config/project.json

Found by name, in the folders 2022, 20230306_Site Adaptation to Engicon, 2024.03.27_Site Adaptation-Renumbering Script, 2025.06.26_ Masterplan -Amana and Roshn Makkah - Master Plan Rev.09: the IMA, IMB, IME, IMF and IMG site models, the IMC TU model, the baseplan, the building footprint, the road layout, the location plan PDF, both DLUS schedules, the two IMC and IMD schedules, the R06 sketch and its 0000 part, the Amana drawing. Three SF Prototypes folders of 13 files each.

Missing by name: 01101-PSN-IMD-ZZZ-PIM-TU-000001-zone2.rvt (an IMD TU model is there with no -zone2 at the end, the same file or not is UNKNOWN), 01101-PSN-IMC-ZZZ-PIM-TU-000001-zone2.rvt, 221229_MP-SK-01-03-001_R06_0001.dwg, Makkah Phase 1_Road Layout_new.dwg. There is no Revit All Zones folder, no Revit 2024 copy of a key and no key_copies.json, so R312 cannot check a key here.

The repo holds no hash of the originals, so whether a file found here is the same as the one on the personal PC is UNKNOWN until the copies of question QL1 are checked by python/key_copies.py.

## What needs what

- 5a needs items 1, 11, 21 and a green gate (items 22, 23). Item 23 needs PowerShell 7, step 2 below.
- 5b needs items 12 and 14, the inputs and the work folder, questions QL1 and QL2.
- The usage row of each phase needs item 20, step 3 below.

## Bader

Only Bader can do these. In this order.

1. On the personal PC, open the folder C:\02-Site Adaptation in VS Code, open the terminal, run the count below and keep its output.

```powershell
Get-ChildItem 'C:\02-Site Adaptation' -Directory | ForEach-Object { $f = Get-ChildItem $_.FullName -Recurse -File; "{0} | {1} files | {2:F2} GiB" -f $_.Name, $f.Count, (($f | Measure-Object Length -Sum).Sum/1GB) }
```

Look for: one line per folder, Revit All Zones among them.

2. Copy the whole folder C:\02-Site Adaptation from the personal PC to C:\02-Site Adaptation on the laptop, by the route of your choice. Then run the same count on the laptop.

Look for: the same lines on both machines.

3. On the laptop, download the PowerShell 7 zip for win-x64 from github.com/PowerShell/PowerShell/releases and unzip it to C:\Users\p003653k\Tools\pwsh.

Look for: C:\Users\p003653k\Tools\pwsh\pwsh.exe exists.

4. Download the Node.js LTS zip for win-x64 from nodejs.org and unzip it to C:\Users\p003653k\Tools\node, so node.exe sits directly in that folder.

Look for: C:\Users\p003653k\Tools\node\node.exe exists.

5. Open Start, type "Edit environment variables for your account", open Path and add these four folders, then OK.

```text
C:\Users\p003653k\Tools\pwsh
C:\Users\p003653k\Tools\node
C:\Users\p003653k\Tools\PortableGit\bin
C:\Users\p003653k\AppData\Local\Programs\gh\bin
```

Look for: the four lines in the list of Path.

6. Close every VS Code window, open the repo folder in VS Code again and open the terminal. Set git to the Windows certificate store.

```powershell
git config --global http.sslBackend schannel
```

Look for: no output.

7. In the same terminal, run these one by one.

```powershell
git fetch
pwsh -v
sh --version
gh --version
npx ccusage@latest --version
```

Look for: no fatal line from git fetch, and a version line from each of the other four.

8. Answer QL1 to QL6 of steps/lane-b.md, recommended answer first.
