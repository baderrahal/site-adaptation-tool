## 7. Every screen and file of the mock

In the order of docs/addin-mock.md, every sentence of the mock word for word with its punctuation. The two sentences that name a file stand in a text fence, as in the mock, so Markdown keeps their angle brackets as written. The values are those of NBH 012, the IMC area, from the outputs of this round, each with its source. A coordinate shows [coordinate], a level [level]. The fully filled pages are in round_addin\design\mock_filled.md outside the repo.

### 7.1 Ribbon

Ribbon tab "Site Adaptation" with three ribbon panels in this order: Setup (1 Set up site, 2 Read data, 3 Check), Build (4 Place villas, 5 Floors and ramps, 6 Levels, 7 Walls), Output (8 Report). Large buttons, one icon each.

- How: App.OnStartup calls UIControlledApplication.CreateRibbonTab("Site Adaptation"), then CreateRibbonPanel for "Setup", "Build" and "Output" in that order, and one PushButtonData per button with its text as the mock gives it, the full class name of its IExternalCommand, a LargeImage of 32 px and an Image of 16 px, so each is a large button in the normal Revit look (R371 planned). The icons are PNG files of the add-in, one per button, the number of the button on a plain square (CHOSEN, the mock asks one icon each and shows none). An availability class keeps the buttons grey with no project open.
- Values: the labels only.

### 7.2 Dockable pane

Dockable pane "Site Adaptation" on the right, project name at the top right, four sections in this order:

- How: App.OnStartup registers a DockablePaneId with a fixed GUID and an IDockablePaneProvider whose FrameworkElement is a WPF Page and whose InitialState docks it right. The page is a stack of four sections under a top row, each a header TextBlock and a Grid. The project name is a TextBlock aligned right in the top row, Document.Title of the active document, or "not saved" when the title is empty, as for a detached document. On DocumentOpened and on a new document the add-in gives the document a session id, a new GUID held in memory beside the Document, which the requests of its worker runs carry (section 2). The pane, its view model and the check table belong to the active document: on ViewActivated when the document of the view changes, on DocumentOpened and on DocumentClosed the pane loads the settings and the step state of the active document again, every row and every status, and a check table window open on another document closes, so no row and no button click acts on a document that is not the active one, and a worker answer acts only on the document of its request, active or not (section 2). With no document open the pane shows its labels with no values and every button is grey. A real run reaches the model through an ExternalEvent (R372 planned).
- Project name filled: 01101-PSN-IMC-R17B-TEST-NEW, the last new IMC site file this tool made (READ, kept\round_17).

Project: Settings (config file), Rules (rules file), Level profile

| Label | Control | Filled | Source |
|---|---|---|---|
| Settings | TextBlock with the file name and a Browse button (Microsoft.Win32.OpenFileDialog) | project.json | config/project.json |
| Rules | TextBlock and Browse | roshn-makkah.json | profile of config/project.json |
| Level profile | ComboBox of the names of levelProfiles of the rules file | parsons2024Rev6 | levelProfiles.default, Q050 |

Input files: Baseplan, Villa blocks, Curbs, Road surface, DLUS and sales, Plot numbers, Prototypes. Each shows the chosen file or folder name.

| Label | Control | Filled | Source |
|---|---|---|---|
| Baseplan | TextBlock and Browse | ROSHN Makkah Baseplan.dwg | drawings.baseplan |
| Villa blocks | TextBlock and Browse | Makkah Building Footprint.dwg | drawings.villaBlocks, R300 |
| Curbs | TextBlock and Browse | Makkah Phase 1_Road Layout.dwg | drawings.roadLayout, analysis.roadEdgeLayers RD-CURB-LINE |
| Road surface | TextBlock and a folder Browse | landxml, 6 surfaces of Makkah Phase 1_Road Surface C3D | siteBuild.roadSurfaces, the landxml folder of the snapshot |
| DLUS and sales | TextBlock with two names and Browse | TBD-PSN-ZZZ-XXX-SCH-TM-000201- DLUS Schedule_Rev9.0.xlsx, PSN-ZZZ-XXX-SCH-TM-000201- DLUS Schedule_(Rev9)_Accomodation_Schedule.xlsx | dataBook.data5.areaKey and dataBook.locationPlan.sales of python/c3d_probe.json |
| Plot numbers | TextBlock and Browse | 01101-PSN-ZZZ-XXX-DWG-TU-005000.pdf, then in grey 2.91 MiB 7061ac0d | dataBook.locationPlan.pdf, the file of Master Plan Rev.09, 3,056,293 bytes |
| Prototypes | TextBlock and a folder Browse | Prototypes_2024 | pastJob.upgradedPrototypeFolder |

How each input row tells two files of one name apart: the TextBlock shows the file or folder name as the mock asks, then in a smaller grey run its size and the first 8 hex digits of its SHA-256, and its ToolTip gives the full path with the folder. So the two different files named 01101-PSN-ZZZ-XXX-DWG-TU-005000.pdf on this machine, 3,056,293 bytes (7061ac0d) and 2,395,117 bytes (24ffcbac), read apart in the pane (READ, 3.2). The label of the row stays the mock's.

Scope: radio Whole project, Neighbourhood [code], Plot list. Checkbox "Dry run first, change nothing", on by default.

| Label | Control | Filled | Source |
|---|---|---|---|
| Whole project | RadioButton | not chosen | |
| Neighbourhood | RadioButton and a ComboBox of the NBH codes of the plot table | 012 chosen | area key of R256, the NBH of the IMC key (R326) |
| Plot list | RadioButton and a TextBox of official numbers and ranges, such as 2290-2299, 2311 (section 3, Scope) | not chosen | |
| Dry run first, change nothing | CheckBox | ticked | the default of the mock |

Steps: one line per button 1 to 8 with a status: Done, Done with [n] issues, Ready, or Needs step [n].

| Line | Filled, after buttons 1 to 3 on NBH 012 | Source |
|---|---|---|
| 1 Set up site | Done | 5 of 5 check points at 0.0 mm on the IMC new site file of Round 17 (READ, site_report_i18.json) |
| 2 Read data | Done | the reads of the data book, section 4 |
| 3 Check | Done with 1,247 issues | the rows of the check table, 7.4 |
| 4 Place villas | Ready | step 3 done |
| 5 Floors and ramps | Needs step 4 | |
| 6 Levels | Needs step 5 | |
| 7 Walls | Needs step 6 | |
| 8 Report | Ready | step 3 done |

How: an ItemsControl of eight rows, the text of each from the step state of Common (R366 planned), refreshed after every run and every settings change.

### 7.3 Set up site dialog

Set up site dialog. Title "1 Set up site" and a badge "Empty project found" or "Existing site file".

- How: a modal WPF Window, its owner the Revit main window by WindowInteropHelper, title "1 Set up site". The badge is a Border with a TextBlock, its text by the test of 3.1: Existing site file when the document holds the Settings entity of SiteAdaptation_Settings, a shared parameter bound with the GUID or the name of one of config, a project position other than zero or a survey point or project base point off the internal origin by more than 0.5 mm, else Empty project found, never by its types, which the default metric template holds too. A line under the badge names the signs found (R373, R374 planned). The left is a Grid of two parts, the right a DataGrid, the buttons a right aligned StackPanel. The dialog is filled by a dry run of button 1 in Common, the Revit side only reads (SiteTarget).

Left: "What the project has now" (Survey point, Project base point, Levels, Floor types and wall types, Shared parameters, Units) each with its state, then "What the tool will set" (Coordinate system, Survey point, Angle to true north, Levels, Floor types, Wall types, Shared parameters, Units) with the values it will set.

The states of What the project has now:

| Row | Empty project found, from the default metric template | Existing site file, the IMD 2024 copy | Source |
|---|---|---|---|
| Survey point | at the internal origin of the template | [coordinate] | Revit BasePoint.GetSurveyPoint, the RD read |
| Project base point | at the internal origin of the template | [coordinate] | BasePoint.GetProjectBasePoint, the RD read |
| Levels | Level 1, Level 2 of the template | Level 1 [level], Level 2 [level] | site_report_i18.json, levels kept, the RD read |
| Floor types and wall types | Generic 150mm, no yard, no ramp, the wall types of the template UNKNOWN until Phase 5b reads them | Flat Ramps, Generic 150mm, Ramps 0.5%, Ramps 2%, yard, BW and 4 garden wall types | site_report_i18.json, the RD read and key_reads.json |
| Shared parameters | none | the 9 RRE parameters and CASE on its links | site_report_i18.json made 10, the RD read |
| Units | metric, the length unit of the template is UNKNOWN until Phase 5b reads it | meters, accuracy 0.01 | the RD read, R311 |

The values of What the tool will set:

| Row | Value it will set | Source |
|---|---|---|
| Coordinate system | shared coordinates of CAD, site Internal, the Parsons project position [coordinate] and the project base point [coordinate], which has no row of its own in the mock and is shown here | keyRules.site, the position file (Q068) |
| Survey point | [coordinate] | the position file, equal on the 7 keys (Phase 1) |
| Angle to true north | 0 | keyRules.site.angleToTrueNorthDeg |
| Levels | Level 1 [level], Level 2 [level], 4.0 m over Level 1 | keyRules.site.levelStepsM, the position file |
| Floor types | Generic 150mm, yard, Ramps 0.5%, Ramps 2%, Flat Ramps | floors.types of config |
| Wall types | BW, Generic Wall-150mm_ Garden | the IMC key read, config block addin (Phase 4a) |
| Shared parameters | Plot on floors and walls, the 9 RRE parameters and CASE on links | floors.plotParameter, pastJob.parameters, RRE_ZZ_SP_Prototype-FFL from the key reads |
| Units | meters, accuracy 0.01 | keyRules.site.units, lengthAccuracy of the TU keys |

Right: table "Check points from CAD" with columns Point, CAD, Revit, Diff, and a line "Pass limit 1 mm. [n] of [n] pass".

| Point | CAD | Revit | Diff |
|---|---|---|---|
| control 1 | [coordinate] | [coordinate] | 0.00 mm |
| control 2 | [coordinate] | [coordinate] | 0.00 mm |
| control 3 | [coordinate] | [coordinate] | 0.00 mm |
| plots min | [coordinate] | [coordinate] | 0.00 mm |
| plots max | [coordinate] | [coordinate] | 0.00 mm |

Pass limit 1 mm. 5 of 5 pass.

- Source: the five check points of the newSite job of the IMC new site file of Round 17 from the default metric template, each 0.0 mm, and its survey point 0.0 mm (READ, site_report_i18.json). That run placed the position of R211 and took plots min and plots max of the IMC plots of its round. Button 1 takes the extreme plots of the whole baseplan (section 3, Scope), and under Q068 it adds the survey point and the project base point as two more rows, held the same way, so Phase 5b fills these rows again. For the existing IMD copy the survey point and the project base point are 0.0 mm from the IMC key in shared and internal coordinates (RAN Phase 1, p1_compare.json).
- How: a DataGrid with four text columns, CAD and Revit as east, north and elevation in metres, Revit as 3.1 Checks gives it, the value Apply would give in a dry run and the value Revit reports after Apply, Diff in mm with two decimals rounded by R385, so a gap of 1.04 mm reads 1.04 and not 1.0. A point passes when the Diff shown is at most the pass limit, so what the table shows is what is judged. The line is a TextBlock under it.

Below it: "On an existing site file" with a line saying nothing changes until Apply.

- Filled: "On an existing site file", then "Nothing changes until Apply. Apply sets only the rows that differ.", the differing rows marked in What the tool will set, with no tick per row. Source: the round prompt.
- How: a TextBlock header and a TextBlock line, shown on both badges.

Buttons Cancel, Dry run, Apply.

- How: Cancel closes with no change. Dry run runs the dry run of button 1 and fills both tables. Apply runs the real run in one transaction and closes when it commits, else shows the reason and stays open (R382 planned).

### 7.4 Check table

Check table. Title "3 Check, NBH [code], [n] plots", a line with the level profile and the override file.

- How: a modeless WPF Window owned by the Revit main window, so the user can work in Revit while it is open, built from `checks_<NBH>.json` and bound to the document it was opened on, which closes it when another document becomes active (7.2). The title is a TextBlock, the line under it a TextBlock (R376 planned). For Neighbourhood [code] is the code of the NBH. For Whole project the title reads "3 Check, NBH all, [n] plots", [code] showing all and [n] every unit plot of the scope in one table, the NBH column telling the rows apart and the filter chips counting over all of them. For a Plot list [code] is the codes of the NBH the list touches, joined by commas.
- Filled: "3 Check, NBH 012, 337 plots", then "Level profile parsons2024Rev6, override file roshn-makkah.json, 12,421 overrides, 1,227 of them on NBH 012". Source: the 337 client code labels of NBH 012 (RAN, labels.csv), the list after PR 52 (READ, config/overrides/roshn-makkah.json, 12,421 entries, 1,227 of key imc). The counts of the summary table and the chips below were taken on the 3c list of 12,424 with 1,225 of key imc and are not counted again.

Buttons Zoom to plot, Export to Excel, Save overrides.

- How: three Buttons. Zoom to plot raises an ExternalEvent that calls UIDocument.ShowElements on the tagged elements of the plot, else zooms the active view to the face box by UIView.ZoomAndCenterRectangle. Export to Excel asks the worker to write the rows shown to `Check_<NBH>.xlsx`. Save overrides writes the rows whose Use the user changed in this session into the override list as 3.3 says, each marked byHand and no other row, and rebuilds the plan.

Filter chips: All, then one per check type (Plot cut, Floor, Hand, Unit code, Position, Level, and any other type found), each with its count, and "[n] plots follow every rule" on the right.

| Chip | Count | Source of the rows |
|---|---|---|
| All | 1,247 | the sum |
| Plot cut | 12 | the cut plots of the IMC join, 2311, 2312, 2375 to 2378, 2387 to 2390, 2544, 2545 (R326) |
| Floor | 38 | 32 overrides (genericFloor 4, yardOutline 14, rampOutline 4, rampType 10), 5 plots with no fitted yard outline (2378, 2387, 2441, 2544, 2545), 2441 with no generic floor in the key |
| Hand | 12 | 11 mirrored overrides, and IMC 2302 decided by Q017 |
| Unit code | 3 | 2301 and 2302, VD4 by Rev 9 while the key links the DP4 file (Q017), 2312 with no unit, it shares the face of 2311 (Q016) |
| Position | 4 | 4 insert overrides |
| Level | 817 | entryM 304, heightM 300, rampFrontM 113, rampFrontFallM 90, steps 5, Prototype-FFL 5 |
| Parameter | 361 | CASE 206, Prototype-Foundation-Type 150, RVT-Name 5 |

"2 plots follow every rule" on the right: 2616 and 2624, the plots of the 337 with no row.

- Source: the plan of NBH 012 by the parsons rules (RAN, round_addin\design\work\plan_012.json), the override list, decidedByQuestion of majority.json of the 3c review and key_join.json, counted by round_addin\design\work\mock_values.py (RAN).
- How: a WrapPanel of ToggleButtons, each with its type and count, All first, then the types in the order of the mock, then any other type found. The count line is a TextBlock aligned right.

Columns: Plot, NBH, Unit, Check, Tool value (rule), Past job, Rule, Use. Use is a dropdown Rule, Past, Skip. Past writes the past value to the override list with source parsons. Skip leaves the plot out and lists it in the report. Selecting a row zooms the Revit view to that plot.

- How: a DataGrid, Plot, NBH, Unit, Check, Tool value (rule), Past job and Rule as read only text columns, Use a DataGridComboBoxColumn with Rule, Past and Skip, each row opening on the Use of 3.3. Past writes in the form of the table of 3.3, a gap to the rule for a level or a position and never the level or the coordinate, and is grey on a row whose field no build reads, on every row of an existing site file that is no answer key and on every row of a plot no answer key holds. Skip writes the plot to the skip list, which the report lists. A selected row raises the same ExternalEvent as Zoom to plot.
- Filled, real IMC rows (the levels as [level], the past value of a level row is the rule value plus the gap of its entry, the gap a distance):

| Plot | NBH | Unit | Check | Tool value (rule) | Past job | Rule | Use |
|---|---|---|---|---|---|---|---|
| 2311 | 012 | DP2 | Plot cut | built on the whole baseplan face | the key generic floor holds under 0.9 of one face | R120, R326 | Rule |
| 2441 | 012 | VD2 | Floor | the Generic floor on the whole face | the key holds no generic floor on the plot | R208, R326 | Rule |
| 2301 | 012 | VD4 | Floor | fitted yard outline | the key yard 0.037 m off it | yardOutline, fittedOutlineRefit, a build places the fitted outline | Rule |
| 2291 | 012 | DT1 | Hand | not mirrored | mirrored | hand cluster, clusterSide | Past |
| 2293 | 012 | VD1 | Hand | not mirrored | mirrored | hand villa, blockMirrorRoadSide | Past |
| 2301 | 012 | VD4 | Unit code | RRE-WP-NSP-KEO-ARC-VD4-ZZ-MOD-2400.rvt | RRE-WP-NSP-KEO-ARC-DP4-ZZ-MOD-2400.rvt | Typology_Rev9, Q017 | Rule |
| 2338 | 012 | DP4 | Position | the origin of the plotFrame refit [coordinate] | [coordinate], 0.011 m back along the link X axis and 0.001 m along Y, into the plot | linkOrigin, plotFrameRefit | Past |
| 2290 | 012 | VD1 | Level | El @ Entry [level] | [level], 0.0077 m lower | entry, roadLevel | Past |
| 2289 | 012 | VD4 | Level | FFL [level] | [level], 0.001 m higher | ffl, oneFflPerPieceAtTheHighestEntry | Past |
| 2291 | 012 | DT1 | Parameter | CASE D | F | parameter CASE, typologyMirrored | Past |

### 7.5 Level Excel

```text
Level Excel, file <NBH>_Levels_<profile>.xlsx.
```

- How: the worker writes it with DocumentFormat.OpenXml inside the transaction of button 6 to its temporary name, from the plan and the values the add-in read back, and the add-in moves it onto its name after the commit (3.6), one workbook per NBH of the scope (R364 planned). Filled: 012_Levels_parsons2024Rev6.xlsx.

A top line with NBH, Zone, road surface and the formulas used.

- Filled: "NBH 012, Zone 2, road surface Top Surface-02 on 672 of 672 front corners, formulas: El @ Entry = the road at the two front corners + the slope of the unit x the ramp depth (R278, R339), FFL = the highest El @ Entry of the piece + the base of its unit, to the mm (R340), SSL = FFL - 0.075 m (levels.ground_ssl), Height = FFL - El @ Entry, Steps = one for the base and one per riser over it, half a riser no step (R340)". Source: the summary of plan_012.json (RAN) and the rules profile (READ).
- How: row 1, one merged cell, bold.

Columns in this order: Plot No, Unit, Cluster, Road at front, El @ Entry, FFL, SSL, Height, Steps, Source (Rule, or Override with the reason).

| Plot No | Unit | Cluster | Road at front | El @ Entry | FFL | SSL | Height | Steps | Source |
|---|---|---|---|---|---|---|---|---|---|
| 2289 | VD4 | none | [level] | [level] | [level] | [level] | 0.2006 | 1 | Override heightM (breaks oneFflPerPieceAtTheHighestEntry), rampFrontM (breaks roadLevel) |
| 2290 | VD1 | 2290-2291 | [level] | [level] | [level] | [level] | 0.1000 | 1 | Override entryM (breaks roadLevel), heightM (breaks oneFflPerPieceAtTheHighestEntry) |
| 2292 | DT1 | 2292-2293 | [level] | [level] | [level] | [level] | 0.1003 | 1 | Override heightM (breaks oneFflPerPieceAtTheHighestEntry) |
| 2294 | DT3 | 2294-2295 | [level] | [level] | [level] | [level] | 0.1027 | 1 | Override entryM (breaks roadLevel) |
| 2295 | DT3 | 2294-2295 | [level] | [level] | [level] | [level] | 0.1003 | 1 | Rule |

- Source: plan_012.json levels and levelRule per plot, the cluster by clusterPlots of majority.json (the site clusters a build takes, R271), the reason by the override list (RAN, mock_values.py). Of the 336 rows 24 show Source Rule and 312 an override of El @ Entry, the FFL, the steps or the ramp front, since El @ Entry holds by the rule alone on 157 of 1,753 key plots (Q001).
- Road at front is the front level the rule takes: the mean of the road levels at the two front corners, as entry_of of R278 and R339 takes it before the offset and the slope, from the unrounded corner levels of the road snap. It is never taken from levelRule.rampFrontCorners, which the plan writes at 3 decimals per corner while entry_of takes the unrounded mean (READ, plan_from_book.rule_levels and plan_rules.entry_of l.85 to 88), so the two could part by up to 0.5 mm. The build writes that unrounded mean into the levels of the plot as roadAtFrontM, and Road at front and El @ Entry are each rounded once from the same unrounded values by R385, at 4 decimals (R359). The two corner levels stay in plan_<NBH>.json, so the ten columns and the four sheets stay those of the mock.
- How: a header row and one row per written plot, Road at front, El @ Entry and Height with 4 decimals as R2 keeps them, FFL and SSL with 3, a plot not written listed at the end with its reason.

Sheets: Levels, Clusters, Overrides used, Settings.

- Levels: the table above. Clusters: one row per piece, its plots, unit sequence, split (levelSplit.bySequence) and FFL [level]. Overrides used: plot, field, gap, reason, source parsons, model of the key, from the list. Settings: the run id of the run of button 6 that wrote it, the profile, the SHA-256 of the rules file and the override list, the road surface order, the pass limit, the run time.

### 7.6 Report Excel

```text
Report Excel, file <NBH>_Report.xlsx.
```

- How: the worker writes it with DocumentFormat.OpenXml from the run logs of buttons 1 to 7 and the counts the add-in read from the model at report time (R365 planned). Every number of one report comes from one build, the plan of the last real run of button 3, and one read of the model. Filled: 012_Report.xlsx, every number of this page from the one build plan_012.json (RAN).

A top line with file name, NBH, plot count, profile, run time.

- Filled: "[file name].rvt, NBH 012, 337 plots, parsons2024Rev6, run time [n] min". Source: the plot count and the profile of plan_012.json (RAN). The build is not placed yet, so the file name and the run time stay open until Phase 6 places it. The Round 17 IMC rebuild placed another build into 01101-PSN-IMC-R17B-TEST-NEW.rvt in 7.96 min over 13 place runs (READ, place_codes_b.txt) and is not shown on this page.

Summary table with columns Step, Done, Skipped, Override, Issues, one row per button 1 to 7.

| Step | Done | Skipped | Override | Issues | Source |
|---|---|---|---|---|---|
| 1 Set up site | 17 | 0 | 0 | 0 | what 3.1 sets on the default metric template: 4 floor types, 2 wall types and 11 parameters made, Level 1, Level 2 and Generic 150mm kept, the check points pass (CHOSEN by this design, UNKNOWN by a run until 5b, the Round 17 newSite run made the 14 of R212, 4 floor types and 10 parameters, READ site_report_i18.json) |
| 2 Read data | 337 | 43 | 0 | 0 | the snapshot plots.csv, 380 faces of NBH 012, 337 unit plots, 43 faces with no unit |
| 3 Check | 337 | 0 | 1,225 | 1,247 | 337 plots checked, the 1,225 overrides of the list and the 1,247 rows of 7.4 |
| 4 Place villas | 336 | 1 | 371 | 0 | plan_012.json: 336 links, 2312 not written, overrides applied mirrored 11, insert 4, CASE 206, Prototype-Foundation-Type 150 |
| 5 Floors and ramps | 1,003 | 8 | 123 | 112 | plan_012.json: generic 336, yard 331, ramp 336, the 3 floors of 2312 and 5 yards not written, rampType 10 and rampFrontM 113 applied, genericFloor 4, rampOutline 4, yardOutline 14 and rampFrontFallM 90 left (Q057) |
| 6 Levels | 336 | 1 | 619 | 0 | plan_012.json: entryM 304, heightM 300, steps 5, Prototype-FFL 5 and RVT-Name 5 applied, 2312 with no level |
| 7 Walls | 0 | 0 | 0 | 1 | no wall placed yet, Phase 4g codes the walls (R362), the wall top rule is weak and no garden wall rule is measured (Q071), keyRules wallTop 0.346 |

- How: a table of the five columns of the mock from row 3, one row per button 1 to 7. The Source column above is the note of this design on where each count comes from and is no column of the file. The counts of steps 4 to 6 are those of the plan of NBH 012 a real run would place, read from plan_012.json (RAN). No Parsons build has been placed in Revit yet, Phase 6 runs it.

A box "Counts agree across outputs" with columns Item, Revit, Excel for Links, Floors, Levels.

| Item | Revit | Excel |
|---|---|---|
| Links | not placed yet | 336 |
| Floors | not placed yet | 1,003 |
| Levels | not placed yet | 336 |

- Source: the one build of this page, plan_012.json (RAN). Excel is what the files of that build hold: the rows of the Placed sheet of kind link, 336, and of the floor kinds, generic 336, yard 331 and ramp 336, 1,003, the rows of the Levels sheet of the level Excel, 336, the same numbers as the summary above. Revit is read from the model at report time by the tags, Levels by the elements the last run of button 6 that committed set (3.8), and no Parsons build is placed yet, so it stays open until Phase 6 places NBH 012. The plan count is held against both apart and a mismatch of any two is an issue of the report. The Round 17 IMC rebuild placed another build, 336 links and 999 floors (RAN, prototypes_imc_r17b.json against plan_imc_r17b.json), and is not shown here.
- How: a bordered range of three columns under the summary.

A box "Rules applied".

- Filled: one line per decision of keyRules the build applied with its coded rule, share and strong or weak: linkRotation frontRule 0.991 strong, frontCorner frontRule 0.978 strong (applied: frontCorner measures the link rotation of R226 on the corner plots, and the build gives the 110 plots of NBH 012 with two or more road edges the edge the Orientation of the sales sheet names and the other 227 the nearest curb, RAN summary.frontRule of plan_012.json), linkOrigin plotFrameRefit 0.971 strong, hand villa blockMirrorRoadSide 0.960 strong, hand cluster clusterSide 0.981 strong, hand alone rowPairSideOrBlockMirrorOpenEnd 1.0 strong, rampSlope typology 0.988 strong, rampOutline outlineR127 0.969 strong, generic all 0.991 strong, yard zone 1.0 strong, yardOutline fittedOutlineRefit 0.963 strong, entry roadLevel 0.088 weak, rampFront roadLevel 0.692 weak, levelSplit units 0.702 weak, baseHeight profile 0.844 weak, ffl oneFflPerPieceAtTheHighestEntry 0.749 weak, steps stepsFromHeight 0.988 strong, the parameters (CASE typologyMirrored 0.275 weak, Prototype-Foundation-Type 0.620 weak, Prototype-FFL fflDownToTheCm 0.994 strong, the rest 0.992 to 1.0 strong), with the hand rules of NBH 012: clusterSide 202, blockMirrorRoadSide 108, row pair or block mirror 15, override 11. Source: keyRules of rules/roshn-makkah.json (READ) and the summary of plan_012.json (RAN).
- How: a bordered range, one row per decision with its rule id.

Sheets: Summary, Placed, Skipped, Overrides, Issues, Rules.

- Summary: the top line and the three boxes. Placed: one row per placed element, plot, kind, type, the element id. Skipped: plot, kind, reason. Overrides: the overrides applied and left, by plot and field. Issues: every issue of every button. Rules: every rule id applied with its text from docs/rules.md.

### 7.7 What the add-in shows that the mock does not

The mock wins. Each item below is a screen or a part the mock does not show, kept because a rule of section 3 needs it, and Phase 6 step 6 checks the screenshots against the mock and this list, so each difference is named and none is found by chance:

1. The summary of a run before any write, with Cancel and Apply, for buttons 2 to 8: after the dry run when the box is ticked, and as the confirm when it is not (section 3). Button 1 has its own Dry run and Apply in the mock.
2. The TaskDialog of button 4 before its transaction when a link type must be loaded again, or reloaded since its file changed, with Load and Cancel and the line that Undo is cleared (3.4).
3. Leave or Set to the plan per element changed by hand, in the summary of a second run (section 3).
4. Past grey in the Use dropdown, with its reason as a tooltip, on a row whose field no build reads, on every row of an existing site file that is no answer key, and on every row of a plot no answer key holds, which has no key or model to write (3.3).
5. The size and the short SHA-256 in grey beside each input name, and the full path as a tooltip (7.2).
6. The line under the badge that names the signs found (7.3), and the marks on the rows of What the tool will set that differ from What the project has now, with no tick per row.
7. The tooltip of the Settings row with the override list, the work folder and the fit schedule of the location plan, which the config file holds since the mock gives them no row (section 6).
8. The filter chip "Assumed" of the check table (7.4), after the chips of the check types and before "[n] plots follow every rule", with its count, one row per plot and field whose value has an assumed status in the plan: RECOMMENDED, CHOSEN by Code, UNKNOWN or a weak MEASURED rule (Q102, R363). CHOSEN by Code in 4g1 PR 1, for R363 to settle: its rows count in All as the rows of every chip do, and a plot with an assumed value and no row of another chip still counts in "[n] plots follow every rule".
9. The sheet "Assumed" of the report Excel (7.6), one row per plot and field of an assumed status, and on each line of the box Rules applied the count of plots that took its value (Q102, R365). CHOSEN by Code in 4g1 PR 1, for R365 to settle: the sheet after Overrides, its columns plot, field, value, status and the Q ID, share or source that gives the status, and the count written "[n] plots". Both read the status of the plan, nothing new is measured.

