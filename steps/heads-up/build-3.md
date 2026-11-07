# build-3, FAST-10-build-3

F8, a project made with Empty project shows no past job value. Updated 7 Nov 2026 23:00.

## What changed

- R795, R796. dotnet/SiteAdaptation.Common/Settings/NewProjectValues.cs. Config(shipped, projectFolder) empties the past values of the config copy: profile to profile.json, schedules, road surface lists and connect.neighbourhoods empty, inputRoot, fitNeighbourhood and pastJob.schedule null, floors.roadTopography.surface "not chosen yet", own position file. Rules(shipped, projectName, shippedProfile): project is the folder of the model, manualRules say Not measured, R401 status entries keyed by the new profile, keyRules candidates left out.
- dotnet/SiteAdaptation.Common/Settings/ShippedSet.cs. MakeNew writes the new config and rules into the staging copy, CopyOnce calls it. Seen(readText) gives the window the shipped config and profile as the copy will hold them (R797).
- dotnet/SiteAdaptation.AddIn/NewProject/NewProjectViewModel.cs. readText is shipped.Seen of the reader, readFiles the plain reader for Preview, Take and NewMode.
- R798. ReadInputs.Schedules says ReadInputs.NoSchedule once when no neighbourhood names a schedule. PaneText.StepLine4 "Checks each plot against the rules".
- R799. The position file of the copy is {workRoot}/New projects/<folder>/position/position.json.
- DesignBasis.Of(settings, civilPath, picked), BuildSteps passes request.RoadSurfaces, so an empty surface list with a pick builds.

## What calls what

- NewProjectViewModel.Save calls ShippedSet.Take, which calls CopyOnce, MakeNew, NewProjectValues.Config and Rules.
- NewProjectViewModel constructor calls ShippedSet.Seen, Seen calls NewProjectValues.Config and Rules.
- BuildSteps calls DesignBasis.Of with the pick, then TakePick as before.

## Next

- Window shots and the Revit check on a model copy, after short check 2 of revit-test, under the window lock.

## Final statement

FINAL_PLACEHOLDER
