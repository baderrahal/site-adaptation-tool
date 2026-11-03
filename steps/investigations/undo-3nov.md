# Undo in Revit, 3 Nov

R499, Q382. On a copy of the fallback through the real pane, build fea3d964 (first 12 of the SHA-256 of Common.dll), journal 0282. Each key press is in the journal as ID_EDIT_UNDO or ID_EDIT_REDO (READ).

| Press | Rows after | Undo or redo | Rows after |
|---|---|---|---|
| 9 | 9 Done, Run complete | one Ctrl+Z | 9 Ready, box Run 9 |
| (redo) | | one Ctrl+Y | 9 Done, Run complete |
| 3 | 3 Done, 4 Ready, 5 to 9 Need step | one Ctrl+Z | 4 to 9 as before the press |

- One run is one undo entry, RAN on 9 and 3 (R498 group).
- The test of 15:00 was void: that model had just been opened, so its undo stack was empty and no row could change.
- 4 was not proven. Its press stopped with "4 Check changed nothing, the model is as it was. The run failed: 2 Read data was done on other inputs, changed since: picks.json". The Ctrl+Z after it undid the press of 9 before it (9 Ready), as a stopped run leaves no entry. Why picks.json differs on this copy is UNKNOWN, the copy has its work folder copied and a stop is the safe end.
- Save prompt: a close after an undo asks to save (READ journal). After 3, 4 or 9 alone is UNKNOWN, the driver saves after each press.
- The files stay after an undo: the report stays on disk and the undone record no longer matches it, which is why a model not saved after a press of 9 opens on 9 Ready (READ, fallback file 14:57, its report 18:14).
