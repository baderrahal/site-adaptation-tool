#!/usr/bin/env bash
# Blocks git commit while on main and any git push that names main. Reads the hook JSON on stdin.
input=$(cat)
branch=${GUARD_BRANCH:-$(git -C "${CLAUDE_PROJECT_DIR:-.}" branch --show-current 2>/dev/null)}

semi=$'\x3b'
bs=$'\x5c'
sep="(\"|&&|${semi}|[|][|]|[|]|${bs}${bs}n)"
gap="[^&|${semi}\"${bs}]*"
end="([[:space:]]|${bs}${bs}|\")"
re_commit="${sep}[[:space:]]*git[[:space:]]+(${gap}[[:space:]])?commit${end}"
re_push="${sep}[[:space:]]*git[[:space:]]+(${gap}[[:space:]])?push${end}"
re_push_main="${re_push}${gap}[[:space:]](HEAD:)?(refs/heads/)?(main|master)${end}"

deny() {
  echo "Blocked by .claude/hooks/guard-main.sh: $1. Work on a branch and merge by pull request." >&2
  exit 2
}

if [[ "$branch" == "main" || "$branch" == "master" ]]
then
  [[ "$input" =~ $re_commit ]] && deny "git commit on $branch"
  [[ "$input" =~ $re_push && ! "$input" =~ --delete ]] && deny "git push from $branch"
fi
[[ "$input" =~ $re_push_main ]] && deny "git push to main"
exit 0
