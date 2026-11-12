---
paths:
  - "dotnet/**/*.cs"
  - "dotnet/**/*.csproj"
---

# C# files

- Build and test on both targets: net48 and net8.0. SiteAdaptation.Common stays netstandard2.0 with no Revit type.
- Every new function has a caller, and the PR lists it under "Callers" as function name, then the caller. A test alone is not a caller.
- No project value in code, values go in config and the rules profile.
- Cowork writes these files, in the Cowork folder. Claude Code never edits them, a hook blocks it, a fault goes in the Bridge report (docs/truth.md, Who does what).
