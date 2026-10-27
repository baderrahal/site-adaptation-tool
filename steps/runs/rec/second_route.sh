#!/usr/bin/env bash
# Round REC, the second route of the split proof: git, sha256sum, cat and grep, no Python. Run from the repo root.
set -e
base=73b8c42
pair() { echo "$1: $(git show $base:$1 | sha256sum | cut -c1-64) original, $(cat $2 | sha256sum | cut -c1-64) parts"; }
pair docs/rules.md "docs/rules/0[1-7]-*.md"
pair docs/addin-design.md "docs/design/[01][0-9]*-*.md"
pair steps/investigations/round-addin.md "steps/history/round-addin/[01][0-9]-*.md"
tmp=$(mktemp -d)
echo "steps/PROGRESS.md copy: $(git show $base:steps/PROGRESS.md | sha256sum | cut -c1-64) original, $(sha256sum < steps/history/progress-addin-4e.md | cut -c1-64) copy"
moved() {
  git show $base:$1 | awk -v s="$2" -v e="$3" '$0==s{f=1;next} $0==e{f=0} f && NF' > "$tmp/moved"
  cat steps/questions-open.md steps/latent.md > "$tmp/held"
  echo "$1 under $2: $(wc -l < "$tmp/moved") lines, $(grep -Fxvf "$tmp/held" "$tmp/moved" | wc -l) not held whole"
}
moved steps/PROGRESS.md "## Bader's turn" "## Next three steps"
moved steps/questions-closed.md "## Open questions, full text" "## Old numbers to IDs"
run() {
  git show $base:steps/PROGRESS.md | awk -v a="$1" -v b="$2" '{i=index($0,a); if(i){s=substr($0,i); j=index(s,b); print substr(s,1,j+length(b)-1)}}' > "$tmp/run"
  echo "PROGRESS run from $1: $(wc -c < "$tmp/run") bytes, held by steps/latent.md $(grep -cF -f "$tmp/run" steps/latent.md) times"
}
echo "CLAUDE.md Repo map to Real files: $(git show $base:CLAUDE.md | awk '/^## Repo map$/{f=1} /^## Real files$/{f=0} f' | sha256sum | cut -c1-64) original, $(awk '/^## Repo map$/{f=1} f' docs/repo-map.md | sha256sum | cut -c1-64) docs/repo-map.md"
run "key_copies.py keeps a last part NUL" "PrototypeSets.ps1 throws on it."
run "The check points of R353 hold the residuals" "none reached by a real input (READ, steps/questions-closed.md)"
git archive $base docs steps CLAUDE.md .claude/agents | tar -x -C "$tmp"
for kind in 'R[0-9]{1,3}' 'Q[0-9]{3}'; do
  before=$(cd "$tmp" && find . -name '*.md' -print0 | xargs -0 grep -ohE "\b$kind\b" | sort -u)
  after=$(git ls-files --cached --others --exclude-standard docs steps CLAUDE.md .claude/agents | grep '\.md$' | tr '\n' '\0' | xargs -0 grep -ohE "\b$kind\b" | sort -u)
  echo "$kind distinct: before $(echo "$before" | wc -l), after $(echo "$after" | wc -l), lost $(comm -23 <(echo "$before") <(echo "$after") | wc -l)"
done
rm -rf "$tmp"
