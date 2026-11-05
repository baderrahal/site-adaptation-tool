---
id: R2-C2-build-1
lane: build-1
stage: 2
status: waiting
allowed:
  - dotnet/SiteAdaptation.Common/SiteBuild/
  - dotnet/SiteAdaptation.Tests/SiteBuild/
  - dotnet/CadReader/
  - docs/design/
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
