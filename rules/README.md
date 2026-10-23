# Project rules

One JSON file per project. The tool is meant to load the file named in the project settings and read every rule value from it, so a new rule value goes in the profile and not in code. The settings file and the checks do not exist yet, see below.

- roshn-makkah.json is ROSHN Makkah Phase 1, from 380159-SAPL-ROSHN-DSN-RPT-000252 Rev 01. Lengths are in mm and slopes are in percent.
- riser_height is null until the KEO value is supplied. Any check that needs it reports that it cannot be computed.
- The loader is python/project_profile.py. load_profile(path) reads the file and profile.get("clusters.Q01") returns a value. A key the file lacks raises an error, a key that holds null returns None.
- The project settings file does not exist yet, so today the loader takes the path as an argument.
- None of the ten checks below is coded yet. This round adds the data and the loader only.
- Some older rule constants are already in code, for example the Generic floor type name in python/link_summary.py. They stay until a round moves them into a profile.

## Checks the tool must run

1. Attachment follows typology kind: villa detached, duplex one side, townhouse both sides.
2. Only clusters listed in the profile are valid. Any other unit sequence is a conflict.
3. One FFL per cluster. If slope forces a split, split only at the listed positions and record a double wall there.
4. Front yard slope is one value for the whole front surface, parallel to the road, inside front_min and front_max. Here they are slopes_percent.front_min and slopes_percent.front_max.
5. Step count from FFL minus landscape level at the entrance divided by riser_height, rounded up, clamped to front_min and front_max. Over front_max is a flag. If riser_height is null, report that steps cannot be computed. Here they are steps.front_min and steps.front_max.
6. Back yard level is FFL minus back_step, graded toward the back wall inside back_min and back_target_max.
7. Boundary wall placement: flush with the blind side between units, inside the plot at a road, centred when shared.
8. VL4 on a plot wider than the standard plot gets a wide_plot flag.
9. Four tie ins per unit, checked between the Revit model and the Civil 3D network.
10. Yard sewage with cover under sewage_encasement_cover_under gets an encasement flag.
