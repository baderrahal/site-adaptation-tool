# Bader's form, 6 Nov 2026

At most 5 items that block work, each with a recommended answer first. Answer by the letter.

1. D-46, the Confirm window, steps/ui-shots/confirm-profile.png. It is the smallest version, CHOSEN by Lead, as no sketch covered it: two columns, the code map, the hand of each prototype starting empty with the guess beside it, the group levels.
   - A. Approve it as it is. Recommended.
   - B. Change it, say what.
2. Q520, the Windows account name in public files, 52 times in 10 files under steps/.
   - A. Lead writes <work root> for those paths and adds a check that fails a new one. History stays. Recommended.
   - B. Leave it.
3. SETUP-sign-2, the route of the add-in trust, steps/bader-trust.md. Load Once stopped unattended Revit runs.
   - A. Press Always Load once by hand now, and ask IT for a company certificate for a team. Recommended.
   - B. Add the lane's certificate to Trusted Root on this PC, the steps in steps/bader-trust.md.
   - C. Leave it, runs stay attended.
4. The R310 tests of ParsonsModelTests fail at random on this PC, idle too, so the stop hook of every lane blocks. steps/latent.md.
   - A. Lead adds ParsonsModelTests.cs to FAST-2 by exact path, it measures the time of a CIM call on this PC and takes the limit from it. Recommended.
   - B. Leave it, the lanes stop with a Blocker line.
5. Q504, the floor level with no road surface. The build sets no level today, ffl 0 of 610 on the hold-out, so the demo on Mon 9 Nov builds floors with no level.
   - A. Steps 6 to 8 skip a plot whose ffl is missing and the plan says how many. Recommended by build-1.
   - B. Take the road level and a rule for the height, Bader names the rule.
