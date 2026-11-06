# revit-test heads-up, FAST-3-revit-test

7 Nov 2026, 02:10. Run 1 of the one test, Revit open since 01:11, stage 1 reading.

## Now

- Prep done, outside the repo: work folder fast3-revit-test of the work root, a fresh copy of the empty model of R2-C1, SHA-256 of the 37 files of S1 before the run, the driver of R2-C1 with the Always Load answer of Bader's order of 23:37 and a meeting guard.
- 00:16, Bader's order by Lead: the Teams window "Meeting compact view" is kept open to keep the laptop awake and is no meeting window. The driver and the wait skip that one window by its exact title, any other meeting window still stops them.
- 00:44:38 the PC was 5 minutes idle. 00:46 the launch of the driver under python/window_lock.py was refused by the permission check of this session, auto mode, no reason given. Revit was not opened, no input sent, no lock taken. Written in steps/seam.md.
- Before that, at 23:43 a Teams window "Meeting compact view" stood on this PC, so by the driver rule nothing is sent. A wait runs until no meeting window stands and the PC is 5 minutes idle.

- 01:15 Bader allowed the driver launch. 01:11 Revit opened, 01:11:56 Always Load on our box. Settings, Project setup, Save setup, Rules seen. Choose project folder stopped on "2 Read data needs step 1", so step 1 ran from Advanced after the Baseplan row was set. 02:07 the read of S1 started.
- The record grows in steps/revit-results/FAST-3-revit-test.md.

## Next

- From the empty model through the 3 cards on S1, one shot per card, one shot per window, the fix list by lane in steps/seam.md, the record in steps/revit-results/FAST-3-revit-test.md, then steps/demo-live.md.
