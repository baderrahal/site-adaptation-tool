# build-3, FAST-4-build-3

The plot read and the mirror. Updated 6 Nov 2026.

## What changed

- R730, the plot read. dotnet/SiteAdaptation.Common/DataCheck/DataCheckMarks.cs Build: plots.json `plots` holds the plots a villa goes on (VillaGoesOn: a villa type, any villa code of any source, also two that disagree, or a villa block on it). Every other numbered plot goes to plots.json `otherPlots` with number, areaM2 and `why` (OtherRow). A read with no villa sign on any plot keeps every plot. The plotOutlines line says how many were listed apart. read_plotTable.json follows plots.json, so the plot table holds the villa plots only.
- R731, the mirror from the footprint. dotnet/CadReader/Exploder.cs BlockOf gives each BlockEntry AxisX and AxisY, the world directions of the block axes after the whole chain of inserts (Axis). They are JsonIgnore in CadModels.cs, so blocks.json of R68 is unchanged. dotnet/CadReader/CadScan.cs Inventory with withHand writes `outline`, `axisX` and `axisY` on each insert. dotnet/SiteAdaptation.Common/DataCheck/BlockCover.cs Of gives a block to every plot its outline covers by at least half of what it covers of its best plot, and ranks them along the block axis their centres spread along. Hand gives the scale sign of the insert, turned at each odd rank. DataCheckFind.cs FindMirror calls both. An insert with no outline keeps the old point rule.
- R732, the pair trace. DataCheckFind.cs PairTrace: a typed plot with no block that shares an edge, two corners, with exactly one plot of its type, and that plot likewise, is a pair. The outlines do not say which half is mirrored, so the mirror stays missing and its reason names the other half.
- scripts/scenarios/run_stage1.py `--suffix` runs into a run folder of its own, `_before` for main. score_scenarios.py writes the before table and the line of plots listed apart into steps/scenarios.md.

## What calls what

- DataCheckCommand.Run, ReadDrawing, CadScan.Inventory(withHand true), which reads BlockEntry.Outline, AxisX, AxisY, set by Exploder.BlockOf through Exploder.Axis.
- DataCheckLogic.Run, FindMirror, BlockCover.Of and BlockCover.Hand, then PairTrace. Build, VillaGoesOn, PlotRow or OtherRow.

## Rules for Lead

The rule text for docs/rules is in steps/seam.md, R730 to R732, docs/rules is not on the FAST-4 list.

## Result

PR 240. S1 plots read 2964 to 2618, 346 listed apart, numbers right 1873 kept, mirror right 448 to 735 of 1891. S2 2964 to 2632, mirror 0 to 0, 802 plots traced into pairs. steps/scenarios.md holds both tables.

## Next

Nothing left on FAST-4 after this PR, unless Lead reopens it.

## Final statement

FAST-4 is done. The plot table holds the plots a villa goes on, and every other plot is listed with its reason, 346 on S1. Numbers right stay at 1873. Mirror right on S1 went from 448 to 735 of 1891. S2 has no villa block, so it stays at 0, and its 802 traced pair plots say why their hand is missing. 2618 plots stay against 1891 because the tool cannot tell the hold-out areas. Lead takes R730 to R732 from steps/seam.md into docs/rules. The admin folder .git/worktrees/b3_main_src of a worktree I added and removed for the before run stays behind, Windows denied its delete, git worktree prune is Bader's to run.
