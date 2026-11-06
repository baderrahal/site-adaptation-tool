---
id: R2-C2-build-1
lane: build-1
stage: 2
status: done
allowed:
  - dotnet/SiteAdaptation.Common/SiteBuild/
  - dotnet/SiteAdaptation.Tests/SiteBuild/
  - dotnet/CadReader/
  - docs/design/01-option-b.md
  - docs/design/02-folders.md
  - docs/design/03a-buttons-shared-and-1.md
  - docs/design/03b-buttons-2-to-8.md
  - docs/design/04-inputs.md
  - docs/design/05-plan-steps.md
  - docs/design/06-settings.md
  - docs/design/07-mock.md
  - docs/design/08-install.md
  - docs/design/09-risks.md
  - docs/design/10-rule-ids.md
  - docs/design/11-light-mode.md
  - docs/design/12-ui-spec.md
  - docs/design/13-workflow-changes.md
  - docs/design/15-manual-rules.md
  - docs/design/16-design-basis.md
  - docs/design/17-scanner.md
  - docs/design/18-new-project.md
  - docs/design/README.md
  - docs/design/boards/
  - python/
  - scripts/scenarios/
  - steps/scenarios.md
  - steps/latent.md
---

# Scope

The logic of Stage 2 Build site, no Revit call. Starts after R2-C1-fix-build-1 is merged. Bader's order, round R2 C2.

Side by side with R2-C3-build-1 on build-2, Bader's order of 5 Nov 2026, so:

- In Common and Tests this lane writes only new files, in SiteBuild/ of each. The command siteBuild is registered in dotnet/CadReader/WorkerReads.cs, which is this lane's. Its name is already in WorkerRequest.Commands, and it takes an empty SHA-256 (R640_WorkerSeamTests).
- A change to an existing file of Common is asked of Lead with the reason. Lead adds that exact path to this list by a PR, or says do it another way. Do not edit it first.
- Measure siteBuild on S1 and give Lead the time limit, 600 s in config is chosen and not measured. Rule lines R640 to R654 and any new key of config/project.json go through Lead, as docs/rules/ and config/project.json are build-2's. Write them in this task file under a heading Rules for Lead and tell Lead in the last reply. Lead enters them by a Lead PR before the code PR.
- docs/design/14-ui-labels.md is R2-C3-revit-test's, Bader's answer of 5 Nov. A label C2 needs goes through Lead.
- Do not touch rules/. A missing rule value stays missing and marked.
- The rules reach the build by SiteAdaptation.Common.ProjectRules.RulesForBuild.Check(string rulesPath, JObject config, string projectMode) of R2-C3-build-1, which answers RulesChoiceLine and BlockingLine and the rules. projectMode is the mode of the request, never the word in config. Merge main for it once C3 is merged, and call it from siteBuild. The plan wants blocking as a list of line and issues as a list of plot and line, so wrap what it gives. Finding project_rules.json beside the profile is yours, a null path asks even when the project has rules. So C3 merges first, then this task.
- Tests of the rules through siteBuild, marked for this task in tests/acceptance/test_r2_build.py, are this task's.

1. A worker command siteBuild builds from the stage 1 plot table, with no past file and no override list. Every value carries computed, rule with its id, person entered, default or missing. A value with no rule is never guessed: the plot is marked and the value left empty. Copied only in past job mode with a docs/signoff.md entry.
2. Its result carries what one Build run needs: the steps in order, one progress line, only a blocking issue stops and says why in plain CAD and Revit words, the rest go to the issues list. preview.json is the 2D preview before anything is written.
3. Coordinates from the project CAD for an empty model, the position file only as a check, both given with the difference in mm.
4. Q439: the plan says which floors the tool placed have a changed outline, so the floors step replaces them, on old and new models.
5. Toposolid cut to the neighbourhood, not a hull. Report road edge points over 5 mm.
6. Masses: a check for overlap over 1 mm on the whole site.
7. Version 1 leftovers of steps/backlog.md: shared ramp joints, floor split test, irregular plot walls by Rev 6 section 2.8, garden walls, real villa links mode, the move stop R523. Do those with a clear answer, the rest become questions with a recommended answer.
8. Parity: a step that exists in both languages stays in step.

Rule ids: R640 to R654. Question ids: Q501 to Q508. Take no other id.

# Acceptance tests

tests/acceptance/test_r2_build.py, those marked R2-C2-build-1.

# Done when

- Those tests pass and past job mode still equals main.
- S1 and the hold-out run through siteBuild before the merge, Bader's order of 5 Nov 2026. steps/scenarios.md holds what was built and what was missing, counts only. Lead runs all five once at the end of the round.
- The source mix of one scenario plan: no copied value. A second check that shares no code.

# Revit check needed

None here, R2-C2-revit-test does it.

# Rules for Lead

Written by build-1 for Lead to enter by a Lead PR before the code PR. Nine rule ids used of R640 to R654, R649 to R654 stay free. Each is named by a test in dotnet/SiteAdaptation.Tests/SiteBuild/SiteBuildTests.cs, class R640_PlanTests and so on.

Rule lines, status CHOSEN by Code unless a question says otherwise:

- R640. The command siteBuild builds the plan from the plot table of stage 1, the project profile and the config, and reads no past file and no override list. Every value of a plot carries a source: computed, rule with its rule id, person entered, default or missing. A value with no rule and no read stays empty with its reason and the plot is marked. A value of source past is never written outside past job mode. It writes build_plan.json and preview.json and touches no model.
- R641. A number comes from the text of a rule only in a plain form: M10 "<unit types> always have <n> steps", M13 "Front setback <n> m" with ", <unit type> <n> m" for a unit type of its own, M11 and M12 "FFL + <n> m". The value has source rule, the rule id, and the status of the rule. A text in any other form gives no number and an issue. Q501.
- R642. The coordinates of an empty model come from the project CAD: the centre of the box of the plot outlines, rounded to coordinates.originRoundM. The position file is only a check. The plan gives both and the difference in mm. A box outside the UTM zone is an issue.
- R643. Only a blocking line stops a build: the plot table cannot be read, it holds no plot, no plot has an outline, or the rules say block. The line is in plain CAD and Revit words with no brace and no word null. Every other fault is an issue with its plot, and the build goes on.
- R644. The plan lists the steps in the order of the buttons 4 to 8 with the plots ready for each, and one progress line. preview.json holds the closed outline and villa type of each plot with the count of issues, before anything is written.
- R645. The plot table of stage 1 holds the outline of each plot in metres, found again from the same reads of the drawing. A plot without a ring has none and the run says so in an issue, an empty ring is never written.
- R646. Plot outlines are checked on the whole site for overlap over siteBuild.massOverlapMm (1 mm), masses too when the request gives their rings. Two outlines that share an edge do not overlap.
- R647. The toposolid is cut to the boundary of each neighbourhood, the plot outlines of that neighbourhood joined and grown by floors.roadTopography.marginM, never to a hull. The plan gives the area of the cut and of the hull it replaces. A road edge point further than floors.roadTopography.limitMm from the cut is listed. Q473, Q502.
- R648. The plan names the floors the tool placed earlier whose outline differs from the plot now (same corner points within designValues.samePoint.sameMm), so button 5 replaces them and does not stop on them. Q439, Q503.

New key of config/project.json, in the siteBuild block that exists: siteBuild.massOverlapMm = 1.0 (CHOSEN, the order of 5 Nov). Until it is there the code uses 1.0 and says nothing.

New optional inputs of siteBuild: positionFile, rules, placedFloors, masses, roadEdge. Formats are in the comments of FloorsToReplace.cs, MassOverlap.cs and NeighbourhoodCut.cs.

Questions Q501 to Q508, recommended answer first (Lead puts them in steps/questions-open.md, that file is not this lane's):

Q501. Numbers from the text of manual rules (R641). Rules M01 to M15 of the manual template are text, so only M10, M11, M12 and M13 give a number, and only in the plain forms of R641.
- Recommended: Keep the narrow forms. A rule that needs a number gets a number field in the rules window in R3.
- Other: Read any number in a rule text.

Q502. Road edge points (R647). Nothing in stage 1 yields road edge points today, so the check reads an optional file roadEdge and says when it is not given.
- Recommended: Stage 1 writes the points of the road surface edge in R3 and the check uses them.
- Other: The Revit lane reads them from the toposolid edge.

Q503. Floors placed by the tool (R648). The plan can only compare floors it is given. The Revit lane lists them in placedFloors from the floors that carry the tool mark.
- Recommended: R2-C2-revit-test writes placedFloors from the floors with the tool mark before it runs Build.
- Other: Compare every floor of the model, which would replace a floor a person drew.

Q504. Floor level with no road surface (R640). The level of a plot needs the road level at its front and a rule for the height above the road. Neither is in the inputs, so ffl is missing and the plot is marked.
- Recommended: Steps 6 to 8 skip a plot whose ffl is missing and the plan says how many.
- Other: Stop the build when any ffl is missing.

Q505. Masses for the overlap check (R646). The masses exist only in the model after step 7.
- Recommended: The Revit lane lists the base ring of each mass in masses after step 7 and the check runs again.
- Other: Check plot outlines only.

Q506. Shared ramp joints and the floor split test (backlog Q344). No input of the plan holds a ramp or a split floor, and no answer is in the repo.
- Recommended: Leave both to R3 with the floors step, the plan does not touch them.
- Other: Build a joint at every shared edge of two floors.

Q507. Irregular plot walls and garden walls (backlog Q149, Q259). Rule M14 and the Rev 6 section 2.8 are text, M12 gives only the top of the garden wall.
- Recommended: A wall of an irregular plot is marked and not planned until a rule gives its form. The garden wall takes the top of M12 and nothing else.
- Other: Plan the boundary wall along the plot outline for an irregular plot.

Q508. Real villa links mode and the move stop R523 (backlog). Both need a Revit run.
- Recommended: They stay with the Revit lane and are not part of siteBuild.
- Other: Plan the link position in siteBuild from the plot outline.
