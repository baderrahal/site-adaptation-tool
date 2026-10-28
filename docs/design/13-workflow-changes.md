## 13. Workflow changes of 28 Oct 2026

Bader's design changes from the canvas "Site Adaptation UI", approved by Bader on 28 Oct 2026, written in Lane B 5a from his text, word for word where it gives labels and values. Where they differ from docs/addin-mock.md, docs/design/07-mock.md or the button order of docs/design/03a-buttons-shared-and-1.md and 03b-buttons-2-to-8.md, these changes prevail. The look of each part is in docs/design/12-ui-spec.md. The last part says which phase builds each change and which waits as a join item of steps/lane-b.md.

### Order

- Nine numbered steps: 1 Set up site, 2 Read data, 3 Review data, 4 Check, 5 Floors and ramps, 6 Levels, 7 Place villas, 8 Walls, 9 Report.
- Each step needs the one before, and 9 Report needs 4 Check.
- Place villas places each link at its FFL and writes Prototype-FFL. Levels writes no link parameter.

### Ribbon (5a)

- Panels Setup (1 to 4), Rules (Site Adaptation Rules), Build (5 to 8), Output (9 Report, Update from report).
- New icons on the 32 unit grid with the style rules of the UI spec, given in docs/design/12-ui-spec.md, Icons.

### Pane (5a)

- The steps list holds the 9 steps. The line of 3 is "The team checks what was read", the line of 7 "Places a villa on each plot at its FFL".
- The Next step box follows the new order.
- Input files gain a row "Shared parameters" for the parameter sheet Excel.
- The Project section gains a row "Pre-setting" with the buttons "Import from past job" and "Export".
- The first open Start here box adds: "Or start from a past job: Import pre-setting. It brings the settings, rules, level profile, ramp slopes, parameter sheet and overrides of that job."
- A pre-setting never holds drawing paths or coordinates. The logic joins R367 at 5c.

### Set up site (5b)

- Pass limit 0.1 mm, from addin.position.checkPointLimitMm of config. Config is Phase 4 territory, so the key is a join item for 5c, and 5b reads it from config.
- Floor types row: "Generic 150mm, yard, Flat Ramps, and one Ramps type per slope below".
- New row "Ramp slope" with a dropdown 0.5%, 1%, 2%, then 3% to 15% in 1% steps, default 0.5%, and the line "Default 0.5%. Choices 0.5%, 1%, 2%, then 3% to 15%".
- The Shared parameters row reads "From the parameter sheet: ..." with the sheet name, its count and Browse. Parameters and their GUIDs come only from that sheet, never made by the tool.

### Review data (5c)

- A new window "3 Review data, NBH [code], [n] plots".
- Band line "Read from" the source drawings, "[n] of [n] checked by the team", buttons Zoom to plot, Export to Excel, Save review.
- How to use line: "This is what the tool read from CAD and Civil 3D for each plot. Pick a plot to see its corners and sources, then mark it OK or Wrong. A plot marked Wrong is listed in 4 Check and in the report."
- Chips All, Not checked, OK, Wrong. A Find plot box.
- Columns Plot No, CAD label, Villa type, Mirror, Cluster, Area m2, Road at front, Team check.
- Side box: Selected plot, CAD label, NBH, Cluster, Mirror, Front edge, Road at front, a Plot corners table (Corner 1 to 4, X, Y, shared coordinates, corner 1 the left front corner seen from the road), Team check OK or Wrong, a note "What is wrong".

### Site Adaptation Rules (5c)

- A new window: the title, a band line with the manual name, revision, number and "Rev 06 prevails over Rev 01", buttons Open the manual PDF, Export to Excel.
- Chips by group, and "Past job differs".
- Columns ID, Rule, Manual, Checked in, Past job.
- Side box with the full text, values, checked in, past job, what the tool does.
- Rules M01 to M15 as drafted on the canvas, held per project in the rules profile, a block manualRules with id, text, group, manual section and the steps that check it, never in code. Code reads the page numbers from the manual PDF.
- Every check row, Review data row and report line names its manual rule ID. The Check table side box shows the manual rule with "Open in Site Adaptation Rules".

### Update from report (5f)

- A new phase 5f, after 5e and before 6a, and a window "Update from report".
- Pick the edited report Excel and list every change: Apply, Plot, Field, In model, In Excel, Rule check.
- Tiles Changes found, Follow the rules, Break a manual rule, Not editable ignored.
- Chips All, FFL, El @ Entry, Steps, Villa type, Mirror, Breaks a rule.
- A change that breaks a manual rule is unticked by default, with Keep the model or Apply anyway plus a note.
- Apply saves each change as an override and updates only the changed plots. A report whose build id differs is refused.
- Foot line "Updates the levels, floors, links and parameters of the changed plots only." Buttons Cancel, Dry run, Apply [n] changes.

### UI controls

The UI controls of docs/design/12-ui-spec.md hold as given before: the theme test, the text test with these new labels in the label list, the picture test and the UI checklist in review. Each new window gets its PNG in steps/ui-shots/ in the phase that builds it.

### Where each change lands

- 5a, Lane B PR 8: the order of the steps in the pane and in the flow, the ribbon of four panels with the three new icons, the pane changes of the part Pane, the label list and the PNGs. The buttons Site Adaptation Rules and Update from report, and Import from past job and Export, say they are not built yet until their phase (CHOSEN by Code).
- 5b: the Set up site part, with the pass limit read from config once 5c adds the key.
- 5c: the key addin.position.checkPointLimitMm 0.1 in config, the manualRules block of the rules profile, the pre-setting logic with R367, the Review data and Site Adaptation Rules windows, the manual rule ID on every check row, Review data row and report line.
- 5d: Place villas at the FFL with Prototype-FFL, and Levels with no link parameter, in the buttons of the new order.
- 5e: the report line with its manual rule ID.
- 5f: Update from report.
