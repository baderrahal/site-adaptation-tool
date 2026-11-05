---
id: R2-C2-build-1
lane: build-1
stage: 2
status: waiting
allowed:
  - dotnet/SiteAdaptation.Common/
  - dotnet/CadReader/
  - dotnet/SiteAdaptation.Tests/
  - config/project.json
  - rules/
  - docs/rules/
  - docs/design/
  - python/
  - scripts/scenarios/
  - steps/scenarios.md
  - steps/latent.md
---

# Scope

The logic of Stage 2 Build site, no Revit call. Starts after C1 is merged. Bader's order, round R2 C2.

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
- S1 to S5 and the hold-out run through siteBuild. steps/scenarios.md holds what was built and what was missing, counts only.
- The source mix of one scenario plan: no copied value. A second check that shares no code.

# Revit check needed

None here, R2-C2-revit-test does it.
