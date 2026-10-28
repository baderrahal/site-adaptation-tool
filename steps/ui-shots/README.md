# UI shots

Control 3 of docs/design/12-ui-spec.md, UI controls. Each PNG is a render of a window of the add-in, offscreen, with sample data, no Revit, at 100 percent scale (96 DPI). Each is a DRAFT until Bader approves it. An approved PNG is copied into baseline/ under the same name, and from then the gate test R372_each_window_with_an_approved_baseline_renders_within_half_a_percent_of_its_pixels fails when more than 0.5 percent of its pixels differ. baseline/ is empty until then, and the test says it has nothing to compare.

## The PNG files

| File | Window | State |
|---|---|---|
| ribbon-icons.png | The 11 ribbon icons in the order of the ribbon, 32 px over 16 px: 1 to 4, Site Adaptation Rules, 5 to 9, Update from report | Draft |
| pane.png | The pane in use, filled like the NBH 012 column of 7.2 of docs/design/07-mock.md, its steps in the order of docs/design/13-workflow-changes.md | Draft |
| pane-first-open.png | The pane on first open, no settings file chosen, with the line of a past job | Draft |
| revit-ribbon.png | The Site Adaptation tab in Revit 2024, cut from the window by PrintWindow in Revit run 3 of 5a, 29 Oct 2026 | Taken in Revit, for Bader's check |
| revit-pane.png | The live pane page in Revit run 3 of 5a by RenderTargetBitmap at 96 DPI, first open state, the detached document of the run active | Taken in Revit, for Bader's check |

Bader's changes of 28 Oct 2026 (docs/design/13-workflow-changes.md) were rendered into all three in Lane B 5a PR 8, his UI answers QL10, QL14 and QL15 of 29 Oct 2026 in Lane B 5a run 3 (fixed items 12 to 14). Set up site comes in 5b (W1 of steps/lane-b.md), the check table, Review data and Site Adaptation Rules in 5c (W5), Update from report in 5f (W8). 5a builds none of these windows, so none has a PNG yet.

## Render them again

From the repo root, after the build of the gate:

```
dotnet build dotnet\SiteAdaptation.sln -c Release
dotnet\SiteAdaptation.UiShots\bin\Release\net48\SiteAdaptation.UiShots.exe steps\ui-shots
```

The command line and the picture tests call the one entry Shots.Render of dotnet/SiteAdaptation.UiShots/Shots.cs. Each PNG is kept in a palette of 256 colours of its own, so it stays within the 100 KB cap of R400 on steps/.

## Sample data

The project name 01101-PSN-IMC-R17B-TEST-NEW and the file and folder names are the mock's, on a made up folder C:\Sample. The size 2.91 MiB and the short hash 7061ac0d of Plot numbers are the mock's. Every other size and short hash is a sample value, a hash of the file name, not of a real file. The mock has no parameter sheet, so its name "Parameter sheet.xlsx" and its size are sample values too. The steps are the mock's state after its buttons 1 to 3 on NBH 012, put in the new order: Set up site, Read data and Review data done, Check with 1,247 issues, Floors and ramps next, Report ready since it needs 4 only (CHOSEN, the mock has no Review data, so Code marks it done). No coordinate and no level is shown.


## Control 4, the pane and the ribbon against docs/design/12-ui-spec.md

Code went through 7.1, 7.2 and the parts Rules, Font, Colours, Sizes and Icons of docs/design/12-ui-spec.md, and the parts Order, Ribbon, Pane and UI controls of docs/design/13-workflow-changes.md, line by line against dotnet/SiteAdaptation.AddIn/Pane/SitePanePage.xaml, Theme/Theme.xaml, App/RibbonLayout.cs and the PNGs. The test R372_the_pane_has_rows_32_inputs_30_with_radius_3_and_the_project_name_at_the_right_of_the_band reads the sizes off the laid out pane, R372_the_pane_shows_pre_setting_under_level_profile_shared_parameters_after_prototypes_and_the_past_job_line_on_first_open the rows of 28 Oct 2026, R372_step_rows_are_40_with_the_line_on_one_line_and_input_rows_show_the_file_name_only the answers QL14 and QL15.

### Fixed in the XAML

1. The project name stands at the right of the band, on the row of "Project", as 7.2 of docs/design/07-mock.md says.
2. Input and project rows are 32 high. They show the file name alone since QL15 (fixed item 14), the tooltip gives the full path and the size and short hash under it.
3. The inputs are drawn from Theme.xaml: the drop downs and the plot list box 30 high, radius 3, border SA.Border. The check box has radius 3 and the radio buttons are round. Both have the grey outline SA.BorderStrong of QL7, and SA.Accent when chosen, a colour the spec does not name for them (CHOSEN).
4. The marks take the colour words of the spec by QL7: the green check is SA.Ok, the amber mark SA.Warn. SA.Warn is #7A4B00 and reads brown, which is the value the spec gives. The shapes, a tick and a filled circle with a white sign, are Code's.
5. Start width. DockablePaneState of Revit 2024 offers DockPosition, MinimumWidth (docked, 200 by default, since 2020), MinimumHeight, FloatingRectangle (floating only) and TabBehind, and no start width for a docked pane (READ RevitAPIUI.xml of the Revit 2024 API package). The pane sets MinimumWidth to SA.Size.Pane, 380, the one lever there is.
6. Villa mode. The row stands directly under the Dry run checkbox, inside its SA.Panel box. Code takes that as "under the Dry run checkbox": outside the box it would stand under the box.
7. Review 1 of PR 90: the sections stand 16 apart, a value of the scale 4, 8, 12, 16, 24 of the spec, where SA.Pad.Section 16,16,16,4 gave 20 before.
8. Review 1 of PR 90: the Neighbourhood drop down and the Plot list box start at the x of the Level profile drop down, in the value column of the rows.
9. PR 8: the label "Shared parameters" is 116 wide in SA.Type.Body and was cut at the label column of 104 ("Shared paramete"). The label column is now 124, the widest label and a gap of 8, for every row, so the value columns stay in one line. The file names lose 20 px.
10. PR 8: Import from past job and Export are 224 wide with the Browse padding and the gap of 8, more than the value column of 200 at 380 wide. In the value column Export ran past the edge of the pane at 368 of 364. They now stand right-aligned at the edge of the Browse buttons, Import from past job 31 px after the end of "Pre-setting".
11. Review 1 of PR 92: the link Import pre-setting kept a local SA.Accent, so with no document it stayed blue and underlined while its command could not run and the button above it was grey. It now takes the style SA.Pane.Link of Theme.xaml: SA.Accent and underlined while it can run, SA.Wait, the text colour of the grey buttons, and no underline when it cannot (CHOSEN, the spec gives no look for a link). The test R372_the_link_import_pre_setting_is_accent_and_underlined_only_while_its_command_can_run holds both. The PNGs show a document, so they did not change, the same bytes rendered again.
12. QL10, Bader 29 Oct 2026: the hole of the Place villas pin is drawn white with no stroke (32 px only), so the pin shows a white hole where it showed a navy dot. In the pin head of ribbon-icons.png 0 of 49 pixels are navy, 8 before (RAN, a pixel count of the PNG before and after). The test R371_the_hole_of_the_place_villas_pin_is_white_with_no_stroke_at_32_px holds it.
13. QL14, Bader 29 Oct 2026: step rows are 40 high (SA.Size.StepRow), input and project rows stay 32. The line under a step name stays on one line with no wrap, cut with an ellipsis, its full text as tooltip. On first open the line of step 1 now reads "Coordinates, levels and types from..." beside the chip "Needs settings", where it wrapped before. The tests R371_step_rows_are_40_high_and_input_and_project_rows_stay_32 and R372_step_rows_are_40_with_the_line_on_one_line_and_input_rows_show_the_file_name_only hold it, 9 lines of one line in rows of 40 in both PNGs.
14. QL15, Bader 29 Oct 2026: input and project rows show the file name only. The tooltip gives each full path with its own size and short hash right under it, for a row of two files the two in turn. In pane.png 10 of 10 rows show the file name with no size or hash, 10 of 10 have them in the tooltip.

### Not changed, each with its reason

1. QL9, tabular figures. Libre Franklin has no tnum feature, so the digits are proportional, as in the sizes and the step chips. Bader's question.
2. QL10, answered 29 Oct 2026, fixed item 12.
3. These are offscreen renders, not the Revit screenshots the spec asks for each window. The Revit screenshots come with the run in Revit of PR 6.
4. The pane may still open wider than 380 in Revit, since MinimumWidth is a least width. UNKNOWN until the Revit run. The PNG is exactly 380 wide. QL13 of steps/lane-b.md.
5. QL15, answered 29 Oct 2026, fixed item 14. A long file name is still cut with an ellipsis at 380 wide.
6. The primary button matches every value the spec gives: 34 high, white text on SA.Accent, SemiBold, radius 3. The spec gives no width or place, so it stands left under the line, as wide as its text.
7. The Next step line. The spec asks one plain line on what the step does and whether dry run is on or off and gives no words. Code writes the step's plain line, then "Dry run is on." or "Dry run is off." (CHOSEN, docs/design/14-ui-labels.md).
8. While a large file is hashed, off the UI thread, its row shows the size alone, "<n> MiB" (CHOSEN).
9. The Settings row tooltip shows the full path only. docs/design/06-settings.md has it show the override file and the work folder too, from the config file, which 5a does not read (join J1 of 5c).
10. The Level profile and Pre-setting rows have no mark. The spec gives the marks to rows of a file, and a level profile is no file, nor is a pre-setting until R367 at 5c.
11. QL14, answered 29 Oct 2026, fixed item 13.
12. The label column is 124 wide, the mark column 24, the step mark column 38. The spec gives no widths (CHOSEN).
13. The chip colours by status: Done SA.Ok, issues SA.Warn, Ready SA.Info, Needs step and Needs settings SA.Wait. The spec names the chip texts, not their colours (CHOSEN).
14. A step that is Ready but not the next one keeps the grey outline, as 9 Report in pane.png. The spec names the outline of the next step and of a waiting one only.
15. The sections have no line between them. The spec gives none.
16. With no document open the controls, the buttons and the link Import pre-setting are grey and the text blocks keep their colour. 7.2 of the mock asks every button grey, nothing more.
17. The list of a drop down, when open, keeps the item look of WPF. It is not in a PNG.
18. A PNG is in a palette of 256 colours, so the edges of the text may differ a little from a screen.
19. The band and the Next step box stay at the top and do not scroll, only the sections under them do. The spec gives the order top to bottom and says nothing of scrolling (CHOSEN, so the next step stays in sight). On first open the line of a past job, 47 px high and 12 under the button, makes the box 59 px higher.
20. The number in a step mark is SA.Type.Label, 13 SemiBold, in SA.Surface on a filled mark, SA.Accent in the mark of the next step and SA.TextMuted in a waiting one. The spec gives the mark 26 and its fill and outline, not the type or colour of its number (CHOSEN).
21. The outline of a step mark is 2 wide, the lines of the boxes, the inputs and the Browse buttons 1. The spec gives no line widths (CHOSEN).
22. The chip text is SA.Type.Small, 12, with a padding of 8 across and 4 down. The spec gives the chip radius 12 and its texts, not its type or padding (CHOSEN).
23. Browse is drawn in the look the spec gives Cancel, SA.Text with an SA.BorderStrong border on SA.Surface, 28 high. The spec names no look for Browse (CHOSEN). Import from past job and Export take the same look.
24. The check box and the radio buttons are 16 across, the dot of a chosen radio button 8. The spec gives no size for them (CHOSEN).
25. The tooltips and the scroll bar keep the look of Windows. The spec gives none.
26. The Neighbourhood list is empty in Revit until button 2 reads the plot table, it lists the NBH codes of that table (7.2 of the mock). The 012 of pane.png is sample data.
27. On first open the box says "The tool fills the rest of this panel from it.", while in 5a choosing the settings file fills only the Settings row. The rest fills from the config file with join J1 of 5c.
28. The two dialogs of DLUS and sales are titled "DLUS schedule" and "Sales schedule", one for each workbook by its role (CHOSEN in review 1 of PR 90, docs/design/14-ui-labels.md).
29. Two inputs on stacked rows stand 2 px apart, the Neighbourhood drop down and the Plot list box, and 3 px the Rules Browse and the Level profile drop down, and the Level profile drop down and the Pre-setting buttons, off the spacing scale 4, 8, 12, 16, 24. The spec gives rows 32, inputs 30 and buttons 28, so no gap of the scale can stand between them while all hold. QL16 of steps/lane-b.md.
30. Icons. The accent strokes of 3 Review data and Update from report are given as "stroke SA.IconAccent 2.5" with no width at 16 px, where 4 Check gives "width 2.5 (3 at 16 px)". Code draws them 2.5 at both sizes, the 2.4 of the 16 px set being the default stroke only (CHOSEN, read so by ThemeGateTests).
31. Icons. A button with no number has its icon keyed by its label with no spaces, SA.Icon.SiteAdaptationRules and SA.Icon.UpdateFromReport (CHOSEN).
32. Ribbon. The labels Site Adaptation Rules and Update from report are long for a large button, and how Revit wraps them under the icon is UNKNOWN until a run in Revit. The ribbon is not in a PNG, only its icons.
33. Ribbon. Site Adaptation Rules and Update from report run through the flow as a numbered button with no body does and say "<label> is not built yet." in a TaskDialog (CHOSEN in docs/design/13-workflow-changes.md). Their windows come in 5c and 5f.
34. Pane. Import from past job, Export and the link Import pre-setting say "<label> is not built yet." in the TaskDialog of the ribbon buttons, raised through an external event as the box button is (CHOSEN in docs/design/13-workflow-changes.md, the logic joins R367 at 5c, W4). Not in a PNG.
35. Pane. The line of a past job on first open is a help line, 13 SA.TextMuted, 12 under the button, with the link Import pre-setting in SA.Accent, underlined, and SA.Wait with no underline while it cannot run (fixed item 11). The spec gives its words and what the link runs, not its look (CHOSEN). It is one line of three texts in the label list, the second the link.
36. Pane. The parameter sheet is the input parameterSheet of the settings JSON. docs/design/06-settings.md gives it no key (CHOSEN).
37. Review 1 of PR 92: the label list holds Bader's texts for the windows not built yet. Set up site has the Floor types text, Ramp slope, its default line and "From the parameter sheet: <n>" of docs/design/13-workflow-changes.md, and the pass line as "Pass limit <n> mm. <n> of <n> pass", since QL17 asks where 0.1 mm applies. The check table title is "4 Check, NBH <n>, <n> plots" (CHOSEN, the title of 7.4 of docs/design/07-mock.md under the new number). Both windows stay marked not built yet, so the text test holds their entries from 5b and 5c. The test R372_the_entries_of_windows_not_built_yet_hold_the_texts_of_the_workflow_changes holds them.
38. docs/addin-mock.md item 1 still names three panels and the old numbers. 7.1 of docs/design/12-ui-spec.md prevails (its Rules line 10), so the ribbon test reads 7.1. The mock changes at the fold in, item 11 of steps/lane-b.md.
39. The Steps help line "Run them in order. Each one waits for the one before." holds for every step but 9 Report, which waits for 4 Check only, so in pane.png 9 Report is Ready while 6 to 8 wait. QL19 of steps/lane-b.md asks Bader whether 9 needs 8 too.
40. pane-first-open.png is 85,172 bytes, 83 percent of the 100 KB cap of R400 on every file of steps/ outside steps/history/. A taller pane or one more row may pass the cap, then the render needs fewer colours or a split.

## The Revit shots against the drafts

Revit run 3 of 5a, 29 Oct 2026, Revit 2024 on the laptop at 125 percent scale (RAN, the screen 1920 device px and 1536 logical). The run files stay outside the repo in C:\Users\p003653k\Documents\SiteAdaptation\lane_b_5a_run3\run_outputs. The two PNGs here hash the same as those the run wrote (RAN sha256). The canvas itself is not in the repo, so each shot is held against its draft above and docs/design/12-ui-spec.md, and every difference is listed.

Ribbon, revit-ribbon.png against ribbon-icons.png and 7.1 of 12-ui-spec.md:

1. Same: the tab Site Adaptation active, the panels Setup, Rules, Build and Output in that order, the 11 buttons 1 to 4, Site Adaptation Rules, 5 to 9 and Update from report under them (RAN, the run read 0 panels and 0 buttons missing).
2. Same: each 32 px icon as drawn in ribbon-icons.png, the Place villas pin with its white hole (QL10).
3. Different: the labels sit on one line under each icon in Revit's ribbon font, the draft has no labels. Site Adaptation Rules and Update from report do not wrap, which answers item 32.
4. Not seen: the 16 px icons. Revit shows every button large on this tab, so the shot holds no 16 px icon, and whether they are sharp at 16 px is not measured in Revit.

Pane, revit-pane.png against pane-first-open.png:

5. Width: Revit opened the pane 765 WPF units wide, about 956 device px at 125 percent, against MinimumWidth 380 (QL13, RAN). The draft is 380. At 765 every help line holds on one or two lines and the Browse buttons stand at the right edge, far from the file names.
6. Height: the shot is 694 high and ends at the Prototypes row, with a scroll bar on the right, so Scope and Steps are below it. The step rows of 40 (QL14) are not in the Revit shot.
7. Scroll bar: the Revit pane shows the Windows scroll bar beside the Project part. The draft is rendered at full height and has none. 12-ui-spec.md gives no scroll bar look.
8. Start here box: flat SA.Info in Revit. In the draft, as Code saw it, it shows a fine dot pattern. Its cause is UNKNOWN, not measured.
9. Project bar: the Revit pane names the document active at the shot, SA5A-CENTRAL-TEST-NEW_detached, a detached copy that was never saved. The pane names it as a project, as the run found it takes the PathName of a detached document as a saved path (steps/lane-b.md, Where 5a stopped).
10. Same: the fonts, colours, the marks of the rows, Not chosen, 0 of 8 found, the Choose settings file button and the link Import pre-setting, as in the draft.
