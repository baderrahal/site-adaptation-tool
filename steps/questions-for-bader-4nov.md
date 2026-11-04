# Questions for Bader, 4 Nov 2026

Every open question of the four lanes, from steps/questions-open.md at main, with the recommended answer. Round TRUTH-4NOV-C. The full text of each stays in steps/questions-open.md.

Count: 197 open, 196 there on the audit's read and Q441 opened by the audit (RAN).

Audit column: CHANGED means the audit changes the recommended answer, AFFECTED means it adds facts and the answer stands, empty means no effect found. Marks: PAST, the subject is a past output. PAST~, the answer or its evidence leans on one. REVIT, not seen in Revit. No REVIT mark does not prove a thing was seen in Revit.

**The audit closes no question.** Each is Bader's to answer. CHANGED: 9. AFFECTED: 20.

**Answer these first, they decide the rest:** Q441, Q255, Q379, Q361, Q439.

Lane comes from the question text or the lane's line in steps/HEADSUP.md, a star means named only as the owner of the work, a dash means no lane is named. The recommended answers are shortened, and a rewording here is not a new answer until Bader takes it.

Faults in the open file, left as they are: its index at the top does not match the text. 14 ids in the index are closed (Q148, Q156, Q190, Q214, Q215, Q216, Q218, Q219, Q220, Q221, Q331, Q333, Q338, Q391) and 32 with text are not in it. One paragraph above Q343 has no id, it reads as a leftover of closed Q340. Q452, Q453, Q454 and Q456 have no other answer.

## Changed by the audit

| ID | Title | Recommended now | Why |
|---|---|---|---|
| Q441 | Order of the raw input changes | Truth first: short list 7, 5, 11, then 1, 2, then 3, 4, 6 | steps/truth-audit.md section 8 |
| Q157 | The type button 1 copies new types from | A new project copies from a type in its own model, the IMC key only for the past job | A new team has no IMC key |
| Q196 | addin.overrideFile in config | Name the list for the past job only, a new project names its own empty list | RAN: the list changes 322 of 336 plots on NBH 012 |
| Q228 | Default slopes of road plus slope | Each project states its slopes, the tool ships no default from the past job | The slopes decide door level on a new project, and were CHOSEN from a past value |
| Q255 | The override list under rev6 | Same answer, and answer it first: a Rev 06 build applies no past job override, only team edits | RAN: Rev 06 with the full list still copies past values, FFL over 5 mm on 232 of 336 |
| Q379 | A new project on another PC | Own round, but no schedule folder row: step 2 must run with no schedule, short list 1, 2, 8 | RAN: step 2 and 4 stop with no past file. Its text says button 1 stops, the window now offers CAD |
| Q401 | 12 plots differ from the past FFL | Accept, no override, and stop quoting 324 of 336 as proof, the list was on in that count | READ 6c-excel.md: the 324 came with the list on, 94 of 324 without |
| Q439 | A model built before the pull | Start the model again for any kit now, and build button 5 replacing its own floors, short list 12 | READ: the same skip can leave some floors off plan with no stop, and Q476 hides the lines |
| Q472 | Ramp front overrides stand off the road | Decide with Q255, under Rev 06 these overrides go | They are stored past offsets |

## All open questions

| ID | Lane | Title | Recommended | Marks | Audit |
|---|---|---|---|---|---|
| Q149 | - | M14 boundary wall of an irregular plot | PDF reading: outer face on the plot limit, 200 mm inside, canvas text rewritten so |  | AFFECTED. Facts: no thickness is set on BW at all, READ RevitSiteDocument.cs |
| Q150 | - | M15 foundation depth, at least or equal | Keep canvas text, at least 1.20 m, as the check of 9 Report |  |  |
| Q151 | - | Q075 moved without the majority run | Accept the move, run --replace native when Native reads return, re-measuring the other 3,876 entries | PAST |  |
| Q152 | - | Mode of the last run of a button | Mode is how the real run was confirmed: afterDryRun or direct |  |  |
| Q153 | - | A lone copy of a tagged element | Keep: list and leave the element, skip the item, never take over what it did not place |  |  |
| Q154 | B | Freeze guard limit for an image job | Image job takes testHost.imageStepMinutes for not responding, every other run 5 minutes | REVIT |  |
| Q157 | B | The type button 1 copies new types from | A new project copies from a type in its own model, the IMC key only for the past job | PAST~ REVIT | CHANGED |
| Q159 | - | Q055 reads 11 more 1.4 plots off the ramp | Keep the one rule on all 16 plots | PAST |  |
| Q160 | - | A plot list takes unit plots only | Keep it, buttons place villas on unit plots alone |  |  |
| Q161 | - | A changed step reruns every higher step | Keep it, the report reads what 5 to 8 placed |  |  |
| Q162 | - | Reads of entryM with heightM or rampFrontM | Keep the one rule on every value, as Q119 reads | PAST |  |
| Q163 | - | A yard that stands only by its mirror | Mirror override marks every value that would not stand without it, found by build with and without | PAST |  |
| Q164 | - | Levels count and the link Z of 7 | No, Levels stays the floors of the last run of 6, link Z becomes a check of 7 |  |  |
| Q165 | B* | Shape of the block manualRules | List with id, text, group, section, steps, worded as 13-workflow-changes.md, as R381 reads |  |  |
| Q166 | - | Manual rule of a check row | Take the row's own manual rule ID once 5c writes it, else the IDs of its step |  |  |
| Q167 | - | Manual rule on the other sheets | Keep it on the Summary and Issues lines |  |  |
| Q168 | - | The villa type of Update from report | Keep saving as override field unit, a keyRules build applies it in Phase 6 |  |  |
| Q169 | - | Key of an update entry with none of its job | Take the answer key whose join holds the plot | PAST~ |  |
| Q170 | B* | updateChecks of manualRules | M04 oneFflPerPiece, M08 stepsAtMost and stepHeights, M09 riser, M10 oneStepUnits |  |  |
| Q171 | - | Which breaks untick a change | Only a manual rule break unticks, a profile rule break stays ticked with the rule named |  |  |
| Q172 | - | heightM gap with an El @ Entry edit | Keep the gap to levelRule.ffl of the plan, measure the effect on the rebuilt plan in 6a |  |  |
| Q173 | B* | Level profile of Update from report | The Level profile of the pane, once Lane B exposes it to the buttons |  |  |
| Q174 | - | The plan Update from report reads | The plan of the last real run of 4 Check, named by its run record |  |  |
| Q175 | - | Run log format 9 Report reads | Keep this shape as the format R383 writes for every button |  |  |
| Q176 | - | Plain names of the update rule checks | Keep the 9 names as drafted, listed in docs/design/14-ui-labels.md |  |  |
| Q177 | B* | Manual rule ids in Rule check | Show the id until 5c gives a plain name, then the name with id as tooltip |  |  |
| Q178 | - | A computed mirror edit breaks the hand rule | Keep it, the team sees the break and decides |  |  |
| Q179 | - | Guide line, side box and row mark of Update | Add a How to use line, no side box, no selected row |  |  |
| Q180 | B | designValues status in the R401 block | Bader confirms them CHOSEN by Bader, the block says so |  |  |
| Q181 | B | Timing of the labels J11 and J14 | In the PR of the window that changes those labels |  |  |
| Q182 | B | Side band of majority.side_neighbours | Move it to hands.sideBandM in config in the next code PR |  |  |
| Q183 | B | Time cap of a buttons job | Accept for the Revit run after 5c, change by measured open and run times |  |  |
| Q184 | B | Plain rule names of the check table | Accept, Bader checks the wording in the shots | PAST~ |  |
| Q185 | B | A plot list over two NBHs | Yes, stop, the plan is per NBH |  |  |
| Q186 | B | Close J2 as no work | Close it |  |  |
| Q188 | B | Entity load on a session unseen | Yes, load it, in a later PR |  |  |
| Q189 | B | Check table modal or modeless | Modal for now, modeless in 6c |  |  |
| Q191 | B | The plan name of a plot list | The nbhId its plots share, else all |  |  |
| Q192 | - | Solid height of light mode | 3.0 m, one storey |  |  |
| Q193 | - | Footprint limit of the base | 100 mm, measured on NBH 012 in 5d |  |  |
| Q194 | - | RRE parameters on Generic Models | Yes, parameter sheet gains Generic Models, until then a solid lists its plot as an issue |  |  |
| Q196 | - | addin.overrideFile in config | Name the list for the past job only, a new project names its own empty list | PAST | CHANGED |
| Q197 | - | key_join.json and key reads as inputs | Two optional pane rows, Key join and Key reads | PAST | AFFECTED. Facts: the pane names no key, so step 4 has no live compare. This is the way to make the compare separate |
| Q198 | - | Past gaps of the missing and question rows | Take them from the past_all.csv rows of the keys | PAST | AFFECTED. Goes with Q197 |
| Q199 | - | What the tool does per manual rule | Code drafts one line per rule from docs/rules, Bader checks them together |  |  |
| Q200 | A | Export to Excel of the two windows | The worker writes the xlsx (R350), the button asks the worker, for both windows |  |  |
| Q203 | - | A worker end on a document not active | The message at once in 5c, the wait of 02-folders.md in 5d |  |  |
| Q204 | - | Manual rule of a Review data row | M01 now, per column once manualRules is in the profile (M02 cluster, M13 front edge) |  |  |
| Q205 | - | Corner table coordinates of Review data | X, Y in model coordinates, Shared coordinates the CAD value |  |  |
| Q206 | - | Base boundary of a prototype | Outer loop of the lowest level floor by default, named in config, measured against CAD blocks |  |  |
| Q207 | - | No foot and no Esc in the windows | Add both as the spec says |  |  |
| Q208 | B | How to use line of Rules | Code drafts one line, Bader checks it |  |  |
| Q209 | - | Manual column mixes section and revision | Show the section only, the revision in the title |  |  |
| Q210 | - | Q015 in a past job text | Drop the id from the text a user reads | PAST |  |
| Q211 | B | 7 after a plot list 4 Check | Stop as now, 7 asks for a 4 Check on the NBH |  |  |
| Q212 | - | 4 Check stops 7 on another NBH | Keep as R377 says, one plan per newest 4 Check |  |  |
| Q213 | - | Use of an override entry under Skip | Accept: "rule" gives Rule, any other value gives Past | PAST |  |
| Q217 | - | Follow every rule count, table against chip | The table takes the build's count |  |  |
| Q222 | - | The NBH 03 image of Q063 | Bader names the area and the level | REVIT |  |
| Q223 | - | Test host gives up after a refused raise | Retry a refused raise on the next tick, a test host change in 5d |  |  |
| Q225 | - | The level of a yard floor under Rev 6 | Keep the yard floor off under rev6, the owner builds it (2.3.B) |  |  |
| Q226 | - | The hand of a detached unit | The team sets it in 3 Review data, the proposal stays empty with its reason |  |  |
| Q228 | - | Default slopes of road plus slope | Each project states its slopes, the tool ships no default from the past job |  | CHANGED |
| Q229 | - | A row with no single cluster split | The team sets the cluster in 3 Review data, the reason names the row |  |  |
| Q230 | - | Value status of the Rev 6 values | A valueStatus.fieldsRev6 overlay, El @ Entry and ramp type to designBasis.doorLevel, yard fields empty |  |  |
| Q231 | - | Civil door level where no road level | Keep it so, the plot listed in noRoadLevel |  |  |
| Q232 | - | yardGrade in plan.json | Add floors.yardGrade to plan.json under rev6 in the round that shows it in Excel |  |  |
| Q233 | - | Slope limit at 3 or 4 decimals | Keep the raw value at 3 decimals |  |  |
| Q234 | - | Read step road levels under Rev 6 | Leave the read as it is, it reads, the build chooses |  |  |
| Q235 | - | Hand table cover per unit code | Only the codes with a hand, the hand tables of keyRules |  |  |
| Q236 | - | Label size in the PDF check | No, pdfplumber and PdfPig words may differ in size |  |  |
| Q238 | - | TH1-IM in the cluster sequences | Add TH1-IM sequences the manual table 2.1.D allows to clusters as data, no code change |  |  |
| Q239 | - | Mirror of DP2 and DP4 | Take pair left and right of the hand alone table, the plan_rules.hand_table fallback |  |  |
| Q240 | - | The 121 IMC plots of the audit | Record 116, detached plus no proposal, as the plots with no Rev 6 mirror | PAST |  |
| Q241 | A* | doorLevel.source reads as a note in R401 | Rename the key to sourceKind in config, Python and C# in the next Lane A round |  |  |
| Q242 | - | The reason of an unreadable input | Keep the one line, the user opens the file to see why |  |  |
| Q244 | D | A tie in a scan proposal | Propose none on a tie, the user picks in the scanner window |  |  |
| Q245 | D | Old and new code of one row agree | A check row, not a conflict |  |  |
| Q248 | D | Villa blocks on one layer per type | Propose as one source every insert layer whose block names hold a code of R425 |  |  |
| Q249 | D | A sheet whose numbers shift by area | Where location plan fit is read, join by official number, else CAD label and shift | PAST |  |
| Q250 | D | The pair trace on the Road Layout | Trace pairs only of a file with a layer that covers a plot alone |  |  |
| Q251 | D | The centroid of today's plots.json | Take the centroid from the first point in PlotLabels too, in a round rerunning reader parity |  |  |
| Q252 | D | PDF tables and turned text in the scan | R180 pairs only, a PDF table waits for a real one to measure | PAST~ |  |
| Q253 | D | The header row of a scanned sheet | The header is the row above the first row that joins a plot | PAST |  |
| Q255 | - | The override list under rev6 | Same answer, and answer it first: a Rev 06 build applies no past job override, only team edits | PAST | CHANGED |
| Q257 | D | Threads of the scan pair trace | 4, or the cores less one on a PC that runs the scan alone |  |  |
| Q258 | D | The time cap of the scan pair trace | 360 s with the list, and pairs only of a file with a covering layer (Q250) |  |  |
| Q259 | - | Garden door place of each unit | Revit probe reads garden walls and doors in 13 ARC prototypes, door place into rules profile, then R417 | PAST~ REVIT |  |
| Q260 | B | Outline change of a placed floor | In 5, delete the tagged floor and place it again inside the transaction, listing old UniqueId |  |  |
| Q262 | B | Level Excel and the read back values | The Levels sheet reads levels_<run id>.json and shows the level read back beside the plan |  |  |
| Q263 | B | Levels count still at its level | RevitCountsReader counts a plot only while its tagged floors stand within 1 mm of the read back |  |  |
| Q266 | B | Walls on edges other units meet | Measure in Phase 6 whether a villa stands on the edge, leave the span open likewise | PAST |  |
| Q267 | B | Base level of a boundary wall | The level of floorSettings.levels.yard, base offset to the plan base, as coded |  |  |
| Q268 | B | Scope of a wall on two plots | Any plot in the scope, as coded |  |  |
| Q269 | B | Untagged BW wall on a plan wall | Match by both ends, skip and list it, never change it, as coded |  |  |
| Q270 | B | Where a prototype set upgrades now | The set target goes under the work root as prototypes_2024\<discipline> |  |  |
| Q271 | B | Python build takes the pane folder | plan_from_book.py takes a --prototypes folder for parity runs, config folder by default | PAST |  |
| Q273 | B | 6 Levels on a floor moved by hand | 6 lists a floor differing from tag and plan as changed by hand, and leaves it |  |  |
| Q277 | B | Footprint block names unlike the unit | Keep choosing the block by position as R404 does, never by its name |  |  |
| Q284 | C | Villa mode row the boards drop | Keep the row |  |  |
| Q285 | C | Name the Rules row shows | Keep the project field as it is |  |  |
| Q286 | C | Approve the four drafts of NEWPROJ-UI | Approve each as drafted, then copy into baseline. Bader waits for the Revit screenshots | REVIT |  |
| Q287 | C | Button messages that name a work file | A later round rewords those messages by what the file holds |  |  |
| Q288 | C | Scan picks in the settings at version 1 | Keep formatVersion 1, every PC runs the same add-in after the install of Part 1 |  |  |
| Q289 | C | Board rows the scan does not read | Keep the four until a scanner round reads mirror, survey point and road level |  |  |
| Q290 | C | Curbs and road surface left out of scan | Leave them out until round 4j-1b makes the Road Layout scan fast |  |  |
| Q291 | C | Where the line Current files is shown | The pane line now, a row of the Excel report in Part 2 |  |  |
| Q293 | C | Approve the three drafts of SCAN-UI | Approve as drawn, labels in 14-ui-labels.md. Bader waits for the Revit screenshots | REVIT |  |
| Q294 | C | Rules chosen in Set up site, when used | Keep it so, the window says the choice is saved with the setup |  |  |
| Q295 | C | Test host runs skip the scan | Keep it so until the test host can answer the window | REVIT |  |
| Q300 | C | Plots with no cluster proposal | List those plots with their reason, buttons 5 to 8 skip them until the team sets the cluster |  |  |
| Q301 | C | The build takes the door level pick | Lane A round: build takes doorLevelSource over config, adds the civil file to the 4 Check request | REVIT |  |
| Q302 | C | Door level row under the past job | Show the row only when the mode is rev6 | PAST |  |
| Q305 | C | Hide Confirmed under the past job | Show Confirmed and Confirm all proposed only when the plan holds a proposal | PAST |  |
| Q306 | B | A neighbourhood picked after the first read | Button 2 takes the project key as button 1 does, the pick does not change the read |  |  |
| Q323 | B | A keyCopy run refused by its guard | Copy the key under the job's name, guard skips that one file, with a test, next Lane B round | PAST |  |
| Q326 | B | Shared origin at the plot box centre | Keep the centre of the plot box rounded to 1 m | PAST~ | AFFECTED. Facts: this is the CAD position of a new project, a rule on raw input |
| Q327 | B | Elevation of the shared origin from CAD | Bader names a CAD source for the height, such as lowest LandXML road level, until then config value | PAST | AFFECTED. Facts: the height is the one site value a new project cannot get from CAD today |
| Q329 | B | The rule that picks the control points | Keep this rule |  |  |
| Q332 | D | FFL bound but never written | As built, bound and empty | PAST |  |
| Q334 | D | An xlsx read inside Revit | As built, for the parameter sheet alone |  |  |
| Q336 | D | Link parameter group is Text not Data | Text, as the past links hold it | PAST |  |
| Q337 | D | Swap back in the same model unproven | Answer Q323 as it recommends, then run the swap back on the D3L model once | REVIT |  |
| Q343 | - | Passing joint rows in the check chips | Keep every row as asked, count only Fail rows in chips, a later change |  |  |
| Q344 | - | Joints at the edge of a build | Keep the scope, each corner takes road level at its place, list edge joints later |  |  |
| Q345 | C* | The key of a conflict pick | The saved form <plot>/<field>/<second source>, docs and two tests set to it by Lane C |  |  |
| Q346 | C* D* | A villa block pick of one layer | Keep it so, the villa block row names the drawing, not a layer, a Lane C and D change |  |  |
| Q347 | - | A second Update before 4 Check runs again | Keep it, later mark a change already in the override list as saved in the window |  |  |
| Q348 | C* | A conflict pick of the first source | Keep it, window saves only conflicts a person changed, Lane C change, then take both sources |  |  |
| Q349 | - | A conflict pick and the build | Build takes the plot table value where a pick set it, plan logic change with parity |  |  |
| Q351 | - | The limit of the footprint check | 1,200 mm, catches a mass a house width away, passes plots that stand right |  |  |
| Q353 | - | The footprint check on other type plots | Hold them by the plot: base inside its plot, front parallel to plot front, later round |  |  |
| Q354 | - | Yards of a model already built | Keep it, build NBH 012 again from a new model to take the cut |  |  |
| Q355 | A | A Wrong on a plot of two numbers | Check takes a team row by location plan number, each part, one line when no row holds it |  |  |
| Q356 | B* | The off axis warning of a cut yard | Step 5 accepts this warning too and counts it, in Lane B item L2NOV-1 |  |  |
| Q357 | - | Key walls with the face on the line | Keep the centre on the edge, SE keys are structural models, leave these as they are | PAST |  |
| Q358 | - | Walls only the keys hold | Leave structural walls out, garden walls with Q259, read 37 back edges in Phase 6 | PAST |  |
| Q359 | - | A C10 side wall beside a VL6 | Leave them, 12 edges are listed and the wall is short | PAST |  |
| Q361 | D | FFL rule far from keys without overrides | Mark level rules weak, keep ROSHN on the list, compare rev6 on the same keys next | PAST | AFFECTED. Confirms it, RAN 232 of 336 on NBH 012. Mark weak stands, short list 3 |
| Q362 | D | Chosen limits of the zone compare | Keep them | PAST |  |
| Q363 | D | Plot 2302 and two IMD FFL plots | Bader adds 2302 to the override list by hand, the Revit half of 6b opens the IMD plots | PAST REVIT |  |
| Q365 | - | The side of a key wall | Read wall flip and orientation from one key in a Revit read of Phase 6 | PAST REVIT |  |
| Q366 | - | C10 side walls of the SE keys | Keep R480 on the TU measure, read the 19 against architects' intent in Phase 6 | PAST |  |
| Q370 | D | Approve the road surface window draft | Approve it as built, a small picker needs no band |  |  |
| Q372 | D | A road pick in the pane, when it is kept | Keep it as a Browse, one rule for the pane rows | REVIT |  |
| Q374 | D | The stop looks at the zone, not the neighbourhood | Keep the zone as Bader set it |  |  |
| Q375 | - | Ramp type after a rampFrontM gap | Leave R408, decide this class with Q255 and Q230, no line for 1172 alone | PAST | AFFECTED. Goes with Q255 |
| Q379 | A | A new project on another PC | Own round, but no schedule folder row: step 2 must run with no schedule, short list 1, 2, 8 | PAST REVIT | CHANGED |
| Q380 | B | The save dialog and input folder write time | Leave it, no file changed, test on a scratch folder if a D19 record fails | REVIT |  |
| Q381 | B | The crash on Browse For Folder | Bader's three tries, part 20 of the sheet, fix only if it comes back | REVIT |  |
| Q382 | B | Step 4 builds on a site.json off the model | Step 4 compares site.json with the model by the Q158 compare, stops with a plain line | REVIT |  |
| Q383 | B | A tick on a held step reruns steps | Keep as built, rare, steps rerun in seconds up to step 5 |  |  |
| Q385 | B | Accepted warnings return after a save | Keep as built, close Q324 as answered in part, answer Q356 for off axis alone | REVIT |  |
| Q386 | B | Override list changes leave steps Done | Hash these files as inputs of steps 4 to 9, a separate round with Lane A | PAST~ REVIT | AFFECTED. Supports the answer: the list decides most levels |
| Q390 | D | A wall modelled inside out, far face kept | Keep the far face, named in the run line, as Bader's outer face of the walls |  |  |
| Q392 | D | The primary option is not every plot's condition | Keep primary for mass and link, list these plots, neighbour rule is its own round |  |  |
| Q393 | D | The chosen limits of the wall ring | Keep them, run the sidecar check block on the first new prototype |  |  |
| Q394 | A | Update from report on a new project | Under rev6 Apply writes a new entry, once Bader says what model an entry with no key names | PAST REVIT | AFFECTED. Goes with Q255, short list 5 |
| Q395 | A | A mode in memory lost on reopen | Decide with Q372: the settings entity is written whenever the settings change | REVIT |  |
| Q397 | A | Save As of a new project keeps one list | Save setup offers a new copy when the model path no longer fits the folder | REVIT |  |
| Q401 | A | 12 plots differ from the past FFL | Accept, no override, and stop quoting 324 of 336 as proof, the list was on in that count | PAST | CHANGED |
| Q402 | A | Plots 2301 and 2302 read VD4 | Accept VD4, no override, the IMC key reads VD4 too, schedule is older, TH1-IM stays | PAST |  |
| Q405 | C | Export to Excel hidden in Rules | Build the export in Part 2, hidden until then |  |  |
| Q407 | C | Amber in the spec or the boards | The spec holds, the boards are drafts of words and layout, say so in Q286 |  |  |
| Q409 | C | Two greens and the step name weight | One green, SA.Ok, and Regular step names, the spec holds, change with Q286 |  |  |
| Q412 | B | Level 1 is empty after the build | Button 1 or 5 sets the plan view range, or opens a 3D view at run end |  |  |
| Q413 | B | Input picks lost before a real run | A Browse pick writes the setup at once as Save setup does, in its own transaction |  | AFFECTED. The kit's second pass stopped here, the records disagree on what ran |
| Q414 | C | How long a whole project takes | Lane B times one whole project run on a copy, the guide takes the numbers | REVIT | AFFECTED. Whole project has never run through the pane |
| Q415 | C | Pane lines say more than is built | Keep both keys (coordinates.newProjectSource, coordinates.source) as Lane A set them | PAST | AFFECTED. Facts: the dry run took the position file, say so plainly. The answer stands |
| Q416 | C | Road row reads optional, step 2 needs it | The row loses Optional and that line until a build without roads exists | REVIT |  |
| Q417 | C | Whole project stops at step 3 | Step 3 lists all NBH in one table in Part 2, until then pane says Neighbourhood only | REVIT | AFFECTED. Whole project has never run through the pane |
| Q418 | C | Empty project keeps old rules on a set up model | A line in the window says this model keeps its rules, under Empty project | REVIT |  |
| Q419 | C | Ramp slope limit does nothing under the past job | Keep it in the table, the row says Rev 06 only, in Part 2 | PAST REVIT | AFFECTED. Facts: the ramp limit has no effect in the mode that was shown, Rev 06 not run in Revit |
| Q420 | A | Moving a model with placed content | Keep the stop, a move starts from a model with nothing placed | PAST~ |  |
| Q430 | A | Past job keeps its shared corner gaps | Keep the joint off on the past job | PAST | AFFECTED. Past job only, a new project meets 0 |
| Q432 | A | Two plot overlaps left after the pull | Name the 2 at the next run, an override or survey fix by Bader for 2950 | PAST REVIT | AFFECTED. Past job only, a new project meets 0 |
| Q433 | A | 2% units in mixed rows drop to 0.5% | Accept |  |  |
| Q436 | A | Generic floor no longer the whole face | Accept |  |  |
| Q437 | A | One hand entry turns the joint off | Say it in a line at 4 Check until Q255 is answered | PAST~ | AFFECTED. Goes with Q255 |
| Q438 | A | C10 ramp beside a neighbour | Join back corners of ramps whose front corners are joined, the C10 takes 0.5% |  |  |
| Q439 | A | A model built before the pull | Start the model again for any kit now, and build button 5 replacing its own floors, short list 12 |  | CHANGED |
| Q441 | C | Order of the raw input changes | Truth first: short list 7, 5, 11, then 1, 2, then 3, 4, 6 | PAST | CHANGED, opened by the audit |
| Q450 | B | Report defaults of old number, revision, status | Keep these, addin.report in config changes them |  | AFFECTED. Facts: the name codes in the shipped set are the past job's, short list 8 |
| Q451 | B | Report Issues column against its sheets | Keep the pane's count, add column Overrides left, name the sheet's rows Listed for review |  |  |
| Q452 | B | Button 1 on a past model, the 6b line | Change the line to: a second press changes nothing | PAST |  |
| Q453 | B | More overlap warnings from no template | Leave it, no number moves | PAST~ |  |
| Q454 | B | Report name section on the demo config | Refresh that config, then one look | REVIT | AFFECTED. Report section still not seen |
| Q455 | B | A villa on an open space strip | Leave a row of type OS with no block out, listed in Skipped |  |  |
| Q456 | B | Plot list with a plot not in the plan | Stop by name in buttons 5 to 8 |  |  |
| Q470 | D | Plots a mass of its unit does not fit | No Rev 6 evidence in the keys, unit stays by the rule, Bader or the schedule decides | PAST~ |  |
| Q471 | D | Free ends that lose a wall they had room for | Keep one mass per prototype, 300 mm on 10 plots changes no build |  |  |
| Q472 | D | Ramp front overrides stand off the road | Decide with Q255, under Rev 06 these overrides go | PAST | CHANGED |
| Q473 | D | The chosen values of the road toposolid | Keep them and the hull, maxPoints stays, an NBH outline replaces the hull later |  |  |
| Q474 | D | The road toposolid stands as a tall block | Keep Level 1 now, name a level in config once the model holds one near the road |  |  |
| Q475 | D | The road bends between ramp front corners | Keep the straight front, the report lists them |  |  |
| Q476 | D | What a real press shows of the toposolid | The row opens its issue lines, Lane C, for every step |  | AFFECTED. Raises it: the quiet fault of Q439 stays unseen until this is built |
| Q477 | D | A plot list press sends the pane to step 2 | Steps 2 to 4 hold while the plots lie in the NBH they ran on |  |  |
