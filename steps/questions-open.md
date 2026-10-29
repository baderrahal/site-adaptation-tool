# Open questions

The full text of every question open for Bader, by its fixed ID. steps/PROGRESS.md and the top entry of steps/log.md list them by ID and a title of at most 8 words. A closed question moves to steps/questions-closed.md with its answer and date. Q088 to Q094 were closed by Q095 as known limits and are in steps/latent.md. Find a question by grep of its ID, never read this file whole.

Every question open before 2026-10-29 was closed in the answer pass of Round FOLD (steps/questions-closed.md, their text as asked in steps/history/questions-2026-10-29.md).

## Open

Q148 Place villas before Floors and Levels after 5c. Q149 M14 boundary wall of an irregular plot. Q150 M15 foundation depth, at least or equal. Q151 Q075 moved without the majority run. Q152 Mode of the last run of a button. Q153 A lone copy of a tagged element.
Q154 Freeze guard limit for an image job. Q155 The parameter sheet of button 1. Q156 The GUID of Plot. Q157 The type button 1 copies new types from. Q158 Step 1 Done on a model already set up.

Q148. Place villas before Floors and Levels in the run after 5c (Round FOLD, review of the design PR 102). The new order of docs/design/13-workflow-changes.md has each step wait for the one before, so 7 Place villas waits for 6 Levels, and 5d builds 5 Floors and ramps and 6 Levels. The phase changes of 28 Oct 2026 give 5c the Place villas button with light mode, and the Revit run after 5c places villas in Masses and in one NBH in Links. Round FOLD wrote that the test host runs 7 by Buttons.Run on the plan before 5 and 6 are built (CHOSEN, steps/01_next.md). It does not block 5b or 5c.
- Recommended: keep it. The test host runs 7 from the plan in the run after 5c, which measures light mode and the base boundary as 28 Oct asks, and the pane keeps the order, so a user cannot run 7 before 6.
- Other: move Place villas and light mode to 5d, after 5 and 6, and the run after 5c places nothing.

Q149. M14, the boundary wall of an irregular plot (Round FOLD, docs/design/15-manual-rules.md). The canvas text reads "boundary wall 200 mm inside the plot limit". The Rev 06 manual, 2.8.A, p. 20 (PDF 23), reads "the boundary wall external face will be identical to the plot limit, and the 200mm will be positioned fully inside the plot", the outer face on the limit and the 200 mm wall thickness inside it. The canvas text is kept until Bader answers. It does not block, M14 is checked by 8 Walls in 5d.
- Recommended: the PDF reading, the outer face of the wall on the plot limit and its 200 mm inside the plot, and the canvas text written so.
- Other: the wall 200 mm in from the plot limit, as the canvas text can be read.

Q150. M15, the foundation depth (Round FOLD, docs/design/15-manual-rules.md, review of PR 105). The canvas text reads "Foundation at least 1.20 m below existing ground". The Rev 06 manual, 3.2.A, p. 23 (PDF 26), reads "Foundation Depth = 1.20m below the EGL." A deeper foundation passes the canvas rule and not the PDF text. It does not block, M15 is only reported by 9 Report.
- Recommended: keep the canvas text, at least 1.20 m, as the check of 9 Report, since a foundation deeper than the design depth is no fault on site.
- Other: the PDF reading, the foundation at 1.20 m, a deeper one reported too.

Q151. Q075 moved without the majority run (4h-1). Bader answered Q075 to move the 1,346 native entries with no value to the cannot list by a run of python/majority.py with --replace native, once the other native entries of that run are shown equal to the kept ones. That run needs the reads of the 4 Native keys u11 to u14 and their models, and this laptop holds only the reads of the 7 Parsons keys (round_addin
eads, READ), so the run cannot be made here. 4h-1 moved them instead by the new command cannot of python/override_source.py with --source native: 1,346 entries moved (entryM 657, rampFrontM 657, rampFrontFallM 32), each with no value and no rule and its reason marked Q075, the other 11,075 entries equal to the list before in their order and the 154 parsons cannot entries kept (RAN, test_override_cannot.py and the run on a copy). A parsons2024Rev6 build reads native entries of neither list.
- Recommended: accept the move. The run with --replace native comes when the Native reads are back, and it re-measures the other 3,876 native entries then.
- Other: put the 1,346 entries back and wait for the Native reads.

Q152. Mode of the last run of a button (R367, 4h-1). docs/design/06-settings.md gives each button a last run with "mode" and does not say what the mode holds. Only a real run writes the settings, so "dry" never appears there. SettingsJson of Common holds afterDryRun or direct (CHOSEN by the developer of 4h-1).
- Recommended: mode is how the real run was confirmed, afterDryRun when Apply followed a dry run, direct when the box was unticked.
- Other: name another meaning for mode, or drop the field.

Q153. A lone copy of a tagged element (R368, 4h-1). 03a lists a key held by two elements, where the one whose UniqueId equals its placedId stays the tool's. It does not say what to do with one element alone on its key whose UniqueId is not its placedId, such as a copy whose original was deleted. SecondRun.Match lists it and leaves it and skips the item, as when none holds its own UniqueId (CHOSEN by the developer of 4h-1).
- Recommended: keep it, list and leave the element and skip the item, so the tool never takes over an element it did not place.
- Other: take the lone element over as the tool's element and tag it with its own UniqueId.

Q154. The freeze guard limit for an image job (Lane B 5b). Bader asked the test host to end a Revit that does not respond for 5 minutes. An image step may run 30 minutes (R302, Q061), and in Q063 Revit stopped responding inside the whole NBH 03 image, so 5 minutes would end an image job that is still working. It does not block, the only image run is the NBH 03 run after 5c.
- Recommended: an image job takes the image step limit, testHost.imageStepMinutes, for the not responding check, every other run 5 minutes. Run-TestHost.ps1 does so since 5b and prints the limit, -NotRespondingMinutes overrides it.
- Other: 5 minutes for every run, the image run of Q063 included.

Q155. The parameter sheet of button 1 (Lane B 5b). Bader's change of 28 Oct 2026 has the parameters and their GUIDs come only from the parameter sheet, the Excel of the row Shared parameters. No ROSHN parameter sheet is on the laptop and none in C:\02-Site Adaptation (SEARCHED both, 2026-10-29), so its sheet and columns are UNKNOWN. It does not block, button 1 runs by the recommended reading.
- Recommended: one sheet with one row per parameter, the columns Name, GUID, Data type and Categories. Until Bader gives the sheet, the row takes the 10 names and GUIDs of addin.sharedParameters of config, measured from the 7 keys by the read job and not made by the tool, and says "From the parameter sheet: not chosen, 10 parameters of the answer keys". A sheet chosen by Browse replaces them.
- Other: button 1 stops until a parameter sheet is chosen.

Q156. The GUID of Plot (Lane B 5b). Q085 and Q087 bind Plot, the floor parameter of floors.plotParameter, as a shared parameter with a GUID Bader fixes in a block of config no run writes. No GUID is given and config is Lane A's file. It does not block 5b, 5d places the floors that take it.
- Recommended: faecae98-f82c-46b5-bd80-489380545236, made once by Code on 2026-10-29, written by Lane A into its own block of config and into the parameter sheet. Until then button 1 binds the other 10 and lists Plot as not bound, waiting on Q156.
- Other: a GUID Bader gives.

Q157. The type button 1 copies new types from (Lane B 5b, breaker pass 1). 03a lets button 1 make a floor type or a wall type the project lacks as a copy of a type it holds. RevitSiteDocument.cs copies the first basic wall type and the first floor type in element id order, whatever their structure, so BW and Generic Wall-150mm_ Garden take the layers and the width of that first type, and on a project from no template Generic 150mm takes those of Floor 1. Which type comes first in the default metric template and its width are UNKNOWN, the Revit run of 5b read type names only. A wall placed later with a wrong width stands off by the difference with no error. It does not block 5b, 8 Walls places walls in 5d.
- Recommended: copy each new type from the type of the IMC answer key of the same name, its layers and widths read once into config by the read job, and stop when the key has none.
- Other: keep the copy of the first type and have 8 Walls and 5 Floors and ramps set the width they need.

Q158. Step 1 Done on a model already set up (Lane B 5b, breaker pass 2). 03a says a step is Done when its last real run committed, and that a second run of button 1 changes nothing and says so. Since 5b a run with nothing to set opens no transaction, so on a model set up in an earlier Revit session, by hand, or by a run whose site.json move failed, step 1 can never become Done in the pane and 2 Read data stays Needs step 1 once 5c joins R366. It does not block 5b, buttons 2 to 9 are not built yet.
- Recommended: a run where nothing differs counts as Done for the step state and writes site.json and the Settings entity of R367 when they are missing, with no change to the model, and says so.
- Other: R366 keeps the Done state of the earlier run in the Settings entity, and button 1 never commits when nothing differs.
