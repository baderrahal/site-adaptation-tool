---
id: SETUP-sign-2
lane: revit-test
stage: rules
status: waiting
allowed:
  - scripts/sign/
  - dotnet/SiteAdaptation.RevitTests/
  - dotnet/SiteAdaptation.AddIn/Install-SiteAdaptation.ps1
  - docs/licences.md
  - steps/revit-results/
---

# Scope

What SETUP-sign could not close, moved here so that task can close on what it proved. Waits for Bader's decision on how to get from one prompt to none.

1. Zero prompts on a cold Revit start, by the route Bader picks: Always Load pressed by him, a certificate the company trusts, or the self-made one in Trusted Root, which a security check refused and only he may do. Whether a new build asks again is to be measured.
2. If a certificate is used: the remove and restore test, the prompt comes back when it is taken away.
3. A screenshot of the Site Adaptation ribbon tab with its buttons. The tab sits under the ribbon's overflow arrow.
4. The ricaun.RevitTest test reaching Revit, or a plain finding that it cannot on this PC and what replaces it, with two floor counts by different methods.

# Acceptance tests

None in tests/acceptance, this needs Revit.

# Done when

The four items above, each with its result in steps/revit-results/SETUP-sign-2.md, counts only and no user name.

# Revit check needed

All of the above.
