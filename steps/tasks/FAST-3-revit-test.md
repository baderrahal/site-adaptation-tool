---
id: FAST-3-revit-test
lane: revit-test
stage: 3
status: waiting
allowed:
  - steps/revit-results/
  - steps/ui-shots/
  - steps/demo-live.md
  - dotnet/SiteAdaptation.Tests/FAST3/
  - steps/heads-up/revit-test.md
  - steps/seam.md
  - steps/log.md
---

# Scope

The one test, Sun 8 Nov. Opens when FAST-1 and FAST-2 are merged. Bader's order of 6 Nov.

One full run from an empty model through the 3 cards on a neighbourhood not used for tuning, with the real villa links. What broke goes to steps/seam.md as a fix list by lane, not fixed here. One screenshot per card. Then steps/demo-live.md, the script for Mon 9 Nov in plain words, nothing pre-run, nothing copied from the past job.

The record is steps/revit-results/FAST-3-revit-test.md. The acceptance tests of tests/acceptance/test_r2_build.py and test_r2_pane.py marked FAST-3-revit-test read it and the card shots pane-stage-1.png to pane-stage-3.png.

It shares steps/revit-results/ and steps/ui-shots/ with FAST-1-build-1, which is merged before this task opens, so the two never run at once.

The driver in Revit: a person may be at this PC. Send a click, a key or a shot only with Revit in front, after 5 minutes idle and with no meeting window, and stop when another input comes (CLAUDE.md).

Rule ids: R720 to R729. Question ids: Q550 to Q559. Take no other id.

# Build rules

- No vibe code. Add only what the task lists. A change not in the task goes in steps/seam.md as a line for Lead, not in the code.
- Wiring. Every new function has a caller named in the PR. A part nothing calls is a failure.
- Logic. Every rule has its id, input, output and test. Where the task is silent, take the smallest version, mark it CHOSEN in the PR, go on. Do not stop.
- Folder structure. New files go only in the folders of the allowed list. No new folder without a line in the PR saying why.
- Any project. No ROSHN value in code. Every project value comes from config, rules or the Excel. No hand editing of JSON by a user.
- Each PR body: plan items done, skipped and extra with the reason, callers, what was opened in Revit, the log entry.
- Push at least every 2 hours. Open the PR when one card or one function works, not at the end. Merge main before every push.
- Keep steps/heads-up/<lane>.md current: what changed, where, what calls what, what is next. One page.

At the end, put your final statement in the PR body and in steps/heads-up/<lane>.md, then push.
