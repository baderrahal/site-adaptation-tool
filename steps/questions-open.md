# Open questions

The full text of every question open for Bader, by its fixed ID. steps/PROGRESS.md and the top entry of steps/log.md list them by ID and a title of at most 8 words. A closed question moves to steps/questions-closed.md with its answer and date. Q088 to Q094 were closed by Q095 as known limits and are in steps/latent.md. Find a question by grep of its ID, never read this file whole.

Every question open before 2026-10-29 was closed in the answer pass of Round FOLD (steps/questions-closed.md, their text as asked in steps/history/questions-2026-10-29.md).

## Open

Q148 Place villas before Floors and Levels after 5c. Q149 M14 boundary wall of an irregular plot. Q150 M15 foundation depth, at least or equal. Q151 Q075 moved without the majority run. Q152 Mode of the last run of a button. Q153 A lone copy of a tagged element. Q154 Q055 reads 11 more 1.4 plots off the ramp. Q155 A plot list takes unit plots only. Q156 A changed step reruns every higher step. Q157 Reads of entryM with heightM or rampFrontM.

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

Q154. Q055 reads 11 more 1.4 plots off the ramp (4h-2). Bader answered Q055 for the 5 TH3 plots 2461, 2470, 2473, 2481 and 2557, where Round 17 found the 1.4 ELl@Entry 0.41 to 1.59 m off its own ramp back while the ramp backs of the two keys agree (steps/investigations/round-17.md). clean_entry of python/majority.py already took every key parameter off its own ramp back by more than majority.entryRampM, 0.3 m, as no value, and on past_all.csv of the laptop that holds 16 rows, all of the 1.4 key u14: the 5 and 2018, 2025, 2071, 2075, 2080, 2083, 2088, 2092, 2176, 2466 and 2478, 0.30 to 3.45 m off (RAN). 4h-2 reads El @ Entry off the ramp back on all 16, one rule for the one fault. No parsons2024Rev6 build reads it, majority.py and build_proof.py do.
- Recommended: keep the one rule on all 16.
- Other: read off the ramp on the 5 alone, the other 11 kept as no value.

Q155. A plot list takes unit plots only (R366, 4h-2). 03a says the scope Whole project and Neighbourhood hold the unit plots of the plot table and does not say so of a plot list. ButtonScope of Common keeps the unit plots of a plot list too: a token whose plots are all no unit plot stops with "Plot list: 500 holds no unit plot in the plot table", and a range of mixed plots keeps its unit plots only (CHOSEN by the developer of 4h-2).
- Recommended: keep it, the buttons place villas on unit plots alone.
- Other: a plot list takes every plot the table holds, a plot with no unit listed and skipped by each button.

Q156. A changed step reruns every higher step (R366, 4h-2). 03a says an input changed since a step makes that step and every step after it need running again. StepStates of Common reads after by step number, so a change or a run again of 5 to 8 makes 9 Report need running again, though 9 needs 4 only, and a step run before a lower step was run again needs running again (CHOSEN by the developer of 4h-2).
- Recommended: keep it, the report reads what 5 to 8 placed.
- Other: after by the steps each step needs, so 9 stays Done when only 5 to 8 change.

Q157. Reads of entryM with heightM or rampFrontM (Q119, 4h-2). Q119 asks that a value list the reads of the override that set each input it stands on, and names the wall base and top, the yard and the insert. The one rule of 4h-2 applies to every value, so where entryM is applied with heightM or rampFrontM the reads change too, with no status changed: levels.height 1,499 values now read none, levels.steps 1,485 the steps rule and the riser, parameters.RRE_ZZ_SP_Number-of-Steps 1,485, parameters.RRE_ZZ_SP_RVT-Name and linkName 1,470 each, floors.ramp.corners and floors.ramp.heightOffset 489 each (RAN, the whole project build of 4h-2 against 4h-1).
- Recommended: keep the one rule on every value, as the sentence of Q119 reads.
- Other: apply Q119 to the walls, the yard and the insert only.
