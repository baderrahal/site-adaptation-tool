# Tasks

One file per task, written by /round, done by /task in the lane it names. The shape is in .claude/skills/round/SKILL.md.

A lane's current task is the file with `status: open` and `lane:` equal to its folder name. The Edit and Write hook and the folder rules check both read its `allowed` list. A helper in its own worktree has another folder name, so it needs its own task or works in the lane's folder.
