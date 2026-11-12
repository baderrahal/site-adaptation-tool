# Rule lines against docs/truth.md, 12 Nov 2026

Round 0, item 3. CLAUDE.md, .claude/rules, .claude/agents, the project skills and docs/rules were read against the standing rules and decisions of docs/truth.md. Found by a read only search, each line below seen by Lead before it was fixed or listed. 48 lines disagree today: 6 fixed in this PR, 42 listed with the task that fixes them.

## Fixed in this PR

| Line | Was | Now |
|---|---|---|
| CLAUDE.md, the old project part | S1 and the hold-out per PR wait until their folders exist again | The standing rules of 12 Nov, no scenario folder |
| CLAUDE.md, How to work | Per PR, S1 and the hold-out, all five scenarios at the end of a round | Per fix, the compare at the pass level |
| CLAUDE.md, Real files | Two lines the standing rules now say | Taken out |
| .claude/rules/revit.md line 13 | At most 3 Revit or Civil 3D windows in all | One Revit or Civil 3D window at a time |
| dotnet/SiteAdaptation.AddIn/Help/help.json, 3 lines | The prototypes in prototypes_2024 of the work root | The Prototypes_2024 folder of the inputs |
| scripts/checks/test_set_machine.py | A refused root on the drive root | Every refused root under the folder of the test |

## Listed, fixed by round 1 step d

Step d removes past job mode, light mode, the data book reads and the old step buttons, and the rules of those parts go with the code in the same PR, each rule line changed with its test.

Copies of the inputs, the data book copy and the key copies, 20 rules: R104, R118, R151, R160, R179, R185, R189, R201, R242, R245, R247, R252, R253, R301, R312, R321, R322, R353, R388, R420.

A write place outside the work root, 5 rules: R40 (sample_output), R87, R110, R310 and R343 (Documents\SiteAdaptation). R110 also lets several Revit windows run at once.

Decisions of 12 Nov, 8 rules: R607 (the five scenario folders), R608 (the clean folder project), R823 (a test on S1), R680 and R764 (Advanced keeps one button per step), R410 (no yard floor until Q225, fixed by the yard floor task of round 2), R570 and R781 (one road surface for the toposolid, fixed by the join task of round 2).

The pass level, 1 rule: R261 passes a key only when every plot is right. The compare of step c takes the pass level of docs/truth.md under its own rule id, R261 goes with the placer check it belongs to.

Other files, 8 lines:

- CLAUDE.md, the 3 stages table names the 9 buttons, and the repo map names samples/new-project. Both are true until step d and are written again by it.
- CLAUDE.md, sample_data and sample_output stay ignored: true as a guard, no run writes there.
- .claude/rules/rules-docs.md line 14: the rules from before setup-lanes are let off the input, output and test lines by scripts/checks/rules_baseline.txt. Step d removes most of them, the rest are rewritten there.
- .claude/agents/breaker.md line 25 and .claude/skills/task/SKILL.md line 32 name a scratch folder outside the work root.
- .claude/skills/audit/SKILL.md line 11 names the clean folder and hold-out checks.
- .claude/skills/ai-max, 4 lines of a general skill that tell to copy real files or use a temporary folder. Not this project's rules, the lines of CLAUDE.md win.

## Listed, in code

- dotnet/SiteAdaptation.Common/WorkRoot.cs lines 57 to 60, python/project_profile.py lines 72 to 84, scripts/scenarios/scenario_tools.py line 30 and .claude/hooks/lane.ps1 line 126: a PC with no machine file gets Documents\SiteAdaptation as its work root. This PC has the machine file. Step d decides: refuse to run with no machine file, CHOSEN by Lead as the recommended fix.
- config/project.json line 173 names a folder on the drive root for Revit jobs. Step d.
- The gate tests on GitHub write their temporary files to the temporary folder of the runner. scripts/quick-tests.ps1 sends them under the work root on this PC.

## Will disagree after step d, to change in that PR

CLAUDE.md, the past job mode part and the source tag "past". .claude/rules/tests.md line 13. The agents breaker, developer and developer-light where they name the override list. The skills audit and task where they name overrides and the tag past. About 75 lines of docs/rules on past job mode, light mode and the data book reads, by id in the report of the search: R160 to R198, R208, R228, R236, R242 to R260, R272, R300, R301, R330, R332, R339, R344, R348 to R361, R375, R385 to R388, R404, R433, R450 to R453, R467, R486, R487, R500 to R504, R530, R635, R636, R639, R640, R646, R660, R664, R702, R704, R740, R780, R795.

## Not a disagreement

A work copy of a model that Revit opens, made under the work root, is not a copy of the input folder: CLAUDE.md "Run the placer and add-in only on copies of a model", revit.md line 15, the runner and prober agents, R109, R202, R204, R205, R227, R448, R460. The one copy of a drawing in round 1 step a is Bader's own order and is deleted after.
