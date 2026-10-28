# UI shots

Control 3 of docs/design/12-ui-spec.md, UI controls. Each PNG is a render of a window of the add-in, offscreen, with sample data, no Revit, at 100 percent scale (96 DPI). Each is a DRAFT until Bader approves it. An approved PNG is copied into baseline/ under the same name, and from then the gate test R372_each_window_with_an_approved_baseline_renders_within_half_a_percent_of_its_pixels fails when more than 0.5 percent of its pixels differ. baseline/ is empty until then, and the test says it has nothing to compare.

## The PNG files

| File | Window | State |
|---|---|---|
| ribbon-icons.png | The eight ribbon icons, 32 px over 16 px, buttons 1 to 8 left to right | Draft |
| pane.png | The pane in use, filled like the NBH 012 column of 7.2 of docs/design/07-mock.md | Draft |
| pane-first-open.png | The pane on first open, no settings file chosen | Draft |

Set up site comes in 5b and the check table in 5c.

## Render them again

From the repo root, after the build of the gate:

```
dotnet build dotnet\SiteAdaptation.sln -c Release
dotnet\SiteAdaptation.UiShots\bin\Release\net48\SiteAdaptation.UiShots.exe steps\ui-shots
```

The command line and the picture tests call the one entry Shots.Render of dotnet/SiteAdaptation.UiShots/Shots.cs. Each PNG is kept in a palette of 256 colours of its own, so it stays within the 100 KB cap of R400 on steps/.

## Sample data

The project name 01101-PSN-IMC-R17B-TEST-NEW and the file and folder names are the mock's, on a made up folder C:\Sample. The size 2.91 MiB and the short hash 7061ac0d of Plot numbers are the mock's. Every other size and short hash is a sample value, a hash of the file name, not of a real file. The steps are the mock's state after buttons 1 to 3 on NBH 012. No coordinate and no level is shown.

## Differences from docs/design/12-ui-spec.md Code knows of

1. QL9, tabular figures. Libre Franklin has no tnum feature, so the digits are proportional, as in the sizes and the step chips.
2. QL10, icon 4. The hole of the pin is drawn as written, white with the 2 wide SA.Heading stroke, so it shows a navy dot.
3. These are offscreen renders, not the Revit screenshots the spec asks for each window. The Revit screenshots come with the run in Revit of PR 6.
4. Pane width. Revit has no start width for a docked pane. The pane sets its least width to 380, so it opens at 380 or wider, UNKNOWN until the Revit run. The PNG is exactly 380 wide.
5. The band stacks "Project" over the project name on the left, as 7.2 of the spec. Item 7.2 of the mock puts the name at the top right.
6. The Next step line. The spec asks one plain line on what the step does and whether dry run is on or off and gives no words. Code writes the step's plain line, then "Dry run is on." or "Dry run is off." (CHOSEN, docs/design/13-ui-labels.md).
7. The primary button stands left under the line, as wide as its text.
8. Input and project rows are two lines high, the file name over the size and the short hash, so they stand about 40 px high, over the 32 of the spec. A long file name is cut with an ellipsis and shows in full in the tooltip with its path.
9. While a large file is hashed, off the UI thread, its row shows the size alone, "<n> MiB" (CHOSEN).
10. The Settings row tooltip shows the full path only. docs/design/06-settings.md has it show the override file and the work folder too, from the config file, which 5a does not read.
11. The green check is a tick in SA.Ok and the amber mark a filled SA.Warn circle with a white stroke, drawn by Code. SA.Warn, the amber of QL7, #7A4B00, reads brown.
12. The Level profile row has no mark, it names no file.
13. The label column is 104 wide, the mark column 24, the step mark column 38 (CHOSEN, the spec gives no widths).
14. The Villa mode row sits inside the SA.Panel box of the Dry run checkbox, under it.
15. The chip colours by status are Code's choice: Done SA.Ok, issues SA.Warn, Ready SA.Info, Needs step and Needs settings SA.Wait.
16. A step done with issues has its mark filled SA.Warn with a white number. A step that is Ready but not the next one keeps the grey outline, as 8 Report in pane.png.
17. The check box, the radio buttons, the drop downs and the text box keep the look of WPF with the theme font and sizes, so radius 3 is not on them. Browse is drawn as the Cancel button of the spec, SA.Text with an SA.BorderStrong border, 28 high.
18. The sections have no line between them, 16 apart.
19. With no document open the controls are grey, the text blocks keep their colour.
20. A PNG is in a palette of 256 colours, so the edges of the text may differ a little from a screen.
