---
paths:
  - "docs/rules/**"
---

# Rule docs

Every rule has an id, a plain name of at most 6 words, input, output and test, on one line:

`- **R600 Plain name of the rule.** Input: what it reads. Output: what it gives. Test: the test that names R600.`

- docs/rules/index.md is written by python/rules_index.py, never by hand. A new rule goes after the last in its part.
- Take the id after checking origin/main and the open branches.
- scripts/checks/rules_docs.py holds new and changed rules to this. Rules from before setup-lanes are listed in scripts/checks/rules_baseline.txt until each is rewritten.
- A rule measured on past models says so, it is a past measure and not a manual rule.
