# Bader's form, 7 Nov 2026

One item, Bader's order of 14:40, F15. Answer by the letter.

1. The villa types of neighbourhood 010 for the demo of Sun 8 Nov 17:00, on the demo folder S1-new.
   - What is done: S1-new is a copy of S1 in the work root, S1 unchanged by SHA-256. In its DLUS Accomodation_Schedule workbook, 11 of the 16 villa plots of 010 got a row in sheet `JN Sales Data_Base_Rev09`, the plot number in column `Plot No` and the code of the plot's footprint block in column `Typology_Rev8.1`, each block holding exactly one prototype code. The worker of the installed add-in, run outside Revit on S1-new, gives those 11 plots their villa type.
   - What is left: 5 plots of 010 hold no type. 4 hold no footprint block and the DLUS calls them Open Space, 1 holds a block whose code has no prototype file.
   - What to know: sheet PH_1-DLUS calls all 11 typed plots Multi Family, so the drawing and the schedule disagree on 010. The demo says the types came from the footprint drawing typed into the Excel by a person, and card 3 marks them person entered.
   - A. Leave the 5 empty. The demo places 11 villas on 010. Recommended.
   - B. The team fills the 5: file `<work root>\S1-new\PSN-ZZZ-XXX-SCH-TM-000201- DLUS Schedule_(Rev9)_Accomodation_Schedule.xlsx`, sheet `JN Sales Data_Base_Rev09`, one new row per plot, the plot number in `Plot No` and a prototype code in `Typology_Rev8.1`, before Sun 8 Nov 11:00.
