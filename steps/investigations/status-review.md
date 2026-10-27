# Status review

Round ADDIN 4g1 PR 3, 2026-10-27. Bader's review list of Q104: the values of the block status of rules/roshn-makkah.json marked CHOSEN by Code, and the values still UNKNOWN that set the position or a level of a plot. A third table, for information, gives the values marked CHOSEN by Bader with the text that gave that status, since a wrong text match there would never be reviewed. The block is written by python/rule_status.py (R401), each fact here is tagged READ with a path, RAN with a count, SEARCHED, CHOSEN or UNKNOWN. No coordinate and no level of a real site is shown.

## Counts

| Status | Before, the audit of PR 68 | After, the block of this PR |
|---|---:|---:|
| CHOSEN by Bader | 3 | 4 |
| MEASURED | 35 | 50 |
| DOCUMENT | not a status then | 275 |
| RECOMMENDED | 9 | 8 |
| CHOSEN by Code | 16 | 16 |
| UNKNOWN | 528 | 238 |
| Values of the two files | 591 | 591 |

Before is section 2 of steps/investigations/rule-status-audit.md (READ). After is RAN by python/rule_status.py and recounted from the JSON by steps/investigations/rule-status-scripts/second_check.ps1 with the same counts (RAN). The changes: DOCUMENT of Q100 takes the 275 values under a client document, 139 of the Rev 06 report, 130 of the DLUS Schedule Rev 9 workbook and 6 base heights of the past job schedules. MEASURED of Q101 takes the 15 fields of parsons2024Rev6 outside keyRules. Q098, closed by Bader, turns its one value from RECOMMENDED to CHOSEN by Bader.

## How the values of table 2 were chosen

- A value is in table 2 when code on the route of a build or a placement reads it and it changes one of: the insert, rotation, mirror, front, link frame, FFL, El @ Entry, SSL, height, steps, a ramp or floor level or outline of a plot. The route is python/plan_from_book.py with what it calls for those fields (plan_rules.py, cad_links.py, corner_front.py, floor_fit.py, override_source.py, plan_from_past.load_floors), python/position_file.py for the project position, and their C# twins in dotnet/SiteAdaptation.Common/Plan/, Read/ and FloorPlacer.cs (CHOSEN).
- Each UNKNOWN value was looked up by its key in python/ and dotnet/, outside the tests, and each reader line of the table was opened and read (SEARCHED, then READ at the line given).
- The native2022 values are listed, though native2022 is never the default (Q050), since a build named with it takes them.
- Left out, 193 of the 238 UNKNOWN values (RAN, the grouping script of this PR):
  - read by no code, 19: the 14 empty cells of typology_codes, steps.riser_height, the rule texts of keyRules site and ssl (SSL is the FFL plus levels.ground_ssl, python/plan_rules.py:670, a DOCUMENT value), native2022 medianOf and steps (READ, no reader found)
  - the data book and the plot table, 54, which number the faces and never move one: dataBook.locationPlan 24, dataBook.wiring 23, dataBook.rev9 2, dataBook.data5 2, connect 3 (the label shifts) (CHOSEN, a number decides which plot a face is, not where it is)
  - measuring and checking only, 92: the majority settings python/majority.py and the checkers read, 61, placerCheck 12, keyPlans 4 of the plans of the answer keys (R325), floors.yardShareTie and floors.yardMinShare 2 of the past plans, entrySpots 1, siteBuild.footprint 3 of the footprint check, cadLinks 2 of the past links, analysis 7 of the analysis clusters a build no longer takes since Q089 (READ, python/plan_from_book.py:1196 to 1290 reads none of them)
  - read only when a build has no level profile, which main never gives (R277), 2: siteBuild.steepUnits and floors.yardBelowFflM (READ, python/plan_from_book.py:835 and 994)
  - the floor type names and the past key type names, 5, floors.types 3 and floors.keyTypes 2, names and no position or level
  - the host and the job, 21: addin.workerPeakMiB 7, testHost 1, siteBuild 13 of the test host, the answer key plans, the checks and the site file, siteBuild.coordinateStepM among them, which moves the internal origin of a new file, never the shared position of a plot (READ, python/site_spec.py:5)
- Values of other statuses are not in table 2: a MEASURED value shows its share, weak ones among them, in the block.

## Found while writing the list

- Row 2 of table 3 is CHOSEN by Bader by the text of Q098, as Bader's answer gives a value an open question named and Bader closed. The text names Duplex_DP4 as a kind of footprint cluster, not the block cluster code D04 of analysis.blockClusters.Duplex_DP4, so the match is likely wrong and the value UNKNOWN (CHOSEN, a reading of the text). Left as the rule gives it, for Bader.
- In the first run of this PR the same value took Q089: the closed file holds the text of Q098 with "since Q089. keyRules" inside it, and the cut of a question text at each "Qnnn. " started a question there. python/rule_status.py now starts a question only at a line start or after a sentence end, the test test_r401_a_question_id_inside_a_sentence_starts_no_question holds it, and --check showed that one entry change (RAN).
- The Q101 tie gives height the share of keyRules ffl, 0.7465 weak, while ffl codes oneFflPerPieceAtTheHighestEntry and height holds oneFflPerClusterAtTheHighestEntry, a candidate of ffl at 0.6868, weak as well. stepBands takes the share of keyRules steps, 0.9877 strong, while the candidate stepBands of that entry holds 0.9336, strong as well. frontRoad is tied to rampFront, 0.6924 weak, whose candidates are the road surfaces, entry, 0.088 weak, measures against the same road (READ, rules/roshn-makkah.json keyRules, CHOSEN).

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

## 2 Values still UNKNOWN that set the position or a level of a plot

| # | What it sets | File and dotted path | Read by | Value |
|---:|---|---|---|---|
| 1 | front: the limits of the front edge of a plot, so its rotation and insert | config/project.json analysis.toleranceM | python/cad_links.py:68, dotnet/SiteAdaptation.Common/Plan/Fronts.cs:235 | 0.01 |
| 2 | front: the share of the long side a party wall holds, in the front limits, so rotation and insert | config/project.json analysis.partyWallShare | python/cad_links.py:68, dotnet/SiteAdaptation.Common/Plan/Fronts.cs:235 | 0.75 |
| 3 | front: the layers the road edges are read from, so the front edge, rotation and insert | config/project.json analysis.roadEdgeLayers | python/plan_from_book.py:1358, dotnet/SiteAdaptation.Common/Read/ReadSettings.cs:49 | ["RD-CURB-LINE"] |
| 4 | front of a corner plot and the side road of the hand, so rotation, insert and mirror | config/project.json siteBuild.corner.roadEdgeM | python/corner_front.py:100, python/plan_from_book.py:1250, dotnet/SiteAdaptation.Common/Plan/BuildSteps.cs:348 | 8.0 |
| 5 | front of a corner plot, how near a road edge counts as on it, so rotation and insert | config/project.json siteBuild.corner.onEdgeM | python/corner_front.py:193 | 0.3 |
| 6 | El @ Entry: the road surfaces the level at the front corners is taken from, so FFL, SSL, height, steps and the ramp | config/project.json siteBuild.roadSurfaces | python/plan_rules.py:280, dotnet/SiteAdaptation.Common/Read/ReadSettings.cs:56 | an input name, not shown |
| 7 | El @ Entry: how far a front corner may lie off a road surface and still take its level | config/project.json siteBuild.snapM | python/plan_rules.py:289, dotnet/SiteAdaptation.Common/Read/ReadSettings.cs:69 | 0.005 |
| 8 | El @ Entry: the largest snapM a run takes | config/project.json siteBuild.snapMaxM | python/plan_rules.py:289, dotnet/SiteAdaptation.Common/Read/ReadSettings.cs:64 | 0.01 |
| 9 | floor outlines: the width step the outline of a ramp, generic or yard floor is looked up by | config/project.json cadLinks.floorWidthStepM | python/floor_fit.py:282, dotnet/SiteAdaptation.Common/Plan/FloorOutlines.cs:42 | 0.5 |
| 10 | floor level: the Revit level the generic floor is placed on | config/project.json floors.levels.generic | python/plan_from_past.py:181, python/plan_from_book.py:1048, dotnet/SiteAdaptation.Common/FloorPlacer.cs:320 | "Level 1" |
| 11 | floor level: the Revit level the yard floor is placed on | config/project.json floors.levels.yard | python/plan_from_past.py:181, python/plan_from_book.py:1048, dotnet/SiteAdaptation.Common/FloorPlacer.cs:320 | "Level 2" |
| 12 | floor level: the Revit level the ramp floor is placed on | config/project.json floors.levels.ramp | python/plan_from_past.py:181, python/plan_from_book.py:1048, dotnet/SiteAdaptation.Common/FloorPlacer.cs:320 | "Level 1" |
| 13 | mirror: the unit kinds that pair in a row of units for the hand | config/project.json majority.pairKinds | python/plan_from_book.py:1200, dotnet/SiteAdaptation.Common/Plan/Hands.cs:114 | ["duplex", "townhouse"] |
| 14 | mirror: the kind of a unit code with prefix VL for the hand | config/project.json majority.kindByPrefix.VL | python/plan_from_book.py:1202, dotnet/SiteAdaptation.Common/Plan/Hands.cs:118 | "villa" |
| 15 | mirror: the kind of a unit code with prefix DP for the hand | config/project.json majority.kindByPrefix.DP | python/plan_from_book.py:1202, dotnet/SiteAdaptation.Common/Plan/Hands.cs:118 | "duplex" |
| 16 | mirror: the kind of a unit code with prefix TH for the hand | config/project.json majority.kindByPrefix.TH | python/plan_from_book.py:1202, dotnet/SiteAdaptation.Common/Plan/Hands.cs:118 | "townhouse" |
| 17 | mirror: the share of the depth a side neighbour shares, so the open end and the row pair of the hand | config/project.json majority.sideShare | python/plan_from_book.py:1241, dotnet/SiteAdaptation.Common/Plan/Hands.cs:122 | 0.5 |
| 18 | overrides: the keys of the parsons source, so which overrides of insert, rotation and levels a build applies | config/project.json majority.groups.parsons | python/plan_from_book.py:1278, dotnet/SiteAdaptation.Common/Plan/PlanOverrides.cs:263 | ["imc", "ima", "imb", "imd", "ime", "imf", "img"] |
| 19 | overrides: the keys of the native source, the same for a native build | config/project.json majority.groups.native | python/plan_from_book.py:1278, dotnet/SiteAdaptation.Common/Plan/PlanOverrides.cs:263 | ["u11", "u12", "u13", "u14"] |
| 20 | overrides: the sources a build applies when it names none (R330) | config/project.json majority.overrideDefaultSources | python/override_source.py:177, dotnet/SiteAdaptation.Common/Plan/PlanOverrides.cs:262 | ["parsons"] |
| 21 | levels: the rounding of the levels and level overrides of a build of parsons2024Rev6 (R385) | config/project.json majority.overrideRounding.levelProfiles.parsons2024Rev6 | python/plan_rules.py:118 | "R385" |
| 22 | levels: the rounding of a build of native2022, never the default (Q050) | config/project.json majority.overrideRounding.levelProfiles.native2022 | python/plan_rules.py:118 | "pythonRound" |
| 23 | insert of every plot: the key the project position file of button 1 is written from (R353) | config/project.json majority.siteReference.parsons | python/position_file.py:565, dotnet/SiteAdaptation.Common/Read/ProjectPositionFile.cs:41 | "imc" |
| 24 | the same for the native group, read only when majority.check.group is native | config/project.json majority.siteReference.native | python/position_file.py:565, dotnet/SiteAdaptation.Common/Read/ProjectPositionFile.cs:41 | "u14" |
| 25 | El @ Entry of a native2022 build: the road surface at the front | rules/roshn-makkah.json levelProfiles.native2022.frontRoad.surface | python/plan_rules.py:279 | an input name, not shown |
| 26 | El @ Entry of a native2022 build: the offset above that road | rules/roshn-makkah.json levelProfiles.native2022.frontRoad.offsetM | python/plan_rules.py:94 | 0.7 |
| 27 | El @ Entry and ramp of a native2022 build: the ramp slope | rules/roshn-makkah.json levelProfiles.native2022.rampSlopePercent.default | python/plan_rules.py:77 | 2.0 |
| 28 | ramp of a native2022 build: the ramp type of the 2 percent class | rules/roshn-makkah.json levelProfiles.native2022.rampTypes.2 | python/plan_from_book.py:349 | "Ramps 2%" |
| 29 | FFL of a native2022 build: the height rule | rules/roshn-makkah.json levelProfiles.native2022.height | python/plan_rules.py:147 | "medianPerUnit" |
| 30 | FFL and height of a native2022 build: the median height of C10 | rules/roshn-makkah.json levelProfiles.native2022.medianHeightM.C10 | python/plan_rules.py:149 | 0.1 |
| 31 | FFL and height of a native2022 build: the median height of DP1 | rules/roshn-makkah.json levelProfiles.native2022.medianHeightM.DP1 | python/plan_rules.py:149 | 0.24 |
| 32 | FFL and height of a native2022 build: the median height of DP2 | rules/roshn-makkah.json levelProfiles.native2022.medianHeightM.DP2 | python/plan_rules.py:149 | 0.12 |
| 33 | FFL and height of a native2022 build: the median height of DP3 | rules/roshn-makkah.json levelProfiles.native2022.medianHeightM.DP3 | python/plan_rules.py:149 | 0.24 |
| 34 | FFL and height of a native2022 build: the median height of DP4 | rules/roshn-makkah.json levelProfiles.native2022.medianHeightM.DP4 | python/plan_rules.py:149 | 0.12 |
| 35 | FFL and height of a native2022 build: the median height of DT1 | rules/roshn-makkah.json levelProfiles.native2022.medianHeightM.DT1 | python/plan_rules.py:149 | 0.254 |
| 36 | FFL and height of a native2022 build: the median height of DT3 | rules/roshn-makkah.json levelProfiles.native2022.medianHeightM.DT3 | python/plan_rules.py:149 | 0.244 |
| 37 | FFL and height of a native2022 build: the median height of TH1 | rules/roshn-makkah.json levelProfiles.native2022.medianHeightM.TH1 | python/plan_rules.py:149 | 0.297 |
| 38 | FFL and height of a native2022 build: the median height of TH1-IM | rules/roshn-makkah.json levelProfiles.native2022.medianHeightM.TH1-IM | python/plan_rules.py:149 | 0.289 |
| 39 | FFL and height of a native2022 build: the median height of TH3 | rules/roshn-makkah.json levelProfiles.native2022.medianHeightM.TH3 | python/plan_rules.py:149 | 0.3 |
| 40 | FFL and height of a native2022 build: the median height of VD1 | rules/roshn-makkah.json levelProfiles.native2022.medianHeightM.VD1 | python/plan_rules.py:149 | 0.28 |
| 41 | FFL and height of a native2022 build: the median height of VD2 | rules/roshn-makkah.json levelProfiles.native2022.medianHeightM.VD2 | python/plan_rules.py:149 | 0.1 |
| 42 | FFL and height of a native2022 build: the median height of VD4 | rules/roshn-makkah.json levelProfiles.native2022.medianHeightM.VD4 | python/plan_rules.py:149 | 0.141 |
| 43 | FFL and height of a native2022 build: the median height of VL4 | rules/roshn-makkah.json levelProfiles.native2022.medianHeightM.VL4 | python/plan_rules.py:149 | 0.182 |
| 44 | FFL and height of a native2022 build: the median height of VL6 | rules/roshn-makkah.json levelProfiles.native2022.medianHeightM.VL6 | python/plan_rules.py:149 | 0.1 |
| 45 | yard level of a native2022 build: null, so a build of it makes no yard | rules/roshn-makkah.json levelProfiles.native2022.yardBelowFflM | python/plan_from_book.py:994 | null |

Rows: 45 of the 238 UNKNOWN values (RAN, python/rule_status.py, each listed value checked UNKNOWN in the block).

## 3 Values CHOSEN by Bader, for information

| # | File and dotted path | Value | Question or line | The text that gave the status |
|---:|---|---|---|---|
| 1 | rules/roshn-makkah.json levelProfiles.default | "parsons2024Rev6" | Q050 | CLAUDE.md and steps/questions-closed.md: parsons2024Rev6 is the level profile of every new build, Rev 6 prevails |
| 2 | config/project.json analysis.blockClusters.Duplex_DP4 | "D04" | Q098 | Q098: Q098. Hand of 5 DP2 plots in DP4 clusters (R354, R357, Round ADDIN 4f PR 2): the build by keyRules takes the footprint clusters alone since Q089. keyRules hand cluster, measured again on them, learned no table for DP2 or DP4, since no footprint cluster of the 7 keys holds such a unit, where the analysis clusters M02 and M04 gave one before. On the whole project 5 DP2 units sit in Duplex_DP4 footprint clusters, 859/86... |
| 3 | config/project.json testHost.imageStepMinutes | 30 | Q061 | R302: - **R302 Image step limit.** The image step of an image job of the test host runs at most testHost.imageStepMinutes of config/project.json, 30 (Bader, Q061). |
| 4 | config/project.json majority.strongShare | 0.9 | the Majority rule of CLAUDE.md | CLAUDE.md: Bader's standing rule since Round 17, for every round |

Rows: 4 (RAN, python/rule_status.py, every value of status CHOSEN by Bader).
