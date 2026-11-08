# revit-test heads-up, FAST-3-revit-test

8 Nov 2026, 15:45. The demo run is merged, PR 287. The Phase 3 run stopped at 15:38 inside card 1. Task status stays open, Lead decides what comes next.

## Final statement

- Demo model of part 2: built and saved, FAST3-demo-01A.rvt of fast3-revit-test, 15,785,984 bytes, write time 14:04:26. The disk file was unchanged at 15:38. Build 4.2 min, reopen 70 s with no box, 263 of 278 plots. Detail in the demo run section of steps/revit-results/FAST-3-revit-test.md.
- Phase 3 NOT finished. Done: Set up site, the read 189 s, Confirm 17 s with no second read, the presses of F35 and F36, the second a full read of 216 s as a road drawing open in Civil 3D kept the first read from being whole. Not done: Scope 3C, 3 Review data, the preview, the Build, the save as FAST3-phase3-demo. No element placed, no plot built.
- Why it stopped: a person on the PC from 15:19, no 5 idle minutes by 15:37, and at 15:38 a person working in the Revit the driver had started. The session stopped its own driver by id at 15:38:52, sent no input and left Revit open. The driver's limit at 15:46 would have force stopped that Revit.
- Open: process 89596 holds FAST3-demo-01A and FAST3-phase3. FAST3-phase3 is not saved, the file on disk is the empty copy. A Save in that Revit changes the demo model. The person closes it.
- The position of Phase 3 in that Revit is typed by Lead, Easting 500000, Northing 0, Angle 0, not the site, source tag user pick.
- New faults in steps/seam.md: F40 Set up site gets no Baseplan from the folder, F41 Set up site finds no plot outline in the drawing where the read finds 3445, cause not measured, F42 a typed origin of 0 is refused by the range check. Owner build-2.
- Phase 3\Input unchanged: 26 files, 0 changed, 0 gone. 4 hidden Civil 3D lock files are in the after list, not proved new, the tool writes none.
- The driver rule held to the end: no click, key or shot while a person was on the PC. Lead's relayed "you can continue" of 14:22 was not taken as lifting it.

## What changed, where

- steps/revit-results/FAST-3-revit-test.md: the Phase 3 section.
- steps/seam.md: the Phase 3 line, F40 to F42.
- steps/log.md: the top entry.
- No code. drive3.ps1 unchanged, the helpers of this run in the session scratch folder.

## Next

- Lead: how the demo at 17:00 shows Phase 3, and whether the run is taken up again on a quiet PC. It then needs a fresh start of card 1, or the open FAST3-phase3 saved by hand.
