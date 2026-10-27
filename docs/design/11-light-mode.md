## 11. Light mode, the villa as a mass

Bader's decision D2 of 28 Oct 2026, steps/decisions-2026-10-28.md, written in Lane B L0. Its rule id is given at the fold in (steps/lane-b.md). Button 4 of section 3 places each villa as a link. Light mode places it first as one solid, so a test run on a whole zone stays light, and swaps the solids for the links once the shapes are right.

### Villa mode

- Button 4 has a setting Villa mode with two values, Masses and Links.
- Default Masses for test runs (D2). The default of a run Bader starts by hand is question QL4 of steps/lane-b.md.
- The setting sits with the other settings of the pane (section 6) and only button 4 reads it.

### The solid of a villa

- One solid per villa. Its base follows the exact base boundary shape of its prototype, every corner, never a bounding rectangle. This shape is what Bader cares about most.
- The shape is read once per prototype file and cached in the work folder by the prototype file hash. Every villa of that prototype reuses it with its own point, rotation and mirror, the transform button 4 gives the link.
- The height of the solid comes from config/project.json, a key added at the fold in.
- The solid carries the same shared parameters and element tags as the link: plot, unit code, Prototype-FFL and link name. A later button finds a solid the way it finds a link (R368, R383).

### The base against the footprint

- Each placed base boundary is checked against the villa outline of the CAD building footprint, the villa blocks of R300.
- Over a limit in config/project.json the plot is flagged in the check rows (R363). The run never stops on it.
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
