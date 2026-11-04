---
id: R1-build-1
lane: build-1
stage: 1
status: open
allowed:
  - config/project.json
  - rules/roshn-makkah.json
  - dotnet/SiteAdaptation.Common/
  - dotnet/CadReader/
  - dotnet/SiteAdaptation.Tests/
  - docs/rules/
  - docs/design/17-scanner.md
  - samples/new-project/
  - scripts/scenarios/
  - scripts/checks/past_paths.txt
  - steps/scenarios.md
  - .gitignore
---

# Scope

Stage 1 Project data from raw inputs. A new worker command dataCheck, built on the scanner (R420 to R427, docs/design/17-scanner.md), run on a folder as a team hands it over.

1. Folder scan with a data check. Each need, plot outlines, plot numbers, group levels, villa type, villa blocks, road edges, road levels, coordinates, prototypes, rules, shared parameters, marked strong, usable, weak or missing, with one plain line in CAD and Revit words. The run never stops without saying why.
2. Groups found by shape, not by name: plots that touch or share a block, areas apart by distance. All distances in config or the rules profile.
3. Project profile with the project's own word for each group level, found in CAD text and Excel headers, missing when there is none.
4. Read plots with no past file: count, number, villa type, mirror and groups per plot, each with its source tag. No schedule, plots.csv, labels.csv, border, override list or key read (truth audit items 1, 2, 7). A value not found is missing with a reason, never a guess.
5. Turn on the clean folder test: samples/new-project gets the made up project of tests/acceptance/made_up_project.py and a build.json. A .gitignore line lets those made up files in, no other.
6. Run stage 1 on all five scenario folders by scripts/scenarios/run_stage1.py and score by scripts/scenarios/score_scenarios.py, which writes steps/scenarios.md.

Out of scope: the add-in window and pane, that is R1-revit-test. Stage 2. Past job mode stays as it is, its tests must still pass, and the step parity of the old read stays.

Rule ids: R600 to R619. Question ids: Q480 to Q489. Take no other id.

# What is there

- The scenario folders and right answers are under the work root, scenarios\S1 to S5 and scenarios\answers, made by scripts/scenarios/make_scenarios.py, copies only. Read them, never change them, the manifest of each is checked after a run. Never put a file, a path with a client name, or a value from them in git.
- Tune on the five folders. The answers there leave some neighbourhoods out on purpose. Do not go looking for those, they are the hold-out.
- steps/truth-audit.md section 5 lists each stop with its line.

# Acceptance tests

tests/acceptance/test_r1_stage1.py, every test marked R1-build-1. Set status: done before the last run, that makes them hard. Read its top comment for the file shapes. The lead owns them, if one looks wrong stop and say why.

# Done when

- All acceptance tests pass.
- steps/scenarios.md holds S1 to S5, and each data check says in plain words what it found and what is missing.
- A second check that shares no code with the reader: count plots and villa types per neighbourhood by another route, such as python/scan.py on S1 or the DXF text itself. Report the gap.
- The manifests of all five folders hold after the runs.

# Revit check needed

None in this task, R1-revit-test does it. Label the PR revit-later if it touches an add-in file, it should not.
