---
id: R2-C1-build-1
lane: build-1
stage: 1
status: open
allowed:
  - dotnet/SiteAdaptation.Common/
  - dotnet/CadReader/
  - dotnet/SiteAdaptation.Tests/
  - config/project.json
  - rules/roshn-makkah.json
  - docs/rules/
  - docs/design/17-scanner.md
  - samples/new-project/
  - scripts/scenarios/
  - steps/scenarios.md
  - steps/latent.md
---

# Scope

The stage 1 gaps that block a correct build, in the worker with no Revit call. Bader's order, round R2 C1.

1. Villa type code map, a project input and not a past output. Stage 1 looks in the handed over workbooks for a table of old code to new code and for a typology column per plot, by what they hold and never by a sheet name in code. It proposes them. The confirmed map comes back in the project profile.
2. Prototype hand. The project profile holds the hand each prototype file is drawn in, with the scanner's guess shown as a guess. Mirror is computed from the block scale and that hand.
3. Group words. A level word comes only from a text that names groups, never from any nearby text. With none, the user names the level once.
4. These inputs carry source person entered or computed, never copied.
5. Score villa type, mirror and words again on S1 to S5, once with no confirmed input, and once with scripts/scenarios/confirmed_profile.py, which stands in for a person pressing Confirm on what the tool itself proposed, nothing from a past file. Keep both in steps/scenarios.md, counts and marks only.

Out of scope: the window, that is R2-C1-revit-test. Past job mode stays as it is.

Rule ids: R630 to R635. Question ids: Q495 to Q498. Take no other id.

# What is there

Scenario folders under the work root, scenarios\S1 to S5. The answers are read by the scoring script alone, a hook blocks you from them. Run scripts/scenarios/score_scenarios.py for counts. The latent items of PR 197 are in steps/latent.md, take any this work touches.

# Acceptance tests

tests/acceptance/test_r2_stage1_gaps.py, marked R2-C1-build-1. Set status: done before the last run, which makes them hard.

# Done when

- All those tests pass, and the R1 acceptance tests still pass.
- steps/scenarios.md holds S1 to S5, before and after a confirmed profile.
- A second check that shares no code with the reader: villa type counts per neighbourhood from the sheet read with openpyxl against plots.json.
- The manifests of all five folders hold after the runs, and built_from.json ties each to its commit.

# Revit check needed

None here, R2-C1-revit-test does it.
