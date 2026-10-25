# Questions, fixed IDs and the closed list

Every question for Bader has a fixed ID, Q001 upward. An ID is never reused or renumbered. Open questions are listed by ID in Bader's turn of steps/PROGRESS.md and in the top entry of steps/log.md, with the same text in both. A closed question moves here with its answer and the date, and keeps its ID.

Round 17 gave the IDs. Each open question of the DATA-4 list kept its number as its ID, so Q001 is the old 1 and Q043 the old 43. The DATA-4 list had no 14 and no 34, those two were closed in DATA-4 and are below. Round DATA-5 ran beside Round 17, closed 43 and added 44 to 49, which keep those numbers as Q044 to Q049. Round 17 added Q050 to Q061. Round ADDIN adds Q062 upward.

## Closed

| ID | Question | Answer | Closed |
|---|---|---|---|
| Q014 | VL6 hand: is VL6 drawn the other hand from VL4 | Yes. Round DATA-4 read both prototypes in Revit (R245, R246): the VL6 gate and driver's room sit on the right seen from the road, the VL4 gate on the left | 2026-10-25, DATA-4 |
| Q028 | Zone 4: the 1.1 and 1.2 models hold 234 of the 297 plots of 01A, use them as its past key, still send the IMG job if it exists | Replaced by the answer keys (Bader): the IMG model is in C:\02-Site Adaptation\Revit All Zones, and the 7 Rev 6 Parsons site models there (IMA and IMB Zone 1, IMC and IMD Zone 2, IME and IMF Zone 3, IMG Zone 4) are the main reference for every rule. The Native 1.1 to 1.4 reads are a second reference only, for plots none of the 7 hold, and never for unit codes | 2026-10-25, ADDIN |
| Q034 | Rule 3 of the dialog watcher: where may it click | Inside the Revit or Civil 3D process this session started only, matched by process id. Load Once for this tool's DLLs, Do Not Load for any other add-in, and for an information dialog the button that leaves the file as it is. Never a window of another process. Bader restated it for Round 17 | 2026-10-25, DATA-4 and Round 17 |
| Q035 | round_z4\work and round_16\work: delete next round | Done: the files the rounds list as kept moved to Documents\SiteAdaptation\kept\round_z4 (6) and kept\round_16 (4), each copy equal to its source by hash, then both work folders deleted whole | 2026-10-25, ADDIN 0b |
| Q043 | The IFC in Downloads, 1TB02012-012D03-UNA-00-XX-MOD-UT-00010.ifc: is it the Infrastructure IFC package of the schedule notes | No. Round DATA-5 read its header from a copy (R258): it is another project, so it was not copied under C:\02-Site Adaptation | 2026-10-25, DATA-5 |
| Q044 | config/area_codes.json: approve as names only, the area stays the Neighbourhood_ID, but correct its guessed rows | Done by job (Bader): one table parsons and one native (R301). native holds the Round 17 coverage, IMA 1.1 with 02 309 and 01A 84, IMB 1.2 with 03 431 and 01A 150, IMC 1.3 with 021 399 and 013 382, IMD 1.4 with 012 336 and 022 294. parsons holds only the IMC key and schedule (337 in 012) and the IMD schedule (410 in 013), Phase 2 fills the rest from the 7 models. The guessed rows IMA 021, IMB 022, IME 02, IMF 03 and the IMG text row are gone. The area stays the Neighbourhood_ID | 2026-10-25, ADDIN 0b |
| Q045 | R06_0000 and R06_0001 were saved here on 2026-10-22: confirm their source | Villa blocks come from Makkah Building Footprint (Bader, 24 Oct), R06_0000 is reference only. No reader takes R06_0000 or R06_0001 for villa blocks since Round 12b, before it drawings.villaBlocks was R06_0000. drawings.referenceOnly now names both copies and every villa block reader refuses them by their stem (R300) | 2026-10-25, ADDIN 0b |
| Q046 | A probe pass for the unread alignments and parcels | Dropped (Bader): the add-in does not need the 154 alignments | 2026-10-25, ADDIN |
| Q047 | Delete the IFC copy next round | Done: data_book\work\Downloads\1TB02012-012D03-UNA-00-XX-MOD-UT-00010.ifc, 10,497,165 bytes, equal by hash to its source in Downloads\New folder, which stays | 2026-10-25, ADDIN 0b |
| Q048 | Delete the old Cutsheet lock files | Done: Makkah Phase 1 Cutsheet - 500.dwl (61 bytes) and .dwl2 (211 bytes) beside the Cutsheet copy in data_book\work\Base Plans, left by the Civil 3D window of 24 Oct 17:56:55 that lists both as new files (data_book\logs\20261024-175655_cutsheet.run.json). Their user and that no AutoCAD ran at the delete were read before the delete with no saved output | 2026-10-25, ADDIN 0b |
| Q049 | Leave attribute values out of the probe | Leave the 879 attribute values of Road Layout out (Bader). The probe writes none of them: the Road Layout tables hold 0 ATTRIB rows and 0 attr_ columns. Its only attribute filter is DataCommands.cs line 198, which skips an Invisible attribute, and why the 879 fall under it is UNKNOWN. No code changed. The visible attribute values of the Rev 9 drawing stay, see Q062 | 2026-10-25, ADDIN 0b |
| Q050 | Level profile: no profile is the default, which one does every new build use | parsons2024Rev6 for every new build, Rev 6 prevails (Bader). native2022 stays in the repo for reference and is never the default. In the code since ADDIN 0b: levelProfiles.default (R277) | 2026-10-25, ADDIN |
| Q060 | round_17\work keeps the three 2024 key copies and the two new files: delete next round | Done: U11, U12, U13, the two TEST-NEW files, the three site specs and the three site reports moved to Documents\SiteAdaptation\kept\round_17 (11), each copy equal by hash, then round_17\work deleted whole with its 3 parameter files and 3 key settings files, which round 17 did not list as kept | 2026-10-25, ADDIN 0b |
| Q061 | The image of the NBH 03 file ran over the 10 minute image step: raise it to 30 minutes | Done: testHost.imageStepMinutes 30 in config/project.json, the test host takes the limit from the job (R302). The full NBH 03 area still ran over 30 minutes, in I20 and in the one retry I25, so the image was made in four quarters, each image step 52 to 56 s and each window 2.62 to 2.84 min, in round_17\images. In I25 Revit did not respond with 10.7 GB private memory and one Win32 list showed no dialog, why is Q063 | 2026-10-25, ADDIN 0b |
| Q067 | The 13 STR prototypes the SE models of IMA and IMB link are not upgraded, their 2021 files are in Zone 02\STR\Prototypes: leave them, or upgrade them, which needs a change of the upgrade job | Upgraded (Bader, relayed in the brief of Phase 1 part 2): the upgrade job of R109 on the prototype set STR of R313 saved the 13 Revit 2021 files of Zone 02\STR\Prototypes as new centrals into Zone 02\ARC\Prototypes_2024\STR, a folder of their own beside the ARC prototypes, one window at a time under the lock. 13 of 13 are Format 2024 by their bytes and by Revit, their names equal the 13 STR links of the SE models, and the 13 2021 files are equal by hash before and after | 2026-10-25, ADDIN 1b |

## Open questions, full text

The full text of every question open on 2026-10-25 when Round ADDIN Phase 2 part 1 shortened Bader's turn of steps/PROGRESS.md and the top entry of steps/log.md to one short sentence each, up to 7 to a line. Each keeps its ID and its meaning. A question closed later moves to Closed above with its answer.

Q001. El @ Entry rule: the IFC in Downloads is another project, ask the client for the Infrastructure IFC package and the architect for the ramp slope. The road plus the ramp slope holds 0.285 of the IMC key within 5 mm, the 241 others are overrides until then
Q002. Ramp type: coded by Rev 9 code (0.970), 2% for C10, VL4, VL6, 0.5% for the rest. The 10 Flat Ramps of the IMC key slope like the others, keep them as overrides. Q003. Villa mirror: coded, the block mirror flag with the open end (0.913 IMC, 0.945 Native): accept
Q004. Setback: DLUS front. Q005. Front level: road snapped. Q006. Back corners parallel. Q007. Generic floor: whole face (0.955 IMC)
Q008. Units: ask for a baseplan with 01b. Q009. Typology_Rev9 for codes with no schedule. Q010. Sales Land Width. Q011. IME: nothing names it, Phase 2 answers it if the Parsons IME model names its neighbourhood, else ask which neighbourhood each IM letter is
Q012. FFL split: ask why. Q013. CASE second letter as built (CASE follows no tested logic, 0.424). Q015. Yard FFL minus 0.100 (0.997). Q016. Plots 2311, 2312 by baseplan. Q017. Plots 2301, 2302 VD4. Q018. 58 past ramps listed. Q019. Schedule FFL. Q020. Keep the north stop
Q021. Parallel windows over 8 GB free. Q022. TEST copies in Zone 02\ARC: delete when done. Q023. Delete the empty 15a folder. Q024. Curb and ROW from the R94 edge. Q025. 13 PDF refusals listed
Q026. Two curbs from one 2021 drawing, they differ at junction corners and curb breaks: keep the Roads curb, ask which is current
Q027. Two DP4 labels: ask for a split. Q029. Location plan number per face. Q030. Ask for the 01b sales rows. Q031. Rev 9 faces: 97 differ, keep the baseplan, ask which is current
Q032. 1.4 FFL parameter as the design level. Q033. The lone DT3 on a VL3 face: now a unit alone with the hand rule of Q042. Q036. TBD for DLUS, accommodation for sales. Q037. Orientation side coded (909 of 940 corner plots): confirm
Q038. The Native ramp fronts sit 0.698 to 0.700 m over Total Top of Roads Subgrade (-0.70) in all four models, so the 2022 job used the road that surface was cut from: confirm, and keep the IMC schedule as the level key. Q039. Footprint test in every placer check: done as the edge only third check. Q040. Footprint clusters
Q041. Rows 2290 to 2299: the 4 middle DT1 are overrides of the IMC key: accept. Q042. Lone duplex hand: coded, block mirror flag with the open end (15 of 16): accept
Q051. Rev 01 values are not in the repo, only its date for the foundations: send the Rev 01 sections on steps, base heights and ramps, to tell whether native2022 follows Rev 01
Q052. Weak rules, each coded with its overrides: El @ Entry, ramp front and fall, Native height (0.353), Native Generic floor (0.899), foundation type (0.685), CASE: accept until Q001 and Q051 are answered
Q053. The override list holds 5,709 overrides, 5,222 from the Native models, most from their weak level rules: keep all as the majority rule says, a build applies only its source
Q054. R260 is used by DATA-5 outside its range, Round 17 used R261 to R282: keep both as they are. Q055. The 1.4 ELl@Entry parameter is typed 0.41 to 1.59 m off its own ramp on 2461, 2470, 2473, 2481, 2557: read El @ Entry off the ramp there
Q056. The Native models use the 2022 layout with old client units, other than Rev 9 on most plots: use them for hands, fronts and ramps, never for unit codes. Q057. Ramp front fall: 97 IMC overrides list a fall with no direction, not applied: accept
Q058. Old prototype files of 20221206 - Links are not here, their hand comes from the CAD reference (V3 and V2 opposite to VL6 and VD2, the others the same): send the folder to check it on the files
Q059. NBH 03 build: native2022 levels, the 1.2 El @ Entry and front overrides, no mirror or height overrides (old files, other units): accept.
Q062. The probe keeps the visible attribute values of the Rev 9 drawing, 2,139 ATTRIB rows and 16 attr_ columns of room names, finish numbers and levels, and the text survey of R189 reads them. Q049 left out the 879 tags of Road Layout, which the probe never wrote: keep the Rev 9 values, take them out only if you meant every attribute value
Q063. The whole NBH 03 image ran past the 30 minute image step in I20 and I25. In I25 Revit did not respond on 39 of 39 samples at 10,732 MiB (10.48 GiB) private memory and one Win32 list showed no dialog, while a quarter image step takes 52 to 58 s and a quarter rises to 6,377 MiB (6.23 GiB) private memory by the same measure (I28): accept the four quarter images in round_17\images and make large images in quarters, the cause stays a known bug
Q064. The dialog watcher through the job (R303) is withdrawn after two attempts: keep the watcher stopping at the first log step. In I25 the Win32 list found no dialog and Revit was not responding at 10,732 MiB (10.48 GiB) private memory, so the hang points at memory. 5 windows of Phase 1 that ended ok did not respond at times. Q063 keeps its measurements
Q065. The zone2 IMC file of Revit All Zones is the master of the floor scenarios: last saved as _TEST-MASTER, not workshared, links unloaded, 0 floors against 1,010. Its 2024 copy is made from the Zone 02\ARC original, the IMC key: replace the zone2 file by a copy of that original (recommended), or keep it
Q066. No Rev 6 Parsons TU model of Zone 1 is here: IMA and IMB are Engicon SE models with STR prototype links, 432 and 301 plots, no level, ramp or yard floor. Your rule takes a Native model only for plots none of the 7 Parsons models hold, and these plots are in IMA and IMB, so taking their levels, ramps and yards from the Native 1.1 and 1.2 reads is the lead's choice, not your rule: send the Parsons TU models of IMA and IMB (recommended), or accept that Native stand-in until then
Q068. The 7 Parsons models share one project position, survey point, base point, angle 0, Level 1 and Level 2. R211 puts the internal origin at the plot centre of the NBH, 547.5 m away for NBH 012: let button 1 set the Parsons position from config (recommended), or keep R211
Q069. Record of a remade key copy, withdrawn after two attempts since its run proof could be fooled: when a key source changes, let the test host write the SHA-256 of the work copy at the job start and of the made copy at its end into the run log, and let record file a copy only when the work copy hash equals the source hash of the manifest request and the made copy hash on disk equals the logged one (recommended). Until Q069 is answered a remade copy is filed by editing its manifest entry by hand, since import refuses once a manifest exists

## Old numbers to IDs

The steps files of three rounds named questions by the number they had in that round. This table gives the ID for each. A dash means the round had no such question. Closed before IDs means the question was settled before Round 17 and has no ID.

| Topic | Z4 | DATA-3 | 16 | ID |
|---|---|---|---|---|
| El @ Entry rule | 1 | 1 | 1 | Q001 |
| Ramp type | 2 | 2 | 2 | Q002 |
| Villa mirror | 3 | 3 | 3 | Q003 |
| Setback | 4 | 4 | 4 | Q004 |
| Plot front level | 5 | 5 | 5 | Q005 |
| Back corners | 6 | 6 | 6 | Q006 |
| Generic floor | 7 | 7 | 7 | Q007 |
| Round 16 test host run | 8 | 8 | - | closed before IDs, round 16 ran it |
| Units | 9 | 9 | 8 | Q008 |
| Unit codes with no schedule | 10 | 10 | 9 | Q009 |
| Widths | 11 | 11 | 10 | Q010 |
| IME | 12 | 12 | 11 | Q011 |
| FFL split | 13 | 13 | 12 | Q012 |
| CASE | 14 | 14 | 13 | Q013 |
| VL6 hand | 15 | 15 | 14 | Q014, closed |
| Yard depth | 16 | 16 | 15 | Q015 |
| Plots 2311 and 2312 | 17 | 17 | 16 | Q016 |
| Plots 2301 and 2302 | 18 | 18 | 17 | Q017 |
| The 58 past ramps above the surface | 19 | 19 | 18 | Q018 |
| RRE_ZZ_SP_Prototype-FFL | 20 | 20 | 19 | Q019 |
| Angle to true north | 21 | 21 | 20 | Q020 |
| Parallel windows | 22 | 22 | 21 | Q021 |
| TEST copies in Zone 02\ARC | 23 | 23 | 22 | Q022 |
| The empty site-adaptation-tool-15a folder | 24 | 24 | 23 | Q023 |
| Curb and ROW distance | 25 | 25 | 24 | Q024 |
| The 13 plots the PDF check refuses | 26 | 26 | 25 | Q025 |
| The two curbs | 27 | 27 | 26 | Q026 |
| Two DP4 labels in one face | 28 | 28 | 27 | Q027 |
| Zone 4 IMG past job | 29 and 33 | 29 and 33 | 28 | Q028, closed |
| Official numbers | 30 | 30 | 29 | Q029 |
| Plan plots with no sales row | 31 | 31 | 30 | Q030 |
| Location plan versions | 32 | - | - | closed before IDs, DATA-3 found the Rev.09 PDF the latest (R196) |
| Rev 9 plot faces | - | 32 | 31 | Q031 |
| The 1.4 key datum | 34 | 34 | 32 | Q032 |
| The DT3 plot alone on a VL3 face | 38 | 38 | 33 | Q033 |
| Rule 3 of the watcher | 40 | 40 | 34 | Q034, closed |
| round_z4\work and round_16\work | 41 | 41 | 35 | Q035 |
| Rev 9 workbooks | - | 42 | 36 | Q036 |
| Corner front | 37 | 37 | 37 | Q037 |
| Which road the 1.4 job used | 35 | 35 | 38 | Q038 |
| The 8 old prototypes of 20221206 - Links | 36 | 36 | - | closed before IDs, round 16 proved links by the footprint, Round 17 asks again as a new ID if the hands need them |
| Reader fix of Z4 | 39 | 39 | - | closed before IDs, round 16 kept it in PlacementRotation (R220) |
| Footprint test in every placer check | - | - | 39 | Q039 |
| Clusters from the footprint blocks | - | - | 40 | Q040 |
| Rows 2290 to 2293 and 2296 to 2299 | - | - | 41 | Q041 |
| Duplex plots in no cluster | - | - | 42, written 46 in bader-16.md before the merge renumbered the list | Q042 |
| The IFC in Downloads | - | - | - | Q043, from DATA-4, closed in DATA-5 |
