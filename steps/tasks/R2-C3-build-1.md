---
id: R2-C3-build-1
lane: build-1
stage: rules
status: waiting
allowed:
  - dotnet/SiteAdaptation.Common/
  - dotnet/CadReader/
  - dotnet/SiteAdaptation.Tests/
  - config/project.json
  - rules/
  - docs/rules/
  - steps/latent.md
---

# Scope

Rules per project, the basics, no Revit call. Bader's order, round R2 C3.

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

Those tests pass, and a project with no rules builds with every rule value missing and marked, never a default from a past job.

# Revit check needed

None here, R2-C3-revit-test does it.
