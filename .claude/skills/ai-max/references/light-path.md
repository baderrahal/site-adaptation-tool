# The light path

For a tool that is one file and needs no repo: one HTML page that opens in Chrome, one script, one workbook. Most of the ten phases do not apply, and running them anyway costs the person more than the tool is worth.

## When it fits

- one person runs it, or a few who will open a file and nothing more
- it reads files the person picks and writes a result they download
- nothing needs a build, an install or the host program's API

If any of that stops being true while building, say so in one line and move to the full path at phase 2.

## The steps

1. **Decide.** What it does in one sentence, and the done check with known numbers. For a reader of exported data that is usually: the totals match a test file whose counts are known, and five real records match the source system
2. **Read every input whole.** Every file the person gave, every sheet. A workbook split across several files because of Excel's row limit is read across all of them as one table
3. **Ask once.** One form, recommended answers first: scope, who opens it, whether it keeps the last result, what the report looks like
4. **Show it before building** if it has a window, using the house look in phase 1 of `phases.md`
5. **Build it here, then run it here.** There is no repo, so Claude in chat builds the file in its own workspace and checks it there. Open the page in a browser, load a test file with known counts, and compare. Report what was RAN and what stays UNKNOWN, such as the person's own files
6. **Deliver the file** in the chat with how to open it, the done check for the person to run, and their turn

## Rules for a one page HTML tool

- one self contained file. Opens in Chrome from the disk and works with no network
- no CDN if it can be avoided. If one is needed, cdn.jsdelivr.net, never unpkg.com, which the proxy blocks
- no .wasm fetched from a CDN, the proxy blocks it. Inline it or avoid it
- keeping the last result between visits uses the browser's own storage, wrapped so a blocked storage never stops the page
- an Excel file it writes avoids IFS and every other function stored with the `_xlfn.` prefix
- the writing rules apply to every label and every line of the page

## When it outgrows the light path

The page grows past a single afternoon of changes, a second person starts changing it, or it needs a check that should run on every change. Then it gets a repo, and the full path starts at phase 3 with the file as the first commit.
