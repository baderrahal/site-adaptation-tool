# Project rules

One JSON file per project. config/project.json names the profile the tool reads (R63), and every rule value is read from that file, so a new rule value goes in the profile and not in code. The profile stays in git.

- roshn-makkah.json is ROSHN Makkah Phase 1, from 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06 (June 2024), with Rev 01 (Dec 2022) for foundations. Lengths are in mm and slopes are in percent. A key that ends in _m, such as plot_width_m, is in metres.
- riser_height is null until the KEO value is supplied. Any check that needs it reports that it cannot be computed. The step count of R67 does not need it.
- Old client codes and new codes both exist in the data and a code is never renamed. typologies lists both sets, for example VL3 and VL4 next to VD2 and VD4. keo_file names the KEO model file of a typology, so VD2 uses DP2. plot_width_m is the plot width of the manual, for reference only, the tool measures the width from CAD. DP2 and DP4 carry a note because the count table of the manual says 10 m. TH1-IM is listed with a note because its 18 units in the count are not explained. VL2 is not in the manual set, so it is not in typologies, although the baseplan has VL2 plots and the Master Plan has VL2 blocks. Plot types and unit codes stay apart (R30). The baseplan label type is a client plot type, and the unit code of a plot comes from its schedule row only, a block is a cluster hint (R69, R90).
- clusters are the 11 of the manual, M01 to M11, numbered as the manual numbers them. M05 and M06 are the DT pairs that the manual writes as TH1-TH1 and TH3-TH3 on 10 m plots. The old codes D02, D04, Q01 and Q02 stay in clusters with their own units. cluster_aliases names the manual code that D02, D04 and Q01 point at. Q02 has DP4 ends and Rev 6 has no cluster with them (M10 has DP2 ends), so Q02 is an old client code with no Rev 6 match and has no alias. config/project.json lists it in analysis.noRev6Match, and every cluster written Q02 is an issue (R75). The cluster step takes the first cluster whose units equal the row, so a manual code is written wherever one fits and an old code only where no manual cluster has the same units.
- rev6 holds values of the manual that no code reads yet, kept for later rounds. The slope values in it are in percent. The front setback of the ramp (Rev 6 section 2.3.A, 5520 and 5500 for C10) is ramp.front_setback, a default and a value per typology in mm, and the typologies that take one front step only (Rev 6 Appendix A) are steps.one_step_only, both read by the plan from CAD (R126, R127).
- steps.legend, steps.count_rule and steps.base_height_source hold heights in metres as in the schedules. steps.base_height and steps.riser_added are in mm. The base heights come from 747 plots of past schedules and the architect has not confirmed them, so every row of R67 carries a note until steps.base_height_source says otherwise.
- The loader is python/project_profile.py. load_profile(path) reads the file and profile.get("clusters.Q01") returns a value. A key the file lacks raises an error, a key that holds null returns None, and a key that appears twice raises. profile.metres(key) returns a length in metres.
- Cluster membership comes from CAD adjacency (R64) and not from any file.
- Checks 1, 2, 3 and 5 are coded as R65, R66, R125 and R67 in docs/rules.md and are no longer listed here. Each check below moves to docs/rules.md with the next R number once it is coded. The numbers below are kept, so check 3 stays check 3.
- Some older rule constants are already in code, for example the Generic floor type name in python/link_summary.py. They stay until a round moves them into a profile.

## Checks still to code

4. Front yard slope is one value for the whole front surface, parallel to the road, inside front_min and front_max. Here they are slopes_percent.front_min and slopes_percent.front_max.
6. Back yard level is FFL minus back_step, graded toward the back wall inside back_min and back_target_max.
7. Boundary wall placement: flush with the blind side between units, inside the plot at a road, centred when shared.
8. The standard VL4 plot width is not a constant. plot.vl4_width is "derive", so the tool takes the most common VL4 plot width in the CAD analysis and flags the VL4 plots that differ from it with wide_plot.
9. Four tie ins per unit, checked between the Revit model and the Civil 3D network.
10. Yard sewage with cover under sewage_encasement_cover_under gets an encasement flag.
