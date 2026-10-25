# Approved mock

Copied word for word from the round prompt of Round ADDIN. The fence keeps it as written.

```text
The approved mock (Bader, 25 Oct). Labels are exact. [n] means the real value
1. Ribbon tab "Site Adaptation" with three ribbon panels in this order: Setup (1 Set up site, 2 Read data, 3 Check), Build (4 Place villas, 5 Floors and ramps, 6 Levels, 7 Walls), Output (8 Report). Large buttons, one icon each.
2. Dockable pane "Site Adaptation" on the right, project name at the top right, four sections in this order:
   - Project: Settings (config file), Rules (rules file), Level profile
   - Input files: Baseplan, Villa blocks, Curbs, Road surface, DLUS and sales, Plot numbers, Prototypes. Each shows the chosen file or folder name
   - Scope: radio Whole project, Neighbourhood [code], Plot list. Checkbox "Dry run first, change nothing", on by default
   - Steps: one line per button 1 to 8 with a status: Done, Done with [n] issues, Ready, or Needs step [n]
3. Set up site dialog. Title "1 Set up site" and a badge "Empty project found" or "Existing site file". Left: "What the project has now" (Survey point, Project base point, Levels, Floor types and wall types, Shared parameters, Units) each with its state, then "What the tool will set" (Coordinate system, Survey point, Angle to true north, Levels, Floor types, Wall types, Shared parameters, Units) with the values it will set. Right: table "Check points from CAD" with columns Point, CAD, Revit, Diff, and a line "Pass limit 1 mm. [n] of [n] pass". Below it: "On an existing site file" with a line saying nothing changes until Apply. Buttons Cancel, Dry run, Apply.
4. Check table. Title "3 Check, NBH [code], [n] plots", a line with the level profile and the override file. Buttons Zoom to plot, Export to Excel, Save overrides. Filter chips: All, then one per check type (Plot cut, Floor, Hand, Unit code, Position, Level, and any other type found), each with its count, and "[n] plots follow every rule" on the right. Columns: Plot, NBH, Unit, Check, Tool value (rule), Past job, Rule, Use. Use is a dropdown Rule, Past, Skip. Past writes the past value to the override list with source parsons. Skip leaves the plot out and lists it in the report. Selecting a row zooms the Revit view to that plot.
5. Level Excel, file <NBH>_Levels_<profile>.xlsx. A top line with NBH, Zone, road surface and the formulas used. Columns in this order: Plot No, Unit, Cluster, Road at front, El @ Entry, FFL, SSL, Height, Steps, Source (Rule, or Override with the reason). Sheets: Levels, Clusters, Overrides used, Settings.
6. Report Excel, file <NBH>_Report.xlsx. A top line with file name, NBH, plot count, profile, run time. Summary table with columns Step, Done, Skipped, Override, Issues, one row per button 1 to 7. A box "Counts agree across outputs" with columns Item, Revit, Excel for Links, Floors, Levels. A box "Rules applied". Sheets: Summary, Placed, Skipped, Overrides, Issues, Rules.
```
