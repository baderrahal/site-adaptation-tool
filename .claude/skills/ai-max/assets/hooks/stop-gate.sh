#!/bin/sh
# Stop hook. From ai-max, a short form of the gate first proved in the NWC Federator repo.
# While steps/PROGRESS.md reads STATE OPEN, it sends the session back once before it stops, so a
# session cannot end with the work half done, the page stale and the person reading main misled.
#
# A Stop hook gets JSON on standard input carrying stop_hook_active, which is true when the
# session is already carrying on because a Stop hook sent it back. It blocks by exiting 0 with
# {"decision": "block", "reason": "..."} on standard output, and the reason goes back to Claude.
# This blocks only while stop_hook_active is false, so it sends a session back once per stop and
# can never trap it.
#
# ONLY OPEN BLOCKS. WAITING, CLOSED, any other word, no STATE line and no page all let the stop
# through, because a gate that cannot read the state must never be what traps a session. The
# STATE line is looked for in the first ten lines, read without regard to case, asterisks,
# colons or backticks, so STATE OPEN, 2026-10-13 09:00 and **STATE:** Open both read OPEN.
#
# Its limits, said out loud. It reads what the page says and does not check that the page is
# true, the audit does that. And a copy checked out with Windows line endings fails on its
# first line with exit 2, which blocks every stop, so .gitattributes pins this file to LF and
# hook-tests.sh checks it.
#
# BUILTINS ONLY. No program starts.

input=
while IFS= read -r line || [ -n "$line" ]; do
    input="$input$line
"
done

case $input in
    *'"stop_hook_active":true'*|*'"stop_hook_active": true'*) exit 0 ;;
esac

page="${CLAUDE_PROJECT_DIR:-.}/steps/PROGRESS.md"
[ -f "$page" ] || exit 0

state=
n=0
while [ "$n" -lt 10 ] && { IFS= read -r line || [ -n "$line" ]; }; do
    n=$((n + 1))
    line=${line%[[:cntrl:]]}
    while :; do
        case $line in
            *'*'*) line=${line%%'*'*}${line#*'*'} ;;
            *':'*) line=${line%%':'*}${line#*':'} ;;
            *'`'*) line=${line%%'`'*}${line#*'`'} ;;
            *) break ;;
        esac
    done
    line=${line#"${line%%[![:space:]]*}"}
    case $line in
        [Ss][Tt][Aa][Tt][Ee][[:space:]]*) ;;
        *) continue ;;
    esac
    word=${line#?????}
    word=${word#"${word%%[![:space:]]*}"}
    word=${word%%[![:alpha:]]*}
    [ -n "$word" ] || continue
    state=$word
    break
done < "$page"

case $state in
    [Oo][Pp][Ee][Nn]) ;;
    *) exit 0 ;;
esac

printf '%s\n' '{"decision": "block", "reason": "steps/PROGRESS.md reads STATE OPEN. Before stopping, push and merge what is finished, then rewrite the page, never append: STATE WAITING and what it waits for, or STATE CLOSED. Or carry on from the first step of Next."}'
exit 0
