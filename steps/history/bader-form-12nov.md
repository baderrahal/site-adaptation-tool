# Form for Bader, 12 Nov 2026

Five items. Each has a recommended answer first. Write the letter after each Answer line, or other words. Nothing of steps/plan.md starts before this is answered.

## 1. The order of work

- A, recommended: the order of steps/plan.md. The full old project run, the compare script beside it, then the fixes worst first by the compare, and only when the old project compares right the new project path, roads from 3D solids and the installer.
- B: the compare script first on the one neighbourhood built so far, 01A, then the full run.
- Why A: one full run shows every neighbourhood that stops, and the compare then sorts the fixes by a number, not by 01A alone.

Answer:

## 2. The pass level of the compare

- A, recommended: a neighbourhood compares right when 95 percent of the plots of its past model are built, and of the built plots 95 percent hold each of these: villa position within 5 mm, FFL within 5 mm, rotation within 0.1 degree, mirror right. Floors by type, yard floors, walls and levels within 5 percent of the past count. Yard floors are counted only against the four past models that hold them, IMC, IMD, IME and IMF. The two structural models, IMA and IMB, are held for plots, position, rotation and mirror only.
- B: the same at 1 mm, the pass level the old placer check used.
- Why A: 5 mm and 0.1 degree are the hold bands the repo already measures by (config/project.json, majority.levelHoldMm and rotationHoldDeg). 95 percent leaves room for the plots the team placed by hand, each listed by number in the work root.

Answer:

## 3. Yard floors

- A, recommended: build the yard floor at FFL less 0.1 m, as your word of 24 Oct set it for the past job (floors.yardBelowFflM), and count it against the past models. This answers Q225.
- B: keep rule R410, no yard floor under Rev 06 because the manual gives no level, and leave yard floors out of the compare.
- Why it is asked: 0 yard floors were placed in every run of 6 to 8 Nov, by R410, not by a fault.

Answer:

## 4. Q600, join the two road surfaces

Full text: Q600 in steps/questions-open.md.
- A, recommended: yes. Where the front corners of a neighbourhood took their road level from two road surfaces, the road toposolid joins those two. R485 stays, no ground or grading surface ever stands in for a road.
- B: no toposolid for such a neighbourhood.
- Why it is asked: 01A takes its road levels from two surfaces and the tool makes the toposolid from one.

Answer:

## 5. The repo goes private

- A, recommended: yes, now. 159 tracked files name the client project or its city and 193 the company (`git grep -l -i` for roshn or makkah, and for parsons, on the branch of this form), and the names are in the git history too, so cleaning files would not take them away. Two things to check on the GitHub plan of the account before the switch, both UNKNOWN today: a private repo has a monthly limit of Actions minutes and the Windows jobs of the gate count more than Linux ones, and the required check of main may need a paid plan on a private repo. Say first if the team needs to read it without an invite.
- B: stay public, and a later task takes the names out of the files and writes the history again.

Answer:
