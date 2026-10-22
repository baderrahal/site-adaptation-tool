# Rules

This file is the only source of the rules for this tool. Code, tests and prompts point here and hold no rule of their own.
Every rule has an id. A test that proves a rule names the id in its function name.

## Reader rules

- **R1 Read only.** A reader never changes the file it opens. The Revit command makes no transaction and closes every document it opened with no save. The Python readers open files in read mode.
- **R2 Round to 3 decimals.** Every coordinate or measure written to JSON is rounded to 3 decimals. Negative zero is written as 0. The one exception is the Excel columns El @ Entry and Height, which keep 4 decimals because a step count switches on thresholds that sit within 0.001 of real values.
- **R3 Small output.** Each JSON file holds only the fields listed for it below. No raw geometry dumps, no curve control points, no binary data.
- **R4 Revit prototypes.** prototypes.json lists every family name, type name, key parameters, bounding box, insertion point, rotation, level, plus the project survey point and base point. Lengths are in metres and angles in degrees. Key parameters default to a fixed list and can be replaced by a key-parameters.txt file next to the add-in DLL.
- **R5 CAD files.** layers.json lists every layer with entity counts. plots.json lists polylines and blocks with layer, coordinates and attribute text. texts.json lists every text and mtext with position. Coordinates are in drawing units, written as found. plots.json also holds the LINE and ARC entities of layer MP_PLOT_LINE as plot geometry. Lines are split where one ends on or crosses another, and every closed face is one plot_shape with its vertices, area and any arcs (centre and radius). Pieces that bound no closed face are written as plot_open with their segments, so nothing on that layer is dropped.
- **R6 CAD reader library.** The CAD reader uses the ACadSharp NuGet package. If ACadSharp fails to open a real sample DWG, the fallback is an ezdxf script on a DXF export, and the round log says which path was used.
- **R7 PDF licence.** The PDF reader uses pdfplumber, MIT licence. PyMuPDF is not allowed, in code or in requirements.
- **R8 PDF output.** pdf_labels.json lists every text label with its page position. pdf_shapes.json lists vector lines and rectangles with position. Positions are in PDF points from the top left of the page.
- **R9 Excel output.** coords.json holds the rows of the coordinates sheet keyed by column header. The header row is the first row with a cell containing Plot No. and rows above it are not data. Data stops at the first fully empty row, at a row with a cell starting with Total, or at a row with no plot number. coords_headers.json holds the headers found and the sheet row they came from. A missing sheet is an error that lists the sheets present, and a sheet with no Plot No. cell is an error.
- **R10 No invented data.** Tests use the smallest dummy file built in code. A reader is not called confirmed on real data until a real sample exists in the repo or the person has run it. Until then it stops at the dummy test stage.

## Test rules

- **R11 Zero tests is a failure.** The test gate fails when a job runs no tests.
- **R12 Every rule has a test.** The test name carries the rule id.

## Writing rules

- **R13 No attribution lines.** No generated-by footer and no co-authored-by line in any file, commit message or pull request text.
- **R14 No emoji and no em dash** in any file, commit message or report.
- **R15 No semicolons in prose, comments, documents, commit messages, or in Python, YAML, JSON and Markdown files.** C# statements end in a semicolon because the language requires it, so C# source is the one exception, and its comments and strings still follow the rule.

## Environment

- Windows, Revit 2024, .NET Framework 4.8, VS Code. A net48 project compiles through the Microsoft.NETFramework.ReferenceAssemblies package.
- Python readers run on Python 3.12.
