# Rules

This file is the only source of the rules for this tool. Code, tests and prompts point here and hold no rule of their own.
Every rule has an id. A test that proves a rule names the id in its function name.

## Reader rules

- **R1 Read only.** A reader never changes the file it opens. The Revit command makes no transaction and closes every document it opened with no save. The Python readers open files in read mode.
- **R2 Round to 3 decimals.** Every coordinate or measure written to JSON is rounded to 3 decimals. Negative zero is written as 0. The one exception is the Excel columns El @ Entry and Height, which keep 4 decimals because a step count switches on thresholds that sit within 0.001 of real values.
- **R3 Small output.** Each JSON file holds only the fields listed for it below. No raw geometry dumps, no curve control points, no binary data.
- **R4 Revit prototypes.** prototypes.json lists every family name, type name, key parameters, bounding box, insertion point, rotation, level, plus the project survey point and base point. Lengths are in metres and angles in degrees. Key parameters default to a fixed list and can be replaced by a key-parameters.txt file next to the add-in DLL.
- **R5 CAD files.** layers.json lists every layer with entity counts. plots.json lists polylines and blocks of model space with layer, coordinates and attribute text. texts.json lists the text of the drawing with position. Coordinates are in drawing units, written as found. The LINE and ARC pieces on the plot line layers named in the config (rule R18) are plot geometry. Lines are split where one ends on or crosses another, and every closed face has its vertices, area and any arcs (centre and radius). Pieces that bound no closed face are written to plots.json as plot_open with their segments, so nothing on those layers is dropped. What a closed face becomes is rule R20.
- **R6 CAD reader library.** The CAD reader uses the ACadSharp NuGet package. If ACadSharp fails to open a real sample DWG, the fallback is an ezdxf script on a DXF export, and the round log says which path was used.
- **R7 PDF licence.** The PDF reader uses pdfplumber, MIT licence. PyMuPDF is not allowed, in code or in requirements.
- **R8 PDF output.** pdf_labels.json lists the text labels with their page position, except the margin stamp and the key plan text, which are counted in pdf_rows.json. pdf_shapes.json lists vector lines and rectangles with position. Positions are in PDF points from the top left of the page. The table rows are rule R21.
- **R9 Excel output.** coords.json holds the rows of the coordinates sheet keyed by column header. The header row is the first row with a cell containing Plot No. and rows above it are not data. Data stops at the first fully empty row, at a row with a cell starting with Total, or at a row with no plot number. coords_headers.json holds the headers found and the sheet row they came from. Each row carries sheetRow, its row number in the sheet. coords_extra.json holds the non-empty rows above the header and from the end of the table down, with sheet row numbers, and the formula of each derived column in the first plot row. A missing sheet is an error that lists the sheets present, and a sheet with no Plot No. cell is an error.
- **R10 No invented data.** Tests use the smallest dummy file built in code. A reader is not called confirmed on real data until a real sample exists in the repo or the person has run it. Until then it stops at the dummy test stage.

## Round 4 rules

- **R16 Main is protected.** The hook in .claude/hooks/guard-main.sh blocks git commit while on main and any git push that names main. Work happens on a branch and lands by pull request. A session that starts outside the repo has no hooks.
- **R17 Explode.** The CAD reader explodes every block reference, nested ones too, down to lines, arcs, circles, polylines, hatches and points, to a depth of 16 and with a stop on a block that contains itself. Each piece in pieces.json has layer, handle, world coordinates, the handles of the insert chain, and any attributes and extended data. Layer 0 inside a block takes the layer of its insert. Mirrored inserts flip arc direction and bulge. layers.json holds the entity count of each layer before and after exploding, the extents of the kept pieces, and totals. An array insert is read for its first cell and counted.
- **R18 Layer filter.** config/cad_layers.json holds keep and drop lists of layer names, with * and ? wildcards and no case. Drop wins over keep. A layer on neither list is kept and shown as unlisted. Pieces and texts on a dropped layer, and everything inside an insert on a dropped layer, are counted and not written. The same file names the plot line layers and the plot label layers. A config that is missing falls back to no filter and the default plot layers.
- **R19 Texts and xrefs.** texts.json also holds text inside exploded blocks, block attribute values and MULTILEADER text, each with its handle and insert path. layers.json lists every xref with name, saved path and whether the file was found next to the drawing, and the run goes on without it. Reader notes are grouped with counts.
- **R20 Plot labels.** A plot label is a text on a plot label layer whose last line is a number, with any lines or words before it as the label type. A closed face that holds exactly one label is a plot_shape in plots.json with plot, type and centre. A face with no label or with several goes to other_faces.json with the reason no_label or many_labels and is not deleted. plots.json summary counts shapes, plots, other faces, labels outside every face, labels with no number and duplicate numbers.
- **R21 PDF rows.** pdf_rows.json holds one record per table row with the five values plot, code, type, n and e, and page, block and row. Blocks are read left to right and rows top to bottom, so a plot that runs from the foot of one block to the top of the next stays in order, and continued counts those plots. Text that sits in a block but is not a row is listed as rejected. A number with no decimal point is kept as found and listed in odd. The margin stamp, the key plan text and short strokes outside the table are counted and left out. Touching pieces of one table rule are one line.
- **R22 Link table.** link_table.py joins CAD plots, PDF rows and Excel rows by plot number into links.json. It checks each join by location, where every PDF point must lie inside the CAD plot of the same number or within the tolerance of its edge. Each plot also records which CAD plot holds the points and the number difference. conflicts.json lists every disagreement, duplicate and missing match and nothing is resolved silently. extents.json compares the extents of every drawing and says yes or no for overlap.
- **R23 Full run.** full_run.py runs each reader in place on the real files and gives each file its own output folder and a time limit of 600 seconds. A failure or a timeout is recorded with its exact text and the run goes on with the next file. DWG files under 01_Output, .bak files and Excel lock files are not read, and the skipped files are listed.
- **R24 Harness files.** CLAUDE.md is under 200 lines. steps/PROGRESS.md is at most 60 lines. The top entry of steps/log.md fits one page and has Done, Remains, Known bugs, Next and Your turn in that order.

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
