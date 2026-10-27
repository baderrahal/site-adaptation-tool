# Rule status audit

Round AUDIT-RULES, 2026-10-27, on main at 69a3711. The status of every rule value, no logic change. Each fact is tagged READ with a path, RAN with a count, SEARCHED, CHOSEN or UNKNOWN. The sections are found by grep on their heading, the file is over 30 KB.

## 1 Method

- Statuses, as the round prompt gives them: CHOSEN by Bader (an answer in steps/questions-closed.md or a Bader line in CLAUDE.md), MEASURED (taken from the past models by the majority rule, with a share and keys), RECOMMENDED (an open question carries it), CHOSEN by Code (a threshold or setting Code set with no answer from Bader), UNKNOWN (none of the above can be shown).
- The statuses come from one script, steps/investigations/rule-status-scripts/audit.py, kept in the repo so every count tagged RAN can be run again: `.venv/Scripts/python -I steps/investigations/rule-status-scripts/audit.py . <folder>`. No status is typed by hand. It replaced the first script after review pass 1 of PR 68, which found a status given by a Q anywhere in a sentence and CHOSEN by Code by absence, and took the fixes of pass 2.
- A value is a leaf of the JSON, a scalar or a list of scalars. Kinds: value, a design value, and host, a value of the host program such as a time limit or a memory peak. Both get a status and both count in the values below.
- Not values, counted and given no status: note text, the measurement records of a keyRule or hand rule (plots, misses, byKey, candidates and such), input names and locations (file, folder, sheet and parameter names of the real inputs), and settings of a reader or check with no design value (parity lists, shared parameter GUIDs, the answer key list, prototype names, the snapshot list, the text patterns of the readers of dataBook and connect).
- Evidence of a value, its own text only: its own note (a key named with Note added), the note, why or question of its parent when the parent has at most 8 leaves, the sentences of the rule lines of docs/rules/ and of every note of the two files that name it by its last two keys (three when the two are a path of their own in a file), by its own key name when that is 9 letters or more and unique in the two files, or by its key in a list after its parent, as workerTimeLimitS, build, reportExcel, the text of each open question that names it, and a Q ID in its path.
- A mark in that text counts within 80 characters of a name of the value, or when the last camelCase key named before it is the value's, as a note writes sampleSeconds is how often ... every 5 s (CHOSEN, Round ADDIN). So a Q about another key of the same sentence does not count. In its own note or the note of an object of at most 3 leaves a mark counts anywhere, in the note of an object of at most 8 leaves in the sentence that names the value, and an open question counts for the values its text names.
- Order: a closed Q whose answer names Bader, or with Bader within 40 characters of it as R302 writes (Bader, Q061), gives CHOSEN by Bader. Else an open Q of Q001 to Q099 gives RECOMMENDED. Else the word CHOSEN gives CHOSEN by Code. A keyRules entry with a share, and the hands rules, give MEASURED with the share, strong or weak against majority.strongShare and the keys of byKey. majority.strongShare is CHOSEN by Bader by the Majority rule of CLAUDE.md, levelProfiles.default by Q050.
- Anything else is UNKNOWN, with the nearest source above the value when there is one. Absence of a mark never gives CHOSEN by Code.
- Open questions are the 67 IDs of Bader's turn of steps/PROGRESS.md before Q100, as Q100 to Q103 come from this audit, closed ones the 32 of the table of steps/questions-closed.md, 14 of whose answers name Bader (RAN, audit.py).
- A rule takes every status of the values it names and of the values whose evidence names it, every closed Q of Bader and open Q its line names, every open question whose text names the rule and every closed answer of Bader that names it, MEASURED with the share of each keyRules entry it names, and CHOSEN by Code for the word CHOSEN. A rule with a value, a number with a unit or a key, and none of those is UNKNOWN. A rule with nothing of these is process, no value.
- Limits: the match is by text, so a status rests on the words of the records, not on a reading of each rule. Each row gives its evidence so a reader can check it. The rule-level status is the set of the statuses of its values, not one status.
- Second check by another tool: PowerShell, ConvertFrom-Json and Select-String in place of Python, recounted the rule ids, the MEASURED shares and the sums of section 5 by status (section 12).

## 2 Counts

| What | Count | Source |
|---|---:|---|
| Rule ids of docs/rules/index.md | 316 | RAN, audit.py |
| of them process, no value | 128 | RAN |
| of them with a value | 188 | RAN |
| rules with UNKNOWN | 170 | RAN, a rule may hold several statuses |
| rules with RECOMMENDED | 39 | RAN |
| rules with MEASURED | 20 | RAN |
| rules with CHOSEN by Bader | 17 | RAN |
| rules with CHOSEN by Code | 10 | RAN |
| Leaves of rules/roshn-makkah.json | 2,798 | RAN |
| Leaves of config/project.json | 853 | RAN |
| Values of the two files, kinds value and host | 591 | RAN |
| CHOSEN by Bader | 3 | RAN, Q050 1, Q061 1, the Majority rule of CLAUDE.md 1 |
| MEASURED | 35 | RAN, 32 keyRules entries and 3 hands rules, 9 of them weak |
| RECOMMENDED | 9 | RAN, Q017 3, Q002, Q037, Q066, Q085, Q086 and Q098 1 each |
| CHOSEN by Code | 16 | RAN, the word CHOSEN in R347, in the root note and in the notes of addin.position and addin.plotTable |
| UNKNOWN | 528 | RAN, rules 330, config 198 |
| Overrides of config/overrides/roshn-makkah.json | 12,421 | RAN, all RECOMMENDED by Q053 |
| Cannot list of the overrides file | 154 | RAN |
| Design values in code, rows | 120 | RAN by the Explore helper, 14 duplicate a config key, 106 not in config |

The 528 UNKNOWN by evidence (RAN): rules/roshn-makkah.json 139 under the top source, the Rev 06 report with Rev 01 for foundations, 130 under typology_codes.source, the DLUS Schedule Rev 9 workbook, 6 under steps.base_height_source, past job schedules of 747 plots, 15 fields of parsons2024Rev6 and 21 of native2022 under the source of their profile with no share, 2 keyRules entries with no share (site and ssl), 17 null. config/project.json 195 with no Q, no share, no Bader line and no CHOSEN mark, 3 null.

## 3 Rules with a value

Where the value lives names the keys of config/project.json or rules/roshn-makkah.json the rule line names, at most 4, else the rule text. The Q ID or share column gives the Q IDs, the standing rule or the CHOSEN mark, then each keyRules or hands entry with its share (RAN, audit.py).

| Rule | Title | Where the value lives | Status | Q ID or share |
|---|---|---|---|---|
| R2 | Round to 3 decimals. | the rule text | UNKNOWN |  |
| R27 | Link summary. | the rule text | UNKNOWN |  |
| R29 | Master Plan roles and skip list. | the rule text | UNKNOWN |  |
| R31 | Second source check. | the rule text | UNKNOWN |  |
| R33 | Face and label reasons. | the rule text | UNKNOWN |  |
| R38 | Link name parse. | parameter RRE_ZZ_SP_RVT-Name | MEASURED | parameter RRE_ZZ_SP_RVT-Name 0.9916 |
| R45 | Gap bands. | the rule text | UNKNOWN |  |
| R47 | Ramp corner levels. | the rule text | UNKNOWN |  |
| R48 | Role proposal. | the rule text | UNKNOWN |  |
| R49 | Floor count per plot. | the rule text | UNKNOWN |  |
| R53 | Parameter details and floor types. | floorTypes | UNKNOWN |  |
| R58 | Yard and shared edges. | the rule text | UNKNOWN |  |
| R59 | Ramp geometry. | Flat Ramps | RECOMMENDED | Q002 |
| R60 | Gaps, overlaps and link box. | the rule text | UNKNOWN |  |
| R61 | Relation summary. | the rule text | UNKNOWN |  |
| R62 | Project profile. | cluster_aliases, clusters.Q01, riser_height | UNKNOWN |  |
| R63 | Project settings. | blockClusters, noRev6Match, partyWallShare, roadEdgeLayers | RECOMMENDED, UNKNOWN | Q098 |
| R64 | Cluster derivation. | analysis.endShare, analysis.partyWallShare, analysis.toleranceM, lo... | UNKNOWN |  |
| R66 | Only listed clusters are valid. | typologies | UNKNOWN |  |
| R67 | FFL, SSL and steps. | baseHeight, base_height, base_height.default, count_rule | MEASURED, UNKNOWN | baseHeight 0.8433 |
| R69 | Unit code per plot. | analysis.unitPlotTypes, unitPlotTypes | UNKNOWN |  |
| R70 | Row split. | analysis.pairRule, typologies | UNKNOWN |  |
| R71 | Front by road edge. | analysis.endShare, analysis.roadEdgeLayers, analysis.toleranceM, lo... | UNKNOWN |  |
| R73 | Block scan. | analysis.noRev6Match, analysis.unitPlotTypes, cluster_aliases, noRe... | UNKNOWN |  |
| R75 | Cluster with no Rev 6 match. | analysis.noRev6Match, cluster_aliases, noRev6Match | UNKNOWN |  |
| R81 | Plan from the past job. | the rule text | UNKNOWN |  |
| R82 | Shared position proof. | the rule text | UNKNOWN |  |
| R83 | Coordinates check. | the rule text | UNKNOWN |  |
| R84 | Villa link placement. | the rule text | UNKNOWN |  |
| R89 | Plan against the placed model. | the rule text | UNKNOWN |  |
| R103 | Test host guard. | the rule text | UNKNOWN |  |
| R104 | Test host steps and log. | the rule text | UNKNOWN |  |
| R105 | Placer check. | the rule text | UNKNOWN |  |
| R107 | Link instance name. | the rule text | RECOMMENDED | Q099 |
| R108 | Add-in security dialogs. | the rule text | UNKNOWN |  |
| R109 | Prototype upgrade job. | the rule text | CHOSEN by Bader | Q067 |
| R110 | Parallel windows. | the rule text | UNKNOWN |  |
| R90 | Block as a cluster hint. | Duplex_DP2, Duplex_DP4, Townhouse_TH1, Townhouse_TH3 | RECOMMENDED, UNKNOWN | Q098 |
| R93 | Entry measurement. | the rule text | UNKNOWN |  |
| R94 | Entry rule. | the rule text | RECOMMENDED | Q024 |
| R95 | Entry proof. | the rule text | UNKNOWN |  |
| R97 | Block offset. | the rule text | UNKNOWN |  |
| R98 | Fit and test per group. | the rule text | UNKNOWN |  |
| R99 | Entrance point per block. | the rule text | UNKNOWN |  |
| R101 | Second methods. | the rule text | UNKNOWN |  |
| R111 | Yard rule. | floors.yardMinShare, floors.yardShareTie, yardMinShare, yardShareTie | UNKNOWN |  |
| R112 | Floors in the plan. | floors.keyTypes, floors.levels, floors.types, floors.yardBelowFflM | UNKNOWN |  |
| R113 | Floor step. | the rule text | UNKNOWN |  |
| R114 | Plot value, skip and ramp corners. | the rule text | UNKNOWN |  |
| R115 | Floor geometry. | the rule text | UNKNOWN |  |
| R116 | Floor run and report. | the rule text | UNKNOWN |  |
| R119 | Floor check. | the rule text | UNKNOWN |  |
| R120 | Plot cut list. | cadLinks.cutNeighbours, cadLinks.cutShareMin, cutNeighbours, cutSha... | UNKNOWN |  |
| R121 | Corner test and origin offset. | the rule text | UNKNOWN |  |
| R122 | Rotation from the front edge. | analysis.partyWallShare, analysis.toleranceM, long_side, partyWallS... | UNKNOWN |  |
| R123 | Mirror and CASE. | analysis.pairRule, foundation | UNKNOWN |  |
| R124 | Second method for the links from CAD. | the rule text | UNKNOWN |  |
| R125 | One FFL per cluster. | cluster_max_units, cluster_split_positions | UNKNOWN |  |
| R126 | Levels of the CAD plan. | one_step_only, steps.one_step_only, typologies | UNKNOWN |  |
| R127 | Ramp outline test. | front_setback, ramp.front_setback | UNKNOWN |  |
| R128 | Plan from CAD. | the rule text | UNKNOWN |  |
| R129 | Plan against plan. | the rule text | UNKNOWN |  |
| R130 | Second method for the CAD plan. | the rule text | UNKNOWN |  |
| R131 | Villa hand test. | analysis.partyWallShare, analysis.unitPlotTypes, driver_house, long... | UNKNOWN |  |
| R132 | CASE second letter test. | typologies | UNKNOWN |  |
| R133 | FFL split test. | steps.legend | UNKNOWN |  |
| R135 | Floor outlines in the link frame. | cadLinks.floorWidthStepM, floorWidthStepM | UNKNOWN |  |
| R136 | Ramp type and front level. | Flat Ramps | RECOMMENDED | Q002 |
| R137 | Floors in the plan from CAD. | floors.levels, floors.yardBelowFflM | UNKNOWN |  |
| R138 | Floors against a plan. | the rule text | UNKNOWN |  |
| R139 | Second method for the floors. | the rule text | UNKNOWN |  |
| R152 | TIN border. | the rule text | UNKNOWN |  |
| R156 | Plot front against the lines and the surfaces. | the rule text | UNKNOWN |  |
| R157 | Front corner levels. | the rule text | UNKNOWN |  |
| R159 | Second method for the C3D round. | the rule text | UNKNOWN |  |
| R160 | Full window runner. | minFreeGB | UNKNOWN |  |
| R162 | Data command. | the rule text | UNKNOWN |  |
| R166 | Plots joined. | dataBook.wiring | UNKNOWN |  |
| R170 | El @ Entry from the road and the ramp. | front_setback, ramp.front_setback, slopes_percent, slopes_percent.f... | UNKNOWN |  |
| R172 | Width meaning. | typologies | UNKNOWN |  |
| R173 | Second method for round DATA-2. | check.minEdgeM | UNKNOWN |  |
| R174 | Wiring methods. | dataBook.wiring, labelNearM, levelToleranceM, pdfToleranceM | UNKNOWN |  |
| R175 | Score and pick. | answerShareMin, dataBook.wiring, ownerShareMin | UNKNOWN |  |
| R176 | Weak spots and drawing to drawing. | blockOverlapMin, curbBandsM, curbStepM, labelNearM | UNKNOWN |  |
| R177 | Chosen wiring. | dataBook.wiring | UNKNOWN |  |
| R178 | Second method for the wiring. | distanceSlackM, maxGridPoints, shareSlack | UNKNOWN |  |
| R179 | Location plan text. | dataBook.locationPlan, joinGapPt, labelSize, lineTolerancePt | UNKNOWN |  |
| R181 | Page to site. | controlSearchPt, controlShapeMaxPt, fitDropFactor, fitFloorM | CHOSEN by Bader, UNKNOWN | Q081 |
| R182 | Number to face and code. | codeTable | UNKNOWN |  |
| R183 | Zones and wiring. | dataBook.wiring, zoneHatch, zoneStyles | UNKNOWN |  |
| R200 | Window lock. | minFreeGB, siteBuild, siteBuild.minFreeGB, siteBuild.staleHours | UNKNOWN |  |
| R201 | Snapshot. | siteBuild | UNKNOWN |  |
| R202 | Originals. | siteBuild | UNKNOWN |  |
| R204 | Site upgrade. | answerKey, siteBuild, siteBuild.answerKey | UNKNOWN |  |
| R206 | Answer key. | siteBuild, siteBuild.zoneNbh | UNKNOWN |  |
| R208 | Plan from the data book. | floors.yardBelowFflM | UNKNOWN |  |
| R209 | Estimated levels. | siteBuild, siteBuild.roadSurfaces, siteBuild.snapM, siteBuild.steep... | UNKNOWN |  |
| R211 | Site coordinates. | coordinateStepM, siteBuild, siteBuild.coordinateStepM | RECOMMENDED, UNKNOWN | Q068 Q086 |
| R212 | New site file. | floors.levels, floors.plotParameter, floors.types, plotParameter | RECOMMENDED, UNKNOWN | Q085 |
| R213 | New site file name. | siteBuild, siteBuild.newFile | UNKNOWN |  |
| R214 | Plan view image. | siteBuild | UNKNOWN |  |
| R215 | Compare in bands. | siteBuild, siteBuild.bandsMm | UNKNOWN |  |
| R218 | Clean up. | siteBuild | UNKNOWN |  |
| R185 | Rev 9 workbook inventory and diff. | dataBook.rev9 | UNKNOWN |  |
| R186 | DLUS setbacks. | dataBook.rev9, rev9.dlus | UNKNOWN |  |
| R187 | Land width and length. | dataBook.rev9, rev9.sales | UNKNOWN |  |
| R188 | Old to new code table. | codeTable, dataBook.rev9, typology_codes | UNKNOWN |  |
| R189 | Rev 9 drawing read. | dataBook.rev9 | RECOMMENDED, UNKNOWN | Q062 |
| R190 | Rev 9 PDF labels and wiring. | labelSize | UNKNOWN |  |
| R221 | Two past jobs on the same plots. | siteBuild, siteBuild.bandsMm, siteBuild.key14 | UNKNOWN |  |
| R224 | Road edges and the past ramp edge. | corner.onEdgeM, corner.roadEdgeM, roadEdgeM, siteBuild | UNKNOWN |  |
| R225 | Corner front rules. | the rule text | UNKNOWN |  |
| R227 | House outline of a prototype. | footprint.groundSpanM, groundSpanM, siteBuild, siteBuild.footprint | UNKNOWN |  |
| R228 | Footprint of a plot. | footprint.mirrorGain, mirrorGain, siteBuild, siteBuild.footprint | UNKNOWN |  |
| R229 | Footprint band of a key. | footprint.slack, siteBuild, siteBuild.footprint | UNKNOWN |  |
| R231 | One run per prototype code. | minFreeGB, placeSettleMinutes, siteBuild, siteBuild.minFreeGB | UNKNOWN |  |
| R236 | Clusters from the footprint blocks. | the rule text | CHOSEN by Bader | Q089 |
| R245 | Rooms and doors of a read. | the rule text | UNKNOWN |  |
| R251 | Audit of DATA-4. | dataBook.data5 | UNKNOWN |  |
| R252 | Xref search. | dataBook.data5 | UNKNOWN |  |
| R255 | Two road layouts. | curbStepM, dataBook.data5 | UNKNOWN |  |
| R256 | Area key. | data5.areaKey, dataBook.data5 | UNKNOWN |  |
| R257 | IM codes. | dataBook.data5, neighbourhoods | UNKNOWN |  |
| R258 | IFC header. | dataBook.data5 | UNKNOWN |  |
| R259 | Coverage table. | dataBook.data5 | UNKNOWN |  |
| R260 | Second method for round DATA-5. | dataBook.data5 | RECOMMENDED, UNKNOWN | Q054 |
| R261 | Plan position and level apart. | level.passMm, placerCheck, position.passMm | RECOMMENDED, UNKNOWN | Q054 |
| R263 | The Native dialect. | floorTypes, placerCheck, placerCheck.dialects | UNKNOWN |  |
| R270 | El @ Entry gaps between keys. | entryGapM, entryGaps, entryGaps.entryGapM, kindLeaveOut | UNKNOWN |  |
| R271 | Context of a past plot. | majority.groups | RECOMMENDED, UNKNOWN | Q072 |
| R272 | The majority rule. | decidedByQuestion, majority.decidedByQuestion, majority.siteReferen... | CHOSEN by Bader, RECOMMENDED, UNKNOWN | Q017 standing rule |
| R274 | Hand. | the rule text | MEASURED | alone 0.938 cluster 0.981 villa 0.913 |
| R275 | Levels. | levelHoldMm, majority.levelHoldMm | UNKNOWN |  |
| R276 | Floors and parameters. | parameterTests, wholeFaceShare | UNKNOWN |  |
| R277 | Level profiles. | levelProfiles.default, majority.groups | CHOSEN by Bader, UNKNOWN | Q050 |
| R278 | Levels and ramps of a plan from CAD. | ground_ssl, levels.ground_ssl | UNKNOWN |  |
| R279 | Hands of a plan from CAD. | the rule text | UNKNOWN |  |
| R280 | Overrides in a plan from CAD. | majority.overrideDefaultSources, overrideDefaultSources | UNKNOWN |  |
| R281 | Second method for round 17. | majority.overrideDecisions, overrideDecisions | UNKNOWN |  |
| R282 | Hands against a Native key. | the rule text | RECOMMENDED | Q054 |
| R300 | Villa blocks from the Building Footprint. | the rule text | CHOSEN by Bader | Q045 |
| R301 | Area codes by job. | dataBook.data5, majority.groups | CHOSEN by Bader, UNKNOWN | Q044 |
| R302 | Image step limit. | imageMarginMinutes, imageStepMinutes, testHost.imageMarginMinutes, ... | CHOSEN by Bader, CHOSEN by Code | CHOSEN mark Q061 |
| R303 | Withdrawn. | the rule text | RECOMMENDED | Q064 |
| R310 | Window samples. | cimTimeoutSeconds, helperProcesses, minFreeGB, sampleSeconds | CHOSEN by Code, UNKNOWN | CHOSEN mark |
| R312 | Key copies by code. | siteBuild | RECOMMENDED, UNKNOWN | Q065 Q069 |
| R313 | Prototype sets of the upgrade. | the rule text | CHOSEN by Bader | Q067 |
| R321 | Floor types of each key. | floors.keyTypes, floors.types | UNKNOWN |  |
| R323 | Second method of the key reads. | floors.keyTypes, floors.plotParameter, floors.types, plotParameter | RECOMMENDED, UNKNOWN | Q085 |
| R325 | Plans of the answer keys. | answerKey, cadLinks.cutShareMin, cutShareMin, entryHeightM | CHOSEN by Code, UNKNOWN | CHOSEN mark |
| R326 | The join of the answer keys. | cadLinks.cutShareMin, cutShareMin, keyPlans.minShare, kindFoldCase | RECOMMENDED, UNKNOWN | Q066 |
| R327 | Spot elevations at the plot entrance. | cutShareMin, entryHeightM, entrySpots, entrySpots.bandsM | RECOMMENDED, CHOSEN by Code, UNKNOWN | CHOSEN mark Q001 |
| R328 | Second method of Phase 2b. | cutShareMin, entryHeightM, floors.plotParameter, keyPlans.entryHeightM | RECOMMENDED, CHOSEN by Code, UNKNOWN | CHOSEN mark Q085 |
| R330 | The source of an override. | laterPhase, majority.groups, majority.overrideDefaultSources, major... | RECOMMENDED, UNKNOWN | Q053 |
| R331 | Coordinates of the keys. | keyRules.site, levelProfiles.parsons2024Rev6, majority.groups, majo... | MEASURED, RECOMMENDED, UNKNOWN | Q068 baseHeight 0.8433 entry 0.088 ffl 0.7465 frontCorner 0.9776 frontOpen 0.9883 generic 0.9908 ... |
| R332 | Link origin and rotation on the keys. | keyRules.linkOrigin, linkHoldMm, linkOrigin, linkRotation | MEASURED, RECOMMENDED, UNKNOWN | Q037 frontCorner 0.9776 linkOrigin 0.9712 linkRotation 0.9908 |
| R333 | Hands on the keys. | keyRules.hand villa, majority.pairKinds, pairKinds | CHOSEN by Bader, MEASURED, UNKNOWN | Q089 hand alone 1.0 hand cluster 0.9805 hand villa 0.9598 |
| R334 | Ramps and floors on the keys. | majority.outlineHoldMm, outlineHoldMm | MEASURED, RECOMMENDED, UNKNOWN | Q066 generic 0.9908 rampOutline 0.9687 yardOutline 0.9632 |
| R335 | Walls and shared parameters on the keys. | boundary_wall_thickness, boundary_wall_top_above_ffl, frontOpen, fr... | MEASURED, RECOMMENDED, CHOSEN by Code, UNKNOWN | CHOSEN mark Q071 frontOpen 0.9883 wallBase 0.9825 wallLine 0.9208 wallTop 0.3465 |
| R336 | Second method of Round ADDIN 3a. | check.seeds, corner.roadEdgeM, decidedByQuestion, frontOpen | CHOSEN by Bader, MEASURED, RECOMMENDED, UNKNOWN | Q017 Q072 Q089 frontOpen 0.9883 |
| R337 | A build by the rules of the keys. | decidedByQuestion, keyRules.linkOrigin, keyRules.rampSlope, keyRule... | MEASURED, RECOMMENDED, UNKNOWN | Q017 Q057 generic 0.9908 hand alone 1.0 hand cluster 0.9805 hand villa 0.9598 linkOrigin 0.9712 r... |
| R338 | The keyRules block. | keyRulesBlock, keyRulesBlock.levelProfile, keyRulesBlock.nativeRoun... | RECOMMENDED, UNKNOWN | Q066 |
| R339 | Levels of the keys to the mm. | baseHeight, base_height, ground_ssl, keyRules.levelSplit | CHOSEN by Bader, MEASURED, RECOMMENDED, UNKNOWN | Q012 standing rule baseHeight 0.8433 generic 0.9908 levelSplit 0.7024 |
| R340 | A build and its proof by the level rules of the... | baseHeight, ground_ssl, keyRules.steps, levelBandsMm | MEASURED, RECOMMENDED, UNKNOWN | Q057 baseHeight 0.8433 levelSplit 0.7024 rampFront 0.6924 steps 0.9877 |
| R341 | Second method of the levels of Round ADDIN 3c. | levelPassMm, majority.levelPassMm, siteBuild, siteBuild.roadSurfaces | UNKNOWN |  |
| R342 | Licences of the libraries. | the rule text | RECOMMENDED | Q079 |
| R343 | The add-in design. | siteBuild | UNKNOWN |  |
| R344 | The road at a front corner from the Roads folder. | siteBuild, siteBuild.roadSurfaces, siteBuild.snapMaxM | CHOSEN by Bader, RECOMMENDED, UNKNOWN | Q052 Q053 Q057 Q073 Q074 Q082 |
| R345 | The parity harness. | the rule text | UNKNOWN |  |
| R346 | The projects of the add-in. | the rule text | RECOMMENDED | Q079 |
| R347 | The worker run. | addin.workerPeakMiB, addin.workerTimeLimitMinS, addin.workerTimeLim... | CHOSEN by Code, UNKNOWN | CHOSEN mark |
| R348 | The DWG reads of button 2. | analysis.roadEdgeLayers, roadEdgeLayers | UNKNOWN |  |
| R349 | The LandXML read and the road level snap of but... | siteBuild, siteBuild.roadSurfaces, siteBuild.snapMaxM | UNKNOWN |  |
| R350 | The DLUS and sales reads of button 2. | data5.areaKey, dataBook.data5, dataBook.locationPlan, dataBook.rev9 | CHOSEN by Code, UNKNOWN | CHOSEN mark |
| R351 | The location plan of button 2. | connect.neighbourhoods, dataBook.locationPlan, neighbourhoods | UNKNOWN |  |
| R352 | The plot table of button 2. | addin.plotTable, dataBook.wiring, overlapDecimals, plotTable.overla... | CHOSEN by Code, UNKNOWN | CHOSEN mark |
| R353 | The project position file, the check points and... | addin.position, checkPointLimitMm, floors.keyTypes, floors.plotPara... | CHOSEN by Bader, RECOMMENDED, CHOSEN by Code, UNKNOWN | CHOSEN mark Q050 Q068 Q085 Q086 Q087 |
| R354 | The clusters of a build. | Duplex_DP4, baseHeight | CHOSEN by Bader, MEASURED, RECOMMENDED | Q050 Q089 Q098 baseHeight 0.8433 hand alone 1.0 hand cluster 0.9805 hand villa 0.9598 parameter C... |
| R355 | The front of a plot in a build. | analysis.partyWallShare, analysis.roadEdgeLayers, analysis.toleranc... | UNKNOWN |  |
| R356 | The link frame of a plot in a build. | keyRules.linkOrigin, linkOrigin | MEASURED | linkOrigin 0.9712 |
| R357 | The hand of a plot in a build. | keyRules.linkOrigin, kindByPrefix, linkOrigin, majority.pairKinds | MEASURED, RECOMMENDED, UNKNOWN | Q072 Q098 hand alone 1.0 hand cluster 0.9805 hand villa 0.9598 linkOrigin 0.9712 |
| R358 | The floors of a plot in a build. | cadLinks.floorWidthStepM, floorWidthStepM, keyRules.rampSlope, keyR... | MEASURED, UNKNOWN | rampSlope 0.9883 yard 1.0 yardOutline 0.9632 |
| R359 | The levels of a plot in a build. | baseHeight, base_height, ground_ssl, keyRules.baseHeight | MEASURED, UNKNOWN | baseHeight 0.8433 ffl 0.7465 levelSplit 0.7024 steps 0.9877 |
| R360 | The overrides of a plot in a build. | floors.levels, majority.overrideDefaultSources, overrideDefaultSources | MEASURED, RECOMMENDED, UNKNOWN | Q057 rampOutline 0.9687 yardOutline 0.9632 |
| R361 | The link parameters of a plot in a build. | parameter RRE_ZZ_SP_RVT-Name | MEASURED, RECOMMENDED | Q099 parameter RRE_ZZ_SP_RVT-Name 0.9916 |
| R385 | One rounding rule in Python and C#. | baseHeight, ground_ssl, keyRules.baseHeight, levels.ground_ssl | CHOSEN by Bader, MEASURED, RECOMMENDED, UNKNOWN | Q050 Q052 Q078 baseHeight 0.8433 |
| R387 | The control point fit as a check of the schedul... | controlMark, controlSearchPt, dataBook.locationPlan, locationPlan.c... | CHOSEN by Bader, UNKNOWN | Q081 |
| R388 | Units of the inputs of the Python reference. | the rule text | UNKNOWN |  |

## 4 Rules with no design value

| Rule | Title | Value |
|---|---|---|
| R1 | Read only. | process, no value |
| R3 | Small output. | process, no value |
| R4 | Revit prototypes. | process, no value |
| R5 | CAD files. | process, no value |
| R6 | CAD reader library. | process, no value |
| R7 | PDF licence. | process, no value |
| R8 | PDF output. | process, no value |
| R9 | Excel output. | process, no value |
| R10 | No invented data. | process, no value |
| R16 | Revit links. | process, no value |
| R17 | Walls, floors and ramps. | process, no value |
| R18 | Main is protected. | process, no value |
| R19 | Explode. | process, no value |
| R20 | Layer filter. | process, no value |
| R21 | Texts and xrefs. | process, no value |
| R22 | Plot labels. | process, no value |
| R23 | PDF rows. | process, no value |
| R24 | Link table. | process, no value |
| R25 | Full run. | process, no value |
| R26 | Harness files. | process, no value |
| R28 | Plot number shift. | process, no value |
| R30 | No plot type against unit type check. | process, no value |
| R32 | Workbook survey. | process, no value |
| R34 | Floor corners. | process, no value |
| R35 | Corner source and slope. | process, no value |
| R36 | Spot elevations. | process, no value |
| R37 | Floor and wall plot numbers. | process, no value |
| R39 | Link contents default. | process, no value |
| R40 | LandXML reader. | process, no value |
| R41 | Invisible faces. | process, no value |
| R42 | Surface lookup. | process, no value |
| R43 | Surface roles. | process, no value |
| R44 | Ramp road levels. | process, no value |
| R46 | Full run stages and second check. | process, no value |
| R50 | One line for floors with no Plot parameter. | process, no value |
| R51 | Shared coordinates. | process, no value |
| R52 | Floor measurements. | process, no value |
| R54 | Link state. | process, no value |
| R55 | Spot host. | process, no value |
| R56 | Output size. | process, no value |
| R57 | Plot relation report. | process, no value |
| R65 | Attachment follows the cluster position. | process, no value |
| R68 | Block inserts. | process, no value |
| R72 | Xref resolution. | process, no value |
| R74 | Excel unit by baseplan label. | process, no value |
| R76 | Second block match. | process, no value |
| R77 | Block against link. | process, no value |
| R80 | Plan file. | process, no value |
| R85 | Link parameters. | process, no value |
| R86 | Dry run and one transaction. | process, no value |
| R87 | Placer report and skip. | process, no value |
| R88 | Placer install. | process, no value |
| R102 | Placer filter. | process, no value |
| R106 | No registry change. | process, no value |
| R91 | Road level lookup. | process, no value |
| R92 | Ramp test with the lookup. | process, no value |
| R96 | Computed entry levels through the level rule. | process, no value |
| R100 | Measured offsets file. | process, no value |
| R117 | Floor jobs of the test host. | process, no value |
| R118 | Work file life cycle. | process, no value |
| R134 | Second method for the rule tests. | process, no value |
| R150 | Civil 3D probe. | process, no value |
| R151 | Core console runner. | process, no value |
| R153 | LISP inventory. | process, no value |
| R154 | Drawing survey. | process, no value |
| R155 | Surfaces from the LandXML. | process, no value |
| R158 | Coverage and cut. | process, no value |
| R161 | Civil 3D dialog watcher. | process, no value |
| R163 | Phase 1 border. | process, no value |
| R164 | Stand-in tables and the size limit. | process, no value |
| R165 | Workbook sheets. | process, no value |
| R167 | Analysis. | process, no value |
| R168 | Second method for the data book. | process, no value |
| R169 | Export every surface. | process, no value |
| R171 | Units per face. | process, no value |
| R180 | Label pairs. | process, no value |
| R184 | Second method for the location plan. | process, no value |
| R203 | Revit file info from bytes. | process, no value |
| R205 | Read. | process, no value |
| R207 | Prototype choice. | process, no value |
| R210 | Ramp from CAD. | process, no value |
| R216 | Plan before Revit. | process, no value |
| R217 | Second method of the round. | process, no value |
| R191 | Rev 9 faces against the baseplan. | process, no value |
| R192 | Cross sections read in full. | process, no value |
| R193 | El @ Entry from its terms. | process, no value |
| R194 | Unit blocks on no plot. | process, no value |
| R196 | Location plan DWG. | process, no value |
| R197 | Civil 3D under the shared window lock. | process, no value |
| R198 | Second method for round DATA-3. | process, no value |
| R220 | Rotation of a placed instance. | process, no value |
| R222 | Road surface of the past ramp fronts. | process, no value |
| R223 | Gap per unit. | process, no value |
| R226 | Corner front in the plan. | process, no value |
| R230 | Plan of a whole neighbourhood. | process, no value |
| R234 | Links loaded for an image. | process, no value |
| R235 | Second method of round 16. | process, no value |
| R240 | Civil 3D dialogs by rule 3. | process, no value |
| R241 | CadReader header only. | process, no value |
| R242 | Surface survey. | process, no value |
| R243 | Parcels of a drawing. | process, no value |
| R244 | Parcels against the plan. | process, no value |
| R246 | Villa hand. | process, no value |
| R247 | Past schedule formulas. | process, no value |
| R248 | El @ Entry by surface version. | process, no value |
| R249 | The past key by surface version. | process, no value |
| R250 | Second method for round DATA-4. | process, no value |
| R253 | DXF of a copy and its count. | process, no value |
| R254 | Readers against the third count. | process, no value |
| R262 | The footprint as the third check. | process, no value |
| R264 | Names that are no plot. | process, no value |
| R265 | A shortened TransmissionData. | process, no value |
| R266 | Dates from the bytes. | process, no value |
| R267 | Coverage of the past keys. | process, no value |
| R268 | Model inventory. | process, no value |
| R269 | One table of every past plot. | process, no value |
| R273 | Ramp type and slope. | process, no value |
| R304 | A clean build in a deep folder. | process, no value |
| R311 | Site facts of the reader. | process, no value |
| R320 | Spot value and base. | process, no value |
| R322 | Counts of each key read. | process, no value |
| R386 | Use and skip in the override list. | process, no value |
| R11 | Zero tests is a failure. | process, no value |
| R12 | Every rule has a test. | process, no value |
| R13 | No attribution lines. | process, no value |
| R14 | No emoji and no em dash | process, no value |
| R15 | No semicolons in prose, comments, documents, commit messa... | process, no value |
| R400 | Record sizes. | process, no value |

## 5 Values of the rules profile and the settings

One row per value, or per group of values that share a pattern (a code, a number or a key letter as *), a kind and a status, with n the values of the group (RAN, audit.py). A keyRules row is its rule field, the rest of the entry is its measurement record. No coordinate and no level of a key is shown: keyRules site shows its rule only.

### Values of rules/roshn-makkah.json

| Value | n | Kind | Status | Q ID or share | Evidence |
|---|---|---|---|---|---|
| typologies.*.kind | 16 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| typologies.*.attachment | 16 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| typologies.*.keo_file | 16 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| typologies.*.plot_width_m | 16 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| clusters.* | 15 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| cluster_aliases.* | 3 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| cluster_max_units = 4 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| cluster_split_positions = [2, 3] | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| cluster_block_max = 4 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| cluster_six_unit_joint_after = 4 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| slopes_percent.front_min = 0.5 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| slopes_percent.front_max = 2.0 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| slopes_percent.front_default = 0.5 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| slopes_percent.back_min = 0.5 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| slopes_percent.back_target_max = 1.0 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| slopes_percent.back_hard_max = 2.0 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| steps.front_min = 1 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| steps.front_max = 3 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| steps.riser_max = 180 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| steps.riser_min = 100 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| steps.riser_height = null | 1 | value | UNKNOWN |  | null or empty |
| steps.tread_min = 280 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| steps.tread_added = 300 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| steps.landing_depth_min = 1000 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| steps.landing_depth_code_min = 914 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| steps.passage_min = 800 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| steps.planter_width = 300 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| steps.back_step = 100 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| steps.landing_required_from_steps = 3 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| steps.handrail_required_from_steps = 4 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| steps.legend.* | 6 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| steps.riser_added = 170 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| steps.count_rule = "under 0.205 is 1, 0.205 to under 0.3... | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| steps.base_height.default = 100 | 1 | value | UNKNOWN |  | steps.base_height_source: past job schedules, 747 plots... |
| steps.base_height.* | 5 | value | UNKNOWN |  | steps.base_height_source: past job schedules, 747 plots... |
| steps.one_step_only = ["C10", "VL4", "VL6"] | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| ramp.front_setback.default = 5520 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| ramp.front_setback.C10 = 5500 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| villa.driver_house = ["C10", "VL6"] | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| typology_codes.rows.*.form | 12 | value | UNKNOWN |  | null or empty |
| typology_codes.rows.*.* | 34 | value | UNKNOWN |  | typology_codes.source: PSN-ZZZ-XXX-SCH-TM-000201- DLUS ... |
| typology_codes.rows.*.width_m | 18 | value | UNKNOWN |  | typology_codes.source: PSN-ZZZ-XXX-SCH-TM-000201- DLUS ... |
| typology_codes.rows.*.depth_m | 18 | value | UNKNOWN |  | typology_codes.source: PSN-ZZZ-XXX-SCH-TM-000201- DLUS ... |
| typology_codes.rows.*.plot_area_m2 | 18 | value | UNKNOWN |  | typology_codes.source: PSN-ZZZ-XXX-SCH-TM-000201- DLUS ... |
| typology_codes.rows.*.gfa_m2 | 18 | value | UNKNOWN |  | typology_codes.source: PSN-ZZZ-XXX-SCH-TM-000201- DLUS ... |
| typology_codes.rows.*.bua_m2 | 18 | value | UNKNOWN |  | typology_codes.source: PSN-ZZZ-XXX-SCH-TM-000201- DLUS ... |
| typology_codes.rows.*.form | 6 | value | UNKNOWN |  | typology_codes.source: PSN-ZZZ-XXX-SCH-TM-000201- DLUS ... |
| typology_codes.rows.*.* | 2 | value | UNKNOWN |  | null or empty |
| plot.long_side = 25000 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| plot.boundary_wall_thickness = 200 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| plot.vl3_width = 12000 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| plot.vl3_front_yard = 5520 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| plot.vl3_back_yard = 4420 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| plot.th1_width = 8000 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| plot.vl4_width = "derive" | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| levels.ground_ssl = -75 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| levels.floor_to_floor_contemporary = 3200 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| levels.floor_to_floor_makkawi = 3400 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| levels.precast_wall_thickness = 330 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| foundation.depth_min_below_ngl = 1200 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| foundation.bearing_capacity_kpa = 150 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| mep.tie_ins = ["sewage", "water", "power", "telecom"] | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| mep.sewage_encasement_cover_under = 1000 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| rev6.back_slope_to_front_min_percent = 0.3 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| rev6.back_slope_to_front_max_percent = 0.5 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| rev6.th_back_slope_percent = 2.0 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| rev6.boundary_wall_top_above_ffl = 1700 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| rev6.boundary_wall_continuity_limit = 500 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| rev6.garden_wall_top_above_ffl = 1700 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| rev6.utility_cabinet_raise = 100 | 1 | value | UNKNOWN |  | source: 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 20... |
| hands.villa.rule = "blockMirrorOpenEnd" | 1 | value | MEASURED | share 0.913, strong | hands.source, the Parsons IMC key, Round 17, R274 |
| hands.alone.rule = "blockMirrorOpenEnd" | 1 | value | MEASURED | share 0.938, strong | hands.source, the Parsons IMC key, Round 17, R274 |
| hands.cluster.rule = "clusterSide" | 1 | value | MEASURED | share 0.981, strong | hands.source, the Parsons IMC key, Round 17, R274 |
| levelProfiles.default = "parsons2024Rev6" | 1 | value | CHOSEN by Bader | Q050 | CLAUDE.md and steps/questions-closed.md |
| levelProfiles.parsons2024Rev6.frontRoad.surface = "roadLevel" | 1 | value | UNKNOWN |  | levelProfiles.parsons2024Rev6.source: the Parsons IMC k... |
| levelProfiles.parsons2024Rev6.frontRoad.offsetM = 0.0 | 1 | value | UNKNOWN |  | levelProfiles.parsons2024Rev6.source: the Parsons IMC k... |
| levelProfiles.parsons2024Rev6.rampSlopePercent.default = 0.5 | 1 | value | UNKNOWN |  | levelProfiles.parsons2024Rev6.source: the Parsons IMC k... |
| levelProfiles.parsons2024Rev6.rampSlopePercent.* | 3 | value | UNKNOWN |  | levelProfiles.parsons2024Rev6.source: the Parsons IMC k... |
| levelProfiles.parsons2024Rev6.rampTypes.0.5 = "Ramps 0.5%" | 1 | value | UNKNOWN |  | levelProfiles.parsons2024Rev6.source: the Parsons IMC k... |
| levelProfiles.parsons2024Rev6.rampTypes.2 = "Ramps 2%" | 1 | value | UNKNOWN |  | levelProfiles.parsons2024Rev6.source: the Parsons IMC k... |
| levelProfiles.parsons2024Rev6.rampTypes.flat = "Flat Ramps" | 1 | value | UNKNOWN |  | levelProfiles.parsons2024Rev6.source: the Parsons IMC k... |
| levelProfiles.parsons2024Rev6.height = "oneFflPerClusterAtTheHighestEntry" | 1 | value | UNKNOWN |  | levelProfiles.parsons2024Rev6.source: the Parsons IMC k... |
| levelProfiles.parsons2024Rev6.steps = "countRule" | 1 | value | UNKNOWN |  | levelProfiles.parsons2024Rev6.source: the Parsons IMC k... |
| levelProfiles.parsons2024Rev6.stepBands.* | 3 | value | UNKNOWN |  | levelProfiles.parsons2024Rev6.source: the Parsons IMC k... |
| levelProfiles.parsons2024Rev6.yardBelowFflM = 0.1 | 1 | value | UNKNOWN |  | levelProfiles.parsons2024Rev6.source: the Parsons IMC k... |
| levelProfiles.parsons2024Rev6.keyRules.site.rule = "oneSet" | 1 | value | UNKNOWN |  | keyRules entry with no share |
| levelProfiles.parsons2024Rev6.keyRules.linkRotation.rule = "frontRule" | 1 | value | MEASURED | share 0.9908, strong | keys ima imb imc imd ime imf img |
| levelProfiles.parsons2024Rev6.keyRules.frontCorner.rule = "frontRule" | 1 | value | MEASURED | share 0.9776, strong | keys ima imb imc imd ime imf img |
| levelProfiles.parsons2024Rev6.keyRules.linkOrigin.rule = "plotFrameRefit" | 1 | value | MEASURED | share 0.9712, strong | keys ima imb imc imd ime imf img |
| levelProfiles.parsons2024Rev6.keyRules.hand villa.rule = "blockMirrorRoadSide" | 1 | value | MEASURED | share 0.9598, strong | keys ima imb imc imd ime imf img |
| levelProfiles.parsons2024Rev6.keyRules.hand cluster.rule = "clusterSide" | 1 | value | MEASURED | share 0.9805, strong | keys ima imb imc imd ime imf img |
| levelProfiles.parsons2024Rev6.keyRules.hand alone.rule = "rowPairSideOrBlockMirrorOpenEnd" | 1 | value | MEASURED | share 1.0, strong | keys ima imb imc imd ime imf img |
| levelProfiles.parsons2024Rev6.keyRules.rampSlope.rule = "typology" | 1 | value | MEASURED | share 0.9883, strong | keys imc imd ime imf img |
| levelProfiles.parsons2024Rev6.keyRules.rampOutline.rule = "outlineR127" | 1 | value | MEASURED | share 0.9687, strong | keys imc imd ime imf img |
| levelProfiles.parsons2024Rev6.keyRules.rampFront.rule = "roadLevel" | 1 | value | MEASURED | share 0.6924, weak | keys imc imd ime imf img |
| levelProfiles.parsons2024Rev6.keyRules.rampFrontFall.rule = "roadAtEachCorner" | 1 | value | MEASURED | share 0.7638, weak | keys imc imd ime imf img |
| levelProfiles.parsons2024Rev6.keyRules.generic.rule = "all" | 1 | value | MEASURED | share 0.9908, strong | keys ima imb imc imd ime imf img |
| levelProfiles.parsons2024Rev6.keyRules.yard.rule = "zone" | 1 | value | MEASURED | share 1.0, strong | keys imc imd ime imf img |
| levelProfiles.parsons2024Rev6.keyRules.yardOutline.rule = "fittedOutlineRefit" | 1 | value | MEASURED | share 0.9632, strong | keys imc imd ime imf |
| levelProfiles.parsons2024Rev6.keyRules.wallBase.rule = "yardLevelOfTheHigherPlot" | 1 | value | MEASURED | share 0.9825, strong | keys imc imd ime imf img |
| levelProfiles.parsons2024Rev6.keyRules.wallTop.rule = "higherFflPlusRev6" | 1 | value | MEASURED | share 0.3465, weak | keys imc imd ime imf img |
| levelProfiles.parsons2024Rev6.keyRules.wallLine.rule = "centreOnTheEdge" | 1 | value | MEASURED | share 0.9208, strong | keys imc imd ime imf img |
| levelProfiles.parsons2024Rev6.keyRules.frontOpen.rule = "all" | 1 | value | MEASURED | share 0.9883, strong | keys imc imd ime imf img |
| levelProfiles.parsons2024Rev6.keyRules.parameter RRE_ZZ_SP_Plot-Number.rule = "plot" | 1 | value | MEASURED | share 1.0, strong | keys ima imb imc imd ime imf img |
| levelProfiles.parsons2024Rev6.keyRules.parameter RRE_ZZ_SP_Prototype.rule = "typology" | 1 | value | MEASURED | share 1.0, strong | keys ima imb imc imd ime imf img |
| levelProfiles.parsons2024Rev6.keyRules.parameter RRE_ZZ_SP_RVT-Name.rule = "plotCodeStepsAndFacade" | 1 | value | MEASURED | share 0.9916, strong | keys imc imd ime imf img |
| levelProfiles.parsons2024Rev6.keyRules.parameter RRE_ZZ_SP_Number-of-Steps.rule = "steps" | 1 | value | MEASURED | share 1.0, strong | keys imc imd ime imf img |
| levelProfiles.parsons2024Rev6.keyRules.parameter RRE_ZZ_SP_Façade-Color.rule = "all" | 1 | value | MEASURED | share 1.0, strong | keys imc imd ime imf img |
| levelProfiles.parsons2024Rev6.keyRules.parameter RRE_ZZ_SP_Façade-Design-Option.rule = "all" | 1 | value | MEASURED | share 1.0, strong | keys imc imd ime imf img |
| levelProfiles.parsons2024Rev6.keyRules.parameter RRE_ZZ_SP_Prototype-Foundation-Type.rule = "typologyMirrored" | 1 | value | MEASURED | share 0.6198, weak | keys ima imb imc imd ime imf img |
| levelProfiles.parsons2024Rev6.keyRules.parameter RRE_ZZ_SP_Prototype-HVAC-Schedule-Type.rule = "typology" | 1 | value | MEASURED | share 1.0, strong | keys imc imd ime imf img |
| levelProfiles.parsons2024Rev6.keyRules.parameter CASE.rule = "typologyMirrored" | 1 | value | MEASURED | share 0.2747, weak | keys ima imb imc imd ime imf img |
| levelProfiles.parsons2024Rev6.keyRules.parameter RRE_ZZ_SP_Prototype-FFL.rule = "fflDownToTheCm" | 1 | value | MEASURED | share 0.9939, strong | keys imc imd ime imf img |
| levelProfiles.parsons2024Rev6.keyRules.entry.rule = "roadLevel" | 1 | value | MEASURED | share 0.088, weak | keys imc imd ime imf img |
| levelProfiles.parsons2024Rev6.keyRules.levelSplit.rule = "units" | 1 | value | MEASURED | share 0.7024, weak | keys imc imd ime imf img |
| levelProfiles.parsons2024Rev6.keyRules.baseHeight.rule = "profile" | 1 | value | MEASURED | share 0.8433, weak | keys imc imd ime imf img |
| levelProfiles.parsons2024Rev6.keyRules.ffl.rule = "oneFflPerPieceAtTheHighestEntry" | 1 | value | MEASURED | share 0.7465, weak | keys imc imd ime imf img |
| levelProfiles.parsons2024Rev6.keyRules.steps.rule = "stepsFromHeight" | 1 | value | MEASURED | share 0.9877, strong | keys imc imd ime imf img |
| levelProfiles.parsons2024Rev6.keyRules.ssl.rule = "fflPlusGroundSsl" | 1 | value | UNKNOWN |  | keyRules entry with no share |
| levelProfiles.native2022.frontRoad.surface = "Makkah Phase 1_Rough Grading__Total ... | 1 | value | UNKNOWN |  | levelProfiles.native2022.source: the Native site models... |
| levelProfiles.native2022.frontRoad.offsetM = 0.7 | 1 | value | UNKNOWN |  | levelProfiles.native2022.source: the Native site models... |
| levelProfiles.native2022.rampSlopePercent.default = 2.0 | 1 | value | UNKNOWN |  | levelProfiles.native2022.source: the Native site models... |
| levelProfiles.native2022.rampTypes.2 = "Ramps 2%" | 1 | value | UNKNOWN |  | levelProfiles.native2022.source: the Native site models... |
| levelProfiles.native2022.height = "medianPerUnit" | 1 | value | UNKNOWN |  | levelProfiles.native2022.source: the Native site models... |
| levelProfiles.native2022.medianOf = "the Native plots by their Typology_R... | 1 | value | UNKNOWN |  | levelProfiles.native2022.source: the Native site models... |
| levelProfiles.native2022.medianHeightM.* | 15 | value | UNKNOWN |  | levelProfiles.native2022.source: the Native site models... |
| levelProfiles.native2022.steps = null | 1 | value | UNKNOWN |  | null or empty |
| levelProfiles.native2022.yardBelowFflM = null | 1 | value | UNKNOWN |  | null or empty |

Every leaf of rules/roshn-makkah.json by kind and status (RAN, audit.py): note note text, no value 23, record measurement record of a hand rule 68, record measurement record of a keyRule 2337, setting setting of a reader or check, no design value 4, value CHOSEN by Bader 1, value MEASURED 35, value UNKNOWN 330.

### Values of config/project.json

| Value | n | Kind | Status | Q ID or share | Evidence |
|---|---|---|---|---|---|
| analysis.toleranceM = 0.01 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| analysis.partyWallShare = 0.75 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| analysis.endShare = 0.25 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| analysis.roadEdgeLayers = ["RD-CURB-LINE"] | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| analysis.unitPlotTypes = ["C10", "DP2", "DP4", "TH1", "TH3", "... | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| analysis.noRev6Match = ["Q02"] | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| analysis.pairRule = false | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| analysis.blockClusters.Duplex_DP2 = "D02" | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| analysis.blockClusters.Duplex_DP4 = "D04" | 1 | value | RECOMMENDED | Q098 | Q098 |
| analysis.blockClusters.Townhouse_TH1 = "Q01" | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| analysis.blockClusters.Townhouse_TH3 = "Q02" | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.rev9.dlus.headerRow = 3 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.rev9.sales.headerRow = 1 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.data5.areaKey.dlus.headerRow = 1 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.data5.areaKey.sales.headerRow = 1 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.locationPlan.sales.headerRow = 1 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.locationPlan.labelSize = [1.4, 1.6] | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.locationPlan.joinGapPt = 0.3 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.locationPlan.lineTolerancePt = 0.05 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.locationPlan.pairDyPt = [1.3, 1.8] | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.locationPlan.pairDxPt = 1.5 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.locationPlan.fitDropFactor = 3 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.locationPlan.fitRounds = 3 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.locationPlan.fitFloorM = 0.001 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.locationPlan.codeTable.headerRow = 2 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.locationPlan.dlus.headerRow = 1 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.locationPlan.zoneStyles.magenta.width = 4.0 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.locationPlan.zoneStyles.red.width = 11.0 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.locationPlan.zoneHatch.width = 2.0 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.locationPlan.zoneHatch.bufferPt = 1.5 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.locationPlan.check.seeds = [14, 7] | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.locationPlan.check.size = 20 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.locationPlan.check.windowPt = 4 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.locationPlan.controlSearchPt = 120 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.locationPlan.controlShapeMaxPt = 12 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.locationPlan.controlMark.widthPt = 3.96 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.locationPlan.controlMark.heightPt = 3.96 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.locationPlan.controlMark.tolerancePt = 0.01 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.locationPlan.controlMark.points = 6 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.wiring.shiftSearch = 1 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.wiring.pdfToleranceM = 1.0 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.wiring.widenM = 0.5 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.wiring.minShare = 0.1 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.wiring.shareTie = 0.02 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.wiring.ownerShareMin = 0.5 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.wiring.nearestM = 2.0 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.wiring.tieM = 0.01 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.wiring.labelNearM = 5.0 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.wiring.answerShareMin = 0.5 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.wiring.frontToleranceM = 0.1 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.wiring.nearestVertexM = 1.0 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.wiring.levelToleranceM = 0.005 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.wiring.blockOverlapMin = 0.5 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.wiring.curbStepM = 1.0 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.wiring.curbBandsM = [0.01, 0.1, 1.0] | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.wiring.check.seeds = [14, 7] | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.wiring.check.size = 20 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.wiring.check.gridM = 0.2 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.wiring.check.shareSlack = 0.03 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.wiring.check.minEdgeM = 1.0 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.wiring.check.distanceSlackM = 0.01 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| dataBook.wiring.check.maxGridPoints = 40000 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| connect.neighbourhoods.*.shift | 2 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| connect.neighbourhoods.IME = null | 1 | value | UNKNOWN |  | null or empty |
| testHost.imageStepMinutes = 30 | 1 | host | CHOSEN by Bader | Q061 | R302 |
| testHost.imageMarginMinutes = 10 | 1 | host | CHOSEN by Code | CHOSEN mark | note |
| testHost.sampleSeconds = 5 | 1 | host | CHOSEN by Code | CHOSEN mark | note |
| testHost.cimTimeoutSeconds = 2 | 1 | host | CHOSEN by Code | CHOSEN mark | note |
| testHost.helperProcesses = ["RevitWorker", "sqlservr", "CefSharp... | 1 | host | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| keyPlans.wallEdgeM = 0.3 | 1 | value | CHOSEN by Code | CHOSEN mark | note |
| keyPlans.entryHeightM.* | 2 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| keyPlans.flatTopM = 0.0005 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| keyPlans.minShare = 0.001 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| entrySpots.radiiM = [0.5, 6.0] | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| entrySpots.cornerM = 0.05 | 1 | value | CHOSEN by Code | CHOSEN mark | note |
| entrySpots.bandsM = [0.001, 0.005] | 1 | value | CHOSEN by Code | CHOSEN mark | note |
| floors.types.generic = ["Generic 150mm"] | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| floors.types.yard = ["yard"] | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| floors.types.ramp = ["Ramps 0.5%", "Ramps 2%", "Flat Ramps"] | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| floors.keyTypes.*.generic | 2 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| floors.levels.generic = "Level 1" | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| floors.levels.yard = "Level 2" | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| floors.levels.ramp = "Level 1" | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| floors.plotParameter = "Plot" | 1 | value | RECOMMENDED | Q085 | Q085 |
| floors.yardBelowFflM = 0.1 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| floors.yardShareTie = 0.01 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| floors.yardMinShare = 0.5 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| cadLinks.cutShareMin = 0.9 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| cadLinks.floorWidthStepM = 0.5 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| cadLinks.cutNeighbours = ["2374", "2391", "2543", "2546"] | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| siteBuild.newFile = null | 1 | value | UNKNOWN |  | null or empty |
| siteBuild.answerKey.others = [] | 1 | value | UNKNOWN |  | null or empty |
| siteBuild.answerKey.code = "KC" | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| siteBuild.waitMinutes = 5 | 1 | host | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| siteBuild.staleHours = 2 | 1 | host | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| siteBuild.minFreeGB = 2.0 | 1 | host | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| siteBuild.windowProcesses = ["acad", "accoreconsole", "Revit"] | 1 | host | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| siteBuild.zoneNbh = "01A" | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| siteBuild.roadSurfaces = ["Makkah Phase 1_Road Surface C3D__To... | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| siteBuild.snapM = 0.005 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| siteBuild.snapMaxM = 0.01 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| siteBuild.coordinateStepM = 100 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| siteBuild.parameterGroup = "SiteAdaptation" | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| siteBuild.steepUnits = ["C10", "VL4", "VL6"] | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| siteBuild.bandsMm = [0, 1, 10, 30] | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| siteBuild.key14.fflParameter = "FFL" | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| siteBuild.corner.roadEdgeM = 8.0 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| siteBuild.corner.onEdgeM = 0.3 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| siteBuild.footprint.groundSpanM = 0.5 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| siteBuild.footprint.mirrorGain = 0.05 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| siteBuild.footprint.slack = 0.05 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| siteBuild.placeSettleMinutes = 5 | 1 | host | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| placerCheck.position.bandsMm = [0, 1, 10, 30] | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| placerCheck.position.passMm = 1.0 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| placerCheck.level.bandsMm = [0, 1, 10, 30] | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| placerCheck.level.passMm = 1.0 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| placerCheck.dialects.imc.plot = "parameter" | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| placerCheck.dialects.imc.datum = "none" | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| placerCheck.dialects.native.plot = "name" | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| placerCheck.dialects.native.datum = "surveyElevation" | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| placerCheck.dialects.native.floorTypes.generic = ["FFL"] | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| placerCheck.dialects.native.floorTypes.ramp = ["Ramps", "Ramps 2", "Ramps 3", "Ramp... | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| placerCheck.dialects.parsons.plot = "name" | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| placerCheck.dialects.parsons.datum = "none" | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.strongShare = 0.9 | 1 | value | CHOSEN by Bader | standing rule | CLAUDE.md Majority rule, Bader's standing rule |
| majority.levelHoldMm = 5 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.levelPassMm = 0.5 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.levelBandsMm = [1, 5] | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.entryRampM = 0.3 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.entryGaps.entryGapM = 0.5 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.entryGaps.entryRampM = 0.3 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.entryGaps.sameFaceShare = 0.9 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.groups.native = ["u11", "u12", "u13", "u14"] | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.groups.parsons = ["imc", "ima", "imb", "imd", "ime", "... | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.kindLeaveOut.SE = ["rampSlope", "entry", "rampFront", "... | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.kindGroups = ["parsons"] | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.kindFoldCase = false | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.codeFrom.native = "keyUnit" | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.codeFrom.parsons = "rev9" | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.kindByPrefix.* | 3 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.slopeBands.* | 3 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.rampTypeClass.Ramps 0.5% = "0.5" | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.rampTypeClass.Ramps 2% = "2" | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.rampTypeClass.Flat Ramps = "flat" | 1 | value | RECOMMENDED | Q002 | Q002 |
| majority.groupSlopes.native.default = 2.0 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.groupSlopes.parsons.default = 0.5 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.groupSlopes.parsons.* | 3 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.depthBands.* | 3 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.heightBands.* | 3 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.sideShare = 0.5 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.wholeFaceShare = 0.98 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.measuredSlopeGroups = ["native"] | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.datumSurfaces = ["roadLevel", "Makkah Phase 1_Rough G... | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.parameterTests = ["RRE_ZZ_SP_Number-of-Steps", "RRE_ZZ... | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.wallTypes.boundary = "^BW$" | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.wallTypes.garden = "Garden" | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.wallTypes.seBoundary = "^SBW" | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.wallCentreCodes = [0, 1] | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.frontOpenShare = 0.2 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.overrideDecisions = ["rampSlope", "hand", "entry", "heigh... | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.outlineHoldMm = 10 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.pairKinds = ["duplex", "townhouse"] | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.flatRampType = "Flat Ramps" | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.rotationHoldDeg = 0.1 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.linkHoldMm = 10 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.cornerFrontRule = "orientation" | 1 | value | RECOMMENDED | Q037 | note |
| majority.siteReference.parsons = "imc" | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.siteReference.native = "u14" | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.overrideDefaultSources = ["parsons"] | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.overrideKeep.sources = ["native"] | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.overrideKeep.laterPhase = "3c" | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.overrideRounding.levelProfiles.native2022 = "pythonRound" | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.overrideRounding.levelProfiles.parsons2024Rev6 = "R385" | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.kindDiscipline.SE = "STR" | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.decidedByQuestion.Q017.fields = ["prototypeFile", "mirrored"] | 1 | value | RECOMMENDED | Q017 | the Q ID in the path |
| majority.decidedByQuestion.Q017.plots.* | 2 | value | RECOMMENDED | Q017 | the Q ID in the path |
| majority.keyRulesBlock.levelProfile = "parsons2024Rev6" | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.keyRulesBlock.zonePrefix = "Zone " | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.keyRulesBlock.zoneFrom.Zone 1.from = ["Zone 2", "Zone 3"] | 1 | value | RECOMMENDED | Q066 | majority.keyRulesBlock.zoneFrom.Zone 1.why |
| majority.keyRulesBlock.nativeRound17.rampSlope = 0.977 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.keyRulesBlock.nativeRound17.hand villa = 0.945 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.keyRulesBlock.nativeRound17.hand cluster = 0.983 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.keyRulesBlock.nativeRound17.hand alone = 0.714 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.keyRulesBlock.nativeRound17.frontCorner = 0.967 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.keyRulesBlock.nativeRound17.rampFront = 0.634 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.keyRulesBlock.nativeRound17.rampFrontFall = 0.623 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.keyRulesBlock.nativeRound17.generic = 0.899 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.keyRulesBlock.nativeRound17.yard = 1.0 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.keyRulesBlock.nativeRound17.parameter CASE = 0.314 | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.keyRulesBlock.sitePosition = "the project position, survey point a... | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.keyRulesBlock.siteNative = "the 4 Native keys hold one set of th... | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.keyRulesBlock.flatRampRule = "no flat ramp is coded, a key ramp of... | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| majority.keyRulesBlock.wallListedIn = "wallMisses of the report, outside gi... | 1 | value | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| addin.workerPeakMiB.roles = ["curbs", "baseplan", "villaBlocks"] | 1 | host | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| addin.workerPeakMiB.files.*.bytes | 3 | host | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| addin.workerPeakMiB.files.*.peakMiB | 3 | host | UNKNOWN |  | no Q, no share, no Bader line and no CHOSEN mark |
| addin.workerPeakMiB.commands.read = 3900 | 1 | host | CHOSEN by Code | CHOSEN mark | R347 |
| addin.workerTimeLimitS.read = 300 | 1 | host | CHOSEN by Code | CHOSEN mark | R347 |
| addin.workerTimeLimitS.build = 600 | 1 | host | CHOSEN by Code | CHOSEN mark | R347 |
| addin.workerTimeLimitS.levelExcel = 60 | 1 | host | CHOSEN by Code | CHOSEN mark | R347 |
| addin.workerTimeLimitS.reportExcel = 600 | 1 | host | CHOSEN by Code | CHOSEN mark | R347 |
| addin.workerTimeLimitS.exportChecks = 600 | 1 | host | CHOSEN by Code | CHOSEN mark | R347 |
| addin.workerTimeLimitS.position = 600 | 1 | host | CHOSEN by Code | CHOSEN mark | R347 |
| addin.workerTimeLimitMinS = 10 | 1 | host | CHOSEN by Code | CHOSEN mark | R347 |
| addin.position.checkPointLimitMm = 1.0 | 1 | value | RECOMMENDED | Q086 | Q086 |
| addin.position.gapDecimals = 2 | 1 | value | CHOSEN by Code | CHOSEN mark | addin.position.note |
| addin.plotTable.overlapDecimals = 4 | 1 | value | CHOSEN by Code | CHOSEN mark | addin.plotTable.note |

Every leaf of config/project.json by kind and status (RAN, audit.py): host CHOSEN by Bader 1, host CHOSEN by Code 11, host UNKNOWN 13, input input name or location, no design value 66, note note text, no value 45, setting setting of a reader or check, no design value 517, value CHOSEN by Bader 1, value CHOSEN by Code 5, value RECOMMENDED 9, value UNKNOWN 185.

## 6 The override list

config/overrides/roshn-makkah.json is 3,274,980 bytes and was read by script only (RAN).

- 12,421 overrides and 154 cannot entries. No override is marked byHand (RAN, 0 of 12,421).
- By source: parsons 7,199, native 5,222 (RAN).
- By field: entryM 3,364, heightM 3,083, parameter CASE 1,814, rampFrontM 1,394, rampFrontFallM 1,293, parameter RRE_ZZ_SP_Prototype-Foundation-Type 951, mirrored 149, rampType 75, insert 70, yardOutline 56, rampOutline 54, steps 48, genericFloor 23, rotationDeg 21, parameter RRE_ZZ_SP_RVT-Name 15, parameter RRE_ZZ_SP_Prototype-FFL 11 (RAN).
- By field and source: entryM native 1,768 and parsons 1,596, heightM native 1,539 and parsons 1,544, rampFrontM native 875 and parsons 519, rampFrontFallM native 900 and parsons 393, mirrored native 86 and parsons 63, rampType native 54 and parsons 21, every other field parsons only (RAN).
- By key: imf 1,729, u13 1,582, imd 1,442, u14 1,372, u12 1,321, imc 1,227, ime 1,031, u11 947, img 849, ima 518, imb 403 (RAN).
- 9,395 overrides have rule null (RAN).
- The cannot list by field, all parsons: entryM 32, rampFrontM 32, rampFrontFallM 30, heightM 27, parameter RRE_ZZ_SP_Prototype-FFL 27, insert 2, rampOutline 2, rotationDeg 2 (RAN).
- Status: every entry is a past model value of one plot that the coded rule does not give, and the list as a whole is the open Q053 (READ, steps/questions-open.md line 17, keep all as the majority rule says). So all 12,421 are RECOMMENDED by Q053 (CHOSEN by this audit from that text). Narrower open questions over parts of it: Q052 the weak rules held through their overrides, Q056 the native models for hands, fronts and ramps only, Q075 the native entries with no value (READ, steps/questions-open.md).

## 7 Values written into code

The sweep is of the Explore helper of this session over python/ and dotnet/, the tests left out (python/tests/, conftest.py, dotnet/SiteAdaptation.Tests/, dotnet/SiteAdaptation.TestHost/ and the TestHost files of SiteAdaptation.Common). Its commands are in its report, the main ones: `rg -n -P '^\s*[A-Z][A-Z0-9_]+\s*=\s*[^=]' python` for the Python module constants and `rg -n -P '(const|static readonly)\s+(double|int|long|float|decimal)\s+\w+\s*=' dotnet` for the C# constants. Rerun in this session: 333 Python module constants with a number and 70 C# numeric constants outside the tests (RAN), of which the helper kept the 120 rows below as design values and set apart host settings, float epsilons (Python 87 lines, C# 28) and unit conversions (Python 133 lines, C# 28). Six rows were opened by file and line and match: P1, P36, P82, C1, C9 and C11 (RAN).

Config: DUP is the same value under a config key, NOT is not in config. A note "value only" means the same number sits in config under a key of another meaning.

| Row | File and line | Literal | What it sets | Rule | Config |
|---|---|---|---|---|---|
| P1 | python/face_reasons.py:14 | SLIVER_AREA 50.0 | sliver face area | none found | NOT |
| P2 | python/face_reasons.py:15 | SMALL_AREA 1000.0 | small face area | none found | NOT |
| P3 | python/face_reasons.py:16 | NEAR 10.0 | near distance, unit not stated | none found | NOT |
| P4 | python/face_reasons.py:17 | OWNER_NEAR 30.0 | owner near distance | none found | NOT |
| P5 | python/entry_levels.py:15 | BANDS 0.005, 0.010, 0.030 m | entry level error bands | R93 to R95 | NOT, value only siteBuild.bandsMm |
| P6 | python/entry_levels.py:19 | MIN_EDGE 1.0 m | shortest front edge | none found | NOT, value only wiring.check.minEdgeM |
| P7 | python/entry_levels.py:21 | FRONT_TIE 1.0 m | front tie distance | R94 | NOT |
| P8 | python/levels.py:21 | HALF_MM 0.0005 m | half mm rounding of a height | R67 | NOT, value only keyPlans.flatTopM |
| P9 | python/levels_proof.py:29 | FFL_TOLERANCE 0.0005 m | FFL match tolerance | R28, R67, R95 | NOT, value only keyPlans.flatTopM |
| P10 | python/levels_proof.py:32 | ROUNDING_LIMIT 0.01 | rounding limit of the sheet check | R28 | NOT |
| P11 | python/levels_proof.py:35 | FLOORS cm 10, fiveMm 5 | sheet level grain in mm | R67 | NOT |
| P12 | python/link_summary.py:12 | FFL_TOLERANCE 0.0015 m | link FFL tolerance | R112 | NOT |
| P13 | python/link_summary.py:23 | LEVEL_TOLERANCE 0.0005 m | link level tolerance | R112 | NOT |
| P14 | python/plan_from_past.py:42 | CORNER_ON_VERTEX 0.002 m | ramp corner on an outline vertex | R2 | NOT |
| P15 | python/plan_from_past.py:44 | ARC_FIT 0.001 m | arc end to circle | R80 to R83 | NOT |
| P16 | python/plan_from_past.py:47 | SAME_LOOP_SHARE 0.01 | top loop equals sketch loop | R80 to R83 | NOT |
| P17 | python/plan_from_past.py:49 | ARC_SAG 0.0001 m | arc chord sag | R80 to R83 | NOT |
| P18 | python/plan_from_past.py:51 | AXIS_TOLERANCE 0.002 | link axis match | R80 to R83 | NOT |
| P19 | python/plan_from_past.py:52 | LEVEL_CONFLICT 0.0015 m | level conflict | R80 to R83 | NOT |
| P20 | python/plan_from_past.py:53 | TOP_CONFLICT 0.005 m | top conflict | R80 to R83 | NOT, value only siteBuild.snapM |
| P21 | python/plan_from_past.py:54 | PROOF_LIMIT_MM 1.0 | proof limit | R80 to R83 | NOT |
| P22 | python/plan_from_past.py:77 | SLIVER_SHARE 0.0001 | sliver share | R80 to R83 | NOT |
| P23 | python/pdf_reader.py:11 to 20 | 10 literals, COLUMN_SLACK 4 to JOIN_GAP 0.6 | PDF label and stamp thresholds in points | none found | NOT |
| P24 | python/placer_check.py:25 | POSITION_MM 1.0 | position pass limit | R105 to R119 | DUP placerCheck.position.passMm |
| P25 | python/placer_check.py:26 | LOOSE_MM 10.0 | loose tolerance | R105 to R119 | NOT, value only placerCheck bandsMm |
| P26 | python/placer_check.py:27 | ANGLE_TOLERANCE 0.01 deg | angle tolerance | R105 to R119 | NOT |
| P27 | python/z16_check.py:28 | CHECKS gridM 0.2 | grid of the check | R235 | DUP wiring.check.gridM |
| P28 | python/z16_check.py:28 | CHECKS shareSlack 0.01 | share slack | R235 | NOT, config wiring.check.shareSlack is 0.03 |
| P29 | python/z16_check.py:28 | CHECKS offsetSlackM 0.05 | offset slack | R235 | NOT |
| P30 | python/z16_check.py:28 | CHECKS levelSlackMm 1.0 | level slack | R235 | NOT |
| P31 | python/z16_check.py:156 | box test under 200 | search window for curbs | R235 | NOT |
| P32 | python/z16_check.py:286 | bands 0, 1, 10, 30 | error bands in mm | R235 | DUP siteBuild.bandsMm |
| P33 | python/z16_check.py:243 to 244 | defaults 14,7 and 20 | check seeds and size | R235 | DUP wiring.check.seeds and size |
| P34 | python/z4_check.py:118 to 119 | defaults 14,7 and 20 | check seeds and size | R217 | DUP wiring.check.seeds and size |
| P35 | python/z4_compare.py:30 to 35 | EQUAL_M 1e-6, EDGE_MM 1e-6, ROTATION_EQUAL_DEG 0.01, EDGE_M 1.0 | equality tolerances | R217 | NOT |
| P36 | python/surface_roles.py:4 | GROUND_COVER 0.95 | share of corners a ground surface covers | none found | NOT |
| P37 | python/surface_roles.py:6 | NEAR 0.03 m | corner on a surface | none found | NOT, value only wiring.check.shareSlack |
| P38 | python/ramp_levels.py:16 | CHECK_TOLERANCE 0.02 | ramp check tolerance | R91, R92 | NOT |
| P39 | python/ramp_levels.py:18 | DIFF_EDGES 0.01, 0.03, 0.1, 0.3 m | band edges | R91, R92 | NOT, value only curbBandsM, entryRampM |
| P40 | python/ramp_levels.py:21 | SPOT_RADIUS 0.05 m | spot radius | R91, R92 | NOT |
| P41 | python/footprint_check.py:38 | OFFSET_BANDS 0.010, 0.100, 1.0 m | offset bands | R229 | NOT, value only wiring.curbBandsM |
| P42 | python/footprint_check.py:39 | SHARE_BANDS 0.95, 0.90, 0.80 | share bands | R229 | NOT |
| P43 | python/footprint_check.py:58 | buffer 0.05 m | union seam closing | R229 | NOT, value only siteBuild.footprint.slack |
| P44 | python/cad_links.py:34 | BANDS 0.001, 0.010 m | link offset bands | R120 to R123 | NOT, value only entrySpots.bandsM |
| P45 | python/cad_links.py:37 | SLIVER 1e-4 | neighbour sliver share | R120 | NOT |
| P46 | python/cad_links.py:38 | ROTATION_TOLERANCE 0.1 deg | link rotation tolerance | R120 | NOT, value only majority.rotationHoldDeg |
| P47 | python/cad_links.py:42 | UNION_SLACK 0.001 m | seam closing | R122 | NOT |
| P48 | python/cad_analysis.py:26 | SEGMENT_FACTOR 4 | a shared segment exceeds 4 tolerances | R69 to R71 | NOT |
| P49 | python/cad_analysis.py:28 | COVER_SHARE 0.2 | a block covers a plot at 20 percent | none found | NOT, value only frontOpenShare |
| P50 | python/cad_analysis.py:32 | HINT_MIN_PLOTS 2 | fewest plots of a hint | none found | NOT |
| P51 | python/block_offsets.py:30 | MIN_PLOTS 4 | fewest plots of a group | R97, R98 | NOT |
| P52 | python/block_offsets.py:32 to 33 | OFFSET_BANDS 0.001, 0.010 and ENTRANCE_BANDS 0.010, 0.100 m | error bands | R97, R98 | NOT |
| P53 | python/block_offsets.py:38 | BLOCK_SIZES 1, 2, 4 | block sizes tested | R69 | NOT |
| P54 | python/block_proof.py:18 | NEAR_TIE 1e-4 | near tie of shares | R69, R76 | NOT |
| P55 | python/c3d_check.py:23 to 25 | LEVEL_SLACK 0.0011, CURB_SLACK 0.05, ROW_SLACK 0.01 | check slacks | R159 | NOT |
| P56 | python/c3d_check.py:33 | under 10 | point near a triangle | none found | NOT |
| P57 | python/cad_plan_check.py:25 to 28 | FFL_SLACK 0.0006, CORNER_SLACK 0.001, LENGTH_BANDS | check tolerances | R130 | NOT |
| P58 | python/cad_floor_check.py:26 to 30 | POINT_SLACK 0.0015, LEVEL_SLACK 0.0015, FIT_SLACK 0.002, DENSIFY 0.05 | check tolerances | R139 | NOT |
| P59 | python/cad_links_check.py:18 to 21 | REPORT_SLACK 0.0006, ANGLE_SLACK 0.05, STRAIGHT 1e-3 | check tolerances | R120 to R124 | NOT |
| P60 | python/cad_rules_check.py:22 | ON_LINE 0.01 m | edges on one line | R134 | NOT |
| P61 | python/cad_rules_check.py:90 | over 60 m | ring distance limit | R134 | NOT |
| P62 | python/cad_rules.py:30 | FFL_SAME 0.0005 | FFLs are one level | none found | NOT |
| P63 | python/rules_checks.py:61 | FFL_SAME 0.0005 | FFLs are one level, a second copy of P62 | R65 to R75 | NOT |
| P64 | python/round13_check.py:27 to 30 | LEVEL_TOLERANCE 1e-6, OFFSET_TOLERANCE 0.0008, ANGLE_TOLERANCE 0.05 | check tolerances | R91, R97 | NOT |
| P65 | python/key_rules_check.py:62 | CELL_M 5.0 m | triangle grid cell | R341 | NOT |
| P66 | python/key_rules.py:28 to 29 | SAME_MM 0.5, SAME_DEG 1e-6 | same point, same angle | R311, R331 | NOT, value only majority.levelPassMm |
| P67 | python/position_file.py:54 to 55 | SAME_MM 0.5, SAME_DEG 1e-6 | a second copy of P66 | R311, R331 | NOT |
| P68 | python/key_plans_check.py:46 to 59 | GRID 50 and six tolerances | check grid and tolerances | R2, R322 | NOT |
| P69 | python/key_reads_check.py:43 | ORIGIN_TOLERANCE 0.0005 m | origin match | R2 | NOT |
| P70 | python/key_reads_check.py:46 | BANDS_MM 1, 5 | floor top bands | R320 to R323 | DUP majority.levelBandsMm |
| P71 | python/key_reads_check.py:49 to 51 | EDGE_IN_PLAN 0.005 m, PLANE_FIT 0.001 m | edge and plane fit | R2 | NOT |
| P72 | python/key_reads_check.py:35 to 36 and key_plans_check.py:39 to 40 | SEEDS 14, 7 and SAMPLE 20 | check seeds and sample | none found | DUP wiring.check.seeds and size |
| P73 | python/cad_plan_check.py, cad_rules_check.py, cad_links_check.py, cad_floor_check.py | SEED 14, SAMPLE 20 | check seed and sample | none found | DUP wiring.check.seeds and size |
| P74 | python/block_offsets.py:28, floor_fit.py:36, entry_proof.py:28, round13_check.py:24 to 25 | SEED 13, SAMPLE 10 | check seed | none found | NOT |
| P75 | python/data_book_check.py:26 to 28 | SAMPLE 200, AREA_SLACK 0.01, SHARED_M 0.5 | check sample and tolerances | R168, R177, R178 | NOT |
| P76 | python/data_book.py:42 | SHARED_M 0.5 m | shared point match | R162 to R164 | NOT |
| P77 | python/data_book_retest_check.py:29 | EXPORT_SLACK 0.001 | export match | R170 | NOT |
| P78 | python/data_book_cad.py:28 | ARC_STEP_DEG 5.0 | arc polygon step | R162, R164 | NOT |
| P79 | python/link_table.py:11 to 12 | EDGE_TOLERANCE 1.0 m, ARC_STEP pi/36 | edge tolerance, arc step | none found | NOT |
| P80 | python/floor_fit.py:38 to 41 | OUTLINE_BANDS, LEVEL_BANDS, DENSIFY 0.05 m | floor fit bands | R121 to R139 | NOT, value only siteBuild.bandsMm |
| P81 | python/plan_compare.py:20 to 21, 96, 138 to 140 | POSITION_TOLERANCE 0.001, ANGLE_TOLERANCE 0.01, bands 5, 10, 30 mm | plan compare | R129, R138 | NOT |
| P82 | python/plan_rules.py:638 | (mm // 10) / 100 | a level down to 10 mm, fflDownToTheCm | R339, R340 | NOT, the rule name is in the rules profile, the 10 mm grain is not |
| P83 | python/plan_rules.py:520 to 523, levels.py:15 to 25, common.py:6 to 9 | ENTRY 4, FFL 3, BAND 6, DECIMALS 3, WIDE 4, SIGNIFICANT 15 | rounding | R385, R339, R340 | NOT |
| P84 | python/yard_rule.py:12 | SHARE_DECIMALS 4 | share rounding | R111, R28 | NOT |
| P85 | python/second_source.py:14 to 15 | SEVERAL 2, INSIDE 0.9 | second source inside share | none found | NOT, value only cutShareMin |
| P86 | python/road_check.py:19 | STORED_TOLERANCE 0.0006 | stored road level match | none found | NOT |
| P87 | python/data_book_retest.py:169 to 170 | code C10 | steep unit kind | R170 | NOT, value only siteBuild.steepUnits |
| P88 | python/data_book_analysis.py:128 | TH1-IM | TH1-IM naming check | none found | NOT |
| P89 | python/c3d_connect.py:393 | neighbourhoods IMC | IMC neighbourhood shift key | R158 | DUP connect.neighbourhoods.IMC |
| P90 | python/corner_front.py:40, key_rules_check.py:50, z16_check.py:29 | quadrants EN, SE, WS, NW by 90 degrees | orientation quadrants | R224, R225 | NOT |
| T1 | python/cad_rules_check.py:58, 118, block_offsets.py:319, cad_links_check.py:133 | RRE_ZZ_SP_Plot-Number | link parameter name | none found | DUP pastAll.linkParameters |
| T2 | python/cad_rules_check.py:124 | RRE_ZZ_SP_Prototype | link parameter name | none found | DUP pastAll.linkParameters |
| T3 | dotnet/SiteAdaptation.Common/LinkFields.cs:13, Placer.cs:17 | RRE_ZZ_SP_Prototype-FFL | FFL parameter name | R339 | DUP majority.parameterLevels |
| T4 | python/key_plans_check.py:44 | RRE_ZZ_SP_Number-of-Steps | steps parameter name | R325 to R327 | DUP majority.parameterTests |
| T5 | dotnet/SiteAdaptation.Common/LinkFields.cs:16 | RRE_ZZ_SP_RVT-Name | link name parameter | none found | DUP pastAll.linkParameters |
| T6 | python/plan_from_past.py:65 | El @ Entry, FFL | sheet column labels | R325 | NOT |
| T7 | python/area_key.py:41, data5_check.py:41, im_codes.py:223 | Zone %d and its pattern | zone label form | R301, R350 | NOT |
| T8 | python/key_copies.py:269, plan_from_past.py:63 | _TEST- | key copy suffix | R312, R321 | NOT |
| T9 | python/full_run.py:13 to 14 | two folder names of 01_Output | real input folders | none found | NOT |
| C1 | dotnet/SiteAdaptation.Common/Placer.cs:12 to 13 | CoordinateTolerance 0.001 m, AngleTolerance 0.001 | placement tolerances | R83 to R87 | NOT |
| C2 | dotnet/SiteAdaptation.Common/Placer.cs:14 to 16 | ZTolerance 0.001, ReadBackOrigin 0.001, ReadBackAngle 0.01 | placement read back | R83 to R87 | NOT |
| C3 | dotnet/SiteAdaptation.Common/FloorPlacer.cs:15 to 16 | TopTolerance 0.001, LevelTolerance 0.001 | floor top and level match | none found | NOT |
| C4 | dotnet/SiteAdaptation.Common/FloorPlacer.cs:18 | CornerOnVertex 0.002 m | ramp corner on a vertex, as P14 | none found | NOT |
| C5 | dotnet/SiteAdaptation.Common/FloorPlacer.cs:20 | ShortestEdge 0.001 m | shortest curve Revit takes | none found | NOT |
| C6 | dotnet/SiteAdaptation.Common/FloorMath.cs:75 | BottomNormalLimit -0.001 | bottom face normal | none found | NOT |
| C7 | dotnet/SiteAdaptation.Common/OutlineLoops.cs:10 | Tolerance 0.001 m | one point | none found | NOT |
| C8 | dotnet/CadReader/PlotShapes.cs:16 to 17 | Tolerance 0.01 m, MinArea 0.001 | shape tolerance and least area | none found | NOT |
| C9 | dotnet/SiteAdaptation.Common/Plan/Hands.cs:76 | Band 0.05 m | buffer of the side neighbours | none found | NOT, value only siteBuild.footprint.slack |
| C10 | dotnet/SiteAdaptation.Common/Plan/Hands.cs:77 | Quantum PI/2/16 | buffer arc step | none found | NOT |
| C11 | dotnet/SiteAdaptation.Common/Plan/Fronts.cs:201 to 202 | FrontTie 1.0 m, Probe 0.01 m | front tie and outward probe, as P7 | none found | NOT |
| C12 | dotnet/SiteAdaptation.Common/Plan/Fronts.cs:206 | quadrants EN, SE, WS, NW | orientation quadrants, as P90 | none found | NOT |
| C13 | dotnet/SiteAdaptation.Common/SiteSpec.cs:70 | ToleranceMm 1.0 | shared coordinate check | R211 | NOT, value only placerCheck passMm |
| C14 | dotnet/SiteAdaptation.Common/Read/ProjectPositionFile.cs:87 to 88 | SameMm 0.5, SameDeg 1e-6 | same point, same angle, as P66 | R311, R331 | NOT |
| C15 | dotnet/SiteAdaptation.Common/Plan/KeyLevels.cs:260 | FloorDivide(mm, 10) / 100 | fflDownToTheCm, as P82 | R339, R340 | NOT |
| C16 | dotnet/SiteAdaptation.Common/SiteFacts.cs:71, Common/Rounding.cs:12, Common/JsonOut.cs:13 | AngleDecimals 6, Significant 15, Decimals 3 | rounding | R311, R385 | NOT |
| C17 | dotnet/CadReader/Exploder.cs:19 | ArcStep PI/18 | arc polygon step | none found | NOT |
| C18 | dotnet/CadReader/PlotLabels.cs:13 | ArcStep PI/36 | arc polygon step | none found | NOT |
| C19 | dotnet/SiteAdaptation.C3DProbe/Civil/DataCommands.cs:34 | ArcStepDeg 5 | arc polygon step, as P78 | none found | NOT |
| C20 | dotnet/SiteAdaptation.Common/Read/LocationPlan.cs:339 | WordTolerance 3 | label word match, unit not stated | none found | NOT |
| C21 | dotnet/RevitPrototypeReader/ExportPrototypesCommand.cs:105 | under 20 MB | prototype export size | none found | NOT |

Counts of the helper: 120 rows, Python 97 and C# 23, 14 DUP (Python 12, C# 2), 106 NOT (Python 85, C# 21). A row may hold several literals. The ones a build or the add-in uses on a real input are the C# rows of Placer, FloorPlacer, Fronts, Hands, KeyLevels and the rounding (C1 to C5, C9 to C16) and their Python twins (P7, P14, P66, P82, P83, P90). The rest are tolerances of the one-off checkers of past rounds (READ, the files named).

## 8 Do the outputs show which plots used an assumed value

Assumed here is a value of status RECOMMENDED, CHOSEN by Code or UNKNOWN, and a MEASURED rule that is weak. On this audit that is 562 of the 591 values: 528 UNKNOWN, 9 RECOMMENDED, 16 CHOSEN by Code and the 9 weak of the 35 MEASURED (RAN, audit.py and section 5).

- The plan of plan_from_book.py: no, not the status. Each value of a plot is a pair of value and source (READ, python/plan_from_past.py:81 `return {"value": value, "source": source}`), the sources being schedule, locationPlan, estimate, override, keyRules, footprint, levelProfileStepBands and pastJob (READ, python/plan_from_book.py:58 to 69, python/plan_from_cad.py:36). So a plot shows a value came from an override, an estimate or keyRules, but not that the keyRules entry is weak, that an open Q carries it, or that Code chose its threshold.
- The plan summary, step 15 (READ, docs/design/05-plan-steps.md:50): no. It gives counts by field, not plots by status: `"rules": {"levelProfile": ..., "applied": ..., "handRules": ..., "overridesNotApplied": ..., "steps": {"from": "keyRules.steps %s of %s" ...}}` (READ, python/plan_from_book.py:1060 to 1070), and summary_lines prints `built by keyRules of %s (R337)` (READ, python/plan_from_book.py:1431). The place for it is a new count beside applied.
- The check table of button 3 (READ, docs/design/03b-buttons-2-to-8.md:18 and docs/design/07-mock.md 7.4): only in part. Its rows come from seven sources, one is `a plot and field an open question decides (majority decidedByQuestion)`, and the Rule column names that Q, as the mock row `| 2301 | 012 | VD4 | Unit code | ... | Typology_Rev9, Q017 | Rule |`. A plot that follows a weak rule, a threshold Code chose or an UNKNOWN value with no override has no row and counts in `[n] plots follow every rule`. The place for it is one more chip and source of rows, Assumed.
- The report Excel of button 8 (READ, docs/design/07-mock.md 7.6, line 238): per decision, not by status. The box Rules applied has `one line per decision of keyRules the build applied with its coded rule, share and strong or weak`, such as `entry roadLevel 0.088 weak`, and gives plot counts for some decisions, the hand rules and the front rule, such as `the 110 plots of NBH 012 with two or more road edges`. It names no plot by number, no open Q and no UNKNOWN or Code value. The place for it is a plot count by status on each line of that box and a sheet Assumed beside Overrides.

## 9 What happens today when a value changes

- Nothing marks a step to run again. Each run reads config/project.json and rules/roshn-makkah.json from disk again, so a change takes effect at the next run of a step and only then (READ, python/plan_from_book.py build_inputs at 1137 to 1143, project_profile.profile_path).
- What exists: each step file records the SHA-256 of the config it read, configSha256, and of every input by role, rules and overrides among them (READ, python/parity.py:106 to 117, python/plan_from_book.py:1125 to 1129 and 1140 to 1141, R345, R347). The worker hashes each input before and after a run and refuses a run whose input changed under it (READ, dotnet/SiteAdaptation.Common/Worker/WorkerRunner.cs:280 to 294). Parity refuses two step files written from other inputs (READ, python/parity.py:192 to 196).
- None of this compares an input with the last run before the person starts one, so the tool does not know a value changed until it runs and nothing tells the person which later steps are stale (READ, the same lines, SEARCHED step state in python/ and dotnet/).
- keyRules values are written by python/key_rules_block.py from the majority.json of a run of python/majority.py, by text, so a value edited by hand in rules/roshn-makkah.json holds until key_rules_block.py writes that decision again (READ, python/key_rules_block.py:1 to 10, R338).
- R366, planned for 4h and not built (READ, steps/01_next.md:23, docs/design/03a-buttons-shared-and-1.md:18): one SHA-256 over every input of a step, the files of the pane, Settings, every config file beside it the step reads, the rules file, the override list and the project position file. Every output the run record lists must be on disk with its SHA-256. A folder input is hashed by its file list. An input changed since a step makes that step and every step after it need running again, shown in the pane as Done, Done with [n] issues, Ready or Needs step [n], with the order 2 needs 1, 3 needs 2 up to 7 needs 6, and 8 needs 3.

## 10 The smallest change that shows every assumed value per plot

Proposed, not coded (CHOSEN, a proposal for Bader, Q102).

1. A status beside each source. Each sourced value of a plot of the plan gets a third field, status, one of the five, taken once per build from one table: keyRules share and strong give MEASURED or weak, a decidedByQuestion or Q named in the rules profile gives RECOMMENDED with the Q, the word CHOSEN gives CHOSEN by Code, a field with none of these gives UNKNOWN. The table is a new block status of rules/roshn-makkah.json, by field, written from steps/investigations/rule-status-scripts/audit.py once it moves into python/, so no status sits in code.
2. One count in the plan summary of step 15: plots by field and status, the assumed ones listed by plot, as applied is now.
3. The check table gets a chip Assumed, one row per plot and field of an assumed status, and the report Excel a plot count on each line of Rules applied and a sheet Assumed. Both read the status of the plan, nothing new is measured.

## 11 New questions

Full text in steps/questions-open.md.

- Q100 Status of values from client documents.
- Q101 Level profile fields with no share.
- Q102 Show the assumed values per plot.
- Q103 Design values in code, not in config.
- Q104 Audit statuses rest on text matching.

## 12 Second check

By another tool than the script, steps/investigations/rule-status-scripts/second_check.ps1, PowerShell with ConvertFrom-Json and Select-String, on the file as written (RAN):

- Rule ids of docs/rules/index.md 316, rows of sections 3 and 4 188 and 128, sum 316.
- keyRules and hands entries with a share in rules/roshn-makkah.json 35, MEASURED rows of section 5 35, shares that differ from the JSON 0.
- Sums of n of section 5 by status: CHOSEN by Bader 3, CHOSEN by Code 16, MEASURED 35, RECOMMENDED 9, UNKNOWN 528, the counts of section 2.

The claim-checker of PR 68 recounted the leaves, the 35 MEASURED, the tables of sections 3 to 5 and every count of section 6 with a Python script of its own that does not import audit.py and found them equal (RAN, pass 2). The rows the reviews found on a Q of another key or with a CHOSEN mark not read, imageMarginMinutes, pairKinds, overrideRounding, rotationHoldDeg, the position limits and the worker limits, now follow the rules of section 1. What text matching cannot settle is Q104.
