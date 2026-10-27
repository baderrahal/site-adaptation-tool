#!/usr/bin/env bash
# Round REC, the second route of the split proof: git, sha256sum, cat and grep, no Python. Run from the repo root.
set -e
base=73b8c42
pair() { echo "$1: $(git show $base:$1 | sha256sum | cut -c1-64) original, $(cat $2 | sha256sum | cut -c1-64) parts"; }
pair docs/rules.md "docs/rules/0[1-7]-*.md"
pair docs/addin-design.md "docs/design/[01][0-9]*-*.md"
pair steps/investigations/round-addin.md "steps/history/round-addin/[01][0-9]-*.md"
tmp=$(mktemp -d)
git archive $base docs steps CLAUDE.md .claude/agents | tar -x -C "$tmp"
for kind in 'R[0-9]{1,3}' 'Q[0-9]{3}'; do
  before=$(cd "$tmp" && find . -name '*.md' -print0 | xargs -0 grep -ohE "\b$kind\b" | sort -u)
  after=$(git ls-files --cached --others --exclude-standard docs steps CLAUDE.md .claude/agents | grep '\.md$' | tr '\n' '\0' | xargs -0 grep -ohE "\b$kind\b" | sort -u)
  echo "$kind distinct: before $(echo "$before" | wc -l), after $(echo "$after" | wc -l), lost $(comm -23 <(echo "$before") <(echo "$after") | wc -l)"
done
rm -rf "$tmp"
