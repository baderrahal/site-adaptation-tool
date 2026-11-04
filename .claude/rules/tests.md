---
paths:
  - "tests/**"
  - "python/tests/**"
  - "dotnet/SiteAdaptation.Tests/**"
---

# Tests

- tests/acceptance, tests/golden and tests/holdout are owned by the lead. A hook blocks every other lane from editing them.
- The lead writes and merges acceptance tests before lanes start.
- Hold-out answers are read by no lane and scored only by GitHub checks.
- A test never reads a past job output unless it proves past job mode. Never tune and test on the same data.
- No real coordinate or level in a test file, the repo is public.
- A test that only passes the good case proves nothing, break one thing and assert the check names it.
