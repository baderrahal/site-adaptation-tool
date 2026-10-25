STATE 2026-10-26, Round ADDIN Part 1, Phase 3 part 2 in review (docs/addin-design): docs/addin-design.md, the design before any add-in code (R343), and docs/licences.md (R342). Phases 0a to 3c merged (PR 41 to 48). Round 17 done and merged (PR 39, 40)

# Progress

Live state of the tool. At most 60 lines. The round detail is in steps/log.md, steps/history/ and steps/investigations/.

## State

- ADDIN Part 1 (R300 to R399): the whole add-in inside Revit 2024 in C#, .NET Framework 4.8 (option B), parsons2024Rev6 for every new build (Q050), the 7 Rev 6 Parsons site models as answer keys, the approved mock in docs/addin-mock.md. Plan: steps/investigations/round-addin.md, design: docs/addin-design.md. Part 2, the sheets, waits for Bader's check of Part 1
- ADDIN 3b (R342, R343): docs/addin-design.md holds Option B and the two parts, the folders (Common on netstandard2.0 as the logic, CadReader.exe as the worker beside Revit, SiteAdaptation.AddIn, one test project on net48 and net8.0), the 8 buttons, the data path, the 14 steps Phase 4 moves to C# with their parity tests, the settings in Extensible Storage, the mock item by item filled with NBH 012, install, risks, R345 to R384 planned. docs/licences.md lists every library with its licence, load check and what Revit 2024 ships
- ADDIN 3c (R339 to R341): the levels of the keys to the mm as a build gives them, a build and its proof by them, the second method by an own LandXML route. 3a (R330 to R338): overrides by job and model, keyRules of parsons2024Rev6 written by python/key_rules_block.py, python/build_proof.py
- ADDIN 2a, 2b (R320 to R328): the 7 key reads and plans, the join by area share, past_all.csv by source and kind, no entrance spot gives El @ Entry. ADDIN 1, 1b (R310 to R313): window samples, site facts, key copies, the 13 STR prototypes. IMA and IMB are SE models with no level, ramp or yard (Q066)
- ADDIN 0b (R300 to R304): villa blocks only from the Building Footprint, area codes by job, the image step from testHost, R303 withdrawn (Q064). 17 (R261 to R282): the majority rule and the IMC rebuild. DATA-5 and earlier: steps/investigations/ and steps/history/
- Gate of 3b: in the PR body with its head. Real files are read from copies, nothing real is in git

## Done

- ADDIN 3b in review: R342, R343, docs/addin-design.md and docs/licences.md. In a net48 host holding what Revit 2024 holds, PdfPig failed on System.Memory 4.0.2.0 with no resolver and ran with one, ACadSharp, OpenXml 3.5.1 and Newtonsoft ran, CadReader took 2,576 MiB on the Road Layout DWG, the parsons build of NBH 012 wrote 336 of 337 plots, 1,247 check rows (RAN)
- ADDIN 3c, PR 48: R339 to R341, at 0.5 mm El @ Entry 0.088 and FFL per piece 0.749 weak, steps 0.988 and Prototype-FFL 0.994 strong, 0 unexplained levels on every key, overrides 12,424 and 154 in cannot. 3a, PR 47: R330 to R338. 2b, PR 46. 2a, PR 45
- ADDIN 1b, PR 44, 1, PR 43, 0b, PR 42 and 0a, PR 41: the harness, key copies, STR upgrades, Q011, Q028, Q035, Q040, Q044 to Q050, Q060, Q061, Q067 closed. 17: PR 39, 40. DATA-5: PR 38. DATA-4: PR 36. 16: PR 34. DATA-3: PR 32. Z4: PR 30. DATA-2: PR 28. DATA: PR 26. 14c to 1: PRs 1 to 25

## Open bugs

- The whole NBH 03 area runs past the 30 minute image step, I25 did not respond at 10,732 MiB (10.48 GiB) private memory (Q063). R310 logs free memory in every window, no stop at the floor. In 2a free memory fell to 0.53 to 1.42 GiB in each read window, the cause is UNKNOWN. Revit starts 9 to 11 child processes in a read (RC 11)
- rvt_info.py (R203) does not see cloud links, the SE models list 1 of their 14 Revit links. The 1,409 spots of the SE models give no value, their STR links are not loaded in a read. key_copies.py keeps a last part NUL, nul. or "NUL. " as a name where Windows opens the NUL device, reachable only by answerKeys.folder, and PrototypeSets.ps1 throws on it
- A read is tied to its key by the name <copy stem>_TEST-<job>, the zone2 master and the IMC 2024 copy share one stem, only a hash tie parts them (Q065 removes the cause)
- The ELl@Entry parameter is off its schedule by over 30 mm on 100 IMC and 115 IMD plots (Q070), no entrance spot gives El @ Entry (R327). Civil 3D ends with 0xC0000005 or 0xC0000409 after C3DQUIT. The top level angleToTrueNorthDeg of the reader has 3 decimals and is not brought into 0 to 360, the placer takes 0 only. A native2022 build with --overrides is refused on 1,346 native entries with no value (Q075)

## Bader's turn

Q001. El @ Entry: ask the client for the Infrastructure IFC and the architect for the ramp slope, no entrance spot gives it (2b) and the road a build gives holds it on 0.088 of 1,785 TU plots at 0.5 mm, 0.442 at 5 mm (3c), 1,596 overrides until then, 32 with no value listed in cannot
Q002. Ramp type: by Rev 9 code (0.988 on 5 keys), the 21 Flat Ramps are the plots with a placeholder ELl@Entry, overrides: accept. Q003. Villa mirror: block mirror flag with the side road (0.960), one plot ahead of the open end: accept
Q004. Setback: DLUS front. Q005. Front level: road snapped. Q006. Back corners parallel. Q007. Generic floor: whole face (0.991 on 7 keys)
Q008. Units: ask for a baseplan with 01b. Q009. Typology_Rev9 for codes with no schedule. Q010. Sales Land Width
Q012. FFL split by units, the plurality per sequence: DP1 TH1 TH1 DP1 (M07) middle 17 of 37 and DP1 TH1-IM TH1-IM DP1 3 of 6 weak, DP1 TH3 TH3 DP1 middle 6 of 6 and DP3 TH3 TH3 DP3 none 33 of 35 strong: accept, ask why IMF and IMG split at an end or not (15 of 36). Q013. CASE second letter as built (0.275, 1,814 overrides). Q015. Yard 0.100 under the floor top, none in Zone 4, Zone 1 taken from Zone 2 and 3 until Q066: accept. Q016. Plots 2311, 2312 by baseplan. Q017. IMC 2301, 2302 and IMG 544: VD4 by Rev 9, the keys link DP4 on these 3 of 2,501 plots, a build links VD4 with no override, the VD4 hand of 2302 differs from its key: accept.
Q018. 58 past ramps listed. Q019. Schedule FFL. Q020. Keep the north stop. Q021. Parallel windows over 8 GB free. Q022. TEST copies in Zone 02\ARC: delete when done. Q023. Delete the empty 15a folder. Q024. Curb and ROW from the R94 edge. Q025. 13 PDF refusals listed
Q026. Two curbs of one drawing: keep the Roads curb, ask which is current. Q027. Two DP4 labels: ask for a split. Q029. Location plan number per face. Q030. Ask for the 01b sales rows
Q031. Rev 9 faces, 97 differ: keep the baseplan, ask which is current. Q032. 1.4 FFL parameter as the design level. Q033. Lone DT3 on a VL3 face: hand rule of Q042. Q036. TBD for DLUS, accommodation for sales
Q037. Corner fronts by the Orientation side (0.978 on 7 keys): confirm. Q038. Native ramp fronts 0.698 to 0.700 m over the subgrade: confirm, the IMC schedule stays the level key. Q039. Footprint test: done as the edge only third check
Q041. Rows 2290 to 2299, the 4 middle DT1 as overrides: accept. Q042. Lone duplex hand: row pairs from the right end (382 of 382): accept
Q051. Rev 01 values: send the Rev 01 sections on steps, base heights and ramps. Q052. Weak rules hold on the keys through their overrides, as the majority rule says, by the rule alone El @ Entry 157 of 1,753, FFL 217 of 1,759, ramp front 1,283 of 1,759 (3c): accept until Q001 and Q051
Q053. 12,424 overrides and 154 misses with no value in cannot, native 5,222 kept unless a run names --replace native, a build applies parsons only unless it names native (R330), mark a hand edit "byHand": true: keep. Q054. R260 of DATA-5 and R261 to R282: keep both. Q055. 1.4 ELl@Entry typed off its ramp on 5 plots: read El @ Entry off the ramp
Q056. Native models: hands, fronts and ramps only, never unit codes. Q057. Ramp front fall with no direction (0.764), not applied, a front corner off over 0.5 mm on 1,665 of 1,759 plots, over 5 mm on 257: accept. Q058. Old 20221206 prototypes: send the folder to check their hands
Q059. NBH 03 build on native2022 with only the 1.2 entry and front overrides: accept. Q062. Rev 9 attribute values of the probe: keep them
Q063. Whole NBH 03 image past 30 minutes: accept the quarter images and make large images in quarters, the cause stays a known bug. Q064. Watcher through the job, withdrawn: keep it stopping at the first log step
Q065. zone2 IMC file, the floor scenario master: replace it by a copy of the Zone 02\ARC original. Q066. No Parsons TU model of Zone 1: send the IMA and IMB TU models, else accept the Native stand-in. Until then the Zone 1 yard is the Zone 2 and 3 value, and by the ARC files a build links 6 SE hands differ, overrides since the last pass
Q068. Project position of button 1: set the Parsons position from config, not R211. Q069. A remade key copy: file it by the hashes the test host logs, by hand until then
Q070. El @ Entry with no schedule: take the ELl@Entry parameter of the key, it is off the IMC schedule by over 30 mm on 100 of 314 plots, off the IMD schedule on 115 of 398, and IME to IMG set 113 FFL groups over 5 mm off it, 107 by whole cm (IME 28, IMF 67, IMG 12, heightM overrides), ask Parsons for the IME to IMG schedules
Q071. Walls: accept the base at the yard level, the centre on the edge and the open front, ask the architect for the wall top rule (0.346), wall overrides once button 7 places walls
Q072. Side road of a villa with three road edges (IMC 2301, IME 2710, IMG 441): the side edge, not the back: accept. Q073. 33 plots with a front corner on no road surface have no El @ Entry and are not written (IMA 1, IMB 2, IMD 3, IME 3, IMF 14, IMG 10): take the road of the covered corner for both, else send the surface. Q074. Steps of the base and risers, half a riser no step (0.988 on 5 keys), against the count rule the IMC and IMD schedules follow (0.934 on 5): accept, 48 overrides, 16 that break it, 32 with no value of it (no road level or no front frame). Q075. 1,346 native entries with no value kept from Round 17, a native2022 build loading them is refused: move them to cannot by a run with --replace native
Q076. Python checker licences: shapely is BSD-3-Clause and its wheels bundle GEOS under LGPLv2.1, nothing of Python ships: keep it for the checker
Q077. The worker: buttons 2 and 3 read and build in CadReader.exe beside Revit, the Road Layout read alone took 2,576 MiB and PdfPig needs a System.Memory Revit 2024 lacks: accept

## Next three steps

1. ADDIN Phase 4a: Common on netstandard2.0, the worker, the parity harness (R345 to R347), then 4b to 4f, the logic in C# with a parity test per step
2. ADDIN Phase 5: the add-in, the ribbon, the pane, the settings and the 8 buttons (R370 to R384)
3. ADDIN Phase 6: all 7 zones from a new project through the test host, the mock check by screenshots
