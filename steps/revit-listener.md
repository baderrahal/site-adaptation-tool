# Revit listener (R448)

For developers. Never in the user install.

## What it is

Each Revit start and model open is waiting time. The listener keeps one Revit open and runs jobs in it. A lane drops a job file in a folder, the open Revit runs it through Buttons.Run, writes the result to another folder and waits for the next job. It uses a job folder and no network port, so Parsons IT has nothing to approve.

- Lives in the test host, dotnet/SiteAdaptation.TestHost, which has its own install. Install-SiteAdaptation.ps1 never holds it.
- Off unless Start-RevitListener.ps1 started that Revit. A Revit started any other way does what it did before.
- Folders: root in testHost.listener of config/project.json, then in-<slot> and out-<slot> under it. The slot comes from the window lock.
- One job at a time, first in first out. A job that fails writes its error and the next job runs.
- SITEADAPT_JOB stands in Revit while a job runs, as it does from the start of an own run, so button 2 opens no window What the tool read. First Revit run, SPL1: without it that window stayed open with nobody to answer.
- Each job gets a new run folder under the work root, as a run of its own does. Real inputs are only read, the result holds their SHA-256 before and after.
- A job waiting in the queue still runs however long it waits. Its age is checked at its arrival, and queueSeconds counts from the send.
- Before a job starts the listener looks at free memory against siteBuild.minFreeGB. Under it the job gets a refused result and the queue goes on.

## Status

Nothing in this file has run in Revit yet. The build and the tests ran with no Revit. Every step below that opens Revit is marked NOT RUN until the first run is written here.

## Run order in VS Code

One terminal per job here. Each step is one action.

1. Close every Revit. Look for: no Revit.exe in Task Manager. Not needed again until the next install.
2. Terminal 1, build: `dotnet build dotnet\SiteAdaptation.sln -c Release`. Look for: Build succeeded.
3. Terminal 1, install the add-in and the test host from that one build, Revit loads one Common for all of them: `pwsh dotnet\SiteAdaptation.AddIn\Install-SiteAdaptation.ps1 -NoBuild` and then `pwsh dotnet\SiteAdaptation.TestHost\Install-TestHost.ps1 -NoBuild -SkipSigningReport`. Look for: the manifests listed as written or kept and no stale Common named. NOT RUN with the listener build.
4. Terminal 1, start, it stays running: `.venv\Scripts\python python\window_lock.py --round <round> --label listener -- pwsh dotnet\SiteAdaptation.TestHost\Start-RevitListener.ps1`. Look for: `Listener ready on slot <n>`. NOT RUN.
5. Leave terminal 1 open. It holds the lock of the slot as long as Revit runs and ends when Revit ends.
6. Terminal 2, send: `pwsh dotnet\SiteAdaptation.TestHost\Send-RevitJob.ps1 -Slot <n> -RoundSpec <spec.json>`. The spec is the -RoundSpec of a Run-TestHost.ps1 buttons run. Look for: `Result of <run id>: ok`. NOT RUN.
7. Terminal 2, send the next job with the same line. Look for: a run time with no Revit start in it, and `Real inputs: <count>, changed 0`. NOT RUN.
8. Close Revit by hand when the lane is done. Look for: terminal 1 prints `Revit <pid> exited` and ends, the slot is free. NOT RUN.

After a new build go back to step 1. Revit holds the DLLs open, so a running listener keeps the code it started with.

## What is in the folders

- in-<slot>: job files waiting, `<run id>.job.json`. in-<slot>\claimed holds each job taken.
- out-<slot>: `<run id>.result.json`, `<run id>.log.json`, the run_timing file when the run wrote one, `heartbeat.json`, `ready.json` with the process and its journal, `session.json`, `listener_log.json` and `listener_home.rvt`, the small project that stays active while a model closes.

## If a job fails

- Exit code 1. The Error line names the failed step. Read `<run id>.log.json` for all of it. The listener goes on, fix the cause and send again. A send makes a new run folder and a new run id.
- Result refused: the job never ran, the error says which check refused it.
- Result timeout: the job ran past its limit. Its result goes out at once with marksFinal false, and the send says the marks are not final. If Revit is still inside the call the next job waits until Revit is back. Then the marks are taken again, the same result file is written over with marksFinal true, and a real input that changed is named in its error (D19). Read the result file again then. Terminal 1 stays as it is.
- Exit code 5, result findings: the run reached its end but a named command stopped, villas were not read, notHeld is above 0 or a save after a button failed. The send prints the counts, buttons_result.json of the run folder holds which. The outputs are there, the listener goes on.
- Result refused for free memory: close what holds the memory and send again.
- Exit code 2: no result in the time of -TimeoutMinutes. The job may still run or wait, its result still comes to out-<slot>.

## If Revit freezes

- The send script watches the heartbeat and whether Revit answers, with the limits of a run of its own. The heartbeat has a thread of its own, so hashing large inputs does not stop it. Past the limits it logs the windows and the tail of the journal that Revit named as its own into `<run id>.log.json`, ends that Revit only and exits with code 4. With no journal named the log has no tail. NOT RUN.
- Exit code 3: Revit is gone and left no result.
- Either way terminal 1 ends and the slot is free. Go back to step 4 and send again. A job that was waiting behind it is refused at the next start, its token belonged to the Revit that ended, and its own send exits with code 3.

## Proven in Revit first

None of these has run. Each is a fact the tests cannot reach.

1. The listener wakes, ready.json is written, the watcher answers the security boxes.
2. Idling keeps running between jobs with no user at the window, with a 100 ms sleep per tick, and Revit stays usable.
3. A job with a new project from the template runs buttons to a result ok, the worker wait of 2 and 4 included.
4. The model closes behind listener_home.rvt, Revit stays open, the pane does not stop on the small project.
5. A second job runs clean in the same Revit, the small project closes after its model opens, and a third job after it.
6. A job that throws gives failed and the next job runs. A model already there, a key copy, runs as well.
7. A job past its limit gives timeout, the add-in is put back and the next job runs.
8. A killed Revit gives exit code 3, a hung one exit code 4, with the tail of its own journal.
9. RecordingJournalFilename gives the full path at OnStartup, and GlobalMemoryStatusEx reads inside Revit.
10. A job past its limit that Revit comes back from: the result is written over with marksFinal true and the next job runs.
11. A real buttons_result.json with notHeld above 0 gives result findings and exit code 5.
