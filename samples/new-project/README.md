# New project sample

A small made up project with no past job data, for the clean folder test. That test copies only this folder and runs the build named in build.json, and fails if any path in scripts/checks/past_paths.txt is read.

The files here are written by tests/acceptance/made_up_project.py and hold invented names and numbers only: one drawing with 8 plots in two areas, one sheet and two empty prototype files. build.json names scripts/scenarios/clean_stage1.py, which runs stage 1 on the folder through the worker command dataCheck. That needs the worker built first (dotnet build dotnet/SiteAdaptation.sln -c Release).

Never put a real drawing, coordinate or level here, the repo is public. The .gitignore lets in site.dxf, plots.xlsx and Prototypes/*.rvt of this folder and no other drawing file.
