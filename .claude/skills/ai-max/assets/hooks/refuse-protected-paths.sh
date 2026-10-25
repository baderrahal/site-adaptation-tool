#!/bin/sh
# PreToolUse hook for Edit, Write, MultiEdit, NotebookEdit, Bash, PowerShell and Monitor.
# From ai-max. Reads the tool call off standard input. Exit 2 refuses the call, and the line
# on standard error goes back to Claude.
#
# Two walls.
#
# 1. A file tool never changes a file under a PROTECTED path. The defaults are the evidence
#    folders, steps/logs and steps/runs, and the walls themselves, .claude/hooks and
#    .claude/settings, so a file tool cannot switch them off. A change to a wall is written
#    somewhere else, proved with hook-tests.sh, and copied in with a command. Commands are
#    not judged by this list, because evidence is copied in by a command on purpose.
#
# 2. A LIVE folder, such as the ACC Desktop Connector folder that syncs to the client's CDE,
#    is never written by a file tool and never named in a command at all. Whatever the tool
#    needs from it is copied out by one script that finds the folder itself.
#
# Edit the two lists for this repo, one entry per line. A PROTECTED entry ending in / is a
# folder and matches only that folder. One without a trailing / matches any name it starts,
# so .claude/settings covers settings.json and settings.local.json. A LIVE entry matches
# anywhere in the path or the command. Matching ignores case and reads / and \ alike.
#
# WHAT IT CANNOT SEE, named on purpose because it reads words and is not a sandbox: a script
# that builds a path at run time, a Windows short name such as ACCDOC~1, and a command that
# writes a file it never names. Older Desktop Connector installs used DC\Autodesk Docs and
# DC\BIM 360, add them to LIVE where they exist. It is a wall against a mistake.
#
# WHY BUILTINS. Starting a program costs about two seconds on a Windows machine, so the call
# is read with read and matched with case, and no program starts at all.

PROTECTED='
steps/logs/
steps/runs/
.claude/hooks/
.claude/settings
'

LIVE='
ACCDocs
'

input=
while IFS= read -r line || [ -n "$line" ]; do
    input="$input$line
"
done

refuse() {
    printf '%s\n' "Refused. $1" >&2
    exit 2
}

# ci WORD sets CI to a glob matching WORD in any case, with / matching / or \.
ci() {
    w=$1
    CI=
    while [ -n "$w" ]; do
        c=${w%"${w#?}"}
        w=${w#?}
        case $c in
            a|A) c='[aA]' ;; b|B) c='[bB]' ;; c|C) c='[cC]' ;; d|D) c='[dD]' ;;
            e|E) c='[eE]' ;; f|F) c='[fF]' ;; g|G) c='[gG]' ;; h|H) c='[hH]' ;;
            i|I) c='[iI]' ;; j|J) c='[jJ]' ;; k|K) c='[kK]' ;; l|L) c='[lL]' ;;
            m|M) c='[mM]' ;; n|N) c='[nN]' ;; o|O) c='[oO]' ;; p|P) c='[pP]' ;;
            q|Q) c='[qQ]' ;; r|R) c='[rR]' ;; s|S) c='[sS]' ;; t|T) c='[tT]' ;;
            u|U) c='[uU]' ;; v|V) c='[vV]' ;; w|W) c='[wW]' ;; x|X) c='[xX]' ;;
            y|Y) c='[yY]' ;; z|Z) c='[zZ]' ;;
            /|\\) c='[/\\]' ;;
            '*'|'?'|'['|']') c="[$c]" ;;
        esac
        CI=$CI$c
    done
}

# names_live TEXT returns 0 when TEXT names any LIVE folder.
names_live() {
    text=$1
    old_ifs=$IFS
    IFS='
'
    set -f
    for entry in $LIVE; do
        ci "$entry"
        case $text in
            *$CI*) set +f; IFS=$old_ifs; return 0 ;;
        esac
    done
    set +f
    IFS=$old_ifs
    return 1
}

# The tool is read off the tool_name key. Claude Code sends it before tool_input, and a key is
# a quoted word followed by a colon, which no string value can fake, because a quote inside a
# value arrives escaped.
t=${input#*\"tool_name\":}
[ "$t" = "$input" ] && exit 0
t=${t#*\"}
tool=${t%%\"*}

case $tool in
    Bash|PowerShell|Monitor)
        # Claude Code sends the command before its description, so everything before the
        # description is searched, and a description can neither trip the wall nor hide a
        # command from it.
        body=${input%%\"description\":*}
        case $body in *\"command\":*) ;; *) body=$input ;; esac
        names_live "$body" || exit 0
        refuse "This command names a live project folder, which this repo never touches from a command. Copy what is needed out of it with the one script made for that. See .claude/hooks/refuse-protected-paths.sh."
        ;;
    Edit|Write|MultiEdit|NotebookEdit) ;;
    *) exit 0 ;;
esac

# A file tool. The path is the value of file_path, or notebook_path. The content is never
# searched, so a file may talk about a protected folder and still be written.
p=${input#*\"file_path\":}
[ "$p" = "$input" ] && p=${input#*\"notebook_path\":}
[ "$p" = "$input" ] && exit 0
p=${p#*\"}
path=${p%%\"*}
[ -z "$path" ] && exit 0

# JSON writes a backslash as two, so a value cut at the first quote that ends in an odd number
# of backslashes ended on an escaped quote. A path holding a quote is refused rather than half
# read.
tail=$path
while :; do
    case $tail in
        *'\\') tail=${tail%??} ;;
        *) break ;;
    esac
done
case $tail in
    *'\') refuse "The path in this call holds a quote, so it cannot be read whole." ;;
esac

# Every escaped backslash becomes a slash, so a Windows path and a Unix path read alike.
rest=$path
norm=
while :; do
    case $rest in
        *'\\'*) norm=$norm${rest%%'\\'*}/; rest=${rest#*'\\'} ;;
        *) norm=$norm$rest; break ;;
    esac
done

names_live "$norm" && refuse "$path is under a live project folder, which a file tool never writes. See .claude/hooks/refuse-protected-paths.sh."

old_ifs=$IFS
IFS='
'
set -f
for entry in $PROTECTED; do
    ci "$entry"
    case "/$norm" in
        */$CI*)
            set +f
            IFS=$old_ifs
            refuse "$path is protected in this repo. Evidence is never edited, and a wall is changed by writing the change elsewhere, proving it with hook-tests.sh and copying it in with a command. See CLAUDE.md."
            ;;
    esac
done
set +f
IFS=$old_ifs

exit 0
