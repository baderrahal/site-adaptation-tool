## 17. New project and How it works

Lane C round NEWPROJ-UI, 31 Oct 2026. Bader's boards of 31 Oct 2026 are in docs/design/boards/ (NewProject, HowItWorks, Pane, PaneFirst and Tokens, the theme), copied from C:\SA-boards. They set the look and the words of the window New project, the page How it works and the pane changes. Where they differ from 7.2 of docs/design/12-ui-spec.md, the boards prevail (Bader, 31 Oct 2026). Part 16 is left to Lane A round 4i.

### What the user does

1. On first open the pane says "Set up this project" with the button Open New project. The Setup row says Not done.
2. New project lists the five files of the board with who gives each, which button uses it, Browse, Get template and Found or Missing. Start from picks Empty project or a saved project such as ROSHN Makkah.
3. Check files shows what is found and the yellow stop box, read only.
4. Save setup writes the settings into the model at once, in a transaction of its own that rolls back unless it commits, and the pane loads them as written (R445, Q280). With no model to write to, the foot says so in one line, the pane keeps the settings and the next real run writes them.

The user never types a path inside a file and never edits a JSON. No text of the add-in names a file ending in .json (NEWPROJ_no_text_of_any_window_names_a_json_file).

### New rules

docs/rules/ is a Lane A file, and a design part names an id from 400 up only once its rule is written there (R343). So the rules are named here by their plain names, their ids are in steps/HEADSUP.md and the code, and Lane A writes them into docs/rules (steps/waiting-on-others.md).

| Plain name | Input | Output | Test |
|---|---|---|---|
| Files the tool needs | none | the five rows of the board with the inputs of the settings JSON each fills, the buttons, optional or not, the template input | the_rows_follow_the_board_and_each_input_is_one_of_the_settings_json |
| Check files before saving | the settings Save would give, a probe of the disk | Found, Missing or Later per row, the ready count, the rows that stop a button, read only | check_files_reads_the_disk_only_and_stops_on_a_missing_file_a_button_needs and two more |
| Save setup in settings format | the settings of the pane, the saved project chosen, the picks | ProjectSettings in the settings JSON of R367, the scope, dry run tick, last runs and inputs not picked kept | save_keeps_the_field_names_and_every_value_not_picked and two more |
| Template of an input workbook | an input name, config | an empty xlsx with the sheet names and header rows the readers take | the_sales_template_holds_each_sheet_of_the_sales_workbook_at_its_header_row and three more |
| Saved projects to start from | the work root | Empty project, then each pre-setting of W4 in `<work root>\Saved projects` by its file name | saved_projects_are_the_pre_settings_of_the_folder_by_name_and_a_refused_file_is_skipped |

### The rows of the board and the settings JSON

| Board row | Input of the settings JSON | Buttons | Note |
|---|---|---|---|
| Site drawing (CAD) | baseplan | 1 to 4 | Villa blocks and Curbs keep their rows in the pane (Q281) |
| Plot list (Excel) | sales, the accommodation workbook of addin.workbookRoles | 3 | the DLUS workbook keeps its row in the pane (Q281) |
| Villa levels (Excel) | none yet | 4 and 5 | waits on the door level source of Lane A, shows Comes later |
| Road levels (civil file) | roadSurface, a folder | 4 | optional, never stops a button |
| Parameter sheet (Excel) | parameterSheet | 1 | |

Get template writes the parameter sheet with the eight columns of ParameterSheet, Parameter, GUID, Data type, Group, Category, Filled by, Sample value and Keep, on a sheet Parameters (R461), and the plot list with each sheet of dataBook.rev9 whose workbook has the sales role, at its headerRow. It refuses a path inside inputRoot of config (R1).

### What the tool read from the drawing

LastReadFacts gives what the current readers already give: the unit plots and the villa types (typologyRev9) of the plot table of the latest read of 2 Read data, and the plot line layers of the layer list beside config. The survey point, the layer count and the lists behind Change and View come with the scanner of a later Lane B round through the hook IDrawingFacts, NewProjectDialog.Facts (steps/waiting-on-others.md). Until then Change and View are grey.

### Wiring

- Pane: Open New project of the first open box, Open and Edit of the Setup row, New project of the row Help raise NewProjectRequested. How it works of the row Help and the link of the first open box raise HowItWorksRequested. Use a saved project runs Import of a pre-setting, as Import from past job did.
- PaneHost.Register: NewProjectRequested raises the external event NewProjectHandler, which opens NewProjectDialog.Show in the API context, writes its result into the model by SetupWrite and gives it to PaneViewModel.LoadSettings (R445). HowItWorksRequested opens HowItWorksDialog.Show, and Open New project there opens New project. The names of the saved projects reach the first open line by PaneViewModel.SetSavedStarts.
- New project: Browse, Get template, Check files, Save setup and Cancel each call the method of that name of NewProjectViewModel.

### Not built, each a question or a wait

The project name box shows the document title read only, since the settings JSON has no name (Q282). Picking a layer, a sheet, a column or a coordinate would change a config file (Q283). The Villa mode row stays, though the boards drop it (Q284). The rules name is the project field of the rules file, "ROSHN Makkah Phase 1" (Q285).

## SCAN-UI, What the tool read and the step gates, Lane C, 1 Nov 2026

The rules are R435 to R441 of docs/rules/12-lane-c-r430-up.md. The board is docs/design/boards/ScanResults.dc.html, Bader's approved board. The section Current files, the column Cluster and the dropdown Door level from have no board, they follow the style of the SetUpSite, ReviewData and CheckTable windows and are drafts (Q293).

### What the user does

1. Presses 2 Read data. The scan of Lane D (R427) runs first on the drawings, schedules, plot numbers and prototypes of the pane, or a scan of the same files is reused (R438, R439).
2. When every value has a source and no conflict is open, one pane line says what was read and the read goes on. Else the window What the tool read opens: a source dropdown per value, a yellow box for a value below the strong share, the conflicts with a pick per plot and Use the proposed for all, Export to Excel of the three tables, and Use these picks, which keeps the picks with the setup and lets the read go on (R435, R436).
3. In New project the layer, sheet and column rows are dropdowns of what the scan found, and View opens the same window (R440).
4. In 1 Set up site ticks the drawings that are the current issue and picks the rules profile. Apply stays grey until the site drawing is ticked, and button 9 names the ticked files in one line (R441).

### Settings

The settings JSON of R367 gains scanPicks (value kind to source), conflictPicks ("<plot>|<field>" to source) and currentFiles (input names). Each is left out while null, so formatVersion stays 1 (Q288). A saved project carries scanPicks, not the conflicts of its plots.

### Wiring

- Buttons.Body calls ReadDataBody.For, which wraps the read of ReadDataRun with ScanStep.Wrap and the gate ScanGate.Ask, ScanGate.Window opening ScanResultsDialog.Show. A run of the test host reads with no scan (Q295).
- NewProjectDialog.Facts is LatestScanFacts, View calls ScanResultsDialog.Show, Save setup gives the picks to SetupSave.Apply.
- R445: the pane button New project raises the external event NewProjectHandler of PaneHost, which calls PaneHost.OpenNewProject in the API context, NewProjectDialog.Show, then SetupWrite.Write with RevitModel and SettingsEntity of the document shown, SettingsStore.Save inside the transaction, then PaneViewModel.LoadSettings with the settings as written.
- SetUpSiteBody wraps the window of button 1 in CurrentFilesDialog, ReportBody adds the line of CurrentFiles.WithReportLine.

### Not built, each a question or a wait

The board rows Mirror, Survey point and Road level have no scan row (Q289). Curbs and road surface are not scanned (Q290). The read of button 2 does not take the picks yet (Q292). The line Current files is in the pane, not the Excel report (Q291).


## R492, Road surfaces picked in Set up, Lane D FIX-2NOV-D, 2 Nov 2026

R492 Road surfaces picked in Set up. A new project picks its road surfaces from a list, several of them, in the order the tool tries them, with no file editing. The scan does not read road surfaces (Q290), so the list is the surface files of the road surface folder of the pane. No board holds the window, so it is a DRAFT in the style of New project, its words CHOSEN in docs/design/14-ui-labels.md.

### What the user does

- On the row Road levels of New project, and on the row Road surface of the pane, the button Pick surfaces sits next to Browse. One line under the row gives the count and the short names in order. A short name is the part of the file name after the double underscore.
- The window Road surfaces holds one list, a tick box and the short name on each row. Ticked rows stand first in their order, the rest below in sorted order. Up and Down move the selected ticked row and are grey at the ends and on an unticked row. Use these picks keeps the order, Cancel keeps what was there. With no tick Use is grey and one line says to tick one.
- The window does not offer a file that siteBuild.roadFolder.notTaken of config says is no road, as ground or grading (R490). A saved pick naming one is refused with its reason by 2 Read data.
- A change of Start from in New project clears the pick of the row, so the line and the window show the list of the chosen project. A pick made after it stays. A saved project with no pick clears the pick when it is imported, its settings replace those of the project.
- The window opens on the effective order, the saved pick, else the list of the saved project. A name of the pick that the folder no longer holds stays in the list marked Missing and unticked, with one line naming it. A change of folder does not clear the pick.
- Under the past job a build does not read the pick, so the button is grey and its tooltip says the saved project takes its road surfaces from its past job. With no saved project chosen it is grey and says to pick one.

### Settings

ProjectSettings.RoadSurfaces, a list of file names with no extension, null while never picked, so a file written before it reads as before. SetupSave.Apply lays it over the settings when the draft holds one. RoadSurfaceChoices of Common gives the files of a folder a road surface may be, the effective order and whether the design basis is rev6. Under rev6 a new pick or order runs step 2 and the chain after it again, as a Browse does (R492).

### Wiring

- RoadPickState.Of reads the settings and the saved project and gives the reason the button is grey, the folder and the effective order. NewProjectViewModel.PickSurfaces and PaneViewModel.PickSurfaces call it, then the SurfacePicker they hold, which NewProjectDialog.Show and PaneHost.Register set to RoadSurfacesDialog.Show.
- New project keeps the order in the draft until Save setup (SetupDraft.RoadSurfaces), Cancel of the whole window keeps nothing. The pane keeps it in the settings in memory as a Browse does, and the next real run writes it into the model (R367).
- CheckTableBody.DoorWired sets CheckRun.RoadPick and ReadDataBody.For sets ReadDataRun.RoadPick, each from ProjectSettings.RoadSurfaces of the pane settings.
- Tests: dotnet/SiteAdaptation.Tests/R492_RoadPickTests.cs. The picture is road-surfaces of dotnet/SiteAdaptation.UiShots.
