# Site Adaptation add-in, the design

Round ADDIN Phase 3b, 2026-10-26, revised the same day after the breaker and the claim-checker read it and again in the last pass on it (PR 49). Written before any add-in code (R343). Phases 4 and 5 build what this file says, or change it first in the same change. The rules live in docs/rules.md, this file names their ids. The approved mock is docs/addin-mock.md and wins over this file where the two differ, the libraries are in docs/licences.md.

How each fact is known is written beside it: READ with the file, RAN with the count, SEARCHED with the link, CHOSEN for a choice of this design with its reason, UNKNOWN for what no run has shown yet. A coordinate is written [coordinate] and a level [level]. The same pages fully filled with the real IMC values are in Documents\SiteAdaptation\round_addin\design\mock_filled.md, outside the repo.

Contents:

1. Option B and the two parts
2. Folder structure
3. The eight buttons
4. From each input file to Revit
5. The plan builder steps Phase 4 moves to C#
6. Settings
7. Every screen and file of the mock
8. Install and the unattended runs
9. Risks
10. Planned rule ids, conflicts and questions

## 1. Option B and the two parts

Option B (Bader): the whole tool runs inside Revit 2024, in C# on .NET Framework 4.8. No Python on the user's PC. Python stays in the repo as the checker of the C# and as the builder of the data book, nothing of it ships (READ, the round prompt and CLAUDE.md).

Every new build takes the level profile parsons2024Rev6 (Q050), whose keyRules hold the rules the 7 Rev 6 Parsons answer keys follow (R337, R338, R340), then the parsons overrides of config/overrides/roshn-makkah.json (R280, R330). python/build_proof.py holds that Python build against every key plot (R340). The C# of Phase 4 must equal that Python build, plot by plot (section 5).

Part 1, this round, is the add-in: the ribbon tab, the dockable pane, the eight buttons, the Set up site dialog, the check table, the level Excel and the report Excel of the mock.

Part 2, after Bader's check of Part 1, is the output sheets. It is not started. Where it attaches:

- Ribbon: new buttons in the Output panel after 8 Report. Part 1 adds no placeholder button.
- Logic: Part 2 reuses the plan of R80 that button 3 writes for each neighbourhood (`plan_<NBH>.json` in the work folder), the plot table of button 2 (section 4), the element tags of section 3 that find each placed link, floor and wall again, and the level rows of button 6.
- Data the plan files keep for it, per plot: the official number and the number of the model, the link origin in shared coordinates (the setting out point), the rotation and mirror, the generic and yard outlines with their arcs, the four ramp corners with their levels, FFL, SSL, El @ Entry, height and steps, the unit and the prototype file, CASE and the parameters, and per wall its id, type, length, height, base and top, location line ends and plots. R325 keeps the same fields in the key plans of the answer keys (READ, round-addin.md Phase 2 part 2), so a sheet can be tested against a key.

