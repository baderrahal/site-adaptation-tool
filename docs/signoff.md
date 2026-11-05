# Sign off

A value tagged override or past needs a row here, signed by Bader, or scripts/checks/source_tags.py fails the PR. Value is `<file>#<JSON path>`, or the file alone to sign every value in it.

| Value | Why it stands | Signed by | Date |
|---|---|---|---|

## Not signed

These hold past values and carry no tag yet, so the check does not see them. They are listed so that a green check is not read as approval.

- config/overrides/roshn-makkah.json, the override list of the past job. On NBH 012 it changes 322 of 336 plots, 1,115 values (steps/truth-audit.md). Q255 and Q196 decide what happens to it.
- rules/templates/past-job.json, of PR 211, 36 past job measures tagged past, a template a request must name past job mode to take. Bader's to sign, Lead does not sign for him. source_tags.py does not scan rules/, so no check shows this.
