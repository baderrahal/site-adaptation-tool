# Site Adaptation Tool

Read-only data readers for the Site Adaptation Automation Tool, ROSHN Makkah.
Phase 1 to 3. There is no placement logic yet.

| Reader | Language | Writes |
|---|---|---|
| Revit prototype reader | C#, Revit 2024 add-in, .NET Framework 4.8 | prototypes.json |
| CAD reader | C#, ACadSharp, .NET Framework 4.8 | layers.json, plots.json, texts.json |
| PDF reader | Python, pdfplumber (MIT) | pdf_labels.json, pdf_shapes.json |
| Excel reader | Python, openpyxl (MIT) | coords.json, coords_headers.json |

Every reader only reads. None of them changes a model, a drawing, a PDF or a workbook.
The rules are in [docs/rules.md](docs/rules.md). The latest round is the top entry of [steps/log.md](steps/log.md).

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
dotnet\CadReader\bin\Release\net48\CadReader.exe site.dwg out
```

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
