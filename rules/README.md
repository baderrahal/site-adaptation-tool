# Project rules

One JSON file per project. config/project.json names the profile the tool reads (R63), and every rule value is read from that file, so a new rule value goes in the profile and not in code. The profile stays in git.

- roshn-makkah.json is ROSHN Makkah Phase 1, from 380159-SAPL-ROSHN-DSN-RPT-000252 Rev 01. Lengths are in mm and slopes are in percent.
- riser_height is null until the KEO value is supplied. Any check that needs it reports that it cannot be computed.
- The loader is python/project_profile.py. load_profile(path) reads the file and profile.get("clusters.Q01") returns a value. A key the file lacks raises an error, a key that holds null returns None, and a key that appears twice raises. profile.metres(key) returns a length in metres.
- Cluster membership comes from CAD adjacency (R64) and not from any file.
- Checks 1 and 2 are coded as R65 and R66 in docs/rules.md and are no longer listed here. Each check below moves to docs/rules.md with the next R number once it is coded. The numbers below are kept, so check 3 stays check 3.
- Some older rule constants are already in code, for example the Generic floor type name in python/link_summary.py. They stay until a round moves them into a profile.

## Checks still to code

3. One FFL per cluster. If slope forces a split, split only at the listed positions and record a double wall there. A position n is the wall after unit n, counted from 1, only 4-unit clusters can split, and D02 and D04 never split.
4. Front yard slope is one value for the whole front surface, parallel to the road, inside front_min and front_max. Here they are slopes_percent.front_min and slopes_percent.front_max.
5. Step count from FFL minus landscape level at the entrance divided by riser_height, rounded up, clamped to front_min and front_max. Over front_max is a flag. If riser_height is null, report that steps cannot be computed. Here they are steps.front_min and steps.front_max.
6. Back yard level is FFL minus back_step, graded toward the back wall inside back_min and back_target_max.
7. Boundary wall placement: flush with the blind side between units, inside the plot at a road, centred when shared.
8. The standard VL4 plot width is not a constant. plot.vl4_width is "derive", so the tool takes the most common VL4 plot width in the CAD analysis and flags the VL4 plots that differ from it with wide_plot.
9. Four tie ins per unit, checked between the Revit model and the Civil 3D network.
10. Yard sewage with cover under sewage_encasement_cover_under gets an encasement flag.
