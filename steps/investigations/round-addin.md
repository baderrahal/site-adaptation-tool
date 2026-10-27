# Round ADDIN from Phase 4f, Part 1, the add-in for Revit 2024

Counts and distances only. This file gathers the findings of Round ADDIN from Phase 4f on, phase by phase, each under its own ## heading.

- The plan and the findings of phases 0a to 4e, PR 41 to 55, moved whole in Round REC to steps/history/round-addin/, one file per phase in their old order, with no byte changed (steps/runs/rec/split_proof.txt).
- The plan to finish Part 1 is steps/01_next.md. The design is docs/design/, its README names each part.
- A phase keeps this file under 30 KB. When the next phase would pass that, the closed phases move to steps/history/round-addin/ as files of their own.
- Since Round REC, a finding no real input reaches goes to steps/latent.md with no fix and a PR takes at most 2 review passes, as Bader answered on 2026-10-27 (Q095, Q097 in steps/questions-closed.md).

## 4f, 2026-10-27, PR 61 to 66

Logs in Documents\SiteAdaptation\round_addin\logs, counts only.

- Q089 (Bader): the build proof on the footprint clusters alone with the overrides and keyRules measured again holds 0 unexplained on ima, imb, imc, imd, ime, imf and img, levels 0 at pass, 1 mm and 5 mm (RAN, q089_none.txt, p4fq089_none.txt). The control route on the snapshot analysis gave back the list and rules of main by SHA-256 (RAN, q089_snap.txt). So PR 63 dropped the analysis clusters in Python and C#, 22 of 44 plots by the Q089 measure of 4e, 21 of 42 plots in the build of the whole project, 567 clusters to 546 (RAN, p4fq089_whole_compare.txt). One override moved, imd 1338 heightM. On the whole project 2,518 plots written became 2,513, the 5 DP2 plots of Q098 (RAN, p4fq089_whole_compare.txt)
- Parity on the real data, whole project and 7 key covers, net48 and net8.0, 0 one unit cases and 0 failures: PR 64 96 runs, steps clusters to floors (p4f_parity_build_head.txt), PR 65 128 runs, steps clusters to parameters (p4fb_parity_build.txt). The C# of PR 63 is held by both
- The worker read asks about 5.8 GiB free with siteBuild.minFreeGB. The logs give 3,722 to 7,099 MiB free over the runs, 3.6 to 6.9 GiB, the person's own programs holding the rest, so a read ran only when free memory rose. No floor was lowered
- The steps give values for 44 unit plots the plan does not place (p4f_unplaced.txt, p4fb_unplaced.txt). Not done in 4f: heightOffset and floor z need the site file of button 1, roadAtFrontM in the plan with R364, the past_all.csv rebuild R385 names, linkName of a build (Q099)
- The skill refresh of Bader's addition was skipped: C:\Users\bader\Downloads\ai-max.skill was not on disk when checked, only ai-max-skill.zip of 8 Sep 2026. It appeared at 16:35, after this section was first written, and its refresh is PR 1 of Round AUDIT-RULES, and C:\Users\bader\.claude\skills\ai-max does not exist (RAN). The repo copy stays version 3. A version 4 SKILL.md of 27 Oct 2026 lies in C:\Users\bader\.claude\skills\synced\, synced from claude.ai, not used for the refresh
- Rules of the Usage section of that version 4 copy (READ, lines 112 to 149) this repo does not follow yet, nothing changed for them: START.md does not end a phase with STATE WAITING in PROGRESS, open questions live in steps/questions-open.md and not steps/02_questions.md, the rule parts hold their measurements inline with no docs/history/, seven of the eight rule parts pass 30 KB (44,927 to 92,952 bytes, RAN wc), the cap test does not hold START.md under 20 lines, no rule names a phase that costs twice the one before, step 7 of START.md gives no UNKNOWN when npx is missing, and no audit checks the usage row. The agent descriptions this session showed name docs/addin-design.md, as .claude/agents does at 73b8c42, the branch the session started on, and not at main (READ)

## 4g1, 2026-10-27, PR 69 to 73

Logs in Documents\SiteAdaptation\round_addin\logs, counts only.

- Q098 to Q104 closed with Bader's answers of 2026-10-27, 4g split into 4g1 and 4g2, the chip and sheet Assumed in 7.7 of the mock (PR 69)
- Q098 (PR 70): a cluster unit whose file has no table of hand cluster takes the table of hand alone. The 5 DP2 plots 859, 1767, 1768, 2959, 2960 take it and still get no hand, as a cluster unit has no row pair, so a build still writes 2,513 plots (RAN, p4g1_q098_second.txt). That is Q105
- Q099 (PR 70): linkName of a build equals RRE_ZZ_SP_RVT-Name on 2,513 of 2,513 written plots (RAN, p4g1_q098_second.txt). Parity at 592e1d8 128 of 128 lines with 0 failures, the build proof 0 unexplained on the 7 keys (RAN, p4g1_q098_parity_head.txt, p4g1_q098_route.txt)
- The status block of R401 (PR 71): 595 values, CHOSEN by Bader 3, MEASURED 50, DOCUMENT 273, RECOMMENDED 11, CHOSEN by Code 16, UNKNOWN 242, equal by second_check.ps1 (RAN). The audit of PR 68 had 591 values with UNKNOWN 528. Bader's review list of Q104 is steps/investigations/status-review.md, 41 rows
- The status of each plotted value (PR 72): parity at e37c33e 144 of 144 lines with 0 failures on the whole project and the 7 keys, net48 and net8.0, the plans equal to main less the status, 105,861 values, and a build with no override applied: 20,026 values differ and all carry the override status (RAN, p4g1_status_*.txt)
- Whole project, 2,513 written plots: plots holding each status DOCUMENT 2,513, MEASURED strong 2,513, MEASURED weak 2,094, RECOMMENDED 2,355, UNKNOWN 2,513. Every plot holds an assumed value, prototypeFile and the level elevation of each floor being unmapped (RAN)
- Questions opened: Q105 the row pair of the 5 DP2 cluster units, Q106 the step values of the schedules as DOCUMENT, Q107 the status detail of values built from others
- Each PR took two review passes of the breaker and the claim-checker. The fixes after the second pass of PR 3 and PR 4 had no third pass, as Q097 allows two
