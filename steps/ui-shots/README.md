# UI shots

Control 3 of docs/design/12-ui-spec.md, UI controls. Each PNG is a render of a window of the add-in, offscreen, with sample data, no Revit, at 100 percent scale (96 DPI). Each is a DRAFT until Bader approves it. An approved PNG is copied into baseline/ under the same name, and from then the gate test R372_each_window_with_an_approved_baseline_renders_within_half_a_percent_of_its_pixels fails when more than 0.5 percent of its pixels differ. baseline/ is empty until then, and the test says it has nothing to compare.

## The PNG files

| File | Window | State |
|---|---|---|
| ribbon-icons.png | The 11 ribbon icons in the order of the ribbon, 32 px over 16 px: 1 to 4, Site Adaptation Rules, 5 to 9, Update from report | Draft |
| pane.png | The pane in use, filled like the NBH 012 column of 7.2 of docs/design/07-mock.md, its steps in the order of docs/design/13-workflow-changes.md | Draft |
| pane-first-open.png | The pane on first open, no settings file chosen, with the line of a past job | Draft |

Bader's changes of 28 Oct 2026 (docs/design/13-workflow-changes.md) were rendered into all three in Lane B 5a PR 8. Set up site comes in 5b (W1 of steps/lane-b.md), the check table, Review data and Site Adaptation Rules in 5c (W5), Update from report in 5f (W8). 5a builds none of these windows, so none has a PNG yet.

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

Code went through 7.1, 7.2 and the parts Rules, Font, Colours, Sizes and Icons of docs/design/12-ui-spec.md, and the parts Order, Ribbon, Pane and UI controls of docs/design/13-workflow-changes.md, line by line against dotnet/SiteAdaptation.AddIn/Pane/SitePanePage.xaml, Theme/Theme.xaml, App/RibbonLayout.cs and the PNGs. The test R372_the_pane_has_rows_32_inputs_30_with_radius_3_and_the_project_name_at_the_right_of_the_band reads the sizes off the laid out pane, R372_the_pane_shows_pre_setting_under_level_profile_shared_parameters_after_prototypes_and_the_past_job_line_on_first_open the rows of 28 Oct 2026.

### Fixed in the XAML

1. The project name stands at the right of the band, on the row of "Project", as 7.2 of docs/design/07-mock.md says.
2. Input and project rows are 32 high. The size and the short hash are a smaller grey run (SA.Type.Small, SA.TextMuted) on the line of the file name, and the tooltip gives the full path.
3. The inputs are drawn from Theme.xaml: the drop downs and the plot list box 30 high, radius 3, border SA.Border. The check box has radius 3 and the radio buttons are round. Both have the grey outline SA.BorderStrong of QL7, and SA.Accent when chosen, a colour the spec does not name for them (CHOSEN).
4. The marks take the colour words of the spec by QL7: the green check is SA.Ok, the amber mark SA.Warn. SA.Warn is #7A4B00 and reads brown, which is the value the spec gives. The shapes, a tick and a filled circle with a white sign, are Code's.
5. Start width. DockablePaneState of Revit 2024 offers DockPosition, MinimumWidth (docked, 200 by default, since 2020), MinimumHeight, FloatingRectangle (floating only) and TabBehind, and no start width for a docked pane (READ RevitAPIUI.xml of the Revit 2024 API package). The pane sets MinimumWidth to SA.Size.Pane, 380, the one lever there is.
6. Villa mode. The row stands directly under the Dry run checkbox, inside its SA.Panel box. Code takes that as "under the Dry run checkbox": outside the box it would stand under the box.
7. Review 1 of PR 90: the sections stand 16 apart, a value of the scale 4, 8, 12, 16, 24 of the spec, where SA.Pad.Section 16,16,16,4 gave 20 before.
8. Review 1 of PR 90: the Neighbourhood drop down and the Plot list box start at the x of the Level profile drop down, in the value column of the rows.
9. PR 8: the label "Shared parameters" is 116 wide in SA.Type.Body and was cut at the label column of 104 ("Shared paramete"). The label column is now 124, the widest label and a gap of 8, for every row, so the value columns stay in one line. The file names lose 20 px.
10. PR 8: Import from past job and Export are 224 wide with the Browse padding and the gap of 8, more than the value column of 200 at 380 wide. In the value column Export ran past the edge of the pane at 368 of 364. They now stand right-aligned at the edge of the Browse buttons, Import from past job 31 px after the end of "Pre-setting".

### Not changed, each with its reason

1. QL9, tabular figures. Libre Franklin has no tnum feature, so the digits are proportional, as in the sizes and the step chips. Bader's question.
2. QL10, the icon of 7 Place villas, 4 before the new order. The hole of the pin is drawn as written, white with the 2 wide SA.Heading stroke, so it shows a navy dot. Bader's question.
3. These are offscreen renders, not the Revit screenshots the spec asks for each window. The Revit screenshots come with the run in Revit of PR 6.
4. The pane may still open wider than 380 in Revit, since MinimumWidth is a least width. UNKNOWN until the Revit run. The PNG is exactly 380 wide. QL13 of steps/lane-b.md.
5. At 380 wide, with the label column and Browse, a long file name is cut with an ellipsis and its grey run with it. In pane.png the size and hash show in full on 0 of 10 rows. So the tooltip gives the size and hash again under the full path (CHOSEN). The spec sets the width, the row and the run, so this is open to Bader as QL15.
6. The primary button matches every value the spec gives: 34 high, white text on SA.Accent, SemiBold, radius 3. The spec gives no width or place, so it stands left under the line, as wide as its text.
7. The Next step line. The spec asks one plain line on what the step does and whether dry run is on or off and gives no words. Code writes the step's plain line, then "Dry run is on." or "Dry run is off." (CHOSEN, docs/design/13-ui-labels.md).
8. While a large file is hashed, off the UI thread, its row shows the size alone, "<n> MiB" (CHOSEN).
9. The Settings row tooltip shows the full path only. docs/design/06-settings.md has it show the override file and the work folder too, from the config file, which 5a does not read (join J1 of 5c).
10. The Level profile and Pre-setting rows have no mark. The spec gives the marks to rows of a file, and a level profile is no file, nor is a pre-setting until R367 at 5c.
11. The step rows are about 38 high, not 32, since the spec puts the plain line under the step name. On first open the line of step 1 wraps beside the wide chip "Needs settings". QL14.
12. The label column is 124 wide, the mark column 24, the step mark column 38. The spec gives no widths (CHOSEN).
13. The chip colours by status: Done SA.Ok, issues SA.Warn, Ready SA.Info, Needs step and Needs settings SA.Wait. The spec names the chip texts, not their colours (CHOSEN).
14. A step that is Ready but not the next one keeps the grey outline, as 9 Report in pane.png. The spec names the outline of the next step and of a waiting one only.
15. The sections have no line between them. The spec gives none.
16. With no document open the controls and the buttons are grey and the text blocks keep their colour. 7.2 of the mock asks every button grey, nothing more.
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
28. The two dialogs of DLUS and sales are titled "DLUS schedule" and "Sales schedule", one for each workbook by its role (CHOSEN in review 1 of PR 90, docs/design/13-ui-labels.md).
29. Two inputs on stacked rows stand 2 px apart, the Neighbourhood drop down and the Plot list box, and 3 px the Rules Browse and the Level profile drop down, and the Level profile drop down and the Pre-setting buttons, off the spacing scale 4, 8, 12, 16, 24. The spec gives rows 32, inputs 30 and buttons 28, so no gap of the scale can stand between them while all hold. QL16 of steps/lane-b.md.
30. Icons. The accent strokes of 3 Review data and Update from report are given as "stroke SA.IconAccent 2.5" with no width at 16 px, where 4 Check gives "width 2.5 (3 at 16 px)". Code draws them 2.5 at both sizes, the 2.4 of the 16 px set being the default stroke only (CHOSEN, read so by ThemeGateTests).
31. Icons. A button with no number has its icon keyed by its label with no spaces, SA.Icon.SiteAdaptationRules and SA.Icon.UpdateFromReport (CHOSEN).
32. Ribbon. The labels Site Adaptation Rules and Update from report are long for a large button, and how Revit wraps them under the icon is UNKNOWN until a run in Revit. The ribbon is not in a PNG, only its icons.
33. Ribbon. Site Adaptation Rules and Update from report run through the flow as a numbered button with no body does and say "<label> is not built yet." in a TaskDialog (CHOSEN in docs/design/13-workflow-changes.md). Their windows come in 5c and 5f.
34. Pane. Import from past job, Export and the link Import pre-setting say "<label> is not built yet." in the TaskDialog of the ribbon buttons, raised through an external event as the box button is (CHOSEN in docs/design/13-workflow-changes.md, the logic joins R367 at 5c, W4). Not in a PNG.
35. Pane. The line of a past job on first open is a help line, 13 SA.TextMuted, 12 under the button, with the link Import pre-setting in SA.Accent, underlined. The spec gives its words and what the link runs, not its look (CHOSEN). It is one line of three texts in the label list, the second the link.
36. Pane. The parameter sheet is the input parameterSheet of the settings JSON. docs/design/06-settings.md gives it no key (CHOSEN).
37. The label list keeps the check table title "3 Check, NBH <n>, <n> plots" and the Set up site line "Pass limit 1 mm. <n> of <n> pass" as 7.3 and 7.4 of docs/design/07-mock.md give them, where Bader's changes make them 4 Check and 0.1 mm. Neither window is built in 5a, so their entries change with 5b (W1) and 5c (W5) or the fold in.
38. docs/addin-mock.md item 1 still names three panels and the old numbers. 7.1 of docs/design/12-ui-spec.md prevails (its Rules line 10), so the ribbon test reads 7.1. The mock changes at the fold in, item 11 of steps/lane-b.md.
