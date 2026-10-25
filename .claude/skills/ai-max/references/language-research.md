# Choosing the language

## Why this is researched, not recalled

The host program decides most of the answer, and host programs change. An API that was current when the model was trained may be deprecated, replaced or newly supported in the version the person runs. Recalling an answer here produces a tool built on something that no longer exists.

So this phase searches, every time, and then measures what the search cannot settle.

## How each kind of claim is settled

- a claim about our own code gets **run**
- a claim about someone else's product gets **searched**, because their source cannot be run. The vendor's documentation is evidence. A forum post is a lead to test
- a claim about what the installed program actually holds gets **measured** off that program
- a threshold gets **chosen** and declared, with the reason

Mixing these up is how a guess becomes a rule. One past fix was right only because the vendor's page was read: running our own code could never have found it, because our code did not yet produce the shape that page refused.

## What to establish first

Ask, or find, before searching:

1. **Which program does it run inside?** Revit, Navisworks, Civil 3D, AutoCAD, Rhino, Excel, a browser, or nothing at all
2. **Which version?** The one the people who run it have, which is often not the author's. A team may run several years at once
3. **Who runs it?** The author alone, or people who cannot install anything and have no admin rights
4. **How often?** A one-time job and a weekly team job justify very different effort

Question 3 changes the answer more than people expect. A tool for machines you do not control cannot need a compiler, an install with admin rights, or a licence.

## What to search for

Search for the current state, not the concept:

- the program name plus API plus the version, for what is supported now
- the program name plus the year, to catch a recent change of direction
- whether the older route is deprecated
- the target framework and language version that version of the program loads, and whether it changed between the years the team runs
- whether a package supplies the program's reference assemblies, so the add-in compiles on a machine without the program. Revit has the Nice3point packages. Not every host has one, Navisworks does not
- what the program's own documentation currently recommends

Prefer the vendor's documentation and release notes. Community posts are useful for whether something works in practice, and unreliable for what is current. Check at least two sources before settling. A single result that matches the expectation is the easiest way to confirm something wrong.

Record each answer in `CLAUDE.md` under host and target, with the date and the source, so the next session reads it rather than searching again.

## Measure what the plan depends on

Before code calls a member of the host's API, measure that it exists and what it does. List every class, member, property and behaviour the plan leans on, then for each:

1. Say the question in one sentence, and what answer would change the code
2. Measure it. A member's existence and its signature come off the installed DLL by reflection, which needs no host running. A behaviour, such as what a call does to an open document, needs a small probe run inside the host, against a copy of a real file and never the live one
3. Keep the raw output, and add the question, the method, the command, the answer and the date to `docs/history/scan.md`
4. UNKNOWN is an allowed answer. A guess is not

Examples of what measuring caught on past tools: an export options class that exists in the 2026 API and not in 2025, a point count member that did not exist and broke the build, and a boundary call that silently kept only the first item it was given. Each was confident in the documentation someone remembered.

A `try` and `catch` around a member that might not exist is not protection. It guards a run time problem, and a missing member is a compile time problem. Measure it instead.

## What matters in the comparison

Weigh these, roughly in this order:

**Can it reach what the tool needs?** Some things are only exposed through one route. This is decided first, because it can rule everything else out.

**Can the people who run it actually run it?** Something needing a compiled install with admin rights fails at once for a team on locked machines. A per user install that needs no admin, and no compiler on their side, is the bar.

**Can it be tested where it is written?** Code that can only be tested inside the host costs a full round trip for every attempt. That is not a reason to avoid it, but it is the reason for the Core and host split in phase 6, and for writing host calls in larger, more carefully reviewed pieces.

**Can it build away from the host?** If a reference package exists, the add-in compiles in the test gate and in a cloud session. If not, every host side change is unbuilt until a local build, and the plan should say so.

**How much does one change cost?** A script that is edited and rerun in seconds is worth a lot against something that has to be compiled, installed and reloaded.

**Will it still work next version?** A route the vendor is moving away from will need rewriting.

## How to present the decision

- the recommendation, in one line
- why, in one paragraph, tied to the points above
- the two runners up and what each one loses
- anything the choice closes off later
- the host facts still to measure, as a list

Never present one option as the only possibility. There is almost always a second route, and the person knows their own constraints better than any search does.

## When there is no network

Say so plainly and ask rather than guessing: the current state of that API cannot be checked from here, and an answer from memory could be a version or two out of date.

Then offer what can still be done:

- ask which route the person has used before for this program
- lay out the trade-offs above so they can decide with their own knowledge
- measure off the installed program if a session can reach it
- write the choice down as provisional, to be confirmed when research is possible

A provisional choice labelled provisional is fine. A guess presented as a finding is not.
