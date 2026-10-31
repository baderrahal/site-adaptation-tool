## 16. The design basis, Manual Rev 06 or the past job

Round 4i, Lane A. The audit of 30 Oct 2026 found the build runs the past job, not the manual: El @ Entry is given by a slope fixed first and holds on 221 of the 747 schedule plots within 5 mm (steps/investigations/data-book.md, R170), the road level comes from six surfaces in their order, the yard is a flat floor 0.100 m under the FFL (Q015) and a plot with no footprint block has no cluster. Round 4i makes the plan follow the manual on a new project and keeps the past job behind one switch, so nothing that works today changes. Rules R407 to R412.

### The switch (R407)

designBasis.mode of config/project.json, rev6 or roshnPastJob. The ROSHN Makkah config keeps roshnPastJob, since its answer keys are the past job, and newProjectMode rev6 is the mode the prefill of a new project gives (J16 of Lane B). A question asks Bader which mode the ROSHN config runs.

| Part | rev6 | roshnPastJob |
|---|---|---|
| El @ Entry | the input: the civil file, or the road at front plus the slope of the unit (R408) | road at front plus offsetM plus the slope of keyRules over the ramp depth |
| Ramp slope | the result, its type by band, listed outside 0.5% to 2% | fixed first by keyRules.rampSlope |
| Road level | designBasis.roadLevel.surface alone (R409) | siteBuild.roadSurfaces in their order |
| Back yard | the grade of 2.3.B and 2.3.C, no floor (R410, Q225) | the flat floor of keyRules.yard |
| Clusters and mirror | proposed with reasons, confirmed in 3 Review data (R411) | footprint clusters and the hand rules of keyRules |

### Door level (R408)

Bader's decision C: the user picks per project. civilFile is a CSV of the civil design, one row per plot, plotColumn the official plot number and levelColumn the door level in metres. roadPlusSlope is for a project with no such file. Either way the step levels gives doorSource, El @ Entry and slopePercent, and the step floors the ramp type of that slope by rampTypes.bands of config/floor_outlines.json.

### Road level (R409)

One surface. The 0.21 m the past 1.4 job ran over today's Top Surface-02 is held as roadLevel.oldJobOffsetM, MEASURED in the status block, and never added to a level.

### Back yard (R410)

Bader's decision B. Manual 2.3.B leaves the back yard to the owner with a grade to the front of 0.3% to 0.5%, 2.3.C gives the townhouse back yard 2% to the trench of the back boundary. The step floors gives yardGrade, the slope and its direction, and no yard floor. The manual gives no level for a yard floor, so backYard.floor true under rev6 is refused until Q225 is answered.

### Clusters and mirror (R411)

Bader's decision B. The step hands gives per plot proposedCluster, proposedMirror and proposalReason, from the attachment of the unit in typologies of the rules profile, its side neighbours and the combinations of clusters. The mirror rule is that of 2.1.B and 2.1.C, the two halves opposite hands. A detached unit has no Rev 6 hand (Q226), and a unit with no single partner gets no proposal (Q229). 3 Review data shows them for the team to confirm (Lane B, steps/waiting-on-others.md).

### New project check (R412)

A read only check of the inputs of a new project, the report in plain words. It covers the position file, the parameter sheet, the villa offsets and hands, and the sheet names and headers of the Excel and PDF inputs, and marks each input that fits only the ROSHN Makkah job today.

### Parity

The new fields join the steps they belong to, so the parity harness keeps 13 steps: doorSource exact, El @ Entry and slopePercent by the levels tolerance in levels, yardGrade exact in floors, proposedCluster, proposedMirror and proposalReason exact in hands.
