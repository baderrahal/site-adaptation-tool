---
id: R2-C3-build-1
lane: build-2
stage: rules
status: open
allowed:
  - dotnet/SiteAdaptation.Common/ProjectRules/
  - dotnet/SiteAdaptation.Tests/ProjectRules/
  - rules/
  - docs/rules/
  - config/project.json
---

# Scope

Rules per project, the basics, no Revit call. Bader's order, round R2 C3.

Side by side with R2-C2-build-1 on build-1, Bader's order of 5 Nov 2026, so:

- In Common and Tests this lane writes only new files, in ProjectRules/ of each. The commands rulesSetup and rulesChange are added in ProjectRulesCommands.Add, which the worker already calls. Their names, the request fields Who, RuleId and NewValue, a project folder as a folder input and a rules file that rulesChange may change are already in place (R640_WorkerSeamTests).
- A change to an existing file of Common is asked of Lead with the reason. Lead adds that exact path to this list by a PR, or says do it another way. Do not edit it first.
- steps/latent.md is build-1's. LATENT findings go in the PR body, Lead enters them.
- SiteAdaptation.Common.ProjectRules.RulesForBuild.Check(string rulesPath, JObject config) is what siteBuild of R2-C2-build-1 calls: a null path gives RulesChoiceLine, the line that asks once, rules not confirmed give BlockingLine under block, and an issue under warn, and it returns the rules. This task merges before C2.

1. Rules live per project in the project folder beside the project profile, never in code. Shipped templates under rules/templates are read only: manual.json from the manual, past-job.json holding the past job measures, never a default.
2. A first Build with no rules chosen asks once to use the manual rules template.
3. Rules from a template stay not confirmed until a person confirms them.
4. A project setting Before Build: warn and build, the default, or block Build.
5. Every rule change logged with who, when, old and new value.
6. Fixed and not settable: the Saudi checks always run, a missing value is never guessed, every change is logged.

Kept for R3, do not build: rules read from the manual PDF, required rules per project, lead only confirm, the report section on rules.

Rule ids: R660 to R664. Question ids: Q513 to Q516. Take no other id.

# Acceptance tests

tests/acceptance/test_r2_build.py, those marked R2-C3-build-1.

# Done when

Those tests pass. RulesForBuild.Check with no rules gives every rule value missing and marked, never a default from a past job. S1 and the hold-out are unchanged by the PR, Bader's order of 5 Nov 2026. The build through siteBuild is proven by the tests C2 holds.

# Revit check needed

None here, R2-C3-revit-test does it.
