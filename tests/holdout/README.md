# Hold-out tests

The hold-out answers are not read by any lane and are scored only by GitHub checks.

The answers are not in this folder. The repo is public and real levels never go in git, so they live in the Actions secret HOLDOUT_ANSWERS, which Bader sets, in the shape scripts/checks/holdout.py names. A run writes its predictions to tests/holdout/predictions/ and the check prints counts only, never a value. Owned by the lead, a hook blocks every other lane from editing here.

Empty today, the check says NOT RUN. Which zones to hold back and how predictions reach GitHub with no real value in git is open: steps/backlog.md, truth audit item 6.
