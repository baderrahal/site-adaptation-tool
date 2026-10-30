## 14. UI labels

The label list of control 2 of docs/design/12-ui-spec.md, UI controls. Every label, title, help line, button text and chip text of a window, word for word from docs/design/12-ui-spec.md and 7.1 to 7.4 of docs/design/07-mock.md, grouped by window. A value is written `<n>`, and `<m>` or `<name>` where the spec writes them so. An entry the spec gives no words for is marked CHOSEN with its reason, for Bader's check.

The gate test UiLabelTests reads this file. It fails on any text in the XAML or the view models of the add-in that the list of its window does not hold, and on any entry of a built window that no file of that window shows. A window with the line "Not built yet" counts against neither until the PR that builds it (Q136).

### Ribbon

The four panels and their buttons of 7.1 of docs/design/12-ui-spec.md, Bader's changes of 28 Oct 2026 (docs/design/13-workflow-changes.md).

- `Site Adaptation`
- `Setup`
- `Rules`
- `Build`
- `Output`
- `1 Set up site`
- `2 Read data`
- `3 Review data`
- `4 Check`
- `Site Adaptation Rules`
- `5 Floors and ramps`
- `6 Levels`
- `7 Place villas`
- `8 Walls`
- `9 Report`
- `Update from report`

### Pane

- `Site Adaptation`
- `Project`
- `not saved`
- `Next step`
- `Run <n> <name>`
- `Dry run is on.` CHOSEN, the spec asks one plain line that says dry run is on or off and gives no words
- `Dry run is off.` CHOSEN, as the line above
- `Start here`
- `Choose the settings file`
- `It names the drawings, the rules and the prototypes for this job. The tool fills the rest of this panel from it.`
- `Choose settings file`
- `Or start from a past job:` the first open line of Bader's changes of 28 Oct 2026 in three parts, the second its link
- `Import pre-setting`
- `It brings the settings, rules, level profile, ramp slopes, parameter sheet and overrides of that job.`
- `Rules and settings for this job. Set once.`
- `Settings`
- `Rules`
- `Level profile`
- `Pre-setting`
- `Import from past job`
- `Export`
- `Input files`
- `The drawings and schedules the tool reads. All <n> found.`
- `The drawings and schedules the tool reads. <m> of <n> found.`
- `Baseplan`
- `Villa blocks`
- `Curbs`
- `Road surface`
- `DLUS and sales`
- `Plot numbers`
- `Prototypes`
- `Shared parameters`
- `DLUS schedule` CHOSEN, the title of the first of the two file dialogs of the row DLUS and sales, each picks its workbook by its role
- `Sales schedule` CHOSEN, the title of the second of them
- `Browse`
- `Not chosen`
- `<n> MiB` CHOSEN, the size alone while the hash of a large file is worked out off the UI thread
- `<n> MiB <n>`
- `Scope`
- `Which plots the next button works on.`
- `Whole project`
- `Neighbourhood`
- `Plot list`
- `Dry run first, change nothing`
- `Villa mode`
- `Masses`
- `Links`
- `Steps`
- `Run them in order. Each one waits for the one before.`
- `Coordinates, levels and types from CAD`
- `Reads the drawings and schedules`
- `The team checks what was read`
- `Compares the rules with the past job`
- `Generic, yard and ramp floors`
- `Sets FFL and entry levels`
- `Places a villa on each plot at its FFL`
- `Boundary and garden walls`
- `Excel report of the run`
- `Done`
- `<n> issues`
- `Done with <n> issues`
- `Ready`
- `Needs step <n>`
- `Needs settings`

### Button messages

The messages and the confirm of a button, the TaskDialogs of the flow of R382, written in dotnet/SiteAdaptation.Common/Flow/RunTexts.cs, whose texts build a message from parts. A value `<n>` is the ribbon label of the button, an input, a file, a reason or a count. Cancel is the common button of the TaskDialog, Revit gives its word. Bader kept TaskDialogs for the short stops and messages, the confirm becomes a themed window in 5b (Q137 of steps/questions-closed.md).

An entry marked CHOSEN in 5a was worded in 5a PR 2 or PR 5, where docs/design/ gives the stop or the step but no words for it.

- `Save the project first, its work folder is named by its file`
- `<n> is not built yet.` CHOSEN in 5a
- `Site Adaptation <n>` CHOSEN in 5a, the name of the transaction in the undo list of Revit
- `<n> needs step <n>`
- `<n> needs <n>, the file is not chosen` CHOSEN in 5a
- `<n> needs <n>, the folder is not chosen` CHOSEN in 5a
- `<n> needs <n>, the file is not on disk`
- `<n> needs <n>, the folder is not on disk` CHOSEN in 5a
- `<n>: <n> changed since the dry run, nothing was changed. Run the button again.` CHOSEN in 5a
- `<n> changed nothing, the model is as it was. <n>` CHOSEN in 5a
- `The run failed: <n>` CHOSEN in 5a
- `The read back does not match: <n>` CHOSEN in 5a
- `Revit refused the change: <n>` CHOSEN in 5a
- `Revit did not commit the change.` CHOSEN in 5a
- `<n> changed the model, but <n> was not saved: another program holds it open. Close it and run the button again.` CHOSEN in 5a
- `<n> was not saved: another program holds it open.` CHOSEN in 5a
- `Dry run done, nothing was changed.` CHOSEN in 5a
- `Nothing changes until Apply.`
- `Apply`
- `Cancel`

### Set up site

- `1 Set up site`
- `Empty project found`
- `Existing site file`
- `Sets the coordinates, levels and types of this model from the CAD drawings.`
- `<n> of <n>`
- `check points pass`
- `What the project has now`
- `What the tool will set`
- `Survey point`
- `Project base point`
- `Levels`
- `Floor types and wall types`
- `Shared parameters`
- `Units`
- `Coordinate system`
- `Angle to true north`
- `Floor types`
- `Generic 150mm, yard, Flat Ramps, and one Ramps type per slope below`
- `Ramp slope`
- `Default 0.5%. Choices 0.5%, 1%, 2%, then 3% to 15%`
- `From the parameter sheet: <n>` the sheet name and its count
- `Wall types`
- `Will change`
- `Check points from CAD`
- `Point`
- `CAD`
- `Revit`
- `Diff`
- `Pass limit <n> mm. <n> of <n> pass` the value is the mock's 1 mm or Bader's 0.1 mm of 28 Oct 2026 by the answer of Q142 of steps/questions-closed.md
- `On an existing site file`
- `Nothing changes until Apply. Apply sets only the rows that differ.`
- `Cancel`
- `Dry run`
- `Apply`
- `Browse` the Browse of the row Shared parameters
- `Nothing differs, Apply changes nothing.` CHOSEN, under the pass line on a second run by 03a Second run, Apply stays off

### Check table

Not built yet, Phase 5c.

- `4 Check, NBH <n>, <n> plots` CHOSEN, the title of 7.4 of docs/design/07-mock.md under the number 4 of docs/design/13-workflow-changes.md
- `4 Check, NBH all, <n> plots` CHOSEN, as the line above
- `Level profile <n>, override file <n>, <n> overrides, <n> of them on NBH <n>`
- `Zoom to plot`
- `Export to Excel`
- `Save overrides`
- `Each row is a plot where the tool and the past job differ. Pick a row to see why, then choose Rule, Past or Skip. Save overrides keeps your choices.`
- `All`
- `Plot cut`
- `Floor`
- `Hand`
- `Unit code`
- `Position`
- `Level`
- `Parameter`
- `<n> plots follow every rule`
- `Plot`
- `NBH`
- `Unit`
- `Check`
- `Tool value (rule)`
- `Past job`
- `Rule`
- `Use`
- `Past`
- `Skip`
- `Selected`
- `Plot <n>, <unit>, <check>`
- `Tool`
- `Choose`
- `Keep the tool value.`
- `Use the past job. Saved as an override.`
- `Leave this plot out. It is listed in the report.`

### Update from report

The window of the button Update from report, 5f, word for word from the part Update from report of docs/design/13-workflow-changes.md. Built in 5f, its picture is steps/ui-shots/update-from-report.png.

- `Update from report`
- `Pick the edited report Excel`
- `Browse` CHOSEN, the button beside the line above
- `Changes found`
- `Follow the rules`
- `Break a manual rule`
- `Not editable ignored`
- `All`
- `FFL`
- `El @ Entry`
- `Steps`
- `Villa type`
- `Mirror`
- `Breaks a rule`
- `Apply`
- `Plot`
- `Field`
- `In model`
- `In Excel`
- `Rule check`
- `Keep the model`
- `Apply anyway`
- `Note` CHOSEN, the label of the note box of a change that breaks a manual rule
- `Updates the levels, floors, links and parameters of the changed plots only.`
- `Cancel`
- `Dry run`
- `Apply <n> changes`
- `Dry run: <n> changes would be saved, <n> replaced. Nothing was changed.` CHOSEN, the line a dry run of the window shows, it saves nothing
- `Saved <n> changes to the override list, plots changed: <n>.` CHOSEN in 5f fix, the message after Apply of a real run
- `<n> ticked changes were not saved:` CHOSEN in 5f fix, the line above the changes Apply could not save, in the same message
- `<name> <n>` CHOSEN in 5f fix, a chip with the count of its changes
- `follows the rules` CHOSEN in 5f fix, the rule column of a change every check that ran holds
- `no rule checks this field` CHOSEN in 5f fix, the rule column of a change no check ran on, not counted in Follow the rules
- `FFL over entry by the base` CHOSEN in 5f fix, the plain name of the check fflOverEntry, its id the tooltip
- `Steps by the steps rule` CHOSEN in 5f fix, the plain name of stepsRule
- `Front steps within their range` CHOSEN in 5f fix, the plain name of stepsAtMost
- `Height in the step legend` CHOSEN in 5f fix, the plain name of stepHeights
- `Riser height within its limits` CHOSEN in 5f fix, the plain name of riser
- `One step for one step units` CHOSEN in 5f fix, the plain name of oneStepUnits
- `One FFL per piece` CHOSEN in 5f fix, the plain name of oneFflPerPiece
- `Villa type of the profile` CHOSEN in 5f fix, the plain name of villaType
- `Mirror by the hand rule` CHOSEN in 5f fix, the plain name of handRule
- `the plot has no override entry of the job <n>, so Apply can not save it (Q169)` CHOSEN in 5f fix, the rule column of a change Apply can not save
- `the cell holds no number` CHOSEN in 5f fix, the rule column of a cell that is no change Apply can take
- `the cell holds no whole number` CHOSEN in 5f fix, as the line above
- `the cell is empty` CHOSEN in 5f fix, as the line above
- `the cell holds no villa type` CHOSEN in 5f fix, as the line above
- `the cell holds no TRUE or FALSE` CHOSEN in 5f fix, as the line above
- `<n> holds no sheet Settings with the Plan build id and the Plan SHA-256, so its build is not known (R402)` CHOSEN in 5f fix, the refusal line
- `<n> is of another build, its Plan build id is <n>, the plan of the model <n> (R402)` CHOSEN in 5f fix, the refusal line
- `<n> is of another build, its Plan SHA-256 is <n>, the plan of the model <n> (R402)` CHOSEN in 5f fix, the refusal line
- `<n> holds no sheet Levels or Placed, so it is no level Excel or report Excel (R402)` CHOSEN in 5f fix, the refusal line
- `the rules profile holds no level profile <n> (R402)` CHOSEN in 5f fix, the refusal line
- `<n> holds no header <n> in its sheet <n> (R402)` CHOSEN in 5f fix, the refusal line
- `the rules profile holds no <n>, so the check <n> is not run (R402)` CHOSEN in 5f fix, a line of the lacks, the check by its plain name
- `the rules profile holds no block manualRules, so no change breaks a manual rule (R402)` CHOSEN in 5f fix, a line of the lacks
- `<n> manual rules of the block name no updateChecks, so no change is held against them: <n> (R402)` CHOSEN in 5f fix, a line of the lacks
- `plot <n> <n>: <n> (R402)` CHOSEN in 5f fix, a ticked change Apply could not save, in the dry run line and the message after Apply
- `no field of the override list` CHOSEN in 5f fix, why in the line above
- `the plan holds no levelRule of the plot, so no gap to the rule` CHOSEN in 5f fix, why in the line above
- `no entry of source <n> of the plot names its key and model` CHOSEN in 5f fix, why in the line above
- `the level profile <n> names no job, so an override has no source (R402)` CHOSEN in 5f fix, the problem line of a dry run, the message of Apply
- `the override list holds no overrides (R402)` CHOSEN in 5f fix, as the line above
- `the override list holds <n>, which the form of majority.overrides_text does not write (R402)` CHOSEN in 5f fix, the message of Apply
- `the file name of a report Excel starts with its NBH and an underscore, as 012_Levels_<profile>.xlsx does (R402)` CHOSEN in 5f fix, the problem line of a pick, <profile> is a part of the name and no value
- `the files are of NBH <n>, Update from report takes the files of one NBH (R402)` CHOSEN in 5f fix, the problem line of a pick
- `<n> and <n> are both level Excels, Update from report takes one level Excel and one report Excel of one NBH (R402)` CHOSEN in 5f fix 2, the refusal line of two level Excels picked together
- `<n> and <n> are both report Excels, Update from report takes one level Excel and one report Excel of one NBH (R402)` CHOSEN in 5f fix 2, the refusal line of two report Excels picked together
- `the work folder holds no <n>, the plan of NBH <n> that 4 Check writes, so the Excel can not be compared (R402)` CHOSEN in 5f fix, the problem line of a pick
- `the worker CadReader.exe is not beside the add-in: <n>` CHOSEN in 5f fix, the problem line of a pick
- `the worker did not read the Excel: <n>` CHOSEN in 5f fix, the problem line of a pick, the status and message of the worker or the line below
- `no answer` CHOSEN in 5f fix, the worker gave no answer
- `the list the worker wrote is no JSON object` CHOSEN in 5f fix, the problem line of a pick
