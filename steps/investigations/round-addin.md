# Round ADDIN from Phase 4f, Part 1, the add-in for Revit 2024

Counts and distances only. This file gathers the findings of Round ADDIN from Phase 4f on, phase by phase, each under its own ## heading.

- The plan and the findings of phases 0a to 4e, PR 41 to 55, moved whole in Round REC to steps/history/round-addin/, one file per phase in their old order, with no byte changed (steps/runs/rec/split_proof.txt).
- The plan to finish Part 1 is steps/01_next.md. The design is docs/design/, its README names each part.
- A phase keeps this file under 30 KB. When the next phase would pass that, the closed phases move to steps/history/round-addin/ as files of their own.
- Since Round REC, a finding no real input reaches goes to steps/latent.md with no fix and a PR takes at most 2 review passes, as Bader answered on 2026-10-27 (Q095, Q097 in steps/questions-closed.md).

## 4f, 2026-10-27, PR 61 to 66

Logs in Documents\SiteAdaptation\round_addin\logs, counts only.

- Q089 (Bader): the build proof on the footprint clusters alone with the overrides and keyRules measured again holds 0 unexplained on ima, imb, imc, imd, ime, imf and img, levels 0 at pass, 1 mm and 5 mm (RAN, q089_none.txt, p4fq089_none.txt). The control route on the snapshot analysis gave back the list and rules of main by SHA-256 (RAN, q089_snap.txt). So PR 63 dropped the 22 analysis clusters of 44 plots in Python and C#. One override moved, imd 1338 heightM. On the whole project 2,518 plots written became 2,513, the 5 DP2 plots of Q098 (RAN, p4fq089_whole_compare.txt)
- Parity on the real data, whole project and 7 key covers, net48 and net8.0, 0 one unit cases and 0 failures: PR 64 96 runs, steps clusters to floors (p4f_parity_build_head.txt), PR 65 128 runs, steps clusters to parameters (p4fb_parity_build.txt). The C# of PR 63 is held by both
- The worker read asks about 5.8 GiB free with siteBuild.minFreeGB. The PC held 4.7 to 6.6 GiB, the person's own programs holding the rest, so a read ran only when free memory rose. No floor was lowered
- The steps give values for 44 unit plots the plan does not place (p4f_unplaced.txt, p4fb_unplaced.txt). Not done in 4f: heightOffset and floor z need the site file of button 1, roadAtFrontM in the plan with R364, the past_all.csv rebuild R385 names, linkName of a build (Q099)
- The skill refresh of Bader's addition was skipped: C:\Users\bader\Downloads\ai-max.skill is not on disk, only ai-max-skill.zip of 8 Sep 2026, and C:\Users\bader\.claude\skills\ai-max does not exist (RAN). The repo copy stays version 3. A version 4 SKILL.md of 27 Oct 2026 lies in C:\Users\bader\.claude\skills\synced\, synced from claude.ai, not used for the refresh
- Rules of the Usage section of that version 4 copy (READ, lines 112 to 149) this repo does not follow yet, nothing changed for them: START.md does not end a phase with STATE WAITING in PROGRESS, open questions live in steps/questions-open.md and not steps/02_questions.md, the rule parts hold their measurements inline with no docs/history/, seven of the eight rule parts pass 30 KB (45 to 93 KB, RAN wc), the cap test does not hold START.md under 20 lines, no rule names a phase that costs twice the one before, step 7 of START.md gives no UNKNOWN when npx is missing, and no audit checks the usage row. This session loaded its agents at its start on the old branch at 73b8c42, so its helpers saw the read lists of before Round REC, docs/addin-design.md among them
