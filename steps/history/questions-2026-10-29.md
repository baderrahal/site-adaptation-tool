# Questions as asked, closed on 2026-10-28 and 2026-10-29

The full text of the questions closed at the fold in and the answer pass of Round FOLD, 2026-10-29, as they were asked. The answer of each is in the table of steps/questions-closed.md. Find a question by grep of its ID.

## Q126 to Q147, the Lane B questions

Lane B numbered its questions Q126 upward, since both lanes would take the same next free Q id. At the fold in each QL id took the next free Q id, QL<n> is Q<125+n>.

### Q126 to Q133, as L0 asked them, 28 Oct 2026

Q126. Inputs on the laptop. C:\02-Site Adaptation is not on the laptop (steps/laptop-check.md item 12).
- Recommended: copy C:\02-Site Adaptation from the personal PC to the same path on the laptop and compare the counts, steps 1 to 4 of the Bader section of steps/laptop-check.md. inputRoot, the key copies and key_copies.json then hold as they are, and R312 checks the copies.
- Other: point inputRoot at C:\10- ROSHEN MECCA. It has no Revit 2024 key copies, no key_copies.json and lacks 4 named files (item 13).

Q127. The work root of the laptop. Config names 10 paths under C:\Users\bader (item 14), and the test host, the job guard, the placer and the default work folder of section 6 take Documents\SiteAdaptation from the Windows Documents folder instead. On the laptop that folder is redirected to C:\Users\p003653k\OneDrive - Parsons Corp\Documents, which syncs to the company cloud, so the two would split, and plans with real coordinates would sync.
- Recommended: one file per machine outside git, %APPDATA%\SiteAdaptation\machine.json, gives one root that stands for C:\Users\bader\Documents\SiteAdaptation and for Documents\SiteAdaptation, read before config by the C# and the Python side. On the laptop the root is C:\Users\p003653k\Documents\SiteAdaptation, outside OneDrive. 5a codes it in the add-in shell, 5c joins it for the rest (J5, J8). With it the R343 check of item 22 finds siteBuild.folder and runs on the laptop.
- Other: Lane B works under the OneDrive Documents folder and accepts the sync.

Q128. The split of U1 to U7 in steps/decisions-2026-10-28.md is CHOSEN by Code.
- Recommended: keep it.
- Other: Bader gives the split of the canvas.

Q129. The Villa mode default of a run Bader starts by hand. D2 sets Masses for test runs only.
- Recommended: Masses, Links chosen per run, since Links is the heavy mode.
- Other: Links.

Q130. How the base of a solid is measured against the footprint outline (D2).
- Recommended: the largest distance from any point of one outline to the other outline, both ways, in mm after the placement, against a limit in config/project.json.
- Other: the area between the two outlines over the footprint area.

Q131. `py -m venv .venv` of CLAUDE.md makes a 3.14 venv on the laptop, since py picks 3.14 there (item 6).
- Recommended: CLAUDE.md reads `py -3.12 -m venv .venv` from the fold in.
- Other: leave CLAUDE.md, and each laptop session makes the venv on 3.12 by hand.

Q132. docs/design/12-ui-spec.md says Theme.xaml holds every size and colour by key, but some values it uses have no key: the sizes 16 (project name), 14 (step name) and 28 (the check point count), and the colours amber, grey outline, green check and white. A change to the look is Bader's, so 5a needs these before it writes Theme.xaml.
The "Next step" label is 13 SemiBold, while SA.Type.Body is 13 Regular. The Input files help line has text only for "All [n] found".
- Recommended: three more type keys SA.Type.Project 16 SemiBold, SA.Type.Step 14 Regular and SA.Type.Count 28 SemiBold, and SA.Type.Label 13 SemiBold. Amber is SA.Warn, grey outline SA.BorderStrong, green check SA.Ok and white SA.Surface. When a file is missing the help line reads "The drawings and schedules the tool reads. [m] of [n] found."
- Other: Bader gives the keys, the values and the line from the canvas.

Q133. Where the pane shows Villa mode (docs/design/11-light-mode.md). The approved pane of 12-ui-spec.md has no control for it, and section 6 gives a pane row only to a row of the mock.
- Recommended: a row "Villa mode" with Masses and Links under the Dry run checkbox of Scope, a NEW item of 7.7. Test runs set it in the settings file.
- Other: no pane row, the settings file only.

### Q134 to Q145, as 5a asked them, 29 Oct 2026

Q134. Tabular figures. Libre Franklin 2.000 and 3.000 have no tnum feature, so the "Tabular figures for numbers" of docs/design/12-ui-spec.md cannot come from it, and WPF does not fall back to another font for one feature. The GSUB feature list of both files holds aalt ccmp dnom frac liga locl numr ordn sinf subs sups, and rvrn in 3.000, no tnum (READ in 5a PR 2). WPF gives the bundled Regular the digit widths 0=688 1=465 2=599 3=641 4=649 5=654 6=661 7=599 8=668 9=658 in thousandths of the em, 9 widths for 10 digits (RAN), the breaker measured 0=675 1=438 9=641. Under SA.Type.Count "1111", "0000" and "7777" are 55.3, 78.5 and 68.5 px wide (RAN).
- Recommended: keep Libre Franklin, right-align the number columns and accept proportional digits.
- Other: numbers in Segoe UI. Segoe UI Regular has one width for every digit by default, 539 for each, and Segoe UI Semibold has 5 widths, 402 to 576, by default, both files hold a tnum feature (RAN and READ in 5a PR 2), so the Semibold numbers need the tnum request the theme already makes.
- Other: Bader gives another font.

Q135. Icon of Place villas, its hole. The 12-ui-spec.md line of 7 Place villas (4 before Bader's changes of 28 Oct 2026) reads "M25 6.5a1 1 0 1 0 2 0a1 1 0 1 0 -2 0 fill white (32 only)" with no "no stroke", so by the defaults of the spec the 2 wide SA.Heading stroke covers the white fill and the pin shows a navy dot. 5a PR 2 draws it as written, seen in a render of the 16 icons (RAN).
- Recommended: "no stroke", a white hole as the canvas likely shows.
- Other: keep it as written.

Q136. The text test and windows not built yet. Bader's control 2 says the gate test fails on any entry of the label list that no window shows. Set up site comes in 5b and the check table in 5c, so their entries would fail every gate of 5a. The UI spec now counts an entry only once its window is built (CHOSEN by Code, docs/design/12-ui-spec.md, UI controls item 2).
- Recommended: keep it, each window's entries count from the PR that builds it.
- Other: the entries of a window go into the list only in the PR that builds it.

Q137. Messages and the confirm of a button as TaskDialogs. docs/design/03a-buttons-shared-and-1.md line 5 says a TaskDialog shows the reason of a roll back, and the flow of 5a PR 5 shows every stop and message in a TaskDialog and its confirm (Apply, Cancel, the list per plot) in one too. A TaskDialog takes the Windows font, colours and buttons, none of Theme.xaml (docs/design/12-ui-spec.md lines 9 to 13 and the Font, Colours and Sizes parts), and it cannot scroll, so a whole-project list per plot grows past the screen. In 5a only "<label> is not built yet." is reached.
- Recommended: keep TaskDialogs for the short stops and messages as 03a says, accepted as the Revit look, and build the confirm as the themed summary window of 7.7 item 1 with the first button that has a body (5b), with its PNG and labels.
- Other: every message in a themed window of its own.
- Other: keep TaskDialogs for all, the confirm included.

Q138. Pane start width. docs/design/12-ui-spec.md says the pane starts 380 wide. DockablePaneState of Revit 2024 offers DockPosition, MinimumWidth (docked, 200 by default), MinimumHeight, FloatingRectangle (floating only) and TabBehind, and no start width for a docked pane (READ RevitAPIUI.xml of the Revit 2024 API package), so 5a sets MinimumWidth 380, a floor.
- Recommended: keep MinimumWidth 380 and measure the width Revit opens the pane at in the Revit run of 5a.
- Other: Bader gives another rule.

Q139. Step rows and rows 32. docs/design/12-ui-spec.md says rows 32 and gives each step its name at 14 and one plain line under it at 12, which needs about 38 (RAN, the layout of the pane in the picture test). On first open the line of step 1 wraps beside the chip "Needs settings" at 380 (steps/ui-shots/pane-first-open.png).
- Recommended: step rows of 40, on the scale as 24 plus 16, with the input and project rows kept at 32, and the step line cut with an ellipsis and its full text as tooltip.
- Other: Bader gives the heights.

Q140. Input rows at 380 wide. docs/design/12-ui-spec.md puts mark, label, file name, size, short hash and Browse on one row of 32 at 380 wide, and in steps/ui-shots/pane.png the size and hash show in full on 0 of 9 rows (READ the PNG), and on 0 of 10 since the Shared parameters row (5a PR 8, steps/ui-shots/README.md).
- Recommended: keep the file name, put size and short hash in the tooltip under the full path, as 7.2 of docs/design/07-mock.md lets the tooltip tell two files apart.
- Other: two-line input rows.
- Other: a wider pane.

Q141. Gaps between stacked inputs. docs/design/12-ui-spec.md gives rows 32, inputs 30 and spacing 4, 8, 12, 16, 24 only, so two inputs on stacked rows stand 2 px apart (Neighbourhood and Plot list) and 3 px (Rules Browse and Level profile), seen in steps/ui-shots/pane.png.
- Recommended: accept the 2 px left by the row and input heights as part of those sizes, not a spacing.
- Other: inputs 28 high so they stand 4 apart.
- Other: Bader gives the rule.

Q142. Where the pass limit of 0.1 mm applies. Bader's change of 28 Oct 2026 gives Set up site a pass limit of 0.1 mm from addin.position.checkPointLimitMm. That key holds 1.0 today in config/project.json, the 1 mm of R211 for the check points of button 1, and R353 holds the check points of the worker against the keys by it, which lie about half a mm off the baseplan, 127 of 141 passing at 1 mm (docs/design/03a-buttons-shared-and-1.md, Q086 open).
- Recommended: 0.1 mm for the check points of button 1 against Revit, the Revit column after Apply against the transform the dry run gave, under a key of its own, and the key check of R353 kept at the answer of Q086.
- Other: 0.1 mm for both, so most key check points fail.

Q143. The pane shown again each session. RevitAPIUI.xml says VisibleByDefault counts only the first time a pane loads, after that Revit keeps the state of the last session (SEARCHED, 5a PR 6). So 5a PR 6 shows the pane once per Revit session at the first project view when it is hidden, which also brings back a pane the user closed.
- Recommended: keep it, the pane is the way into every step.
- Other: show it only on the first session after the install.

Q144. 9 Report and the steps before it. Bader wrote "Each needs the one before, 9 Report needs 4 Check." 5a reads it as 9 needing 4 only, so 9 is Ready while 5 to 8 wait, and the Steps help line "Each one waits for the one before." then holds for every step but 9.
- Recommended: keep it, the report can run after the check.
- Other: 9 needs 8 and 4.

Q145. Revit run 3 of 5a. The run failed twice: run 1 on OpenAndActivateDocument inside Idling, run 2 because Revit refuses to subscribe Idling from inside an Idling handler (READ journal.0132.txt line 1957). The cause is measured and the fix is known, subscribe the clock once in OnStartup, outside any Idling call, and run each phase from an ExternalEvent. Whether a raised ExternalEvent runs on the Home screen of Revit with no document is still UNKNOWN. The rule stops a fix on its third attempt.
- Recommended: allow run 3 with that fix, and if it fails, measure the ExternalEvent on the Home screen with one probe before any fourth.
- Other: close 5a with open items 1 and 2 measured in part and the offscreen PNGs only, and measure the rest in 5b.

