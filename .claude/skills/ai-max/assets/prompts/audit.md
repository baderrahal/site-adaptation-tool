# The audit prompt

Sent when Claude Code says a round is finished, before anything else moves. It checks the repo
itself, not only the report. Claude in chat fills in the brackets and sends it as one code block.

```
Plan the round before writing anything. List what you will do and in what order, then work through it. Do not start the first edit until the plan is written.

AUDIT OF ROUND [N]: [short goal of that round]

Context
Repo [owner/name]. Round [N] merged as pull request [number], merge commit [sha, if known]. Its entry is at the top of steps/log.md. This round changes no code. It checks round [N] and writes what it found.

Read first, in this order
CLAUDE.md, every file under .claude/rules, steps/PROGRESS.md, the top entry of steps/log.md, .claude/skills/ai-max/SKILL.md.

Do this
1. Pull main. Confirm the merge commit of round [N] is on main and read it back: title, message, files changed. Check the message carries no generated-by or co-authored-by line.
2. Run [the exact build command], [the exact test command] and sh .claude/hooks/hook-tests.sh. Keep every count.
3. Ask breaker to review every file round [N] changed, and reviewer to review the change as a whole.
4. Ask claim-checker to check round [N]'s log entry and its pull request body against the repo and the counts from step 2.
5. Recount every number in the entry yourself. Open every file it names. Mark each claim BACKED, UNBACKED or WRONG with what shows it.
6. Check the steps folder: PROGRESS.md under 60 lines and true to main, 03_your_steps.md at most 30 steps with a Look for line on each, every open question numbered in 02_questions.md.
7. Check for open pull requests and branches already merged. Name them.
8. Write the result as a new entry at the top of steps/log.md titled Audit of round [N]: what was checked, every WRONG and UNBACKED claim with its evidence, every breaker and reviewer finding, and what is fixed in this round versus filed as an item in 01_next.md.
9. Fix only what is wrong in the records, such as a wrong count in the log or a stale PROGRESS.md. A code fault found here becomes an item in 01_next.md, not a fix in this round.
10. Rewrite steps/PROGRESS.md, never append.

Rules
- Branch off main, one pull request, merged by you when the gate is green. On a squash merge, pass the title and the message on the merge call. Never merge red, never leave it open.
- No generated-by or co-authored-by line, no emoji, no em dash, no semicolon.
- Never report a check that did not run. Say UNKNOWN instead.

Final message to [Name]
Plain and short: what held, what was wrong, what is filed for the next round, and anything that needs [Name]'s hand.
```
