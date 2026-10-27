# Usage

What drives the cost of a build, how to measure it, and how to shrink records without losing a byte. The rules themselves are in the Usage section of `SKILL.md`. This file holds the evidence and the methods behind them.

## What was measured

The Site Adaptation Tool, 25 to 27 Oct 2026, by ccusage on the author's machine:

| | Tokens | Share |
|---|---:|---:|
| All tokens, three days | 3,845,904,313 | 100 % |
| Cache read | 3,766,134,695 | 97.9 % |
| Output | 14,455,754 | 0.4 % |
| One session, 15 pull requests over about 56 hours | 3,663,838,707 | 95.3 % |

About 1,348 USD at API prices for the three days, about 1,294 of it in that one session.

At the same time the files that session read had grown:

| File | 3 days earlier | At the end |
|---|---:|---:|
| the round plan and findings | 95 KB | 470 KB |
| the rules file | 21 KB | 405 KB, touched by 218 commits |
| the design | | 150 KB, read whole by every helper on every spawn |
| `CLAUDE.md` | | 29 KB, 188 lines, so under the line cap |
| `PROGRESS.md` | | 21 KB, 60 lines, one line 3,278 characters |

Review passes grew too. The last two phases took 4 and 7 passes, 4h57 and 6h12, against 1 to 2.5 hours for the phases before. Most findings of the late passes were inputs that no real file reaches.

The NWC Federator showed the same shape earlier: most of its usage ran with over 150k of context, from sessions left open for eight hours.

## Why cache read dominates

Every turn of a session sends its whole context again. A cached read is cheaper per token than a fresh one, but it is paid on every turn. So the cost of a session grows with how long it runs times how much it holds. A 470 KB file read once early in a session is paid again on every later turn until the session ends or compacts, and after a compaction it tends to be read again.

That gives the three levers, in order of size:

1. **Session length.** A fresh session per phase resets the context
2. **What sits in the context.** Small start reads, reads by id, agents that start light
3. **How many turns.** Review passes capped, findings no real input reaches left alone

## Measuring

After each phase merges, the session adds one row to `steps/usage.md`:

```
npx ccusage@latest session --since <yyyymmdd>
```

Take this session's row: total tokens and cost. Where npx is missing or the network refuses it, write UNKNOWN and say so in the log. Never work around a refusal.

For a look across days:

```
npx ccusage@latest daily --since <yyyymmdd>
```

The session table and the day table can differ, because sessions are grouped by their last activity and days by each message. Name the gap as UNKNOWN rather than closing it by guess.

## Shrinking records without losing data

A file past its cap is split, never reworded. The steps, in one pull request per file or two:

1. Record the SHA-256 of the file and a count of every rule id and question id in the repo
2. Split by its own headings or id ranges into parts, byte for byte. Closed history goes to `steps/history/`, live parts stay in `docs/` or `steps/`
3. Write the index by a script: for rules, one line per id with its title only. For the design, a README naming each part
4. Prove it: the parts joined in order hash to the original. The id counts are equal before and after. Write both into `steps/runs/<round>/split_proof.txt`
5. Find every file that reads the old path, by grep, and point it to the new one. Notes and comments that name the old path are pointed too, so nobody follows a dead path
6. Run the gate. The counts are the counts before, plus the new tests

Shortening a rule's text is a separate change, made by the person's decision, never as part of a split.

## The chat side

Chat pays for what it reads too. Through the GitHub connector, chat reads `PROGRESS.md` and the top log entry, then single files by path. A file over 30 KB is read in parts or searched. Where the session's git access allows a read only clone into chat's workspace, sizes and counts come from commands there, which is quicker and keeps the big files out of the reply.
