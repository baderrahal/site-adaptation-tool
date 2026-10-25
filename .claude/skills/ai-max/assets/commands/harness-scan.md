---
description: Scan this repo's harness item by item, evidence only, at most 40 lines
---

Run the scan in .claude/skills/ai-max/references/harness-checklist.md, part 1, against this repo as
it is right now.

One line per item: ITEM | PRESENT or ABSENT or UNKNOWN | path | one short fact.

- A path for anything PRESENT. A claim without a path is UNKNOWN
- ABSENT means looked for and not found. A check that could not run is UNKNOWN
- Answer an item with the command that shows it, such as git status, git branch, gh pr list or a
  line count, rather than reading about it
- No recommendations. This is a report
- Branch state last

End with one line naming what matters most for this tool. At most 40 lines in all.
