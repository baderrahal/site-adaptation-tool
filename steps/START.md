Plan the round before writing anything. List what you will do and in what order, then work through it. Do not start the first edit until the plan is written.

ROUND ADDIN, the next phase of Part 1

Repo baderrahal/site-adaptation-tool. Bader decides, chat drafts and reads, Code does and checks.

1. Read CLAUDE.md, then steps/PROGRESS.md, then the first phase marked open in steps/01_next.md, and nothing else at start.
2. Preflight by items 1 and 3 of Start of every session in CLAUDE.md. If the session did not start in the repo folder, or main is not level with origin, stop and say so.
3. Run that one phase and no other, under the rules of CLAUDE.md. Read its docs/design/ parts and its rules by id, never a whole file over 30 KB.
4. Each PR: at most 4 changes, the gate, the checkers CLAUDE.md names, at most 2 review passes. A REAL finding is fixed with a test, a LATENT one goes to steps/latent.md.
5. Merge on green with gh pr checks --watch, then delete the branch and pull main. Never two PRs open.
6. At the end of the phase: mark it done in steps/01_next.md, write the top entry of steps/log.md, rewrite steps/PROGRESS.md to the caps of R400, merge, and stop.
7. After the merge, run npx ccusage@latest session --since <today as YYYYMMDD> and add one row for this session to steps/usage.md, total tokens and cost only.

Final message to Bader: plain and short, the PR numbers, the checks with their counts, what is UNKNOWN, and every new question by ID.
