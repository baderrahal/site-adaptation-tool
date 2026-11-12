# Questions moved on 12 Nov 2026

Cut from steps/questions-open.md by the audit of 12 Nov, whole lines, no byte changed. Each has its row in steps/questions-closed.md.

Q336. Link parameter group is Text not Data (Lane D D2, R460, R464). The read KC2 of the IMC key copy in Revit gives all 10 shared link parameters the group Text (autodesk.parameter.group:text-1.0.0) and the spec String, 338 links, 337 holding a value (RAN, round_addin\reads\KC2\run4\testhost_KC2_parameters.json). The sheet held Data, CHOSEN in FIX-1NOV-D. D2 wrote Text into config/shared_parameters.xlsx for the 10 rows, so button 1 binds them under Text as the past job does, and R464 moves a parameter a model holds under Data to Text. Plot is a project parameter of the key with no GUID, the read gives it no group, it stays Data.
- Recommended: Text, as the past links hold it.
- Other: Data, the group of FIX-1NOV-D, by editing the Group column back.

Q441. Order of the raw input changes (Lane C TRUTH-4NOV-C). The tool stops with no past output, steps/truth-audit.md section 5.
- Recommended: Truth first, short list 7, 5, 11, then 1, 2, then 3, 4, 6.
- Other: 1 and 2 first.

Q454. Report name section on the demo config (R542). Hidden, C:\SA-demo\config has no fileName block, so not seen in Revit.
- Recommended: refresh that config, then one look.

## Closed by Bader's answers of 12 Nov 03:25

Q600. The road toposolid of a neighbourhood whose road levels come from two road surfaces (FAST-8, steps/seam.md lines 78 and 82, 7 Nov). On 01A every plot gets its floor level, the front corners took their road levels from two picked road surfaces, and the toposolid is made from one surface, so the road edge of the ramps lies in a hole of it.
- Recommended: a task joins the road surfaces the front corners took into the one toposolid. R485 stays, no ground or grading surface stands in for a road.
- Other: no toposolid for such a neighbourhood.
