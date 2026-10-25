#!/bin/sh
# PreToolUse hook for Bash, PowerShell and Monitor. From ai-max. Refuses a git commit unless the
# log, steps/log.md by default, differs from the last commit, staged or not. A commit that moves
# the work without moving the log leaves the next session, and the person reading main from a
# phone, guessing where things stand. Exit 2 refuses the call and the line on standard error
# goes back to Claude.
#
# How a commit is read: the command holds git commit, git.exe commit, or git -C or -c before
# commit. Then one git call, git status --porcelain on the log alone, says whether the log
# differs from the last commit. So git add -A && git commit passes when the log was edited and
# not yet staged, and a new repo's first commit passes while the log is still untracked.
#
# WHAT IT CANNOT SEE, named on purpose because it reads words and is not a sandbox: a commit run
# from inside a script, through an alias, or built at run time. A commit that names other paths
# and leaves an edited log unstaged still passes, because the log differs from the last commit.
# An amend refuses like any commit when the log is unchanged. When this folder is not a git
# repository, or git cannot answer, the call goes through, because there is no log to protect.
#
# Starting a program costs about two seconds on a Windows machine, so the call is read with
# read and matched with case, and git starts only for a call that commits.

LOG="steps/log.md"

input=
while IFS= read -r line || [ -n "$line" ]; do
    input="$input$line
"
done

body=${input#*\"tool_input\":}
body=${body%%\"description\":*}

case $body in
    *"git commit"*|*"git.exe commit"*|*"git -C "*" commit"*|*"git -c "*" commit"*) ;;
    *) exit 0 ;;
esac

root="${CLAUDE_PROJECT_DIR:-.}"
changed=$(git -C "$root" status --porcelain -- "$LOG" 2>/dev/null) || exit 0

if [ ! -f "$root/$LOG" ]; then
    printf '%s\n' "Refused. $LOG does not exist and every commit here carries it. Create it with this round's entry at the top, then commit." >&2
    exit 2
fi

if [ -z "$changed" ]; then
    printf '%s\n' "Refused. $LOG is unchanged, and every commit here carries the round's entry. Write it at the top of $LOG, stage it, then commit." >&2
    exit 2
fi

exit 0
