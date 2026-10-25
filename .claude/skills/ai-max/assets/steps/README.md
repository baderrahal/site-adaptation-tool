# steps

The worker's notes on this repo, written by Claude Code, merged to main, and read on a phone.
Short lines. One action per step. Every command in a code block.

## Files

- `PROGRESS.md` the one page to start from, at most 60 lines. Read first
- `START.md` the prompt to paste into a new Claude Code session
- `00_analysis.md` what the code does, what is wrong, what is missing. History once the plan exists
- `01_next.md` the plan, items numbered F1, F2, in the order they are worked
- `02_questions.md` every question for the person, numbered, the answer under each
- `03_your_steps.md` what the person does at the machine, numbered, one action per step
- `log.md` one entry per round, newest at the top
- `logs/` run logs the person sends back. Evidence, never edited
- `history/` older log entries, opened for a line, never read first

## How to read it

`PROGRESS.md` is always the latest word on where things stand. Start there, then `01_next.md`.
Read `03_your_steps.md` at the machine that has the host program. Answer `02_questions.md` in any
order.

## Marks used

- `UNKNOWN` could not be found out, and was not guessed
- `LOCAL MACHINE ONLY` needs the host program on the machine
- `F1, F2` an item of the plan in `01_next.md`
- `Q1, Q2` a question in `02_questions.md`
