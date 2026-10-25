#!/bin/sh
# Proves the walls in .claude/hooks refuse what they should and let through what they should.
# From ai-max. Run it from anywhere in the repo:
#
#     sh .claude/hooks/hook-tests.sh
#
# The test gate runs it before the build and fails when a case fails or when zero cases ran,
# because a check that checks nothing reports green forever. Each case feeds a hook the JSON
# Claude Code sends and reads its answer. A scratch git repo stands in for this one, so nothing
# in this repo is touched.
#
# The protected and live folder names below are test data. When a wall's lists change for this
# repo, change the cases here in the same commit, then run this before copying the wall in.
#
# Passing here is not the whole proof. Settings load when a session starts, so the first
# session after the walls land also tries one refusal of each for real.

hooks=$(cd "$(dirname "$0")" && pwd)
scratch=$(mktemp -d)
verdicts=$(mktemp)
trap 'rm -rf "$scratch" "$verdicts"' EXIT INT TERM

# Every case runs at the end of a pipe, which is a subshell, so a counter kept in a variable
# would be lost and the total would read low. Each verdict is a line in a file, and the totals
# are counted off the file at the end.
answer() {
    # $1 case name, $2 wanted, $3 got
    if [ "$3" = "$2" ]; then
        printf '  ok    %s\n' "$1" | tee -a "$verdicts"
    else
        printf '  FAIL  %s, wanted %s and got %s\n' "$1" "$2" "$3" | tee -a "$verdicts"
    fi
}

run() {
    # $1 hook file, $2 case name, $3 exit wanted. The JSON comes on standard input.
    json=$(cat)
    printf '%s' "$json" | CLAUDE_PROJECT_DIR="$scratch" ${HOOK_SHELL:-sh} "$hooks/$1" >/dev/null 2>&1
    answer "$2" "exit $3" "exit $?"
}

gate() {
    # $1 case name, $2 block or allow. The JSON comes on standard input.
    json=$(cat)
    out=$(printf '%s' "$json" | CLAUDE_PROJECT_DIR="$scratch" ${HOOK_SHELL:-sh} "$hooks/stop-gate.sh" 2>/dev/null)
    code=$?
    got=allow
    case $out in *'"decision": "block"'*) got=block ;; esac
    [ "$code" = 0 ] || got="exit $code"
    answer "$1" "$2" "$got"
}

file_call() {
    # $1 tool, $2 key, $3 path written the way JSON carries it
    printf '{"session_id":"t","hook_event_name":"PreToolUse","tool_name":"%s","tool_input":{"%s":"%s","content":"x"}}' "$1" "$2" "$3"
}

command_call() {
    # $1 tool, $2 command written the way JSON carries it, $3 description
    printf '{"session_id":"t","hook_event_name":"PreToolUse","tool_name":"%s","tool_input":{"command":"%s","description":"%s"}}' "$1" "$2" "$3"
}

stop_call() {
    # $1 true or false
    printf '{"session_id":"t","hook_event_name":"Stop","stop_hook_active":%s}' "$1"
}

# A scratch repo on main with one commit. Its own empty hooks folder, so a pre-commit set on
# this machine never runs here.
git -C "$scratch" init -q
git -C "$scratch" symbolic-ref HEAD refs/heads/main
git -C "$scratch" config user.name "hook tests"
git -C "$scratch" config user.email "hook-tests@example.invalid"
git -C "$scratch" config commit.gpgsign false
git -C "$scratch" config core.hooksPath "$scratch/.no-hooks"
mkdir -p "$scratch/steps"
printf '# Log\n' > "$scratch/steps/log.md"
printf 'start\n' > "$scratch/a.txt"
git -C "$scratch" add -A
git -C "$scratch" commit -q -m start

P=refuse-protected-paths.sh
G=refuse-git-on-main.sh
L=require-log.sh

echo "the paths wall, file tools"
file_call Write file_path 'steps/logs/run.log' | run $P "Write under steps/logs, relative" 2
file_call Write file_path '/home/me/repo/steps/runs/01/a.txt' | run $P "Write under steps/runs, absolute" 2
file_call Edit file_path 'C:\\Users\\me\\repo\\steps\\logs\\run.log' | run $P "Edit under steps/logs, Windows spelling" 2
file_call Write file_path '.claude/hooks/new.sh' | run $P "Write a hook" 2
file_call Write file_path 'C:\\Users\\me\\repo\\.claude\\settings.json' | run $P "Write the settings" 2
file_call Write file_path '.claude/settings.local.json' | run $P "Write the local settings" 2
file_call Write file_path 'STEPS/Logs/run.log' | run $P "Write under steps/logs, other case" 2
file_call NotebookEdit notebook_path 'steps/runs/a.ipynb' | run $P "NotebookEdit under steps/runs" 2
file_call Write file_path 'C:\\Users\\me\\DC\\ACCDocs\\Parsons\\a.txt' | run $P "Write under the live folder" 2
file_call Write file_path 'C:\\a\\b\"c.txt' | run $P "Write a path holding a quote" 2
file_call MultiEdit file_path 'src/Tool.Core/Rule.cs' | run $P "MultiEdit a source file" 0
file_call Write file_path 'steps/logsheet.md' | run $P "Write a near miss of steps/logs" 0
file_call Write file_path 'steps/log.md' | run $P "Write the log itself" 0
file_call Write file_path 'C:\\Users\\me\\repo\\steps\\PROGRESS.md' | run $P "Write the page, Windows spelling" 0
file_call Read file_path 'C:\\Users\\me\\DC\\ACCDocs\\a.txt' | run $P "Read is not judged" 0

echo
echo "the paths wall, commands"
command_call Bash 'dir \"C:\\Users\\me\\DC\\ACCDocs\"' 'list a folder' | run $P "Bash naming the live folder" 2
command_call PowerShell 'Get-ChildItem C:\\Users\\me\\DC\\accdocs' 'list a folder' | run $P "PowerShell naming the live folder, other case" 2
command_call Bash 'cp out.log steps/runs/01/' 'copy evidence in' | run $P "Bash copying evidence in" 0
command_call Bash 'ls' 'mentions ACCDocs only in the description' | run $P "Description naming the live folder" 0

echo
echo "the branch wall, on main"
command_call Bash 'git commit -m x' 'commit' | run $G "commit on main" 2
command_call Bash 'git push origin main' 'push' | run $G "push to main" 2
command_call Bash 'git merge fix-1' 'merge' | run $G "merge on main that is not ff-only" 2
command_call PowerShell 'git.exe commit -m x' 'commit' | run $G "git.exe commit on main" 2
command_call Bash 'git pull --ff-only' 'pull' | run $G "pull --ff-only on main" 0
command_call Bash 'git status' 'status' | run $G "status on main" 0
command_call Bash 'git checkout -b fix-1 && git commit -m x' 'branch then commit' | run $G "new branch then commit" 0

git -C "$scratch" checkout -q -b fix-2

echo
echo "the branch wall, on a branch"
command_call Bash 'git commit -m x' 'commit' | run $G "commit on a branch" 0
command_call Bash 'git push -u origin fix-2' 'push' | run $G "push the branch" 0
command_call Bash 'git push origin fix-2:main' 'push' | run $G "push the branch onto main" 2
command_call Bash 'git push --all' 'push' | run $G "push --all" 2
command_call Bash 'git status\ngit push origin main' 'two calls' | run $G "second call on a new line pushes to main" 2
command_call Bash 'git log --oneline' 'log' | run $G "log on a branch" 0

echo
echo "the log wall"
command_call Bash 'git commit -m x' 'commit' | run $L "commit with the log unchanged" 2
command_call Bash 'git -C sub commit -m x' 'commit' | run $L "git -C commit with the log unchanged" 2
printf '## Round 1\n' >> "$scratch/steps/log.md"
command_call Bash 'git add -A && git commit -m x' 'commit' | run $L "add then commit with the log edited" 0
git -C "$scratch" add steps/log.md
command_call Bash 'git commit -m x' 'commit' | run $L "commit with the log staged" 0
command_call Bash 'git log --oneline' 'log' | run $L "a git call that is not a commit" 0
command_call Bash 'echo hello' 'echo' | run $L "a command that is not git" 0
git -C "$scratch" commit -q -m round-1
rm "$scratch/steps/log.md"
command_call Bash 'git commit -am x' 'commit' | run $L "commit with the log deleted" 2
git -C "$scratch" checkout -q -- steps/log.md

echo
echo "the stop gate"
stop_call false | gate "no page" allow
printf 'STATE OPEN, 2026-10-13 09:00\n' > "$scratch/steps/PROGRESS.md"
stop_call false | gate "OPEN, first stop" block
stop_call true | gate "OPEN, already sent back once" allow
printf 'STATE WAITING, 2026-10-13 09:00\n' > "$scratch/steps/PROGRESS.md"
stop_call false | gate "WAITING" allow
printf '# Progress\n\n**STATE:** Open, 2026-10-13 09:00\n' > "$scratch/steps/PROGRESS.md"
stop_call false | gate "OPEN with bold and a colon" block
printf 'STATE OPEN, 2026-10-13 09:00\r\n' > "$scratch/steps/PROGRESS.md"
stop_call false | gate "OPEN with a Windows line ending" block
printf '1\n2\n3\n4\n5\n6\n7\n8\n9\n10\nSTATE OPEN\n' > "$scratch/steps/PROGRESS.md"
stop_call false | gate "STATE below line ten" allow
printf 'STATE 2026-10-13 OPEN\n' > "$scratch/steps/PROGRESS.md"
stop_call false | gate "STATE with no word after it" allow

echo
echo "line endings"
cr=$(printf '\r')
for f in "$hooks"/*.sh; do
    if grep -q "$cr" "$f"; then
        answer "$(basename "$f") has LF line endings" "LF" "CRLF"
    else
        answer "$(basename "$f") has LF line endings" "LF" "LF"
    fi
done

passed=$(grep -c '^  ok ' "$verdicts")
failed=$(grep -c '^  FAIL ' "$verdicts")
echo
echo "$passed passed, $failed failed"
[ "$failed" = 0 ] && [ "$passed" -gt 0 ]
