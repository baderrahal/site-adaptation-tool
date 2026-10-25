# Environment

Facts measured on the team's machines, in Claude Code and through the GitHub connector, August to October 2026. When one seems to have stopped being true, check it again rather than working around it, and record each repo's own machine facts in its `CLAUDE.md`.

## The team's machines

- **No admin rights.** Nothing installs to Program Files and no script runs as administrator. Add-ins go to per user folders, such as `%APPDATA%\Autodesk\ApplicationPlugins\<Name>.bundle`
- **Files from a browser carry a block mark.** An install script runs `Unblock-File` first, because a blocked DLL fails to load with a message that never mentions the block
- **The proxy blocks unpkg.com.** cdn.jsdelivr.net works. Better still, no CDN at all, with everything inside the file
- **The proxy blocks .wasm downloads from every CDN.** Keep the WASM file local, or serve it from memory
- **Python** installs per user through the python.org install manager, `py install 3.12`, and packages go in with `py -V:3.12 -m pip`. Downloads that uv fetches from GitHub fail, because the proxy re-signs TLS
- **nuget.org is reachable.** A net48 project compiles through the Microsoft.NETFramework.ReferenceAssemblies package, with no Visual Studio and no targeting pack
- **git.** A clone over HTTPS can fail with a certificate revocation error. GitHub Desktop pulls and pushes. git on the PATH is not a given, so steps name the app to use
- **No Node.js and no Claude Code CLI** by default. The Claude desktop app's Code tab works on a local folder once that folder is a git repo with at least one commit, after the app is restarted
- **Starting a program costs about two seconds.** That is why hooks use shell builtins and start a program only when a call is already on its way to a refusal
- **Excel lacks IFS** and the other functions stored with the `_xlfn.` prefix, which show as #NAME?. A workbook a tool writes avoids them
- **Host versions differ by person.** Ask which year each person runs, and build for theirs
- **Autodesk cloud routes**, the ACC and Forma APIs, need a registered APS app and a client ID from the hub admin. Until there is one, that route is closed

## Claude Code

- **A cloud session** runs in a Linux container. No Windows, no Autodesk install and no host DLLs. It builds Autodesk free code, and add-in code only where a reference package exists. Everything else it writes against a host API is UNBUILT until a local build
- **A local session**, the desktop app's Code tab on the machine with the host program, can build, install and run against the host. A Windows session has a PowerShell tool beside Bash. Hooks run under Git for Windows' sh, which Claude Code finds on its own
- **What loads when.** `CLAUDE.md`, `.claude/rules`, agent definitions and settings load when a session starts. An edit in the middle of a session bites in the next one
- **The session's proxy refuses** GitHub repository settings and remote branch deletes. Changing the default branch and deleting a branch are done by the person on the GitHub web page
- **A cloud session cannot create a GitHub repo.** The person creates it with a README so main exists before Code's first branch
- **The GitHub web uploader skips dot folders** such as `.claude`. Those reach the repo through a commit from the machine
- **A Bash scope in an agent's tools line fails open.** `Bash(git log:*)` was measured running other commands. An agent that must not write gets no Bash at all. Project permissions in `settings.json` do hold, but apply to the main session too
- **Attribution** of commits and pull requests is turned off with `"attribution": {"commit": "", "pr": "", "sessionUrl": false}` in `.claude/settings.json`. Commits read back clean after it was set. Read them back anyway
- **A squash merge** gets its message built on GitHub's server, where no hook runs, and a co-author line has appeared there. Pass the title and message on every squash merge
- **Scheduled check-ins.** After opening a pull request a session may schedule its own check-in, and each check-in schedules the next. Name it and remove it unless asked
- **Usage limits** stop agents part way. Work after that point was not machine audited

## The GitHub connector in chat

- a private repo returns not found until the Claude GitHub app is given that repo, in GitHub settings, Installed GitHub Apps
- the merge commit with its file stats shows what a round changed
- a file past the connector's inline limit is fetched raw instead
- GitHub Actions results cannot be read from chat
- the pull request list can report merged false on merged pull requests. Read merged_at
- reading is not checking. Chat reports what it READ, and everything that needs a run stays UNKNOWN until Claude Code runs it
