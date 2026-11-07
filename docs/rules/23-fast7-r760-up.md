## Fast build FAST-7 of build-3, the polish of run 1, 7 Nov 2026

Bader's order of 7 Nov 10:05. The findings are F1 to F17 of steps/seam.md, from run 1 of FAST-3. Ids R760 to R769.

- **R760 Stop box of Build.** Input: the end of a step of the Build and the line BuildChain.Ended gave for it. Output: a stop, a roll back or a cancel shows that line, the step and the reason, in a box at the top of card 2, so the person need not find it at the end of the long line of card 2. A step that ended well or goes on outside Revit leaves the box as it is. The next press of Build clears it before the Revit side runs. The words are those of SiteBuildText.StepStopped and StepCancelled, no new word. CHOSEN by Code: a stop of the plan before the first step keeps its line on card 2 only, as a cancel of the preview ends there too and is no fault. Test: dotnet/SiteAdaptation.Tests/FAST7/R760_StopBoxTests.cs.
