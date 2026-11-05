# Latent findings

A finding no real input reaches, kept as a known limit with no fix (Q095, Bader, 2026-10-27). Parity and review run on real inputs only. The reviewer and the breaker mark each finding REAL or LATENT, and a LATENT one is added here with its PR, file and line. A real input that reaches one later makes it a bug again, fixed with a test.

## Q088 to Q094, closed by Q095

Their lines of Bader's turn of steps/PROGRESS.md at 73b8c42, then their full text from steps/questions-closed.md, moved with no word changed. Q089 left this list on 2026-10-27, Bader reopened it as REAL and answered it, its two lines are in steps/questions-closed.md.

Q088. Check points of R353, four residuals (a) to (d) of its full text after the second fix, latent on the real data. Take the check point corners from the drawn line ends with no join, refuse (b) and (c) in both languages, hold the baseplan by its hash, and read answerKeys and key_copies.json through the checked loader of R312 with a key held by copySha256 and ordinal case, in a fresh PR with the inputs of the reviews as tests (recommended), or keep them as known bugs.
Q090. Prototype folder of a build (R357): a fresh PR that refuses a folder value that is no text as Python does, refuses a folder that lacks a file for a code of the scope, and clears SITE_ADAPTATION_INPUT in the tests, with the inputs of the reviews as tests (recommended), or keep them as known bugs
Q091. JSON reads of the worker that still differ from Python (R354): a fresh PR that routes every JSON input of the worker through PyJson and raises a duplicate when the object closes, with the same messages and the inputs of the reviews as tests (recommended), or keep them as known limits
Q092. Settings and keyRules values a build takes unchecked (R355 to R357): a fresh PR that gives every number setting and keyRules number its range in config, finite, sign and bounds, and one check order in both languages, with the inputs of the reviews as tests (recommended), or keep them as known limits
Q093. CSV reads that still differ (R345): a fresh PR that routes every Python reader of plots.csv, labels.csv and phase1_border.csv through csv_rows, which sets its own field limit and restores it, counts the C# limit in code points and gives the same decode line, with the inputs of the reviews as tests (recommended), or keep them as known limits
Q094. Scope and tolerance refusals: one hardening PR that refuses each case, a faceless number only in a list or cover, with one line and exit 2 on both sides (recommended), or keep them as known limits

Q088. Check points of R353, four residuals after the second fix, latent on the real data (Round ADDIN 4d, the fourth review of PR 54, B2, B3 and B4 stopped by the round rule, and N1 after it): (a) CadReader PlotShapes records a vertex merge at a node but not where a split bends a line to a node or where a merged node is dropped, so a corner can sit 10 mm or more off its drawn line with mergeMm 0, 18 mm in a chain of ends 9 mm apart, the breaker's brk4\merge probe on made up lines, a T undershoot of 8 mm bending the wall to its stem at mergeMm 0 and a chain of ends 9 mm apart dropping the merged node and putting the corner 18 mm east of the end of its line at mergeMm 0 (RAN, p4dfix5_merge.txt). On the real baseplan 0 of its 12,252 vertices lie 0.5 to 15 mm off the line of their neighbours, so 0 near straight ones, and 0 of the 118 of the faces of the check points (RAN, round_addin\logs\p4dfix4_q088_measure.txt). The worker takes the baseplan input by any name, refusing only a reference only drawing, while python/position_file.py holds plots_exact.json to drawings.baseplan by name only (READ, WorkerReads.Drawing and exact_plots). (b) A request with labels alone ends done with not made in the worker and is refused by Python (READ, WorkerReads.Position, RAN, the breaker's p_b3.py, p4dfix4_q088_measure.txt). (c) A labels.csv with no kind column or no clientCode row numbers every IMC and IMD point by the shift alone: the real labels.csv has the kind column and 2,704 clientCode rows, 2,580 of them with a face (RAN, p4dfix4_q088_measure.txt). (d) A key read or parameter file is held to its key by the name of its copy alone, in both languages: two keys whose copies share their bytes, one copySha256, which key_copies.manifest_faults takes (RAN, p4dfix5_same_bytes.txt, p4dfix5_answer_keys_measure.txt), a name Windows opens as another, a trailing dot or space, a relative, rooted or 8.3 name, a copy name that starts with the stem of another key, IMA x.rvt and IMB x_TEST-KB.rvt, whose read model_faults takes for IMA, and two codes differing only in case, the first dropped with no error and the second kept, all pass answer_copies and AnswerCopies (RAN, p4dfix5_py_check.txt, p4dfix5_cs_check_net48.txt and p4dfix5_cs_check_net8.txt, the breaker's probes on made up names). Python and C# differ on non-ASCII case, lower against ToLowerInvariant, and C# on net48 against C# on .NET 10 on the Kelvin sign and the titlecase digraph, and on a BOM in key_copies.json, which Python refuses and File.ReadAllText of WorkFiles.ReadText takes (RAN, p4dfix5_answer_keys_measure.txt, READ WorkFiles.cs). Latent: the real answerKeys name 7 keys of 7 distinct codes and copies by case, 7 distinct copySha256, no BOM, no such name, and key_copies.load_keys refuses the name forms and a code differing in case, but position_file.py:617, key_parameters.py:195 and WorkerReads.cs:176 parse key_copies.json raw (RAN, p4dfix5_answer_keys_measure.txt, p4dfix5_same_bytes.txt, READ the three lines). Recommended: take the check point corners from the drawn line ends with no join, refuse (b) and (c) in both languages, hold the baseplan by its hash, and let every caller read answerKeys and key_copies.json through the checked loader of R312, a key held by copySha256 and by ordinal case, in a fresh PR with the inputs of the reviews as tests. Or keep them as known bugs
Q090. The prototype folder of a build (R357, Round ADDIN 4e, the second review of PR 55, the fix of B2 stopped after its two attempts 57cae65 and 0919e2c): three residuals the breaker found, each measured (RAN). (a) BuildSteps.PrototypeFolder reads a pastJob.upgradedPrototypeFolder that is no text, 2024, true or a list, as missing and lists prototypeFolder, the Revit 2021 folder, on net48 and net8.0, where plan_from_past.prototype_folders stops with TypeError, and with no prototypeFolder the C# goes on with the upgraded folder where load_job refuses "pastJob has no prototypeFolder" (round_addin\logs\p4efix2_q090_a.txt). (b) A partial prototype folder passes both sides: with only the DP2 file of the 13, made up, the build of the 268 plots of img placed 26 links in Python and in the worker, exit 0 and status done, 233 plots lacking a prototype file in Python, and only the count links of the scope placed 26 shows it. An empty folder passes Python, 0 links placed and 268 lacking a file, and the worker refuses it, the step hands needing an input prototype:* that the request cannot name (p4efix2_q090_b.txt). (c) The two worker tests of R357 in BuildStepTests.cs read SITE_ADAPTATION_INPUT and fail on both runtimes when it is set, 2 of 2 failed and 2 of 2 passed with it not set, while the 7 Python tests of R357 pass with it set, as they name their folders themselves (p4efix2_q090_c.txt). Recommended: a fresh PR that refuses a folder value that is no text as Python does and a pastJob with no prototypeFolder as load_job does, refuses a folder that lacks a file for a code of the scope in both languages, and clears SITE_ADAPTATION_INPUT in the C# tests, with the inputs of the reviews as tests. Or keep them as known bugs, latent on the real config, whose folder value is a text and whose folder holds a file for every code it links.
Q091. The JSON reads of the worker that still differ from Python (R354, Round ADDIN 4e, the third review of PR 55, the fix of B4 stopped after its two attempts 9a1e211 and dae1fc8): four cases, each measured on made up texts with Python 3.12.10 and the probe q091 of p4efix3_probe on net48 and net8.0, and none reached by a real input (RAN, round_addin\logs\p4efix3_q091_python.txt and p4efix3_q091_csharp.txt). (a) ReadSettings.Load and AddinConfig.Load read config by Newtonsoft and not by PyJson: on net48 734 of 200,000 random decimals of 16 to 20 significant digits came out another double than float() gives, the first 79.193795980084694, 4053cc67274123d8 against 4053cc67274123d7, and none on net8.0. The real config holds 104 floats of at most 4 significant digits, all 104 equal by Newtonsoft and by PyJson on both runtimes. (b) PyJson refuses a duplicate key when it reads it and read_object of Python when the object closes: on {"a": 1, "a": 2, "b": {"c": 1, "c": 2}} Python names the duplicate key c and the C# the key a, and on {"a": 1, "a": 2, "b": } Python says the file is not valid JSON, Expecting value, where the C# names the key a. The real config, rules profile, analysis and worker reads hold no duplicate key. (c) An integer of 4301 digits is refused on both sides with other texts: Python "Exceeds the limit (4300 digits) for integer string conversion: value has 4301 digits" with a hint to sys.set_int_max_str_digits() after it, the C# "the input analysis is no JSON as Python reads it: Exceeds the limit (4300 digits) for integer string conversion: value has 4301 digits at char 4302 (R354)". The real inputs hold integers of at most 8 digits. (d) An integer past the long range, a BigInteger to Newtonsoft: FaceStep.Build gives the bound 1.1805916207174113E+21 for 2**70 + 2**17 + 1 where float() and shapely give 1.1805916207174116E+21, FaceRings.Refusals takes the cut value in silence, and ReadSteps.LabelStep and ReadSettings.SnapM throw InvalidCastException. The real inputs hold no integer past the long range. Recommended: a fresh PR that routes every JSON input of the worker through PyJson and raises a duplicate key when its object closes, with the messages of Python and the inputs of the reviews as tests. Or keep them as known limits, since no real input reaches them.
Q092. The values of the settings and of keyRules a build takes unchecked (R355, R356, R357 and R337, Round ADDIN 4e, the fourth review of PR 55, the fix of P3 stopped after its two attempts, 1ad1a21 and F1 to F4 of the third review): five cases, each measured at 28f2b2f (RAN), none reached by the real config and rules, whose roadEdgeM is 8.0, toleranceM 0.01, partyWallShare 0.75 and sideShare 0.5, every link offset finite and rampSlopePercent an object with a default (READ). (a) number_settings and BuildRules.NumberSettings, hand_settings and Hands.Majority hold roadEdgeM, toleranceM, partyWallShare and sideShare by their type alone, with no finite or sign check as road_snap and ReadSettings.SnapM hold snapM (READ, plan_from_book.NUMBER_SETTINGS and number_settings, BuildRules.NumberSettings). toleranceM was not run, so for it this is READ alone (L4 of the fifth review of PR 55). On the whole project of the snapshot with a config copy both sides exit 0 and agree: roadEdgeM -8.0 or NaN moves the frontRule of 965 plots, the front of 45, blockMirrorRoadSide of 963, the mirror flag of 40, the insert of 54 and the rotation of 44, links placed 2,544 against 2,545, partyWallShare NaN moves 2 plots and places 2,547, sideShare -0.5 flips 141 mirror flags and places 2,471 (round_addin\logs\p4efix4_q092_a.txt, the worker on net48). (b) check_link_fits and CheckLinkFits take a link offset of NaN or Infinity (p4efix4_q092_bce.txt). (c) A rampSlopePercent with no default that lists every code stops Python with KeyError 'default' where the C# passes, and a rampSlopePercent or a keyRules given as a list stops Python with an AttributeError where the C# refuses with one line, the log holding its type and message from a direct call of the check and no traceback of a run (p4efix4_q092_bce.txt, L4 of the fifth review of PR 55). (d) The checks run in another order: on the plots of img with sideShare and roadEdgeM both texts, Python names sideShare, by hand_settings, and the worker roadEdgeM, by NumberSettings, which BuildSteps runs first (p4efix4_q092_d.txt). (e) TextRepr escapes the control characters alone, where repr of Python escapes U+00A0, U+00AD, U+200B, U+2028 and a lone surrogate, as \xa0, \xad, \u200b, \u2028 and \ud800, and SlopeClass of NaN gives NaN where slope_class gives nan (p4efix4_q092_bce.txt, on net48 and net8.0). Recommended: a fresh PR that gives every number setting and every number of keyRules its range in config, finite, its sign and its bounds, held in one order in both languages, with the inputs of the reviews as tests. Or keep them as known limits, since the real config and rules hold none.
Q093. The reads of plots.csv, labels.csv and phase1_border.csv that still differ between Python and the C# (R345, R352, R353, Round ADDIN 4e, the fifth review of PR 55, the fix of the CSV reads stopped on its third attempt, after F5 b396313 of the third review, the first, and G2 6cbc878 of the fourth, the second, M4 of the sixth review): four reads, each measured at a9137dd on made up files in round_addin\q093 (RAN, round_addin\logs\p4efix5_q093_python.txt, p4efix5_q093_csharp.txt on net48 and net8.0, p4efix5_q093_c.txt), none reached by the real files: labels.csv and plots.csv of the snapshot and of the data book and phase1_border.csv hold 0 double quotes, no field over 3,525 characters and 0 characters past U+FFFF, and each decodes as UTF-8 (RAN, p4efix5_q093_python.txt). (a) position_file.py at lines 656 and 657 reads labels.csv, and plot_table.border_points reads phase1_border.csv, with a plain open and csv.DictReader, no newline="" and no strict, where the C# position command and the plot table read them by CsvRows.Read (WorkerReads.cs lines 237 and 669 at a9137dd). On a labels.csv of 104 client code rows with an unclosed quote at row 102, Python reads 102 rows, the last holding rows 102 to 104 in its kind field, and position_file.numbering gives plots 102, 103 and 104 the faces 52, 53 and 54 by the shift of 50 in place of 2, 3 and 4 with no error, where CsvRows.Read refuses the file with unexpected end of data. On a border of three points with an unclosed quote border_points stops with ValueError could not convert string to float, the C# refuses it with the line of csv. (b) CsvRows.Append counts the field limit of 131,072 in UTF-16 units, where csv counts characters: a field of 70,000 U+1F600, 140,000 units, is read by Python and refused by the C#. (c) csv_rows takes whatever field limit the process holds: rev9_parcels.read_parcels, dxf_count.reader_counts and im_codes.table_texts set it to 1 << 30 and leave it, and test_r345_a_csv_of_the_data_book_is_read_as_the_csharp_reads_it_with_newline_and_strict fails with DID NOT RAISE when test_r243_parcels_end_where_a_property_comes_again runs before it in one process, the gate passing only as test_build_steps.py runs before test_data4.py. No module that imports plan_from_book, read_steps or position_file names one of the three (SEARCHED, grep of python at a9137dd, p4efix5_q093_c.txt). Each of the three reaches plan_from_book through imports, rev9_parcels and dxf_count by data_book, plan_rules, override_source and majority, and im_codes by data_book_analysis and then the same (RAN, p4efix6_m6_imports.txt), so a run of one of them can call a setter and then csv_rows in one process (three links are imports inside a function, plan_rules.py line 453, override_source.py line 315 and data_book_analysis.py line 252, so they load plan_from_book only when those functions run, READ), and the log does not rule such a run out (M6 of the sixth review of PR 55). (d) csv_rows lets UnicodeDecodeError through: on a plots.csv with the byte 0xE9 at 40,040 of the file Python says 'utf-8' codec can't decode byte 0xe9 in position 7272, with no file name and a place inside its read chunk, and plan_from_book.py exits 2 with that line, where the C# names the file and the byte at 40040. Recommended: a fresh PR that routes every Python reader of plots.csv, labels.csv and phase1_border.csv through csv_rows, which sets its own field limit and restores it, counts the C# limit in code points, and gives the same decode line, with the inputs of the reviews as tests. Or keep them as known limits, since the real files hold no quote, no such field and no bad byte.
Q094. The scope and tolerance refusals of 4e that still differ between Python and the C# (R345, R348, Round ADDIN 4e, the sixth review of PR 55, H1 and H2 stopped on their third attempts, G1 1f7a418 and H1 d53033f before, G3 6295291 and H2 b6c0fe6): each case measured at d754e57, whose code under python and dotnet outside the tests is that of b6c0fe6 (RAN, git diff), on made up inputs in round_addin\q094 (RAN, round_addin\logs\p4efix6_q094_python.txt, p4efix6_q094_csharp.txt on net48 and net8.0, p4efix6_q094_compare.txt). (a) Scope. A plot list or a cover of numbers that name no face, 999, ' 101' and 0101, and a client code row whose number is one space end done on both sides, exit 0 with every plot face false, as PlotScope.Numbers and scope_plots refuse only a scope of no number. A cover whose plots is the text 101 builds the plots 1 and 0 in Python, where the worker cannot read a request whose plots is a text and exits 2 with no answer. A cover of the integers 101 and 102 gives both face false in Python, where the worker reads them as the texts 101 and 102 and gives both their face. The faces step of the worker and its roadLevels read end exit 1 failed with an InvalidDataException on a labels.csv of no client code row or a client code row with no number, where BuildSteps, run by the step clusters, and Python end exit 2 refused with the same line. A read with steps and no scope, or the scope kind zone, ends exit 1 failed in StepFile.ScopeText, read_steps.py taking no scope. (b) Tolerances. On 32 configs, the addin.parity block of the real config replaced, parity.tolerances and AddinConfig.ParityTolerances give the same line on 25 and differ on 7: an integer tolerance of 10**20, which Python takes, and of -10**20, which Python refuses, both an InvalidCastException in the C#, a field or a step that is no object and a kind that is a list, an AttributeError or a TypeError in Python, a duplicate key, refused by Python at its load and taken by the C# with its last value, and a trailing comma, refused by both at their load with another type and line. parity.area_tolerance and ParityTolerance("areas") differ on 7 of the 32: the two big integers, the duplicate key, the trailing comma, no addin.parity block, by two lines, kinds a list, a TypeError in Python, and areas null, which the method returns and its callers refuse with the line of Python (READ, WorkerCommands.cs line 61, BuildSteps.cs line 82). A read with steps on a config of areas 10**20 ends exit 1 failed on the InvalidCastException, as WorkerReads catches only InvalidDataException, and a build on a config of a trailing comma ends exit 1 failed at AddinConfig.Load in WorkerRunner, a FormatException that names no file, where plan_from_book.py exits 2 naming the file. None is reached by a real input: the 2,704 client code numbers of the real labels.csv are plain digits with no padding or leading zero, the 7 key covers hold texts of plain digits that all have a face, and config/project.json passes both checks with no integer past the long range in it or in the rules (RAN, p4efix6_q094_python.txt). 147 of the 2,704 client code numbers have no face in the baseplan and are written face false by design, so a refusal of a number with no face holds for a plot list and a cover and not for the project. Recommended: one hardening PR that refuses a number of a plot list or a cover with no face, a cover value that is not a list of texts, and every tolerance and scope fault with the same line and exit 2 on both sides, with the inputs of the reviews as tests. Or keep them as known limits, since the real labels.csv, covers and config reach none of them.

## Open bugs of PROGRESS that no real input reaches

Moved from the open bugs of steps/PROGRESS.md at 73b8c42 with no word changed.

- key_copies.py keeps a last part NUL, nul. or "NUL. " as a name where Windows opens the NUL device, reachable only by answerKeys.folder, and PrototypeSets.ps1 throws on it.
- The check points of R353 hold the residuals (a) to (c) of Q088, latent on the real data: a corner bent off its line by a split with mergeMm 0, the baseplan held by name, labels alone done in C# and refused in Python, a labels.csv of no clientCode row numbered by the shift (RAN, p4dfix4_q088_measure.txt). C# MergesOf takes a missing vertexMergeM as 0 where Python refuses it, latent as the worker reads the drawing itself (READ, ProjectPositionFile.cs), and a lengthAccuracy of 10**30 gives Python no fault and C# an InvalidCastException (RAN, p4dfix5_bigint.txt). A key read or parameter file is held by its copy name alone (Q088 d, RAN, p4dfix5_py_check.txt, p4dfix5_cs_check_net48.txt and _net8.txt). 4e, latent on the data book: R385 fails on a value from 1E+11 at 3 decimals, IsDigit of net48 takes 130 astral digits of Python as none, and the C# side band leaves out the GEOS simplification of a concavity under 0.5 mm deep (RAN, p4efix1_c6_digits.txt, READ round-addin.md). 4e open: Q089 a swap of two labels of one type, block and rotation passes the hold of the analysis, Q090 three measured defects of the prototype folder, Q091 the JSON reads of the worker that still differ from Python, Q092 the settings and keyRules values a build takes unchecked, Q093 the reads of plots.csv, labels.csv and phase1_border.csv that still differ, Q094 the scope and tolerance refusals that still differ, none reached by a real input (READ, steps/questions-closed.md)

## Round REC

- PR 56, python/record_parts.py RULE_TITLE: the title part [^*]* also takes a line break, and a title that holds a star would leave the index with no word. No rule title of docs/rules/ holds one, the index lists 311 of 311 (RAN)
- PR 56, python/record_parts.py parts: any other .md file put in docs/rules/ would join the rules
- PR 56, docs/rules/07-addin-r354-r388.md also holds R11 to R15 of the Test and Writing rules sections, the index lists them under it
- PR 60, python/tests/test_records.py test_r400_a_cap_reached_exactly_holds: PROGRESS 30 lines of 6,030 bytes and a log top of 4,086 bytes are under their caps, not at them, so a slip of > to >= on those three caps stays green
- PR 60, python/record_parts.py cap_breaches: the log is read as text, so a CRLF log is counted short, and a ## line inside a code fence ends the top entry early. steps/log.md is LF by .gitattributes and holds no such fence
- PR 58, python/split_proof.py MOVED_LINES: a moved line passes when either new file holds it, so an open question moved into latent.md or a line held twice would pass. The ids of the records show no such move (RAN, 65 open, Q088 to Q094 closed)

## Round ADDIN 4f and 4g1

Moved with no word changed to steps/history/latent-addin-4f-4g1.md on 2026-11-01 (R400).

## Round ADDIN 4g2

Moved with no word changed to steps/history/latent-addin-4g2.md on 2026-11-02 (R400).

## Round ADDIN 4g3

Moved with no word changed to steps/history/latent-addin-4g3.md on 2026-11-03 (R400).

## Lane B L0 and 5a

Moved to steps/history/latent-addin-l0-5a.md.

## Round FOLD, D19

- REAL, closed by the guard of D19 (Bader, 2026-10-29). The Q111 run of 4g3 on the laptop wrote one request file into the real Zone 02 ARC Prototypes folder under C:\02-Site Adaptation through a junction from its work folder. The file was deleted, 13 .rvt files were left, all dated 2024, with no hash taken before, and the junction is gone (the 4g3 log entry of steps/log.md). .claude/hooks/guard-inputs.sh now blocks every link command and every write whose target is under an input root, test_d19 of python/tests/test_harness.py. The run side closed in 4h-1: python/input_guard.py records name, size, write time and SHA-256 of every file a run of plan_from_book.py or read_steps.py opens under an input root, refuses a write there and fails on a change, test_input_guard.py. InputGuard of Common does the same for every input of the worker and the drawing of a CadReader read in place, InputGuardTests and WorkerRunTests.
- LATENT, review of the runner and guard PR of Round FOLD. guard-inputs.sh judges words, not parsed commands: a commit message or PR body given on the command line that names mklink, or names an input root beside a word of its write list such as move or del, is blocked (use a body file or the Write tool), a [IO.File]::Copy out of a root and a Copy-Item with -Destination first and the root as the last argument are blocked. It misses a root held in a variable, a Set-Location into a root then a relative write, a Git Bash escaped space, writes by Export-Csv, Expand-Archive, -OutFile, sed -i or a program's own output argument, ln -f -s and hard links by ln, and the Write and Edit tools, which the matcher Bash|PowerShell does not cover. No command of this project reaches them now.
- LATENT, same review. tools/run-lane.ps1: a missing start file sends an empty prompt that fails, retries once and exits with a poor message. Two sessions started in the same minute share an output file. The output is written only when the session ends. A session killed before its end records PR leaves its phase on line 1 of steps/PROGRESS.md, so every later session of that lane ends BLOCKED and the runner loops every 10 minutes until steps/STOP-<lane> or a hand fix of line 1.

## Lane B 5b, button 1 Set up site and the freeze guard

From the reviewer and the breaker passes 1 and 2 of the 5b PR. Each is LATENT, no real input reaches it today (Q095).

- Breaker 2. The active site is never read back, compared or set. SetUpTarget.cs reads activeSite for the window text only, RevitSiteDocument.cs sets the position on whatever site is active, SetUpPlanner.ReadBack and SetUpRun.SiteJson leave it out. A file on another named site would be moved there with the window saying site Internal. All 7 keys and every new project are on Internal (keyRules.site).
- Breaker 3. The dry run Revit column always comes from the target position, while Apply leaves a position within 0.5 mm per axis as it is (SetUpPlanner.cs SamePosition). A file 0.1 to 0.5 mm off would show 0.00 in the dry run and up to about 0.87 mm after Apply. No real file is off so, the risk comes with the 0.1 mm limit of W2 at 5c, named in W2 of steps/lane-b.md.
- Breaker 4. The angle row of What the tool will set compares the raw angle with no wrap to 0 up to 360, and site.json writes the raw angle where R311 rounds it to 6 decimals in 0 up to 360. The real angle is 0.
- Breaker 7. The Diff cell shows two decimals whatever gapDecimals is, config holds 2.
- Breaker 8. A browsed sheet is matched by its file name only (SetUpRun.cs Confirm). No sheet is read in Revit until 5c.
- Breaker 9. The rules come from the profile of config, not from a Rules input of the pane, and the run record holds no hash of the rules or the position file. 5c gives each button its inputs.
- Breaker 10. Levels already right on the Survey Point base while the position elevation differs: the level plan is made before SetPosition, the levels are not set again and the read back rolls back each run with no stop text. No real file is so.
- Breaker 11. A parameter of a GUID of the sheet under another name: the stop goes by name, the change list keeps it by GUID and the read back rolls back each run.
- Breaker 12. A failed delete of shared_<run id>.txt or a failed restore of the user's setting after a commit leaves ButtonFlow.Run as an exception for a run that committed.
- Breaker 13. The heartbeat of the test host comes from the Timer thread of TestHostWatchdog, so it beats while the main thread of Revit is stuck. A stuck main thread is caught by the not responding check after 5 minutes, a pumping modal box by the deadline of the job. The hang probe of 5b stopped at 7.4 minutes on not responding.
- Breaker 14. A probe manifest with a relative Assembly is resolved against the probe folder by TestHostPaths.ps1, while Revit resolves it against the add-ins root it is copied into. The probes of 5b give full paths.
- Breaker 15. The position file check of the setUp mode of Run-TestHost.ps1 compares the given text with StartsWith, so a path with .. under the work root passes. The round specs give plain full paths.
- Breaker 16. Stop-FrozenRevit of FreezeGuard.ps1 writes the log by Set-Content utf8, which under Windows PowerShell 5.1 adds a BOM. The test host runs under PowerShell 7.
- Reviewer. DialogWatcher Program.cs can meet a sharing IOException reading the pid file in the moment Move-Item replaces it. Run-TestHost.ps1 reads Process.Responding, which can throw when Revit exits between the wait and the check. SetUpSiteRun.cs and AddinRunSession.cs leave a new document open when its SaveAs fails, test host only.- Breaker pass 2. Browse builds its view at the slope of the last dry run and the window marks it as the dry run of the slope now chosen. Every Browse stops in Revit until 5c (Q155).
- Breaker pass 2. A sheet parameter with no GUID other than Plot gives a stop while nothing differs, the window shows both, Apply is off by the stop. Only a browsed sheet can hold one.
- Breaker pass 2. Browse with no SheetChosen callback keeps a path never read, SetUpRun always gives the callback.
- Breaker pass 2. The angle row can show Will change beside nothing differs for a target angle near 360, the target angle is 0 by Q068.

## 4h-1, review 1 and breaker pass 1

- Review, R367 and R368: SettingsStore, SettingsEntity, PlacedTagStore and SecondRun.Match have no caller but their tests until 5b and 5c join them (J1, J2). The tests of AddIn/Revit SettingsEntity.cs and PlacedTagStore.cs read the source text only, the Revit run after 5c judges SchemaBuilder, the vendor access and the ExtensibleStorageFilter. SecondRun.Match reads setToPlan with no null check. Schema and Entity objects are not disposed, as nowhere else in the repo.
- Review, Q122: level_excel_check.py reads levelRule["ffl"] of a written plot with no check where LevelExcel.cs skips a null one, real plans carry it on every written plot.
- Breaker 1, D19: a guard failure at the end of plan_from_book.py or read_steps.py raises after the plan and step files are written, so the files of a failed run stay on disk with a non-zero exit (R345 asks for none).
- Breaker 2 and 3, D19: the Python guard sees the audit event open only, so os.replace, os.rename, os.remove and os.mkdir under an input root are not refused, and reads by a child process are not held. Neither script does either (SEARCHED, grep of subprocess and rename).
- Breaker 4, D19: InputGuard.Mark opens the drawing with FileShare.Read, so a drawing another program holds open for write ends CadReader with an IOException where the read went before. Whether AutoCAD holds such a handle is UNKNOWN.
- Breaker 5, D19: the C# side refuses no write under an input root, a request whose output folder lies there is written.
- Breaker 6 to 8, R368: placed values compare at 3 decimals with no angle wrap and no vertex order, so a rotation read back as -90 against 270 or an outline from another start vertex reads as changed by hand. Untagged elements count only when no tagged one holds the key and are keyed with no part. A hand change made exactly to the new plan is AlreadyPlaced and keeps its old tag. The read back that builds Current is R383, not written.
- Breaker 9 to 11, R367: a level can ride in a string such as an issue text. Record writes the pane settings over an entity of a later formatVersion that Load refused. A lastRuns key that is no button number, a null mode and a time that is not ISO 8601 are taken, and a count past the int range may throw OverflowException.
- Breaker 12, Q075: final_cannot of majority.py drops a kept cannot entry of a plot the join has no row for with no count. past_all.csv of the laptop joins all 1,346 (RAN).
- Breaker, Q122: the column tests a written FFL off its rule, not an override applied, so a profile that rounds the two otherwise could list a value no override moved. In a piece with two rule FFLs, a plot on its own rule but off the first is not listed, 0 such pieces on the whole project (RAN).

## 4h-2, review 1 and breaker pass 1

- Breaker F2, R369: phase4_check.py passes a plan with plots cut (nothing holds the plan plot count against the steps, the plot table or the list), a step with every FFL empty (plotsNotChecked is counted, no zero-check floor fails the run), road levels shifted alike through the chain (taken from the step it checks, never from plots.csv or the LandXML), another front edge of the face, and an override left out with any reason. No real build reaches these. 35 real plots have a step road level 0.10 to 1.14 m off plots.csv roadLevelLeft or Right, right or wrong UNKNOWN (RAN, breaker of 4h-2).
- Breaker, R366: ButtonScope skips with no stop a row whose number is not a whole number, on the real plots.csv 59 rows of locationPlanNumber (30 empty, 29 joined by a bar holding 67 numbers, none a plan plot). The W9 join must feed locationPlanNumber and nbhId, since officialNumber is empty on 2,209 of 2,956 rows and the neighbourhood column holds IM letters, and must fix which column gives a unit plot: clientType takes in 42 plots the plan lacks, typologyRev9 39. StepStates compares run times with a strict greater than, two runs in one tick compare equal. ButtonScope.WholeNumber is a third copy of the R81 rule and trims no blank. No caller outside the tests until W9.
- Review, Q119: value_status Table.of keys a value read by none as field from override with the refs of the original source, so two plots of one field with other original sources could share a key, only walls reach it and a wall field has one source. The same in ValueStatus.cs. C# refs.AddRange also drops duplicates inside held where Python does not, no config list holds one.
- Breaker, Q055: the committed rules profile still says medianHeightM of native2022 leaves the 16 rows of u14 out, stale until a majority run with the Native reads (Q151). No parsons2024Rev6 build reads it.

## 5e and 5f, review 1 and breaker pass 1

- Review 6, breaker 9: ReportExcel reads manualRules as a list only and refuses any other shape, UpdateFromReport also takes an object by id. Lane B 5c writes the block (Q165), no input reaches either today.
- Review 7, breaker 8: with revitCounts and no run of 6 the Levels row reads Revit 0 and a mismatch issue, levelRun is written but never read. levelPlots comes with the run log of 6 in 5d.
- Review 8: UpdateFromReport.ResultJson with applied and UpdateApplied.Plots have no caller outside tests until buttons 5 to 8 take the changed plots.
- Review 9: the tiles do not part the changes, a profile rule break with no manualRules block is in neither Follow the rules nor Break a manual rule (Q171).
- Breaker 4: a blank Plot No cell ends the table of Levels or Placed, the edits below it are neither listed nor ignored.
- Breaker 5: the override list is read at Preview and written at Apply, a hand edit made while the window is open is lost.
- Breaker 6: two level Excels picked together overwrite one another per plot, and two ticked changes of one plot and field are both applied, the last wins.
- Breaker 7: a plan plot with no FFL, entry or steps gets no check and reads as following the rules.
- Breaker 10: every Browse writes the worker list under the work folder, so Cancel writes that file there (under the work root, D19 holds).

## 5e and 5f, breaker pass 2

- D, R402: when an output is held open at the commit of Update from report, the message is the held open line alone and the saved and not saved lines are lost, they still reach the run record (ButtonFlow.cs line 253).
- L1, R402: the steps override check reads the plan, not the override list, so a steps count saved by one Apply and not yet built, or a steps entry Bader removed by hand, is checked against the old count.
- L2, R402: each Apply overwrites update_from_report_applied.json, two applies before a build keep the second set of plots only. Nothing reads the file yet.
- L3, R381: the run logs given to the worker as runLog1 to runLog8 are hashed and never read, so 1 Set up site and 3 Review data read not run yet even after a logged run. No button writes a run log yet (Q175).
- L4, R381: the newest committed run of 6 is paired with the newest committed run of 4 with no check that 6 used that plan.
- L5, R381: revit_counts_<run id>.json is written straight to its name, not by its temporary name, and is left behind when the worker fails.
- Wrong shape, R402: a plot number held as a number in the override list would mark the plot not savable in both routes, all 11,075 entries hold text (RAN).
- Fix of pass 2 B, R402: the refusal of two books of one kind names each by its file name only, so two copies of 012_Report.xlsx in two folders read 012_Report.xlsx and 012_Report.xlsx. The refusal holds, the line is unclear.

## 5e and 5f, the gate and CI

- Flaky, not of this branch: R347 time limit tests of WorkerRunTests failed once each on the Windows CI runner on net8.0 only (a time limit of 1 s that the stand-in worker beat, an answer read before the kill), passed on net48 in the same runs and on a rerun of the job (RAN, PR 110). R310 process tests of ParsonsModelTests failed on the laptop under load and passed after a restart and on CI (RAN).

## Lane B 5c, reviews, breaker passes and the gate

All paths under dotnet/. A line number the reviewer did not note is left out.
- UI, Buttons.cs (SiteAdaptation.AddIn/Commands/Buttons.cs:58): a malformed rules JSON throws JsonReaderException through the Rules button, only FileNotFound and InvalidDataException are caught.
- UI, the three windows: the tab order puts the side box before the table.
- UI, check table window: Zoom to plot is enabled with nothing selected.
- UI, review data and check table windows: a note typed before OK or Wrong is lost on a row change.
- L1, ReadDataRun: the callback is not guarded against JsonException and IOException.
- L2, CheckRun.cs:359: a JsonReaderException is not caught.
- L3, CheckRun.cs:142: GetFiles "*.rvt" takes backup files such as name.0001.rvt.
- L4, the NBH follow count ignores the moved set.
- L5, RevitVillaDocument.cs:249: Line.CreateBound throws on an edge under 0.8 mm.
- L6, SiteAdaptation.AddIn/Revit/RevitBaseShapeReader.cs: a Close that throws skips the cleanup, and arcs are tessellated.
- L7, BuildPlan.cs: its casts throw InvalidCastException, not InvalidDataException.
- L8, Plan/BuildPlan.cs:61: BookLevelM 0.005 sits in code, not in config.
- L9, PlaceVillas/PlaceVillasRun.cs:221 and 572: the UnixFull branch, the notes give no more.
- L10, DesignValues.Current is process wide, a second profile in one process reads the first.
- L11, Outside cancel after the links are loaded says it changed nothing.
- Three parsers of manualRules disagree (ReportExcel, UpdateFromReport, ManualRuleBlock), the real file passes all three. A later PR moves ReportExcel and UpdateFromReport to ManualRuleBlock.
- Breaker, CheckRun.cs:219 and OverrideEdits.cs:102 and 127: the key of a past job is KC against imc.
- Breaker, CheckRun.cs:142: the "*.rvt" filter takes every file of the prototype folder.
- Breaker: a reload is judged on the last record only.
- Second reviewer F2: the info count is strict on nbhId.
- Second reviewer F3, OverrideEdits.cs:27: OverrideEdits.Save has no production caller, only tests.
- Second reviewer F4: a plot with another nbhId is dropped.
- Second reviewer, the stop line "did not end done" is wrong for an older folder, the wording should read "has no check.json".
- Second breaker, CheckTableBuild.cs:41: labels.csv and plots.csv disagree on the NBH of a plot.
- Second breaker, CheckRun.cs:346-347: the Save count is shown nowhere.
- Second breaker, OverrideEdits.cs:202-207: Save is slow, 400 changes take 9.6 s on the real list of 11,075 overrides.
- Second breaker, CheckRun.cs:257 and OverrideEdits.cs:197: the key case is lower against capitals.
- Second breaker, ButtonsRunHost.cs:429-433: the plan folder rule of the test host.
- Second breaker: a clock moved back orders the run ids wrong.
- Second breaker: the first write turns CRLF into LF, the real list has no CR.
- Gate, R310 tests of ParsonsModelTests (R310_a_sample_counts and R310_helpers_running_before_the_window): they fail under load on the laptop, the CIM query of Get-WindowSample (2 s) gives null childProcesses, and they pass alone and on an idle machine (RAN: 12 of 12 alone on both runtimes, 2 failed on each runtime while 3 gates ran at once).
- Probe warning box, JobSession.cs:148-149 and 327 on: a job run of the test host deletes warnings in FailuresProcessing and answers dialog boxes, a probe has no job session, so a probe that opens an old file needs its own handlers (RAN, probe 5c_base_boundary, attempt 1 hung on a modal warning, attempt 2 with handlers ran).
- Revit run after 5c, Run-TestHost.ps1:368-384: the run script and the job guard check no required input before Revit, a bad input name costs a Revit window.
- Revit run after 5c, Run-TestHost.ps1:355-357: keyCopy with buttons copies onto the new project path that the job then makes, the host refuses it.
- Revit run after 5c, probe 1b: the probe subscribed to Idling inside Idling, so 1b stays UNKNOWN. Probe 1a answers the need of the run.
- Revit run after 5c: an ExtentReports FileNotFoundException at Revit shutdown, seen in every window and not from the tool.
- Revit run after 5c, AddinRun.cs:262-268 and AddinRunSession.cs:516-524: the host raises its phase event once and never again after a refusal (Denied), a refused raise ends the run (Q223).

## Lane B 5d, review 1

- BuildSteps.cs:242-250 against CheckRun.cs:145: 4 Check names only the *.rvt files of the Prototypes folder, the build refuses any other file in it, such as Thumbs.db or desktop.ini. The prototype folders of today hold only .rvt files.
- WallsRun.cs:100: no check of the wall height before Wall.Create, a plan wall with its top at or under its base rolls the run back with a Revit message that names no plot. The rule puts the top 1.7 m over the higher FFL and the base at the yard level.
- RevitWallDocument.cs:66-80: setting a tagged wall to the plan leaves its location line as the user set it, so the centre read back fails. Reached only through SetToPlan, which nothing fills.
- FloorLoad.cs:32 and WallLoad.cs:26-30: no load fills SetToPlan, so the set to plan path of 03b for an element changed by hand is reached only by tests.
- plan_walls.py:74 against Walls.cs:125-135: a wallOpen of null is no table in Python and a refusal in C#. No rules file holds a null wallOpen.
- LevelsRun.cs:190-243: a plot whose floor the plan view skipped with a reason is not named in the Levels preview.
- rules/roshn-makkah.json, R401 block: the three values of addin.fixedParameters read UNKNOWN though Q156 is closed by Bader's answer, rule_status.py does not read the closed row. Gone in FIX-1NOV-D, the block left config with R461 and Plot is a row of config/shared_parameters.xlsx.
- FloorPlan.cs:206-225 and FloorsRun.Apply repeat FloorPlacer.SetCorners and its place loop, still called by PlaceFromPlan.cs:42 of the old placer, two copies of R114 that can drift.

## Lane B 5d, breaker pass 1

- FloorsRun.cs:138 and WallsRun.cs:133: floors_ and walls_<NBH>.json hold the elements of the scope of the run only, so a run on a plot list drops the other plots from LastPlaced. Only the case name of a plot placed again changes, Gone or New.
- FloorPlan.cs:190: the mid point of an arc is not in the outline values, an arc that flips with the same ends counts as equal. The real plan holds 0 arcs.
- FloorPlan.cs:173: the vertices of all loops are pooled, a vertex moved from one loop of a yard to the other counts as equal (47 yards of 2 loops in the plan of NBH 012).
- FloorPlan.cs:171: a floor moved to another level with the same top counts as placed. It needs a change of floorSettings.levels, none seen.
- WallsRun.cs:166 and 294: an untagged BW wall along a whole edge that wallOpen cuts in two matches neither piece, both are placed over it with a line. No real model holds BW walls yet.
- WallLoad.cs:23 and 28: PlanSource.Open checks the hash of the plan, WallPlan.Parse reads the file again with no check.
- ParameterSheet.cs:68-74: a name in both blocks that differs only in case, or one GUID in both blocks, is not refused. RevitFloorDocument writes Plot by name, so a model with a project parameter Plot gets one of two.
- BuildSteps.cs:281: Path.IsPathRooted of .NET Framework takes C:foo and \foo, which resolve against the current folder.
- Walls.cs Opening and plan_walls.opening: an edge whose two ends lie equally far from the front middle picks its end by floating point noise, the same in both languages.
- Walls.cs Pieces and plan_walls.pieces: a span over the whole part, or leaving 0.3 m or less, drops the wall with no line, the same in both languages. How often on real edges is UNKNOWN.

## Lane B 5d, Q116 breaker pass 1

- plan_from_book.py open_ramp and Fronts.OpenRamp: a turn of 90 degrees would put the front setback depth of ramp_outline.setback along a side edge, untested. The override list holds 18 turns near 180 degrees and 3 under 5 degrees, none of 90.
- open_edge and OpenEdge keep the first edge on a tie, a front made of two collinear ring segments could flip to the other half under a small turn. The 3 real small turns did not move a ramp.
- Fronts.OpenRamp has no polygon check where open_ramp needs a Polygon face, no real input reaches the difference.

## Lane B 5d, review 2 of the fixes

- RevitFloorDocument.cs:101: ChangeTypeId drops its return value. If Revit made a new element on a type change, the next lookup of the old UniqueId fails and the run rolls back, loud. Not probed in Revit 2024.
- FloorPlan.cs:212-225 Named: a tag of 5d from before the type and plot values reads them from the floor as it stands, so a type the user changed by hand on such a floor reads as a plan change and 5 sets it to the plan with no line. Only floors of the 5d test runs hold such tags.

- Lane C NEWPROJ-UI, TemplateBook.cs Write: a sheet name over 31 characters, one with []:*?/\ or two roles giving one sheet name make an xlsx Excel repairs, and a header with outer spaces loses them. The real names of dataBook.rev9 are 27 and 24 characters and clean (breaker).
- Lane C NEWPROJ-UI, NewProjectViewModel.GetTemplate: a relative inputRoot, an 8.3 short name, a subst drive or a UNC alias of the input folder slips past WorkFolder.Inside. inputRoot of config is a full drive path (breaker).
- Lane C NEWPROJ-UI, SavedStarts.List: a refused saved project is left out with no word to the user, the skipped list has no caller in the add-in (reviewer).
- Lane C NEWPROJ-UI, LastReadFacts: a plot table that does not read shows Not read yet, with no log line. The block shows the latest read of the work folder and is not read again after Browse picks another drawing (reviewer, breaker).
- Lane C NEWPROJ-UI, TemplateBook.SheetsFor: its refusal names addin.workbookRoles and dataBook.rev9, code names on the Template line of New project, reached only by a config with no such block (reviewer).

## Lane D 4j-1, reviewer and breaker pass 1

- ScanLogic.cs LabelNumber uses Regex.Match, which searches, where scan.py uses pattern.match, which anchors. labelPattern holds ^ and $, so both agree. A pattern with no ^ or under 2 groups would part them (reviewer, breaker).
- Two scan inputs of one file name from two folders give one source text on both sides and their rows merge. Every real input has its own name (reviewer, breaker).
- WorkerRunner Steps of scan lists every role as needed, so a request that names Steps scan with no Excel or PDF is refused. No caller names Steps for scan, the test sends none (reviewer).
- ScanSources.cs reads the char box of PdfPage by reflection into its private members, a rename there throws at the first scan (reviewer).
- scan.py raises on a profile with no scan block or an empty prototype suffix where C# refuses with a line (reviewer).
- The prototype files of a scan are marked once and not checked after, they are named only, never read (reviewer).
- A header that holds "|" makes a source text of two meanings. Python orders by code point and C# by UTF-16 unit, apart only past U+FFFF. No real header holds either (breaker).
- A plot dropped as numberTwice is out of the index, so a plot of another drawing inside it gets no number conflict. 0 duplicate numbers among 3036 labels (breaker).
## Lane B 5d, Q116 second pass

- build_proof.py:312-314: on a turned plot rampFrontM now stands in notApplied with the Q116 why, so a ramp front that missed the key would read as an explained override, not a finding. The 18 turned plots lie within 1 mm of the keys.
- The 18 turned plots keep a rampFrontM entry in the override list that the build always leaves, an edit of it has no effect but the why in notApplied.

## 4i, review 1 and breaker pass 1

No real input reaches these, since the ROSHN config runs roshnPastJob (Q224) and no civil file exists. They become real on a project that runs rev6.

- Review 4, breaker F7, LevelWorkbook.cs:49, level_excel_check.py:282, read_steps.py:380, majority.py:676: the level Excel, the read step and majority.py take the six surfaces of siteBuild.roadSurfaces under rev6 while the plan takes the one surface of R409 (Q234).
- Review 6, config/project.json designBasis.doorLevel.slopePercent repeats rampSlopePercent of parsons2024Rev6 and back_rev6_min, back_rev6_max and townhouse_back_rev6 repeat the older rev6 block of the rules profile, nothing ties the two copies (Q228).
- Review 7, new_project_check.py:316, 237, 82 against NewProjectCheck.cs:526, 431, 165, design_basis.py:68 against DesignBasis.cs:176: Python and C# word a config fault apart, a workbook None against an empty name, a unit code mapped to null present against missing, an OSError read as none against an IOException, an empty civil path as path against none.
- Review 8, breaker boundaries, design_basis.py:183 and DesignBasis.cs:231: a ramp depth of zero raises ZeroDivisionError in Python and gives NaN and Ramps 2% in C#. The depth is the setback of a unit, never 0 in the profile.
- Review 8, breaker wrong shape, cluster_proposals.py:69 and ClusterProposals.cs:144: a cluster of the rules profile with an empty unit list loops for ever. No such entry today. ClusterProposals.cs:167 a byFile entry of JSON null throws.
- Breaker F4, plan_from_book.py:946 and plan_rules.piece_top:622: a civil file that lacks one plot of a row pair or cluster leaves the other plot an FFL from its own El @ Entry alone, only the missing plot listed in noDoorLevel.
- Breaker F5, cluster_proposals.py:39-57 and ClusterProposals.cs RowOf: a corner plot whose left and right are swapped against its neighbour makes both plots of a pair the left half with one hand. On the real rev6 run 0 of 1,461 clustered plots disagree (RAN by the breaker).
- Breaker F6, design_basis.py:79-87 and CsvRows.Read(path, out header): a civil file with a header and no rows is taken, every plot goes to noDoorLevel, and a row with an empty plot cell is kept under the empty text. Python lets a UnicodeDecodeError through where C# refuses by name. Arabic-Indic digits pass float() in Python and throw FormatException in C#. A level in mm is taken as metres and shows only in slopeOutsideManual.
- Breaker F8, floor_fit.type_of: a door level under the road gives a negative slope, listed outside the manual, and the plan still draws Ramps 0.5%, NO_BAND never fires with the bands of floor_outlines.json.
- Breaker F9, new_project_check.py:347, 281, 136, 431 and their C# twins: no Schedules line when connect.neighbourhoods holds no schedule, the three hand tables merged so DP2 and DP4 read ok while R411 finds no hand cluster entry (Q239), a header cell that is a number read as empty, the config and the rules read before they are marked.

## Lane B 6a, review 1

- ButtonsRunHostTests.cs:198-210: the test of the named commands holds the text of ReadNoApply, which is Revit bound and never runs in a test, so that it never applies rests on the code as read.
- ButtonsRunHost.cs:457 and 462: the two Shoot overloads part by the conversion of a string over an object, a later call with another third argument would take the other.
- DialogWatcher Program.cs:152-154: a progress window is marked handled before it is left, a dialog Revit drew in the same window after it would not be read. No Revit 2024 run showed it.
- WindowShots.cs:113-117: a throw inside LayOut leaves the window it built unclosed until it is collected.
- RevitShots.cs:301 and 339: the current process is not disposed, and the old bitmap is put back only on success.
- TestHostPaths.ps1:142 Resolve-WorkRootPath is a third copy of the work root mark beside WorkRoot.Resolve and project_profile.py, held to the C# one by its test, and none of the three refuses a part of two dots. The input is the config of the repo.

## Lane D 4j-1b, the gate

- WorkerRunTests R347_a_time_limit_that_fires_after_the_worker_answered_and_exited failed once on net48 while the pytest gate ran beside it, then passed 3 of 3 alone and in the whole net48 leg, 1694 of 1694 (RAN). A timing test under load, as the R347 timing tests of 5e.
- PlotShapes.cs LineKeys: a line over about 1e8 m at the 0.1 m cell floor overflows the step count and gets no cell, its crossings and T junctions skipped. The longest of 340,897 real lines is 2453.2 m (breaker, reviewer).
- PlotShapes.cs LineKeys: time and memory grow with line length over the cell, a stray line from near 0 to the site among 0.1 m pieces may run out of memory. No real line ends near 0 (breaker).
- PlotShapes.cs PieceKeys: near 2.4e6 the box of an arc rounds by up to 2.3e-10 m, a node exactly on the box edge at a cell edge could be left out. Not reproduced (breaker).
- CadScan.cs Inventory: a pair that throws in Parallel.For reaches the caller as an AggregateException, no caller catches a type there (reviewer).

## Lane C SCAN-UI, 1 Nov 2026, breaker pass

- ScanStep hash cache keyed by path, size and write time: another file copied in with the same size and write time is taken as unchanged (ScanStep.cs Marked).
- Reusable parses each scan.json of the work folder that does not match, and scan_ folders are never cleaned, so the first press after many input changes reads more files.
- A sheet named block, text or page n, or a column header holding a bar, is shown or split wrong in the scan window and New project, display only (ScanView.Show, ScanFacts.Excel).
- Export of the scan tables escapes no control character, so a DWG text holding one would make an xlsx that does not open (ScanPicks with TemplateBook).
- A worker that never answers leaves the scan of that work folder marked running until Revit restarts, the time limit of 600 s ends it first.
- The column dropdown of New project holds one line per villa type source, so two shifts of one sheet and column show twice (Q249).


## Lane C SCAN-UI PR 2, 1 Nov 2026, breaker pass

- Door level from counts every unit plot of doorSource civilFile, also those the civil file gives no level, so its count can pass the rows of the file (DoorLevelPick.Count, with the civil path null today).
- The plot key of Review data is the location plan number, else the official number, and the plan of 4 Check its build number. Not proven equal on real data, the confirm of a plot no row shows would be lost.
- A cluster of 4i can name a plot of another NBH, which the window of that NBH cannot tick, so that plot stays unconfirmed. Whether two NBH rows touch on ROSHN is UNKNOWN.

## 4j-2, review 1 and breaker pass 1

No real input reaches these, since the ROSHN config runs roshnPastJob (Q224) and designBasis.split.test is off (Q303, Q304).

- Review 1, rules/roshn-makkah.json rev6.irregular_tolerance_deg reads DOCUMENT in the R401 block, the 2.0 degrees comes from the 4j-2 measurement, not the manual.
- Review 2, breaker 8, plan_rules.py part_fits and KeyLevels.PartFits: a tested cluster with no unit code on any plot gives no FFL and raises in both languages. Fix when the test is turned on: skip it as untested.
- Review 3, BuildSteps.cs walls block against plan_from_book.py build: C# reads irregular_tolerance_deg only when the walls step or the check rows are built, Python on every rev6 build, so a bad value is refused by a different set of steps.
- Review 4, DesignBasis.cs ReadSplit: units past the integer range throw OverflowException where Python refuses by the units rule.
- Review 5, the R415 and R416 tests: no moved wall on a clockwise irregular ring, no value exactly on the tolerance, no build test on a real irregular face end to end.
- Review 6, splitTest is written to the step levels only, no plan, check table or Excel reads it (L4j2-1).
- Review 7, Walls.cs repeats 45, 4 and 90 as literals beside their constants.
- Breaker 4, part_fits: one slope outside the band blocks every split, a cluster that walls 3 would mend on steps stays whole as noFit (Q303).
- Breaker 5, plan_walls.inside: at a 45 degree corner the moved body leaves the face by up to 0.150 m, on baseplan plots outside the scope, in scope only plot 2961 by 0.002 m.
- Breaker 6, a shared edge of two irregular plots: 87 on the whole baseplan, 0 in the all scope, the wall goes inside the plot it is filed under as R416 says.
- Breaker 7, WallsRun.OnLine: under rev6 an untagged hand wall on the old centre line no longer holds back the moved plan wall, so button 8 places a second wall 0.1 m beside it.
- Breaker 8, plan_walls.inside: a zero length edge divides by zero, edge_walls builds no wall on one. corners() on 5,000 vertices takes 5.1 s, the largest real ring holds 18.
- Fix pass, plan_walls.mitre and Walls.Mitre: with every baseplan face written and a wall on every edge, 106 of 1,002 corners whose ends both sit on vertices stay apart, likely three or more moved walls at one point, in the all scope 28 of 28 corners share one end.
- Fix pass, a rotationDeg override on a moved wall reads walls.ends from override by rotationDeg, whose entries still name keyRules.wallLine.
- Review 2, breaker 2, plan_walls.partner and Walls.Partner: the mitre pairs two walls moved to opposite sides of a shared edge of two irregular written plots, and the result follows the walk order. 0 adjacent irregular written plots in the all scope.
- Breaker 2, plan_walls.mitre: no limit on how far a mitre point lies from its vertex, a 3 degree notch grows its walls by 1.9 m. 0 shifts over 0.3 m on every baseplan face.
- Breaker 2, plan_walls.mitre: a wall shorter than its two mitre retreats reverses, under 0.35 m between two 60 degree corners. 0 such walls on every baseplan face.

## FIX-1NOV-A, review 1 and breaker pass 1

- Review 1, check_rows.ramp_joints and CheckRows.RampJoints: R471 groups the plan corners at 3 decimals, R470 the raw corners, so a pair 8.6 to 10 mm apart could join in one and not the other, a missing row. 0 of 4,113 joints over 1 mm after R470.
- Review 1, test gap: no test puts two corners exactly floors.rampJoint.nearM apart, both languages use the same float test.
- Review 1, JointLimits and joint_limits take an inf or NaN nearM, joint_near refuses it and JointNear does not test finite. config holds 0.01.
- Breaker 1, grouping: R470 groups raw corners, R471 plan corners at 3 decimals, a pair at 0.0099 m raw could flip. 0 real pairs between 0.0015 and 0.0197 m.
- Breaker 1, joint_levels and JointLevels: no limit on the length of a chain, and two corners of one plot can meet through a third. Every real joint holds 2 corners of 2 plots, the widest 1.4 mm.
- Breaker 1, NoSiteLevel: C# joins and CheckRows.RampCorners lists ramps BuildPlan does not write, Python writes none. Every real site holds the ramp level.
- Breaker 1, grid cell index at exactly nearM in all four joint finders. No real pair near 0.01 m.
- Breaker 1, S5: 169 back pairs of a shared side sit 19.7 to 20 mm apart, outside nearM, with steps of up to 229 mm that were in the base too. R470 does not join back corners, R471 lists none of them.- Review 2, check_rows.ramp_joints and CheckRows.RampJoints group all four corners and R470 joins only the front pair, so a back corner within nearM of another ramp's front corner would list as a joint row. No real pair seen.

## Lane B FIX-1NOV-B, review and breaker pass of Task 2

No real input reaches these on the ROSHN config, whose linkFolder is masses, massFolder {workRoot}\masses and drawings in metres.

- PlaceVillasRun.cs:30, the constructor of 5 arguments is used by the tests alone, the add-in passes the mass files. A caller of it with linkFolder masses stops with no mass files wired (R450).
- MassFiles.cs:56 and Make (:205), massFolder is held only to be a text and not the prototype folder. A massFolder inside an input root is not refused as D19 asks, the guard WorkFolder.Inside sits in the add-in and Common holds none.
- MassFiles.cs:137, Stale casts readerVersion by (int?), a sidecar edited by hand to a text or a decimal there throws in place of making the mass again.
- CadExtractor.cs:46 JoinedRing drops the bulges of a polyline, so an arc side of a house outline is taken as its chord.
- OutlineDistance.cs:138, houseMinAreaM2 is held against the ring area in drawing units, right while the drawing is in metres.
- PlaceVillasRun.cs:242, linkFolder prototypes or a folder writes footprint_<NBH>.json with no rows, those links give no base to measure, the counts say so.
- UNKNOWN, the mass file takes the name of its prototype (R450). After a Links run of part of the plots, a type of massFolder\VD4.rvt and one of the prototype folder VD4.rvt stand in one model. Whether Revit 2024 holds two link types of one file name from two folders is UNKNOWN, the runner probes it. Naming unchanged until then, with the mass base question of Q279.

## Lane B FIX-1NOV-B fix pass on task 3, R454 to R456

No real input reaches these: coordinates.source is positionFile (Q328), and the demo copies state UTM84-37N or no system, with no named position on the documents.

- AddIn/Revit/RevitSiteDocument.cs:153 SetNamedPosition with SetUpPlanner.cs:128 Changes: a document that already holds an inactive position of coordinates.positionName gets it made active, while Changes and the dry run were computed against the position active before. The read back after Apply shows the gap, the dry run does not.
- Common/SetUp/SitePosition.cs:52 and CoordinateSystems.cs:186: a stated geographic code such as EPSG 4326 that coordinates.systems maps, or a coordinates.expectedEpsg of no UTM zone, throws in UtmMath.ZoneOf before the expected code stop of R454 is listed.
- CadReader/CoordinateSystemRead.cs:141 OfLandXml: a LandXML that names its system only by horizontalCoordinateSystemName, with no epsgCode, is read as stating none.
- CoordinateSystemRead.cs NameOfDefinition: a well known text whose name coordinates.systems does not hold stops button 1 as a name of no known code, even when it is the same system as UTM84-37N.
- RevitSiteDocument.cs:153 SetNamedPosition: the position name is matched with case, so a position of the name in another case is not found and the active position is renamed beside it. Whether Revit refuses two names that differ by case is UNKNOWN.

## Lane D FIX-1NOV-D, review 1 and breaker 1

- Breaker S1, VillaInputs.Of and PlaceVillasRun.WriteParameters: a sheet that gives RRE_ZZ_SP_Plot-Number another button, none or Keep No turns off the R85 guard, button 7 places a link with no plot number and an untagged link is no longer known as placed. The sheet of the repo gives it 7.
- Breaker S4 and L4, python/parameter_sheet.py: openpyxl read only stops at the stated dimension and takes row 1 as the header, ParameterBook reads every row and skips an empty first row. A dimension smaller than the data or a header below row 1 parts the two. The sheet of the repo states A1:H12 with the header on row 1.
- Breaker S5, python/parameter_sheet.py: a GUID of 36 characters with its hyphens moved or in braces passes Python and is refused by C#. The sheet of the repo holds 11 GUIDs of the D shape.
- Breaker S6, key_parameters.against_sheet: a name the keys give two GUIDs or none gets no fault line. Plot is the only noGuid name of the keys, by Q156, and no conflict is in the 7 keys.
- Breaker L3, new_project_check.parameter_line: a file of no xlsx raises BadZipFile, not caught, where the C# side gives a does not fit line.
- Breaker L5, ParameterBook: a shared string index of no number and a relationship with no Target throw outside the R462 message. openpyxl and Excel write neither.
- Breaker L6, ParameterSheet.PathOf: a config in a drive root throws, C:foo.xlsx counts as rooted in C# and not in Python. The config is under config of the repo.
- Breaker L7 and Q330, RevitSiteDocument.BindParameters: the group labels follow the Revit language and one built in group with no label would stop every bind. Not run in Revit.
- Review 1 F6 and breaker L8, SetUpPlanner.Stops: a Plot row with no GUID skips the check that Floors fills it. The sheet of the repo gives Plot its Q156 GUID and Filled by 5.
- Review 1 F8: the data type is written as given with no check, every row of the sheet of the repo is TEXT.
- R439, ScanStep hash cache: two writes of one size in one tick give a stale hash, no real input reaches it. R438_ScanStepTests gives each rewrite its own write time (Lane C, PR 126).

## Lane B Task 6, review of R457

No real input reaches these: the plot table of the demo holds whole numbers and clean area keys.

- ButtonScope.NbhCodes keeps any plot with a number and an area key, Resolve skips a number that is not all digits and trims the key. An NBH whose only units are numbered 12A is offered, then stops with holds no unit plot.
- PaneRunState.RefreshNbhCodes catches no InvalidDataException or ArgumentException. A work folder path with illegal characters would escape into the view activated handler.

## FIX-1NOV-A2, review 1

- ReportRun.cs Find and PlanOf type the stop of no 4 Check run twice, one text, no input reaches a difference.
- UpdateFromReportRun.Read gives PlanOf WorkFiles.ReadText and reads the plan again after the worker, a short window after its SHA-256 check.
- ReadPicks.Resolve repeats the proposal rule of ScanView (Lane C) and falls back to the proposal on a stale pick where the window asks again. One helper in ScanView, a Lane C change.
- ReadPicks.FilesOf reads addin.workbookRoles itself, ReadSettings.WorkbookRole is the rule of the worker, the same role on a valid config.
- ReadPicks.LatestScan reads a broken scan.json as no scan with no line. The scan writes by a temporary name and a rename.
- ReadPicks compares file names ordinally, both from one GetFileName. A plot outline source splits on ' + ', a layer named with it splits wrongly. picks.json is indented with the line end of the system.
- Breaker 1, R474: read.json sources record the resolved picks, a sheet pick whose header the worker does not find or a conflict pick ApplyConflicts skips still reads pick. No real scan offers a sheet villa type, 104 of 104 conflict plots are plots of the read.
- Breaker 1, a saved pick no longer a row of the newest scan falls back to the proposal with no line, and ScanStep.Latest may be a newer scan than the window showed. No real changed drawing reaches it.
- Breaker 2: Take gives a line and Resolve none for a refused proposal, only with picks.json resolved on other files than the request. The folder hash holds picks.json.

## FIX-1NOV-C, R445 Save setup

- Breaker 3: a workshared model synced by another user keeps the old settings in the pane, Save setup and button 1 then write them over. The test host runs one user.
- Breaker 5: a session made without LoadFrom (OnCreated, Seen) holds the defaults, so a template that carries the entity would be written over. No template carries it.
- Breaker 7: Save setup writes and adds an undo entry even with no change.
- Breaker 6: New project now waits for Revit to be idle, a second click while it waits is dropped. Loud, no wrong value.
- Breaker 8: a commit that returns Pending would roll back in the finally, failure handling is never delayed.
- Review F7: SettingsStore.Save repeats the read back of Record. Review F8: Schema and Entity of SettingsEntity are not disposed. Review F9: the result of ExternalEvent.Raise is not read.

## FIX-2NOV-C, R446, review

- PaneHost: Process.Start of Open folder has no catch. A Win32Exception would leave the click. explorer.exe is on every PC of the tool.
- ScanResultsSession.InsideInputs refuses the folder of every input. An input directly in Documents would refuse the default work root. Real inputs lie under the input roots.
- NewProjectViewModel.CanChange is always false, so the two Change buttons have no Click and stay collapsed. The sweep skips a collapsed button.
- No test holds Closing += OnClosing or the IsVisible branch, a shown window cannot be made in a test. The sweep passes any handler, an empty one too. Part 13 of steps/bader-fix-1nov.md checks the ask in Revit.
- WorkFolder.Inside compares by StartsWith, so inputs2 counts as inside inputs. The refusal only errs to the safe side.

## Lane D D3, R467, R468, R405, review

- ButtonsRunHost.Finish saves before it writes the result. A hung save past the step limit of 10 or 20 minutes ends Revit before the entry of that button is written. D3L ran 5.4 minutes in all.
- The test host log says ok for a button whose save failed, the failure is only in savedAfter. Write has its own unguarded Save after it. No save failed in D3L or D3S.
- villaLinkFolder with a full path is read in place with no hash before and after, only the prototypes input is hashed. The D3 specs name prototypes and masses only. prototypes with no prototypes input fails late, inside Revit.
- The R467 and R468 tests hold the text of the host and the script, the usual style for the test host. No test reaches the catch or the value of savedAfter. The runs D3L and D3S do reach the save.
- result saved of Write now seldom appears, since R468 saves first. Nothing reads it.
- The two R405 tests of AddInTests.cs set and read DesignValues.Current outside the alone collection, while other tests call UseConfigOf. No failure seen there.

## Lane D 6B-DATA, R469, review

- zone_compare.py stops the whole zone on a ramp corner list with no points, max and min of nothing. No plan of the 7 zones holds one.
- zone_compare.py reads each field through plan_compare.value_of, which swallows a missing key, so a renamed plan field would show only as toolNone counts. The 7 zones show none.

## Lane D 6B-DATA-REV6, R469, review

- zone_compare.py checks only the plan of --rev6-plan: a rev6 plan handed by hand as --without-overrides would pass under that title. The 5 zones hand the past job plans.
- check_rev6 shows a plan is rev6, not that it holds no override list, so a rev6 build with a list would read as no override list. The rev6 builds of the round name no list.

## FIX-2NOV-A, review 1 and breaker 1

- Review 1, the step floors cuts a yard by the ramp corners of a plot with no ramp type, the build does not. Only the native2022 profile reaches it, Q050 keeps it off.
- Review 1, a yard the cut empties with its level also missing gets another line in Python and C#. Breaker 1: 0 yards of the whole project are emptied.
- Review 1, FloorPlacer.EdgeNoise 1e-6 is a constant beside designValues.floors.shortestEdgeM. Clipper2 2.0.0 and pyclipr's 2.0.1 are one patch apart, parity held on real plots.
- Review 1, ReviewTable.cs and ReadDataRun.cs each hold a rule of which plots are units. Install-TestHost.ps1 gives the watcher Common with no Clipper2Lib, it does not reach the cut.
- Breaker 1, review_012.json keeps 42 checks of plots R476 leaves out, all OK, a Wrong on one could not be cleared. No real Wrong.
- Breaker 1, R477: no unit or no block name reads as other type and passes. Every real plan plot has a unit, the read names every block.
- Breaker 2, the Assumed row Q350 is 68 yards short: a yard with a link override reads from override, RECOMMENDED, with no R475 in its detail. Its level is right.
- Breaker 2, a yard cut reads RECOMMENDED by Q350, now closed, as Q053 and Q057 do, so Assumed names a closed question.
- Breaker 2, the rows R476 keeps on 01A, 02, 021, 022, 03 no pane path shows, the pick list holds 012 and 013, 11 of them hold many numbers (Q355).
- Breaker 2, a new DLL with a config of before FIX-2NOV-A reads a cut yard UNKNOWN, as with any config not refreshed (Q309).
- Yards still reach neighbours by under 2 mm of rounding on slanted edges, 286 and 86 pairs over 1 cm2 on NBH 012, so overlap warnings across plots remain (L2NOV-1).

## FIX-2NOV-A2, the helper's notes

- R480: two C10 on one edge leave open from the first start to the last end, 0 such edges on the 5 keys. An edge shorter than its span end stays whole, the lengths are 25 and 28 m.
- R479: a build with no 4 Check request gives no keo_file map, the footprint check compares by the own name with no line.
- Breaker 1, a mirror edit in Update from report on a C10 now moves its wall gaps, the apply line names no button 8. No real edit yet. A number in a type name such as BW 1800 is held out by the digit limits now. Near span rests on one length class, all side edges are 25 m but 2965.

## Q158 review and breaker, 2 Nov 2026

- A press on a step with DoneWithIssues writes 0 issues over the earlier record, so the step reads Done. Whether button 1 gives warnings on a real model is UNKNOWN.
- A sheet with no GUID for Plot gives a Waiting line and a press makes the step Done with 0 issues. R461 puts Plot in the sheet.
- A level outside the position file, moved by hand, stays old in site.json. floors.levels names only Level 1 and Level 2.
- An entity the reader refuses, as from a newer build, is overwritten by a press with the settings of the pane, as a real run already did.
- A check point over the pass limit with nothing differing rolls back at the read back with its reason, and the window gives no warning before the press. A model set by the tool matches exactly.
- While the pane has no input hash yet it shows Ready and the flow has hashed, so a press in that moment can hold and cancel. It lasts until the hash returns, not measured.
- An extra field added to site.json by hand counts as disagreeing and the file is written again.
- A level type with no base parameter gives a null in the facts, the file parses back as a null of another token type, so each press commits and steps 2 to 8 go back (RAN the compare). A Revit level type has that parameter.
- A level added or moved by hand, of no use to the build, fails the compare, so one press commits and steps 2 to 8 run again. Loud, no wrong number.
- A rules path that differs only by case commits once, then holds.

## R459 review 1 and breaker 1, 2 Nov 2026

- An AfterCommit that throws, or a move of floors_<NBH>_accepted.json that fails, comes after the warnings left the model. The line says the failure, the next step 5 raises none for floors left alone, so chip and sheet stay short. Needs a disk or lock failure.
- An unreadable earlier floors_<NBH>_accepted.json is taken as no earlier pairs and overwritten, while 4 Check shows no chip and 9 Report refuses the same file. Needs a hand edit or a read lock.
- FloorLoad.KnownWarnings reads the config on disk with no SHA hold, as button 6 has. No block or no file gives no list, so every warning shows.
- python accepted_rows raises on a file with a missing field where C# refuses it by name. No real file is malformed.
- A type named in two roles of floors.types takes the last. Today's config holds none.
- A floor moved by hand keeps its pair, the counts read floor ids, not positions. A later Move by step 5 does take it off.
- 9 Report with a floor reader that returns null lists every pair unfiltered. The document is active on the real path.
- ReportRun and CheckRun name the file of step 5 and one input role. A second step's list, as Lane C may add, needs both changed to show in the report and the chip.

## R483 review, 2 Nov 2026

- RevitCounts.ElementId casts each id to long and RevitCounts.Refusal does not check ids, so a hand edited revitCounts file with a non integer id throws rather than being refused. The add-in writes that file itself in the same run, so no real input reaches it.
- RevitCounts.FromTags counts an element tagged under two schema versions twice where FromElements gives one id. PlacedTag holds one version and PlacedTagStore.Write deletes older entities, so no real input reaches it until a version 2 exists.

## FIX-2NOV-A3 review and breaker, 2 Nov 2026

- valueStatus walls.ends wallOpen holds weak inline, rules/roshn-makkah.json holds the share 0.7409. If that share turns strong the inline entry stays weak.
- majority.front_edges_of takes every frontEdge line with no source filter or two entries refusal, the build filters and refuses. The list holds 6 lines, each plot once, all parsons.
- majority.py --keep-from an older list with no frontEdge, or --dry with a new list path, measures on the notch front. No current use found.
- corner_front.named_frame does not check the edge is the same edge after a re-read baseplan with another first vertex. The read is pinned by SHA-256, R388.
- BuildPlan.PlanWalls: a step with no endsSource reads keyRules and loses manualInside. A build remakes its steps.
- BuildSteps: a request with no overrides role takes the notch front. 4 Check asks every step.
- plan_from_book --override-fields without frontEdge drops the line, its insert line is gone too.
- Check table Use Rule on the frontEdge of 469: 469 falls out, 470 keeps -0.004 and stands about 55 mm off its key (worked out, not run).
- Lines left stale on the 5 notched plots, not applied by any build: rampOutline gaps measured on the old front, the reason no value of stepsFromHeight on steps lines.

## Lane D FIX-2NOV-D, review and breaker, 2 Nov 2026

- python/read_steps.py asks plan_rules.road_names with no basis, so the Python read steps take siteBuild.roadSurfaces under rev6 where the C# takes designBasis.roadLevel. They agree while the two lists are one, test_r490 holds that.
- python/level_excel_check.py takes the road order from config alone, so a level Excel of a build with a pick reads as a mismatch on the line Road surface order. No test host job names a pick.
- RoadPickState.Of reads and parses the config on each refresh of the pane and New project.
- PaneRunState.ConfigOf caches by path, time and size in four static fields with no lock.
- plan_from_book.py leaves an older plan at --out beside new step files after a stop. No script calls it so and it takes a person ignoring exit 2.
- Two files with one short name, A__Top and B__Top, show as two like rows and one name in the stop line. ["Road", "road"] passes the unique check and reads one file twice. A hand edited ..\..\x in a pick leaves the folder, read only. None of the real names do this.
- RoadSurfaceChoices.NotTakenWhy finds a short name as a whole name in the prose reasons of notTaken: a surface named as a phrase of a reason, Grading, is refused loudly with the reason, and Typical Cross Sections, written with an underscore before it, is not found.
- A saved pick naming a not taken file shows in the window as Not in the folder any more, the wrong words for it. It stays unticked and 2 Read data refuses it with the reason.
- noRoadStopShare 1.0 never stops, not even at 100 percent, a config value set that way by hand.

Round FIX-2NOV-C-CHECK, review 1 and the gate, 2 Nov 2026:
- WorkerRunTests R347 tests are load sensitive: 7 of 60 runs failed under 12 parallel test processes (RAN), a 1 s time limit fires before the worker answers, or the launch takes over 1 s. One CI run failed with status failed, the message is now printed, its cause UNKNOWN.
- Seven WPF tests failed once in a full net48 run while Revit started on the machine, all passed alone (RAN, 66 of 66).
- Plural by count stays open where no real input reaches it: Review data title, New project plots and types, ScanView plots and files, 0 of 1 rows.
- CheckTableState.Filter of two arguments has only test callers.

Round SPEED-2NOV-C, review 1 and breaker 1 of R447, R448, R449, 3 Nov 2026:
- test_window_lock.py leaves SITE_ADAPTATION_WINDOW_SLOT set in the test process, no other test reads it.
- A crashed holder of slot 2 is not cleaned while any window process runs, it clears when all windows end. It takes a killed terminal.
- With SITEADAPT_LISTENER_INPUT_ROOTS unset the C# side of the listener refuses no input root, only Start-RevitListener.ps1 sets it, and it always does.
- If every result write fails, the listener logs to Trace and retries each second, and the queue stands behind that job. Disk full or a read-only out folder.
- A failed SaveAs of listener_home.rvt leaves the small project open.
- AddinRunSession reads the start time of its process with no guard, test host only.
- A worker thread that ends before NextStep(WorkerStep) gets a wrong category in the timing, never a wrong outcome.
- A refusal the out folder blocks for one moment leaves the job in claimed with no result, the send exits 2 (RAN). A job that passed its check retries.
- A hand-made job naming a used run folder reports the old run_timing as its own (RAN). Send always makes a new folder.
- InsideRoot of the listener compares text only: forward slashes, a parent step, the long prefix and an 8.3 name pass it (RAN). IsPlainFullPath and CheckPlace stop them for a model. prototypeLoops and junctions as in or out folders are hand-made holes.
- Revit does not wait for Civil 3D on the slot side, Civil 3D waits for Revit. python/c3d_probe.json names a lock of another machine, so no Civil 3D run takes it today.
- A new process with the pid of a hard killed listener reads as a listener already there, until that process ends.

Round SPEED-2NOV-C, review 1 of the delta, 3 Nov 2026:
- A step past its limit that returns later overwrites its timeout line with ok in the job log. The result file keeps timeout.
- A result file removed by hand during the second marks stops the queue until Revit restarts. No script removes it.
- A heartbeat file held open makes that look of the listener skip its work, retried each second.
- The second marks of a job past its limit can repeat the D19 line in the error.
- A steps entry of buttons_result.json that is no JSON object would throw out of the look each second, ButtonsRunHost writes objects only.
- RevitFormat.cs is a port of python/rvt_info.py with no parity test. Two small drifts in how it reads the Format key, neither on a real prototype read.
- Three copies of the input root list: Start-RevitListener.ps1, Get-RunInputRoots, Run-TestHost.ps1. They agree today.
- Timing labels only: the second Decide runs under villaMassFiles, a wait on the load dialog is billed to the step before.
- The pane lists the Prototypes folder twice per refresh. RevitFormat walks a long chain only on a corrupt file.

Round SPEED-2NOV-C, breaker 1 of the delta, 3 Nov 2026:
- A person's Cancel on the load dialog of button 7 ends as RolledBack with the failed text, the catch of ButtonFlow is older than this round. No recorded run reached it.
- The long prefix and a UNC path of an input root read as outside it in the check of a .slog (RAN, strings only). The real specs name plain drive paths.
- A config with no input root leaves every .slog of a backup folder out of the check. The real config gives 3 entries (RAN).
- A file removed during an own run stops the after check with Get-Item's error, loud with the wrong cause. The listener says could not be marked.
- The scope of RevitListenerJobVariable disposed twice would clear the variable of the next job (RAN). Only Finish disposes it, once per job.
- A person's click on button 2 during a listener job skips the scan gate and its worker counts in that job. The lanes only send.
- R496 stays silent: a 2021 file past the 20 files it reads, a file from before Revit wrote Format, a file locked with no share, an empty row Prototypes where the build falls back to config. Real folders hold 13 files, 2021 or 2024 (RAN).

Round SPEED-2NOV-C records, 3 Nov 2026:
- R347_the_launch_returns_at_once_and_a_worker_past_its_time_limit failed once on the Windows CI runner on net8.0, status failed, the worker ended with code 0, on PR 152, records only. Same flake as the R347 lines above, net48 passed in that run. Test left as it is.

## Lane D MASS-2NOV-D, review and breakers, 3 Nov 2026

- Arcs: no arc wall in the 13. Not tested: a clockwise arc wall, a concave one, a far face arc, arcs meeting at an angle, a full circle, a radius under half the width. What Wall.Orientation gives on a curved wall is UNKNOWN.
- WallRingBuilder.Walk is greedy and stops on its first edge. A wrong early close that skips a part passes when the share stays at 0.90. None of the 13 does it.
- A join 1 to 5 degrees off parallel crosses beyond the corner gap, the ring is refused. Real angles are 90 and 180. At an acute corner the end of one wall pokes through its neighbour and splits that edge.
- Start corner of the points moves with the order of walls on a tie of two equal sides, TH1 and TH3, same shape. Only the point order of the sidecar changes.
- A rejected ring keeps its build, so a sidecar could list wallsUsed under source slab. A null slab throws a bare null error, the reader throws first. None of the 13 reaches them.
- MassFiles.Stale tests the reader version by not equal on one shared folder, so a build of another version and this one remake each other's masses. It ends at the merge.
- faces_check2.py takes the whole face of a wall at all heights, so an edited profile counts as covering above its ground stretch. Breaker 2 read the profiles and found no fault.

Round FIX-3NOV-B, R498 and R499, review 1 and breaker 1, 3 Nov 2026:
- A failed move puts back the run before while the model keeps the new one, so a rerun of 5 to 8 with an output held open shows the old run Done. The HeldOpen message says so. No real output of 5 to 8 was found with a name another program holds.
- An exception other than an IO or access fault between the commit and Keep disposes the group, which rolls back a committed run, loud. The status of Assimilate is not read. No real input known.
- The settle write failing after a failed move says the step is done though it is not, and the first write stands. Needs a held file and a refused transaction at once.
- The record can roll back a step: a workshared model with ProjectInformation held by another user, whether SetEntity throws is UNKNOWN, the host runs one user. An empty roadSurfaces pick, whether the window can give one is UNKNOWN. Issue text with no cap, the size limit of a Revit string field is UNKNOWN. All loud.
- Clocks of two PCs: a rerun of 3 on a slow clock stays older than 4, and 4 stays Done. An entity of a later formatVersion is overwritten by any button 2 to 9. A session made by Seen with no LoadFrom holds defaults, the flow stops first with no config chosen.
- The pane stays live while a worker runs, with no busy guard. A pick of another NBH during 4 reads Done for it, in the session and after a reopen, and step 5 then stops loud on no plan. A press of 1 during a worker: the end is later, so the worker reads Done on the site before. Older in the session (PaneRunState.RunOf), read only.
- Button 1 keeps no outputs: site.json deleted by hand shows row 1 Ready in the session, Done after a reopen, row 2 holds by its own. Next to Q382.
- The test host keeps its work folder apart from WorkRoot, so after an undo or reload every row with outputs reads Ready in a host run. A false fail, no job reads the rows today, a Revit proof of a reopen goes through the windows.
- A hand edited entity with output keys that differ by case or hold an invalid path character throws from StepRun.FromSettings. A machine file broken while Revit runs now throws from a pane refresh. A work folder copied by hand keeps absolute names in read.json.
FIX-3NOV-A, breaker and review, no real input reaches these:
- CheckRun never adds the role civilFile, so under rev6 with doorLevel.source civilFile button 4 stops as floorOutlines did. The shipped source is roadPlusSlope.
- python/zone_compare.py:205 reads the mode with no project mode, an offline compare of the past job (Q224).
- CheckRun.cs:476 wraps a worker refusal of R485 as the build ended refused, R493 passes plain. The read and the plan stop it first.
- design_basis.py:159 lower against OrdinalIgnoreCase of RoadSurfaceChoices.cs:47 differ on odd Unicode, the stems are ASCII.
- A settings file with a mode its config does not list is refused whole, SavedStarts leaves such a file out. It needs a second project whose past mode has another name.
- A config with no notRoad reads as an empty list under rev6. All 4 older copies of Saved projects lack it, none reaches rev6 since a project that names a config stays on its mode.
- A reinstall leaves every project copy of R486 as it was, a later rule fix in config does not reach it, and nothing records the install that made the copy.
- Settings hold the absolute path of the copy, a work root that moves keeps writing under the old root while that folder stands.
- A model name of about 92 characters or more can reach the name limit of R486, not tested.
- PickConfig has no production caller, the pane never picks a settings file.

Round 6C-3NOV-C, review 1, 3 Nov 2026:
- ScanView.cs about line 170, StartsWith |xref repeats the form CadScan.cs line 190 writes, with no shared constant. A rename shows the file Not read, the safe side.
- ReviewDataWindow.xaml, Mirror column 72: an unread mirror shows not read yet, about 71 against 64, cut with an ellipsis. All 337 plots of the run read Yes or No.
- Review data, the head Team check sits about 10 px left of the chip text, cosmetic.

Round RULES-3NOV-C, review 1 and breaker 1, 3 Nov 2026:
- RuleValues.Read stops the table when a rules file holds no number at a path of the catalog, a loud box, and Manual rules can not be reached from it. The two rules files on disk hold them all.
- RuleValues.Text writes 3 decimals, a value typed with more shows rounded in an export. All real values hold under 3.
KNOWN-3NOV-A, review and breaker, no real input reaches these:
- CheckTableBuild.cs:33 reads overrideDefaultSources with no check. A config with no such key would grey every Past, PlanOverrides.Load stops such a run first with the line of R330.
- A key listed in two groups of majority.groups: OverrideEdits takes the first group, OtherJobKeys the last. The groups of config are apart.
- REPORT-3NOV-B: ReportFields has no writer until Lane C's New project section. A volume with no <nbh> passes on one NBH and Update then stops. A report left under a name of fields since removed is not named. A plot of two numbers has no old number in Plot schedule (0 such plots on 7 NBH, RAN). Old revit counts give no Walls line.

## Lane D PARAM-3NOV-D, review 1 and breaker 1, 3 Nov 2026

- The unfilled template of TemplateBook at the config path gives a green 0 parameters. A sheet that does not read is Found in Check files of New project and amber in the pane. None is in use.
- An xlsx with a relationship lacking Target throws a null error in ParameterBook that DiskProbe.ReadSheet does not catch. No real sheet has one.
- A Filled by 7 row of a hand edited sheet that names Floors and no RVT Links stays on Floors, button 7 finds no parameter on the link. No such sheet exists. ParameterSheet.Counts says 1 parameters for one row. The real sheet has 11.
- RefreshSheet reads the config and the xlsx on the UI thread on each refresh, uncached, not measured. A swallowed config failure reads Not chosen with no reason.
- docs/design/07-mock.md still says Plot on floors and walls. R507 prevails.
- A rebind of a links row that also lacks the link category counts as a removal alone. No model holds a row off the links and on Floors.

## Lane D MASS-3NOV-D, 3 Nov 2026

- R530 builds no cut across an arc, no prototype has an arc wall. A concave side crossed twice stays whole. Neither is in the 13. A new prototype needs its own party lines in config, none means no cut.
- MassCut.NoShortEdge drops the next corner and keeps no longer neighbour's line, unlike R503, so a long edge would slant by up to 4 mm. No corner goes on any of the 9 cut rings, shortest 20 mm.
- After a cut wallRingMinShare and the slab middle are not tested again, real shares stay over 0.9. A read back drops NotCut and UncutAreaM2, no run line lists a refused cut, none of the 9 is refused. A misspelt maxX cuts nothing and says nothing. A revised prototype is remade against the old line.

COORD-3NOV-A, review and breaker, no real input reaches these:
- newProjectCheck has no sender in the add-in. WorkerRequest.CoordinateSource and PositionFile wait for one, until then the check reads coordinates.source of config.
- ProjectElevationM once coordinates.originElevationM is not 0: ReadRules adds that height to every level, no test holds it against Revit. Config holds 0.
- A corrupt position file under CAD: the check line covers a JSON, format or IO failure, a parser failure of another type fails the run.
- A picked position file of another job: any file of the format is accepted. Under CAD the row File origin to the plots shows the distance, under the file nothing does.
- A baseplan revision on a held CAD model: a new plot box moves the rounded centre, only Will change says so, unless R523 stops it.
- More than one named location: the read and the check take the active one.
- Under CAD the levels come from the profile of config, not the rules file picked in the window.
- A baseplan in local coordinates whose box falls inside the UTM range passes OutsideZone.

## Lane B 6b, breaker 1 of R557, 4 Nov

- A unit number with a space or letter: IsUnit true, Resolve skips it, NbhCodes offers the NBH. 0 of 2,558 real rows.
- A unit row with no nbhId is filed under an empty NBH and counted in Whole project. 0 real rows.
- A number held as a JSON number reads as no unit. PlotTable writes strings.
- A label plus shift that hits a schedule of another NBH gives a wrong official number, which UnitNumber prefers. 1 of 747 differs from the location number, 2312, which is right.
- noTemplate with a keyCopy opens the key copy, [bool] of the string false reads true. No spec does either. The guard still asks for the template file of a no template job, harmless.

RAMP-3NOV-A, review and breaker, no real input reaches these:
- A civil file door source, path null in config: the door stays the file's, a back joint takes its first corner, a ramp can stand off El @ Entry.
- join true with joinBack false: no config holds it, only tests run it.
- A joint mixing front and back corners, 0 of 4,121. Chains of 3 plots or more, 0.
- A 2% unit whose only back partner lies outside the build: 0 plots differ on NBH 012 and 5 TU zones against the whole, ima and imb not compared.
- A partner the plan never writes sets a written plot's slope: 14 back joints, no level moves today.
- A corner past maxMitreM stays unpulled, largest real move 3.162 mm against 10 mm.

## Lane D TOPO-3NOV-D, breaker 1, 4 Nov 2026

- RoadTopography: a scope with no ramp passes with no row, nothing proven. RoadTopographySettings.Number takes any range, a gap of 0 or a margin under 0 needs a hand edit. Grow mitres a sharp hull corner far out, NBH 012 matches shapely's limited mitre with no difference.
- FloorsRun: a surface delegate giving null ends in a null error line, and a plan with no NBH runs as all. FloorLoad.RoadSurface: a config path gone from disk ends in the plain file error, as an issue line.
- RevitFloorDocument.CreateToposolid duplicates the first type whatever its thickness, the report names it (R572).

## Build-1 R1-build-1, stage 1 data check, second review, 5 Nov 2026

- DataCheckFind.cs:166 BreakTies takes the candidate with the fewest layers when candidates tie and never checks it is a subset of the others. 0 of the 5 runs broke a tie.
- DataCheckFind.cs DecideWords, a sheet header that parts the plots as the groups do. Narrowed in R2-C1-build-1 by R634, the column must name two groups and wordShare of the groups, so a one valued column no longer fits a level of one big group and a lone plot, as it did on the real inputs. A 2 group level still fits a true 2 valued column.
- config/project.json dataCheck.scan.labelPattern has no status entry, the past profile holds the same name, so the status tool treats it as shared.
- No unit header gives wrong groups with a weak mark and a line: DataCheckSettings.cs:131 and :144, DataCheckFind.cs:74. All five real runs state metres.
- An insert in a hole is credited to the plot for villa type, DataCheckFind.cs:252. Only the mirror leaves the hole out, HoleWhy says so.
- A scale of 0 reads not mirrored: CadScan.cs:97.
- Numbers are compared as exact text: DataCheckFind.cs:465 and ScanLogic.cs:318. Only a risk if a drawing mixes case or leading zeros.
- A killed run or a Ctrl+C can leave the developer add-ins until the next run or install, TestHostPaths.ps1. Two runs a few milliseconds apart can race on them. Developer PCs only.
- Measure-ColdStart.ps1 treats the Invalid Signature box on the tool's own files as another maker's, a matter for SETUP-sign-2.
- Run-RevitTests.ps1, the finally builds a new process tree, a Revit left by a dotnet that has ended stays open. The test does not reach Revit today.
- Flaky in job tests, cause not measured: R104 FreezeGuardTests, R347 worker time limits, R405 AddInTests on net48, a value came back null in 12 ms, which reads as shared state between tests.

## Build-1 R2-C1-build-1, stage 1 gaps, 5 Nov 2026

- DataCheckCodes.cs ColumnCells: a plot listed twice on a sheet takes the first row, a second row with another code is not said. The proposed column of the real run lists each plot once, not measured for every candidate.
- DataCheckCodes.cs TableAt: a code table is two columns side by side, a column between them hides it, and the person types it. Its header reads as a column letter when a title row sits above it, only the source text shows this.
- DataCheckCodes.cs TableAt: the old side is the one the drawings use more, a table whose drawings use neither reads left to right.
- DataCheckCodes.cs Typology: a column of code shape that runs across the groups and differs from the drawing qualifies though it holds no type, it ranks last with no plot of a prototype. The window shows the first and the rest.
- config/project.json dataCheck.codePattern and groupNamePattern have no status entry, rule_status.py reads them as an input, as dataCheck.scan.labelPattern.
- ReadInputs.cs FolderInput: a work folder inside the input root would change the hash of the folder on each run, so no read is reused. Runs write under the work root, D19, and no project has it so.

## R2-C1-build-1, second review, 5 Nov 2026

- ReadDataRun.cs:305 Reusable ignores files not read and a stopped check, so a raw read made while a file was locked is reused on the next press with no new run. Revit-test's wiring of NoPastFiles reaches it, so look again then.
- DataCheckAreas.cs:154 ApplyArea takes the first area of a plot listed twice with two areas, with no word.
- DataCheckCodes.cs:58 KeyColumn takes the first column on a tie, so a serial number column could join codes to the wrong plots. The scored runs joined well.
- DataCheckCodes.cs:348 FindColumn with no workbook takes the first sheet of that name.
- ReadInputs.cs:283 FolderInput throws on a locked subfolder where the worker notes it and carries on. It is said, not silent.
- DataCheckMarks.cs:366 the mirror line says the folder does not give the hand even when hands are confirmed, if every block is of another type.
- DataCheckCodes.cs:674 typologyColumn reads usable, gave 0 plots a code, when a confirmed column holds only words. The villaType need does read missing.
- DataCheckLogic.cs:93 the 160 character cut can drop the tail of a long villaType line. The mark stays weak.
- DataCheckMarks.cs:485 areaM2 under an assumed unit carries no weak of its own. All five real runs state metres.
- Plots a confirmed area column does not list have no neighbourhood and cannot be picked in Review. The count is said in the line.
- codePattern and groupNamePattern have no status entry.

## PR 207, second review, 5 Nov 2026

- DataCheckAreas.cs:106 and DataCheckCodes.cs:232 pass over a column whose name two columns carry and no line says so, DataCheckCodes.cs:704 and DataCheckAreas.cs:194. S3 proposes 11 areas where the others read 13, and a person who presses Confirm is not told. Only S3 reaches it, a copy whose headers were rewritten. A real workbook with two columns of one name makes it REAL.
