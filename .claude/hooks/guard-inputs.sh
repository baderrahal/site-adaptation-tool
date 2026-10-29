#!/usr/bin/env bash
# Keeps the real input folders unwritten (D19, Bader 2026-10-29). Reads the hook JSON on stdin.
# Every link command is blocked wherever it points, since a link made in a work folder is how the Q111 run of 4g3 wrote a
# request file into the real Prototypes folder. A write command is blocked when its target lies under an input root, the
# two roots D19 names and inputRoot of config/project.json.
input=$(cat)
text=${input,,}

bs=$'\x5c'
semi=$'\x3b'
start="(^|[^a-z0-9_.-])"

# Only the command is judged, so a description that names an input root passes
text=${text#*\"command\":}
text=$(printf '%s' "$text" | sed -E 's/"[[:space:]]*,[[:space:]]*"(description|timeout|run_in_background|dangerouslydisablesandbox)".*$//')
text=${text//"${bs}${bs}"/"${bs}"}

deny() {
  echo "Blocked by .claude/hooks/guard-inputs.sh: $1. The real input folders are read only (D19), runs write under the work root." >&2
  exit 2
}

[[ "$text" =~ ${start}mklink([^a-z0-9_-]|$) ]] && deny "mklink"
[[ "$text" =~ ${start}(new-item|ni)[^a-z0-9_-].*-itemtype[[:space:]:\'${bs}\"]+(junction|symboliclink|hardlink) ]] && deny "a link made by New-Item"
[[ "$text" =~ ${start}ln[[:space:]]+(-[a-z]*s|--symbolic) ]] && deny "ln -s"
[[ "$text" =~ ${start}fsutil[[:space:]]+hardlink ]] && deny "fsutil hardlink"

roots=("c:${bs}02-site adaptation" "c:${bs}10- roshen mecca")
config="${CLAUDE_PROJECT_DIR:-.}/config/project.json"
if [[ -f "$config" ]]; then
  line=$(grep -m1 '"inputRoot"' "$config")
  value=${line#*: \"}
  value=${value%%\"*}
  value=${value,,}
  value=${value//"${bs}${bs}"/"${bs}"}
  [[ -n "$value" ]] && roots+=("$value")
fi

# Each root in back or forward slashes and in the /c/ form of Git Bash, as one regular expression
root_re=""
for root in "${roots[@]}"; do
  rest=${root#c:}
  rest=${rest//./${bs}.}
  rest=${rest//${bs}${bs}/[${bs}${bs}/]}
  rest=${rest// /[[:space:]]}
  root_re+="${root_re:+|}(c:|/c)${rest}"
done
root_re="(${root_re})"

writes="${start}(set-content|add-content|out-file|clear-content|new-item|remove-item|rename-item|move-item|sc|ac|ni|ri|rm|del|erase|rd|rmdir|mkdir|md|touch|tee|tee-object|mv|move|mi|rni|ren)([^a-z0-9_-]|$)"
copies="${start}(copy-item|cp|copy|cpi|xcopy|robocopy)([^a-z0-9_-]|$)"
dotnet="(${bs}[(system${bs}.)?io${bs}.(file|directory)${bs}]::(write|append|create|delete|move|copy|replace)|${bs}.(writealltext|writeallbytes|writealllines|appendalltext|moveto|copyto)[[:space:]]*${bs}()"
quote="[\"'${bs}]"

# A command line is cut into its commands at the command separator, and, or, the pipe and new lines
pieces=$(printf '%s' "$text" | sed -e 's/\\n/\n/g' -e 's/&&/\n/g' -e 's/||/\n/g' -e "s/${semi}/\n/g" -e 's/|/\n/g')
while IFS= read -r piece; do
  [[ "$piece" =~ $root_re ]] || continue
  [[ "$piece" =~ \>\>?[[:space:]]*${quote}*${root_re} ]] && deny "a redirect into an input root"
  [[ "$piece" =~ $writes ]] && deny "a write command on an input root"
  [[ "$piece" =~ $dotnet ]] && deny "a .NET file write that names an input root"
  if [[ "$piece" =~ $copies ]]; then
    [[ "$piece" =~ -dest(ination)?[[:space:]:]+${quote}*${root_re} ]] && deny "a copy into an input root"
    # With no -Destination the target is the last argument, a root followed by no quote up to the end
    trimmed=$(printf '%s' "$piece" | sed -E "s/[[:space:]\"'}${bs}${bs}]*\$//")
    [[ "$trimmed" =~ (^|[[:space:]=]|${quote})${root_re}[^\"\']*$ ]] && deny "a copy into an input root"
  fi
done <<< "$pieces"
exit 0
