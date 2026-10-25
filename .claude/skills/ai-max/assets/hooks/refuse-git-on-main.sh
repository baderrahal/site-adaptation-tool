#!/bin/sh
# PreToolUse hook for Bash, PowerShell and Monitor. From ai-max, first proved in the NWC
# Federator repo. Reads the tool call off standard input and refuses git making a commit on
# main or master, and any push that lands on main or master from any branch. Every change
# goes on its own branch and reaches main through a merged pull request. Exit 2 refuses the
# call and the line on standard error goes back to Claude.
#
# THE COMMAND IS JUDGED ONE GIT CALL AT A TIME. It is split where the shell splits it, on
# a newline, a semicolon, &&, ||, | and &, outside quotes, and each git call is read on its own
# words, quotes joined the way the shell joins them. Per call:
#
#   commit, cherry-pick, revert, am, rebase, commit-tree   refused while on main
#   merge and pull                                         refused while on main, unless --ff-only
#   push                                                   refused when a refspec lands on main or
#                                                          master, or deletes it, or --all or
#                                                          --mirror, from any branch, and refused
#                                                          while on main unless it only deletes
#                                                          another branch
#
# "On main" starts as the branch checked out and follows a checkout or switch to main, or
# away from it, earlier in the same command, because the branch is read before it runs.
#
# Earlier versions judged the whole command at once, so a -d or a --ff-only anywhere in it
# excused a push or a merge anywhere in it, and a newline, a semicolon or a quote after main
# hid the word. Per call reading is the answer to all of them.
#
# WHAT IT CANNOT SEE, named on purpose because it reads words and is not a sandbox: git run
# from inside a script, git or its verb built at run time, with $(...), backticks or
# PowerShell such as git @('commit'), and a git alias such as ci for commit. And when this
# folder is not a git repository, or git cannot say which branch is out, it lets the call
# through, because there is no main here to protect.
#
# Starting a program costs about two seconds on a Windows machine, so the call is read with
# read and matched with case first. A call that does not say git and a verb that could write
# leaves before anything is started. git status, git log and git diff start nothing.

input=
while IFS= read -r line || [ -n "$line" ]; do
    input="$input$line
"
done

body=${input#*\"tool_input\":}

case "$body" in
    *[Gg][Ii][Tt]*) ;;
    *) exit 0 ;;
esac
case "$body" in
    *[Cc][Oo][Mm][Mm][Ii][Tt]*|*[Pp][Uu][Ss][Hh]*|*[Mm][Ee][Rr][Gg][Ee]*|*[Pp][Uu][Ll][Ll]*) ;;
    *[Cc][Hh][Ee][Rr][Rr][Yy]*|*[Rr][Ee][Vv][Ee][Rr][Tt]*|*[Rr][Ee][Bb][Aa][Ss][Ee]*|*" "[Aa][Mm]*) ;;
    *) exit 0 ;;
esac

cd "${CLAUDE_PROJECT_DIR:-.}" 2>/dev/null || exit 0
branch=$(git rev-parse --abbrev-ref HEAD 2>/dev/null)

why=$(printf '%s' "$body" | awk -v branch="$branch" '
function decode(s,    out, i, n, c) {
    out = ""; n = length(s)
    for (i = 1; i <= n; i++) {
        c = substr(s, i, 1)
        if (c == "\\" && i < n) {
            i++; c = substr(s, i, 1)
            if (c == "n" || c == "r") out = out "\n"
            else if (c == "t") out = out " "
            else out = out c
        } else out = out c
    }
    return out
}
function tokens(s, w,    n, i, c, q, cur, have) {
    n = 0; q = ""; cur = ""; have = 0
    for (i = 1; i <= length(s); i++) {
        c = substr(s, i, 1)
        if (q != "") { if (c == q) q = ""; else cur = cur c; continue }
        if (c == "\"" || c == "\047") { q = c; have = 1; continue }
        if (c == " " || c == "\t") { if (have) { w[++n] = cur; cur = ""; have = 0 } continue }
        cur = cur c; have = 1
    }
    if (have) w[++n] = cur
    return n
}
{ buf = buf $0 "\n" }
END {
    i = index(buf, "\"command\":"); if (!i) exit
    rest = substr(buf, i + 10); sub(/^[ \t]*"/, "", rest)
    raw = ""; esc = 0
    for (k = 1; k <= length(rest); k++) {
        c = substr(rest, k, 1)
        if (esc) { raw = raw c; esc = 0; continue }
        if (c == "\\") { raw = raw c; esc = 1; continue }
        if (c == "\"") break
        raw = raw c
    }
    cmd = decode(raw)

    # Split into calls outside quotes.
    nseg = 0; cur = ""; q = ""
    for (k = 1; k <= length(cmd); k++) {
        c = substr(cmd, k, 1)
        if (q != "") { if (c == q) q = ""; cur = cur c; continue }
        if (c == "\"" || c == "\047") { q = c; cur = cur c; continue }
        if (c == "\n" || c == ";" || c == "&" || c == "|") { seg[++nseg] = cur; cur = ""; continue }
        cur = cur c
    }
    seg[++nseg] = cur

    onmain = (branch == "main" || branch == "master")
    for (s = 1; s <= nseg; s++) {
        split("", w); nt = tokens(seg[s], w)
        g = 0
        for (j = 1; j <= nt; j++) { x = tolower(w[j]); if (x ~ /(^|[\\\/])git(\.exe)?$/) { g = j; break } }
        if (!g) continue
        j = g + 1; verb = ""
        while (j <= nt) {
            x = w[j]
            if (x == "-C" || x == "-c" || x == "--git-dir" || x == "--work-tree" || x == "--namespace") { j += 2; continue }
            if (x ~ /^-/) { j++; continue }
            verb = tolower(x); break
        }
        if (verb == "") continue

        if (verb == "checkout" || verb == "switch") {
            # A lone dash, or @{-1}, is the branch that was out before, which this cannot
            # read, so it is taken to be main. The safe mistake is a refusal.
            newbranch = 0; first = ""; prevdash = 0
            for (k = j + 1; k <= nt; k++) {
                x = w[k]
                if (x == "-b" || x == "-B" || x == "-c" || x == "-C" || x == "--orphan") newbranch = 1
                if (x == "-") { prevdash = 1; break }
                if (x ~ /^-/) continue
                first = x; break
            }
            if (newbranch) onmain = 0
            else if (first == "main" || first == "master") onmain = 1
            else if (first == "@{-1}" || prevdash) onmain = 1
            else if (first != "" && first != "--") onmain = 0
            continue
        }

        if (verb == "push") {
            del = 0; all = 0; tomain = 0; pos = 0
            for (k = j + 1; k <= nt; k++) {
                x = w[k]
                if (x == "--delete" || x == "-d") { del = 1; continue }
                if (x == "--all" || x == "--mirror") { all = 1; continue }
                if (x == "--repo" || x == "-o" || x == "--push-option" || x == "--receive-pack" || x == "--exec") { k++; continue }
                if (x ~ /^-/) continue
                pos++
                if (pos == 1) continue
                d = x; sub(/^\+/, "", d)
                if (d ~ /:/) sub(/^[^:]*:/, "", d)
                sub(/^refs\/heads\//, "", d)
                if (d == "main" || d == "master") tomain = 1
            }
            if (del && tomain) { print "This push deletes main or master."; exit }
            if (!del && (tomain || all)) { print "This push names main or master as where it goes, so it would land on main without a pull request."; exit }
            if (onmain && !del) { print "main is checked out and nothing is pushed from it."; exit }
            continue
        }

        if (verb == "commit" || verb == "cherry-pick" || verb == "revert" || verb == "am" || verb == "rebase" || verb == "commit-tree") {
            if (onmain) { print "main is checked out and nothing is committed on it."; exit }
            continue
        }

        if (verb == "merge" || verb == "pull") {
            ff = 0
            for (k = j + 1; k <= nt; k++) if (w[k] == "--ff-only") ff = 1
            if (onmain && !ff) { print "main is checked out, and a merge or pull that is not --ff-only makes a commit on it."; exit }
            continue
        }
    }
}')

if [ -n "$why" ]; then
    printf '%s\n' "Refused. $why Make a branch off main and open a pull request. See CLAUDE.md." >&2
    exit 2
fi

exit 0
