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

## Lane B asks of round 4i

What Lane B must add so the add-in shows and uses the work of round 4i (R407 to R412, docs/design/16-design-basis.md). Until then the plan carries the values and the add-in does not show them.

| Item | Rule | What Lane B adds | What the tool does until then |
|---|---|---|---|
| L4i-1 | R407 | The prefill of a new project (J16, Q187) writes designBasis.mode from newProjectMode, and the pane shows the mode of the build | The mode of config, roshnPastJob for ROSHN Makkah (Q224) |
| L4i-2 | R408 | A Settings row to pick the door level source, civilFile or roadPlusSlope, and Browse for the civil file, passed to the worker as the input of role civilFile with config/floor_outlines.json as floorOutlines | The source of config, roadPlusSlope, no civil file |
| L4i-3 | R408 | The rows of noDoorLevel and slopeOutsideManual of the plan summary in 4 Check | They stand in the summary and the step files only |
| L4i-4 | R409 | 2 Read data names designBasis.roadLevel.surface as the road surface of a rev6 build | The read reads every surface of siteBuild.roadSurfaces (Q234) |
| L4i-5 | R410 | 5 Floors and ramps places no yard floor under rev6, and the report shows yardGrade | The plan holds no yard floor under rev6, yardGrade in the step floors only (Q232) |
| L4i-6 | R411 | 3 Review data shows proposedCluster, proposedMirror and proposalReason of each plot with Confirm, and a confirmed proposal goes into the override list as a hand edit | The proposals stand in the step hands and in plan.json under proposal |
| L4i-7 | R412 | A command for the new project check that starts the worker command newProjectCheck and shows its text report | python/new_project_check.py and the worker command run it by hand |
