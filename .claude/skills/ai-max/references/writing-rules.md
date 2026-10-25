# Writing rules

These apply to everything: code, comments, commit messages, pull request text, README files, documentation, text inside the tool itself, the steps folder, and replies to the person.

## Why

Work that reads as machine written gets dismissed before it gets judged on whether it is any good. The point is not to hide how the work was done. The point is that the writing should not be the thing people notice.

If someone asks directly whether AI was involved, the honest answer is yes. Stripping the tells is about quality of writing, not about denying anything.

## The loudest tells, in order

### 1. Automatic lines on commits and pull requests

The single most obvious marker. No generated-by line and no co-authored-by line naming an AI, in commit messages, pull request bodies or merge commits.

Three places they come from, and each is closed differently:

- the session adds them. Turn off `attribution` in `.claude/settings.json`, see `assets/settings.json`
- GitHub adds them when it builds a squash merge message on its own server, where no hook runs. Pass the title and the message on every squash merge
- an old habit puts them back. Read each commit back off main after the merge

Write a commit message to a file and pass it with `git commit -F`, and a pull request body with `--body-file`. The command line stays short and the hooks can read the file.

### 2. Emoji

None. Not in commit messages, not in README headers, not in code comments, not in output the person sees.

### 3. Comments that restate the code

A comment says why, not what. If the line under it already says the same thing, delete the comment.

Bad:
```
# increment the counter
counter += 1
```

Fine:
```
# Revit returns duplicates for hosted elements, so count unique ids only
counter += 1
```

A confident wrong comment is worse than none. One past bug hid for weeks behind a comment claiming the code handled exactly the case it did not, and nobody looked there.

### 4. A docstring on everything

Long explanations attached to two line functions that need none. Document what is genuinely unclear. Leave the obvious alone.

### 5. Certain words

Avoid: comprehensive, robust, seamless, leverage, crucial, delve, ensure, utilize, facilitate, streamline, holistic, cutting-edge, best-in-class, elevate, unlock, empower, journey.

Most have a plain replacement. Use, make sure, help and improve do the same work without the signal.

A word on the list is fine where it is the field's own term for something, such as `unlock` in a game's rules. A hook that scans for these words keeps a short list of such exceptions next to it. Landscape is not on the list at all, because it is a discipline.

### 6. IT words

The person reads these as jargon. Use the plain word.

| Instead of | Write |
|---|---|
| pipeline | workflow, chain, the steps |
| backend | server side |
| deploy | install, publish, release |
| stack | the tools, the set of programs |
| schema | structure, format, shape |

Field terms are welcome and wanted where they are the accurate word. Federated model, clash, workset, CDE, transmittal, LOD, LOIN, MIDP, TIDP and ISO 19650 all mean something precise, and replacing them with something vaguer makes the writing worse.

### 7. Uniform structure

Every function the same length. Every section the same three bullets. Every commit message in exactly the same shape. Real writing varies because real problems vary.

### 8. Punctuation

No em dashes. No semicolons in prose. Use a full stop, a comma, or two sentences. The person has asked for this directly, it makes client facing text read as machine written, and it costs nothing.

## Text inside a tool

- titles, names and descriptions inside files are short. A check's name is a few words, not a paragraph
- a tick box label is at most eight words, with at most one grey line of twelve words under it
- no capitals for emphasis
- no code name and no framework message in a label. A failure dialog may carry the exception text
- a number in a help line or a page is measured, never estimated

## Replies to the person

- short, simple, direct words. One point per line. A long reply packing several points at once is hard to follow
- steps are numbered, one action each, with the exact command in its own code block and a `Look for:` line
- steps for a machine follow the VS Code run order: which folder to open, opening the terminal, then the commands in order
- every piece of text the person will move somewhere gets its own labelled code block, including drafted content such as slide copy
- a prompt for Claude Code goes in the chat as one full code block, complete every time, never a file
- a report on work in progress ends with Done, Remains, Known bugs, Next and Your turn
- questions come once, together, numbered, with the recommended answer first and a short answer format

## Also avoid

**Padding openers.** Cutting straight to the point is stronger than warming up to it.

**Closing summaries that repeat what was just said.** If it was said once clearly, saying it again adds nothing.

**Hedging on everything.** Some things are certain. Say them plainly. Save the hedge for what is actually uncertain, where it carries real information.

**A try and catch wrapped round everything** with a generic message. Handle the failures that can actually happen, in a way that says what went wrong. A caught value that falls through into a success message, such as a -1 reported as a count, is the worst version.

**Generic names.** `data`, `result`, `temp`, `process()`, `handleData()`. Name the thing for what it holds in this tool.

## Quick check before sending anything

- any generated-by or co-authored-by line
- any emoji
- any comment restating the line below it
- any word from the list, or an IT word from the table
- any em dash, or any semicolon in prose
- every pasteable piece in its own labelled code block
- does every paragraph carry something the one before it did not
