STATE 2026-10-25, Round ADDIN Part 1, Phase 2 part 2 in review (data/addin-key-plans): the plans of the 7 keys (R325), the join, the place check, the area codes and past_all.csv by source (R326), the spots at the plot entrance (R327) and their second method (R328). Phases 0a to 2a merged (PR 41 to 45). Round 17 done and merged (PR 39, 40)

# Progress

Live state of the tool. At most 60 lines. The round detail is in steps/log.md, steps/history/ and steps/investigations/.

## State

- ADDIN Part 1 (R300 to R399): the whole add-in inside Revit 2024 in C#, .NET Framework 4.8 (option B), parsons2024Rev6 for every new build (Q050), the 7 Rev 6 Parsons site models as answer keys, the approved mock in docs/addin-mock.md. Plan: steps/investigations/round-addin.md. Part 2, the sheets, waits for Bader's check of Part 1
- ADDIN 2b (R325 to R328): plan_from_past.py --answer-key --names --key maps a Parsons link by its plot number and keeps the key levels and walls (R325), python/key_join.py joins every key plot to its face, number, zone and NBH and checks where each link, generic floor and ramp stands, past_all.csv has a source column (R326), python/entry_spots.py holds the spots at the entrance against El @ Entry (R327), python/key_plans_check.py checks it all a second way (R328). The 7 keys are NBH 021, 022, 012, 013, 02, 03 and 01A, no entrance spot gives El @ Entry
- ADDIN 2a (R320 to R323): the reader writes the Single/Upper Value of a spot, its type and elevation base (R320), floors.keyTypes reads FFL as the Generic floor of IME and IMF (R321), python/key_reads.py counts each key read (R322) and python/key_reads_check.py checks it by the categories of R311, the bytes of the copy, the floor top under each spot and the per plot rule, the Phase 1 read by id is a repeat read (R323). The 7 reads are in round_addin\reads, every count equal to Phase 1. IMA and IMB share 733 Generic floors
- ADDIN 1b (R312, R313, R301): python/key_copies.py reuses a key copy only while the manifest key_copies.json holds its code, source, hashes and format of today, record withdrawn (Q069). The 13 STR prototypes are Revit 2024 files in Prototypes_2024\STR (R313)
- ADDIN 1 (R310, R311, R203, R204, R301): window samples with the true peaks, site facts of the reader. All 7 Parsons models share one survey point, base point, project position, angle 0, site Internal, Level 1 and Level 2, metric units. IMA and IMB are SE models with STR links, Level 1 and Level 2 but no level, ramp or yard floor (Q066), the zone2 IMC is the master of the floor scenarios (Q065)
- ADDIN 0b (R300 to R304): parsons2024Rev6 default with overrides of its own job, villa blocks only from the Building Footprint, area codes by job, the image step from testHost, R303 withdrawn (Q064), a clean clone in a deep folder builds
- 17 (R261 to R282): Native 1.1 to 1.4 hold 2,386 of 2,591 Phase 1 unit plots, the majority rule and the IMC rebuild, 336 of 337 plots, Z within 10 mm on 336. DATA-5 and earlier: steps/investigations/ and steps/history/
- Gate of 2b: in the PR body with its head. Real files are read from copies, nothing real is in git

## Done

- ADDIN 2b: R325 to R328 with their tests, 7 plans in Documents\SiteAdaptation (plan_imc_key_past.json, plan_imc_past.json kept), the new IMC plan equal to plan_imc_past.json on 335 of 335 plots but El @ Entry and SSL, past_all.csv 4,949 rows (native 2,395 cell for cell, parsons 2,554), the join 2,556 of 2,591 link plots, area codes of the 7 keys, Q011 closed, second method all equal but 2 IMG ramps on a face edge
- ADDIN 2a, PR 45: R320 to R323 with their tests, the test host installed from 7b82696, 7 reads one window at a time under the lock, IMC first, 60 of 60 files equal before and after, second method 28 of 28 category counts, the spot value against its floor top 5,445 of 5,455 TU spots within 1 mm, the per plot rule on 1,560 of 1,561 IMC to IMF plots, IMG 0 of 297 for no yard, IMA and IMB 0 of 733 for no yard and no ramp, the review fixes 2d415ec to 83b3713, the second breaker pass f8e54df to 5e090e1: a read is of a key only as <copy name>_TEST-<job>, a spot outside its floor apart, noHost, a null worst gap, no repeat of itself
- ADDIN 1b, PR 44: R312, R313 and the R301 manifest, the 13 STR upgrades. ADDIN 1, PR 43: R310, R311, R203, R204, R301, 17 windows and KG3
- ADDIN 0b, PR 42 and 0a, PR 41: the harness and the open fixes, Q028, Q035, Q044 to Q050, Q060, Q061 closed. 17: PR 39, 40. DATA-5: PR 38. DATA-4: PR 36. 16: PR 34. DATA-3: PR 32. Z4: PR 30. DATA-2: PR 28. DATA: PR 26. 14c to 1: PRs 1 to 25

## Open bugs

- The whole NBH 03 area runs past the 30 minute image step, I25 did not respond at 10,732 MiB (10.48 GiB) private memory (Q063). R310 logs free memory in every window, no stop at the floor. In 2a free memory fell to 0.53 to 1.42 GiB in each read window from 2.10 to 2.94 GiB at the lock, the cause is UNKNOWN. Revit starts 9 to 11 child processes in a read (RC 11)
- rvt_info.py (R203) does not see cloud links, the SE models list 1 of their 14 Revit links. The 1,409 spots of the SE models give no value, their STR links are not loaded in a read. key_copies.py keeps a last part NUL, nul. or "NUL. " as a name where Windows opens the NUL device, reachable only by answerKeys.folder, and PrototypeSets.ps1 throws on it
- A read is tied to its key by the name <copy stem>_TEST-<job>, and the zone2 master and the IMC 2024 copy share one stem, so a read of a work copy of the master passes as IMC, only a hash tie would part them (Q065 removes the cause)
- No El @ Entry rule beyond 0.285 of the IMC key, the rest are overrides, and no spot at the plot entrance gives El @ Entry on the 7 keys (R327). CASE follows no tested logic. The footprint test cannot see a wrong hand. Civil 3D ends with 0xC0000005 or 0xC0000409 after C3DQUIT. The top level angleToTrueNorthDeg of the reader has 3 decimals and is not brought into 0 to 360, the placer takes 0 only and the 7 models are at 0, Phase 4 moves it to C#

## Bader's turn

Q001. El @ Entry: ask the client for the Infrastructure IFC and the architect for the ramp slope, no entrance spot gives it (2b), 241 IMC overrides until then
Q002. Ramp type: by Rev 9 code, 2% for C10, VL4, VL6, else 0.5%, the 10 Flat Ramps as overrides. Q003. Villa mirror: block mirror flag with the open end, accept
Q004. Setback: DLUS front. Q005. Front level: road snapped. Q006. Back corners parallel. Q007. Generic floor: whole face
Q008. Units: ask for a baseplan with 01b. Q009. Typology_Rev9 for codes with no schedule. Q010. Sales Land Width
Q012. FFL split: ask why. Q013. CASE second letter as built. Q015. Yard FFL minus 0.100. Q016. Plots 2311, 2312 by baseplan. Q017. Plots 2301, 2302 VD4.
Q018. 58 past ramps listed. Q019. Schedule FFL. Q020. Keep the north stop
Q021. Parallel windows over 8 GB free. Q022. TEST copies in Zone 02\ARC: delete when done. Q023. Delete the empty 15a folder. Q024. Curb and ROW from the R94 edge. Q025. 13 PDF refusals listed
Q026. Two curbs of one drawing: keep the Roads curb, ask which is current. Q027. Two DP4 labels: ask for a split. Q029. Location plan number per face. Q030. Ask for the 01b sales rows
Q031. Rev 9 faces, 97 differ: keep the baseplan, ask which is current. Q032. 1.4 FFL parameter as the design level. Q033. Lone DT3 on a VL3 face: hand rule of Q042. Q036. TBD for DLUS, accommodation for sales
Q037. Corner fronts by the Orientation side: confirm. Q038. Native ramp fronts 0.698 to 0.700 m over the subgrade: confirm, the IMC schedule stays the level key. Q039. Footprint test: done as the edge only third check. Q040. Footprint clusters
Q041. Rows 2290 to 2299, the 4 middle DT1 as overrides: accept. Q042. Lone duplex hand by block mirror and open end: accept
Q051. Rev 01 values: send the Rev 01 sections on steps, base heights and ramps. Q052. Weak rules with their overrides: accept until Q001 and Q051
Q053. 5,709 overrides, a build applies only its own source: keep. Q054. R260 of DATA-5 and R261 to R282: keep both. Q055. 1.4 ELl@Entry typed off its ramp on 5 plots: read El @ Entry off the ramp
Q056. Native models: hands, fronts and ramps only, never unit codes. Q057. Ramp front fall with no direction, not applied: accept. Q058. Old 20221206 prototypes: send the folder to check their hands
Q059. NBH 03 build on native2022 with only the 1.2 entry and front overrides: accept. Q062. Rev 9 attribute values of the probe: keep them
Q063. Whole NBH 03 image past 30 minutes: accept the quarter images and make large images in quarters, the cause stays a known bug. Q064. Watcher through the job, withdrawn: keep it stopping at the first log step
Q065. zone2 IMC file, the floor scenario master: replace it by a copy of the Zone 02\ARC original. Q066. No Parsons TU model of Zone 1: send the IMA and IMB TU models, else accept the Native stand-in
Q068. Project position of button 1: set the Parsons position from config, not R211. Q069. A remade key copy: file it by the hashes the test host logs, by hand until then
Q070. El @ Entry with no schedule: take the ELl@Entry parameter of the key, it is off the IMC schedule by over 30 mm on 114 of 335 plots, and ask Parsons for the IME to IMG schedules

## Next three steps

1. ADDIN Phase 3a: the Round 17 rules measured on the 7 Parsons keys from round_addin\past_all.csv, the Native share beside them, a source field on every override
2. ADDIN Phase 3b: docs/addin-design.md before any add-in code
3. ADDIN Phase 4: the logic in C# with a parity test per step
