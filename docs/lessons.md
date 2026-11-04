# Lessons

One line each, no repeats. The first 10 are copied into CLAUDE.md under "Lessons, read first". The lesson lines below come from Bader's setup round and the Lessons, wish and traps parts of steps/handover/.

## Read first

- Past job outputs are for compare only, never build from them.
- Never tune and test on the same data.
- Show the tool from an empty model with nothing pre-run.
- Write UNKNOWN instead of guessing.
- Every value carries its source.
- A rule measured on past models is a past measure, not a manual rule.
- Pane words must match what is built.
- A change to floor outlines can make older models skip floors, test on an old model.
- A locked screen stops Revit checks.
- Runs with no one watching froze on Revit's Load Once prompt.

## Way of work

- Long sessions drift, start fresh each task.
- Merge a green PR at once, never two waiting.
- Run a claim before you tell Bader, a read of the code is not a run.
- Check with a second method that shares no code, and chase any gap in the counts.
- Read the result file, never the exit code.
- Before blaming a spec or a hash, check the second cause.
- Name a choice as CHOSEN by Code in the file itself, never credit Bader for it.
- Write the limit in the PR when the check differs from the order.
- A fix found late gets less checking, say so.
- Take an id only after checking origin/main and the open branches.
- Close a round with its choices answered, 197 open questions against one person stacks later rounds on guesses.
- Keep Not proven lists in a shared file, not only in PR bodies.

## Proof and data

- Remove the input before you claim the tool does not need it.
- State who set the numbers on any demo sheet, a script with expected values reads as staged.
- Put the count of past values copied beside every match figure.
- Run every zone early, one neighbourhood hid that 5 of 7 stopped at button 2.
- Measure the size of the input before a round starts, one round met 95,177 points against a limit of 10,000.
- Decide "no coordinates" by position and points only, a template model reads zero yet its site location says Boston.

## Revit

- Prove in Revit early, some faults only show there after a green gate.
- A green job says nothing about a window, press the real window once per build.
- A hidden window still takes a UI automation click, so a driver press proves nothing about what a person sees.
- Write "not seen in Revit" in the record each time, tests do not stand in for it.
- Merging before the Revit pass moves the faults to a second PR.
- Recheck a round's test model against main before it starts, another lane's merge can change what the model does.
- Check the head of the folder you build from before a Revit run, the installed build comes from there.
- Keep job state outside the repo in one folder per round.

## Git and tools

- Never run gh with --delete-branch while a worktree sits on that branch, delete the remote with git push.
- Do not run git worktree prune without Bader, some entries are stuck on permission denied.
- Stopping a process tree can kill the helpers of your own Revit job, stop jobs by their own file.
- Scripts sent through a Bash heredoc lost double backslashes, use a file writer.
- A stray cat with a redirect or a bare python with a dash hangs a command.
- Never change a key to fix a display, saved picks stop matching.
