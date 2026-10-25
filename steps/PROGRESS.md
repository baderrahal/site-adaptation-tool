STATE 2026-10-25, Round ADDIN Part 1 started, Phase 0: PR 0a on chore/addin-harness holds the skill, the agents, the records, CLAUDE.md and the approved mock. Round 17 done and merged (PR 39, 40)

# Progress

Live state of the tool. At most 60 lines. The round detail is in steps/log.md, steps/history/ and steps/investigations/.

## State

- ADDIN Part 1 (R300 to R399): the whole add-in inside Revit 2024 in C#, .NET Framework 4.8 (option B), parsons2024Rev6 for every new build (Q050), the 7 Rev 6 Parsons site models as answer keys, the approved mock in docs/addin-mock.md. Plan: steps/investigations/round-addin.md. Part 2, the sheets, waits for Bader's check of Part 1
- 17 (R261 to R282): Native 1.1 to 1.4 hold 2,386 of 2,591 Phase 1 unit plots, Parsons IMC (2024-03) is newer than Native (2022-12). Strong rules: ramp class by code, hands by block mirror and open end and cluster side, corner front by Orientation, Parsons FFL one per cluster, steps by Rev 6. Weak: El @ Entry and the Native levels. New IMC file 336 of 337 plots, Z within 10 mm on 336. Summary: steps/investigations/round-17.md
- DATA-5 (R251 to R260): 97 Rev 9 plots differ for real, the area key per plot, the IFC in Downloads is another project, the coverage table. Summary: steps/investigations/data-book.md
- DATA-4, 16 (R220 to R250): the Rev 9 parcels, VL6 hand, the footprint test, the corner front, 205 IMC plots from CAD
- DATA-3, Z4, DATA-2, DATA (R160 to R218): the data book of Phase 1, a new site file for DT3 of NBH 022
- 14c, C3D, 14, 15a, 15b (R120 to R159): links and floors from CAD and the placer. Rounds 1 to 13: R1 to R101
- Gate: Python 996 passed and 1 skipped, .NET 421 passed. Real files are read from copies, nothing real is in git

## Done

- ADDIN 0a: the AI MAX skill version 3 in .claude/skills/ai-max, seven agents in .claude/agents, six old entries of steps/log.md moved to steps/history/ (100,466 to 67,551 bytes), CLAUDE.md, docs/addin-mock.md, Q028, Q046 and Q050 closed
- 17: R261 to R282, PR 39. DATA-5: R251 to R260, PR 38. DATA-4: PR 36. 16: PR 34. DATA-3: PR 32. Z4: PR 30. DATA-2: PR 28. DATA: PR 26. 14c: PR 24, 25. C3D: PR 22, 23. 14: PR 20, 21. 1 to 15b: PRs 1 to 19

## Open bugs

- Civil 3D ends with 0xC0000005 or 0xC0000409 after C3DQUIT. CadReader reads model space only. Not read: 154 alignments of Road Surface and some parcels, the add-in does not need them (Q046)
- No El @ Entry rule beyond 0.285 of the IMC key, the rest are overrides. CASE follows no tested logic. The footprint test cannot see a wrong hand
- The image job of the 404 links of NBH 03 ran over the 10 minute image step three times, the file opens in 78 s with its links loaded, no image of it was made
- R277 in docs/rules.md still says no level profile is the default, Q050 makes parsons2024Rev6 the profile of every new build. The rule text changes with the code in a later ADDIN PR

## Bader's turn

Q001. El @ Entry rule: the IFC in Downloads is another project, ask the client for the Infrastructure IFC package and the architect for the ramp slope. The road plus the ramp slope holds 0.285 of the IMC key within 5 mm, the 241 others are overrides until then
Q002. Ramp type: coded by Rev 9 code (0.970), 2% for C10, VL4, VL6, 0.5% for the rest. The 10 Flat Ramps of the IMC key slope like the others, keep them as overrides. Q003. Villa mirror: coded, the block mirror flag with the open end (0.913 IMC, 0.945 Native): accept
Q004. Setback: DLUS front. Q005. Front level: road snapped. Q006. Back corners parallel. Q007. Generic floor: whole face (0.955 IMC)
Q008. Units: ask for a baseplan with 01b. Q009. Typology_Rev9 for codes with no schedule. Q010. Sales Land Width. Q011. IME: nothing names it, ask which neighbourhood each IM letter is (see Q044)
Q012. FFL split: ask why. Q013. CASE second letter as built (CASE follows no tested logic, 0.424). Q015. Yard FFL minus 0.100 (0.997). Q016. Plots 2311, 2312 by baseplan. Q017. Plots 2301, 2302 VD4. Q018. 58 past ramps listed. Q019. Schedule FFL. Q020. Keep the north stop
Q021. Parallel windows over 8 GB free. Q022. TEST copies in Zone 02\ARC: delete when done. Q023. Delete the empty 15a folder. Q024. Curb and ROW from the R94 edge. Q025. 13 PDF refusals listed
Q026. Two curbs from one 2021 drawing, they differ at junction corners and curb breaks: keep the Roads curb, ask which is current
Q027. Two DP4 labels: ask for a split. Q029. Location plan number per face. Q030. Ask for the 01b sales rows. Q031. Rev 9 faces: 97 differ, keep the baseplan, ask which is current
Q032. 1.4 FFL parameter as the design level. Q033. The lone DT3 on a VL3 face: now a unit alone with the hand rule of Q042. Q035. round_z4\work and round_16\work: delete next round. Q036. TBD for DLUS, accommodation for sales. Q037. Orientation side coded (909 of 940 corner plots): confirm
Q038. The Native ramp fronts sit 0.698 to 0.700 m over Total Top of Roads Subgrade (-0.70) in all four models, so the 2022 job used the road that surface was cut from: confirm, and keep the IMC schedule as the level key. Q039. Footprint test in every placer check: done as the edge only third check. Q040. Footprint clusters
Q041. Rows 2290 to 2299: the 4 middle DT1 are overrides of the IMC key: accept. Q042. Lone duplex hand: coded, block mirror flag with the open end (15 of 16): accept
Q044. config/area_codes.json: approve as names only, the area stays the Neighbourhood_ID, but correct its guessed rows: the Native Files show IMA to IMD name sub-phases 1.1 to 1.4, IMA holds 02 and 01A and IMB 03 and 01A, not 021 and 022. Q045. R06_0000 and R06_0001 were saved here on 2026-10-22: confirm their source
Q047. Delete the IFC copy next round. Q048. Delete the old Cutsheet lock files. Q049. Leave attribute values out of the probe
Q051. Rev 01 values are not in the repo, only its date for the foundations: send the Rev 01 sections on steps, base heights and ramps, to tell whether native2022 follows Rev 01
Q052. Weak rules, each coded with its overrides: El @ Entry, ramp front and fall, Native height (0.353), Native Generic floor (0.899), foundation type (0.685), CASE: accept until Q001 and Q051 are answered
Q053. The override list holds 5,709 overrides, 5,222 from the Native models, most from their weak level rules: keep all as the majority rule says, a build applies only its source
Q054. R260 is used by DATA-5 outside its range, Round 17 used R261 to R282: keep both as they are. Q055. The 1.4 ELl@Entry parameter is typed 0.41 to 1.59 m off its own ramp on 2461, 2470, 2473, 2481, 2557: read El @ Entry off the ramp there
Q056. The Native models use the 2022 layout with old client units, other than Rev 9 on most plots: use them for hands, fronts and ramps, never for unit codes. Q057. Ramp front fall: 97 IMC overrides list a fall with no direction, not applied: accept
Q058. Old prototype files of 20221206 - Links are not here, their hand comes from the CAD reference (V3 and V2 opposite to VL6 and VD2, the others the same): send the folder to check it on the files
Q059. NBH 03 build: native2022 levels, the 1.2 El @ Entry and front overrides, no mirror or height overrides (old files, other units): accept. Q060. round_17\work keeps the three 2024 key copies and the two new files: delete next round
Q061. The image of the NBH 03 file ran over the 10 minute image step three times, links on and off (it opens in 78 s with its 404 links, file B made its image in 91.5 s): raise the image step to 30 minutes next round, open the file by hand until then

## Next three steps

1. ADDIN 0b: Q045, Q044 by job, Q049 and the image step of Q061 in the repo, the clean up of Q047, Q048, Q035 and Q060, the old remote branch feat/revit-links-walls-ramps
2. ADDIN Phase 1: the 7 Parsons models hashed, BasicFileInfo, Revit 2024 copies, their links and coordinates, the limits measured
3. ADDIN Phase 2: the 7 keys read into plan files and past_all.csv, the Parsons table of config/area_codes.json
