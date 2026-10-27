# Lane B, the laptop lane

Phase 5 and 6 on Bader's company laptop (D1 of steps/decisions-2026-10-28.md). While Phase 4 is open this file holds what the shared records get at the fold in, and it is where a Lane B phase is marked done.

## Phases

| Phase | Status |
|---|---|
| L0 Laptop check, light mode design, UI spec | done, Lane B, laptop, on the merge of branch docs/laptop-l0 after its review |
| 5a Install, ribbon, pane, one transaction per button | open |
| 5b Button 1 Set up site | open |

After 5b Lane B waits until 4g2, 4g3 and 4h are all marked done in steps/01_next.md.

A laptop session starts from Bader's Lane B prompt, never from steps/START.md. START.md runs the first open phase of steps/01_next.md, which is Phase 4, and nothing there names this file until the fold in.

## Fold in

Not done on 28 Oct 2026 in L0. Two conditions fail: PR 75 "Round ADDIN 4g2 PR 2" is open, and origin holds the branches docs/addin-4g2-state-line and feat/addin-4g2-status-memory besides main. The top entry of steps/log.md was not checked, since the fold in fails already.

Waiting for the fold in, in one records commit:

1. steps/01_next.md: an entry for L0 marked done "Lane B, laptop", and the phase changes below. The Phase 4 next step stays as it is.
2. steps/log.md: one entry within the caps for the Lane B phases done.
3. steps/PROGRESS.md: the state and the next three steps.
4. steps/usage.md: the rows of the Usage section below.
5. steps/questions-closed.md: Q021 "Parallel windows over 8 GB free" closed by D3, 28 Oct 2026. Each QL id of this file gets the next free Q id, the map of old numbers to IDs gets the pair, and every file that names a QL id (this file, steps/laptop-check.md, steps/decisions-2026-10-28.md, docs/design/11-light-mode.md) names the Q id instead.
6. docs/design/README.md: the rows of 11-light-mode.md and 12-ui-spec.md.
7. docs/design/07-mock.md: every item marked NEW in docs/design/12-ui-spec.md joins the list of 7.7, as approved by Bader (U6, U7). In 7.1 the icons of 12-ui-spec.md take the place of the plain numbered squares named CHOSEN. These differences of 12-ui-spec.md from the mock carry no NEW mark and join 7.7 too, so the screenshot check of Phase 6 finds none by chance: the Rule column shows the plain name with the id as a tooltip, the "Will change" mark of 7.3, the found and not found marks of the input rows, and the first open state of 7.2 next to the new project prefilled from config of section 6.
8. CLAUDE.md: the venv command of QL6 when Bader says yes.

## Phase changes to steps/01_next.md

Bader's text of 28 Oct 2026, word for word, for the fold in. Two names are changed, CHOSEN by Code: "R1" is written "The Revit run after 5c", since R1 is also the id of a rule, and "the window rule below" is written "the window rule (D3)", since the rule is in steps/decisions-2026-10-28.md.

- 5a: R372 settings part waits for R367 of 4h, build the pane against a small settings reader, join at 5c. Q103 moves to 5c. The UI spec and Theme.xaml are part of 5a's done-when. Install per user only.
- 5b: R383 element tags wait for R368 of 4h, button 1 calls one tag step that 5c fills.
- 5c: do every join item of steps/lane-b.md first (R367 settings, R368 tags), then Q103, then buttons 2 to 4 with light mode (Masses and Links) and the check table of the UI spec with the plain rule names.
- The Revit run after 5c: as "Revit run after 5c" of steps/01_next.md, in Masses mode, plus one NBH in Links. Measure the open item of the base boundary. Record the peak memory of one window in each mode, and run the whole NBH 03 image again (Q063) now that 64 GB is free.
- 5d: done when NBH 012 is placed in Masses with all 13 prototype shapes, then in Links, peaks logged.
- 6b: Masses for all 7 zones, Links for one NBH. Zones run in parallel windows by the window rule (D3).

## Join items for 5c

Changes Lane B needs in files it may not edit while Phase 4 is open.

- J1. R367 settings: 5a builds the pane against a small settings reader, 5c joins it to R367 of 4h.
- J2. R368 tags: button 1 of 5b calls one tag step, 5c fills it from R368 of 4h.
- J3. Q103 moves from 5a to 5c.
- J4. The keys of light mode in config/project.json: the Villa mode default, the height of the solid and the limit of the base against the footprint (D2, QL4, QL5).
- J5. The 10 paths under C:\Users\bader, 9 in config/project.json and 1 in config/road_surfaces.json (QL2, steps/laptop-check.md item 14).
- J6. The rule id of light mode (D2). Bader gives it "at fold in", and docs/rules/ is outside Lane B while Phase 4 is open, so 5c writes it in docs/rules/ with the code and a test that names it, and names it in docs/design/11-light-mode.md. An id from R400 up must be a written rule with its test, and section 10 holds the planned ids R345 to R388 only, so the id cannot come in a records commit alone.
- J7. D3 keeps a window lock per copy, while R200 holds one lock siteBuild.lock for every window and refuses a window while a process of siteBuild.windowProcesses runs. Parallel windows need R200 changed before 6b. The peak per window is measured on one NBH, 6b opens whole zones, so the first 6b window measures its own peak before a second opens.

## Latent findings

- L0 breaker 21. Step 8 of the Bader section of steps/laptop-check.md sets only http.sslBackend. It works because ~/.gitconfig already holds http.schannelcheckrevoke=false, set by someone UNKNOWN. On a machine without that line the step would not be enough. No other machine runs it now.

## Questions open in Lane B

Lane B numbers its questions QL1 upward, CHOSEN by Code: both lanes would take the same next free Q id. At the fold in each takes the next free Q id, item 5 above.

QL1. Inputs on the laptop. C:\02-Site Adaptation is not on the laptop (steps/laptop-check.md item 12).
- Recommended: copy C:\02-Site Adaptation from the personal PC to the same path on the laptop, steps 1 and 2 of the Bader section of steps/laptop-check.md. inputRoot, the key copies and key_copies.json then hold as they are, and R312 checks the copies.
- Other: point inputRoot at C:\10- ROSHEN MECCA. It has no Revit 2024 key copies, no key_copies.json and lacks 4 named files (item 13).

QL2. The 10 paths of config under C:\Users\bader (item 14).
- Recommended: one file per machine outside git, %APPDATA%\SiteAdaptation\machine.json, gives the root that stands for C:\Users\bader\Documents\SiteAdaptation, read before config. 5a reads it for the work folder and the window lock, 5c joins it to the settings of R367 for the rest. On the laptop the root is C:\Users\p003653k\Documents\SiteAdaptation.
- Other: config gets the laptop paths, and every switch of machine edits it.

QL3. The split of U1 to U7 in steps/decisions-2026-10-28.md is CHOSEN by Code.
- Recommended: keep it.
- Other: Bader gives the split of the canvas.

QL4. The Villa mode default of a run Bader starts by hand. D2 sets Masses for test runs only.
- Recommended: Masses, Links chosen per run, since Links is the heavy mode.
- Other: Links.

QL5. How the base of a solid is measured against the footprint outline (D2).
- Recommended: the largest distance from any point of one outline to the other outline, both ways, in mm after the placement, against a limit in config/project.json.
- Other: the area between the two outlines over the footprint area.

QL6. `py -m venv .venv` of CLAUDE.md makes a 3.14 venv on the laptop, since py picks 3.14 there (item 6).
- Recommended: CLAUDE.md reads `py -3.12 -m venv .venv` from the fold in.
- Other: leave CLAUDE.md, and each laptop session makes the venv on 3.12 by hand.

QL7. docs/design/12-ui-spec.md says Theme.xaml holds every size and colour by key, but some values it uses have no key: the sizes 16 (project name), 14 (step name) and 28 (the check point count), and the colours amber, grey outline, green check and white. A change to the look is Bader's, so 5a needs these before it writes Theme.xaml.
The "Next step" label is 13 SemiBold, while SA.Type.Body is 13 Regular. The Input files help line has text only for "All [n] found".
- Recommended: three more type keys SA.Type.Project 16 SemiBold, SA.Type.Step 14 Regular and SA.Type.Count 28 SemiBold, and SA.Type.Label 13 SemiBold. Amber is SA.Warn, grey outline SA.BorderStrong, green check SA.Ok and white SA.Surface. When a file is missing the help line reads "The drawings and schedules the tool reads. [m] of [n] found."
- Other: Bader gives the keys, the values and the line from the canvas.

QL8. Where the pane shows Villa mode (docs/design/11-light-mode.md). The approved pane of 12-ui-spec.md has no control for it, and section 6 gives a pane row only to a row of the mock.
- Recommended: a row "Villa mode" with Masses and Links under the Dry run checkbox of Scope, a NEW item of 7.7. Test runs set it in the settings file.
- Other: no pane row, the settings file only.

## Usage

| Phase | Date | Tokens | Cost USD | How known |
|---|---|---:|---:|---|
| L0 | 28 Oct 2026 | UNKNOWN | UNKNOWN | npx is not installed, steps/laptop-check.md item 20 |

## How L0 ran

- The main session wrote the four files with no developer helper, CHOSEN since the UI spec text was only in the session prompt.
- "Never two PRs open" was read as Lane B's own PRs, CHOSEN, since PR 75 of Phase 4 was open and touches none of these files.
- Every git command to origin ran with `-c http.sslBackend=schannel -c http.schannelCheckRevoke=false`, item 10.
- To prove the hooks, one PowerShell read of a registry key that does not exist ran before guard-registry.sh was read. That hook blocks changes only. CLAUDE.md says nothing in the registry is read, so it is listed here. The second probe was `reg /?`, which the hook blocked.
- The local dotnet test fails 42 per framework on main and on this branch alike, all from pwsh missing (item 23). So the L0 PR merges only when the python and the dotnet check of CI both pass.
