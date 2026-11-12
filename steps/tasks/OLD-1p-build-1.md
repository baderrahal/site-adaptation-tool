---
id: OLD-1p-build-1
lane: build-1
stage: 1
status: done
allowed:
  - config/project.json
  - rules/
  - dotnet/SiteAdaptation.Common/ReadData/
  - dotnet/SiteAdaptation.Common/DataCheck/
  - dotnet/CadReader/
  - dotnet/SiteAdaptation.Tests/OLD1/
  - python/
  - docs/rules/
  - steps/latent.md
---

# Scope

Round 1 of steps/plan.md, the small change step b needs. Read docs/truth.md first, its standing rules hold here.

The tool reads C:\02-Site Adaptation in place, with the past output folders skipped by settings: 01_Output, Revit All Zones, Native Files, and the site models of 00_Revit Models. Today the folder read skips only 01_Output, by dataCheck.skipFolders of config/project.json, read at three places: the walk of the worker (dotnet/CadReader/DataCheckCommand.cs), the hash and plan of the add-in (dotnet/SiteAdaptation.Common/ReadData/ReadInputs.cs) and the folder roles (FolderRoles.cs).

1. Revit All Zones and Native Files join dataCheck.skipFolders.
2. The site models of 00_Revit Models are single files under the same folder as the villa files, which must still be read. No setting skips a file today. Add the smallest one, CHOSEN by Code and marked so: a list of file name patterns in dataCheck beside skipFolders, read by the same three places, so one test proves all three agree. Find the site model files by a read only listing of 00_Revit Models, name the pattern by what config already names (the answerKeys file names or their pattern), no new project name in code.
3. A skipped folder or file is counted and named in the result of the folder read, so a person sees what was left out and why. If the read has such a line for 01_Output today, the new ones use it. If it has none, add the one line, the smallest version.
4. If Python holds a twin of this walk that the gate compares, change it in the same PR. If not, say so in the PR.

Add only this. No other skip, no change to what a role picks beyond the skipped places, no new window. A setting has a window by the rule of CLAUDE.md: say in the PR where a person changes this list today, and if nowhere, write the line into steps/latent.md for Lead, do not build the window.

Rule ids R830 to R834, in a new file of docs/rules with the next free file number, each with id, plain name, input, output and test. Question ids Q650 and Q651. Take no other id. New config values need python/rule_status.py --write.

Tests in dotnet/SiteAdaptation.Tests/OLD1: a made up folder tree with the four places, each skipped by all three readers, the villa file folder still read, and one broken case each that the test names. No real file name beyond what config names, no real path in a test.

Not seen in Revit is expected, say so in the PR. The run in Revit is step b by revit-test.

# Done when

- The folder read of a made up tree leaves out the four places and names them, proven on the worker walk, the add-in hash and the folder roles.
- config/project.json holds the two folders and the file patterns, with their why lines and source.
- Build Release and the quick tests pass. Every new function has its caller in the PR.
- The PR body starts with `Task: OLD-1p-build-1`.
