# Decisions of 28 Oct 2026

Bader's decisions of 28 Oct 2026, written down in Lane B L0 from his prompt for the laptop lane. Each holds from that day. Where a decision changes a shared record, the change waits for the fold in of steps/lane-b.md.

## D1. Two machines, two lanes

- Phase 5 and 6 run on Bader's company laptop, 64 GB RAM. This is Lane B, the laptop lane.
- Phase 4 (4g2, 4g3, 4h) runs on his personal PC.
- One phase per session on each machine.
- Lane B runs L0, 5a and 5b while Phase 4 is open, then stops until 4g2, 4g3 and 4h are all marked done in steps/01_next.md.

## D2. Light mode

- Button 4 gets a setting Villa mode, Masses or Links. Default Masses for test runs.
- One solid per villa, its base the exact base boundary shape of its prototype, read once per prototype file and cached by the file hash.
- Each placed base is checked against the villa outline of the CAD building footprint. Over a limit in config/project.json it flags the plot, never stops the run.
- Height from config/project.json. The same shared parameters and element tags as the link.
- Links mode swaps each tagged solid for its link at the same transform in one transaction with roll back.
- Phase 6: all zones in Masses, one NBH in Links for the final compare.
- The design is docs/design/11-light-mode.md. Its rule id is given at the fold in.

## The base boundary requirement

The base of each villa solid follows the exact base boundary shape of its prototype, every corner, never a bounding rectangle. Bader cares about this shape most of all. Where the base boundary sits in a prototype file is measured on one prototype at the Revit run after 5c, never guessed.

## D3. The window rule, closes Q021

- Before the Revit run after 5c, one Revit window at a time.
- From that run on, the number of windows open together is (free RAM minus 8 GB) divided by the peak that run measured for one window in that mode, rounded down, at least 1 and at most 3.
- The shared window lock per copy and the 2 GB floor hold in every window. Never two windows on the same file. Bader's words name it "the 2 GB floor of R310". The floor is minFreeGB of R200, and R310 samples the free memory per window without stopping at it (steps/PROGRESS.md, open bugs).
- Q021 "Parallel windows over 8 GB free" closes with this answer at the fold in.

## D4. The report reads back, closes Q121

- The add-in gets a button "Update from report", Phase 5f. It reads the report Excel back after the team edits it, lists each change against the model, checks it against the rules and applies it as an override.
- So the Excel files written from 4g3 on are readable back. Every sheet the team may edit, Levels of the level Excel and Placed of the report Excel, holds the plot number as a stable key in its first column and one row per plot and kind.
- The editable columns are FFL, El @ Entry, Steps, Villa type and Mirror. Their header cells carry a light fill and their list is written into the Settings sheet. Every other column is read only.
- The Settings sheet holds the build id and the SHA-256 of the plan the Excel came from, so the update refuses an Excel of another build.
- The labels and the order of 7.5 and 7.6 of the mock stay. The additions are items 10 to 12 of 7.7 of docs/design/07-mock.md.
- Built in Round ADDIN 4g3, the level Excel in PR 4 and the report Excel in PR 5. A part that does not fit four changes per PR goes into the last PR of 4g3.

## U1 to U7. The UI, approved on the canvas "Site Adaptation UI"

Bader designed and approved the UI on the canvas on 28 Oct 2026. The full text is docs/design/12-ui-spec.md. The prompt names the approvals U1 to U7 and does not say which part each covers, so the split below was CHOSEN by Code, and Bader kept it on 28 Oct 2026 (QL3 of steps/lane-b.md).

- U1. The rules: one theme file Theme.xaml and the test that holds it, labels from the mock, plain rule names, a change to the look is Bader's, a screenshot per window.
- U2. The font: Libre Franklin bundled, the fallbacks and the five type sizes.
- U3. The colours, the 21 keys SA.Surface to SA.Error.
- U4. The sizes and the three button looks.
- U5. The eight icons and the ribbon of 7.1.
- U6. The pane of 7.2 with its NEW items.
- U7. Set up site of 7.3 and the check table of 7.4 with their NEW items.

## D5. Speed rules S2 to S5, 28 Oct 2026

Written down in Lane A 4g3 on 29 Oct 2026 from the Lane A prompt.

- S2. Parity on the 7 keys in each PR. The whole project once, in the last code PR of the phase.
- S3. One review pass for a PR that changes only records, docs or UI text. Two passes at most for logic.
- S4. Override naming: a value lists every override that touched its inputs. No more attempts at which one moved it.
- S5. No design freeze. A new design change from Bader goes to the phase that owns that part as a join item, recorded as his decision. If it belongs to the current phase and fits, it is done there.

## D6. The C# checks on the laptop, 29 Oct 2026

- Lane A runs the C# reads and the C# parity on the whole project and the 7 keys, net48 and net8.0, and measures the build peak three times per runtime: in 4g3 for everything since PR 75, in 4h for 4h. A failure is a REAL finding of that phase. This closes the laptop part of Q111.

## D7. The report round trip, 29 Oct 2026

- Bader kept D4 for the report Excel of 4g3: the plot number as a stable key in the first column of Levels and Placed, the editable columns FFL, El @ Entry, Steps, Villa type and Mirror marked in the header and listed in the Settings sheet, the build id and the plan SHA-256 in the Settings sheet. Items 10 to 12 of 7.7 of docs/design/07-mock.md hold it.

## D8. Q122 and the speed of 4g3, 29 Oct 2026

- Q122 answered as recommended, closed in steps/questions-closed.md. Its code, the columns FFL by rule and FFL as written of the sheet Clusters, goes to the first PR of 4h, since Bader ended 4g3 with its end records after PR 99 of Lane B. A draft stopped mid-work sits on the local branch feat/addin-4g3-q122-clusters-ffl at 00d371f of the Lane A clone, untested
- The build peak is measured once per runtime, not three times. A records-only PR gets one review pass
