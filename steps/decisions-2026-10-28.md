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
- The shared window lock per copy and the 2 GB floor of R310 hold in every window. Never two windows on the same file.
- Q021 "Parallel windows over 8 GB free" closes with this answer at the fold in.

## U1 to U7. The UI, approved on the canvas "Site Adaptation UI"

Bader designed and approved the UI on the canvas on 28 Oct 2026. The full text is docs/design/12-ui-spec.md. The prompt names the approvals U1 to U7 and does not say which part each covers, so the split below is CHOSEN by Code and is question QL3 of steps/lane-b.md.

- U1. The rules: one theme file Theme.xaml and the test that holds it, labels from the mock, plain rule names, a change to the look is Bader's, a screenshot per window.
- U2. The font: Libre Franklin bundled, the fallbacks and the five type sizes.
- U3. The colours, the 21 keys SA.Surface to SA.Error.
- U4. The sizes and the three button looks.
- U5. The eight icons and the ribbon of 7.1.
- U6. The pane of 7.2 with its NEW items.
- U7. Set up site of 7.3 and the check table of 7.4 with their NEW items.
