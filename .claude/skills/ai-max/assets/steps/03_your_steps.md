# 03 your steps

[One line: what this pass proves, and which build it needs.]

One action per step. Do them in order. At most 30 steps, ranked by what breaks the tool if it is
wrong. What did not fit is listed at the end.

## Get the code

1. Open GitHub Desktop
2. Pick the repo [repo name]
3. Check the current branch is main
4. Press Fetch origin
5. Press Pull origin if it appears
6. Open VS Code, File, Open Folder, and pick the repo folder

## Build and install

7. Terminal menu, New Terminal
8. Build:

```
[the exact build command]
```

9. Look for: [the exact words, such as Build succeeded with 0 errors]
10. Close [host program], then install:

```
[the exact install command]
```

11. Look for: [the install's last line, starting DONE]

## Run it

12. Open [host program] and [the file to open]
13. [one action]
14. Look for: [the exact words or count that means it passed]

## Send back

15. Copy the run's log from [where the tool writes it] into steps/logs in the repo folder
16. In GitHub Desktop, commit it to a branch and push, or paste the log into the chat
17. If a window looked wrong, take a screenshot of the whole window and send it too

## Not covered by this pass

- [what did not fit in 30 steps, so nothing is lost]
