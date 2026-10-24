# Round DATA-2, your steps

Round DATA-2 needs no hand export, the full Civil 3D read ran on all three drawings. These are the steps only you can do.

Open in VS Code: C:\Users\bader\Documents\GitHub\site-adaptation-tool

## 1. Pull main

In the VS Code terminal (PowerShell):

```
git checkout main
git pull
```

## 2. Remove the empty 15a folder (Bader's turn 24)

The folder C:\Users\bader\Documents\GitHub\site-adaptation-tool-15a is empty but another program holds it open.

1. Close every VS Code window and terminal that has that folder open.
2. In the VS Code terminal:

```
Remove-Item -Recurse -Force C:\Users\bader\Documents\GitHub\site-adaptation-tool-15a
Test-Path C:\Users\bader\Documents\GitHub\site-adaptation-tool-15a
```

The second line prints False when it is gone. If Remove-Item says the folder is in use, restart Windows and run the two lines again.

## 3. Look at the data book (optional)

Open C:\Users\bader\Documents\SiteAdaptation\data_book\retest.md for the counts of this round, and units.csv and retest_entry.csv for the rows.
