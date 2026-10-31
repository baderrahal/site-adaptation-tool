## 17. New project and How it works

Lane C round NEWPROJ-UI, 31 Oct 2026. Bader's boards of 31 Oct 2026 are in docs/design/boards/ (NewProject, HowItWorks, Pane, PaneFirst and Tokens, the theme), copied from C:\SA-boards. They set the look and the words of the window New project, the page How it works and the pane changes. Where they differ from 7.2 of docs/design/12-ui-spec.md, the boards prevail (Bader, 31 Oct 2026). Part 16 is left to Lane A round 4i.

### What the user does

1. On first open the pane says "Set up this project" with the button Open New project. The Setup row says Not done.
2. New project lists the five files of the board with who gives each, which button uses it, Browse, Get template and Found or Missing. Start from picks Empty project or a saved project such as ROSHN Makkah.
3. Check files shows what is found and the yellow stop box, read only.
4. Save setup hands the settings back to the pane. The next real run of a button writes them into the model, as R367 says of every pick.

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

Get template writes the parameter sheet with the four columns of ParameterSheet on a sheet Parameters (CHOSEN), and the plot list with each sheet of dataBook.rev9 whose workbook has the sales role, at its headerRow. It refuses a path inside inputRoot of config (R1).

### What the tool read from the drawing

LastReadFacts gives what the current readers already give: the unit plots and the villa types (typologyRev9) of the plot table of the latest read of 2 Read data, and the plot line layers of the layer list beside config. The survey point, the layer count and the lists behind Change and View come with the scanner of a later Lane B round through the hook IDrawingFacts, NewProjectDialog.Facts (steps/waiting-on-others.md). Until then Change and View are grey.

### Wiring

- Pane: Open New project of the first open box, Open and Edit of the Setup row, New project of the row Help raise NewProjectRequested. How it works of the row Help and the link of the first open box raise HowItWorksRequested. Use a saved project runs Import of a pre-setting, as Import from past job did.
- PaneHost.Register: NewProjectRequested opens NewProjectDialog.Show, and its result goes to PaneViewModel.LoadSettings. HowItWorksRequested opens HowItWorksDialog.Show, and Open New project there opens New project. The names of the saved projects reach the first open line by PaneViewModel.SetSavedStarts.
- New project: Browse, Get template, Check files, Save setup and Cancel each call the method of that name of NewProjectViewModel.

### Not built, each a question or a wait

Save setup does not write the model at once (Q280). The project name box shows the document title read only, since the settings JSON has no name (Q282). Picking a layer, a sheet, a column or a coordinate would change a config file (Q283). The Villa mode row stays, though the boards drop it (Q284). The rules name is the project field of the rules file, "ROSHN Makkah Phase 1" (Q285).
