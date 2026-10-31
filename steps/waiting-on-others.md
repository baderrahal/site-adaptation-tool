# Waiting on others

The asks of the questions Bader closed in the answer pass of 2026-10-29 whose recommended answer asks someone or waits for a file. Each question is closed in steps/questions-closed.md with the interim answer the tool uses now, and Bader's turn no longer lists it. One line per item: the question, who is asked, for what, what the tool does until then. Who is asked is the one the recommended answer names, or CHOSEN by Code where it names no one. When the answer comes, it opens a new question or a join item, never a change to the closed one. Q022, Q023 and Q065 are Bader's own steps by hand, kept here since no run may write under an input root (D19) and the tool cannot do them.

| Q | Who is asked | For what | What the tool does until then |
|---|---|---|---|
| Q001 | The client, and the architect | The Infrastructure IFC package, and the ramp slope | El @ Entry by the road plus the ramp slope, which holds 0.285 of the IMC key within 5 mm, the other plots of the keys as overrides |
| Q008 | The client (CHOSEN) | A baseplan that holds 01b | Builds from the baseplan it has, 01b is not built |
| Q012 | Parsons, the past job (CHOSEN) | Why IMF and IMG split a cluster FFL at an end or not, 15 of 36 | The plurality per unit sequence of keyRules.levelSplit.bySequence, two sequences weak |
| Q022 | Bader, by hand | Delete the TEST copies in Zone 02\ARC | Leaves them, no run makes a TEST copy under an input root (D19) |
| Q023 | Bader, by hand on the personal PC | Delete the empty site-adaptation-tool-15a folder | Nothing, the folder is outside the repo and the work root |
| Q026 | The road designer (CHOSEN) | Which of the two curbs of the 2021 drawing is current | The Roads curb |
| Q027 | The architect (CHOSEN) | The split of the face that holds two DP4 labels | The face as the baseplan draws it, listed |
| Q030 | The client (CHOSEN) | The 01b sales rows | Plan plots with no sales row are listed |
| Q031 | The client (CHOSEN) | Which is current where 97 Rev 9 faces differ from the baseplan | The baseplan |
| Q051 | Bader, from Parsons | The Rev 01 sections on steps, base heights and ramps | Rev 6 prevails, the weak rules hold through their overrides (Q052) |
| Q058 | Bader | The folder of the old 20221206 prototypes | Their hand from the CAD reference, V3 and V2 opposite to VL6 and VD2 |
| Q065 | Bader, by hand | Replace the zone2 IMC file of Revit All Zones by a copy of the Zone 02\ARC original | The IMC key copy is made from the Zone 02\ARC original, the two files parted by hash. No run writes under an input root (D19) |
| Q066 | Parsons | The Rev 6 TU models of IMA and IMB | The Native 1.1 and 1.2 stand-in for their levels, ramps and yards, the Zone 1 yard from Zones 2 and 3 |
| Q070 | Parsons | The schedules of IME, IMF and IMG | El @ Entry from the ELl@Entry parameter of the key |
| Q071 | The architect | The wall top rule | Base at the yard level of the higher plot, centre on the plot edge, front edge open, top at FFL plus 1.7 m (weak, 0.346), wall overrides per plot side once button 8 places walls |
| Q082 | The road designer | The road surface or road model of the fronts of 33 key plots | Those plots are listed in noRoadLevel with the reason and not written |

## The scanner window, asked of Lane B by Lane D 4j-1

The scanner (R420 to R427) writes scan.json in the run folder, its shape in docs/design/17-scanner.md. Lane D builds no window. What the window needs, CHOSEN by Code until Lane B designs it (Q246):

| Need | From scan.json | What the tool does until then |
|---|---|---|
| Pick the files to scan, CAD, Excel, PDF and the prototype folder | the roles of a scan request, scanCad, scanExcel, scanPdf, prototype | the worker command scan runs from a request written by hand or a test |
| Show each layer of each drawing with its counts | rows of kind cadLayer | nothing shows them |
| Show the proposed layer of plot outlines, plot numbers and villa blocks, the others as checks, and let the user take another | rows plotOutline, plotNumber, villaBlock, proposedUse and score | button 2 reads the layers of config/cad_layers.json |
| Show the villa type source proposed and the others | rows villaType | the plot table of button 2 reads the sales sheet of config |
| List the conflicts and checks by plot, with both values and both sources | conflicts and checks | nothing shows them |
| Keep what the user took for the next read | none, a new file of the window | none |

## Lane C NEWPROJ-UI, 31 Oct 2026

The placeholders of the window New project (docs/design/18-new-project.md). Each waits for another lane, the window shows it grey or as text until then.

| Item | Who is asked | For what | What the tool does until then |
|---|---|---|---|
| Scanner hook | Lane B, a later round (Lane D builds the scanner) | Fill IDrawingFacts through NewProjectDialog.Facts: the layer count, the survey point, the lists behind Change and View | LastReadFacts: unit plots and villa types of the latest read, the plot line layers of the layer list. Change and View grey, Survey point says Comes with the drawing scan |
| Villa levels row | Lane A | The door level source, and with it the input and the sheets of the villa levels Excel | The row shows Comes later, Browse grey, no template, never saved |
| Cluster proposal | Lane A | Whether New project proposes clusters | Nothing in the window |
| Rules R430 to R434 | Lane A | Write the five rules of Lane C NEWPROJ-UI into docs/rules with their tests, from steps/HEADSUP.md, so docs/design may name their ids (R343) | The ids stand in the code, the tests and steps/HEADSUP.md only |
| Back yard | Lane A | Whether New project sets the back yard | Nothing in the window |
