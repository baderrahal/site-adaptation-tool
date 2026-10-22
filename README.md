# Site Adaptation Tool

Read-only data readers for the Site Adaptation Automation Tool, ROSHN Makkah.
Phase 1 to 3. There is no placement logic yet.

| Reader | Language | Writes |
|---|---|---|
| Revit prototype reader | C#, Revit 2024 add-in, .NET Framework 4.8 | prototypes.json |
| CAD reader | C#, ACadSharp, .NET Framework 4.8 | layers.json, pieces.json, plots.json, other_faces.json, texts.json |
| PDF reader | Python, pdfplumber (MIT) | pdf_rows.json, pdf_labels.json, pdf_shapes.json |
| Excel reader | Python, openpyxl (MIT) | coords.json, coords_headers.json, coords_extra.json |
| Link table | Python | links.json, conflicts.json, extents.json |
| Full run | Python | one folder per file, full_run_summary.json |

For a project file the Revit reader also writes the Revit links with the family instances inside them, the walls and the ramps (rules R16 and R17).

Every reader only reads. None of them changes a model, a drawing, a PDF or a workbook.
The rules are in [docs/rules.md](docs/rules.md). The working agreement is in [CLAUDE.md](CLAUDE.md).
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

## Install the Revit add-in

Build the solution, then copy `dotnet\RevitPrototypeReader\bin\Release\net48\` and
`dotnet\RevitPrototypeReader\SiteAdaptation.addin` to
`%APPDATA%\Autodesk\Revit\Addins\2024\`. Edit the Assembly line in the .addin file to the full path of
`RevitPrototypeReader.dll`.

## Tests

```
.venv\Scripts\python -m pytest python
dotnet test dotnet\SiteAdaptation.sln
```
