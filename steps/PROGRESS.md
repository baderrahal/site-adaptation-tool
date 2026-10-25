STATE 2026-10-25, Round ADDIN Part 1, Phase 1 on PR data/addin-parsons-models: the 7 Parsons models hashed and read, their Revit 2024 copies in Revit All Zones4, the memory of every window sampled. Phases 0a and 0b merged (PR 41, 42). Round 17 done and merged (PR 39, 40)

# Progress

Live state of the tool. At most 60 lines. The round detail is in steps/log.md, steps/history/ and steps/investigations/.

## State

- ADDIN Part 1 (R300 to R399): the whole add-in inside Revit 2024 in C#, .NET Framework 4.8 (option B), parsons2024Rev6 for every new build (Q050), the 7 Rev 6 Parsons site models as answer keys, the approved mock in docs/addin-mock.md. Plan: steps/investigations/round-addin.md. Part 2, the sheets, waits for Bader's check of Part 1
- ADDIN 1 (R310, R311, R203, R204): R310 samples every test host window every 5 s with the true peaks to the exit, R311 the site facts of the reader, R203 BasicFileInfo of a BIM 360 model, R204 the source of a site upgrade from the round spec. The 7 copies in Revit All Zones4 with sources.txt. All 7 share one survey point, base point, project position, angle 0, site Internal, Level 1 and Level 2, metric units. IMA and IMB are SE models with STR links and no levels (Q066), the zone2 IMC is the master of the floor scenarios (Q065)
- ADDIN 0b (R300 to R304): parsons2024Rev6 default with overrides of its own job, villa blocks only from the Building Footprint, area codes by job, the image step from testHost, R303 withdrawn (Q064), a clean clone in a deep folder builds
- 17 (R261 to R282): Native 1.1 to 1.4 hold 2,386 of 2,591 Phase 1 unit plots, the majority rule and the IMC rebuild, 336 of 337 plots, Z within 10 mm on 336. DATA-5 and earlier: steps/investigations/ and steps/history/
- Gate: Python 1099 passed and 1 skipped, .NET 445 passed. Real files are read from copies, nothing real is in git

## Done

- ADDIN 1, PR data/addin-parsons-models: R310, R311, R203 and R204 with 14 tests, 17 windows under the lock, 40 of 40 originals equal before and after
- ADDIN 0b, PR 42 and 0a, PR 41: the harness and the open fixes, Q028, Q035, Q044 to Q050, Q060, Q061 closed
- 17: PR 39, 40. DATA-5: PR 38. DATA-4: PR 36. 16: PR 34. DATA-3: PR 32. Z4: PR 30. DATA-2: PR 28. DATA: PR 26. 14c to 1: PRs 1 to 25

## Open bugs

- The whole NBH 03 area runs past the 30 minute image step, I25 did not respond at 10.7 GB private memory (Q063). R310 logs free memory in every window, no stop at the floor
- rvt_info.py (R203) does not see cloud links, the SE models list 1 of their 14 Revit links. The Generic floors of IME and IMF are of type FFL, not in floors.types
- No El @ Entry rule beyond 0.285 of the IMC key, the rest are overrides. CASE follows no tested logic. The footprint test cannot see a wrong hand. Civil 3D ends with 0xC0000005 or 0xC0000409 after C3DQUIT

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
Q065. The zone2 IMC file of Revit All Zones is the master of the floor scenarios: last saved as _TEST-MASTER, not workshared, links unloaded, 0 floors against 1,010. Its 2024 copy is made from the Zone 02\ARC original, the IMC key: replace the zone2 file by a copy of that original (recommended), or keep it
Q066. Zone 1 has no TU model here: IMA and IMB are Engicon SE models with STR prototype links, 432 and 301 plots, no level, ramp or yard floor: send the Zone 1 TU models of IMA and IMB (recommended), until then Zone 1 takes its links from the SE models and its levels from the Native reads
Q067. The 13 STR prototypes the SE models link are not upgraded, their 2021 files are in Zone 02\STR\Prototypes: leave them (recommended), the add-in places the ARC prototypes, or upgrade them, which needs a change of the upgrade job
Q068. The 7 Parsons models share one project position, survey point, base point, angle 0, Level 1 and Level 2. R211 puts the internal origin at the plot centre of the NBH, 547.5 m away for NBH 012: let button 1 set the Parsons position from config (recommended), or keep R211

## Next three steps

1. ADDIN Phase 2: the 7 keys read into plan files and past_all.csv from the 2024 copies, the Parsons table of config/area_codes.json
2. ADDIN Phase 3: the Round 17 rules measured on the 7 Parsons keys, then docs/addin-design.md before any add-in code
3. ADDIN Phase 4: the logic in C# with a parity test per step
