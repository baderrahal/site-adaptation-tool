# Status review

Bader's answer of 2026-10-27 (Q104): all 41 rows of tables 1 and 2 accepted as they are, each CHOSEN by Bader, Q104. python/rule_status.py reads the two tables from this file, never 2b or 3, and --write gave that status in Round ADDIN 4g2 PR 1. The tables stay as Bader reviewed them, the script never rewrites this file.

Round ADDIN 4g1 PR 3, 2026-10-27. Bader's review list of Q104: the values of the block status of rules/roshn-makkah.json marked CHOSEN by Code, and the values still UNKNOWN that set the position or a level of a plot. Two tables for information follow: the UNKNOWN values only a native2022 build reads, and the values marked CHOSEN by Bader with the text that gave that status, since a wrong text match there would never be reviewed. The block is written by python/rule_status.py (R401). Each fact is tagged READ with a path, RAN with a count, SEARCHED, CHOSEN or UNKNOWN. No coordinate and no level of a real site is shown.

Every cite file:line points at the line that reads the key at 97379db, main after PR 2. This PR changes none of the cited files. The script that wrote the tables finds each line by its text and stops when a text is missing (RAN).

## Counts

| Status | Before, the audit of PR 68 | After, the block of this PR | After Bader's review, 4g2 PR 1 |
|---|---:|---:|---:|
| CHOSEN by Bader | 3 | 3 | 44 |
| MEASURED | 35 | 50 | 50 |
| DOCUMENT | not a status then | 273 | 274 |
| RECOMMENDED | 9 | 11 | 8 |
| CHOSEN by Code | 16 | 16 | 0 |
| UNKNOWN | 528 | 242 | 219 |
| Values of the two files | 591 | 595 | 595 |

Before is section 2 of steps/investigations/rule-status-audit.md (READ). After is RAN by python/rule_status.py and recounted from the JSON by steps/investigations/rule-status-scripts/second_check.ps1 with the same counts (RAN). After Bader's review is RAN by python/rule_status.py --write in 4g2 PR 1, 44 entries changed status, and recounted by second_check.ps1 with the same counts (RAN).

- DOCUMENT of Q100 takes the values under a client document: 137 of the Rev 06 report, 130 of the DLUS Schedule Rev 9 workbook and 6 base heights of the past job schedules. Two more of the Rev 06 report, steps.count_rule and steps.riser_added, were RECOMMENDED by Q106 while it was open. Since Q106 was answered (Bader, 2026-10-27) steps.count_rule and the 6 values of steps.legend are DOCUMENT of the past job schedules by a source beside them, and steps.riser_added is UNKNOWN by its source, which names no document (RAN, 4g2 PR 1).
- MEASURED of Q101 takes the 15 fields of parsons2024Rev6 outside keyRules, each tied to the keyRules entry that measures its decision, or to the candidate of that entry it holds.
- 4 values are counted that the audit of PR 68 left out: majority.overrideKeep.laterFields, an empty object, majority.check.group and the job of each level profile, which choose the reference key and the overrides a build takes.
- The one value of Q098, closed now, is RECOMMENDED by Q105 instead, see below. Since Q105 was closed on 2026-10-27 it is UNKNOWN, the answer of Q105 does not name it (RAN, 4g2 PR 1).

## How the values of tables 2 and 2b were chosen

- A value is in table 2 when code on the route of a build or a placement reads it and it changes one of: the insert, rotation, mirror, front, link frame, FFL, El @ Entry, SSL, height, steps, a ramp or floor level or outline of a plot. The route is python/plan_from_book.py with what it calls for those fields (plan_rules.py, cad_links.py, corner_front.py, floor_fit.py, override_source.py, plan_from_past.load_floors), python/position_file.py for the project position, and their C# twins in dotnet/SiteAdaptation.Common/Plan/, Read/ and FloorPlacer.cs (CHOSEN).
- Each UNKNOWN value was looked up by its key in python/ and dotnet/, outside the tests, and each reader line of the tables was opened and read (SEARCHED, then READ at the line given).
- Table 2 holds the values a parsons2024Rev6 build or the add-in reads, Bader's list. Table 2b holds the values only a build named with native2022 reads: those of its level profile and two of config read for it alone, majority.overrideRounding.levelProfiles.native2022 and majority.siteReference.native (python/plan_rules.py:118 takes the rounding of the profile built, python/position_file.py:565 the reference key of majority.check.group, which is parsons). native2022 is a reference profile no build takes by default (Q050).
- Left out, 193 of the 242 UNKNOWN values (RAN, the grouping of the script):
  - read by no code, 19: the 14 empty cells of typology_codes, steps.riser_height, the rule texts of keyRules site and ssl (SSL is the FFL plus levels.ground_ssl, python/plan_rules.py:680, a DOCUMENT value), native2022 medianOf and steps (READ, no reader found)
  - the data book and the plot table, 54, which number the faces and never move one: dataBook.locationPlan 24, dataBook.wiring 23, dataBook.rev9 2, dataBook.data5 2, connect 3, the label shifts (CHOSEN, a number decides which plot a face is, not where it is)
  - measuring and checking only, 92: the majority settings python/majority.py and the checkers read, 61, placerCheck 12, keyPlans 4 of the plans of the answer keys (R325), floors.yardShareTie and floors.yardMinShare 2 of the past plans, entrySpots 1, siteBuild.footprint 3 of the footprint check, cadLinks 2 of the past links, analysis 7 of the analysis clusters a build no longer takes since Q089 (READ, rule_inputs of python/plan_from_book.py, python/plan_from_book.py:1229 to python/plan_from_book.py:1294, reads none of them)
  - read only when a build has no level profile, which main never gives (R277), 2: siteBuild.steepUnits and floors.yardBelowFflM (READ, python/plan_from_book.py:795 and python/plan_from_book.py:1001)
  - the floor type names and the past key type names, 5, floors.types 3 and floors.keyTypes 2, names and no position or level
  - the host and the job, 21: addin.workerPeakMiB 7, testHost 1, siteBuild 13 of the test host, the answer key plans, the checks and the site file, siteBuild.coordinateStepM among them, which moves the internal origin of a new file, never the shared position of a plot (READ, python/site_spec.py:6)
- Values of other statuses are not in table 2: a MEASURED value shows its share, weak ones among them, in the block.

## Found while writing the list

- analysis.blockClusters.Duplex_DP4 first took CHOSEN by Bader from the old text of Q098 moved below the table of steps/questions-closed.md. That text names Duplex_DP4 as a kind of footprint cluster, not the block cluster code D04, so the match was wrong. A closed question now counts by the answer cell of its row of the table alone, never by its question cell or its moved text (test_r401_a_closed_question_counts_by_its_answer_never_by_its_moved_text). The value is now RECOMMENDED by Q105, open, whose text names Duplex_DP4 in the same sense of a footprint cluster, so that match is likely wrong as well (CHOSEN, a reading of the text). Q105 is in Bader's turn, so Bader reads it there. Q105 was closed on 2026-10-27, its answer does not name the value, so it is UNKNOWN again (RAN, 4g2 PR 1).
- With that rule a second entry moved and moved back: the question cell of the row of Q073 names siteBuild.roadSurfaces, which gave it CHOSEN by Bader while the question cell counted. Its answer cell does not name the value, so it stays UNKNOWN (RAN, --check).
- Before that, a question text was cut at each "Qnnn. ", and "since Q089. keyRules" inside the text of Q098 started a question there. python/rule_status.py now starts a question only at a line start or after a sentence end (test_r401_a_question_id_inside_a_sentence_starts_no_question).
- The Q101 ties after review pass 1 of this PR: frontRoad.offsetM, a term of El @ Entry, goes to keyRules entry, 0.088 weak, and frontRoad.surface to rampFront, 0.6924 weak, the share of its candidate roadLevel. height takes the share of the candidate it holds, oneFflPerClusterAtTheHighestEntry of keyRules ffl, 0.6868 weak, and stepBands the candidate stepBands of keyRules steps, 0.9336 strong. The field steps, countRule, is read by no code, its entry says so (READ, rules/roshn-makkah.json keyRules, CHOSEN).
- The steps values rules/README.md ties to the schedules took DOCUMENT of the Rev 06 report by the top source. Q106 asks Bader which document they belong to, and while it is open steps.count_rule and steps.riser_added are RECOMMENDED by it. The 6 values of steps.legend, a list of pairs, have no name of their own the text can match, so they stayed DOCUMENT of the Rev 06 report (RAN, --check). Since Q106 was answered on 2026-10-27 they are DOCUMENT of the past job schedules by steps.legend_source (RAN, 4g2 PR 1).

## 1 Values CHOSEN by Code

| # | What it sets | File and dotted path | Read by | Value | Where the mark is |
|---:|---|---|---|---|---|
| 1 | the minutes an image job of the test host may run past its image step, for the open and close of the model | config/project.json testHost.imageMarginMinutes | dotnet/SiteAdaptation.TestHost/Run-TestHost.ps1:208 | 10 | note |
| 2 | how often the test host samples memory and responding of its Revit | config/project.json testHost.sampleSeconds | dotnet/SiteAdaptation.TestHost/Run-TestHost.ps1:96 | 5 | note |
| 3 | the limit of each CIM call of a sample of the test host | config/project.json testHost.cimTimeoutSeconds | dotnet/SiteAdaptation.TestHost/Run-TestHost.ps1:103 | 2 | note |
| 4 | how near a wall middle must lie to a generic floor edge to go to that plot, in the plan of an answer key (R325) | config/project.json keyPlans.wallEdgeM | python/plan_from_past.py:1036 | 0.3 | note |
| 5 | how near a spot must lie to a corner of the entrance edge (R327) | config/project.json entrySpots.cornerM | python/entry_spots.py:46 | 0.05 | note |
| 6 | the bands a spot value is held to El @ Entry in (R327) | config/project.json entrySpots.bandsM | python/entry_spots.py:49 | [0.001, 0.005] | note |
| 7 | the peak memory of a whole read command of the worker (R347) | config/project.json addin.workerPeakMiB.commands.read | dotnet/SiteAdaptation.Common/Read/AddinConfig.cs:85 | 3900 | R347 |
| 8 | the time limit of a read of the worker (R347) | config/project.json addin.workerTimeLimitS.read | dotnet/SiteAdaptation.Common/Read/AddinConfig.cs:114 | 300 | R347 |
| 9 | the time limit of a build of the worker (R347) | config/project.json addin.workerTimeLimitS.build | dotnet/SiteAdaptation.Common/Read/AddinConfig.cs:114 | 600 | R347 |
| 10 | the time limit of the level Excel of the worker (R347) | config/project.json addin.workerTimeLimitS.levelExcel | dotnet/SiteAdaptation.Common/Read/AddinConfig.cs:114 | 60 | R347 |
| 11 | the time limit of the report Excel of the worker (R347) | config/project.json addin.workerTimeLimitS.reportExcel | dotnet/SiteAdaptation.Common/Read/AddinConfig.cs:114 | 600 | R347 |
| 12 | the time limit of the check export of the worker (R347) | config/project.json addin.workerTimeLimitS.exportChecks | dotnet/SiteAdaptation.Common/Read/AddinConfig.cs:114 | 600 | R347 |
| 13 | the time limit of the position file of the worker (R347) | config/project.json addin.workerTimeLimitS.position | dotnet/SiteAdaptation.Common/Read/AddinConfig.cs:114 | 600 | R347 |
| 14 | the shortest time limit a worker launch takes (R347) | config/project.json addin.workerTimeLimitMinS | dotnet/SiteAdaptation.Common/Read/AddinConfig.cs:127 | 10 | R347 |
| 15 | the decimals the gap of a check point of button 1 is shown and judged at (R353) | config/project.json addin.position.gapDecimals | dotnet/SiteAdaptation.Common/Read/ProjectPositionFile.cs:24 | 2 | addin.position.note |
| 16 | the decimals the overlap of a footprint block with its face is written at before the order (R352) | config/project.json addin.plotTable.overlapDecimals | dotnet/SiteAdaptation.Common/Read/PlotTable.cs:164 | 4 | addin.plotTable.note |

Rows: 16 (RAN, python/rule_status.py, every value of status CHOSEN by Code).

## 2 Values still UNKNOWN that set the position or a level of a plot in a parsons2024Rev6 build or the add-in

| # | What it sets | File and dotted path | Read by | Value |
|---:|---|---|---|---|
| 1 | front: the limits of the front edge of a plot, so its rotation and insert | config/project.json analysis.toleranceM | python/cad_links.py:68, dotnet/SiteAdaptation.Common/Plan/Fronts.cs:235 | 0.01 |
| 2 | front: the share of the long side a party wall holds, in the front limits, so rotation and insert | config/project.json analysis.partyWallShare | python/cad_links.py:68, dotnet/SiteAdaptation.Common/Plan/Fronts.cs:235 | 0.75 |
| 3 | front: the layers the road edges are read from, so the front edge, rotation and insert | config/project.json analysis.roadEdgeLayers | python/plan_from_book.py:1365, dotnet/SiteAdaptation.Common/Read/ReadSettings.cs:49 | ["RD-CURB-LINE"] |
| 4 | front of a corner plot and the side road of the hand, so rotation, insert and mirror | config/project.json siteBuild.corner.roadEdgeM | python/corner_front.py:100, python/plan_from_book.py:1257, dotnet/SiteAdaptation.Common/Plan/BuildSteps.cs:348 | 8.0 |
| 5 | front of a corner plot, how near a road edge counts as on it, so rotation and insert | config/project.json siteBuild.corner.onEdgeM | python/corner_front.py:193 | 0.3 |
| 6 | El @ Entry: the road surfaces the level at the front corners is taken from, so FFL, SSL, height, steps and the ramp | config/project.json siteBuild.roadSurfaces | python/plan_rules.py:280, dotnet/SiteAdaptation.Common/Read/ReadSettings.cs:56 | an input name, not shown |
| 7 | El @ Entry: how far a front corner may lie off a road surface and still take its level | config/project.json siteBuild.snapM | python/plan_rules.py:289, dotnet/SiteAdaptation.Common/Read/ReadSettings.cs:69 | 0.005 |
| 8 | El @ Entry: the largest snapM a run takes | config/project.json siteBuild.snapMaxM | python/plan_rules.py:289, dotnet/SiteAdaptation.Common/Read/ReadSettings.cs:64 | 0.01 |
| 9 | floor outlines: the width step the outline of a ramp, generic or yard floor is looked up by | config/project.json cadLinks.floorWidthStepM | python/floor_fit.py:282, dotnet/SiteAdaptation.Common/Plan/FloorOutlines.cs:42 | 0.5 |
| 10 | floor level: the Revit level the generic floor is placed on | config/project.json floors.levels.generic | python/plan_from_past.py:181, python/plan_from_book.py:1055, dotnet/SiteAdaptation.Common/FloorPlacer.cs:320 | "Level 1" |
| 11 | floor level: the Revit level the yard floor is placed on | config/project.json floors.levels.yard | python/plan_from_past.py:181, python/plan_from_book.py:1055, dotnet/SiteAdaptation.Common/FloorPlacer.cs:320 | "Level 2" |
| 12 | floor level: the Revit level the ramp floor is placed on | config/project.json floors.levels.ramp | python/plan_from_past.py:181, python/plan_from_book.py:1055, dotnet/SiteAdaptation.Common/FloorPlacer.cs:320 | "Level 1" |
| 13 | mirror: the unit kinds that pair in a row of units for the hand | config/project.json majority.pairKinds | python/plan_from_book.py:1207, dotnet/SiteAdaptation.Common/Plan/Hands.cs:114 | ["duplex", "townhouse"] |
| 14 | mirror: the kind of a unit code with prefix VL for the hand | config/project.json majority.kindByPrefix.VL | python/plan_from_book.py:1209, dotnet/SiteAdaptation.Common/Plan/Hands.cs:118 | "villa" |
| 15 | mirror: the kind of a unit code with prefix DP for the hand | config/project.json majority.kindByPrefix.DP | python/plan_from_book.py:1209, dotnet/SiteAdaptation.Common/Plan/Hands.cs:118 | "duplex" |
| 16 | mirror: the kind of a unit code with prefix TH for the hand | config/project.json majority.kindByPrefix.TH | python/plan_from_book.py:1209, dotnet/SiteAdaptation.Common/Plan/Hands.cs:118 | "townhouse" |
| 17 | mirror: the share of the depth a side neighbour shares, so the open end and the row pair of the hand | config/project.json majority.sideShare | python/plan_from_book.py:1248, dotnet/SiteAdaptation.Common/Plan/Hands.cs:122 | 0.5 |
| 18 | overrides: the keys of the parsons source, so which overrides of insert, rotation and levels a build applies | config/project.json majority.groups.parsons | python/plan_from_book.py:1285, dotnet/SiteAdaptation.Common/Plan/PlanOverrides.cs:263 | ["imc", "ima", "imb", "imd", "ime", "imf", "img"] |
| 19 | overrides: the keys of the native source, which a parsons build tells apart from its own | config/project.json majority.groups.native | python/plan_from_book.py:1285, dotnet/SiteAdaptation.Common/Plan/PlanOverrides.cs:263 | ["u11", "u12", "u13", "u14"] |
| 20 | overrides: the sources a build applies when it names none (R330) | config/project.json majority.overrideDefaultSources | python/override_source.py:177, dotnet/SiteAdaptation.Common/Plan/PlanOverrides.cs:262 | ["parsons"] |
| 21 | overrides: the fields of each job left to a later phase, which a build leaves unwritten | config/project.json majority.overrideKeep.laterFields | python/plan_from_book.py:1286, dotnet/SiteAdaptation.Common/Plan/PlanOverrides.cs:506 | {} |
| 22 | levels: the rounding of the levels and level overrides of a build of parsons2024Rev6 (R385) | config/project.json majority.overrideRounding.levelProfiles.parsons2024Rev6 | python/plan_rules.py:118 | "R385" |
| 23 | insert of every plot: the group whose reference key the project position file is written from (R353) | config/project.json majority.check.group | python/position_file.py:564, dotnet/SiteAdaptation.Common/Read/ProjectPositionFile.cs:40 | "parsons" |
| 24 | insert of every plot: the key the project position file of button 1 is written from (R353) | config/project.json majority.siteReference.parsons | python/position_file.py:565, dotnet/SiteAdaptation.Common/Read/ProjectPositionFile.cs:41 | "imc" |
| 25 | overrides and levels: the job of the profile, whose overrides and later fields a build applies | rules/roshn-makkah.json levelProfiles.parsons2024Rev6.job | python/plan_rules.py:438, python/plan_from_book.py:1286, dotnet/SiteAdaptation.Common/Plan/PlanOverrides.cs:327 | "parsons" |

Rows: 25 (RAN, python/rule_status.py, each listed value checked UNKNOWN in the block).

## 2b Values still UNKNOWN read only by a build of the reference profile native2022, which no build takes by default (Q050), for information

| # | What it sets | File and dotted path | Read by | Value |
|---:|---|---|---|---|
| 1 | levels: the rounding of a build of native2022 | config/project.json majority.overrideRounding.levelProfiles.native2022 | python/plan_rules.py:118 | "pythonRound" |
| 2 | the reference key of the native group, read only when majority.check.group is native | config/project.json majority.siteReference.native | python/position_file.py:565, dotnet/SiteAdaptation.Common/Read/ProjectPositionFile.cs:41 | "u14" |
| 3 | overrides of a native2022 build: the job of the profile | rules/roshn-makkah.json levelProfiles.native2022.job | python/plan_rules.py:438 | "native" |
| 4 | El @ Entry of a native2022 build: the road surface at the front | rules/roshn-makkah.json levelProfiles.native2022.frontRoad.surface | python/plan_rules.py:279 | an input name, not shown |
| 5 | El @ Entry of a native2022 build: the offset above that road | rules/roshn-makkah.json levelProfiles.native2022.frontRoad.offsetM | python/plan_rules.py:94 | 0.7 |
| 6 | El @ Entry and ramp of a native2022 build: the ramp slope | rules/roshn-makkah.json levelProfiles.native2022.rampSlopePercent.default | python/plan_rules.py:77 | 2.0 |
| 7 | ramp of a native2022 build: the ramp type of the 2 percent class | rules/roshn-makkah.json levelProfiles.native2022.rampTypes.2 | python/plan_from_book.py:350 | "Ramps 2%" |
| 8 | FFL of a native2022 build: the height rule | rules/roshn-makkah.json levelProfiles.native2022.height | python/plan_rules.py:147 | "medianPerUnit" |
| 9 | FFL and height of a native2022 build: the median height of C10 | rules/roshn-makkah.json levelProfiles.native2022.medianHeightM.C10 | python/plan_rules.py:149 | 0.1 |
| 10 | FFL and height of a native2022 build: the median height of DP1 | rules/roshn-makkah.json levelProfiles.native2022.medianHeightM.DP1 | python/plan_rules.py:149 | 0.24 |
| 11 | FFL and height of a native2022 build: the median height of DP2 | rules/roshn-makkah.json levelProfiles.native2022.medianHeightM.DP2 | python/plan_rules.py:149 | 0.12 |
| 12 | FFL and height of a native2022 build: the median height of DP3 | rules/roshn-makkah.json levelProfiles.native2022.medianHeightM.DP3 | python/plan_rules.py:149 | 0.24 |
| 13 | FFL and height of a native2022 build: the median height of DP4 | rules/roshn-makkah.json levelProfiles.native2022.medianHeightM.DP4 | python/plan_rules.py:149 | 0.12 |
| 14 | FFL and height of a native2022 build: the median height of DT1 | rules/roshn-makkah.json levelProfiles.native2022.medianHeightM.DT1 | python/plan_rules.py:149 | 0.254 |
| 15 | FFL and height of a native2022 build: the median height of DT3 | rules/roshn-makkah.json levelProfiles.native2022.medianHeightM.DT3 | python/plan_rules.py:149 | 0.244 |
| 16 | FFL and height of a native2022 build: the median height of TH1 | rules/roshn-makkah.json levelProfiles.native2022.medianHeightM.TH1 | python/plan_rules.py:149 | 0.297 |
| 17 | FFL and height of a native2022 build: the median height of TH1-IM | rules/roshn-makkah.json levelProfiles.native2022.medianHeightM.TH1-IM | python/plan_rules.py:149 | 0.289 |
| 18 | FFL and height of a native2022 build: the median height of TH3 | rules/roshn-makkah.json levelProfiles.native2022.medianHeightM.TH3 | python/plan_rules.py:149 | 0.3 |
| 19 | FFL and height of a native2022 build: the median height of VD1 | rules/roshn-makkah.json levelProfiles.native2022.medianHeightM.VD1 | python/plan_rules.py:149 | 0.28 |
| 20 | FFL and height of a native2022 build: the median height of VD2 | rules/roshn-makkah.json levelProfiles.native2022.medianHeightM.VD2 | python/plan_rules.py:149 | 0.1 |
| 21 | FFL and height of a native2022 build: the median height of VD4 | rules/roshn-makkah.json levelProfiles.native2022.medianHeightM.VD4 | python/plan_rules.py:149 | 0.141 |
| 22 | FFL and height of a native2022 build: the median height of VL4 | rules/roshn-makkah.json levelProfiles.native2022.medianHeightM.VL4 | python/plan_rules.py:149 | 0.182 |
| 23 | FFL and height of a native2022 build: the median height of VL6 | rules/roshn-makkah.json levelProfiles.native2022.medianHeightM.VL6 | python/plan_rules.py:149 | 0.1 |
| 24 | yard level of a native2022 build: null, so a build of it makes no yard | rules/roshn-makkah.json levelProfiles.native2022.yardBelowFflM | python/plan_from_book.py:1001 | null |

Rows: 24 (RAN, python/rule_status.py, each listed value checked UNKNOWN in the block).

Bader's review list is table 1 and table 2: 41 rows. Tables 2 and 2b together hold 49 of the 242 UNKNOWN values.

## 3 Values CHOSEN by Bader, for information

| # | File and dotted path | Value | Question or line | The text that gave the status |
|---:|---|---|---|---|
| 1 | rules/roshn-makkah.json levelProfiles.default | "parsons2024Rev6" | Q050 | CLAUDE.md and steps/questions-closed.md: parsons2024Rev6 is the level profile of every new build, Rev 6 prevails |
| 2 | config/project.json testHost.imageStepMinutes | 30 | Q061 | R302: - **R302 Image step limit.** The image step of an image job of the test host runs at most testHost.imageStepMinutes of config/project.json, 30 (Bader, Q061). |
| 3 | config/project.json majority.strongShare | 0.9 | the Majority rule of CLAUDE.md | CLAUDE.md: Bader's standing rule since Round 17, for every round |

Rows: 3 (RAN, python/rule_status.py, every value of status CHOSEN by Bader).
