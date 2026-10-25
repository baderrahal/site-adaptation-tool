# The setup prompt

The first Claude Code session on a repo, phases 3 to 6 in one round. Sent after phases 1 and 2
are settled in chat. The person has created the repo on GitHub with a README, so main exists.
Claude in chat fills in the brackets and sends it as one code block.

```
Plan the round before writing anything. List what you will do and in what order, then work through it. Do not start the first edit until the plan is written.

ROUND 0: SET UP THE REPO FOR [tool name]

Context
Repo [owner/name], default branch main. [Tool name] is [one sentence]. Done means: [the checks from phase 1].
It runs in [host program and the versions the people who run it have], in [language and framework, as settled in chat on (date)]. [How many people] run it, installed per user with no admin step.
[Name] decides. Claude in chat drafts prompts and reads steps/PROGRESS.md and steps/log.md on main to learn what you did. You do the work here and check it. Do not ask [Name] to run anything you can run.
This session runs [in the cloud / on (machine) with (host) installed]. [What it can and cannot build there.]

Read first
.claude/skills/ai-max/SKILL.md, then its references/phases.md, phases 3 to 6, and references/writing-rules.md.
[If the skill is not in the repo yet: the skill files are at (where), copy them to .claude/skills/ai-max first.]

Do this
1. Scan the repo with the checklist in references/harness-checklist.md, part 1. One line per item, PRESENT, ABSENT or UNKNOWN, with a path. No recommendations in the scan.
2. Branch setup off main.
3. CLAUDE.md from assets/CLAUDE.md.template, under 200 lines, every bracket filled from the context above. A line that cannot be filled says UNKNOWN.
4. The steps folder from assets/steps. PROGRESS.md filled in. 02_questions.md holds the answered questions from chat: [paste the answers, or say none].
5. .gitignore from assets/gitignore.template, .gitattributes from assets/gitattributes.template, .claude/settings.json from assets/settings.json.
6. The four hooks and hook-tests.sh from assets/hooks into .claude/hooks. Set the PROTECTED and LIVE lists for this repo: [the evidence folders, the live project folders]. Run sh .claude/hooks/hook-tests.sh and keep the count.
7. The agents from assets/agents into .claude/agents.
8. [The project layout: for an add-in, src/(Name).Core with no host reference, src/(Name).(Host) with the host calls, tests/(Name).Core.Tests, and the solution file. For a script, its folder and tests.] One real test of one real rule, so the gate has something to run.
9. The test gate from assets/workflow-[dotnet or python].yml into .github/workflows/tests.yml, with the build and test commands set.
10. Run every check. Then try each wall for real in this session: write steps/logs/probe.txt with the file tool, and commit with steps/log.md unchanged. If the file gets written or the commit goes through, undo it at once and record that wall as not firing. Settings load when a session starts, so a wall that does not fire here is UNKNOWN until the next session proves it. Never report a wall as working unless it was seen refusing.
11. Write the Round 0 entry at the top of steps/log.md: the scan table, what was added, every count, what is UNKNOWN.
12. Open the pull request. When the gate is green, merge it yourself, passing the title and the message on a squash merge, and read it back off main. Delete the branch, or name it in PROGRESS.md if the session cannot.

Out of scope
Any feature of the tool beyond the one rule in step 8.

Rules
No generated-by or co-authored-by line, no emoji, no em dash, no semicolon, in any file, commit or pull request. Never report a check that did not run after the last file was written. Say UNKNOWN instead. If a step needs something only [Name] can do, such as a setting on the GitHub web page, write the exact steps under Waiting for the person in PROGRESS.md and carry on with the rest.

Final message to [Name]
Plain and short: what is set up, what is open, what is UNKNOWN, and anything that needs [Name]'s hand.
```
