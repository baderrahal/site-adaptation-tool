"""Round AUDIT-RULES. The status of every rule id and every value of the rules profile and the settings, from evidence in the text.

Writes the tables of steps/investigations/rule-status-audit.md into the folder given and prints the counts the audit quotes.
.venv\\Scripts\\python -I steps\\investigations\\rule-status-scripts\\audit.py <repo root> <output folder>
Read only on the repo. A status is only given where the text shows it, else UNKNOWN.
"""
import collections
import json
import re
import sys
from pathlib import Path

ROOT = Path(sys.argv[1])
OUT = Path(sys.argv[2])
# R63, no project name in a Python file, so the rules profile is the one config/project.json names
PROFILE = json.load(open(ROOT / "config/project.json", encoding="utf-8"))["profile"]
Q = re.compile(r"\bQ\d{3}\b")
RID = re.compile(r"\bR\d{1,3}\b")
BADER, MEASURED, RECOMMENDED, CODE, UNKNOWN = "CHOSEN by Bader", "MEASURED", "RECOMMENDED", "CHOSEN by Code", "UNKNOWN"
ORDER = (BADER, MEASURED, RECOMMENDED, CODE, UNKNOWN)

progress = (ROOT / "steps/PROGRESS.md").read_text(encoding="utf-8")
# Q100 upward were asked by this audit, so they are not evidence of it
OPEN = {q for q in Q.findall(progress.split("## Bader's turn")[1].split("## Next")[0]) if q < "Q100"}
closed_text = (ROOT / "steps/questions-closed.md").read_text(encoding="utf-8")
CLOSED_ROWS = {m.group(1): m.group(0) for m in re.finditer(r"^\| (Q\d{3}) \|[^\n]*", closed_text, re.M)}
CLOSED = set(CLOSED_ROWS)
# An answer that names Bader is a choice of Bader, an answer that does not is closed for another reason, such as Q095 closing Q088 to Q094
CLOSED_BADER = {q for q, row in CLOSED_ROWS.items() if "Bader" in row}

# Each open question by its text, the short lines hold several, so a line is cut at each "Qnnn. "
open_text = (ROOT / "steps/questions-open.md").read_text(encoding="utf-8")
OPEN_BY_RULE = collections.defaultdict(set)
for line in open_text.splitlines():
    for piece in re.split(r"(?=\bQ\d{3}\. )", line):
        m = re.match(r"(Q\d{3})\. ", piece)
        if m and m.group(1) in OPEN:
            for rid in RID.findall(piece):
                OPEN_BY_RULE[rid].add(m.group(1))
BADER_BY_RULE = collections.defaultdict(set)
for q, row in CLOSED_ROWS.items():
    if q in CLOSED_BADER:
        for rid in RID.findall(row):
            BADER_BY_RULE[rid].add(q)

index = (ROOT / "docs/rules/index.md").read_text(encoding="utf-8")
IDS = re.findall(r"^- (R\d+) (.*)$", index, re.M)
RULE_TEXT = {}
for part in sorted((ROOT / "docs/rules").glob("0*.md")):
    for line in part.read_text(encoding="utf-8").splitlines():
        m = re.match(r"^- \*\*(R\d+) ", line)
        if m:
            RULE_TEXT[m.group(1)] = line


def leaves(o, p):
    if isinstance(o, dict):
        for k, v in o.items():
            yield from leaves(v, p + [k])
    elif isinstance(o, list) and any(isinstance(x, (dict, list)) for x in o):
        for i, v in enumerate(o):
            yield from leaves(v, p + [str(i)])
    else:
        yield p, o


NOTE_KEYS = ("note", "why", "source", "question", "matches", "meaning", "listedIn")
NAMEISH = re.compile(r"(\.dwg|\.xlsx|\.rvt|\.json|\.rte|\.csv|\.pdf|\\|/|^RRE_|^ELl@|PSN-|^C:)", re.I)
HOST = re.compile(r"(Minutes|Seconds|TimeLimit|Peak|MiB|minFreeGB|staleHours|helperProcesses|windowProcesses|\.lock|cimTimeout)", re.I)
BOOKKEEPING = ("addin.parity", "addin.sharedParameters", "answerKeys", "prototypeNames", "prototypeSets", "pastJob", "handCheck", "cadReads",
               "tableUnits", "drawings", "schedules", "profile", "inputRoot", "siteBuild.snapshot", "siteBuild.roadFolder", "testHost.order",
               "testHost.model", "pastAll", "majority.check", "project", "units_mm", "addin.workbookRoles")


def is_note(p, v):
    return p[-1] in NOTE_KEYS or p[-1].endswith("Note") or p[-1].endswith("_meaning") or p[-1].endswith("_source") or (isinstance(v, str) and len(v) > 120)


def sentences(text):
    return re.split(r"(?<=[.)])\s+(?=[A-Z])", text)


ROOT_PATHS = set()
NAME_COUNT = collections.Counter()


def short_names(p):
    """The names a rule line gives a key by: its last two keys, or three when the two are a path of its own in a file, such as
    levelProfiles.native2022 under majority.overrideRounding, and its own name when long, never a number such as rampTypes.0.5."""
    names = []
    if len(p) >= 2 and re.search(r"[A-Za-z]", p[-1]) and re.search(r"[A-Za-z]", p[-2]):
        two = ".".join(p[-2:])
        names.append(".".join(p[-3:]) if two in ROOT_PATHS and len(p) > 2 else two)
    if len(p[-1]) >= 9 and re.search(r"[A-Za-z]", p[-1]) and NAME_COUNT[p[-1]] == 1:
        names.append(p[-1])
    return names


def named_in(name, text):
    return re.search(r"(?<![\w.])%s(?![\w])" % re.escape(name), text) is not None


NEAR = 80


def near(s, names, at):
    """A mark counts when it sits within NEAR characters of a name of the value, so a Q about another key of the sentence does not."""
    return any(abs(m.start() - at) <= NEAR for n in names for m in re.finditer(re.escape(n), s))


def marks(texts, names):
    for where, s, own in texts:
        for m in Q.finditer(s):
            if own or near(s, names, m.start()):
                yield where, m.group(0)
        for m in re.finditer("CHOSEN", s):
            if own or near(s, names, m.start()):
                yield where, "CHOSEN mark"


def by_text(texts, names):
    """A status from the text near a name of the value: a closed Q of Bader, else an open Q, else the mark CHOSEN."""
    found = list(marks(texts, names))
    for where, q in found:
        if q in CLOSED_BADER:
            return BADER, q, where
    # A closed answer that does not name Bader still counts when the text gives him beside the Q, as R302 gives (Bader, Q061)
    for where, s, own in texts:
        for m in Q.finditer(s):
            if m.group(0) in CLOSED and "Bader" in s[max(0, m.start() - 40):m.end() + 40] and (own or near(s, names, m.start())):
                return BADER, m.group(0), where
    for where, q in found:
        if q in OPEN:
            return RECOMMENDED, q, where
    for where, q in found:
        if q == "CHOSEN mark":
            return CODE, q, where
    return None


def classify(fname, data):
    rows = []
    flat = {tuple(p): v for p, v in leaves(data, [])}
    for p, v in leaves(data, []):
        path = ".".join(p)
        if is_note(p, v):
            rows.append((path, "note", "note text, no value", "", ""))
            continue
        if "keyRules" in p:
            i = p.index("keyRules")
            if len(p) == i + 3 and p[i + 2] == "rule":
                base = tuple(p[:i + 2])
                share, strong = flat.get(base + ("share",)), flat.get(base + ("strong",))
                keys = sorted({k[i + 3] for k in flat if k[:i + 3] == base + ("byKey",) and len(k) > i + 3})
                if share is None:
                    rows.append((path, "value", UNKNOWN, "", "keyRules entry with no share"))
                else:
                    rows.append((path, "value", MEASURED, "share %s, %s" % (share, "strong" if strong else "weak"), "keys " + " ".join(keys)))
            else:
                rows.append((path, "record", "measurement record of a keyRule", "", ""))
            continue
        if p[0] == "hands":
            if p[-1] == "rule":
                share = flat.get(tuple(p[:-1]) + ("share",))
                strength = "strong" if share >= 0.9 else "weak"
                rows.append((path, "value", MEASURED, "share %s, %s" % (share, strength), "hands.source, the Parsons IMC key, Round 17, R274"))
            else:
                rows.append((path, "record", "measurement record of a hand rule", "", ""))
            continue
        if (isinstance(v, str) and NAMEISH.search(v)) or (isinstance(v, list) and v and all(isinstance(x, str) and NAMEISH.search(x) for x in v)):
            rows.append((path, "input", "input name or location, no design value", "", ""))
            continue
        numbers = isinstance(v, list) and v and all(isinstance(x, (int, float)) for x in v)
        if any(path == b or path.startswith(b + ".") for b in BOOKKEEPING) or (p[0] in ("dataBook", "connect") and isinstance(v, (str, list)) and not numbers):
            rows.append((path, "setting", "setting of a reader or check, no design value", "", ""))
            continue
        if fname == PROFILE and p[0] == "levelProfiles" and p[-1] == "job":
            rows.append((path, "setting", "setting of a reader or check, no design value", "", ""))
            continue
        kind = "host" if HOST.search(path) else "value"
        inpath = [s for s in p if re.fullmatch(r"Q\d{3}", s)]
        if inpath:
            q = inpath[0]
            status = RECOMMENDED if q in OPEN else BADER if q in CLOSED_BADER else UNKNOWN
            rows.append((path, kind, status, q, "the Q ID in the path"))
            continue
        if path == "majority.strongShare":
            rows.append((path, kind, BADER, "standing rule", "CLAUDE.md Majority rule, Bader's standing rule"))
            continue
        if path == "levelProfiles.default":
            rows.append((path, kind, BADER, "Q050", "CLAUDE.md and steps/questions-closed.md"))
            continue
        texts = []
        par = tuple(p[:-1])
        own = par + (p[-1] + "Note",)
        if own in flat:
            texts.append((".".join(own), flat[own], True))
        size = len([x for x in flat if x[:len(par)] == par])
        for k in NOTE_KEYS:
            if par + (k,) in flat and size <= 8 and isinstance(flat[par + (k,)], str):
                # The note of an object of at most 3 leaves speaks of that one value, as zoneFrom.Zone 1 holds from and why
                texts.append((".".join(par + (k,)), flat[par + (k,)], size <= 3))
        names = short_names(p)
        for rid, t in RULE_TEXT.items():
            for s in sentences(t):
                if any(named_in(n, s) for n in names):
                    texts.append((rid, s, False))
        found = by_text(texts, names)
        if found:
            rows.append((path, kind) + found)
            continue
        if v is None or v == [] or v == {}:
            rows.append((path, kind, UNKNOWN, "", "null or empty"))
            continue
        # The nearest source above the value names where it came from, with no Q and no share it stays UNKNOWN
        src = None
        for i in range(len(p) - 1, -1, -1):
            for cand in (tuple(p[:i]) + ("source",), tuple(p[:i]) + (p[i] + "_source",)):
                if cand in flat and isinstance(flat[cand], str):
                    src = "%s: %s" % (".".join(cand), flat[cand][:48])
                    break
            if src:
                break
        why = (src + ", " if src else "") + "no Q, no share, no Bader line and no CHOSEN mark"
        rows.append((path, kind, UNKNOWN, "", why))
    return rows


for f in (PROFILE, "config/project.json"):
    for p, _ in leaves(json.load(open(ROOT / f, encoding="utf-8")), []):
        for depth in range(1, len(p) + 1):
            ROOT_PATHS.add(".".join(p[:depth]))
for path in ROOT_PATHS:
    NAME_COUNT[path.split(".")[-1]] += 1
VALUE_ROWS = {}
counts = {}
for f in (PROFILE, "config/project.json"):
    data = json.load(open(ROOT / f, encoding="utf-8"))
    rows = classify(f, data)
    VALUE_ROWS[f] = rows
    counts[f] = collections.Counter((r[1], r[2]) for r in rows)
    print(f, "leaves", len(rows))
    for k, n in sorted(counts[f].items()):
        print("   ", k[0], k[1], n)

# The status a rule takes from the values: those it names, and those whose evidence names it
KEY_PATHS = collections.defaultdict(list)
for f, rows in VALUE_ROWS.items():
    for row in rows:
        if row[1] in ("value", "host"):
            p = row[0].split(".")
            for depth in range(1, len(p) + 1):
                for n in short_names(p[:depth]):
                    KEY_PATHS[n].append(row)
KEY_RULE_NAMES = {}
rules = json.load(open(ROOT / PROFILE, encoding="utf-8"))
for name, entry in rules["levelProfiles"]["parsons2024Rev6"]["keyRules"].items():
    if isinstance(entry, dict) and "share" in entry:
        KEY_RULE_NAMES[name] = entry
VALUE = re.compile(r"\b\d+(\.\d+)?\s?(mm|m|%|s|minutes|min|GiB|GB|MiB|degrees|deg|kPa)\b|config/project\.json|" + re.escape(PROFILE) + "|keyRules")
rule_rows = []
for rid, title in IDS:
    t = RULE_TEXT.get(rid, "")
    found = collections.defaultdict(set)
    for q in Q.findall(t):
        if q in CLOSED_BADER:
            found[BADER].add(q)
        elif q in OPEN:
            found[RECOMMENDED].add(q)
    for q in OPEN_BY_RULE.get(rid, ()):
        found[RECOMMENDED].add(q)
    for q in BADER_BY_RULE.get(rid, ()):
        found[BADER].add(q)
    if "keyRules" in t:
        for name, entry in KEY_RULE_NAMES.items():
            if (len(name) >= 6 and named_in(name, t)) or re.search(r"keyRules[ .]%s\b" % re.escape(name), t):
                found[MEASURED].add("%s %s" % (name, entry["share"]))
    named = sorted({n for n in KEY_PATHS if named_in(n, t)})
    for n in named:
        for row in KEY_PATHS[n]:
            if row[2] in ORDER:
                found[row[2]].add(row[3] if row[2] != MEASURED else row[0].split(".")[-2] + " " + row[3].split(",")[0].replace("share ", ""))
    for f, rows in VALUE_ROWS.items():
        for row in rows:
            if row[2] in ORDER and rid in RID.findall(row[4]):
                found[row[2]].add(row[3] if row[2] != MEASURED else row[0].split(".")[-2] + " " + row[3].split(",")[0].replace("share ", ""))
    if "CHOSEN" in t:
        found[CODE].add("CHOSEN mark")
    if not found and (VALUE.search(t) or named):
        found[UNKNOWN].add("")
    if not found:
        rule_rows.append((rid, title, "process", "", ""))
        continue
    where = ", ".join(named[:4]) if named else "the rule text"
    status = ", ".join(s for s in ORDER if s in found)
    refs = " ".join(sorted(x for s in ORDER for x in found.get(s, ()) if x and s != MEASURED))
    shares = " ".join(sorted(found.get(MEASURED, ())))[:120]
    rule_rows.append((rid, title, where, status, " ".join(x for x in (refs, shares) if x)))


def cell(text, n):
    text = str(text).replace("|", "/").replace("\n", " ")
    return text if len(text) <= n else text[:n - 3] + "..."


CODE_SEG = re.compile(r"^([A-Z][A-Z0-9]{1,3}(-IM)?|\d+(\.\d+)?|Zone \d+|[a-z]{3}|u1\d)$")


def pattern(path):
    parts = path.split(".")
    return ".".join("*" if i > 0 and CODE_SEG.match(s) and not re.fullmatch(r"Q\d{3}", s) else s for i, s in enumerate(parts))


def value_of(data, path):
    o = data
    for s in path.split("."):
        if isinstance(o, dict) and s in o:
            o = o[s]
        elif isinstance(o, list) and s.isdigit():
            o = o[int(s)]
        else:
            return None
    return o


main = ["| Rule | Title | Where the value lives | Status | Q ID or share |", "|---|---|---|---|---|"]
proc = ["| Rule | Title | Value |", "|---|---|---|"]
for rid, title, where, status, ref in rule_rows:
    if where == "process":
        proc.append("| %s | %s | process, no value |" % (rid, cell(title, 60)))
    else:
        main.append("| %s | %s | %s | %s | %s |" % (rid, cell(title, 50), cell(where, 70), status, cell(ref, 100)))
(OUT / "rules_main.md").write_text("\n".join(main) + "\n", encoding="utf-8")
(OUT / "rules_proc.md").write_text("\n".join(proc) + "\n", encoding="utf-8")

values = []
status_total = collections.Counter()
for f, rows in VALUE_ROWS.items():
    data = json.load(open(ROOT / f, encoding="utf-8"))
    groups = collections.OrderedDict()
    for row in rows:
        path, kind, status, ref, why = row
        if kind not in ("value", "host"):
            continue
        status_total[status] += 1
        key = (pattern(path), kind, status, "" if status == MEASURED else ref, "" if status == MEASURED else why)
        groups.setdefault(key, []).append(row)
    values += ["### Values of %s" % f, "", "| Value | n | Kind | Status | Q ID or share | Evidence |", "|---|---|---|---|---|---|"]
    for (pat, kind, status, ref, why), members in groups.items():
        shown = pat if len(members) > 1 else "%s = %s" % (members[0][0], cell(json.dumps(value_of(data, members[0][0]), ensure_ascii=False), 40))
        if status == MEASURED:
            ref, why = members[0][3], members[0][4]
        values.append("| %s | %d | %s | %s | %s | %s |" % (cell(shown, 110), len(members), kind, status, cell(ref, 40), cell(why, 58)))
    values += ["", "Every leaf of %s by kind and status (RAN, audit.py): %s." % (f, ", ".join("%s %s %d" % (k, s, n) for (k, s), n in sorted(counts[f].items()))), ""]
(OUT / "values.md").write_text("\n".join(values) + "\n", encoding="utf-8")

rule_status = collections.Counter()
for row in rule_rows:
    if row[2] != "process":
        for s in row[3].split(", "):
            rule_status[s] += 1
print("rules", len(rule_rows), "process", sum(1 for r in rule_rows if r[2] == "process"), "with a value", sum(1 for r in rule_rows if r[2] != "process"))
print("rules by status, a rule may hold several", dict(rule_status))
print("values by status", dict(status_total), "total", sum(status_total.values()))
unknown = collections.Counter(r[4].split(":")[0] if ":" in r[4] else r[4] for rows in VALUE_ROWS.values() for r in rows if r[2] == UNKNOWN)
print("UNKNOWN by evidence", dict(unknown))
named_q = collections.Counter(r[3] for rows in VALUE_ROWS.values() for r in rows if r[2] in (BADER, RECOMMENDED, CODE))
print("Bader, RECOMMENDED and Code by reference", dict(named_q))
print("open", len(OPEN), "closed", len(CLOSED), "closed naming Bader", len(CLOSED_BADER))
