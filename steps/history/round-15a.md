# Round 15a, 2026-10-24, plan.json from the past IMC job and the placer step 1

Full record of the round. The top entry of steps/log.md is the one page summary.

## Where

- pwd: /c/Users/bader/Documents/GitHub/site-adaptation-tool-15a, a git worktree. Branch round/addin-15a from main at 5874ae5, level with origin/main at the start.
- This session started inside the worktree, so the main guard hook of .claude/settings.json was live.
- Round 12b ran at the same time in C:\Users\bader\Documents\GitHub\site-adaptation-tool. That folder was not touched. python/cad_analysis.py, python/levels.py, python/levels_proof.py, rules/roshn-makkah.json and config/second_source.json were not changed. New rules take R80 and up.
- Own venv in the worktree, made with Python 3.12 from the install folder. The PATH holds the 12b venv, it was never used.

## Plan, written before the first edit

1. Rules R80 and up in docs/rules.md, amend R1, R54, R55.
2. pastJob block in config/project.json.
3. Pull the R58 yard code of plot_relations.py into functions so the builder reuses it.
4. python/plan_from_past.py and its tests.
5. Run on the real files, proof counts, a second method on 10 random plots.
6. Common: plan models, IPlacerTarget, the Placer logic and math, the report, box helpers.
7. dotnet/SiteAdaptation.Placer, the thin Revit side, the manifest, the solution entry.
8. The two reader fixes.
9. .NET tests on fakes.
10. Gate, commit, PR, a fresh reviewer, fixes, a second short review.
11. Steps for Bader, this file, the log, PROGRESS, merge, remove the worktree.

One step was added while working: python/plan_compare.py (R89), so step f of the Revit run gives a count of matches instead of a look at the model.

## What the inputs hold, counts only

- prototypes.json, saved 24 Oct 01:51: 338 links, 337 with RRE_ZZ_SP_Plot-Number on 13 KEO files and 1 landscape link with none. 94 loaded, 232 unloaded, 12 not found. transformShared on the 94 loaded links. 1010 floors: yard 337, Generic 150mm 336, Ramps 0.5% 280, Ramps 2% 47, Flat Ramps 10. 987 spot elevations.
- Survey point: the offset is on X and Y only, Z offset 0. Angle to true north 0.
- Every link has the Z axis up. The Y axis of a link is the X axis turned by plus 90 degrees, or by minus 90 on the 167 mirrored links, within 0.001. So a mirrored link of the past job is a turn and then a flip of Y, which is what R84 places.
- All 9 shared parameters are text (String storage) on all 337 plot links and none is missing.
- IMC schedule, sheet MP_Phase 1_Zone21, header row 39, 337 rows, the same 337 plot numbers as the links.
- Yard floors: 290 have one outline loop, 47 have two separate pieces, so a floor outline in plan.json is a list of rings.

## Results

- plan_imc_past.json: plots written 337 of 337. Missing by key: floors.yard 337, floors.generic 1 (plot 2441, the known gap). Floors found: Generic 336, yard 0, ramp 337.
- Yard: the R58 rule gives ambiguous on all 337 plots, 2 to 6 candidates each (2 on 33, 3 on 84, 4 on 82, 5 on 37, 6 on 101), because the yards of the neighbours also touch the Generic floor or the ramp within 5 mm. Not guessed. As a measure only, the largest shared edge would give a clear winner on 239 of 337, and 28 yards would then go to two plots, so that tie break does not hold either. Question for Bader.
- Shared position math against transformShared: 94 links, largest difference 0 mm. The reader made transformShared with the same offset, so this proves the builder math and the rounding and not Revit.
- FFL in plan.json against the past link origin Z: 337 of 337 within 1.5 mm, largest difference 0.0 mm.
- Conflicts kept in plan.json: the schedule FFL against RRE_ZZ_SP_Prototype-FFL differs beyond 1.5 mm on 258 plots (112 within 5 mm, 143 within 20 mm, 3 within 100 mm, largest 32 mm). The schedule FFL is kept as Bader chose. Unit against the link file name on 10 plots, all VD1 units placed with the DP1 file, which the profile lists as one typology (DP1/VD1). Unit against the schedule on 2 plots, 2301 and 2302, where the link says VD4 and the schedule and the file say DP4. The link unit is kept. The Generic top equals its computed z (the FFL) within 5 mm on all 336.
- plan_compare.py on the round 9 prototypes.json against its own plan: 337 of 337 full match on every field. This is the baseline for step 21 of steps/round-15a.md.
- prototypes_imc_past.json: a copy of the round 9 prototypes.json, made by me next to it in Documents\SiteAdaptation, byte for byte the same. pastJob.prototypes points at it, because the reader run of step 20 writes prototypes.json again. The plan built from it differs from the first one only in the source file name.

## Second method for the plan builder

- Seed 15, random.Random(15).sample of the 337 written plot numbers: 2395, 2294, 2555, 2307, 2369, 2411, 2297, 2317, 2364, 2477.
- Links read with PowerShell ConvertFrom-Json from the raw prototypes.json, with the survey offset applied in PowerShell. The sheet read from the raw xlsx with zipfile and ElementTree, shared strings and formula text resolved by hand, header row found by the text Plot No. (row 39, 337 rows, as the Excel reader).
- 15 fields per plot: E, N, rotation, mirrored, file, unit, case, RVT-Name, HVAC type, FFL, insert Z, SSL, El @ Entry, Height, steps. 150 checks, 150 matches, 0 mismatches.
- The scripts are in the scratchpad of the session, not in the repo.

## Placer, what it does

- Common holds the decisions: Placer.Run, PlacementMath, the plan models and the report. The Revit side, RevitPlacerTarget, only carries out the steps.
- Coordinates: survey point position and shared position per axis within 1 mm, the angle within 0.001 degrees and 0 in this round. A mismatch stops with a message, nothing is placed and the report is written.
- Steps for one link: RevitLinkInstance.Create with ImportPlacement.Origin, ElementTransformUtils.MoveElement to the internal point, RotateElement about the vertical line through it, and for a mirrored plot ElementTransformUtils.MirrorElements(document, ids, plane, false) across the vertical plane through the point whose normal is the turned Y axis. The API doc of MirrorElements says mirrorCopies false mirrors the elements directly and creates no copy. MirrorElement, the single element call, always makes a copy, so it is not used. CanMirrorElements is asked first.
- Read back: the transform of every placed instance is compared with the plan, origin within 1 mm, rotation within 0.01 degrees, mirror flag. A mismatch rolls the whole run back.
- Link types: one per prototype file, loaded with RevitLinkType.Create and an absolute path. RevitLinkType.Create throws when the document already links that path, so a link type of the document with the same file name is used instead.
- Parameters: written by name as text. Missing, read only or not text is an issue. RRE_ZZ_SP_Prototype-FFL is never written.
- Dry run by default: no transaction, no link loaded. Real run: one transaction with a failures preprocessor that keeps warnings as issues and rolls back on an error.
- Proof on fakes: the fake target carries out the steps with matrix math, so the step order and the mirror plane are tested. Three planted bugs (wrong mirror normal, wrong sign of the offset, the FFL guard removed) were each caught, by 6, 2 and 1 failing tests.
- Unproven until Bader runs Revit: everything the Revit API does with these calls.

## Reader fixes, both unproven until Bader runs Revit

- Link box (R54). The API doc of get_BoundingBox says it returns null when the model box is not known. A link instance holds no model geometry of its own in the host, it lives in the link document, which fits 94 of 94 loaded links giving none. The fix: when the API gives none, the extents of the loaded link document, the union of the boxes of its model elements in link coordinates, go through the link transform. The extents are kept per link document, so 13 files are measured once each. boxSource says api or linkExtents.
- Spot host (R55). The API doc of Dimension.AreReferencesAvailable says it can be false for view specific dimensions whose host references are not available, for example when the view that holds the dimension is closed. Spot elevations are view specific and the reader runs with their views closed, which fits 987 of 987. The fix: the element that holds the spot as a dependent, from GetDependentElements with a spot dimension filter on every floor, ramp, toposurface, toposolid and link instance, worked out once. The API doc says the dependents of an element are the ones deleted with it. hostSource says references or dependents.

## Gate

- Python 323 to 356 passed. .NET 155 to 194 passed.

## Review

Filled in after the review.
