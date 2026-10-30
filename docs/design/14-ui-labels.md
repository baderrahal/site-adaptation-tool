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
- `<n> did not finish, nothing was kept: <n>` CHOSEN in 5c, the end of a button whose worker did not end done (R375)
- `no reason was given` CHOSEN in 5c, the reason of such an end that names none
- `<n> is running already, its worker has not ended.` CHOSEN in 5c, a second click while the worker runs (docs/design/02-folders.md)
- `Reads <n> files, <n> MiB.` CHOSEN in 5c, the counts of the confirm of 2 Read data
- `Reuses the read of run <n>, its <n> input files are unchanged.` CHOSEN in 5c, as the line above on a second run
- `<n>, <n> bytes, SHA-256 <n>, unchanged since the last read` CHOSEN in 5c, a file of the dry run list of 2 Read data
- `<n>, <n> bytes, SHA-256 <n>, new or changed since the last read` CHOSEN in 5c, as the line above
- `<n> done: <n> files written, <n> issues.` CHOSEN in 5c, the end of 2 Read data
- `<n> done: the read of run <n> is reused, nothing was read again.` CHOSEN in 5c, the end of a second run of 2 Read data
- `the worker ended <n>, <n>` CHOSEN in 5c, the reason of a worker that failed, was refused or ran past its time limit
- `<n> read the files, but the scope stops it: <n>. Change the scope and run it again, the read is reused.` CHOSEN in 5c, the scope of R366 held against the plot table

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

### Review data

Built in Lane B 5c. The labels, columns, chips and side box are those of Review data (5c) in docs/design/13-workflow-changes.md, word for word. The entries marked CHOSEN are Code's.

- `3 Review data, NBH <n>, <n> plots`
- `Read from <n>` CHOSEN, the words of 13-workflow-changes.md then the file names of the source drawings
- `<n> of <n> checked by the team`
- `Zoom to plot`
- `Export to Excel`
- `Save review`
- `This is what the tool read from CAD and Civil 3D for each plot. Pick a plot to see its corners and sources, then mark it OK or Wrong. A plot marked Wrong is listed in 4 Check and in the report.`
- `All`
- `Not checked`
- `OK`
- `Wrong`
- `Find plot`
- `Plot No`
- `CAD label`
- `Villa type`
- `Mirror`
- `Cluster`
- `Area m2`
- `Road at front`
- `Team check`
- `Selected plot`
- `NBH`
- `Front edge`
- `Plot corners`
- `Corner <n>`
- `X`
- `Y`
- `Shared coordinates` CHOSEN, written with a capital as a heading of the corners
- `What is wrong`
- `Manual rule <n>` CHOSEN, the rule M01 to M15 of docs/design/15-manual-rules.md that the row is checked by
- `Edge <n>` CHOSEN, the front edge as its number in the ring of the plot, from 1
- `not read yet` CHOSEN, for a value that only the plan of 4 Check gives, or the site file of 1 Set up site
- `Yes` CHOSEN, the Mirror value
- `No` CHOSEN, the Mirror value
- `Pick a plot from the table.` CHOSEN, the side box with no plot selected
- `Earlier note: <n>` CHOSEN, the note of a check made on an earlier read, which shows as Not checked now
- `Not saved yet` CHOSEN, beside Save review after a mark
- `Review saved.` CHOSEN, after Save review

### Site Adaptation Rules

Built in Lane B 5c. The title, band line, buttons, chips, columns and side box are those of Site Adaptation Rules (5c) in docs/design/13-workflow-changes.md, word for word. The rule texts, the groups, the sections and the pages are data of the block manualRules in the rules profile, so they are not in this list. The entries marked CHOSEN are Code's.

- `Site Adaptation Rules`
- `<n>, <n>, document <n>` CHOSEN, the manual name, its revision and its number from the profile, in the band line
- `<n> prevails over <n>` CHOSEN, "Rev 06 prevails over Rev 01" of 13-workflow-changes.md with the two revisions from the profile
- `Open the manual PDF`
- `Export to Excel`
- `All`
- `Past job differs`
- `ID`
- `Rule`
- `Manual`
- `Checked in`
- `Past job`
- `Selected rule` CHOSEN, the heading of the side box
- `Full text` CHOSEN, a label of the side box
- `Section and page` CHOSEN, the section and printed page of the manual with the page of the PDF in brackets
- `Values` CHOSEN, a label of the side box
- `What the tool does` CHOSEN, "what the tool does" of 13-workflow-changes.md written with a capital as a label
- `not read yet` CHOSEN, for the text of what the tool does, and for a past job not measured
- `none` CHOSEN, the values of a rule whose text holds none
- `Pick a rule from the table.` CHOSEN, the side box with no rule selected
- `The manual PDF was not found. Looked for <n>` CHOSEN, when the PDF is in none of the folders of the profile
- `The manual PDF did not open: <n>` CHOSEN, when the shell refuses the file

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
