# Open questions

The full text of every question open for Bader, by its fixed ID. steps/PROGRESS.md and the top entry of steps/log.md list them by ID and a title of at most 8 words. A closed question moves to steps/questions-closed.md with its answer and date. Q088 to Q094 were closed by Q095 as known limits and are in steps/latent.md. Find a question by grep of its ID, never read this file whole.

Every question open before 2026-10-29 was closed in the answer pass of Round FOLD (steps/questions-closed.md, their text as asked in steps/history/questions-2026-10-29.md).

## Open

Q148 Place villas before Floors and Levels after 5c. Q149 M14 boundary wall of an irregular plot. Q150 M15 foundation depth, at least or equal.
Q151 Freeze guard limit for an image job. Q152 The parameter sheet of button 1. Q153 The GUID of Plot.

Q151. The freeze guard limit for an image job (Lane B 5b). Bader asked the test host to end a Revit that does not respond for 5 minutes. An image step may run 30 minutes (R302, Q061), and in Q063 Revit stopped responding inside the whole NBH 03 image, so 5 minutes would end an image job that is still working. It does not block, the only image run is the NBH 03 run after 5c.
- Recommended: an image job takes the image step limit, testHost.imageStepMinutes, for the not responding check, every other run 5 minutes. Run-TestHost.ps1 does so since 5b and prints the limit, -NotRespondingMinutes overrides it.
- Other: 5 minutes for every run, the image run of Q063 included.

Q152. The parameter sheet of button 1 (Lane B 5b). Bader's change of 28 Oct 2026 has the parameters and their GUIDs come only from the parameter sheet, the Excel of the row Shared parameters. No ROSHN parameter sheet is on the laptop and none in C:\02-Site Adaptation (SEARCHED both, 2026-10-29), so its sheet and columns are UNKNOWN. It does not block, button 1 runs by the recommended reading.
- Recommended: one sheet with one row per parameter, the columns Name, GUID, Data type and Categories. Until Bader gives the sheet, the row takes the 10 names and GUIDs of addin.sharedParameters of config, measured from the 7 keys by the read job and not made by the tool, and says "From the parameter sheet: not chosen, 10 parameters of the answer keys". A sheet chosen by Browse replaces them.
- Other: button 1 stops until a parameter sheet is chosen.

Q153. The GUID of Plot (Lane B 5b). Q085 and Q087 bind Plot, the floor parameter of floors.plotParameter, as a shared parameter with a GUID Bader fixes in a block of config no run writes. No GUID is given and config is Lane A's file. It does not block 5b, 5d places the floors that take it.
- Recommended: faecae98-f82c-46b5-bd80-489380545236, made once by Code on 2026-10-29, written by Lane A into its own block of config and into the parameter sheet. Until then button 1 binds the other 10 and lists Plot as not bound, waiting on Q153.
- Other: a GUID Bader gives.

Q148. Place villas before Floors and Levels in the run after 5c (Round FOLD, review of the design PR 102). The new order of docs/design/13-workflow-changes.md has each step wait for the one before, so 7 Place villas waits for 6 Levels, and 5d builds 5 Floors and ramps and 6 Levels. The phase changes of 28 Oct 2026 give 5c the Place villas button with light mode, and the Revit run after 5c places villas in Masses and in one NBH in Links. Round FOLD wrote that the test host runs 7 by Buttons.Run on the plan before 5 and 6 are built (CHOSEN, steps/01_next.md). It does not block 5b or 5c.
- Recommended: keep it. The test host runs 7 from the plan in the run after 5c, which measures light mode and the base boundary as 28 Oct asks, and the pane keeps the order, so a user cannot run 7 before 6.
- Other: move Place villas and light mode to 5d, after 5 and 6, and the run after 5c places nothing.

Q149. M14, the boundary wall of an irregular plot (Round FOLD, docs/design/15-manual-rules.md). The canvas text reads "boundary wall 200 mm inside the plot limit". The Rev 06 manual, 2.8.A, p. 20 (PDF 23), reads "the boundary wall external face will be identical to the plot limit, and the 200mm will be positioned fully inside the plot", the outer face on the limit and the 200 mm wall thickness inside it. The canvas text is kept until Bader answers. It does not block, M14 is checked by 8 Walls in 5d.
- Recommended: the PDF reading, the outer face of the wall on the plot limit and its 200 mm inside the plot, and the canvas text written so.
- Other: the wall 200 mm in from the plot limit, as the canvas text can be read.

Q150. M15, the foundation depth (Round FOLD, docs/design/15-manual-rules.md, review of PR 105). The canvas text reads "Foundation at least 1.20 m below existing ground". The Rev 06 manual, 3.2.A, p. 23 (PDF 26), reads "Foundation Depth = 1.20m below the EGL." A deeper foundation passes the canvas rule and not the PDF text. It does not block, M15 is only reported by 9 Report.
- Recommended: keep the canvas text, at least 1.20 m, as the check of 9 Report, since a foundation deeper than the design depth is no fault on site.
- Other: the PDF reading, the foundation at 1.20 m, a deeper one reported too.
