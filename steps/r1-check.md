# R1 check

Lead's check on 5 Nov 2026 of every Done when line, by reads of the repo and the PC that share no code with the lanes, not by their reports. Tags: RAN a command, READ a file, UNKNOWN. Counts only. PRs: 195 SETUP-sign, 197 R1-build-1, 202 R1-revit-test, all merged.

## In one paragraph

Stage 1 reads a raw folder through the worker on all five scenarios and says what it found. The Load Once prompts are down from 4 to 1. It does not yet run end to end through the pane: button 2 on a raw folder stopped at the old read until R2-C1-build-1, so an empty model and 3 Review data moved to R2-C1-revit-test. Villa type and mirror scored low at the end of R1. A person has not seen the window.

## SETUP-sign

| Done when | Verdict | Proof |
|---|---|---|
| The list of .addin files and prompts before: 4, 4, 4 | Met | READ setup-sign\before5\result.json under the work root, prompts 4, 4 shots. That run ended with no reason recorded, its script crashed after the fourth prompt |
| One .addin, one entry DLL, 1 prompt, the signature count | Met | RAN a listing of the add-in folder: 1 file of this tool, 1 entry, 0 in ProgramData. READ after and after5 result.json, prompts 1. RAN Get-AuthenticodeSignature on 36 installed DLL and EXE files: 25 not signed, 11 signed by other makers, 0 by the tool |
| Developer add-ins for a run only, and a run still works | Met in part | RAN: none left in the folder now. A dry start only, by the lane's record, a real job was not run. A killed run can leave them until the next run or install, steps/latent.md |
| Signing off by default, Trusted Publishers alone gives Invalid Signature | Met | READ scripts/sign, exp-tp-only2 result.json, prompts 0 in a run the record says showed Invalid Signature |
| ricaun licence recorded, a test project out of the solution | Met | READ docs/licences.md:43, RAN grep: RevitTests 0 times in the solution. The test never reached Revit, moved to SETUP-sign-2 |

Moved to SETUP-sign-2, waiting on Bader's choice, steps/bader-trust.md: zero prompts, the certificate removal test, the ribbon buttons, two floor counts.

## R1-build-1

| Done when | Verdict | Proof |
|---|---|---|
| All acceptance tests pass | Met | RAN pytest tests/acceptance/test_r1_stage1.py on main: 16 passed |
| steps/scenarios.md holds S1 to S5, each check says in plain words what it found | Met | READ the table below. The lines are in scenarios\report.md under the work root, as they quote drawing words, 12,638 bytes |
| A second check that shares no code | Met, with a gap it reports | RAN a read of runs\S1\second_check.json: present. By the lane's report, the sheet lists 3,197 plots against 2,964 read, 242 under and 9 over |
| The manifests hold after the runs | Met | RAN my own manifest compare: 0 faults on all five |

## R1-revit-test

| Done when | Verdict | Proof |
|---|---|---|
| The record holds the build, the copy, what was pressed and seen, the count against plots.json, each screenshot's name | Met in part | READ steps/revit-results/R1-revit-test.md. The screenshots are under the work root and not in steps/ui-shots, as they show client names and coordinates |
| A person sees the window, or UNKNOWN | UNKNOWN | The record says so. A driver press proves nothing about what a person sees |
| Not seen in Revit written for the rest | Met | READ the record, its section Not seen in Revit |

Moved to R2-C1-revit-test by Lead: an empty model and a press of 3 Review data.

## Scenario table of PR 197

The answers hold 1,891 plots, the hold-out neighbourhoods left out, and 2,964 plots read includes them.

| Scenario | Plots read | Numbers right | Villa type right | Mirror right | Group words right | Marks: strong, usable, weak, missing | Copies unchanged |
|---|---|---|---|---|---|---|---|
| S1 raw inputs only | 2,964 | 1,873 | 233 | 448 | not scored | 3, 1, 5, 2 | yes |
| S2 no footprint drawing | 2,964 | 1,873 | 233 | 0 | not scored | 3, 1, 5, 2 | yes |
| S3 words changed to Phase and Block | 2,964 | 1,873 | 231 | 448 | 0 of 2 | 3, 1, 5, 2 | yes |
| S4 no LandXML, 2D CAD only | 2,964 | 1,873 | 233 | 448 | not scored | 3, 0, 5, 3 | yes |
| S5 plot numbers in another style | 2,964 | 1,873 | 125 | 448 | not scored | 3, 1, 5, 2 | yes |

## What not to claim

- Not that stage 1 runs through the pane from an empty model.
- Not that villa type or mirror is right. Mirror near half of what a coin would give, the hand of each prototype was not known.
- Not that the hold-out was scored. The answers are set, no run is scored against them yet.
- Not that a person has seen any of it.

## Public repo

PR 197 held two layer names and a sheet word of the real drawings on its branch for some hours, before the review found them. PR 195 named one add-in folder of this PC. Both were squash merged, so those commits are not in main's history. GitHub may still show them on the pages of the merged PRs. That add-in's name is also in older files of the repo, steps/laptop-check.md and two test files. Whether it names a client is a question for Bader.
