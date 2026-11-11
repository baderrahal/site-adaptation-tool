# PC setup, 12 Nov 2026

Lead, on Bader's own PC (Windows 11 Pro, user bader), main 0c31771. Bader's order of 12 Nov: this PC is the work machine, the laptop is gone. RAN means run in this session. Nothing was installed that needs admin, and no registry key was read or written.

## What this PC has

| # | Item | Status | Value | How known |
|---|---|---|---|---|
| 1 | git | found | 2.55.0.windows.5, C:\Program Files\Git\cmd\git.exe. On the PATH of PowerShell 7 and of Windows PowerShell 5.1 | RAN git --version, Get-Command git in both shells |
| 2 | Git Bash | found | C:\Program Files\Git\bin\bash.exe, the one .claude/hooks/sh-guard.ps1 finds above git | RAN Test-Path |
| 3 | gh | found | 2.102.0, C:\Program Files\GitHub CLI\gh.exe, logged in with the repo scope | RAN gh --version, gh auth status |
| 4 | dotnet SDK | found | 8.0.428, 9.0.313, 10.0.400 in C:\Program Files\dotnet\sdk | RAN dotnet --list-sdks |
| 5 | net48 targeting pack | found | C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8 holds mscorlib.dll | RAN Test-Path |
| 6 | Visual Studio | found | 18 Community and a 2022 folder under C:\Program Files\Microsoft Visual Studio. The build does not need it, dotnet builds the solution | RAN Get-ChildItem |
| 7 | Python 3.12 | found, `py` missing | 3.12.10 in %LOCALAPPDATA%\Programs\Python\Python312. No py launcher on the PATH, `py -V:3.12` fails. Fix: call python.exe by that path, as CLAUDE.md now does. The .venv of the repo is on it | RAN python.exe --version, where.exe py |
| 8 | Python packages | 2 were missing, fixed | pyclipr 0.1.8 and PyYAML 6.0.2 were not in the .venv. `pip install -r requirements.txt` put them in. All 7 lines of requirements.txt are now there at their pinned versions | RAN pip list before and after |
| 9 | Revit 2024 API DLLs | found | C:\Program Files\Autodesk\Revit 2024\RevitAPI.dll and RevitAPIUI.dll, 24.3.40.26 | RAN Get-Item |
| 10 | Other Revit | found | Revit 2023, 2025 and 2027 beside 2024, and the older 2016, 2019, 2020, 2021 | RAN Get-ChildItem of C:\Program Files\Autodesk |
| 11 | Revit 2024 template | found | C:\ProgramData\Autodesk\RVT 2024\Templates\English\DefaultMetric.rte | RAN Test-Path |
| 12 | Civil 3D 2026 | found | C:\Program Files\Autodesk\AutoCAD 2026 with acad.exe R25.1.74.0.0, accoreconsole.exe and C3D\AeccDbMgd.dll | RAN Test-Path, Get-Item |
| 13 | PowerShell | found | pwsh 7.6.9 in C:\Program Files\PowerShell\7, Windows PowerShell 5.1.26100 for the hooks | RAN |
| 14 | claude CLI | not on the PATH, no fix needed | No claude command. The Claude Code of the VS Code extension is there, anthropic.claude-code-2.1.322, with claude.exe, which scripts/lanes/run_lane.py finds by itself | RAN Get-Command claude, Test-Path |
| 15 | node | found | C:\Program Files\nodejs\node.exe | RAN Get-Command |
| 16 | Free disk on C | 72.5 GB free | 392.1 GB used. The old project folder is 8.3 GB, a scenario set is a copy of its raw part | RAN Get-PSDrive |
| 17 | RAM | 16 GB | Under the 32 GB of siteBuild.revitSlotsMinTotalGB (config/project.json:283), so python/window_lock.py:104 gives one Revit slot. The laptop had 64 GB (steps/laptop-check.md item 16) | RAN Win32_ComputerSystem |
| 18 | CPU and graphics | 12th Gen Intel Core i7-12700, Intel UHD Graphics 770 | No separate graphics card. The speed numbers of 6 to 8 Nov were measured on the laptop and do not hold here | RAN Win32_Processor, Win32_VideoController |
| 19 | C:\02-Site Adaptation | found | 837 files in 8 folders, see steps/old-project-pc.md | RAN Get-ChildItem |
| 20 | SA_COPY\manifest.csv | missing | No SA_COPY folder on C: or under the OneDrive of this user. The repo holds its row counts only, 837 inputs rows and 470 work rows (steps/laptop-check.md:58) | RAN Get-ChildItem, grep |
| 21 | The old work folder | missing | C:\Users\bader\Documents\SiteAdaptation is not there, and C:\SA-revit-jobs is not there. The data book, the past plans and the scenario answers that lived in the work root must be made again | RAN Test-Path |

## The work root

- `pwsh scripts\set-machine.ps1` wrote %APPDATA%\SiteAdaptation\machine.json with workRoot C:\SA-work\SiteAdaptation and made the folder. RAN, output: `WROTE ...\machine.json, workRoot C:\SA-work\SiteAdaptation`.
- CHOSEN by Code: the order named C:\SA-work. The readers of the machine file refuse a work root whose last folder is not named SiteAdaptation (dotnet/SiteAdaptation.Common/WorkRoot.cs:82, dotnet/SiteAdaptation.TestHost/TestHostPaths.ps1:135), since the test host keeps its files in a folder of that name. So C:\SA-work is the work folder and C:\SA-work\SiteAdaptation the work root. The folders of the order lie in it: prototypes_2024, answer-key, scenarios.
- The script refuses a root the readers would refuse and a root under an input root. scripts/checks/test_set_machine.py, 7 passed, a root that ends in a backslash among the refused.

## Laptop paths

Searched every tracked file for the laptop user name, PortableGit and the pwsh folder under Tools (`git grep`).
- In scripts, hooks, CLAUDE.md and .claude: 0 hits. The scripts and hooks already take git and bash from the PATH (.claude/hooks/sh-guard.ps1:9) and the work root from the machine file (.claude/hooks/lane.ps1:118).
- The hits left are records of past rounds under steps/ and docs/rules/, kept as they are.
- Changed: two agent files and README.md named C:\Users\bader\Documents\SiteAdaptation, they now name the work root. .claude/rules/revit.md said the laptop has enough memory, it now says one Revit at a time. CLAUDE.md lost the fast build section of 6 Nov, kept in steps/history/CLAUDE-fast-build-6nov.md, and holds the order of 12 Nov.
- Civil 3D: every version named in code and config is already 2026 (python/c3d_probe.json:3 and :213, dotnet/SiteAdaptation.C3DProbe/Civil/SiteAdaptation.C3DProbe.Civil.csproj:7). 2025 stands only in records of the laptop.
- Left as they are: the Assembly path of dotnet/SiteAdaptation.TestHost/SiteAdaptationTestHost_Revit2024.addin:5 and dotnet/SiteAdaptation.Placer/SiteAdaptationPlacer_Revit2024.addin:5 holds a folder of this PC's own user, as committed before the laptop. Whether a run reads that line or the manifest the install writes into the add-ins folder is UNKNOWN, not traced. steps/audit-12nov.md section 5 names them for the public repo.

## Lanes

`git worktree add` made .claude/worktrees/build-1, build-2, build-3 and revit-test from origin/main 0c31771, each on its own branch lane/<name>. RAN git worktree list, 5 rows with the main folder.

## Build, tests, install

| Step | Result | How known |
|---|---|---|
| dotnet build dotnet\SiteAdaptation.sln -c Release | 0 errors, 40 warnings, 49 s | RAN |
| pwsh scripts\quick-tests.ps1 | PASS. pytest 154 passed, 0 failed. dotnet net8.0 2,774 passed, 0 failed, 0 skipped. 81 s | RAN |
| Install-SiteAdaptation.ps1 -NoBuild | exit 0. One manifest of the tool in the Revit 2024 add-ins folder, SiteAdaptationAddIn_Revit2024.addin. The installed SiteAdaptation.AddIn.dll equals the Release build by SHA-256 | RAN, Get-FileHash of both |

Not run here: the full gate (all of pytest, the net48 leg of dotnet test). GitHub runs it on the PR. Not seen in Revit: the add-in was installed, Revit was not started in this round.
