# The round prompt

Claude in chat fills in the brackets and sends the whole thing as one code block. Complete every
time, never a fragment of an earlier prompt. Delete a line that does not apply rather than leaving
it empty.

```
Plan the round before writing anything. List what you will do and in what order, then work through it. Do not start the first edit until the plan is written.

ROUND [N]: [short goal]

Context
Repo [owner/name], main at [commit, if known]. The top entry of steps/log.md says [one line].
[Name] decides. Claude in chat drafts this prompt and reads steps/PROGRESS.md and steps/log.md on main to learn what you did. You do the work here and check it. Do not ask [Name] to run anything you can run.

Goal
By the end of the round:
- [an observable outcome]
- [an observable outcome]

Read first, in this order
CLAUDE.md, every file under .claude/rules, steps/PROGRESS.md, .claude/skills/ai-max/SKILL.md, then [files by path].

Do this
1. [one action]
2. [one action]
3. [one action]

Out of scope
[what not to touch, such as another task's folders or the shared folder]

Checks that must pass
[the exact build command]
[the exact test command]
sh .claude/hooks/hook-tests.sh

Rules
- Branch off a fresh pull of main. Nothing is committed or pushed on main.
- Run the full checks above after the last file is written. Keep the counts before and after.
- Ask breaker to review every file you changed and reviewer to review the change. Draft the log entry, then ask claim-checker to check it against the repo. Fix every WRONG or UNBACKED claim.
- Write the round's entry at the top of steps/log.md in the shape the file already uses: Goal, Done, Checked, UNKNOWN, Remains, Known bugs, Next, Your turn. Numbers only from runs made after the last file was written.
- Rewrite steps/PROGRESS.md, never append. STATE WAITING with what it waits for, or OPEN if work continues.
- Where the host side changed, rewrite steps/03_your_steps.md: at most 30 steps, one action each, the exact command in a code block, a Look for line, and what to send back.
- A question for [Name] goes in steps/02_questions.md, numbered after the last one. Carry on with everything it does not block.
- Commit messages and the pull request body go in a file, passed with -F and --body-file. No generated-by or co-authored-by line, no emoji, no em dash, no semicolon.
- One pull request. When the gate is green and nothing from the checkers is open, merge it yourself. On a squash merge, pass the title and the message on the merge call. Read the merge commit back off main. Never merge red. Never leave it open. Do not open a second while one is open.
- Delete the branch. If the session cannot, name it under Waiting for the person in PROGRESS.md.
- If the same finding survives three attempts, stop and write what was tried and what each run showed.
- Name anything recurring you start, such as a scheduled check-in, and remove it.
- Never report a check that did not run. Say UNKNOWN instead.
- If this round is too large for one pull request, split it and say so. Never quietly do half.

Final message to [Name]
Plain and short: what is done, what is open, what is UNKNOWN, and anything that needs [Name]'s hand.
```
