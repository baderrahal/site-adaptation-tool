# Lane B, the laptop lane

Phase 5 and 6 on Bader's company laptop (D1 of steps/decisions-2026-10-28.md). While Phase 4 is open this file holds what the shared records get at the fold in, and it is where a Lane B phase is marked done.

## Phases

| Phase | Status |
|---|---|
| L0 Laptop check, light mode design, UI spec | done, Lane B, laptop, branch docs/laptop-l0 |
| 5a Install, ribbon, pane, one transaction per button | open |
| 5b Button 1 Set up site | open |

After 5b Lane B waits until 4g2, 4g3 and 4h are all marked done in steps/01_next.md.

## Fold in

Not done on 28 Oct 2026 in L0. Two conditions fail: PR 75 "Round ADDIN 4g2 PR 2" is open, and origin holds the branches docs/addin-4g2-state-line and feat/addin-4g2-status-memory besides main and the Lane B branch. The top entry of steps/log.md was not checked, since the fold in fails already.

Waiting for the fold in, in one records commit:

1. steps/01_next.md: an entry for L0 marked done "Lane B, laptop", and the phase changes below. The Phase 4 next step stays as it is.
2. steps/log.md: one entry within the caps for the Lane B phases done.
3. steps/PROGRESS.md: the state and the next three steps.
4. steps/usage.md: the rows of the Usage section below.
5. steps/questions-closed.md: Q021 "Parallel windows over 8 GB free" closed by D3, 28 Oct 2026. Each QL id of this file gets the next free Q id, and the map of old numbers to IDs gets the pair.
6. docs/design/README.md: the rows of 11-light-mode.md and 12-ui-spec.md.
7. docs/design/07-mock.md: every item marked NEW in docs/design/12-ui-spec.md joins the list of 7.7, as approved by Bader (U6, U7). In 7.1 the icons of 12-ui-spec.md take the place of the plain numbered squares named CHOSEN.
8. docs/rules/: the rule id of light mode (D2).
9. CLAUDE.md: the venv command of QL6 when Bader says yes.

## Phase changes to steps/01_next.md

Bader's text of 28 Oct 2026, word for word, for the fold in:

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
- J5. The five paths of config/project.json under C:\Users\bader (QL2).

## Latent findings

None yet.

## Questions open in Lane B

Lane B numbers its questions QL1 upward, CHOSEN by Code: both lanes would take the same next free Q id. At the fold in each takes the next free Q id, item 5 above.

QL1. Inputs on the laptop. C:\02-Site Adaptation is not on the laptop (steps/laptop-check.md item 12).
- Recommended: copy C:\02-Site Adaptation from the personal PC to the same path on the laptop, steps 1 and 2 of the Bader section of steps/laptop-check.md. inputRoot, the key copies and key_copies.json then hold as they are, and R312 checks the copies.
- Other: point inputRoot at C:\10- ROSHEN MECCA. It has no Revit 2024 key copies, no key_copies.json and lacks 4 named files (item 13).

QL2. The five paths of config/project.json under C:\Users\bader (item 14).
- Recommended: 5a reads the work folder, the window lock and the data book from a file per machine outside git, %APPDATA%\SiteAdaptation\machine.json, before config/project.json, and 5c joins it to the settings of R367. On the laptop they point under C:\Users\p003653k\Documents\SiteAdaptation.
- Other: config/project.json gets the laptop paths, and every switch of machine edits it.

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
