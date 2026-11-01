# Folder cleanup, 2 Nov 2026

Done by Lane C on Bader's resume prompt. Moves only, nothing was deleted. Every moved folder is in C:\_cleanup-1nov under its own name. To put one back, move it back to C:\Users\p003653k\OneDrive - Parsons Corp\Documents\GitHub\ under the same name. If a lane needs one, it tells lane-c and the move back is noted here.

How each move was checked:

- The lane that made the folder said it is free (lane-b and lane-d by message, Lane C's own h1 to h4).
- No config, script, saved project or add-in manifest names the folder by path (SEARCHED).
- No running process names it on its command line (RAN).
- Git could no longer read 38 of the helper folders, so each file was hashed as a git blob and looked up among the 12,417 objects reachable from origin. A file not found sends the folder to ASK (RAN, outside bin, obj and .git).

## MOVED, 36 folders into C:\_cleanup-1nov

| Folders | Made by | Files checked per folder | Not on origin |
|---|---|---|---|
| Lane B 5b logic, 5b window | Lane B | 639 to 643 | 0 |
| Lane B 5c check, fix, host, joins, logic, pane, place, plan, read, review, rules, table, ui | Lane B | 679 to 786 | 0 |
| Lane B 5d button8, fix1, fix2, fix3, floors, protos, q156, run | Lane B | 797 to 809 | 0 |
| Lane B 6a base, dev, run, runlog, shots | Lane B | 838 to 897 | 0 |
| Lane B fix1nov coords, fix1nov masses | Lane B | 924 to 933 | 0 |
| Site Adaptation Tool Lane C h1, h2, h3, h4 | Lane C | 852 to 894 | 0 |
| Site Adaptation Tool Lane D read, Lane D run | Lane D | 912 to 933 | 0 |

## KEEP

| What | Why |
|---|---|
| C:\02-Site Adaptation, C:\10- ROSHEN MECCA | Real inputs, Bader's list |
| C:\SA-demo, C:\SA-boards | Bader's list. Lane B Task 6 uses C:\SA-demo now |
| site-adaptation-tool (the Lane A folder), Lane B WT, Lane C WT, Lane D WT | Live worktrees of the lanes |
| Documents\SiteAdaptation | The work root. Config and saved projects point at it. Lane A parity and Lane B Task 6 read and write there now |
| %APPDATA%\Autodesk\Revit\Addins\2024 | The installed add-ins |
| OneDrive sync roots | Bader's list |
| PAR_NWC-Federator, RCRC-Green, Rhino-To-Civil3D-V2 | Other repos, not made by these sessions |
| Everything else in C:\ | Not made by these sessions |

## ASK, left in place for Bader

| What | What is in it | Recommended |
|---|---|---|
| Lane B 5d q116 | 18 files under sample_output\round4c not on origin, run outputs that git ignores | Move it, the outputs are from Round 4c |
| Lane B 5d walls | The same 18 sample_output files and docs\rules\07-addin-r354-r388.md, a version not on origin | Move it, Lane B says it is free and 5d is merged. Lane B can check the rule file first |
| Lane B fix1nov run | config\project.json, a version not on origin. Lane B says it is free | Move it, FIX-1NOV-B is merged |
| scratch, next to the repo | Empty, made on 30 Oct, maker unknown | Leave it or move it, it holds nothing |
| .claude\worktrees in the Lane A folder, 39 folders, 31 of them registered worktrees | Old helper worktrees and the parity folders p4i, p4j2, p1nov. Lane A uses p1nov-base and p1nov-head now. The folder is on Bader's never touch list | Review after FIX-1NOV-A2 merges. Each has its own branch, so each needs a check for commits not on main first |
| Old run folders in Documents\SiteAdaptation, such as lane_b_5a_run3, lane_b_5b, lane_b_5c, lane_b_5d, lane_b_6a, lane_d_4j1 | Run evidence the records may cite | Leave them, or review once the lanes are idle |
| 46 leftover records in .git\worktrees of the Lane A folder | What remains of removed worktrees. git worktree prune stops on each with Permission denied, and it was the same before this cleanup | Harmless. Each git fetch and prune prints these lines. Try again after a restart with no session open |

## Git

- git worktree prune ran. The Lane D run entry left the list. The 4 live worktrees still read their branches (RAN).
- Remote branches deleted, each an ancestor of main when deleted: feat/addin-4j2-fix, feat/addin-4j2-split, feat/addin-4j2-tests, feat/addin-4j2-walls, fix/1nov-a-joints, fix/1nov-a-joints2, fix/1nov-a-rows.
- Remote branches left, each with commits not on main: fix/1nov-a2, fix/1nov-a2-picks, fix/1nov-b-task6, fix/1nov-c, fix/1nov-d3. Delete fix/1nov-a2-picks only after fix/1nov-a2 merges and a check that its commits are on main.

The moved folders sat inside the OneDrive folder, so OneDrive treats them as removed from the cloud copy. They stay on this disk in C:\_cleanup-1nov.
