## 12. UI spec

Bader designed and approved it on the canvas "Site Adaptation UI" on 28 Oct 2026, approvals U1 to U7 of steps/decisions-2026-10-28.md. Written in Lane B L0 from Bader's text, word for word where it gives values. Section 7 and docs/addin-mock.md still set the labels and their order. This part sets the look. A text in angle brackets stands for a value, as `<n>` for a number.

At the fold in each item marked NEW joins the list of 7.7, and the icons below take the place of the plain numbered squares that 7.1 names CHOSEN (steps/lane-b.md).

### Rules

- One theme file, Theme.xaml, holds every colour, font, size and icon by key. A test in the gate fails on any colour literal or font size in any other XAML or C# UI file.
- Labels and order follow 7.1 to 7.4 of the mock, word for word, plus the NEW items below. Nothing smaller than 12.
- Every rule shown to a user shows a plain name of at most 6 words, its rule id as a tooltip. Code drafts the plain names in one list file and lists them for Bader's check in one question.
- A change to the look is a question for Bader, never Code's choice.
- Each window built gets a Revit screenshot in steps/ui-shots/, and Bader compares it with the canvas. Code lists every difference it knows of.

### Font

- Libre Franklin (SIL Open Font License), bundled in the add-in and loaded from its resources. If no route through the proxy reaches the TTF files, ask Bader to download Libre Franklin from fonts.google.com into a folder, and use Franklin Gothic Book then Segoe UI until then.
- SA.Type.Title 20 SemiBold. SA.Type.Next 18 SemiBold. SA.Type.Section 15 SemiBold. SA.Type.Body 13 Regular. SA.Type.Small 12 Regular, the smallest. Tabular figures for numbers.
- Added by Bader's answer QL7 of 28 Oct 2026 (steps/lane-b.md): SA.Type.Project 16 SemiBold, the project name. SA.Type.Step 14 Regular, the step name. SA.Type.Count 28 SemiBold, the check point count. SA.Type.Label 13 SemiBold, the "Next step" label.

### Colours

- SA.Surface #FFFFFF, SA.Panel #F5F7FA, SA.Band #1B3349, SA.BandText #DCE8F5, SA.BandAccent #9ED9FD, SA.Guide #EAF3FC, SA.GuideBorder #C9DDF3, SA.Border #D5DCE4, SA.BorderStrong #B8C2CD, SA.Text #1B1F24, SA.TextMuted #4A5663, SA.Heading #1B3349, SA.Accent #0052CC, SA.Selected #E3ECF8, SA.IconAccent #00AEE6, SA.IconTick #2F9E2A, SA.Ok #0B6B2E on #E6F4EA, SA.Warn #7A4B00 on #FFF4D6, SA.Info #0A3D91 on #E6EEFB, SA.Wait #4A5663 on #F0F2F5, SA.Error #B42318 on #FDECEA.
- The plain colour words below take these keys (QL7): amber SA.Warn, grey outline SA.BorderStrong, green check SA.Ok, white SA.Surface.

### Sizes

- Spacing 4, 8, 12, 16, 24 only. Main button 34 high, other buttons 28 to 32, rows 32, inputs 30. Radius 3 on controls, 12 on chips. Pane starts 380 wide.
- Buttons: primary white text on SA.Accent, SemiBold. Dry run SA.Heading text with SA.Heading border. Cancel SA.Text with SA.BorderStrong border.

### Icons

32 unit grid, stroke SA.Heading width 2, round caps and joins, fill none unless named. Draw as WPF geometry, render to 32 and 16 px PNG for the ribbon. Lane B 5a PR 2 renders them in memory at start from Theme.xaml at the screen scale, with no PNG file, the same drawing at 32 and 16 px at scale 1 and with more pixels above it (CHOSEN by Code). The 16 px set uses stroke 2.4 and drops the parts marked (32 only).

```text
1 Set up site: M8 19a8 8 0 1 0 16 0a8 8 0 1 0 -16 0 | M16 14v10M11 19h10 | M16 2l4 7l-4-1.5l-4 1.5z fill SA.IconAccent no stroke
2 Read data: M24 16V10l-6-6H7v24h9 | M18 4v6h6M11 15h8M11 19h4 | M17 24a6 6 0 1 0 12 0a6 6 0 1 0 -12 0 fill SA.IconAccent no stroke | M23 20.5v6M20.5 24.5l2.5 2.5l2.5-2.5 stroke white
3 Check: M10 6H7v23h18V6h-3 | M12 4h8v4h-8z | M11 18l4 4l7-8 stroke SA.IconTick width 2.5 (3 at 16 px) | M12 26h8
4 Place villas: M3 16l10-8l10 8v11H3z | M10 27v-6h6v6 (32 only) | M26 2.5a4 4 0 0 1 4 4c0 3-4 7.5-4 7.5s-4-4.5-4-7.5a4 4 0 0 1 4-4z fill SA.IconAccent no stroke | M25 6.5a1 1 0 1 0 2 0a1 1 0 1 0 -2 0 fill white (32 only)
5 Floors and ramps: M2 27h28 | M4 27l12-8v8 | M16 13h13v6H16z fill SA.IconAccent no stroke
6 Levels: M3 9h21M3 23h21 | M24 9l-3.5-5h7z fill SA.Heading no stroke | M24 23l-3.5-5h7z fill SA.IconAccent no stroke | M9 12v8 stroke SA.IconAccent | M7 14l2-2l2 2M7 18l2 2l2-2 stroke SA.IconAccent (32 only)
7 Walls: M2 8h28v4H2z fill SA.IconAccent no stroke | M3 12h26v15H3z | M3 19.5h26 | 32 px joints M11 12v7.5M21 12v7.5M7 19.5V27M16 19.5V27M25 19.5V27 | 16 px joints M16 12v7.5M9 19.5V27M23 19.5V27
8 Report: M5 5h22v22H5z | M5 11h22 | M9 23v-5h3v5zM14.5 23v-8h3v8zM20 23v-6h3v6z fill SA.IconAccent no stroke | M8 23h16 (32 only)
```

### 7.1 Ribbon

As the mock, these icons, large buttons, labels "1 Set up site" to "8 Report".

### 7.2 Pane

Top to bottom:

- Band, SA.Band: small "Project" in SA.BandAccent, the project name 16 SemiBold white, "not saved" when the title is empty.
- NEW Next step box, SA.Guide with SA.GuideBorder line under it: "Next step" 13 SemiBold SA.Info, the next button's name 18 SemiBold, one plain line on what it does and that dry run is on or off, a primary button `"Run <n> <name>"` that runs that ribbon button.
- NEW First open state, when no settings file is chosen: the box reads "Start here", "Choose the settings file", "It names the drawings, the rules and the prototypes for this job. The tool fills the rest of this panel from it.", button "Choose settings file". Settings row shows an amber mark and "Not chosen". Steps show "Needs settings" or "Needs step n".
- Sections Project, Input files, Scope, Steps in the mock's order and labels. Section titles 15 SemiBold SA.Heading.
- NEW One help line under each section title, 13 SA.TextMuted: Project "Rules and settings for this job. Set once." Input files `"The drawings and schedules the tool reads. All <n> found."`, or `"The drawings and schedules the tool reads. <m> of <n> found."` when a file is missing (QL7). Scope "Which plots the next button works on." Steps "Run them in order. Each one waits for the one before."
- Input and project rows: a green check mark when the file is found, amber mark when not, label, file name with tooltip of the full path, size and short hash as the mock's 7.7 item 5, Browse.
- Dry run checkbox in a SA.Panel box.
- NEW A row "Villa mode" under the Dry run checkbox, with the choices Masses and Links, Masses by default (QL4, QL8, docs/design/11-light-mode.md). Test runs set it in the settings file.
- Steps: a round number mark 26 px (filled SA.Band when done, amber when done with issues, SA.Accent outline for the next step, grey outline when waiting), the name 14, NEW one plain line under it at 12: 1 "Coordinates, levels and types from CAD", 2 "Reads the drawings and schedules", 3 "Compares the rules with the past job", 4 "Places a villa on each plot", 5 "Generic, yard and ramp floors", 6 "Sets FFL and entry levels", 7 "Boundary and garden walls", 8 "Excel report of the run". Status chip on the right: Done, `"<n> issues"` (NEW short form of `"Done with <n> issues"`, full text as tooltip), Ready, `Needs step <n>`.

### 7.3 Set up site

- Band, SA.Band: "1 Set up site" 20 SemiBold, the badge on SA.BandAccent, NEW one line "Sets the coordinates, levels and types of this model from the CAD drawings.", the signs line in SA.BandAccent, NEW on the right `"<n> of <n>"` 28 SemiBold over "check points pass".
- Left: the two tables of the mock as label and value rows, rows that will change on SA.Warn ground with "Will change" at the end.
- Right: the check point table, Diff in SA.Ok when it passes, the pass line, the existing site file note in a SA.Guide box.
- Foot on SA.Panel: Cancel, Dry run, Apply.

### 7.4 Check table

- Band, SA.Band: the title 20 SemiBold and the line, buttons Zoom to plot, Export to Excel, Save overrides (primary).
- NEW How to use band, SA.Guide: "Each row is a plot where the tool and the past job differ. Pick a row to see why, then choose Rule, Past or Skip. Save overrides keeps your choices."
- Chips as the mock and item 8 of 7.7, the active chip filled SA.Band, Assumed with a dashed amber border. `"<n> plots follow every rule"` on the right with a green check.
- Table columns as the mock. Rule column shows the plain name, id as tooltip. Selected row on SA.Selected with a 3 px SA.Accent mark on its left.
- NEW Side box, 340 wide, SA.Panel: "Selected", `"Plot <n>, <unit>, <check>"`, two boxes Tool and Past job with their values, one plain line on the rule with its share and strong or weak, then "Choose" with three options Rule "Keep the tool value.", Past "Use the past job. Saved as an override.", Skip "Leave this plot out. It is listed in the report.", each with the value it would give. It sets the same Use as the row's dropdown.
