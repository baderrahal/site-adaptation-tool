STATE 2026-10-25, Round ADDIN Part 1, Phase 0: PR 0a merged (PR 41), PR 0b (PR 42, fix/addin-open-fixes) holds the open fixes of Q035, Q044, Q045, Q047 to Q050, Q060 and Q061 and the fixes of three breaker and claim-checker passes. Round 17 done and merged (PR 39, 40)

# Progress

Live state of the tool. At most 60 lines. The round detail is in steps/log.md, steps/history/ and steps/investigations/.

## State

- ADDIN Part 1 (R300 to R399): the whole add-in inside Revit 2024 in C#, .NET Framework 4.8 (option B), parsons2024Rev6 for every new build (Q050), the 7 Rev 6 Parsons site models as answer keys, the approved mock in docs/addin-mock.md. Plan: steps/investigations/round-addin.md. Part 2, the sheets, waits for Bader's check of Part 1
- ADDIN 0b (R300 to R304): levelProfiles.default parsons2024Rev6 with overrides of its own job only (R277, R280), villa blocks only from the Building Footprint with R06_0000 and R06_0001 reference only by stem (R300), config/area_codes.json one table per job, every row measured (R301), the image step and its margin from testHost (R302), the watcher through the job withdrawn after two attempts, the watcher stops at the first log step (R303, Q064), a clean clone in a deep folder builds (R304). Kept files of Z4, 16 and 17 in Documents\SiteAdaptation\kept
- 17 (R261 to R282): Native 1.1 to 1.4 hold 2,386 of 2,591 Phase 1 unit plots, Parsons IMC (2024-03) is newer than Native (2022-12). Strong rules: ramp class by code, hands by block mirror and open end and cluster side, corner front by Orientation, Parsons FFL one per cluster, steps by Rev 6. Weak: El @ Entry and the Native levels. New IMC file 336 of 337 plots, Z within 10 mm on 336. Summary: steps/investigations/round-17.md
- DATA-5 (R251 to R260): 97 Rev 9 plots differ for real, the area key per plot, the IFC in Downloads is another project, the coverage table. Summary: steps/investigations/data-book.md
- DATA-4, 16 (R220 to R250): the Rev 9 parcels, VL6 hand, the footprint test, the corner front, 205 IMC plots from CAD
- DATA-3, Z4, DATA-2, DATA (R160 to R218): the data book of Phase 1, a new site file for DT3 of NBH 022
- 14c, C3D, 14, 15a, 15b (R120 to R159): links and floors from CAD and the placer. Rounds 1 to 13: R1 to R101
- Gate: Python 1097 passed and 1 skipped, .NET 433 passed, a clean clone builds. Real files are read from copies, nothing real is in git

## Done

- ADDIN 0b, PR 42: R277 default, R280 overrides by job, R300 to R302, R304, R303 withdrawn, the NBH 03 image in four quarters, I25 measured, the IFC copy and the two Cutsheet lock files deleted, 21 kept files moved to kept and the three work folders deleted, data5_check runs to its end again, the old branch deleted on origin and here, the watcher of main proven again on the quarter I29, 40 of 40 originals equal. Q035, Q044, Q045, Q047, Q048, Q049, Q060, Q061 closed
- ADDIN 0a, PR 41: the AI MAX skill version 3, seven agents, six old log entries moved to steps/history/, CLAUDE.md, docs/addin-mock.md, Q028, Q046 and Q050 closed
- 17: R261 to R282, PR 39. DATA-5: R251 to R260, PR 38. DATA-4: PR 36. 16: PR 34. DATA-3: PR 32. Z4: PR 30. DATA-2: PR 28. DATA: PR 26. 14c: PR 24, 25. C3D: PR 22, 23. 14: PR 20, 21. 1 to 15b: PRs 1 to 19

## Open bugs

- Civil 3D ends with 0xC0000005 or 0xC0000409 after C3DQUIT. CadReader reads model space only. Not read: 154 alignments of Road Surface and some parcels. The add-in does not need the 154 alignments (Q046)
- No El @ Entry rule beyond 0.285 of the IMC key, the rest are overrides. CASE follows no tested logic. The footprint test cannot see a wrong hand
- The whole NBH 03 area runs past the 30 minute image step: in I25 Revit did not respond with 10.7 GB private memory and no dialog in one Win32 list, a quarter rises to 6.4 GB by the same measure (Q063). A quarter took free memory to 0.55 GB in I29, the 2 GB floor of R200 is checked at the lock only. Run-TestHost.ps1 prints a peak working set under 2 GB while the samplers saw 4.9 GB (I25) and 5.7 GB (I28), why is UNKNOWN
- siteBuild.folder still names round_17, whose work folder is gone, siteBuild.newFile is null and the IFC input is retired by Q047. Phase 1 sets siteBuild again

## Bader's turn

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

## Next three steps

1. ADDIN Phase 1: the 7 Parsons models hashed, BasicFileInfo, Revit 2024 copies, their links and coordinates, the limits measured
2. ADDIN Phase 2: the 7 keys read into plan files and past_all.csv, the Parsons table of config/area_codes.json
3. ADDIN Phase 3: the Round 17 rules measured on the 7 Parsons keys, then docs/addin-design.md before any add-in code
