# Tasks

One file per task, written by /round, done by /task in the lane it names. The shape is in .claude/skills/round/SKILL.md.

A lane's current task is the file with `status: open` and `lane:` equal to its folder name, as it stands on origin/main, so `status: done` on the branch takes effect at the merge. The Edit and Write hook and the folder rules check both read its `allowed` list from main. A lane holds one open task at a time. A later one takes `status: waiting` until the lead opens it. If two are open, the one its branch `task/<id>` names wins, else the first by name. A helper in its own worktree has another folder name, so it needs its own task or works in the lane's folder.
