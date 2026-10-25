# Round log

Newest entry first. The top entry is one page at most. Older rounds keep a short stub here and the detail lives in steps/history/.

## Round ADDIN Part 1, 2026-10-25, the Revit 2024 add-in from the Parsons site models

In progress. Phases 0a and 0b are done, the phases after them follow the plan in steps/investigations/round-addin.md.

Goal:
- The whole Site Adaptation add-in inside Revit 2024 in C#, .NET Framework 4.8 (option B), built from the 7 Rev 6 Parsons site models, parsons2024Rev6 for every new build (Q050), the screens and files of the approved mock in docs/addin-mock.md. Part 2, the sheets, waits for Bader's check of Part 1

Done:
- 0a (PR 41): the AI MAX skill version 3, seven agents, six old log entries moved to steps/history/, CLAUDE.md with the Round ADDIN section, docs/addin-mock.md. Q028, Q046 and Q050 closed
- 0b (PR 42, fix/addin-open-fixes): R277 levelProfiles.default parsons2024Rev6, and R280 applies an override only on a build whose profile has the job of its source (Q050). R300 villa blocks only from drawings.villaBlocks: no reader takes R06_0000 or R06_0001 since Round 12b, before it villaBlocks was R06_0000, and every villa block reader now refuses them by stem with its caller's settings (Q045). R301 config/area_codes.json by job, zone and neighbourhood checked together, the guessed rows gone (Q044). R302 the image step 30 minutes and a margin of 10 from testHost (Q061). R303, the watcher through the job, withdrawn after two attempts, each review found new faults in its start phase worker, the watcher is back to main and stops at the first log step (Q064). R304 a clean clone in a deep folder builds, main failed there too. Reviews 2 and 3: R300 matches the reference stem as a whole token, R301 verdicts by the zone and neighbourhood pairs first with keyed and unkeyed counts and mostlyUnkeyed, names and counts of their form, R280 refuses an override list or a source alone and an empty list name, R12 needs no test for a withdrawn rule. Q049 needed no code
- 0b outside the repo: the NBH 03 image in four quarters, the whole area timed out in I20 and in the one retry I25. The IFC copy (10,497,165 bytes) and the two Cutsheet lock files (61 and 211) deleted. 21 kept files, 285,195,525 bytes, in Documents\SiteAdaptation\kept, the three work folders deleted. The IFC input retired by Q047, the two road layout reads made again, data5_check.py runs to its end. feat/revit-links-walls-ramps deleted on origin and here. Q035, Q044, Q045, Q047, Q048, Q049, Q060, Q061 closed
- Gate: Python 1097 passed and 1 skipped, .NET 433 passed, the build of a clean clone in the scratchpad 0 errors

Checked:
- 0a: the skill copy 39 of 39 files equal by SHA-256, the 6 moved entries word for word, the mock equal to the round prompt byte for byte
- 0b: 40 of 40 originals equal by hash before, after the first windows and after the review windows. 21 of 21 kept copies equal by round_files.sha256 and by Get-FileHash. 8 of 8 native area rows equal the coverage text of round-17.md. data5_check.py at seeds 14 and 7: area 20 and 19 of 20 plus 1 noDlusRow, parcels 20 of 20, curb 20 of 20, IFC skipped by Q047. The old branch: squash merged as 0a0d4c3 with the same tree, 12 of 12 files equal main or changed later on main. 8 Revit windows one at a time under the lock, 5.46 to 8.92 GB free at each lock, every model copy unchanged by hash. Pass 2: I28 ran the R303 watcher, now withdrawn, on a quarter. Pass 3: I29, a quarter under the lock with 6.95 GB free, the watcher of main answered 5 security dialogs in the start phase and stopped at the first log step, result ok, the copy unchanged and deleted, 40 of 40 originals equal before and after. A clean worktree of main and of 83583e3 failed with MSB3030 at a path of 271 characters and built at a short path, with R304 a clean worktree of acdca67 built and passed 433 tests. data5_check.py exit 0 in 4.4 s with the counts of pass 1. This Your turn equals Bader's turn of steps/PROGRESS.md

Remains:
- Phases 1 to 6 of the plan. siteBuild still names round_17, Phase 1 sets it again

Known bugs:
- The whole NBH 03 area runs past the 30 minute image step: in I25 Revit did not respond with 10.7 GB private memory, 2.16 to 2.77 GB free, and showed no dialog in one Win32 list, a quarter rises to 6.4 GB private memory by the same measure (I28) (Q063). I28 took free memory to 1.10 GB and I29 to 0.55 GB, the 2 GB floor of R200 is checked at the lock only
- Run-TestHost.ps1 read a 1,884 MB peak for I25 and 1,982 MB for I28 against a 4.9 and a 5.7 GB working set sampled by Get-Process, and 1,907 MB for I29, why is UNKNOWN
- CLAUDE.md still names R303, the dialog watcher through the whole job, in the Round ADDIN line and the rule summary. It was not edited in this PR
- The .NET build gives 1 warning older than this round, xUnit2013 at dotnet/SiteAdaptation.Tests/CadExplodeTests.cs line 206

Next:
- Phase 1: the 7 Parsons models hashed, BasicFileInfo, Revit 2024 copies, their links and coordinates, the limits measured

Your turn:
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
Q063. The whole NBH 03 image ran past the 30 minute image step in I20 and I25. In I25 Revit did not respond on 39 of 39 samples with 10.7 GB private memory and one Win32 list showed no dialog, while a quarter image step takes 52 to 58 s and a quarter rises to 6.4 GB private memory by the same measure (I28): accept the four quarter images in round_17\images and make large images in quarters, the cause stays a known bug
Q064. The dialog watcher through the job (R303) is withdrawn after two attempts: keep the watcher stopping at the first log step. In I25 the Win32 list found no dialog and Revit was not responding at 10.7 GB private memory, so the hang points at memory, which Phase 1 measures. Q063 keeps its measurements

## Round 17, 2026-10-25, the past models of all four sub-phases, the majority rule and the IMC rebuild

Goal:
- Fixed question IDs, placer_check split, the large El @ Entry gaps, the four Native models read, every decision by the majority rule with an override list, IMC rebuilt with it. Summary: steps/investigations/round-17.md, steps for Bader: steps/bader-17.md

Done:
- Session started in the repo, hooks live. Worktree site-adaptation-tool-17 on test/round-17 from main at 3a5dd48, rebased on dfb6a0c after DATA-5 merged PR 38. Plan written before the first edit. Snapshot of 74 files, 32 originals and 5 key inputs equal before and after. Windows one at a time under the lock, 0 waits, 4.78 to 8.9 GB free at the lock, dialogs by rule 3 in our own Revit only, registry untouched. R260 is DATA-5's, this round used R261 to R282
- Q: open questions keep their number as a fixed ID, Q043 closed by DATA-5, DATA-5's 44 to 49 are Q044 to Q049, new ones Q050 on. steps/questions-closed.md maps the old numbers of Z4, DATA-3 and 16. CLAUDE.md holds the ID rule and the majority rule
- K (R261 to R263): plan position and level apart, the footprint as an edge only third check, the Native dialect. On the round 16 and Z4 files it gives the counts of round 16, so their FAIL was the level
- 0 (R264 to R268): 1.1, 1.2, 1.3 upgraded and read (407, 696, 832 links). 1.1 writes levels and wrong plots in link Names, now mapped by the house frame (252 links). Key plans 394, 585, 783 plots. The four models hold 2,386 of 2,591 Phase 1 unit plots, 01A in 1.1 and 1.2. All link 20221206 - Links. Native saved 2022-11 to 2022-12, Parsons IMC job 2024-03: Parsons is newer
- 1, G (R269, R270): past_all.csv 2,732 rows. 6 El @ Entry gaps over 0.5 m between 1.4 and IMC, no wrong join: 5 typed 1.4 parameter faults, 1 real (2609)
- 2 (R271 to R277): strong rules: ramp class by code (Parsons 0.970, Native 2% 0.977), hands by block mirror and open end (0.913, 0.945), cluster side (0.981, 0.983), corner front by Orientation (0.967), Parsons FFL one per cluster at the highest El @ Entry plus base (0.905), steps by Rev 6 count (1.000). Weak: El @ Entry, ramp front, Native levels, CASE. The Parsons key follows Rev 6, the Native job levelled to the road 0.70 m over the roads subgrade surface. Two level profiles, no default. 5,709 overrides
- 3 (R278 to R280, R282): plan_imc_r17b.json 336 of 337 IMC plots with villas, lone duplexes, VD1, TH1-IM, placed 13 runs into 01101-PSN-IMC-R17B-TEST-NEW.rvt. Against the key: position within 1 mm 285, mirror 335, Z within 10 mm 336 (130 worse before), ramp type 336, steps 336. NBH 03 from CAD 404 of 490, hands as built 301 equal, 6 differ
- Gate: Python 950 to 995 passed and 1 skipped, .NET 421 passed

Checked:
- Second method (R281): table, join, rules, split and rebuilt file 20 of 20 (join 25 and 26) at seeds 14 and 7, after two faults of the check itself were fixed

Remains:
- El @ Entry and the Native levels stay weak until the Infrastructure IFC package and Rev 01 come (Q001, Q051)

Known bugs:
- The image job of the 404 links of NBH 03 ran over the 10 minute image step three times, the file opens in 78 s with its links loaded, no image of it was made

Next:
- The rules of Q050, Q001 and Q051 once answered, then the other neighbourhoods of Phase 1

Your turn:
Q050. Level profile: no profile is the default. The Parsons IMC key follows Rev 6 (heights 0.985 given its steps, steps 1.000) and the Native models follow no step rule: use parsons2024Rev6 for every new build, since you decided Rev 6 prevails
Q001. El @ Entry rule: the IFC in Downloads is another project, ask the client for the Infrastructure IFC package and the architect for the ramp slope. The road plus the ramp slope holds 0.285 of the IMC key within 5 mm, the 241 others are overrides until then
Q002. Ramp type: coded by Rev 9 code (0.970), 2% for C10, VL4, VL6, 0.5% for the rest. The 10 Flat Ramps of the IMC key slope like the others, keep them as overrides. Q003. Villa mirror: coded, the block mirror flag with the open end (0.913 IMC, 0.945 Native): accept
Q004. Setback: DLUS front. Q005. Front level: road snapped. Q006. Back corners parallel. Q007. Generic floor: whole face (0.955 IMC)
Q008. Units: ask for a baseplan with 01b. Q009. Typology_Rev9 for codes with no schedule. Q010. Sales Land Width. Q011. IME: nothing names it, ask which neighbourhood each IM letter is (see Q044)
Q012. FFL split: ask why. Q013. CASE second letter as built (CASE follows no tested logic, 0.424). Q015. Yard FFL minus 0.100 (0.997). Q016. Plots 2311, 2312 by baseplan. Q017. Plots 2301, 2302 VD4. Q018. 58 past ramps listed. Q019. Schedule FFL. Q020. Keep the north stop
Q021. Parallel windows over 8 GB free. Q022. TEST copies in Zone 02\ARC: delete when done. Q023. Delete the empty 15a folder. Q024. Curb and ROW from the R94 edge. Q025. 13 PDF refusals listed
Q026. Two curbs from one 2021 drawing, they differ at junction corners and curb breaks: keep the Roads curb, ask which is current
Q027. Two DP4 labels: ask for a split. Q028. Zone 4: the 1.1 and 1.2 models hold 234 of the 297 plots of 01A, use them as its past key, still send the IMG job if it exists. Q029. Location plan number per face. Q030. Ask for the 01b sales rows. Q031. Rev 9 faces: 97 differ, keep the baseplan, ask which is current
Q032. 1.4 FFL parameter as the design level. Q033. The lone DT3 on a VL3 face: now a unit alone with the hand rule of Q042. Q035. round_z4\work and round_16\work: delete next round. Q036. TBD for DLUS, accommodation for sales. Q037. Orientation side coded (909 of 940 corner plots): confirm
Q038. The Native ramp fronts sit 0.698 to 0.700 m over Total Top of Roads Subgrade (-0.70) in all four models, so the 2022 job used the road that surface was cut from: confirm, and keep the IMC schedule as the level key. Q039. Footprint test in every placer check: done as the edge only third check. Q040. Footprint clusters
Q041. Rows 2290 to 2299: the 4 middle DT1 are overrides of the IMC key: accept. Q042. Lone duplex hand: coded, block mirror flag with the open end (15 of 16): accept
Q044. config/area_codes.json: approve as names only, the area stays the Neighbourhood_ID, but correct its guessed rows: the Native Files show IMA to IMD name sub-phases 1.1 to 1.4, IMA holds 02 and 01A and IMB 03 and 01A, not 021 and 022. Q045. R06_0000 and R06_0001 were saved here on 2026-10-22: confirm their source
Q046. A probe pass for the unread alignments and parcels. Q047. Delete the IFC copy next round. Q048. Delete the old Cutsheet lock files. Q049. Leave attribute values out of the probe
Q051. Rev 01 values are not in the repo, only its date for the foundations: send the Rev 01 sections on steps, base heights and ramps, to tell whether native2022 follows Rev 01
Q052. Weak rules, each coded with its overrides: El @ Entry, ramp front and fall, Native height (0.353), Native Generic floor (0.899), foundation type (0.685), CASE: accept until Q001 and Q051 are answered
Q053. The override list holds 5,709 overrides, 5,222 from the Native models, most from their weak level rules: keep all as the majority rule says, a build applies only its source
Q054. R260 is used by DATA-5 outside its range, Round 17 used R261 to R282: keep both as they are. Q055. The 1.4 ELl@Entry parameter is typed 0.41 to 1.59 m off its own ramp on 2461, 2470, 2473, 2481, 2557: read El @ Entry off the ramp there
Q056. The Native models use the 2022 layout with old client units, other than Rev 9 on most plots: use them for hands, fronts and ramps, never for unit codes. Q057. Ramp front fall: 97 IMC overrides list a fall with no direction, not applied: accept
Q058. Old prototype files of 20221206 - Links are not here, their hand comes from the CAD reference (V3 and V2 opposite to VL6 and VD2, the others the same): send the folder to check it on the files
Q059. NBH 03 build: native2022 levels, the 1.2 El @ Entry and front overrides, no mirror or height overrides (old files, other units): accept. Q060. round_17\work keeps the three 2024 key copies and the two new files: delete next round
Q061. The image of the NBH 03 file ran over the 10 minute image step three times, links on and off (it opens in 78 s with its 404 links, file B made its image in 91.5 s): raise the image step to 30 minutes next round, open the file by hand until then

## Round DATA-5, 2026-10-25, the audit of DATA-4, the missing xrefs, the reader counts, the area key, the IFC and the coverage table

Goal:
- Say for every drawing and workbook of Roads, Base Plans, Master Plan and Rev.09 what inside Phase 1 is read and what is not and why, and give every plot one area key. Summary: steps/investigations/data-book.md

Done:
- Session started in the repo, hooks live. Worktree site-adaptation-tool-data5 on data/data-book-5 from main at 3a5dd48. Plan written before the first edit. 576 sources and the IFC hash the same before and after, 171 copies equal (the IFC copy new). Five Civil 3D windows under the lock, one stopped by the 2 GB floor at 16 s, four DXFOUT runs of 25 to 30 s, lowest free 2.05 to 3.00 GB, dialogs by rule 3 in our own process, registry untouched
- Q (R251): of the 115, 97 plots hold a shape, number or code verdict (DATA-4 added them up to 105, every number and code verdict sits on a shape plot) and 18 hold only the DWG text (16 TH1, 2 TH3). The sixth code difference is plot 2302, an OS parcel on a face with two plan numbers, so the PDF had no code to compare. 29 of 30 DATA-4 counts equal the tables, parcels.csv alone falls short on 5 since 8 faces share a parcel, parcel_matches.csv added
- X (R252): the 4 are Makkah Phase 1_Road Layout_new (R06, R06_0000, R06_0001) and XR-TITLE BLOCK (Typical Cross Sections), on no folder searched. Both road layouts on disk were saved after R06 asked for _new. R06_0000 and R06_0001 carry a save of 2026-10-22 on this machine
- C (R253 to R255): a DXF of each road copy counted as plain group codes equals the probe on LINE 165,529 and every other type but 106 arcs, 20 hatches, 3 MTEXT at the border. The 6,043 rows CadReader lacks on Road Layout are 1,418 TEXT, MTEXT, DIMENSION, WIPEOUT, LEADER and surface rows it puts elsewhere or skips and 4,625 geometry rows under MP_CEC and the dynamic ARROW blocks. Road Surface and Rough Grading differ only by cut parts and surface rows. The probe is right and loses only 879 vehicle tracking attribute values. The two road layouts: both created 2021-11-27, saved 2023-03-06 (Roads) and 2023-01-31 (Base Plans), no title block or revision text, they part at junction corners and curb breaks, 14.4 and 16.1 percent, image in the data book
- A (R256, R257): 2,934 of 2,956 plots keyed from TBD PH_1-DLUS, 0 with two values, 22 with no DLUS row (11 parking, 8 SS, 1 OS, 1 buffer, 1 DP4 face), 2,580 same as the sales sheet, 0 differ. IMC files hold 012 only, the IMD schedule 013, the 1.4 model named IMD 012 and 022, IMG is a title text of the Zone 4 plan, IMA and IMB are Native Files 1.1 and 1.2. Nothing names IME. config/area_codes.json proposed
- I (R258): the IFC is Civil 3D 2023, IFC4X1, project 1TB02012, AMA_ fields, 342 utility wall proxies and 32 alignments, 0 of 42,539 points in Phase 1, no spot or surface. Not the Infrastructure IFC package
- T (R259): the coverage table, the truth table and the list of data no file holds in steps/investigations/data-book.md
- New: data4_audit.py, xref_search.py, dxf_count.py, road_layouts.py, area_key.py, im_codes.py, ifc_header.py, coverage.py, data5_check.py, c3d_window.py --dxf-folder, parcel_matches.csv, config/area_codes.json. plots.csv got zone, nbhId, areaKeyMethod, areaKeyCheck. Gate: Python 925 to 950 passed and 1 skipped, .NET 421 passed
- Clean up: the VL4 and VL6 _TEST copies, the DXFs and the CadReader reads of the round deleted, 958,168,126 bytes. Kept the data book tables, the IFC copy, round_z4\work and round_16\work

Checked:
- Second method (R260): area key by the xlsx XML 20 of 20 and 19 of 20 plus 1 with no row, parcels by ray cast 20 of 20, curb by brute force 20 of 20 at seeds 14 and 7. DXF line scan 25 of 25 types and every BLOCK record, IFC 49 of 49 types, IM file names 4 of 4 codes equal
- The DXF parser had two faults, found while matching it with the probe (an attribute tag renamed its block, a flipped insert did not mirror its block), fixed before the counts above. The line scan of the check counted EOF in a test file, fixed

Remains:
- 154 alignments of Road Surface and the parcels of CEC (1), R06 (5), R06_0000 and R06_0001 (1 each) and Road Layout (1) are not read, the probe does not reach them

Known bugs:
- Civil 3D still ends with 0xC0000005 or 0xC0000409 after C3DQUIT. CadReader reads model space only and no Civil 3D object. The probe writes no attribute value

Next:
- A probe pass that walks every block definition for alignments and parcels, then the rules waiting on 1, 3, 38 and 42

Your turn:
1. El @ Entry rule: the IFC in Downloads is another project (Part I), so no plot entrance spots are on this machine: ask the client for the Infrastructure IFC package and the architect for the ramp slope (see 38)
2. Ramp type: Ramps 2% for C10, VL4, VL6 and 0.5% for the rest. 3. Villa mirror: one hand per row by the open end, VL6 the other hand. 4. Setback: the DLUS front, the ramp stays 5.52 m. 5. Plot front level: the road surface snapped within 5 mm. 6. Back corners parallel to the front. 7. Generic floor: the whole baseplan face
8. Units: 2,698, the 120 on no plot are Zone 4: ask for a baseplan with 01b. 9. Codes with no schedule: Typology_Rev9. 10. Widths: the sales Land Width. 11. IME: nothing names it, the letter order guesses 02 (see 44): ask the client which neighbourhood each IM letter is
12. FFL split: ask why M07 and M08 split and M11 never. 13. CASE: the second letter is the hand as built. 15. Yard depth: FFL minus 0.100. 16. Plots 2311 and 2312: follow the baseplan and footprint. 17. Plots 2301 and 2302: VD4 from Rev 9
18. The 58 past ramps 97 mm above the surface: listed apart. 19. Keep the schedule FFL. 20. Angle to true north: keep the stop. 21. Parallel windows: more than 8 GB free. 22. TEST copies in Zone 02\ARC: delete when done. 23. The empty site-adaptation-tool-15a folder: delete it
24. Curb and ROW from the R94 edge. 25. The 13 plots the PDF check refuses: listed. 26. Two curbs: both from one 2021 drawing, Roads saved 5 weeks later, no revision text, they differ at junction corners and curb breaks: keep the Roads curb, ask which is current
27. Two DP4 labels in one face: ask for a split. 28. Zone 4 IMG past job: send 01101-PSN-IMG-ZZZ-PIM-TU-000001. 29. Official numbers: the location plan number per face. 30. 113 plots with no sales row: ask for the 01b rows
31. Rev 9 faces: 97 plots differ for real (not 105), 18 only by DWG text: keep the baseplan faces, ask which layout is current. 32. Treat the 1.4 FFL parameter as the design level. 33. The DT3 plot alone on a VL3 face: list apart
35. round_z4\work and round_16\work: keep until the next round, then delete. 36. Rev 9 workbooks: TBD for DLUS, the accommodation workbook for sales. 37. Corner front by the sales Orientation: confirm it is the ramp side
38. The 1.4 job sits 194 mm over IMC: keep the IMC schedule as the level key, ask which road version it used. 39. Footprint test in every placer check. 40. Clusters from the footprint blocks: keep. 41. Rows 2290 to 2299: keep the rule, list them. 42. Lone duplex hand: needs a rule
44. config/area_codes.json: approve it as names only, the tool keeps the area from Neighbourhood_ID (IMC 012 and IMD 013 measured, IMG Zone 4 by text, IMA 021, IMB 022, IME 02, IMF 03 guessed)
45. R06_0000 and R06_0001 were saved on this machine on 2026-10-22: confirm where they came from, keep R06_0000 for the villa blocks until then
46. The unread Civil 3D objects of Remains: run a probe pass that walks every block definition, one window per drawing
47. Delete the IFC copy in data_book\work\Downloads next round, it is another project. 48. Delete the two old lock files beside the Cutsheet copy in work
49. Attribute values: leave them out of the probe, the 879 in Road Layout are vehicle tracking tags

## Round DATA-4, 2026-10-25, the Rev 9 parcels in Civil 3D, the VL6 hand in Revit and where the past El @ Entry came from

Stub. The full entry is in steps/history/round-data-4.md.
- The Rev 9 parcels in Civil 3D, the VL6 hand in Revit and the source of the past El @ Entry (R240 to R250) were added and merged as PR 36. Gate then: Python 925 passed and 1 skipped, .NET 421 passed
- Its questions are carried in the list of the DATA-5 entry. Answered in DATA-5: 43 (the IFC in Downloads is another project), 35 (the VL4 and VL6 _TEST copies are deleted)

## Round 16, 2026-10-24, links against the CAD footprint, the corner front, two past jobs and a full IMC file from CAD

Stub. The full entry is in steps/history/round-16.md.
- The footprint test, the corner front by the sales Orientation, the levels of two past jobs and 205 IMC plots from CAD (R220 to R236) were added and merged as PR 34. Gate then: Python 912 passed and 1 skipped, .NET 418 passed
- Its questions are carried by fixed ID in the list of the Round 17 entry

## Round DATA-3, 2026-10-24, the Master Plan Rev 9 folder, El @ Entry by terms, the units on no plot and the location plan DWG

Stub. The full entry is in steps/history/round-data-3.md.
- The Rev 9 workbooks, drawing and PDF, El @ Entry by terms, the units on no plot and the location plan DWG (R185 to R198) were added and merged as PR 32. Gate then: Python 892 passed and 1 skipped, .NET 408 passed
- Its questions are carried in the list of the DATA-4 entry. Answered in DATA-4: the dialog rule, the Rev 9 parcels and the VL6 hand

## Round Z4, 2026-10-24, a new site file for one neighbourhood from CAD only, compared with the past site model

Goal:
- Build a new Revit site file from CAD only for one NBH and one prototype, with links, ramps and floors, and compare it with the past site model. Summary: steps/investigations/round-z4.md, steps for Bader: steps/bader-z4.md

Done:
- Session started in the repo, hooks live. Worktree site-adaptation-tool-z4 on test/site-z4 from main at a203d0a. Plan written before the first edit. DATA-3 ran beside it, coordinated by the shared window lock (R200)
- Snapshot of 209 data book files read all round, equal at the end (R201). The four 1.4 originals equal by hash before and after (R202)
- Part 0: BasicFileInfo from the bytes (R203): all four Format 2021, TU, PP, EL workshared. TU upgraded to a Revit 2024 test copy (R204), read (R205) after a reader fix, 643 links, 1,286 floors. The key is another dialect (plot in the link Name, FFL and Ramps floors, no yard, old client type prototypes), plan_z4_past.json by --names, 633 plots (R206)
- Part 0.5 and 0.6: the key holds NBH 012 (336 plots) and 022 (294 of 301), no 01A and no 013. Chosen 022, the NBH the CAD fits were not made on
- Part 1 (R207): DT3, RRE-WP-NSP-KEO-ARC-DT3-ZZ-MOD-3300.rvt, 62 plots, all in the key
- Part 2 (R208 to R210): plan_z4_cad.json 61 of 62 (one alone on a VL3 face has no mirror rule). Road surface of all 124 front corners Top Surface-02, no plot without a road level, front levels equal plots.csv within 5 mm 124 of 124. Ramps 0.5% 61
- Part 3 (R211 to R213): 01101-PSN-Z4-TEST-NEW.rvt from DefaultMetric.rte, coordinates from CAD with 5 check points within 0 mm, angle 0, levels, 4 floor types and 10 parameters by name. linksAndFloors placed 61 links and 183 floors in one run and saved. No fallback needed
- Part 4 (R214 to R216): the new file equals the CAD plan on every field of 61 plots (plan_compare 61 of 61 full match), so no Revit fault. Against the key: Generic outline within 10 mm 59 of 61, ramp outline within 10 mm 53, FFL a median 349 mm low, ramp front corners 210 mm off, link origin 20 to 32 m and rotation 180 deg off since the key links other prototypes. Worst 10 with reasons in the summary. Images of both files in round_z4\images
- New: window_lock.py, round_files.py, rvt_info.py, key_cover.py, site_spec.py, plan_from_book.py, z4_compare.py, z4_check.py, --answer-key and --names in plan_from_past.py, siteUpgrade, read, newSite and image jobs with SiteSpec and SiteCoordinates in Common, -RoundSpec in Run-TestHost.ps1, siteBuild in config/project.json. Gate: Python 808 to 852 passed and 1 skipped, .NET 371 to 405 passed
- Clean up (R218): 5 work entries deleted, 293,987,711 bytes. Kept the 2024 key copy, the new file and the images

Checked:
- Second method (R217): seed 14 38 of 38 plan values and 19 of 19 links agree, seed 7 40 of 40 and 20 of 20
- 8 windows one at a time, one 5 min wait for memory, no lock of DATA-3 met, the watcher only acted inside our own Revit

Remains:
- Coding the El @ Entry and ramp type rules, a 01A answer key, the link compare with the 1.4 prototypes

Known bugs:
- newSite try 1 stopped in its coordinates check (transform taken the wrong way), fixed. The first second method disagreed on 16 values on a band edge, fixed
- placer_check.py reads only the IMC dialect of a key, it reports FAIL on the 1.4 key by construction

Next:
- The rules of 1, 2 and 35 once answered, then a 01A or 1.4 prototype answer key

Your turn:
1. El @ Entry rule: not coded, by typology fits 221 of 747 within 5 mm, ask the architect what sets the ramp slope
2. Ramp type: Ramps 2% for C10, VL4, VL6 and 0.5% for the rest, and what makes a Flat Ramp
3. Villa mirror: one hand per row set by the open end, VL6 the other hand, and the villa alone between cluster units
4. Setback line: 5.00 m (2,530) or 3.00 m (37) is the building setback, the ramp stays 5.52 m. 5. Plot front level: the road surface snapped within 5 mm, Top Surface-02 for IMC, Top Surface-01 for IMD
6. Back corners parallel to the front, mean at El @ Entry. 7. Generic floor: the whole baseplan face
8. Round 16 test host run: yes, on a work file, once 2 is settled
9. Units: count from the footprint blocks (2,698), ask for the 3 villas of the sheet and the 6 of Rev 6. 10. Unit codes with no schedule: take Typology_Rev9 by the location plan number (1,819 of 1,831)
11. Widths over Rev 6, C10 58 and VL3 41: the architect confirms the unit fits. 12. IME: send its schedule or boundary
13. FFL split: ask why M07 and M08 split and M11 never. 14. CASE: the second letter is the hand as built
15. VL6 hand: confirm in Revit it is drawn the other hand from VL4. 16. Yard depth: FFL minus 0.100 until answered
17. Plots 2311 and 2312: follow the baseplan and footprint (VL4, VL4, OS), listed apart. 18. Plots 2301 and 2302: Rev 9 and the link say VD4, the schedule DP4, take VD4 from Rev 9
19. The 58 past ramps a median 97 mm above the surface: a past design choice, listed apart
20. RRE_ZZ_SP_Prototype-FFL: keep the schedule FFL. 21. Angle to true north: keep the stop, CAD gives grid north only
22. Parallel windows: more than 8 GB free, Z4 had 3.5 to 4.9 GB. 23. TEST copies in Zone 02\ARC: delete when done
24. The empty site-adaptation-tool-15a folder: close what holds it and delete it, steps/bader-data-2.md
25. Curb and ROW distance from the R94 edge (312 and 336 of 336), the R122 front stays for width, links, levels
26. The 13 plots the PDF check refuses: keep label plus shift (336 of 336 IMC agree), listed. 27. The two curbs differ by over 1 m on about 15 percent: keep the Roads curb, ask which is current
28. Two DP4 labels in a face with many labels: ask for that face to be split. 29. The Zone 4 IMG past job as a second answer key: send it if it exists, none is on this machine
30. Official numbers: take the location plan number per face (746 of 747), a fixed shift fails in 01A, 03, 022
31. 113 plan plots with no Rev 9 sales row (81 in Zone 4): ask whether the sales sheet is complete
32. Location plan versions: keep the Rev.09 PDF, read the DWG of 01_Output next
33. The 1.4 model named IMD holds NBH 012 (336) and 022 (294), no 01A: Z4 built 022, send a 01A past model if one exists
34. The 1.4 key levels sit a constant datum above its FFL parameter (643 of 643): treat the parameter as the design level
35. Estimated FFL a median 349 mm under the 1.4 key, its ramp fronts 210 mm off Top Surface-02: ask which road it used
36. The 1.4 key links 8 old prototypes (D2, D4 and others) not on this machine: send 20221206 - Links, until then floors only
37. Three pairs with the key ramp on the other edge (19.5 m): look at them in the two images, the R122 front stays. 38. The DT3 plot alone on a VL3 face has no mirror rule: list it apart until 3 is answered
39. Reader fix of Z4 (rotation from the transform when Revit gives none): keep it, the reader changes only for a round
40. Watcher: Do Not Load for other add-ins inside our own Revit only, never another process: keep this reading of rule 3
41. round_z4\work keeps the 2024 key copy (74 MB) and the new file (7 MB): keep until the next round, then delete

## Round DATA-2, 2026-10-24, the full Civil 3D read, the analysis fixes, the wiring (Part W) and the location plan (Part L)

Stub. The full entry is in steps/history/round-data-2.md.
- The full Civil 3D read of three road drawings, El @ Entry from the road and the ramp, units per face, the wiring per source and the Zone 4 location plan (R169 to R184) were added and merged as PR 28. Gate then: Python 808 passed and 1 skipped, .NET 371 passed
- Its questions are carried in the list of the DATA-3 entry. Answered in DATA-3: the location plan versions (its 33), the IMG name in the title block

## Round DATA, 2026-10-24, the data book of Phase 1

Goal:
- Every drawing of Roads, Base Plans and Master Plan and the master plan workbook as tables inside the Phase 1 border, joined per plot and analysed, read only. Summary: steps/investigations/data-book.md

Done:
- Session started in the repo, hooks live. Worktree site-adaptation-tool-data on data/data-book from main at 16b59cf, rebased on c9cbd3b after 14c merged PRs 24 and 25. Plan written before the first edit. No acad.exe ran at the start, free memory 4.00 of 15.78 GB
- 15 sources copied to data_book\work with their layout, equal by SHA-256, all 15 sources and copies hash the same at the end
- Border (R163): Phase 1 Bound, label PHASE - 1, 9 vertices, 2,517,500 m2, holds 747 of 747 schedule plots, the other 7 rings 0
- New: C3DDATA and C3DQUIT in the probe (R162), Border, CsvTable and CivilDialogs, dotnet/SiteAdaptation.C3DWatcher (R161), python/c3d_window.py (R160), data_book_border.py, data_book_cad.py (R164), data_book_excel.py (R165), data_book.py and data_book_readme.py (R166), data_book_analysis.py (R167), data_book_check.py (R168)
- Full window: three tries on the cutsheet copy, each stopped by the runner within 16 s because free memory fell under 2 GB (1.75, 1.72, 1.94), before NETLOAD, no dialog. Stopped after three, see Your turn 1. vlax needs the same window, not tried
- Stand-in tables from CadReader for all 14 drawings, marked reader CadReader, the road surface drawing with surfaces.csv from its LandXML. Workbook 9 sheets, none with a plot or location column, kept whole. plots.csv 2,956 plots kept, 6 dropped as outside, 62 columns. README.md and analysis.md written. Two tables split by layer, no file over 20 MB
- Answers: no source gives a Rev 6 unit code for the 1,831 unit plots with no schedule. Road level snapped against El @ Entry within 5 mm 2, within 30 mm 259, worse 483 of 747, median 55 mm below. No source names IME

Checked:
- Second method (R168): border samples all inside on 14 drawings, row counts match data.json on 14 of 14, fresh CadReader recount equal on 4 of 4 (cutsheet, phase boundaries, Base Plans road layout, V9), plots.csv seed 14 20 of 20 and seed 7 20 of 20 on inside, area, neighbourhood, footprint block and V9 number
- Gate: Python 768 passed and 1 skipped (18 new), .NET 369 passed (11 new). The Civil project builds by hand with 0 errors. R62 forbids the text rev6 in Python, so the plots.csv column is unitRev6

UNKNOWN:
- Everything only Civil 3D shows inside Phase 1: names and levels of the 10 Rough Grading surfaces and 2 Road Layout surfaces, the alignment, profiles, assembly codes, the CEC parcel, point groups, data shortcuts, layer colours and states. Whether the security dialog shows the full DLL path, the watcher never met it
- The IME boundary and plots

Remains:
- The full Civil 3D read of the 14 copies once memory allows, it overwrites the stand-in tables. Then Rough Grading against El @ Entry
- A later round should change, not this one: config/project.json and rules/ for the 5.00 m setback line against 5.52 m and the plot widths over Rev 6

Known bugs:
- The runner first counted the stand-in data.json as done, fixed: done needs reader C3DProbe written in that run
- The build nodes of another session held about 1.3 GB, they were left alone

Next:
- The full window read when Bader answers 1, then the Rough Grading levels against the schedules

Your turn:
1. Memory for the full window. Recommended: close Firefox and the VS Code windows you do not need before the next run (about 3.3 GB), or let the floor go to 1.5 GB for the small drawings only
2. Setback line. Recommended: the drawn line sits at 5.00 m, keep the Rev 6 ramp at 5.52 m and treat the line as the building setback, not the ramp edge
3. Unit codes for new zones. Recommended: no drawing has them, ask the client for the Rev 6 unit schedule of Phase 1 by plot
4. V9 numbers. Recommended: treat V9 as its own numbering and keep the baseplan label plus shift as the official number
5. IME. Recommended: send the IME schedule or its boundary, no source names it
6. Plot widths over Rev 6 (C10 58, VL3 41, VL4 70). Recommended: list them for the architect to confirm the unit fits, plots.csv holds each width

## Round 14c Part B, 2026-10-24, floors in the CAD plan

Goal:
- Generic, yard and ramp floors in plan_imc_cad.json from fitted outlines, the Rev 6 ramp and the road, compared per kind with plan_imc_past.json

Done:
- Branch feat/cad-floors-14c from main at 2119fe9 after PR 24. The first 14c worktree and the c3d worktree were removed outside this session and a data worktree appeared, so Part B runs in a fresh worktree, nothing was lost. Full record: steps/history/round-14c-b.md
- New: python/floor_fit.py (R135, R136), config/floor_outlines.json (written by it), cadLinks.floorWidthStepM, floors in plan_from_cad.py (R137), floor compare in plan_compare.py (R138), python/cad_floor_check.py (R139). Gate: Python 740 to 750 passed and 1 skipped, .NET 358 passed
- B1 (R135): outlines in link axes per unit and mirror, test halves within 10 mm Generic 116 of 152, yard 115 of 152. Adding the plot width: Generic 138 of 139, yard 135 of 139, so the plan groups by width. The whole face holds the Generic floor within 10 mm on 319 of 320
- B2 (R136, R137): slope bands Ramps 0.5% under 1.25 percent, Ramps 2% above, Flat Ramps not separable, they pick the past type on 233 of 321. Front corners at the road of their curb point plus 0.098 m (234 of 640 within 5 mm). Back corners parallel to the front pair, mean at El @ Entry, since both at El @ Entry put 6 of 436 back corners within 5 mm
- B3 (R137, R138), 218 plots outside the cut plots: Generic outline within 10 mm 217 of 217, top within 5 mm 192, type 217. Yard outline 215 of 217, top 192, type 217. Ramp outline 218 of 218, corners within 5 mm 44 and within 30 mm 164, type 140. The 25 tops off are the split clusters
- B4: no Revit run, see Next

Checked:
- Second method (R139): fit 40 of 40 at seeds 14 and 7, all 641. outlines and ramps 20 of 20 at seeds 14 and 7, all 224. compare 6 of 6 counts

UNKNOWN:
- What sets the ramp type: the depth slope breaks on 78 Ramps 0.5%, the typology (2% on C10, VL4, VL6) holds on 311 of 321 and is not coded
- What sets a Flat Ramp
- Where Top Surface-02 ends (C3D question), the front corners take the road at the curb point plus 0.098 m

Remains:
- The villa floors, after the villa mirror. Floors for plot widths with no past group

Known bugs:
- Ramp type differs on 78 of 218 and the ramp corners are within 30 mm on 164 of 218
- The plan writes a mirrored loop the other way round, the placer has not built one from a CAD plan yet

Next:
- Round 16, a test host run of plan_imc_cad.json: make a work file from the test master (scenario_files.py work), a linksAndFloors job on the 2024 prototype folder with the plan path, the floor types Ramps 0.5% and Ramps 2% and the levels of floorSettings in the master, then placer_check --floors against the answer key and plan_compare against the placed model. Known gaps to expect: no villas, 25 split cluster tops, ramp types and corners as above. Free memory under 8 GB keeps it to one window

Your turn:
1. Ramp type. Recommended: take Ramps 2% for the steps.one_step_only typologies (C10, VL4, VL6) and Ramps 0.5% for the rest, 311 of 321, and tell me what makes a Flat Ramp
2. Back corners. Recommended: keep the back edge parallel to the front with its mean at El @ Entry, both at El @ Entry is off on 430 of 436 back corners
3. Generic floor. Recommended: take the whole baseplan face (319 of 320 within 10 mm), it serves every plot width
4. Round 16 test host run. Recommended: yes, on a work file, once the ramp type is settled

## Round 14c Part A, 2026-10-24, villa mirror, CASE and FFL split tested on the data

Goal:
- Find the rules for the villa mirror, the CASE second letter and the FFL split, and code each only when it holds on every plot outside the 16 cut plots

Done:
- Session started in the repo, main at 588d7a5, worktree site-adaptation-tool-14c on feat/cad-rules-14c. The c3d probe worktree and its files were not touched. Plan written before the first edit. Full record: steps/history/round-14c-a.md
- New: python/cad_rules.py (R131 to R133), python/cad_rules_check.py (R134), cadLinks.cutNeighbours and caseParameters in config/project.json, villa.driver_house in the profile, plotFrame measured again with the 16 cut plots apart (fits unchanged). Gate: Python 731 to 740 passed and 1 skipped, .NET 358 passed (main holds round C3D)
- A1 villa mirror (R131), 113 villas: VL6 runs the other hand (10 of 11). a one hand per row 33 of 33 rows, b blind side to the neighbour 62 of 63 (breaks 2534), c C10 15 of 15 and VL6 10 of 11, d the Round 14 pattern holds on C10, VD2, VD4, VL4 and breaks on VL6 because of its hand. The full rule holds on 93, breaks on 2 (2534, 2535) and leaves 8 undecided. Not coded, villas stay out of the plan
- A2 CASE (R132): facade option and colour are XX on all 337. The hand explains 331 of 337 and 317 of 321 outside the cut plots, breaks 2547, 2548, 2589, 2594. Steps 209, unit 182, corner 182. Not coded
- A3 FFL split (R133): the height rule reproduces 0 of 11 IMC and 0 of 13 IMD schedule splits and adds 4 M11 splits the schedule does not make. The schedule splits M07 and M08 in 24 of 25 and never M11 or a pair. Not coded, one FFL per cluster stays
- A4: PROGRESS rewritten, Round 14 merged, the yard answer is in

Checked:
- Second method (R134): villas seed 14 20 of 20, seed 7 20 of 20, all 113. split seed 14 20 of 20, seed 7 20 of 20, all 225. case count and breaks agree

UNKNOWN:
- Why VL6 runs the other hand, the KEO VL3 and VL6 files are not read
- Where Top Surface-02 ends: R127 counted it covering the curb points and not the ramp corners, round C3D measured its edge on the plot line within 2 mm. This decides the front corner levels of Part B (C3D Bader's turn 1)
- What decides a schedule split of M07 and M08 when every height stays under 0.54 m
- The side of the driver's house in the C10 and VL6 files

Remains:
- Part B, floors in the CAD plan

Known bugs:
- 8 villas between two cluster units have no row end, 2534 and 2535 break the row rule

Next:
- Part B from main after the merge

Your turn:
1. Villa mirror. Recommended: confirm the hand rule (one hand per row, set by the open end, VL6 the other hand) and say what decides a villa alone between two cluster units, then I code it with 2534 and 2535 listed
2. FFL split. Recommended: ask the architect why M07 and M08 split at the middle wall and M11 never, the height rule is not it
3. CASE. Recommended: confirm the second letter is the hand as built, the 4 breaks are then past data
4. VL6 hand. Recommended: confirm in Revit that the VL6 file is drawn the other hand from VL4

## Round C3D, 2026-10-24, the road drawings in Civil 3D 2026 tied to the plots

Goal:
- What the road surface drawing, the Roads folder and the Base Plans folder hold, read only, and the best source for the level at the plot front. Summary: steps/investigations/civil3d.md

Done:
- Session started in the repo, both hooks live. Worktree site-adaptation-tool-c3d on probe/civil3d from main at 588d7a5 after PR 21 (Part B merged first). Plan written before the first edit
- Civil 3D 2026 in Program Files\Autodesk\AutoCAD 2026: acad.exe R25.1.74, accoreconsole.exe 25.1.74, C3D\AeccDbMgd.dll 13.8.280, ACA\AecBaseMgd.dll 8.8.58. Free memory 4.87 of 15.78 GB, core console only. No acad.exe or accoreconsole.exe ran at the start, so the open session of the prompt was not on this machine
- The 10 drawings copied to c3d_probe\work with their layout, equal by SHA-256. Every run left its copy unchanged, the sources hash the same at the end. Run logs and process ids in c3d_probe\logs
- New: dotnet/SiteAdaptation.C3DProbe (R150, R152), python/c3d_run.py (R151), c3d_inventory.py (R153), c3d_drawings.py (R154), c3d_surfaces.py (R155), c3d_connect.py (R156 to R158), c3d_check.py (R159), settings in python/c3d_probe.json. The net48 probe project was added to the solution and to the test project, its Civil 3D commands sit in Civil/SiteAdaptation.C3DProbe.Civil.csproj
- Part 1: 7 TIN surfaces and no corridor, alignment, feature line, grading, COGO point or pipe. Top Surface-01 and 02 do not overlap on a 2 m grid, 768 seam vertices differ by median 0 and worst 2 mm
- Part 2: Road Layout holds 2 surfaces, 1 alignment, 3 profiles and 1 assembly, Rough Grading 10 surfaces, the rest is plain AutoCAD. No corridor in any drawing, so no sidewalk link or point code. No site level text. The two road layouts line up
- Part 3: ROW lies on the plot front of all 337 IMC plots, sidewalk curb to ROW median 4.15 m. Top Surface-02 reaches the plot line: 403 of 674 front corners on it, 271 within 2 mm of its edge. Snapped within 5 mm it gives 672 ramp front corners, 539 within 5 mm, 41 within 30 mm, 92 worse, and covers all 119 uncovered ramps. Coverage IMC 100, IMD 99.3, other unit plots 98.6 percent. Building Footprint shows two VL4 on 2301 and 2302, the baseplan cut

Checked:
- Second method (R159): levels seed 14 40 of 40, seed 7 40 of 40, all 672 of 672 and 2 with no level either way. curb seed 14 20 of 20, seed 7 20 of 20, all 335 of 337 and ROW on the front 335 of 337. inventory LISP against ACadSharp 8 of 8 classes on all 10 drawings
- Gate: Python 708 to 731 passed and 1 skipped, .NET 347 to 358 passed. The Civil project builds by hand only (not in the solution), it built with 0 errors here

UNKNOWN:
- The names, counts and definitions of the 7 surfaces inside the drawing, the 10 Rough Grading and 2 Road Layout surfaces, the assembly, point groups, data shortcuts and the drawing coordinate system. They need the probe
- IME: no schedule, no shift, no boundary on this machine

Remains:
- Run the probe once it may load: C3DDUMP on the road surface, road layout and rough grading copies, C3DBORDER against the LandXML border counts
- A later round should change, not this one: road_surfaces.json and levels.py to take the plot front level from the snapped road surface, plan_from_cad.py to write it as the ramp front level, rules/ for the sidewalk width

Known bugs:
- CadReader puts some pieces nested in anonymous blocks of the Road Layout at swapped coordinates (HS-PVMT-ARR), they fall outside the site and were left out
- The LISP inventory of CEC Building Footprint takes 924 s for 186,192 entities
- Conflict: Part B wrote that Top Surface-02 stops at the curb. Measured here its edge lies on the plot line within 2 mm on every IMC front corner

Next:
- The probe on the copies once Bader answers 1, then the plot front level into the CAD plan

Your turn:
1. Probe load. Recommended: add only C:\Users\bader\Documents\SiteAdaptation\c3d_probe\bundle to Trusted Locations in Civil 3D Options yourself, then I run the probe read only. Else allow a copy of the DLL into your C3D Support folder, or stay on LandXML
2. Rough Grading surfaces. Recommended: if 1 is no, ask the designer for a LandXML of the 10 Rough Grading and 2 Road Layout surfaces
3. Plot front level. Recommended: take it from the road surface snapped within 5 mm at the plot line, Top Surface-02 for IMC and Top Surface-01 for IMD
4. The 58 plots whose past ramp sits a median 97 mm above the surface. Recommended: a design choice of the past job, keep the surface level and list them apart
5. Plots 2311 and 2312. Recommended: follow the baseplan and the footprint drawing (VL4, VL4, OS) for new work and keep them listed apart
6. IME. Recommended: send its schedule or its boundary, until then it counts under other unit plots
7. The Part B line on Top Surface-02. Recommended: replace it with this measurement in PROGRESS

## Round 14 Part B, 2026-10-24, clusters and levels for the computed plan

Goal:
- Check 3, levels from El @ Entry, the Rev 6 ramp outline tested on the past ramps, and plan_imc_cad.json compared with plan_imc_past.json field by field

Done:
- Branch feat/cad-levels-14b from main at 497855c after PR 20. Full record: steps/history/round-14b.md
- New: check_one_ffl in rules_checks.py (R125), python/plan_from_cad.py (R126, R128), python/ramp_outline.py (R127), plan_compare.py --against-plan (R129), python/cad_plan_check.py (R130). ramp.front_setback (5520, C10 5500) and steps.one_step_only left rev6 of the profile. Check 3 left rules/README.md. Gate: Python 694 to 708 passed and 1 skipped, .NET 347 passed
- B1 (R125): splits allowed at every inner wall of a 4-unit cluster, counted from either end, never in a pair. IMC schedule FFL: 85 of 96 clusters one FFL, 11 split at allowed walls (10 middle, 1 DP to TH), 0 not allowed
- B2 (R126): levels.py with the raw El @ Entry on 337 plots, link Z is the FFL. No C10, VL4 or VL6 with more than 1 step. Height over 0.54 m on 4 plots
- B3 (R127): ramp of full width by 5.52 m (C10 5.50) from the plot front line matches the past ramp within 10 mm on 321 of 325, the 4 others are next to A1 plots. Top Surface-02 covers 414 of 1348 corners and 1346 of their curb points, all 119 uncovered ramps lie on the sidewalk
- B4 (R128, R129): plan_imc_cad.json writes 224 of 337 plots, 113 have no mirror rule (107 villas, 6 A1). Against the past plan on 218 plots outside A1: rotation, mirror and file equal 218, insert in plan within 1 mm 109 and within 10 mm 217, FFL equal 163 and within 10 mm 193, steps equal 196

Checked:
- Second method (R130): levels seed 14 20 of 20, seed 7 20 of 20, all 224 (24 on a half millimetre). ramps seed 14 20 of 20, seed 7 20 of 20, all 337. compare 4 of 4 counts

UNKNOWN:
- The villa mirror, so 107 villas are not in the CAD plan
- Whether the CAD plan should split the FFL where the schedule does, 25 FFLs are worse because levels.py never splits

Remains:
- Floors in the CAD plan, a Revit run of plan_imc_cad.json, IMD, the villa mirror, case and linkName

Known bugs:
- The FFL of 24 plots lies on a half millimetre and the 5th decimal of El @ Entry decides it
- 2338 is 10 mm off, 2543 and 2546 are 4 m off. 2374 and 2391 have ramps 2 m narrower than their faces

Next:
- Answer the villa mirror, then floors in the CAD plan and a test host run of it on a work file

Your turn:
1. Villa mirror. Recommended: tell me what decides it, the plan leaves the 107 villas out until then
2. FFL split. Recommended: keep one FFL per cluster in the CAD plan and list the 11 split clusters of the schedule, a split needs the slope rule first
3. Split positions [2, 3]. Recommended: confirm they are walls after unit n counted from either end, so all three inner walls of a 4-unit cluster may split
4. Plots 2374, 2391, 2543 and 2546. Recommended: list them apart with A1, their ramps and links follow the past cut of their neighbours

## Round 14 Part A, 2026-10-24, links from CAD for IMC

Goal:
- For every IMC plot, the link origin, rotation and mirror from the baseplan face, the road edges and the clusters, against the answer key prototypes_imc_past.json

Done:
- Session started in the repo, both hooks live, main equal to origin/main at 82a8f1e, branch feat/cad-links-14a. .claude/rules does not exist. Plan written before the first edit. Full record: steps/history/round-14a.md
- Part 0: the 15a folder is empty (no file, no .git) but held open by another process, rmdir and Remove-Item failed, not deleted. The 13 _backup folders of Prototypes_2024 were already gone, nothing deleted, no sizes to list. CLAUDE.md has the merge rule (gh pr checks --watch, both pass)
- New: python/cad_links.py (R120 to R123), python/cad_links_check.py (R124), plotFrame in config/prototype_offsets.json, cadLinks.cutShareMin 0.9 in config/project.json, R100 amended. Gate: Python 680 to 694 passed and 1 skipped, .NET 347 passed
- A1 (R120): 12 plots under 0.9, the list of the prompt (2311 2312 at 0.5, 2375 to 2378 and 2387 to 2390 at 0.8 to 0.833, 2544 2545 at 0.75). 2441 has no Generic floor. All counts below keep them apart
- A2 (R121): no origin within 10 mm of a corner of its face (0 of 337), so no corner rule. DT pairs sit on a front corner of the pair outline, 107 of 108. The origin is a fixed offset from one front corner per typology in link axes, within 10 mm on 322 of 325, misses 2338 (11 mm), 2543 and 2546 (4 m, the neighbours of 2544 and 2545)
- A3 (R122): front edge from 7.99 m up to below 18.75 m nearest RD-CURB-LINE, link Y axis into the plot. Rotation holds on 337 of 337. With the R94 front 329, the 8 others are corner plots
- A4a (R123): the Rev 6 lead holds on every DP pair and DP end (78) and breaks on every DT pair (108) and every middle townhouse (32). Coded as drawn side per typology, DP left, DT and TH right: 218 of 218 cluster plots hold, 0 break. 107 villas have no rule. pairRule stays false
- A4b (R123): no CASE letter maps to one rotation and mirror pair, each holds two. Each letter has one facing of the link Y axis, A B 300.1, C D 210.1, E F 120.1, G H 30.1

Checked:
- Second method (R124), json and math only: seed 14 20 of 20, seed 7 20 of 20, all 337 agree with 1 share not checked (2320, face and floor not convex)

UNKNOWN:
- What tells CASE A from B, C from D, E from F, G from H
- The mirror of a detached villa: one side neighbour on the left gives mirrored on C10, VD2, VD4, VL4 (54 of 54), VL6 breaks and two neighbours are mixed

Remains:
- Part B: check 3, levels, ramp outline, plan_from_cad.py and the compare

Known bugs:
- The empty 15a folder stays until the process that holds it closes
- 2543 and 2546 lie 4 m from the fitted origin, the 2544 2545 block is cut another way in the past model

Next:
- Merge, then Part B from main

Your turn:
1. Close the Claude or VS Code window that started in site-adaptation-tool-15a, then delete that empty folder. Recommended: yes, it holds nothing
2. Villa mirror. Recommended: tell me what decides it, else I keep villas out of the CAD plan as missing mirror
3. Plots 2543 and 2546. Recommended: list them apart like A1, they are the neighbours of the 2544 2545 cut
4. CASE A against B. Recommended: tell me what the second letter means, it is not the mirror

## Round 15b, 2026-10-24, placer step 2, Generic, yard and ramp floors, eight scenarios in Revit

Done:
- Session started in the 15a folder while the 15a session was busy there, so that folder was only read. Work in the main folder on feat/placer-floors-15b. The main guard hook read the branch of the start folder, which went back to main, so it blocked every commit of this session. At Bader's answer the first commit went through a script that refuses main, PR 19. Plan written before the first edit. Gate A passed 08:22 (PR 18, main 6e0da3d). Gate B passed, 13 prototypes Format 2024 by BasicFileInfo, no Revit running. Free memory was 3 to 3.6 GB the whole round, under the 8 GB of R110, so every Revit run was one window alone. Full record: steps/history/round-15b.md
- Gate: Python 620 to 680 passed and 1 skipped (no symlink rights), .NET 319 to 347 passed, locally and on GitHub. The Linux failure of test_r96_entries_without_analysis_are_refused_in_one_line (PR 17 and 18) is fixed: levels_proof.py refuses --entries without --analysis before it looks for the schedules
- Hook fix (Bader answer 2): guard-main.sh now takes the branch of the repo each git command acts on (cd, Set-Location, git -C, the hook cwd), not of the start folder. It splits on separators outside quotes, so a message or a heredoc that holds the words is no command, and walks bash -c and powershell -Command. Tests on real repos: a session started on main elsewhere, a worktree, git -C, cd, PowerShell paths, push, main blocked. A 27 KB command with 280 git segments takes 0.6 s. The second commit went through the fixed hook: it was copied into the 15a folder this session loads its hook from, and a commit on main there was blocked by it
- link_summary.py takes the Generic and yard type names from config/project.json floors (answer 6), no Python file holds a floor type name now
- Plot 2311 (answer 4), read in the baseplan output: face 326 holds one text, OS 326, an open space of 150 m2, and face 2301 holds VL4 2301, 300 m2. The labels are right and no face holds two plots. The past model sets its DP2 pair 2311 and 2312 half a plot over: each Generic floor lies 0.5 on face 2301 and 0.5 on face 326 or 2302, and the link of 2311 stands in face 326. So in that block the baseplan cut (two VL4 plots and an open space) is not the plot cut of the past model. 10 more Generic floors lie 0.75 to 0.83 on one face (2375 to 2378, 2387 to 2390, 2544, 2545), their yards still resolve. The yard rule is not changed
- Rules R111 to R119: yard rule, floors in the plan, floor step and its stops, plot value and skip with ramp corners, geometry, run and report, test host floor jobs, work file life cycle, floor check
- Plan: 337 plots, Generic 336, yard 336, ramp 337 (Ramps 0.5% 280, Ramps 2% 47, Flat Ramps 10). Yard rule: 337 yards assigned, 0 ties, 0 low share, 1 goes to official 336, which is no plan plot. Listed: 2441 no Generic floor, 2311 no yard. No arc in the past model. 47 yards are two pieces, both kept
- API: Floor.Create with CurveLoops of Line.CreateBound and Arc.Create through 3 points, FLOOR_HEIGHTABOVELEVEL_PARAM, and for ramps SlabShapeEditor.Enable and ModifySubElement per corner from the lowest corner, because the past ramps are shape edited and this sets each corner exactly. Two regenerations per run
- Review: 9 points, all fixed with tests (arc loop taken for a second piece, past data hid outline faults, equal ignored the plan, uncompared and filtered floors passed, sliver yards, second method, one plot list in the run script, delete before refuse, ramp read back). A second short review found 6 small points (arc chords too coarse, two rule texts, a WORK name in clean-old, an empty plot list, a crash in the second method), all fixed with tests. S1 and S8 rerun on fresh copies after the fixes, both pass. The last small fixes were checked by the suite and the S1 and S8 checks, not by a third review

| Scenario | Runs | Run min | Result | Against the answer key |
|---|---|---|---|---|
| Master | 1 | 1.1 | ok | 14 links unloaded, 1010 floors deleted, 0 left |
| S1 full | 2 | 1.8 | pass | 1009 placed, all checks equal, outline worst 0 mm, 288 yard tops past data |
| S2 run twice | 1 | 0.7 | pass | both runs placed 0, 1009 skipped as placed, 2 not in plan, 1009 before and after |
| S3 dry run | 1 | 0.5 | pass | would place 1009 on 337 plots, model 0 floors |
| S4 partial | 1 | 0.7 | pass | seed 15, 10 plots, 30 deleted, 30 placed, 981 skipped, all 337 match |
| S5 missing type | 1 | 0.5 | pass | stopped before any change, 0 before and after |
| S6 rollback | 1 | 0.6 | pass | crossed loop of plot 2458 refused by Revit, all rolled back, 0 after |
| S7 ramp types | 0 | 0 | pass | 0.5% 280 of 280, 2% 47 of 47, Flat 10 of 10, arc plots 0 |
| S8 links and floors | 2 | 0.7 | pass | C10 15 links within 1 mm, 45 floors equal, 15 yard tops past data |

- Deleted: the 27 TEST files of the 15a test, 1,413,824,512 bytes, each listed in the history file, then the work files S1 to S8 after their checks. Left: 01101-PSN-IMC-ZZZ-PIM-TU-000001_TEST-MASTER.rvt, 48,742,400 bytes, links unloaded, no floor of the five types. The site model was unchanged by hash after every run

Remains:
- After the merge: delete the 15a folder and the 13 _backup folders of Prototypes_2024 (answer 7), listed in the reply of the round
- Not run: parallel windows (memory under 8 GB), a plan with arcs in Revit (the past model has none, proven on fakes only)

Known bugs:
- Yard depth, a question for the architect: the plan keeps FFL minus 0.100 on every yard (Bader). The past model has FFL minus 0.100 on TH1, TH3 and VL6 only and the FFL on every other unit, so 288 yard tops differ from it and are counted as past data
- Past model data: each full run gives 767 Revit warnings Highlighted floors overlap, since the floors are placed on the outlines of the past model to 0 mm and those overlap there too
- The past plots 2311 and 2312 do not follow the baseplan cut, so plot 2311 has no yard (see Done)
- A second piece of a floor comes from the top face outline, so an arc on it would be chords

Next:
- Merge, then the combined Revit test with parallel windows once more than 8 GB is free
- Fill plan.json from CAD and LandXML in the same format

Your turn:
1. Ask the architect for the yard depth per unit. Recommended: ask with the list above, the plan keeps FFL minus 0.100 until the answer
2. Plots 2311 and 2312. Recommended: ask which plot cut is current in that block, the baseplan (VL4 2301, VL4 2302, OS 326) or the past model (DP2 pair), before the CAD plan is built for it
3. Free memory for the parallel windows. Recommended: close what you can so more than 8 GB is free before the combined test

## Round 15a test, 2026-10-24, the test host drives Revit, one prototype per run

Done:
- Folder C:\Users\bader\Documents\GitHub\site-adaptation-tool-15a, a fresh clone of main at e73da18 (the session folder was empty, the main folder ran 12b), branch test/revit-testhost-15a. The main guard hook loaded at session start, the new registry hook is not live in this session. Main moved to 907bb0c (Round 13, PR 17), merged into the branch, my rules renumbered to R102 to R110 and their tests with them, no Round 13 file changed. Plan written before the first edit. Full record: steps/history/round-15a-test.md
- Gate: Python 620 passed, .NET 319 passed (main had 573 and 205), locally. On GitHub the Linux Python job fails one Round 13 test, as on PR 17, see Known bugs
- Rules: R102 placer filter, R103 test host guard, R104 steps and log, R105 placer_check.py, R106 registry hook, R107 link instance Name, R108 security box watcher, R109 upgrade job, R110 parallel windows. R80, R81, R89 amended for linkName and the 2024 folder
- Answer key copied once to prototypes_original_imc.json, 10,355,725 bytes, 2026-10-24 01:51:15 kept, hash equal. Plan: 337 of 337 plots, linkName on 337, 2 differ from RVT-Name and are conflicts
- The watcher answered 5 boxes per Revit start: Do Not Load for RCRC Green and RCRC Green Audit, Load Once for the reader, the placer and the test host (Bader). The boxes show no GUID, the publisher is logged as vendor
- Prototype runs, each on a fresh TEST copy, first run each, original model unchanged every time:

| Code | Folder | Runs | Run min | Plots placed | Result |
|---|---|---|---|---|---|
| C10 | 2021 | 1 | 3.7 | 15 of 15 | pass |
| TH1 | 2021 | 1 | 4.0 | 16 of 16 | pass |
| VD2 | 2021 | 1 | 2.4 | 29 of 29 | pass |
| DP1 | 2021 | 1 | 4.2 | 34 of 34 | pass |
| DP2 | 2021 | 1 | 2.3 | 12 of 12 | pass |
| DP3 | 2021 | 1 | 2.4 | 10 of 10 | pass |
| DP4 | 2021 | 1 | 2.3 | 32 of 32 | pass |
| DT1 | 2021 | 1 | 3.2 | 44 of 44 | pass |
| DT3 | 2024 | 1 | 1.1 | 68 of 68 | pass |
| TH3 | 2024 | 1 | 0.9 | 16 of 16 | pass |
| VD4 | 2024 | 1 | 0.9 | 27 of 27 | pass |
| VL4 | 2024 | 1 | 0.8 | 15 of 15 | pass |
| VL6 | 2024 | 1 | 0.9 | 19 of 19 | pass |

- Every check: position within 1 mm (worst 0 mm), rotation, mirror, Z equal FFL, instance Name and the 9 parameters equal on every plot, missing, extra and skipped 0. RRE_ZZ_SP_Prototype-FFL empty on every new link. Reader fixes: every loaded link has a box, spots with a host 986 of 987. Second method 3 of 3 on every run, seeds in the history file. PowerShell spot check of 5 random plots, seed 24: 5 of 5
- Upgrade: the 13 prototypes saved as Revit 2024 centrals in Zone 02\ARC\Prototypes_2024, one per run, one after another, 07:24 to 07:57. Sources unchanged. Each new file Format 2024 by BasicFileInfo and again from its bytes, same names. Upgrade time 86 to 142 s, sizes in the history file. plan.json gives the 2024 folder to the placer, the 2021 folder stays in the config as the source
- Review: a fresh reviewer found no way to make a normal session act and 9 edge points, all fixed with tests

Remains:
- Not proven in Revit: the parallel windows of R110 (one job file and token per window, per job outputs) and Run-Parallel.ps1. They wait for the combined test after Round 15b. The parallel upgrade was not run: the 13 targets already exist and are never overwritten, and free memory was 5.2 of 15.8 GB, under the 8 GB rule
- The R110 guard had no fresh review

Known bugs:
- My first install recreated the CodeSigning key in the registry and removed four Always Load answers of other add-ins. Restoring them was blocked and you said to leave them. Since then nothing in the registry was read or written
- On 9 of 13 runs TransmissionData did not get the prototype loaded at open. The placer loads it anyway, so no result changed
- 1 spot of 987 has no host, element 996508
- On the Linux CI runner test_r96_entries_without_analysis_are_refused_in_one_line of Round 13 fails, it looks for the real schedules first. Not changed, it is a Round 13 file. test_r80 failed there since PR 15 and is fixed here (the file name of a Windows path on Linux)

Next:
- Round 15b, then the combined Revit test with parallel windows on the 2024 copies

Your turn:
1. Free memory for the parallel windows. Recommended: close what you can so more than 8 GB is free before the combined test. The peak memory of one window was not measured in this round, Run-TestHost.ps1 logs it from now on
2. You can delete C:\Users\bader\Documents\GitHub\site-adaptation-tool-15a. Next rounds work in the main folder or a git worktree. Recommended: yes, after this merge
3. The TEST copies in Zone 02\ARC (14 models and their backups) and the 13 _backup folders in Prototypes_2024 stay. Recommended: delete the TEST copies when you no longer need them, keep Prototypes_2024
4. The yard rule of Round 15a is still open. Recommended: as in the Round 15a entry

## Round 13, 2026-10-24, unit codes, road levels, entry level per plot, block to link offset

Stub. The full entry is in steps/history/round-13.md.
- Unit codes from the Excel unit only, the road level lookup, the entry level per plot and the block to link offsets (R90 to R101) were added and merged as PR 17. Gate then: Python 573 passed, .NET 205 passed
- Its numbered questions are in the full entry, from before the fixed IDs of Round 17

## Round 12b, 2026-10-24, villa blocks from Makkah Building Footprint, unit codes into the clusters

Stub. The full entry is in steps/history/round-12b.md.
- The block scan, the Excel unit, the clusters with no Rev 6 match, the second block match and the block against the link (R73 to R77) were added and merged as PR 16. Gate then, after main was merged in: Python 468 passed, .NET 205 passed
- Its numbered questions are in the full entry, from before the fixed IDs of Round 17

## Round 15a, 2026-10-24, plan.json from the past IMC job and the placer step 1

Stub. The full entry is in steps/history/round-15a.md.
- plan.json from the past IMC job, the placer step 1 and the plan compare (R80 to R89) were added and merged as PR 15. Gate then: Python 366 passed, .NET 205 passed
- Its numbered questions are in the full entry, from before the fixed IDs of Round 17

## Round 12, 2026-10-24, villa blocks, row split, road fronts, levels, master plan V9

Stub. The full entry is in steps/history/round-12.md.
- Block inserts (R68), block to plot (R69), row split (R70), road fronts (R71), xref lookup (R72), the level rule and its proof (R67), the Rev 6 profile and the master plan V9 role were added and merged as PR 14. Gate then: Python 323 passed, .NET 155 passed
- Its questions are answered in the round 12b entry: the baseplan stays, villaBlocks is the building footprint drawing, VL2 stays out of the profile, Q02 is flagged, the FFL is rounded to the mm, the height flag is the legend span, the road edge layer and the V9 layers stay. Still open from it: check 3 and the pair rule on blocks

## Round 11, 2026-10-24, project settings, cluster derivation, checks 1 and 2

Stub. The full entry is in steps/history/round-11.md.
- config/project.json (R63), python/cad_analysis.py (R64) and checks 1 and 2 (R65, R66) were added and merged as PR 13. Gate then: Python 229 passed, .NET 143 passed
- Its questions are answered in the round 12 entry: split long rows into listed clusters, the villa blocks, the front of a row, my settings stay. Still open from it: the official plot numbers in analysis.json

## Round 10, 2026-10-24, ROSHN Makkah rules profile and loader

Stub. The full entry is in steps/history/round-10.md.
- rules/roshn-makkah.json, the loader python/project_profile.py (R62) and rules/README.md were added and merged as PR 12. Gate then: Python 202 passed, .NET 143 passed
- Its questions are answered in the round 11 entry: split positions, VL4 width, project settings, where the ten checks live, cluster membership. Still open from it: the yard rule and the ramp slope, in steps/history/round-9.md

## Round 9, 2026-10-23, floor, ramp and link relations

Stub. The full entry is in steps/history/round-9.md.
- Floor measurements, parameter details, link state and spot hosts in the Revit reader (R51 to R56) and plot_relations.py (R57 to R61) were added and merged as PR 11. Gate then: Python 201 passed, .NET 143 passed
- Open for Bader: the Revit run of steps/for-bader-round-9.md, the yard rule and the meaning of the ramp slope in the type name. Both questions are in steps/history/round-9.md

## Round 8, 2026-10-23, ramp levels from the real corners, surface roles, floor count, one error line

Stub. The full entry is in steps/history/round-8.md.
- Ramp corner levels with the survey offset (R47), role proposal (R48), floor count per plot (R49) and one error line for floors with no Plot parameter (R50) were added and merged as PR 10. Gate then: Python 166 passed, .NET 109 passed
- Open for Bader: the Generic floor of plot 2441, the 119 ramps with no corner on the road, the stale names of plots 2589 and 2594, the numbered questions in steps/history/round-8.md. The yard plot question is answered by the round 9 measurement

## Round 7 Revit, 2026-10-22, ramp corners, spot elevations, plot numbers, link names

Stub. The full entry is in steps/history/round-7-revit.md.
- Floor corners and cornersSource, slope, spot elevations, plot numbers, the link name parse and the link contents default were added and merged as PR 9. The add-in ran in Revit on 22 Oct 23:12 UTC and the round 8 entry says what it showed. Gate then: Python 135 passed, .NET 105 passed
- The questions of that round are answered or moved: Prototype-FFL 298 lower (round 8), the 337 yard lines (R50). Still open: the stale names of plots 2589 and 2594, the sketch plane at 150 m

## Round 7 road, 2026-10-22, LandXML road surfaces and the ramp level test

Stub. The full entry is in steps/history/round-7-road.md.
- LandXML reader, surface lookup, road config, ramp road levels, gap bands and the road check were added and merged as PR 8. 7 surfaces, 337 ramps tested, only Top Surface-02 covers any corner (119 ramps with no covered corner). Gate then: Python 127 passed, .NET 77 passed
- Open for Bader: the finished road surface, the 119 ramps with no covered corner, the plot check, the numbered questions that are in steps/history/round-7-road.md

## Round 6, 2026-10-22, plot number shift, second source, no type check

Stub. The full entry is in steps/history/round-6.md.
- Shift config IMC 10 and IMD 11, Master Plan skip list, second source check, workbook survey and face reasons were added and merged as PR 7. Location check 734 of 746 joined plots pass, 12 fail. Gate then: Python 96 passed, .NET 77 passed
- Open for Bader: the 12 failing plots, the 17 many label faces, the numbered questions that are in steps/history/round-6.md

## Round 5, 2026-10-22, Revit link names, shared parameters and floors

Stub. The full entry is in steps/history/round-5-revit.md.
- Link names, shared parameters, floors and link_summary.py were added and merged as PR 6. The Revit API code has never run in Revit, so nothing new is confirmed until Bader runs it. Gate then: Python 70 passed, .NET 77 passed
- Open for Bader: rebuild and run the add-in, send prototypes.json, check three link names, Prototype-FFL units, foundation slabs, nested links

## Round 4, 2026-10-22, explode, label join, PDF rows, full run

Stub. The full entry is in steps/history/round-4-cad.md.
- The main guard, explode, the layer filter, texts and xrefs, plot labels, PDF rows, the link table, the full run and the harness files (R18 to R26) were added and merged as PR 5. Gate then: Python 60 passed, .NET 63 passed
- Its numbered questions are in the full entry, from before the fixed IDs of Round 17

## Round 4, 2026-10-22, Revit links, walls and ramps

Stub. The full entry is in steps/history/round-4-revit.md.
- Revit links, walls and ramps in the reader (R16, R17) were added and merged as PR 4. Gate then: Python 24 passed, .NET 40 passed
- Its numbered questions are in the full entry, from before the fixed IDs of Round 17

## Round 3, 2026-10-22, fixes against the real files

Detail: [steps/history/round-3.md](history/round-3.md).
Excel header row and stop rules fixed, El @ Entry and Height kept at 4 decimals. CAD plot geometry read from MP_PLOT_LINE (777 closed shapes, 28 open pieces). One more PDF sheet read and the layout held on all 12. Gate at the end of the round was Python 24 passed and .NET 28 passed.

## Round 2, 2026-10-22, readers run on real ROSHN Makkah files

Detail: [steps/history/round-2.md](history/round-2.md).
First run of the four readers on the real files. Excel, PDF and CAD ran, Revit did not. The list of mismatches found there drove round 3. Gate at the end of the round was Python 18 passed and .NET 22 passed.

## Round 1, 2026-10-22, read-only readers

Stub. The full entry is in steps/history/round-1.md.
- The read-only readers of Revit, CAD, PDF and Excel with R1 to R15 were added and merged as PR 1. Gate then: Python 18 passed, .NET 22 passed
- Its numbered questions are in the full entry, from before the fixed IDs of Round 17
