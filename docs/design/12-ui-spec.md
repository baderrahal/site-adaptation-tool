## 12. UI spec

Bader designed and approved it on the canvas "Site Adaptation UI" on 28 Oct 2026, approvals U1 to U7 of steps/decisions-2026-10-28.md. Written in Lane B L0 from Bader's text, word for word where it gives values. Section 7 and docs/addin-mock.md still set the labels and their order. This part sets the look. A text in angle brackets stands for a value, as `<n>` for a number.

At the fold in each item marked NEW joins the list of 7.7, and the icons below take the place of the plain numbered squares that 7.1 names CHOSEN (steps/lane-b.md).

### Rules

- One theme file, Theme.xaml, holds every colour, font, size and icon by key. A test in the gate fails on any colour literal or font size in any other XAML or C# UI file.
- Labels and order follow 7.1 to 7.4 of the mock, word for word, plus the NEW items below. Where Bader's changes of 28 Oct 2026 in docs/design/13-workflow-changes.md differ from the mock, the changes prevail. Nothing smaller than 12.
- Every rule shown to a user shows a plain name of at most 6 words, its rule id as a tooltip. Code drafts the plain names in one list file and lists them for Bader's check in one question.
- A change to the look is a question for Bader, never Code's choice.
- Each window built gets a Revit screenshot in steps/ui-shots/, and Bader compares it with the canvas. Code lists every difference it knows of.

### UI controls

Bader, 28 Oct 2026, for 5a and every later phase that touches a window.

1. Theme test. A gate test fails on any colour literal or font size outside Theme.xaml.
2. Text test. Every label, title, help line, button text and chip text in the XAML and its view models appears word for word in one label list file, filled from this part and 7.1 to 7.4 of docs/design/07-mock.md. A gate test fails on any text not in that list, and on any list entry that no window shows. The list file is docs/design/14-ui-labels.md, grouped by window (CHOSEN by Code). An entry counts against "no window shows" only once its window is built (CHOSEN by Code, Set up site comes in 5b and the check table in 5c, question Q136 of steps/questions-closed.md).
3. Picture test. Each window is rendered offscreen with sample data, no Revit needed, at 100 percent scale, to PNG in steps/ui-shots/<window>.png: the ribbon icons at 32 and 16 px, the pane in use, the pane on first open, Set up site, the check table. steps/ui-shots/README.md lists each PNG and every difference from this spec Code knows of. Until Bader approves a PNG it is a draft. Once he approves it, it is the baseline, and a gate test renders the window again and fails if more than 0.5 percent of its pixels differ from the baseline. A change to an approved window needs Bader's approval of a new PNG. The sample data holds no real coordinate or level (CHOSEN by Code, by the Real files rules of CLAUDE.md), and each PNG stays within the 100 KB cap of R400 on every file of docs/ and steps/ outside steps/history/ (CHOSEN by Code, python/record_parts.py lines 59 to 62).
4. UI checklist in review. In each review pass of a PR that touches a window, the reviewer goes through this file line by line against the XAML and lists every difference as REAL. No difference is fixed by changing the spec.
5. At the end of 5a the final message of the session holds a UI approval form: one line per PNG, approve or change, recommended answer first.

### Font

- Libre Franklin (SIL Open Font License), bundled in the add-in and loaded from its resources. If no route through the proxy reaches the TTF files, ask Bader to download Libre Franklin from fonts.google.com into a folder, and use Franklin Gothic Book then Segoe UI until then.
- SA.Type.Title 20 SemiBold. SA.Type.Next 18 SemiBold. SA.Type.Section 15 SemiBold. SA.Type.Body 13 Regular. SA.Type.Small 12 Regular, the smallest. Tabular figures for numbers.
- Added by Bader's answer Q132 of 28 Oct 2026 (steps/questions-closed.md): SA.Type.Project 16 SemiBold, the project name. SA.Type.Step 14 Regular, the step name. SA.Type.Count 28 SemiBold, the check point count. SA.Type.Label 13 SemiBold, the "Next step" label.

### Colours

- SA.Surface #FFFFFF, SA.Panel #F5F7FA, SA.Band #1B3349, SA.BandText #DCE8F5, SA.BandAccent #9ED9FD, SA.Guide #EAF3FC, SA.GuideBorder #C9DDF3, SA.Border #D5DCE4, SA.BorderStrong #B8C2CD, SA.Text #1B1F24, SA.TextMuted #4A5663, SA.Heading #1B3349, SA.Accent #0052CC, SA.Selected #E3ECF8, SA.IconAccent #00AEE6, SA.IconTick #2F9E2A, SA.Ok #0B6B2E on #E6F4EA, SA.Warn #7A4B00 on #FFF4D6, SA.Info #0A3D91 on #E6EEFB, SA.Wait #4A5663 on #F0F2F5, SA.Error #B42318 on #FDECEA.
- The plain colour words below take these keys (Q132): amber SA.Warn, grey outline SA.BorderStrong, green check SA.Ok, white SA.Surface.

### Sizes

- Spacing 4, 8, 12, 16, 24 only. Main button 34 high, other buttons 28 to 32, input and project rows 32, step rows 40 (Q139, Bader 29 Oct 2026, for the name and its line under it), inputs 30. Radius 3 on controls, 12 on chips. Pane starts 380 wide.
- Buttons: primary white text on SA.Accent, SemiBold. Dry run SA.Heading text with SA.Heading border. Cancel SA.Text with SA.BorderStrong border.

### Icons

32 unit grid, stroke SA.Heading width 2, round caps and joins, fill none unless named. Draw as WPF geometry, render to 32 and 16 px PNG for the ribbon. Lane B 5a PR 2 renders them in memory at start from Theme.xaml at the screen scale, with no PNG file, the same drawing at 32 and 16 px at scale 1 and with more pixels above it (CHOSEN by Code). The 16 px set uses stroke 2.4 and drops the parts marked (32 only). A part marked (16 px: ...) is drawn at 16 px with the path given there. Bader's changes of 28 Oct 2026 (docs/design/13-workflow-changes.md) renumber the buttons and add the icons of 3 Review data, Site Adaptation Rules and Update from report. Each older icon keeps its drawing under its new number.

```text
1 Set up site: M8 19a8 8 0 1 0 16 0a8 8 0 1 0 -16 0 | M16 14v10M11 19h10 | M16 2l4 7l-4-1.5l-4 1.5z fill SA.IconAccent no stroke
2 Read data: M24 16V10l-6-6H7v24h9 | M18 4v6h6M11 15h8M11 19h4 | M17 24a6 6 0 1 0 12 0a6 6 0 1 0 -12 0 fill SA.IconAccent no stroke | M23 20.5v6M20.5 24.5l2.5 2.5l2.5-2.5 stroke white
3 Review data: M3 5h18v18H3z | M3 11h18M3 17h18M9 5v18 (16 px: M3 11h18M9 5v18) | M18 24a5 5 0 1 0 10 0a5 5 0 1 0 -10 0 fill white, stroke SA.IconAccent 2.5 | M26.8 27.8l3 3 stroke SA.IconAccent 2.5
4 Check: M10 6H7v23h18V6h-3 | M12 4h8v4h-8z | M11 18l4 4l7-8 stroke SA.IconTick width 2.5 (3 at 16 px) | M12 26h8
5 Floors and ramps: M2 27h28 | M4 27l12-8v8 | M16 13h13v6H16z fill SA.IconAccent no stroke
6 Levels: M3 9h21M3 23h21 | M24 9l-3.5-5h7z fill SA.Heading no stroke | M24 23l-3.5-5h7z fill SA.IconAccent no stroke | M9 12v8 stroke SA.IconAccent | M7 14l2-2l2 2M7 18l2 2l2-2 stroke SA.IconAccent (32 only)
7 Place villas: M3 16l10-8l10 8v11H3z | M10 27v-6h6v6 (32 only) | M26 2.5a4 4 0 0 1 4 4c0 3-4 7.5-4 7.5s-4-4.5-4-7.5a4 4 0 0 1 4-4z fill SA.IconAccent no stroke | M25 6.5a1 1 0 1 0 2 0a1 1 0 1 0 -2 0 fill white no stroke (32 only)
8 Walls: M2 8h28v4H2z fill SA.IconAccent no stroke | M3 12h26v15H3z | M3 19.5h26 | 32 px joints M11 12v7.5M21 12v7.5M7 19.5V27M16 19.5V27M25 19.5V27 | 16 px joints M16 12v7.5M9 19.5V27M23 19.5V27
9 Report: M5 5h22v22H5z | M5 11h22 | M9 23v-5h3v5zM14.5 23v-8h3v8zM20 23v-6h3v6z fill SA.IconAccent no stroke | M8 23h16 (32 only)
Site Adaptation Rules: M3 7c4-2 9-2 13 1v19c-4-3-9-3-13-1z | M29 7c-4-2-9-2-13 1v19c4-3 9-3 13-1z | M21 5v9l2.5-1.8l2.5 1.8V5z fill SA.IconAccent no stroke
Update from report: M17 27H5V5h16v9 | M5 11h16M11 5v22 (16 px: M5 11h16) | M29 22.5a6 6 0 1 1 -2-4.5 stroke SA.IconAccent 2.5 | M27.5 14v4.5H23 stroke SA.IconAccent 2.5
```

### 7.1 Ribbon

Ribbon tab "Site Adaptation" with four panels in this order (Bader 28 Oct 2026, docs/design/13-workflow-changes.md): Setup (1 Set up site, 2 Read data, 3 Review data, 4 Check), Rules (Site Adaptation Rules), Build (5 Floors and ramps, 6 Levels, 7 Place villas, 8 Walls), Output (9 Report, Update from report). Large buttons, the icons above.

### 7.2 Pane

Top to bottom:

- Band, SA.Band: small "Project" in SA.BandAccent, the project name 16 SemiBold white, "not saved" when the title is empty or the document has no saved path (answer of Revit run 3: a detached document has a title but no saved path, as its steps show no status).
- NEW Next step box, SA.Guide with SA.GuideBorder line under it: "Next step" 13 SemiBold SA.Info, the next button's name 18 SemiBold, the next by the order of the 9 steps of docs/design/13-workflow-changes.md, one plain line on what it does and that dry run is on or off, a primary button `"Run <n> <name>"` that runs that ribbon button.
- NEW First open state, when no settings file is chosen: the box reads "Start here", "Choose the settings file", "It names the drawings, the rules and the prototypes for this job. The tool fills the rest of this panel from it.", button "Choose settings file", then the line added by Bader on 28 Oct 2026: "Or start from a past job: Import pre-setting. It brings the settings, rules, level profile, ramp slopes, parameter sheet and overrides of that job." "Import pre-setting" in it runs Import from past job of the Pre-setting row (CHOSEN by Code). Settings row shows an amber mark and "Not chosen". Steps show "Needs settings" or "Needs step n".
- Sections Project, Input files, Scope, Steps in the mock's order and labels. Section titles 15 SemiBold SA.Heading.
- NEW One help line under each section title, 13 SA.TextMuted: Project "Rules and settings for this job. Set once." Input files `"The drawings and schedules the tool reads. All <n> found."`, or `"The drawings and schedules the tool reads. <m> of <n> found."` when a file is missing (Q132). Scope "Which plots the next button works on." Steps "Run them in order. Each one waits for the one before."
- Input and project rows: a green check mark when the file is found, amber mark when not, label, the file name only, Browse. Its tooltip gives the full path and under it the size and short hash of the mock's 7.7 item 5, for each file of the row (Q140, Bader 29 Oct 2026).
- Added by Bader on 28 Oct 2026: in Project a row "Pre-setting" with the buttons "Import from past job" and "Export", under Level profile (the place CHOSEN by Code). A pre-setting never holds drawing paths or coordinates, its logic joins R367 at 5c. In Input files a row "Shared parameters" for the parameter sheet Excel, after Prototypes (the place CHOSEN by Code).
- Dry run checkbox in a SA.Panel box.
- NEW A row "Villa mode" under the Dry run checkbox, with the choices Masses and Links, Masses by default (Q129, Q133, docs/design/11-light-mode.md). Test runs set it in the settings file.
- Steps: a round number mark 26 px (filled SA.Band when done, amber when done with issues, SA.Accent outline for the next step, grey outline when waiting), the name 14, NEW one plain line under it at 12: 1 "Coordinates, levels and types from CAD", 2 "Reads the drawings and schedules", 3 "The team checks what was read", 4 "Compares the rules with the past job", 5 "Generic, yard and ramp floors", 6 "Sets FFL and entry levels", 7 "Places a villa on each plot at its FFL", 8 "Boundary and garden walls", 9 "Excel report of the run". The 9 steps and their order are those of docs/design/13-workflow-changes.md, each needs the one before and 9 needs 4. The line stays on one line with no wrap, cut with an ellipsis, its full text as tooltip (Q139, Bader 29 Oct 2026). Status chip on the right: Done, `"<n> issues"` (NEW short form of `"Done with <n> issues"`, full text as tooltip), Ready, `Needs step <n>`.

### 7.3 Set up site

- Band, SA.Band: "1 Set up site" 20 SemiBold, the badge on SA.BandAccent, NEW one line "Sets the coordinates, levels and types of this model from the CAD drawings.", the signs line in SA.BandAccent, NEW on the right `"<n> of <n>"` 28 SemiBold over "check points pass".
- Left: the two tables of the mock as label and value rows, rows that will change on SA.Warn ground with "Will change" at the end.
- Right: the check point table, Diff in SA.Ok when it passes, the pass line, the existing site file note in a SA.Guide box.
- Foot on SA.Panel: Cancel, Dry run, Apply.
- Added by Bader on 28 Oct 2026 for 5b (docs/design/13-workflow-changes.md, Set up site): the pass limit 0.1 mm from addin.position.checkPointLimitMm of config. The Floor types row reads "Generic 150mm, yard, Flat Ramps, and one Ramps type per slope below". A new row "Ramp slope" with a dropdown 0.5%, 1%, 2%, then 3% to 15% in 1% steps, default 0.5%, and the line "Default 0.5%. Choices 0.5%, 1%, 2%, then 3% to 15%". The Shared parameters row reads "From the parameter sheet: ..." with the sheet name, its count and Browse.

### 7.4 Check table

- Band, SA.Band: the title 20 SemiBold and the line, buttons Zoom to plot, Export to Excel, Save overrides (primary).
- NEW How to use band, SA.Guide: "Each row is a plot where the tool and the past job differ. Pick a row to see why, then choose Rule, Past or Skip. Save overrides keeps your choices."
- Chips as the mock and item 8 of 7.7, the active chip filled SA.Band, Assumed with a dashed amber border. `"<n> plots follow every rule"` on the right with a green check.
- Table columns as the mock. Rule column shows the plain name, id as tooltip. Selected row on SA.Selected with a 3 px SA.Accent mark on its left.
- NEW Side box, 340 wide, SA.Panel: "Selected", `"Plot <n>, <unit>, <check>"`, two boxes Tool and Past job with their values, one plain line on the rule with its share and strong or weak, then "Choose" with three options Rule "Keep the tool value.", Past "Use the past job. Saved as an override.", Skip "Leave this plot out. It is listed in the report.", each with the value it would give. It sets the same Use as the row's dropdown.
- Added by Bader on 28 Oct 2026 for 5c: the side box shows the manual rule of the row with "Open in Site Adaptation Rules", and every row names its manual rule ID (docs/design/13-workflow-changes.md, Site Adaptation Rules).

### New windows of 28 Oct 2026

Review data (5c), Site Adaptation Rules (5c) and Update from report (5f) take their labels, columns, chips and side boxes from docs/design/13-workflow-changes.md, word for word. Their look follows 7.3 and 7.4: the band on SA.Band with the title 20 SemiBold, a How to use line on SA.Guide, chips with the active chip filled SA.Band, the selected row on SA.Selected with a 3 px SA.Accent mark, a side box 340 wide on SA.Panel, the foot on SA.Panel with Cancel, Dry run and the primary button (CHOSEN by Code, the canvas is not in the repo, each window's PNG goes to Bader for approval).
