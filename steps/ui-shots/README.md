# UI shots

Control 3 of docs/design/12-ui-spec.md, UI controls. Each PNG is a render of a window of the add-in, offscreen, with sample data, no Revit, at 100 percent scale (96 DPI). Each is a DRAFT until Bader approves it. An approved PNG is copied into baseline/ under the same name, and from then the gate test R372_each_window_with_an_approved_baseline_renders_within_half_a_percent_of_its_pixels fails when more than 0.5 percent of its pixels differ. baseline/ is empty until then, and the test says it has nothing to compare.

## The PNG files

| File | Window | State |
|---|---|---|
| ribbon-icons.png | The 11 ribbon icons in the order of the ribbon, 32 px over 16 px: 1 to 4, Site Adaptation Rules, 5 to 9, Update from report | Draft |
| pane.png | The pane in use, filled like the NBH 012 column of 7.2 of docs/design/07-mock.md, its steps in the order of docs/design/13-workflow-changes.md | Draft |
| pane-first-open.png | The pane on first open, no settings file chosen, with the line of a past job | Draft |
| setup-site.png | The Set up site window of button 1, an existing site file after the dry run, 7 of 7 check points pass, made up numbers, the content of the window at 1280 wide with no title bar of Windows | Draft |
| review-data.png | The Review data window of button 3, NBH 012 with 5 made up plots, the first three with a plan of button 4 and corners, two checked OK and one Wrong, plot 31 selected, the cluster confirm of R442 with plots 32 and 33 proposed as one cluster, 32 confirmed, 34 with no proposal, the content of the window at 1280 wide with no title bar of Windows | Draft |
| rules.png | The Site Adaptation Rules window of the ribbon button of that name, the 15 rules of the block manualRules of the rules profile, rule M06 selected, the content of the window at 1280 wide with no title bar of Windows | Draft |
| check-table.png | The check table window of button 4, NBH 012 with 5 made up plots and 8 rows of every kind, the first row selected, Save overrides on, Door level from with no civil file and 5 plots by road level plus slope (R443), the content of the window at 1280 wide with no title bar of Windows | Draft |
| update-from-report.png | The Update from report window of 5f, the level Excel and the report Excel of NBH 012 picked, 4 changes and 1 cell not editable, one change that breaks manual rule M08 unticked, made up numbers, the content of the window at 1280 wide with no title bar of Windows | Draft |
| new-project.png | The New project window of Lane C NEWPROJ-UI, the site drawing and the plot list found, road levels and the parameter sheet missing as on the board, sample counts 337 plots and 14 villa types, the content of the window at 1080 wide with no title bar of Windows | Draft (Q286) |
| how-it-works.png | The How it works page of Lane C NEWPROJ-UI, the 9 steps, the content of the window at 1080 wide with no title bar of Windows | Draft (Q286) |
| revit-ribbon.png | The Site Adaptation tab in Revit 2024, cut from the window by PrintWindow in Revit run 3 of 5a, 29 Oct 2026 | Taken in Revit, for Bader's check |
| revit-pane.png | The live pane page in Revit run 3 of 5a by RenderTargetBitmap at 96 DPI, first open state, the detached document of the run active | Taken in Revit, for Bader's check |

Bader's changes of 28 Oct 2026 (docs/design/13-workflow-changes.md) were rendered into all three in Lane B 5a PR 8, his UI answers Q135, Q139 and Q140 of 29 Oct 2026 in Lane B 5a run 3 (fixed items 12 to 14). Set up site is built in 5b (W1 of steps/lane-b.md), its PNG is setup-site.png and its lists are at the end of this file. Review data is built in 5c (W5), its PNG is review-data.png and its list is at the end of this file. Site Adaptation Rules is built in 5c (W3), its PNG is rules.png and its list is at the end of this file. The check table is built in 5c (W11), its PNG is check-table.png and its notes are at the end of this file. Update from report is built in 5f (W8), its PNG is update-from-report.png and its differences are at the end of this file.

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

Code went through 7.1, 7.2 and the parts Rules, Font, Colours, Sizes and Icons of docs/design/12-ui-spec.md, and the parts Order, Ribbon, Pane and UI controls of docs/design/13-workflow-changes.md, line by line against dotnet/SiteAdaptation.AddIn/Pane/SitePanePage.xaml, Theme/Theme.xaml, App/RibbonLayout.cs and the PNGs. The test R372_the_pane_has_rows_32_inputs_30_with_radius_3_and_the_project_name_at_the_right_of_the_band reads the sizes off the laid out pane, R372_the_pane_shows_pre_setting_under_level_profile_shared_parameters_after_prototypes_and_the_past_job_line_on_first_open the rows of 28 Oct 2026, R372_step_rows_are_40_with_the_line_on_one_line_and_input_rows_show_the_file_name_only the answers Q139 and Q140.

### Fixed in the XAML

1. The project name stands at the right of the band, on the row of "Project", as 7.2 of docs/design/07-mock.md says.
2. Input and project rows are 32 high. They show the file name alone since Q140 (fixed item 14), the tooltip gives the full path and the size and short hash under it.
3. The inputs are drawn from Theme.xaml: the drop downs and the plot list box 30 high, radius 3, border SA.Border. The check box has radius 3 and the radio buttons are round. Both have the grey outline SA.BorderStrong of Q132, and SA.Accent when chosen, a colour the spec does not name for them (CHOSEN).
4. The marks take the colour words of the spec by Q132: the green check is SA.Ok, the amber mark SA.Warn. SA.Warn is #7A4B00 and reads brown, which is the value the spec gives. The shapes, a tick and a filled circle with a white sign, are Code's.
5. Start width. DockablePaneState of Revit 2024 offers DockPosition, MinimumWidth (docked, 200 by default, since 2020), MinimumHeight, FloatingRectangle (floating only) and TabBehind, and no start width for a docked pane (READ RevitAPIUI.xml of the Revit 2024 API package). The pane sets MinimumWidth to SA.Size.Pane, 380, the one lever there is.
6. Villa mode. The row stands directly under the Dry run checkbox, inside its SA.Panel box. Code takes that as "under the Dry run checkbox": outside the box it would stand under the box.
7. Review 1 of PR 90: the sections stand 16 apart, a value of the scale 4, 8, 12, 16, 24 of the spec, where SA.Pad.Section 16,16,16,4 gave 20 before.
8. Review 1 of PR 90: the Neighbourhood drop down and the Plot list box start at the x of the Level profile drop down, in the value column of the rows.
9. PR 8: the label "Shared parameters" is 116 wide in SA.Type.Body and was cut at the label column of 104 ("Shared paramete"). The label column is now 124, the widest label and a gap of 8, for every row, so the value columns stay in one line. The file names lose 20 px.
10. PR 8: Import from past job and Export are 224 wide with the Browse padding and the gap of 8, more than the value column of 200 at 380 wide. In the value column Export ran past the edge of the pane at 368 of 364. They now stand right-aligned at the edge of the Browse buttons, Import from past job 31 px after the end of "Pre-setting".
11. Review 1 of PR 92: the link Import pre-setting kept a local SA.Accent, so with no document it stayed blue and underlined while its command could not run and the button above it was grey. It now takes the style SA.Pane.Link of Theme.xaml: SA.Accent and underlined while it can run, SA.Wait, the text colour of the grey buttons, and no underline when it cannot (CHOSEN, the spec gives no look for a link). The test R372_the_link_import_pre_setting_is_accent_and_underlined_only_while_its_command_can_run holds both. The PNGs show a document, so they did not change, the same bytes rendered again.
12. Q135, Bader 29 Oct 2026: the hole of the Place villas pin is drawn white with no stroke (32 px only), so the pin shows a white hole where it showed a navy dot. In the pin head of ribbon-icons.png 0 of 49 pixels are navy, 8 before (RAN, a pixel count of the PNG before and after). The test R371_the_hole_of_the_place_villas_pin_is_white_with_no_stroke_at_32_px holds it.
13. Q139, Bader 29 Oct 2026: step rows are 40 high (SA.Size.StepRow), input and project rows stay 32. The line under a step name stays on one line with no wrap, cut with an ellipsis, its full text as tooltip. On first open the line of step 1 now reads "Coordinates, levels and types from..." beside the chip "Needs settings", where it wrapped before. The tests R371_step_rows_are_40_high_and_input_and_project_rows_stay_32 and R372_step_rows_are_40_with_the_line_on_one_line_and_input_rows_show_the_file_name_only hold it, 9 lines of one line in rows of 40 in both PNGs.
14. Q140, Bader 29 Oct 2026: input and project rows show the file name only. The tooltip gives each full path with its own size and short hash right under it, for a row of two files the two in turn. In pane.png 10 of 10 rows show the file name with no size or hash, 10 of 10 have them in the tooltip.

### Not changed, each with its reason

1. Q134, tabular figures. Libre Franklin has no tnum feature, so the digits are proportional, as in the sizes and the step chips. Bader kept Libre Franklin with proportional digits (Q134).
2. Q135, answered 29 Oct 2026, fixed item 12.
3. These are offscreen renders, not the Revit screenshots the spec asks for each window. The Revit screenshots come with the run in Revit of PR 6.
4. The pane may still open wider than 380 in Revit, since MinimumWidth is a least width. Revit run 3 opened it 765 WPF units wide. The PNG is exactly 380 wide. Q138 of steps/questions-closed.md.
5. Q140, answered 29 Oct 2026, fixed item 14. A long file name is still cut with an ellipsis at 380 wide.
6. The primary button matches every value the spec gives: 34 high, white text on SA.Accent, SemiBold, radius 3. The spec gives no width or place, so it stands left under the line, as wide as its text.
7. The Next step line. The spec asks one plain line on what the step does and whether dry run is on or off and gives no words. Code writes the step's plain line, then "Dry run is on." or "Dry run is off." (CHOSEN, docs/design/14-ui-labels.md).
8. While a large file is hashed, off the UI thread, its row shows the size alone, "<n> MiB" (CHOSEN).
9. The Settings row tooltip shows the full path only. docs/design/06-settings.md has it show the override file and the work folder too, from the config file, which 5a does not read (join J1 of 5c).
10. The Level profile and Pre-setting rows have no mark. The spec gives the marks to rows of a file, and a level profile is no file, nor is a pre-setting until R367 at 5c.
11. Q139, answered 29 Oct 2026, fixed item 13.
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
29. Two inputs on stacked rows stand 2 px apart, the Neighbourhood drop down and the Plot list box, and 3 px the Rules Browse and the Level profile drop down, and the Level profile drop down and the Pre-setting buttons, off the spacing scale 4, 8, 12, 16, 24. The spec gives rows 32, inputs 30 and buttons 28, so no gap of the scale can stand between them while all hold. Q141 of steps/questions-closed.md.
30. Icons. The accent strokes of 3 Review data and Update from report are given as "stroke SA.IconAccent 2.5" with no width at 16 px, where 4 Check gives "width 2.5 (3 at 16 px)". Code draws them 2.5 at both sizes, the 2.4 of the 16 px set being the default stroke only (CHOSEN, read so by ThemeGateTests).
31. Icons. A button with no number has its icon keyed by its label with no spaces, SA.Icon.SiteAdaptationRules and SA.Icon.UpdateFromReport (CHOSEN).
32. Ribbon. The labels Site Adaptation Rules and Update from report are long for a large button, and how Revit wraps them under the icon is UNKNOWN until a run in Revit. The ribbon is not in a PNG, only its icons.
33. Ribbon. Site Adaptation Rules and Update from report run through the flow as a numbered button with no body does and say "<label> is not built yet." in a TaskDialog (CHOSEN in docs/design/13-workflow-changes.md). Their windows come in 5c and 5f.
34. Pane. Import from past job, Export and the link Import pre-setting say "<label> is not built yet." in the TaskDialog of the ribbon buttons, raised through an external event as the box button is (CHOSEN in docs/design/13-workflow-changes.md, the logic joins R367 at 5c, W4). Not in a PNG.
35. Pane. The line of a past job on first open is a help line, 13 SA.TextMuted, 12 under the button, with the link Import pre-setting in SA.Accent, underlined, and SA.Wait with no underline while it cannot run (fixed item 11). The spec gives its words and what the link runs, not its look (CHOSEN). It is one line of three texts in the label list, the second the link.
36. Pane. The parameter sheet is the input parameterSheet of the settings JSON. docs/design/06-settings.md gives it no key (CHOSEN).
37. Review 1 of PR 92: the label list holds Bader's texts for the windows not built yet. Set up site has the Floor types text, Ramp slope, its default line and "From the parameter sheet: <n>" of docs/design/13-workflow-changes.md, and the pass line as "Pass limit <n> mm. <n> of <n> pass", since Q142 asks where 0.1 mm applies. The check table title is "4 Check, NBH <n>, <n> plots" (CHOSEN, the title of 7.4 of docs/design/07-mock.md under the new number). Both windows stay marked not built yet, so the text test holds their entries from 5b and 5c. The test R372_the_entries_of_windows_not_built_yet_hold_the_texts_of_the_workflow_changes holds them.
38. docs/addin-mock.md item 1 still names three panels and the old numbers. 7.1 of docs/design/12-ui-spec.md prevails (its Rules line 10), so the ribbon test reads 7.1. The mock changes at the fold in of Round FOLD, item 11 of the Fold in list in steps/history/lane-b-2026-10-29.md.
39. The Steps help line "Run them in order. Each one waits for the one before." holds for every step but 9 Report, which waits for 4 Check only, so in pane.png 9 Report is Ready while 6 to 8 wait. Bader kept 9 needing 4 only, Q144 of steps/questions-closed.md.
40. pane-first-open.png is 85,172 bytes, 83 percent of the 100 KB cap of R400 on every file of steps/ outside steps/history/. A taller pane or one more row may pass the cap, then the render needs fewer colours or a split.

## The Revit shots against the drafts

Revit run 3 of 5a, 29 Oct 2026, Revit 2024 on the laptop at 125 percent scale (RAN in the lead session, not in a run file: Screen.PrimaryScreen.Bounds gave 1536 wide in a process not DPI aware, and the Revit window of the run is 1938 px wide with a border of 9 each side, 1920 device px, READ the shots of testhost_SA5A_addinrun.json). The run files stay outside the repo in C:\Users\p003653k\Documents\SiteAdaptation\lane_b_5a_run3\run_outputs. The two PNGs here hash the same as those the run wrote (RAN sha256). The canvas itself is not in the repo, so each shot is held against its draft above and docs/design/12-ui-spec.md, and every difference is listed.

Ribbon, revit-ribbon.png against ribbon-icons.png and 7.1 of 12-ui-spec.md:

1. Different: the title of the tab Site Adaptation does not show in the tab strip. The strip runs from File to Bird Tools and ends in the overflow mark, so at 1536 logical px a user reaches the tab through that mark. The tab is the active one, its panels show, and the run read it found, visible to the API and active, but the judge does not check that its title shows (READ, AddinRun.cs, ActiveTab only). Q146 of steps/questions-open.md.
1a. Same: the panels Setup, Rules, Build and Output in that order, the 11 buttons 1 to 4, Site Adaptation Rules, 5 to 9 and Update from report under them (RAN, the run read 0 panels and 0 buttons missing).
2. Same: each 32 px icon as drawn in ribbon-icons.png, the Place villas pin with its white hole (Q135).
3. Different: the labels sit on one line under each icon in Revit's ribbon font, the draft has no labels. Site Adaptation Rules and Update from report do not wrap, which answers item 32.
4. Not seen: the 16 px icons. Revit shows every button large on this tab, so the shot holds no 16 px icon, and whether they are sharp at 16 px is not measured in Revit.

Pane, revit-pane.png against pane-first-open.png:

5. Width: Revit opened the pane 765 WPF units wide, about 956 device px at 125 percent, against MinimumWidth 380 (Q138, RAN). The draft is 380. At 765 every help line holds on one or two lines and the Browse buttons stand at the right edge, far from the file names.
6. Height: the shot is 694 high and ends at the Prototypes row, with a scroll bar on the right, so Scope and Steps are below it. The step rows of 40 (Q139) are not in the Revit shot.
7. Scroll bar: the Revit pane shows the Windows scroll bar beside the Project part. The draft is rendered at full height and has none. 12-ui-spec.md gives no scroll bar look.
8. Start here box: a flat ground of SA.Guide in Revit, SA.Info being the colour of its label only. In the draft, as Code saw it, it shows a fine dot pattern. Its cause is UNKNOWN, not measured.
9. Project bar: the Revit pane names the document active at the shot, SA5A-CENTRAL-TEST-NEW_detached, a detached copy that was never saved. The pane names it as a project, as the run found it takes the PathName of a detached document as a saved path (steps/history/lane-b-2026-10-29.md, Where 5a stopped).
10. Same: the fonts, colours, the marks of the rows, Not chosen, 0 of 8 found, the Choose settings file button and the link Import pre-setting, as in the draft.
11. Step rows: each step row is 40 high, as Q139 says, inside the 4 above and below that SA.Gap.Row gave the step rows of 32 before, so the steps stand 48 apart where they stood 40 (CHOSEN, the gap kept as it was). Input and project rows have no such gap and stand 32 apart. Q139 named the row, not the pitch.

## Set up site, setup-site.png against 7.3

The canvas is not in the repo, so the window is held against 7.3 of docs/design/07-mock.md, 7.3 of docs/design/12-ui-spec.md and the part Set up site of docs/design/13-workflow-changes.md. The sample is an existing site file after the dry run with made up numbers only (E 1000.000 style values, no real coordinate and no level), 7 of 7 pass, Levels and Wall types marked Will change. The file is 76,865 bytes, under the 100 KB cap of R400. It is rendered by Shots.Render("setup-site") of dotnet/SiteAdaptation.UiShots/Shots.cs. The 18 tests of dotnet/SiteAdaptation.Tests/SetUpSiteTests.cs read the window off the laid out window and drive its session.

### Every size Code chose

The spec gives no size for these, all are keys of Theme.xaml (CHOSEN).

1. The window is 1280 wide (SA.Size.DialogWidth) and at most 800 high (SA.Size.DialogMaxHeight), with a scroll bar of the body when it is taller. The picture is 1280 by 785.
2. The left part is 640 wide (SA.Grid.SetUpLeft), the right part takes the rest.
3. The label column of the left rows is 176 (SA.Grid.SetUpLabel), wide enough for Floor types and wall types on one line.
4. The check point table has the Point column 128 and the Diff column 80 (SA.Grid.CheckPoint, SA.Grid.CheckDiff), CAD and Revit share the rest.
5. The Ramp slope drop down is 96 wide (SA.Size.RampBox), 30 high as every input.
6. A row of a table is at least 32 high (SA.Size.Row) with a padding of 8 across and 4 down (SA.Pad.Cell), a line of SA.Border under it. A row with two lines of text is higher.
7. The side of the body is 16 (SA.Pad.Side), the foot has a padding of 16 across and 12 down (SA.Pad.Foot), the buttons of the foot stand 8 apart.
8. The badge is a chip of radius 12 on SA.BandAccent with SA.Band text in SA.Type.Small, 16 right of the title.
9. The signs line is SA.Type.Small in SA.BandAccent and the line "check points pass" SA.Type.Small in SA.BandText. The spec gives the colour of the signs line only.
10. The Dry run button is SA.Button.DryRun of Theme.xaml: SA.Heading text and border on the base button of 28 high. Cancel is the base button, Apply the primary button of 34 high.

### Known differences from 7.3 of the mock and the spec

1. The check point table is rows of a Grid, not a DataGrid as the mock says, so its look comes from Theme.xaml alone (CHOSEN). The header row is SA.Type.Label in SA.TextMuted.
2. Diff shows the number with two decimals and no unit, "0.00", where the mock shows "0.00 mm". The label list has no text for the unit and the pass line names it, "Pass limit 0.1 mm" (CHOSEN).
3. A point that fails shows its Diff in SA.Error on SA.ErrorGround. The spec gives the look of a passing point only (CHOSEN).
4. The pass limit shows as the view gives it, 0.1 in the sample. Where 0.1 mm applies is Q142 and Common's side.
5. The word Browse of the Shared parameters row is SetUpSiteText.Browse of the window, the entry Browse of the Set up site list of docs/design/14-ui-labels.md since the merge of 5b.
6. The Set up site list of docs/design/14-ui-labels.md is a built window since the merge of 5b. Both halves of the text test hold for it, every text of the files of SetUpSite is in the list and every entry is shown by them.
7. The Floor types and Shared parameters rows show the values Common gives, the text of 13-workflow-changes.md and "From the parameter sheet: <sheet>, <n> parameters", with Browse under the sheet line (review 1 of 5b).
8. Common gives the row Ramp slope in every view, at its place between Floor types and Wall types, and the window puts the drop down in it.
9. Apply is off while the view holds a stop or before the dry run. It goes off again when the slope is changed after a dry run, until the next dry run, since the dry run was made on another slope (CHOSEN).
10. A stop, or the reason of a callback that threw, shows in a box on SA.ErrorGround above the foot in SA.Error. The spec gives no look for it (CHOSEN).
11. The note "On an existing site file" shows on both badges, as the mock says, in a box of SA.Guide with a line of SA.GuideBorder, its title in SA.Type.Label and SA.Info.
12. The picture is the content of the window with no title bar of Windows. A window that is not shown has no visual tree, so the render takes its content. The Revit screenshot the spec asks for is not taken, it needs a run in Revit (UNKNOWN).
13. The drop down list when open keeps the item look of WPF. It is not in the PNG.
14. The file dialog of Browse is the one of Windows with no filter and no title of ours, as the pane has it.
15. The PNG is in a palette of 256 colours, so the SA.Guide box shows a fine dot pattern, as item 8 of the Revit differences of the pane says.
16. Closing the window with the cross is Cancel.
17. The sign texts, the sheet name and every value of the picture are sample words and numbers, the real ones come from Common.

### Control 4, the UI checklist of 12-ui-spec.md against SetUpSiteWindow.xaml

Each line of the spec, held or not, from the XAML, Theme.xaml and the tests of SetUpSiteTests.cs.

Rules
1. One theme file holds every colour, font, size and icon, and the gate test fails on a literal elsewhere: held. SetUpSiteWindow.xaml and the C# files of SetUpSite hold no colour literal and no FontSize, and ThemeGateTests passes with them in the folder.
2. Labels and order follow 7.1 to 7.4 and the changes of 28 Oct 2026: held, each entry of the list under Set up site is shown by a file of the window and each text of those files is in the list, Browse among them.
3. Nothing smaller than 12: held, the test reads the font size of every text of the laid out window, the smallest is SA.Type.Small 12.
4. Every rule shown to a user shows a plain name with its id as a tooltip: not applicable, the window shows no rule.
5. A change to the look is a question for Bader: held, nothing in the spec was changed, each choice is listed above.
6. Each window gets a Revit screenshot: not held, UNKNOWN until a run in Revit.

UI controls
7. Control 1, the theme test: held, see line 1.
8. Control 2, the text test: held, see line 2.
9. Control 3, the picture test: held as a draft. The window renders offscreen at 96 DPI to setup-site.png, 77,246 bytes. There is no baseline yet, so it compares nothing until Bader approves it.
10. Control 4, this list: this section.
11. Control 5, the approval form: it goes in the final message of the session of 5b, not here.

Font
12. Libre Franklin from the resources, Franklin Gothic Book then Segoe UI after it: held, every TextBlock takes SA.Type.* of Theme.xaml and the buttons and the drop down take SA.Font.
13. Title 20 SemiBold: held, "1 Set up site". Section 15 SemiBold: held, the three section titles. Body 13: held, the rows. Small 12: held, the badge, the signs line, the tag Will change, the caption "check points pass". Count 28 SemiBold: held, "<n> of <n>". Label 13 SemiBold: held, the header of the check table and the title of the guide box. Next 18, Project 16 and Step 14 are not used.
14. Tabular figures: not held, Libre Franklin has no tnum, proportional digits stay by Q134.

Colours
15. The band SA.Band with the title white, the badge on SA.BandAccent, the line in SA.BandText, the signs in SA.BandAccent: held, the test of the band reads the band, the badge and the sizes.
16. Rows that will change on SA.Warn ground with SA.Warn text and "Will change": held, test R373_the_rows_that_differ_stand_on_warn_ground_with_will_change_and_the_others_do_not counts 2 of 2 in the sample.
17. Diff in SA.Ok on its ground when it passes: held, test R373_the_diff_cell_stands_in_ok_when_the_point_passes_and_the_check_table_has_one_row_per_point counts 6 of 6 on SA.OkGround and 1 failing in SA.Error.
18. The existing site file note in SA.Guide: held. The foot on SA.Panel: held.
19. Amber SA.Warn, grey outline SA.BorderStrong, white SA.Surface by Q132: held, the buttons take SA.BorderStrong and the ground is SA.Surface.

Sizes
20. Spacing 4, 8, 12, 16, 24 only: held for every margin and padding of the window (4, 8, 12, 16). The widths of the columns are not spacing and are listed as chosen sizes.
21. Main button 34, other buttons 28 to 32: held, Apply 34, Cancel and Dry run 28, Browse 28, read by test.
22. Input and project rows 32, inputs 30: the drop down is 30, held by test. The rows are at least 32 and grow to two lines where a text wraps, as the Shared parameters and Ramp slope rows do, so not exactly 32 there (CHOSEN).
23. Radius 3 on controls, 12 on chips: held, the buttons, the drop down, the Diff cell and the guide box 3, the badge 12.
24. Primary white text on SA.Accent SemiBold, Dry run SA.Heading text with SA.Heading border, Cancel SA.Text with SA.BorderStrong border: held, the styles of Theme.xaml, read by test.
25. Pane starts 380 wide: not applicable to this window.

7.3
26. The band with "1 Set up site", the badge, the line, the signs and "<n> of <n>" over "check points pass": held.
27. Left the two tables as label and value rows with Will change, right the check point table, the pass line and the note, the foot on SA.Panel with Cancel, Dry run and Apply: held.
28. The Floor types text, the row Ramp slope with its drop down 0.5%, 1%, 2%, then 3% to 15% and default 0.5% and its line, the Shared parameters row with its sheet and count in one line and Browse: held, tests R373_the_rows_stand_in_the_order_of_the_mock_with_the_three_rows_of_28_oct_2026, R373_the_ramp_slope_choices_are_05_1_2_then_3_to_15_percent_and_the_default_is_05 and R373_the_ramp_slope_row_holds_a_30_high_drop_down_of_16_choices_and_the_sheet_row_holds_browse.
29. Apply off while the view holds stops or before the dry run: held, tests R373_apply_is_off_before_the_dry_run_and_while_the_view_holds_a_stop_and_on_after_the_dry_run and R373_the_buttons_of_the_window_drive_the_session_and_apply_is_off_before_the_dry_run.

## Review data, review-data.png against 13-workflow-changes.md

The window of button 3, built in Lane B 5c, rule R406. Its texts, columns, chips and side box are those of Review data (5c) in docs/design/13-workflow-changes.md, held by test R406_the_texts_of_the_window_are_those_of_the_workflow_changes_word_for_word. Its look is 7.3 and 7.4 of docs/design/12-ui-spec.md: the band on SA.Band with the title 20 SemiBold, the How to use line on SA.Guide, the chips with the active one filled SA.Band, the selected row on SA.Selected with a 3 px SA.Accent mark, the side box 340 wide on SA.Panel.

### Sample data

Made up plots of a made up NBH 012, no coordinate or level of a real model. The corners are a 20 by 25 square at 5000, 6000 of shared coordinates and the survey point of the sample site file puts the model coordinates at 100, 200. Plots 34 and 35 have no plan of button 4, so their cluster and road read "not read yet", and plot 35 has no villa type either.

### Every size Code chose (CHOSEN)

1. The window is 1280 wide and at most 800 high as the Set up site window.
2. The table columns are 88, 96, 112, 96, 112, 88 and 128, Team check takes the rest. The side box label column is 120, the corner name column 72. The Find plot box is 200 wide and the table at most 480 high, then it scrolls. These are keys of Theme.xaml.
3. The table sits left and the side box right, the band and the How to use line over both. The window has no foot, Save review is the primary button of the band as 12-ui-spec.md 7.4 puts Save overrides.
4. The Shared coordinates of a corner are a column of the corners table after Y, its heading on the line of Corner, X and Y (fixed in the UI review of 5c). A long pair wraps inside its column. X and Y are the model coordinates by the survey point of site.json, and show "not read yet" while the site file gives none.
5. A message of Zoom to plot, Export to Excel or Save review shows in a band on SA.InfoGround under the How to use line.

### Known differences

1. Mirror is the mirror of the footprint block of the plot table until button 4 gives the one of its plan. Cluster, Front edge, Road at front and the corners come only from the plan of button 4 and read "not read yet" before it.
2. Every row names the manual rule M01, as the tooltip of its Plot No and as "Manual rule M01" in the side box. No other rule of M01 to M15 reads what a row shows (CHOSEN).
3. Export to Excel says it is not built yet, no xlsx writer is reachable from the add-in (R350). Zoom to plot says the same until the Revit side sets its delegate.
4. Lane C SCAN-UI task 5, R442, a DRAFT with no board: the column Confirmed (68) after Cluster with the tick of SA.Pane.Check at the height of the row, the button Confirm all proposed in the band before Save review, grey with nothing to confirm, and a second help line under How to use while the plan proposes a cluster. No existing key changed, Team check, the column that takes the rest, gives up the 68, and the heading Confirmed touches Area m2. The sample proposals and reasons are made up, plot 32 shows "32+33 mirror no" ticked, 33 "32+33 mirror yes" not ticked, 34 "No proposal".

## Site Adaptation Rules, rules.png against 13-workflow-changes.md

The window of the ribbon button Site Adaptation Rules, built in Lane B 5c, rule R403. Its title, band line, buttons, chips, columns and side box are those of Site Adaptation Rules (5c) in docs/design/13-workflow-changes.md, held by test R403_the_texts_of_the_window_are_those_of_the_workflow_changes_word_for_word. Its look is 7.3 and 7.4 of docs/design/12-ui-spec.md as the Review data window has it.

### Sample data

The block manualRules of rules/roshn-makkah.json as it is in the repo, texts only, no coordinate or level. The past job column is the one of the block (M04 and M08 match, M06 differs, the rest not measured, shown as not read yet).

### Every size Code chose (CHOSEN)

1. The window is 1280 wide and at most 800 high as the Review data window.
2. The table columns are ID 64, Manual 112, Checked in 176 and Past job 200, Rule takes the rest. They are keys of Theme.xaml. The Rule and Past job texts wrap, the rows grow to fit.
3. The chips wrap onto a second line if the groups do not fit one line. The window has no foot, both buttons are in the band.
4. A message of Open the manual PDF or Export to Excel shows in a band on SA.InfoGround under the title band.
5. A past job that differs shows in SA.Warn text.

### Known differences

1. What the tool does reads "not read yet" for every rule, the block holds no text of it and the canvas gives none (question to Bader).
2. The header of the table does not move for the scroll bar, so its columns sit about 17 px left of the rows while the list scrolls.
3. Export to Excel says it is not built yet, no xlsx writer is reachable from the add-in (R350). Open the manual PDF opens the file by the shell when it is in a folder of the profile and else names the folders it looked in.
4. The past job column carries the column of docs/design/15-manual-rules.md until the override list or the build gives a measured text.

### Check table, Lane B 5c (W11)

- Columns Plot, NBH, Unit, Check, Tool value (rule), Past job, Rule, Manual rule and Use, the widths in Theme.xaml under SA.Grid.Check*. Manual rule is a column of its own, CHOSEN, as every check row names its manual rule ID (docs/design/13-workflow-changes.md). A row with no rule of the profile shows it empty. The Rule column shows the plain name of the coded rule with its rule id as the tooltip (12-ui-spec.md line 11), the Manual rule column has the manual rule text as its tooltip.
- The window is modal like Review data, since ICheckTableDialog returns the answer to the worker callback. 7.4 of the mock asks for a modeless window, so this differs (CHOSEN).
- Zoom to plot and Export to Excel are grey until the Revit side gives them a callback, Save overrides is grey in a dry run and when no callback is given.
- The one plain line on the rule with its share and strong or weak is not shown, the rows carry no share yet. The line Why shows the why of the row instead (CHOSEN).
- The chip Assumed has the dashed amber border of 7.4, it lists no row (item 8 of 7.7).
- The picture is a render at 100 percent of made up plots 31 to 35, the units VL4, DT1, VL6, C10 and TH2, no coordinate and no level.
## Update from report, differences Code knows (5f)

- 12-ui-spec.md gives a new window a How to use line on SA.Guide, a side box 340 wide on SA.Panel and a selected row with the 3 px SA.Accent mark. 13-workflow-changes.md, part Update from report, gives no text for any of the three, so the window has none of them (Q179).
- Rule check shows the plain name of each check with its rule id as tooltip (12-ui-spec.md line 11), the 9 names CHOSEN in UpdateText.RuleNames (Q176). A manual rule shows its id such as M08 until 5c gives it a plain name (Q177).
- The chips show their count after the name, such as FFL 1, as the tiles do, where 13-workflow-changes.md gives the names only (CHOSEN).

## Lane C NEWPROJ-UI, the differences from the boards of 31 Oct 2026

The boards are docs/design/boards/. pane.png and pane-first-open.png were rendered again in this round. No Revit screenshot was taken in this round: no Revit window ran and the add-in was not installed, so each picture is the offscreen render only (UNKNOWN in Revit).

New project (new-project.png, board NewProject.dc.html):
1. Height follows the content, about 776, capped at 800 by SA.Size.DialogMaxHeight. The board is 860.
2. Band and body padding 16 (SA.Pad.Side), board 20 and 24, and 18 and 24.
3. Row lines SA.Border, placeholder text SA.TextMuted, stop box border SA.Border. The board uses #EEF1F5, #6B7682 and #E8D08A, which are not tokens.
4. Browse and Get template are SA.Button.Base, 28 high, 13 SA.Text. The board has 26 high, 12 Heading. Foot buttons 13, board 14 with min width 96.
5. Status marks are the pane's 18 px marks, the Missing one a filled amber disc. The board has a 14 px tick and a bare "!".
6. The ready count leaves out Villa levels, which waits on Lane A: "2 of 4" where the board's sample says "3 of 5". The row shows Comes later.
7. Survey point says Comes with the drawing scan. Change and View are grey until the scanner of a later Lane B round. The site row has no "Layers found" line yet. Villa types shows "<n> types" without "<n> without a level".
8. The Project name box is read only, the document title (Q282).
9. A long file name is cut at 200 with an ellipsis, its full path in the tooltip.

How it works (how-it-works.png, board HowItWorks.dc.html):
1. Height follows the content, about 660, board 860.
2. Padding 16, board 20 and 24. Row lines SA.Border, board #EEF1F5.
3. Open New project is grey when no project is open.

Pane (pane.png and pane-first-open.png, boards Pane.dc.html and PaneFirst.dc.html):
1. The Villa mode row stays under the dry run box (Q284). The board drops it.
2. The Next step box keeps its name with no number and its short line ("Generic, yard and ramp floors. Dry run is on."), its name in SA.Text and the line in SA.TextMuted. The board shows "5 Floors and ramps", a longer sentence, Heading and #1B1F24.
3. The buttons of Help stand at the right edge across the label and value columns, as those of Pre-setting do. The label column is 124 (SA.Grid.Label), the board's 96.
4. No 1 px line between sections. The boards draw one in #DCE2E9, not a token.
5. The Rules row shows the project field of the rules file, "ROSHN Makkah Phase 1" in the real file (Q285). The sample shows "ROSHN Makkah".
6. First open: steps 1 to 4 keep their line under the name and a grey chip. The board shows the name and plain "Needs step n". The sample document has a saved path, so the band shows its name, where the board says "not saved". The Setup mark is the filled amber disc, the board an amber ring.

## Lane C SCAN-UI, the differences from the board ScanResults.dc.html

The board is docs/design/boards/ScanResults.dc.html. scan-results.png is the offscreen render with made up file names, layers and counts and no coordinate or level, 4 files, 296 plots, 30 conflicts, the villa type on Pick as on the board. No Revit window ran in this round, so the picture is the offscreen render only (UNKNOWN in Revit).

What the tool read (scan-results.png):
1. Height follows the content, 721 in the picture, capped at 800 by SA.Size.DialogMaxHeight. The board is 860. The width is 1400 as the board.
2. Band and body padding 16 (SA.Pad.Side), board 20 and 24, and 18 and 24. The gap of the counts is 24, board 28.
3. Row lines SA.Border, the yellow box border SA.Border, file names SA.Text. The board uses #EEF1F5, #E8D08A and a grey placeholder, which are not tokens.
4. The conflicts count in the band is SA.WarnGround, the board's #FFD580 is not a token and SA.Warn is unreadable on the band.
5. The dropdowns are SA.Pane.Combo, 30 high with the arrow of the theme, board 28. Show all and Use the proposed for all are SA.Button.Base, 28 high 13 SA.Text, board 26 high 12 Heading. Foot buttons 13, board 14 with min width 96. Export to Excel has the SA.Heading border.
6. Chips pad 8 and 4 (SA.Pad.Chip), board 3 and 9. The Pick chip sits on the amber row, so only its text shows, as on the board.
7. The table holds the four values the scanner reads: Plot outline, Plot number, Villa block and Villa type. The board's Mirror, Survey point and Road level have no scan row in scan.json yet. Villa block is added, the board has no row for it.
8. The source of a villa type read from blocks says "blocks on <layer>", the board says "blocks in plots". A count of 1 reads "1 files" and "1 layers" as the templates of the board.
9. A value on Pick shows an empty dropdown. Its yellow box lists one box for each Pick value and goes when the user picks a source. The line "What to do" of a value other than the villa type is CHOSEN, the board gives the villa type only.
10. The Conflicts section and its title are hidden when scan.json has no conflict. Show all is hidden once all are shown or when there are 20 or fewer. The board draws both always.
11. The conflicts list scrolls inside SA.Size.ScanList (300), 20 rows first and all after Show all, virtualized. The board clips at the window.
12. The foot line becomes "Saved <name>." after Export to Excel, or the reason when it is refused. The board has the one line.
13. Use these picks is always on. A value still on Pick when it is pressed stays unsaved, so the next step sees it as unclear.

## The Revit shots of 6a against the boards

Taken in Revit 2024 by the buttons job of R384 with shots true, run LB6A on NBH 012 in Masses, 31 Oct 2026, 10 PNGs, none blank (RAN). The PNGs stay under the work root, addin_run\LB6A_20261031-232100\shots, since they show real file names and values. The references are the boards of docs/design/boards and the offscreen renders of this folder. Not drawn: Update from report, it stopped before its window (Q278), and the confirm windows, which are Revit TaskDialogs.

- Pane: the pane is 765 px wide in Revit, the board 380, so the file names show whole. The shot holds only the visible part of the pane, so Scope and Steps are not in it.
- Pane: the Rules row reads "ROSHN Makkah Phase 1", the board "ROSHN Makkah".
- Pane: after the run the Next step box shows 3 Review data, since the host cancels 3 and step 3 is never Done.
- New project: Plots found and Villa types read "Not read yet. Run 2 Read data." after button 2 ran, the drawing facts come from the scanner hook, join item C1, not built. The board shows 337 plots and 14 types.
- New project: the Parameter sheet row shows testhost_KC_parameters.json, the test input of the run, a .json name in a window.
- New project: 4 of 4 files ready and no stop box, the inputs of the run are all there, as expected.
- Ribbon: the four panels Setup, Rules, Build, Output and the 11 buttons with their 32 px icons as the board. The title of the tab is not seen, it sits past the overflow mark at 1922 px (Q146).
- Set up site, Review data, Check table, Rules and How it works were drawn at their design sizes, 1280 and 1080 px wide, not compared pixel by pixel here.

## Lane C SCAN-UI task 6, Door level from in 4 Check (R443)

No board, so the row follows the style of the check table window and is a DRAFT. check-table.png was rendered again with made up counts, a config with no civil file and 5 plots by road level plus slope, no coordinate, level or file name. No Revit window ran, so the picture is the offscreen render only (UNKNOWN in Revit).

1. The row stands above the chips, the label in HeadText of the window (the style of the table header), the dropdown SA.Pane.Combo as the Use column, its items by the style UseItem, so the grey civil item takes SA.Wait with the help line as its tooltip.
2. The dropdown is SA.Size.SideBox (340) wide, CHOSEN so the longest label fits with no new key in Theme.xaml.
3. The help line is SA.Pane.Help, shown only while the civil item is grey. A gap SA.Gap.Bottom16 parts the row from the chips.
4. A pick goes to DoorLevelSource of the pane settings at once, not with Save overrides, since it is a setting of the project and no override. The build of 4 Check still takes designBasis.doorLevel.source of config until Lane A reads the pick.
