STATE 2026-10-25, Round ADDIN Part 1, Phase 3 part 1 in review (rules/addin-parsons-rules, PR 47): the source of every override (R330), the rules of Round 17 measured again on the 7 Parsons keys (R331 to R335), their second method (R336), and since the review a parsons2024Rev6 build by keyRules (R337). Phases 0a to 2b merged (PR 41 to 46). Round 17 done and merged (PR 39, 40)

# Progress

Live state of the tool. At most 60 lines. The round detail is in steps/log.md, steps/history/ and steps/investigations/.

## State

- ADDIN Part 1 (R300 to R399): the whole add-in inside Revit 2024 in C#, .NET Framework 4.8 (option B), parsons2024Rev6 for every new build (Q050), the 7 Rev 6 Parsons site models as answer keys, the approved mock in docs/addin-mock.md. Plan: steps/investigations/round-addin.md. Part 2, the sheets, waits for Bader's check of Part 1
- ADDIN 3a (R330 to R337): every entry of config/overrides names the job of its key and its model, a run keeps majority.overrideKeep, a build applies parsons only unless it names native (python/override_source.py). python/key_rules.py holds the measures majority.py runs on the site as a build sees it, python/key_rules_check.py checks them a second way with its own fits. keyRules of levelProfiles.parsons2024Rev6 holds the coded rules, a parsons build takes them (R337), the levels wait for 3c
- ADDIN 2b (R325 to R328): the plans of the 7 keys, the join by area share, past_all.csv by source and kind, the entrance spots, second method. NBH 021, 022, 012, 013, 02, 03 and 01A, no entrance spot gives El @ Entry on the 5 TU keys
- ADDIN 2a (R320 to R323): the spot value, FFL as the Generic floor of IME and IMF, the counts of each key read and their second method. IMA and IMB share 733 Generic floors
- ADDIN 1 and 1b (R310 to R313, R203, R204, R301): window samples, site facts, key copies by manifest, the 13 STR prototypes in Revit 2024. IMA and IMB are SE models with STR links and no level, ramp or yard floor (Q066), the zone2 IMC is the floor scenario master (Q065)
- ADDIN 0b (R300 to R304): parsons2024Rev6 default with overrides of its own job, villa blocks only from the Building Footprint, area codes by job, the image step from testHost, R303 withdrawn (Q064)
- 17 (R261 to R282): Native 1.1 to 1.4 hold 2,386 of 2,591 Phase 1 unit plots, the majority rule and the IMC rebuild. DATA-5 and earlier: steps/investigations/ and steps/history/
- Gate of 3a: in the PR body with its head. Real files are read from copies, nothing real is in git

## Done

- ADDIN 3a in review: R330 to R337, four changes in 2d56f89 to bf9941d and the review in 251c74d to 2d27705. Override list 5,709 to 9,336 (parsons 487 to 4,114, native 5,222 kept cell for cell). Strong on the Parsons keys: rotation 0.991, corner front 0.978, origin 0.971, hands 0.960, 0.981, 1.0, ramp type 0.988, ramp outline 0.969, generic floor 0.991, yard 1.0 per zone, yard outline 0.963, wall base 0.983, wall line 0.921, open front 0.988, the plot, code, steps, façade, HVAC and Name parameters. Weak: ramp front 0.808 and fall 0.764, wall top 0.346, foundation type 0.620, CASE 0.275, Prototype-FFL 0.602 for 3c. Second method: 0 mismatches at seeds 14 and 7, 3 over every plot, the villas of Q072 (first push 0, 0, 6). A build by the probe route: 0 unexplained differences on every key, 140 hands before. Q040 closed
- ADDIN 2b, PR 46: R325 to R328, 7 plans, past_all.csv 4,896 rows, the join 2,501 of 2,591 link plots, Q011 closed. ADDIN 2a, PR 45: R320 to R323, 7 reads under the lock
- ADDIN 1b, PR 44: R312, R313 and the R301 manifest, the 13 STR upgrades. ADDIN 1, PR 43: R310, R311, R203, R204, R301, 17 windows and KG3
- ADDIN 0b, PR 42 and 0a, PR 41: the harness and the open fixes, Q028, Q035, Q044 to Q050, Q060, Q061 closed. 17: PR 39, 40. DATA-5: PR 38. DATA-4: PR 36. 16: PR 34. DATA-3: PR 32. Z4: PR 30. DATA-2: PR 28. DATA: PR 26. 14c to 1: PRs 1 to 25

## Open bugs

- The whole NBH 03 area runs past the 30 minute image step, I25 did not respond at 10,732 MiB (10.48 GiB) private memory (Q063). R310 logs free memory in every window, no stop at the floor. In 2a free memory fell to 0.53 to 1.42 GiB in each read window, the cause is UNKNOWN. Revit starts 9 to 11 child processes in a read (RC 11)
- rvt_info.py (R203) does not see cloud links, the SE models list 1 of their 14 Revit links. The 1,409 spots of the SE models give no value, their STR links are not loaded in a read. key_copies.py keeps a last part NUL, nul. or "NUL. " as a name where Windows opens the NUL device, reachable only by answerKeys.folder, and PrototypeSets.ps1 throws on it
- A read is tied to its key by the name <copy stem>_TEST-<job>, the zone2 master and the IMC 2024 copy share one stem, only a hash tie parts them (Q065 removes the cause)
- The ELl@Entry parameter is off its schedule by over 30 mm on 100 IMC and 115 IMD plots (Q070), no entrance spot gives El @ Entry (R327). Civil 3D ends with 0xC0000005 or 0xC0000409 after C3DQUIT. The top level angleToTrueNorthDeg of the reader has 3 decimals and is not brought into 0 to 360, the placer takes 0 only

## Bader's turn

Q001. El @ Entry: ask the client for the Infrastructure IFC and the architect for the ramp slope, no entrance spot gives it on the 5 TU keys (2b), 230 IMC overrides until then
Q002. Ramp type: by Rev 9 code (0.988 on 5 keys), the 21 Flat Ramps are the plots with a placeholder ELl@Entry, overrides: accept. Q003. Villa mirror: block mirror flag with the side road (0.960), one plot ahead of the open end: accept
Q004. Setback: DLUS front. Q005. Front level: road snapped. Q006. Back corners parallel. Q007. Generic floor: whole face (0.991 on 7 keys)
Q008. Units: ask for a baseplan with 01b. Q009. Typology_Rev9 for codes with no schedule. Q010. Sales Land Width
Q012. FFL split: ask why. Q013. CASE second letter as built (0.275, 1,814 overrides). Q015. Yard 0.100 under the floor top, none in Zone 4, Zone 1 taken from Zone 2 and 3 until Q066: accept. Q016. Plots 2311, 2312 by baseplan. Q017. Plots 2301, 2302 VD4.
Q018. 58 past ramps listed. Q019. Schedule FFL. Q020. Keep the north stop
Q021. Parallel windows over 8 GB free. Q022. TEST copies in Zone 02\ARC: delete when done. Q023. Delete the empty 15a folder. Q024. Curb and ROW from the R94 edge. Q025. 13 PDF refusals listed
Q026. Two curbs of one drawing: keep the Roads curb, ask which is current. Q027. Two DP4 labels: ask for a split. Q029. Location plan number per face. Q030. Ask for the 01b sales rows
Q031. Rev 9 faces, 97 differ: keep the baseplan, ask which is current. Q032. 1.4 FFL parameter as the design level. Q033. Lone DT3 on a VL3 face: hand rule of Q042. Q036. TBD for DLUS, accommodation for sales
Q037. Corner fronts by the Orientation side (0.978 on 7 keys): confirm. Q038. Native ramp fronts 0.698 to 0.700 m over the subgrade: confirm, the IMC schedule stays the level key. Q039. Footprint test: done as the edge only third check
Q041. Rows 2290 to 2299, the 4 middle DT1 as overrides: accept. Q042. Lone duplex hand: row pairs from the right end (382 of 382): accept
Q051. Rev 01 values: send the Rev 01 sections on steps, base heights and ramps. Q052. Weak rules with their overrides: accept until Q001 and Q051
Q053. 9,336 overrides, native 5,222 kept unless a run names --replace native, a build applies parsons only unless it names native (R330), mark a hand edit "byHand": true: keep. Q054. R260 of DATA-5 and R261 to R282: keep both. Q055. 1.4 ELl@Entry typed off its ramp on 5 plots: read El @ Entry off the ramp
Q056. Native models: hands, fronts and ramps only, never unit codes. Q057. Ramp front fall with no direction (0.764), not applied: accept. Q058. Old 20221206 prototypes: send the folder to check their hands
Q059. NBH 03 build on native2022 with only the 1.2 entry and front overrides: accept. Q062. Rev 9 attribute values of the probe: keep them
Q063. Whole NBH 03 image past 30 minutes: accept the quarter images and make large images in quarters, the cause stays a known bug. Q064. Watcher through the job, withdrawn: keep it stopping at the first log step
Q065. zone2 IMC file, the floor scenario master: replace it by a copy of the Zone 02\ARC original. Q066. No Parsons TU model of Zone 1: send the IMA and IMB TU models, else accept the Native stand-in. Until then the Zone 1 yard is the Zone 2 and 3 value, and by the ARC files a build links 6 SE hands differ
Q068. Project position of button 1: set the Parsons position from config, not R211. Q069. A remade key copy: file it by the hashes the test host logs, by hand until then
Q070. El @ Entry with no schedule: take the ELl@Entry parameter of the key, it is off the IMC schedule by over 30 mm on 100 of 314 plots and 0 on 10 more, off the IMD schedule by over 30 mm on 115 of 398, and ask Parsons for the IME to IMG schedules
Q071. Walls: accept the base at the yard level, the centre on the edge and the open front, ask the architect for the wall top rule (0.346), wall overrides once button 7 places walls
Q072. Side road of a villa with three road edges (IMC 2301, IME 2710, IMG 441): the side edge, not the back: accept

## Next three steps

1. ADDIN Phase 3c: the levels to the mm under parsons2024Rev6, El @ Entry, base height, FFL per cluster, the M07 and M08 split, steps, SSL and Prototype-FFL, the share at 1 mm and 5 mm
2. ADDIN Phase 3b: docs/addin-design.md before any add-in code, keyRules of 3a and 3c as its rule source
3. ADDIN Phase 4: the logic in C# with a parity test per step
