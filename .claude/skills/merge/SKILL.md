---
name: merge
description: Announces a merge, waits for green checks, merges and deletes the branch. Never leaves a PR waiting.
---

# Merge

Argument: the PR number, or the PR of this branch.

1. Run `pwsh scripts/status.ps1 -Note "merging <branch> now"`, so every lane sees it in steps/STATUS.md. If the ListAgents tool shows other running lanes, also send each the line "merging <branch> now" with SendMessage.
2. Run `gh pr checks <number> --watch`. If a check fails, read its log and fix it now, then come back here. Do not leave the PR waiting and do not open a second PR.
3. Run `gh pr merge <number> --merge`, without --delete-branch, it once cost a worktree its git metadata.
4. Run `git push origin --delete <branch>`.
5. Run `pwsh scripts/status.ps1` to refresh the status.

Show the merge commit from `gh pr view <number> --json mergeCommit,state`. Never bypass a check and never skip a hook.
