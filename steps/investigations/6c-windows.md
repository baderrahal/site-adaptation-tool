# 6C-3NOV-C, the window check

Every window of the add-in against its board in docs/design and its rules, through the real windows in Revit 2024.

## How it was checked

- Real windows (RAN): a run on a copy of SA-demo-start.rvt under the window lock, buttons 1 to 9 on NBH 012, driven through the pane by UI automation, 3 Nov 2026. Shots and logs outside git, Documents\SiteAdaptation\check_6c. 43 input files the same by SHA-256 before and after.
- Static (READ): each XAML and text class against its source, by a helper, with its top findings read again by hand. RAN: every button of the XAML has a handler, every literal is in docs/design/14-ui-labels.md, beside the gate test R372.
- Boards exist only for the pane, What the tool read, New project and How it works. The others are held against docs/design/07-mock.md, 12-ui-spec.md and 13-workflow-changes.md. No PNG of steps/ui-shots/ is approved yet, so none is a baseline.
- docs/design/11-light-mode.md is not about colours. It is Villa mode, Masses or Links, and the one thing it asks of a window, the row Villa mode under Dry run, is there. The theme was held against boards/Tokens.dc.html and 12-ui-spec.md instead. All 18 token colours match. CHOSEN, tell me if "the light mode rules" meant something else.

## Per window

Labels are the literals not in 14-ui-labels.md. "Pressed" is RAN in the real window.

| Window | Matches | Differs, left after this round | Dead buttons | Labels |
|---|---|---|---|---|
| Pane | Band, sections and lines, rows, marks, steps, first open, Scope, Run complete | Road surface row (Q403). Next step words shorter than the board. Amber (Q407). 9 Report waits grey, the board shows it blue. No plot list placeholder. Help row right aligned, by its own comment | 0. Pressed: How it works, New project, Run, Open folder, Browse on each row | 0 |
| 1 Set up site | Band, tables, check points, foot | Band words (Q404). Cancel and Dry run 28 high where Tokens says 34. Diff has a chip ground. Current files is a Draft (Q293) | 0. Pressed: Current ticks, Dry run, Apply | 0 |
| What the tool read | Words, counts, tables, conflicts | 4 value rows against 6 (Q289). Plot column shows the CAD label (Q406). Small buttons 28 and combos 30 by 12-ui-spec.md, where the board has 26 and 28 | 0. Pressed: Use the proposed for all, Use these picks | 0 |
| 3 Review data | Title, chips, columns, side box | Confirm all, Confirmed and Close are added by R442, R444, R446 (Q293). Chips have a border where Tokens has none | 0. Pressed: Confirm all, Save review, Close | 0 |
| 4 Check | Title, chips, columns, guide | Tool and Past job are headed text where 12-ui-spec.md says two boxes. Skip has no value. Rows above the chips by R443, R495 | 0. Pressed: Apply, Save overrides path through the run, X | 0 |
| 5 to 8 run boxes | Loader by R444, bar, clock, Hide | The boxes are Revit dialogs and take no theme, against 12-ui-spec.md line 9 | 0. Pressed: Apply, Close, Revit OK | none of its own |
| 9 Report | Run complete, name, Open folder | None. Summary reads not run yet for 1 and 3 (Q307) | 0 | 0 |
| Rules | Title, band, chips, columns | Export to Excel hidden by R444 against 13-workflow-changes.md (Q405). No guide line, no foot | 0. Pressed: chips. Open the manual PDF not pressed | 0 |
| Update from report | Pick line, columns, foot words | Foot lacks its line, Cancel and Dry run 28 high, chip style of its own. Filled state not seen (Q408) | 0. Pressed: Browse, Cancel | 0 |
| New project | Words, marks, band, box, foot | Reopen shows Empty project (Q400). Villa types without the count "without a level", Comes later. Change buttons hidden, CanChange is false | 0 shown. Change has no handler and never shows. Pressed: Browse, Get template, Check files, Save setup, Cancel | 0 |
| Road surfaces | Not seen in Revit | Unknown in Revit (Q403). Static: no band, where docs/design/18-new-project.md says the style of New project | Pick surfaces grey under a past job. Handlers READ only | 0 |
| How it works | Every word, columns, marks, notes | None | 0. Pressed: Close, Open New project | 0 |

## Fixed in this round, R505

Lane C's differences, each with a test seen failing first. PNGs rendered again.

- Theme: buttons react to hover, warn boxes have the amber border of the boards, the Conflicts count has its colour, combo text clears its arrow and ends with an ellipsis.
- Pane: a line between sections, the found mark a green disc, box lines in the text colour, step and chip weights, the Dry run box border, link weight, the first open marks and grey steps.
- New project: band width, the amber sign with no disc, a long name ends with an ellipsis. How it works: band width.
- What the tool read: a file whose only unread parts are external references reads Read, not Not read, and says how many. Seen on the Baseplan, which gave 2,964 plots and read Not read beside them.
- 3 Review data: Not checked is whole. 4 Check: Tool value (rule) no longer runs into Past job.

## Left, by owner

| Owner | Item |
|---|---|
| Lane B | Set up site band words (Q404). Foot heights of Set up site and Update. Diff chip. Check side box against two boxes, Skip value. Rules guide line and foot. Update foot line and chips. Review and Check chip border. The Accepted chip at step 5 shows Showing 0 of 2,347 rows (READ steps/fix-2nov-b-handrun.md, not seen again in this round) |
| Lane A | New project reopen and its hint (Q400, Lane A asked for a question) |
| Lane D | Road surface row, Road surfaces band and first Revit sight (Q403) |
| Lane C, needs an answer | Amber (Q407), Export to Excel (Q405), Conflicts Plot column (Q406) |
| Bader, drafts | Q284, Q286, Q293 |

Board and spec disagree on spacing, Tokens padding 20, 18, 14 against the scale of 12-ui-spec.md, and the XAML keeps the spec, as control 4 says no difference is fixed by changing it. Button text 13 against 14 on the boards and the title bar words stay as they are for the same reason, listed for Q286.

## Re-check of the fixes in Revit

RAN on 58ed3bc, a third run on a fresh copy, New project and buttons 1 to 9, the steps of steps/part1-check.md. Seen in the real windows: Not checked whole, Tool value (rule) clear of Past job, combos end in an ellipsis before the arrow, the Baseplan reads Read with 3 external references. 1,003 floors, 336 villas, 778 walls, 1,339 Placed rows, 688 accepted, box 485. 43 inputs the same by SHA-256. A separator between the found text and the references line was added after this run, tested, not seen again in Revit. Hover and the pane section lines were read from the shots or the XAML, hover cannot show in a still shot, so UNKNOWN in Revit.

The first attempt of this run stopped at button 4: the private config copy had lost its override list, as make_cfg.py removed the folder first and Windows refused a held directory. It now copies over and checks the sizes. The sheet line is that script.

## Found on the way

- A file dialog took over 90 s to close and showed black inside while it did, on the first of each kind. The second dialog of DLUS and sales took over 30 s to appear. Memory 29.6 GB free and CPU low when read a few minutes later. Cause UNKNOWN. It is on the sheet so nobody presses twice.
- ROSHN Makkah (saved) points at C:\SA-demo\config\project.json, older than PR 153, and button 7 stops on addin.lightMode.wallRingOutsideM. The sheet uses a saved project with a config of the pull. Nothing was written under C:\SA-demo.
