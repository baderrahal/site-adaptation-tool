# Decisions of 28 Oct 2026

Bader's decisions of 28 Oct 2026, written down in Lane B L0 from his prompt for the laptop lane. Each holds from that day. Where a decision changes a shared record, the change waits for the fold in of steps/lane-b.md.

## D1. Two machines, two lanes

- Phase 5 and 6 run on Bader's company laptop, 64 GB RAM. This is Lane B, the laptop lane.
- Phase 4 (4g2, 4g3, 4h) runs on his personal PC.
- One phase per session on each machine.
- Lane B runs L0, 5a and 5b while Phase 4 is open, then stops until 4g2, 4g3 and 4h are all marked done in steps/01_next.md.

## D2. Light mode

- Button 4 gets a setting Villa mode, Masses or Links. Default Masses for test runs.
- One solid per villa, its base the exact base boundary shape of its prototype, read once per prototype file and cached by the file hash.
- Each placed base is checked against the villa outline of the CAD building footprint. Over a limit in config/project.json it flags the plot, never stops the run.
- Height from config/project.json. The same shared parameters and element tags as the link.
- Links mode swaps each tagged solid for its link at the same transform in one transaction with roll back.
- Phase 6: all zones in Masses, one NBH in Links for the final compare.
- The design is docs/design/11-light-mode.md. Its rule id is given at the fold in.

## The base boundary requirement

The base of each villa solid follows the exact base boundary shape of its prototype, every corner, never a bounding rectangle. Bader cares about this shape most of all. Where the base boundary sits in a prototype file is measured on one prototype at the Revit run after 5c, never guessed.

## D3. The window rule, closes Q021

- Before the Revit run after 5c, one Revit window at a time.
- From that run on, the number of windows open together is (free RAM minus 8 GB) divided by the peak that run measured for one window in that mode, rounded down, at least 1 and at most 3.
- The shared window lock per copy and the 2 GB floor hold in every window. Never two windows on the same file. Bader's words name it "the 2 GB floor of R310". The floor is minFreeGB of R200, and R310 samples the free memory per window without stopping at it (steps/PROGRESS.md, open bugs).
- Q021 "Parallel windows over 8 GB free" closes with this answer at the fold in.

## D4. The report reads back, closes Q121

- The add-in gets a button "Update from report", Phase 5f. It reads the report Excel back after the team edits it, lists each change against the model, checks it against the rules and applies it as an override.
- So the Excel files written from 4g3 on are readable back. Every sheet the team may edit, Levels of the level Excel and Placed of the report Excel, holds the plot number as a stable key in its first column and one row per plot and kind.
- The editable columns are FFL, El @ Entry, Steps, Villa type and Mirror. Their header cells carry a light fill and their list is written into the Settings sheet. Every other column is read only.
- The Settings sheet holds the build id and the SHA-256 of the plan the Excel came from, so the update refuses an Excel of another build.
- The labels and the order of 7.5 and 7.6 of the mock stay. The additions are items 10 to 12 of 7.7 of docs/design/07-mock.md.
- Built in Round ADDIN 4g3, the level Excel in PR 4 and the report Excel in PR 5. A part that does not fit four changes per PR goes into the last PR of 4g3.

## U1 to U7. The UI, approved on the canvas "Site Adaptation UI"

Bader designed and approved the UI on the canvas on 28 Oct 2026. The full text is docs/design/12-ui-spec.md. The prompt names the approvals U1 to U7 and does not say which part each covers, so the split below was CHOSEN by Code, and Bader kept it on 28 Oct 2026 (Q128 of steps/questions-closed.md).

- U1. The rules: one theme file Theme.xaml and the test that holds it, labels from the mock, plain rule names, a change to the look is Bader's, a screenshot per window.
- U2. The font: Libre Franklin bundled, the fallbacks and the five type sizes.
- U3. The colours, the 21 keys SA.Surface to SA.Error.
- U4. The sizes and the three button looks.
- U5. The eight icons and the ribbon of 7.1.
- U6. The pane of 7.2 with its NEW items.
- U7. Set up site of 7.3 and the check table of 7.4 with their NEW items.

## D5. Speed rules S2 to S5, 28 Oct 2026

Written down in Lane A 4g3 on 29 Oct 2026 from the Lane A prompt.

- S2. Parity on the 7 keys in each PR. The whole project once, in the last code PR of the phase.
- S3. One review pass for a PR that changes only records, docs or UI text. Two passes at most for logic.
- S4. Override naming: a value lists every override that touched its inputs. No more attempts at which one moved it.
- S5. No design freeze. A new design change from Bader goes to the phase that owns that part as a join item, recorded as his decision. If it belongs to the current phase and fits, it is done there.

## D6. The C# checks on the laptop, 29 Oct 2026

- Lane A runs the C# reads and the C# parity on the whole project and the 7 keys, net48 and net8.0, and measures the build peak three times per runtime: in 4g3 for everything since PR 75, in 4h for 4h. A failure is a REAL finding of that phase. This closes the laptop part of Q111.

## D7. The report round trip, 29 Oct 2026

- Bader kept D4 for the report Excel of 4g3: the plot number as a stable key in the first column of Levels and Placed, the editable columns FFL, El @ Entry, Steps, Villa type and Mirror marked in the header and listed in the Settings sheet, the build id and the plan SHA-256 in the Settings sheet. Items 10 to 12 of 7.7 of docs/design/07-mock.md hold it.

## D8. Q122 and the speed of 4g3, 29 Oct 2026

- Q122 answered as recommended, closed in steps/questions-closed.md. Its code, the columns FFL by rule and FFL as written of the sheet Clusters, goes to the first PR of 4h, since Bader ended 4g3 with its end records after PR 99 of Lane B. A draft stopped mid-work sits on the local branch feat/addin-4g3-q122-clusters-ffl at 00d371f of the Lane A clone, untested
- The build peak is measured once per runtime, not three times. A records-only PR gets one review pass

## Decisions of 29 Oct 2026, records and speed

Bader's decisions of 29 Oct 2026, written down in Round FOLD from his prompt of that day. D8 above is taken, these are D9 to D19. Each holds from that day.

### D9. 4h in two parts

- 4h-1: first the Q122 code of D8, taken from the local branch feat/addin-4g3-q122-clusters-ffl at 00d371f of the old folder "Site Adaptation Tool Lane A" if that folder and branch still exist and the code holds, else written again, with its test. Then R367 settings and R368 element tags.
- 4h-2: R366 scope and step state and R369 the second method. R366 joins the pane as a join item of the Lane B phase open when 4h-2 ends.

### D10. Lanes and merged phases

- Lane A: 4h-1, 4h-2, then 5e button 9 Report and 5f Update from report in one session, then its share of 6b.
- Lane B: 5b, then 5c and the Revit run after 5c in one session, then 5d, 6a, its share of 6b, then 6c.
- The test host jobs of all buttons (R384) move from 5e to 6a.
- Each phase in steps/01_next.md has a Status line that names its lane and a line "Starts when" with the phases it waits for: 5c waits for 4h-1 and 5b, 6a waits for 5d and 5f, 6b waits for 6a, 6c waits for 6b.
- 6b splits the 7 zones: Lane A IMA, IMB, IMC, Lane B IMD, IME, IMF, IMG, each by the window rule D3 with both lanes counted.

### D11. Revit probe first

- Before a phase runs Revit through the test host, it lists each Revit API behaviour the run depends on that no earlier run measured and runs one small probe per item.
- When a Revit run fails, the third attempt becomes a probe of the cause and the phase carries on. It stops for Bader only if the probe does not find the cause.

### D12. Reviews

- The breaker reads a PR only when it changes build logic, numbers in a plan or an Excel, or the override list.
- Every other PR gets the reviewer, with the UI checklist where a window changes, one pass. Logic PRs keep at most two passes.
- The claim-checker reads only the end records PR of each phase, and reads only the claims of the log entry, never whole files.

### D13. Records trim

- The top entry of steps/log.md and Bader's turn in steps/PROGRESS.md list only the questions opened or closed in that phase, by ID and title, plus one line with the count of open questions and the path steps/questions-open.md.
- python/rule_status.py takes the open questions from Bader's turn of steps/PROGRESS.md (READ, _questions), so until it reads steps/questions-open.md instead, Bader's turn must hold every open ID. That change is a join item of 4h-1 (CHOSEN in Round FOLD, the round changes no code).

### D14. Bigger PRs

- At most 8 changes per PR. A phase aims for one code PR and one end records PR.
- The STATE RUNNING line rides in the first code PR, no separate records PR at the start.
- Two developer helpers may work at once in separate worktrees on separate files, one PR.

### D15. Inputs on the laptop

- Rule 3 of the Lane B rules changes: no new copy from the personal PC. 5c checks the laptop inputs by key_copies.json and the hashes of Q126 (QL1).
- The first step of 5c puts together the inputs the Q111 run of 4g3 lacked, the location plan, the tables and the position, from C:\02-Site Adaptation, and runs the 7 read steps that 4g3 could not run, before any button work.

### D16. Models, effort and turn caps

- Frontmatter of .claude/agents: developer opus, effort high, maxTurns 120. developer-light, new, sonnet, effort medium, maxTurns 80, for UI, install, records, docs and tests-only PRs. reviewer sonnet, high, 40. breaker opus, high, 60. claim-checker sonnet, medium, 30. prober sonnet, medium, 40. log-reader haiku, low, 20. runner haiku, low, 40.
- A helper that reaches its cap reports what it found and stops, and the lead treats what it did not reach as UNKNOWN.
- CLAUDE.md names which helper takes which PR. The main session runs on opus at effort high, set by the runner.

### D17. One repo, two lanes, the lane runner

- Both lanes use this one repo. Lane A runs in the repo folder. Lane B runs in a git worktree of this repo at the sibling folder "Site Adaptation Tool Lane B WT", made by the runner with git worktree add --detach if it is missing.
- tools/run-lane.ps1 -Lane A or B loops: git fetch. Lane A: git switch main and git pull --ff-only in the repo folder. Lane B: git checkout --detach origin/main in its worktree, since main is checked out in the repo folder. If steps/STOP-<lane> exists in the repo folder, it exits. It runs claude -p with the text of steps/START.md for Lane A or steps/START-B.md for Lane B, from that lane's folder, with --model opus --effort high --permission-mode auto --max-budget-usd 200, and writes the output to %USERPROFILE%\Documents\SiteAdaptation\runner\<lane>_<yyyyMMdd-HHmm>.txt.
- The last line of the output: "PHASE DONE <id>" loops at once. "BLOCKED <reason>" waits 10 minutes and loops. "QUESTION <ids>" beeps, prints the ids and exits. "LANE DONE" exits. Any other end or a non-zero exit code, a git lock error included: wait one minute, retry once, then exit with the last 20 lines printed.
- The script never edits a tracked file of the repo. steps/STOP-A and steps/STOP-B are in .gitignore.

### D18. Start files

- steps/START.md runs the first open phase whose Status names Lane A and whose "Starts when" phases are all done. steps/START-B.md does the same for Lane B with the Lane B rules of steps/lane-b.md.
- Item 1 of Start of every session in CLAUDE.md accepts a detached HEAD equal to origin/main as level with origin, for the Lane B worktree.
- In both start files the preflight also runs git worktree prune and deletes local branches already merged into main.
- Both keep the planning line first and the STATE line rules, and both end with exactly one last line: "PHASE DONE <id>", "BLOCKED <reason>" when the next phase of the lane waits on another, "QUESTION <ids>" only when a question blocks all work of the phase, or "LANE DONE". A question that does not block goes to steps/questions-open.md and the phase carries on.

### D19. Real folders guard

- No link, junction or symbolic link may point into C:\02-Site Adaptation, C:\10- ROSHEN MECCA or any input root of config. The test host and every run write only under the work root.
- .claude/hooks/guard-inputs.sh, beside guard-registry.sh, blocks mklink, New-Item with ItemType Junction or SymbolicLink, and any write command whose target is under an input root.
- Before and after each run that reads real inputs, the run records name, size, write time and SHA-256 of every file it opens under an input root and fails on any change. That is code of the test host and the readers, so it is a join item of 4h-1, the next phase to run on real inputs (CHOSEN in Round FOLD).
- The slip of the Q111 run of 4g3, a request file written into the real Prototypes folder through a link, is in steps/latent.md as a REAL finding closed by this guard.
