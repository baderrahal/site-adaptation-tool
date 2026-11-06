---
id: FAST-3-revit-test
lane: revit-test
stage: 3
status: open
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

The one test, two runs: a first run tonight, Fri 6 Nov from 23:30, and a second run on Sun 8 Nov. Bader's order of 6 Nov 21:10. Each run on the add-in Lead built from main and installed at 23:00, Bader pressed Always Load once. Open Revit yourself. If Revit stops on a Load Once box, write it in steps/seam.md and stop.

One full run from an empty model through the 3 cards on a neighbourhood not used for tuning, with the real villa links. What broke goes to steps/seam.md as a fix list by lane, not fixed here. One screenshot per card. Also the window shots of FAST-1 item 7, moved here on 6 Nov: each window of the add-in opened once in Revit, one shot each in steps/ui-shots/, with no real value in view. Then steps/demo-live.md, the script for Mon 9 Nov in plain words, nothing pre-run, nothing copied from the past job.

The record is steps/revit-results/FAST-3-revit-test.md. The acceptance tests of tests/acceptance/test_r2_build.py and test_r2_pane.py marked FAST-3-revit-test read it and the card shots pane-stage-1.png to pane-stage-3.png.

It shares steps/revit-results/ and steps/ui-shots/ with FAST-1-build-1, which stops at 23:00 on 6 Nov, before this task starts, so the two never run at once.

The driver in Revit: a person may be at this PC. Send a click, a key or a shot only with Revit in front, after 5 minutes idle and with no meeting window, and stop when another input comes (CLAUDE.md). When Revit shows the box "Security - Unsigned Add-In" for Site Adaptation, the driver presses Always Load itself, once, and writes the time in steps/revit-results. This is the one box it may press on sight, Bader's order of 6 Nov 23:37. Every other box keeps the driver rule, and a box of any other maker stays untouched.

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
