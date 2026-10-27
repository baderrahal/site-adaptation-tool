# Site Adaptation Tool

Data readers for the Site Adaptation Automation Tool, ROSHN Makkah, and the placer, which places the villa links from plan.json.

| Reader | Language | Writes |
|---|---|---|
| Revit prototype reader | C#, Revit 2024 add-in, .NET Framework 4.8 | prototypes.json |
| CAD reader | C#, ACadSharp, .NET Framework 4.8 | layers.json, pieces.json, plots.json, other_faces.json, texts.json |
| PDF reader | Python, pdfplumber (MIT) | pdf_rows.json, pdf_labels.json, pdf_shapes.json |
| Excel reader | Python, openpyxl (MIT) | coords.json, coords_headers.json, coords_extra.json |
| Link table | Python | links.json, conflicts.json, extents.json |
| Full run | Python | one folder per file, full_run_summary.json |
| Plan from past job | Python | plan.json outside the repo |
| Placer | C#, Revit 2024 add-in, .NET Framework 4.8 | villa links in the open model, placer_report.json |

For a project file the Revit reader also writes the Revit links with their names and parameters, the walls, the floors with their corner levels, the ramps and the spot elevations (rules R16, R17 and R34 to R38). The contents of the linked files are off by default (rule R39).

Every reader only reads. None of them changes a model, a drawing, a PDF or a workbook. The placer is the one part that changes a model, only the open document and only in its real run (R83 to R88). Its default is a dry run.

## Build the plan from the past job

```
.venv\Scripts\python python\plan_from_past.py
```

The names come from pastJob in config/project.json (R81). The plan is written to pastJob.output, outside the repo.
The rules are in [docs/rules/](docs/rules/index.md). The working agreement is in [CLAUDE.md](CLAUDE.md).
The latest round is the top entry of [steps/log.md](steps/log.md) and the live state is [steps/PROGRESS.md](steps/PROGRESS.md).

## Run the Python readers

```
py -m venv .venv
.venv\Scripts\python -m pip install -r requirements.txt
.venv\Scripts\python python\pdf_reader.py plan.pdf out
.venv\Scripts\python python\excel_reader.py coords.xlsx out --sheet Coordinates
```

## Run the CAD reader

```
dotnet build dotnet\SiteAdaptation.sln -c Release
dotnet\CadReader\bin\Release\net48\CadReader.exe site.dwg out --layers config\cad_layers.json
```

config\cad_layers.json holds the keep and drop lists of layers and the plot line and plot label layers.
Edit it to change what is written. Without --layers the reader uses cad_layers.json next to the program.

## Link plots

```
.venv\Scripts\python python\link_table.py out\link --cad out\plan.dwg --rows out\a.pdf\pdf_rows.json --coords out\book.xlsx\coords.json
```

## Run everything on the real files

```
.venv\Scripts\python python\full_run.py "C:\02-Site Adaptation" sample_output\full_run
```

Each file runs in place with a time limit of 600 seconds. A failure or a timeout is recorded and the run goes on.
sample_output is ignored by git.

The roles of the drawings (baseplan, masterplan, previousMasterplan, villaBlocks, roadLayout) and the skipped drawings come from config/project.json (R63). The analysis stage writes sample_output/full_run/analysis/analysis.json with the clusters of duplex and townhouse plots cut from their rows (R64, R70), the issues of checks 1 and 2 (R65, R66) and, when the block drawing and the road layout were read, the block of each plot (R69) and the front of each row (R71). To run only that stage on a CAD output folder of the baseplan, add --blocks and --roads with the output folders of the block drawing and the road layout:

```
.venv\Scripts\python python\cad_analysis.py sample_output\analysis --cad "sample_output\full_run\ROSHN Makkah Baseplan.dwg"
```

The level rule (R67) is checked against the two past schedules, read in place, with python\levels_proof.py. Add --analysis with an analysis.json to feed the rule with the CAD clusters.

## Count the links

```
.venv\Scripts\python python\link_summary.py prototypes.json out
```

Prints the counts and writes out\link_summary.json (rule R27). Add --excel coords.json to check the villa links against the schedule rows.

## Relate the floors and the link of each plot

```
.venv\Scripts\python python\plot_relations.py C:\Users\bader\Documents\SiteAdaptation\prototypes.json --excel "C:\02-Site Adaptation\01_Output\1.11 References\00101-PSN-IMC-XXX-SCH-TU-090000.xlsx"
```

Writes plot_relations.csv and plot_relations_summary.md next to prototypes.json, never inside the repo (rules R57 to R61). It measures and sets no rule. It needs the file from the round 9 add-in for the top faces, outlines and link boxes, and falls back to the sketch outline of an older file.

## Install the Revit add-in

Build the solution, then copy `dotnet\RevitPrototypeReader\bin\Release\net48\` and
`dotnet\RevitPrototypeReader\SiteAdaptation.addin` to
`%APPDATA%\Autodesk\Revit\Addins\2024\`. Edit the Assembly line in the .addin file to the full path of
`RevitPrototypeReader.dll`.

The add-in reads the link instances and not what is inside the linked files. To read the contents of each linked file once, put the line `--link-contents once` in a file named reader-options.txt next to the DLL. The default is `--link-contents none`.

## Tests

```
.venv\Scripts\python -m pytest python
dotnet test dotnet\SiteAdaptation.sln
```
