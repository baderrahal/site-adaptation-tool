# Demo fill sheet, 1 Nov 2026

Which file goes in which field. Every path was listed on disk at 08:36. Labels from docs/design/14-ui-labels.md and the add-in code (main ae07cbc, no change in these windows since the installed 691e1fc).

## By themselves, nothing to pick

- Saved project ROSHN Makkah gives the config C:\SA-demo\config\project.json, the rules C:\SA-demo\rules\roshn-makkah.json, parsons2024Rev6, Masses, Dry run first and the scan picks. Its file list is empty (R434), so every file below is picked by hand.
- The config reads from inputRoot C:\SA-demo\inputs with no pick: data_book\ (plots.csv, phase1_border.csv, location_plan\labels.csv), position\position_parsons.json and 01_Output\1.11 References\ (the two 090000 workbooks).

## a. New project, Files the tool needs

| Row | Pick | Check files |
|---|---|---|
| Start from | ROSHN Makkah (saved) | the config is on disk, no stop |
| Site drawing (CAD) | C:\SA-demo\inputs\ROSHN Makkah Baseplan.dwg | Found |
| Plot list (Excel) | C:\SA-demo\inputs\PSN-ZZZ-XXX-SCH-TM-000201- DLUS Schedule_(Rev9)_Accomodation_Schedule.xlsx | Found |
| Villa levels (Excel) | leave as is, no Browse, its input waits on Lane A | Comes later, counts in no total |
| Road levels (civil file) | folder C:\SA-demo\inputs\landxml | Found. Optional, never stops |
| Parameter sheet (Excel) | leave as is, C:\SA-demo holds no parameter sheet | Missing |

Check files then shows 3 of 4 files ready and the stop box "The parameter sheet is missing, so button 1 cannot add the shared parameters." It stops nothing: Save setup is not held back by Check files, and button 1 then takes the 10 shared parameters of the config, as the 04:08 run did with no sheet. Say so if asked. Press Save setup.

## b. The pane, Input files (after Save setup)

| Row | Path | Filled by New project |
|---|---|---|
| Baseplan | C:\SA-demo\inputs\ROSHN Makkah Baseplan.dwg | yes |
| Villa blocks | C:\SA-demo\inputs\Makkah Building Footprint.dwg | no, Browse |
| Curbs | C:\SA-demo\inputs\Makkah Phase 1_Road Layout.dwg | no, Browse |
| Road surface | folder C:\SA-demo\inputs\landxml | yes |
| DLUS and sales | first dialog DLUS schedule: C:\SA-demo\inputs\TBD-PSN-ZZZ-XXX-SCH-TM-000201- DLUS Schedule_Rev9.0.xlsx, second dialog Sales schedule: the Plot list file above | Sales yes, DLUS no, Browse |
| Plot numbers | C:\SA-demo\inputs\01101-PSN-ZZZ-XXX-DWG-TU-005000.pdf | no, Browse |
| Prototypes | folder C:\SA-demo\inputs\Prototypes (13 .rvt) | no, Browse |
| Shared parameters | leave Not chosen | no |

These are the files of the 04:08 run, file for file. The head then reads "The drawings and schedules the tool reads. <m> of <n> found."

## c. 1 Set up site, Current files

Tick Current on the Baseplan row (Site drawing). Apply stays grey until it is ticked. The other rows may stay unticked.

## d. 2 Read data, What the tool read

- With the saved picks every value should be Clear and the window does not open. The pane shows "Read <n> files, <n> plots, every value clear." The first run scans first, about 3 min.
- If it opens: the rows Plot outline, Plot number, Villa block, Villa type show Clear or Pick. Press Use the proposed for all, then Use these picks. The pane shows "Your picks are saved with the setup." and the read goes on. Cancel stops the read.
