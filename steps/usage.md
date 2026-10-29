# Usage

Token use of Claude Code on this machine, measured by ccusage, counts only. Cost is the API price ccusage puts on the tokens, not a bill.

## 25 to 27 October 2026

RAN on 2026-10-27 in Round REC: npx ccusage@latest daily --since 20261025 --json --breakdown and npx ccusage@latest session --since 20261025 --json. 27 October holds this session up to the time of the run.

### By day

| Day | Model | Input | Output | Cache write | Cache read | Total | Cost USD |
|---|---|---:|---:|---:|---:|---:|---:|
| 2026-10-25 | opus-5-5 | 9,248 | 5,569,645 | 22,508,429 | 1,399,041,132 | 1,427,128,454 | 515.54 |
| 2026-10-25 | sonnet-5-5 | 1,340 | 569,181 | 1,845,029 | 74,881,925 | 77,297,475 | 17.80 |
| 2026-10-25 | all | 10,588 | 6,138,826 | 24,353,458 | 1,473,923,057 | 1,504,425,929 | 533.34 |
| 2026-10-26 | opus-5-5 | 8,336 | 4,819,395 | 21,634,907 | 1,427,253,790 | 1,453,716,428 | 508.17 |
| 2026-10-26 | sonnet-5-5 | 916 | 346,439 | 1,259,322 | 56,740,935 | 58,347,612 | 12.29 |
| 2026-10-26 | all | 9,252 | 5,165,834 | 22,894,229 | 1,483,994,725 | 1,512,064,040 | 520.46 |
| 2026-10-27 | opus-5-5 | 4,100 | 2,452,884 | 15,852,227 | 710,171,288 | 728,480,499 | 271.69 |
| 2026-10-27 | sonnet-5-5 | 1,662 | 698,210 | 2,188,348 | 98,045,625 | 100,933,845 | 22.26 |
| 2026-10-27 | all | 5,762 | 3,151,094 | 18,040,575 | 808,216,913 | 829,414,344 | 293.95 |
| Total | all | 25,602 | 14,455,754 | 65,288,262 | 3,766,134,695 | 3,845,904,313 | 1,347.75 |

### By session

| Session | Last activity | Model | Input | Output | Cache write | Cache read | Total | Cost USD |
|---|---|---|---:|---:|---:|---:|---:|---:|
| 2cb85595, 0a to 4e from 25 Oct | 2026-10-27 07:42 | opus-5-5 | 20,880 | 12,350,775 | 59,006,996 | 3,355,881,124 | 3,427,259,775 | 1,241.55 |
| 2cb85595, 0a to 4e from 25 Oct | 2026-10-27 07:42 | sonnet-5-5 | 3,918 | 1,613,830 | 5,292,699 | 229,668,485 | 236,578,932 | 52.34 |
| this session, REC | 2026-10-27 08:12 | opus-5-5 | 28 | 15,614 | 82,090 | 1,230,302 | 1,328,034 | 1.22 |
| Total | | all | 24,826 | 13,980,219 | 64,381,785 | 3,586,779,911 | 3,665,166,741 | 1,295.11 |

### What the counts show

- Cache read is 97.9 % of every token of the three days (RAN, 3,766,134,695 of 3,845,904,313). Each turn of a session reads its whole context again, so a long session and a large start read cost on every turn.
- One session, 2cb85595, ran 0a to 4e from 25 October and holds 95.3 % of the tokens of the three days (RAN, 3,663,838,707 of 3,845,904,313).
- The session table sums 180,737,572 fewer tokens than the day table. ccusage picks sessions by their last activity and days by each message, the sessions behind the gap are UNKNOWN.

## By phase session

One row per phase session since Round ADDIN 4f, added by step 7 of steps/START.md, total tokens and cost only.

| Session | Phase | Total tokens | Cost USD |
|---|---|---:|---:|
| a86761c9 | 4f, PR 61 to 66, the row taken before the merge of PR 66 | 187,845,111 | 77.69 |
| 8429ce98 | AUDIT-RULES, PR 67 and 68, the row taken before the merge of PR 68, its sonnet and haiku tokens are the helpers' | 42,709,465 | 17.07 |
| dfb83c1c | 4g1, PR 69 to 73, the row taken before the merge of PR 73. npx ccusage was refused by the auto mode of the session, so the tokens are the sum of the usage fields of the session transcript and its 15 helper transcripts by message id, opus 181,149,748 and sonnet 3,461,728, and the cost is UNKNOWN | 184,611,476 | UNKNOWN |
| 231f3894 | 4g2, PR 74 to 81 and the end records, the row taken before the merge of the end records | 410,432,276 | 222.37 |
| 491a6a45 | 4g3 on the laptop, PR 95, 96 and the end records, the row taken before the merge of the end records. The helpers ran on opus and their transcripts may not be in the row, so it is a lower bound | 89,761,189 | 43.47 |
