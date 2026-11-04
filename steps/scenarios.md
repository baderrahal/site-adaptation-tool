# Scenarios

Written by scripts/scenarios/score_scenarios.py, never by hand. Stage 1 on each folder under the work root, scored against its right answer. Counts only, the folders stay out of git. The right answers leave the hold-out neighbourhoods out.

| Scenario | What it is | Ran | Plots read | Numbers right | Villa type right | Mirror right | Marks: strong, usable, weak, missing | Copies unchanged |
|---|---|---|---|---|---|---|---|---|
| S1 | raw inputs only, every past output removed | done | 2964 of 1891 | 1873 of 1891 | 233 of 1891 | 448 of 1891 | 3, 2, 4, 2 | yes |
| S2 | as S1, footprint drawing removed | done | 2964 of 1891 | 1873 of 1891 | 233 of 1891 | 0 of 1891 | 3, 2, 4, 2 | yes |
| S3 | as S1, zone and neighbourhood words changed to Phase and Block | done | 2964 of 1891 | 1873 of 1891 | 231 of 1891 | 448 of 1891 | 3, 2, 4, 2 | yes |
| S4 | as S1, no LandXML and no Civil 3D surfaces, 2D CAD only | done | 2964 of 1891 | 1873 of 1891 | 233 of 1891 | 448 of 1891 | 3, 1, 4, 3 | yes |
| S5 | as S1, plot numbers in another style | done | 2964 of 1891 | 1873 of 1891 | 125 of 1891 | 448 of 1891 | 3, 3, 3, 2 | yes |

## S1, what the data check said

- plotOutlines, strong: 2964 plot outlines found on layer MP_PLOT_LINE + MP_PLOT_SUBD, 49 numbers are outside an outline or shared.
- plotNumbers, strong: Plot numbers are text on layer MP_PLOT_TEXT, 2964 of 2964 plots hold one.
- groupLevels, usable: By distance: level 1 has 2 groups, level 2 has 139 groups. Words: Facades.
- villaType, weak: Villa type is read from blocks and text and a sheet for 2261 of 2964 plots, another kind of source gives another type on 2076 of them.
- villaBlocks, weak: Villa blocks are inserts on 6 layers, 1285 of 2964 plots hold one.
- roadEdges, weak: Road edges are lines on layer MP_WADI_CHAN, within 20 m of 279 of 2964 plots.
- roadLevels, usable: 18 surface files of Land XML found, how far they reach over the plots is checked when the data is read.
- coordinates, weak: The drawing is in meters and names no coordinate system, the plots sit in drawing space.
- prototypes, strong: 13 prototype files found, 13 with a villa type code in the file name. The files are opened at Read data.
- rules, missing: No rule set file is in the folder, the rules are chosen in the add-in.
- sharedParameters, missing: No shared parameter file is in the folder.

## S2, what the data check said

- plotOutlines, strong: 2964 plot outlines found on layer MP_PLOT_LINE + MP_PLOT_SUBD, 49 numbers are outside an outline or shared.
- plotNumbers, strong: Plot numbers are text on layer MP_PLOT_TEXT, 2964 of 2964 plots hold one.
- groupLevels, usable: By distance: level 1 has 2 groups, level 2 has 139 groups. Words: Facades.
- villaType, weak: Villa type is read from text and a sheet for 2261 of 2964 plots, another kind of source gives another type on 2076 of them.
- villaBlocks, weak: Villa blocks are inserts on layer RD-CURB-LINE, 15 of 2964 plots hold one.
- roadEdges, weak: Road edges are lines on layer MP_WADI_CHAN, within 20 m of 279 of 2964 plots.
- roadLevels, usable: 18 surface files of Land XML found, how far they reach over the plots is checked when the data is read.
- coordinates, weak: The drawing is in meters and names no coordinate system, the plots sit in drawing space.
- prototypes, strong: 13 prototype files found, 13 with a villa type code in the file name. The files are opened at Read data.
- rules, missing: No rule set file is in the folder, the rules are chosen in the add-in.
- sharedParameters, missing: No shared parameter file is in the folder.

## S3, what the data check said

- plotOutlines, strong: 2964 plot outlines found on layer MP_PLOT_LINE + MP_PLOT_SUBD, 49 numbers are outside an outline or shared.
- plotNumbers, strong: Plot numbers are text on layer MP_PLOT_TEXT, 2964 of 2964 plots hold one.
- groupLevels, usable: By distance: level 1 has 2 groups, level 2 has 139 groups. Words: Facades.
- villaType, weak: Villa type is read from blocks and text and a sheet for 2253 of 2964 plots, another kind of source gives another type on 2075 of them.
- villaBlocks, weak: Villa blocks are inserts on 6 layers, 1285 of 2964 plots hold one.
- roadEdges, weak: Road edges are lines on layer MP_WADI_CHAN, within 20 m of 279 of 2964 plots.
- roadLevels, usable: 18 surface files of Land XML found, how far they reach over the plots is checked when the data is read.
- coordinates, weak: The drawing is in meters and names no coordinate system, the plots sit in drawing space.
- prototypes, strong: 13 prototype files found, 13 with a villa type code in the file name. The files are opened at Read data.
- rules, missing: No rule set file is in the folder, the rules are chosen in the add-in.
- sharedParameters, missing: No shared parameter file is in the folder.

## S4, what the data check said

- plotOutlines, strong: 2964 plot outlines found on layer MP_PLOT_LINE + MP_PLOT_SUBD, 49 numbers are outside an outline or shared.
- plotNumbers, strong: Plot numbers are text on layer MP_PLOT_TEXT, 2964 of 2964 plots hold one.
- groupLevels, usable: By distance: level 1 has 2 groups, level 2 has 139 groups. Words: Facades.
- villaType, weak: Villa type is read from blocks and text and a sheet for 2261 of 2964 plots, another kind of source gives another type on 2076 of them.
- villaBlocks, weak: Villa blocks are inserts on 6 layers, 1285 of 2964 plots hold one.
- roadEdges, weak: Road edges are lines on layer MP_WADI_CHAN, within 20 m of 279 of 2964 plots.
- roadLevels, missing: No Land XML surface file is in the folder, so there are no road levels.
- coordinates, weak: The drawing is in meters and names no coordinate system, the plots sit in drawing space.
- prototypes, strong: 13 prototype files found, 13 with a villa type code in the file name. The files are opened at Read data.
- rules, missing: No rule set file is in the folder, the rules are chosen in the add-in.
- sharedParameters, missing: No shared parameter file is in the folder.

## S5, what the data check said

- plotOutlines, strong: 2964 plot outlines found on layer 0 + ROW, 278 numbers are outside an outline or shared.
- plotNumbers, weak: Plot numbers are text on layer 0, 62 of 2964 plots hold one.
- groupLevels, usable: By distance: level 1 has 2 groups, level 2 has 139 groups. Words: Facades.
- villaType, usable: Villa type is read from blocks and text for 2080 of 2964 plots.
- villaBlocks, weak: Villa blocks are inserts on 6 layers, 1285 of 2964 plots hold one.
- roadEdges, strong: Road edges are lines on layer RD_PR_CNRL, within 20 m of 2964 of 2964 plots.
- roadLevels, usable: 18 surface files of Land XML found, how far they reach over the plots is checked when the data is read.
- coordinates, weak: The drawing is in meters and names no coordinate system, the plots sit in drawing space.
- prototypes, strong: 13 prototype files found, 13 with a villa type code in the file name. The files are opened at Read data.
- rules, missing: No rule set file is in the folder, the rules are chosen in the add-in.
- sharedParameters, missing: No shared parameter file is in the folder.
