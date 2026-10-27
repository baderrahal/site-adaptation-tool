## 11. Light mode, the villa as a mass

Bader's decision D2 of 28 Oct 2026, steps/decisions-2026-10-28.md, written in Lane B L0. Its rule id is given at the fold in or at 5c, join item J6 of steps/lane-b.md. Button 4 of section 3 places each villa as a link. Light mode places it first as one solid, so a test run on a whole zone stays light, and swaps the solids for the links once the shapes are right.

### Villa mode

- Button 4 has a setting Villa mode with two values, Masses and Links.
- Default Masses for test runs (D2). The default of a run Bader starts by hand is question QL4 of steps/lane-b.md.
- The setting is a field of the settings of section 6. Where the pane shows it is question QL8 of steps/lane-b.md, since section 6 gives a pane row only to a row of the mock.
- Button 4 sets the mode. Buttons 6 and 8 act on what button 4 placed and must know it, open points 1 and 2 below.

### The solid of a villa

- One solid per villa. Its base follows the exact base boundary shape of its prototype, every corner, never a bounding rectangle. This shape is what Bader cares about most.
- The shape is read once per prototype file and cached in the work folder by the prototype file hash. Every villa of that prototype reuses it with its own point, rotation and mirror, the transform button 4 gives the link.
- The height of the solid comes from config/project.json, a key added at the fold in.
- The solid carries the same shared parameters and element tags as the link: plot, unit code, Prototype-FFL and link name. A later button finds a solid the way it finds a link (R368, R383).

### The base against the footprint

- Each placed base boundary is checked against the villa outline of the CAD building footprint, the villa blocks of R300.
- Over a limit in config/project.json the plot is flagged in the check rows. The run never stops on it. The check rows of R363 are built by button 3 before button 4 reads any shape, so how the flag reaches them is open point 3 below.
- How the distance between the two outlines is measured is question QL5 of steps/lane-b.md.

### Links mode

- Links mode replaces each tagged solid with its real link at the same transform, in one transaction with roll back (R382). When one swap fails the whole transaction rolls back and every solid stays.

### Where each mode runs

- The Revit run after 5c: buttons 1 to 4 in Masses, then one NBH in Links.
- 5d: NBH 012 placed in Masses with all 13 prototype shapes, then in Links, the peak of each logged by R310.
- Phase 6: all zones in Masses, one NBH in Links for the final compare.

### Memory

The build peak of about 3 GB, 2,913 MiB on net48 and 3,262 MiB on net8.0 (Q107, steps/PROGRESS.md), is the plan build and not the links. Light mode does not lower it. What light mode changes is the memory of the Revit window, measured in each mode at the Revit run after 5c.

### Open for the Revit run after 5c

- Where the base boundary sits in a prototype file: the element, category or sketch that holds it. Measured on one prototype by the prober, never guessed.

### Open for 5c

Points found by the breaker of L0 that 5c settles in its design before it codes light mode. A point that changes the look goes to Bader.

1. Button 6 sets the link Z at the FFL and RRE_ZZ_SP_Prototype-FFL on every tagged link (section 3, 6 Levels). What it does to a solid.
2. Button 8 counts Links by the tags and the Placed rows of kind link (7.6). How a run in Masses shows in the report, so the counts still agree.
3. The footprint flag is known only after button 4, while R363 builds the check rows in button 3. Whether button 4 adds its rows to the check table, and how "[n] plots follow every rule" counts them.
4. Links mode on a model with no solids, a new site file or a second Links run, must place the links the normal way and never report step 4 done with nothing placed.
5. The shape cache key holds the prototype file hash and a version of the shape reader, so a change of the reader after the Revit run reads every shape again.
6. The category of the solid, and how button 1 binds the shared parameters to it, since 7.3 binds the 9 RRE parameters and CASE on links only. The solid carries every parameter the link carries, not only the four named above.
7. The link types load outside any transaction with the TaskDialog of item 2 of 7.7 (section 3, 4 Place villas), then the swap runs in one transaction.
