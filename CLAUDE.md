# Site Adaptation Tool

Read-only data readers for the ROSHN Makkah site adaptation work. No placement logic yet.
The rules live in docs/rules.md and nowhere else. This file points to them and adds the working agreement.

## Roles

- Bader decides. Every open question goes to Bader as a numbered list with the recommended answer first.
- Chat drafts the prompts and reads what Code did.
- Code does the work and checks it. Code never marks a thing done without a second check by a different method.

## Start of every session

1. Confirm the folder is the repo root, the branch is not main, and `git fetch` shows main level with origin.
2. Read steps/PROGRESS.md, then the top entry of steps/log.md.
3. Open a new branch for the round. Never commit to main. A hook blocks commits and pushes to main.

## Repo map

- python/ holds the PDF reader, the Excel reader, the link table and the full run script, plus tests in python/tests.
- dotnet/CadReader reads DWG and DXF with ACadSharp.
- dotnet/RevitPrototypeReader is the Revit 2024 add-in. Do not change it unless the round says so.
- dotnet/SiteAdaptation.Common holds JSON output and unit helpers.
- config/cad_layers.json is the keep and drop list of CAD layers. Bader edits it.
- docs/rules.md holds the rules with ids R1 and up.
- steps/log.md holds the round log. steps/PROGRESS.md holds the live state. steps/history/ holds old rounds.

## Rules in short

The full text is docs/rules.md. Ids are the contract, a test names the id it proves.

- R1 Read only. A reader never changes the file it opens.
- R2 Numbers are rounded to 3 decimals, El @ Entry and Height keep 4.
- R3 Small output. Only the fields the rules list.
- R9 Excel reads from the header row with Plot No. and stops at the first row with no plot number.
- R10 No invented data. Tests use small dummy files built in code.
- R11 Zero tests is a failure.
- R12 Every rule has a test whose name carries the rule id.
- R16 and up cover explode, layer filter, plot labels, PDF rows and the link table.

## Real files

- Real drawings, sheets and workbooks stay where they are. Readers run on them in place.
- Never copy a real file into git. sample_data/ and sample_output/ are ignored, keep them ignored.
- Never commit real coordinates or levels. Logs and reports hold counts, row numbers and distances only.
- Full run outputs go to sample_output/full_run/<file name>/.
- A file that fails or times out is recorded with its exact error and the run carries on. Never swap in a dummy file.
- The Revit reader is run by Bader by hand.

## Commands

```
py -m venv .venv
.venv\Scripts\python -m pip install -r requirements.txt
.venv\Scripts\python -m pytest python
dotnet build dotnet\SiteAdaptation.sln -c Release
dotnet test dotnet\SiteAdaptation.sln -c Release --no-build
```

The gate is both test commands green. Run it locally before every pull request.
If `py` is missing, call python.exe from the Python 3.12 install folder.

## Writing rules

These apply to every file, commit message, pull request text and report.

- No generated-by footer and no co-authored-by line.
- No emoji.
- No em dash.
- No semicolon in prose, comments, Markdown, JSON, YAML or Python. C# statements keep the semicolon the language needs.
- No comment that restates the line under it. A comment says why.
- Keep names and titles short.
- Say what happened. If a test failed or a step was skipped, say so with the output.

## Pull requests

- One branch per round, named type/short-topic.
- Title states the round and the topic.
- Run the gate, push, open the pull request, merge it once the gate is green. Do not wait for approval when the round says so.
- After a merge, delete the branch on the remote and pull main.

## Log shape

The top entry of steps/log.md fits on one page and has these headings in this order.

1. Done
2. Remains
3. Known bugs
4. Next
5. Your turn

Your turn is one numbered list of every open question for Bader, recommended answer first.
steps/PROGRESS.md is 60 lines at most and holds state, done, open bugs, Bader's turn and the next three steps.

## Checking work

- Check each output a second way, with a different tool or method than the reader uses.
- Report counts of matches and mismatches.
- Open a handful of random items by id and compare them across sources.
- Never resolve a conflict silently. Write it to a conflicts list.

## Hooks

.claude/settings.json holds a hook that stops git commit on main and git push to main.
A hook only loads when a session starts in the repo. A session that started elsewhere has no hooks, say so in the log.
