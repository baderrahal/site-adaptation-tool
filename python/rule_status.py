"""R401. The status of every value of the rules profile and the settings from evidence in the records, and the block status of the
rules profile that holds it, one entry per value by file and dotted path.

Moved from steps/investigations/rule-status-scripts/audit.py of Round AUDIT-RULES (PR 68) in Round ADDIN 4g1, with DOCUMENT of Q100
and the ties of Q101. A status is only given where the text shows it, else UNKNOWN. The block is never read as values or as evidence,
so a second write gives the same bytes.

python python/rule_status.py <repo root> [<output folder>]   prints the counts, and writes the audit tables when a folder is named
python python/rule_status.py <repo root> --write              writes the block status of the rules profile
python python/rule_status.py <repo root> --check              lists the entries whose evidence changed since the block was written
"""
import argparse
import collections
import json
import re
import sys
from pathlib import Path

import key_rules_block

STATUS = "status"
BADER, MEASURED, DOCUMENT, RECOMMENDED, CODE, UNKNOWN = ("CHOSEN by Bader", "MEASURED", "DOCUMENT", "RECOMMENDED", "CHOSEN by Code",
                                                          "UNKNOWN")
ORDER = (BADER, MEASURED, DOCUMENT, RECOMMENDED, CODE, UNKNOWN)
SETTINGS_FILE = "config/project.json"
Q = re.compile(r"\bQ\d{3}\b")
RID = re.compile(r"\bR\d{1,3}\b")
NOTE_KEYS = ("note", "why", "source", "question", "matches", "meaning", "listedIn")
NAMEISH = re.compile(r"(\.dwg|\.xlsx|\.rvt|\.json|\.rte|\.csv|\.pdf|\\|/|^RRE_|^ELl@|PSN-|^C:)", re.I)
HOST = re.compile(r"(Minutes|Seconds|TimeLimit|Peak|MiB|minFreeGB|staleHours|helperProcesses|windowProcesses|\.lock|cimTimeout)", re.I)
# valueStatus of R401 maps each plotted value to its entry of the block, it holds no design value of its own, and checkRows of R363 names the check
# type and the form of Past of each row of the check table, which set no plotted value (review 1 of PR 81), and addin.report of R365 names the
# button of the report each override field counts under and the decisions of the walls, which set no plotted value either, and manualRules of R403
# holds the texts, groups and pages of the manual rules the window Site Adaptation Rules shows, which set no plotted value either
BOOKKEEPING = ("addin.parity", "addin.sharedParameters", "answerKeys", "prototypeNames", "prototypeSets", "pastJob", "handCheck", "cadReads",
               "tableUnits", "drawings", "schedules", "profile", "inputRoot", "siteBuild.snapshot", "siteBuild.roadFolder", "testHost.order",
               "testHost.model", "pastAll", "majority.check", "project", "units_mm", "addin.workbookRoles", "valueStatus", "checkRows", "addin.report", "manualRules")
# Settings inside a bookkeeping block that still choose what a build or button 1 takes: the group the reference key of the position
# file and the overrides follow (position_file.key_of, ProjectPositionFile.cs), and the job of a level profile (plan_rules.check_override_jobs)
CHOOSERS = ("majority.check.group",)
# Q101 (Bader, 2026-10-27). The keyRules entry that measures the decision a field of a level profile outside keyRules holds, so the
# field shows the share of that entry. A level profile with no such entry, native2022, keeps its status (CHOSEN, the pairs read from
# the rule of each entry and the code that reads the field). A field is looked up by its two keys first, so
# frontRoad.offsetM, a term of El @ Entry (key_rules_check.py, plan_rules.entry_of), goes to entry and the road surface to rampFront.
# A field whose value, or whose name, is a candidate of its entry takes the share of that candidate. The note says what no code reads:
# a build takes the FFL from keyRules.ffl whenever keyRules holds it (plan_rules.key_levels_of), and the stepBands only under the
# rule stepBands of keyRules.steps (plan_rules.steps_by_rule), the rule named third
TIES = {"frontRoad.surface": ("rampFront", None, None), "frontRoad.offsetM": ("entry", None, None), "rampSlopePercent": ("rampSlope", None, None),
        "rampTypes": ("rampSlope", None, None), "yardBelowFflM": ("yard", None, None),
        "height": ("ffl", "no code reads this field while keyRules holds ffl, a build takes its FFL from keyRules.ffl", None),
        "stepBands": ("steps", "no code reads this field while keyRules.steps codes another rule than stepBands", "stepBands"),
        "steps": ("steps", "no code reads this field, a build takes its steps by the rule keyRules.steps codes", None)}
# Q100 asked these five of the audit itself, so they are no evidence of a value, their answers give DOCUMENT and the ties
AUDIT_QUESTIONS = {"Q100", "Q101", "Q102", "Q103", "Q104"}
NEAR = 80
CAMEL = re.compile(r"(?<![\w.])[a-z]+[A-Z][A-Za-z]*(?![\w])")
# Q100. A place in a client document as the records write it: a page, an appendix, a section or a sheet, held only after the revision
# of that document within NEAR characters
PAGE = re.compile(r"\b(page \d+|Appendix [A-Z]\b|section \d+(?:\.\d+)*(?:\.[A-Z])?|sheet [0-9A-Z][^,.()]*?)(?=\s*[,.()]|\s*$)")
REV = re.compile(r"\bRev ?0?(\d+)\b")
WHY_NONE = "no Q, no share, no Bader line and no CHOSEN mark"
# A question starts its line or a sentence, "since Q089. keyRules" inside the text of Q098 starts none
QUESTION_START = re.compile(r"(?:^|(?<=\. ))(?=Q\d{3}\. )")
# Q104 (Bader, 2026-10-27). Bader accepted the 41 rows of tables 1 and 2 of the review list as they are. Tables 2b and 3 are for
# information only, so a heading of table 1 or 2 alone opens a part the list is read from
REVIEW_FILE = "steps/investigations/status-review.md"
REVIEW_TABLE = re.compile(r"^## ([12]) ")
REVIEW_COLUMN = "File and dotted path"


def leaves(o, p):
    # An empty object is a leaf, as an empty list and null are, so majority.overrideKeep.laterFields {} is a value
    if isinstance(o, dict) and (o or not p):
        for k, v in o.items():
            yield from leaves(v, p + [k])
    elif isinstance(o, list) and any(isinstance(x, (dict, list)) for x in o):
        for i, v in enumerate(o):
            yield from leaves(v, p + [str(i)])
    else:
        yield p, o


def is_note(p, v):
    return p[-1] in NOTE_KEYS or p[-1].endswith("Note") or p[-1].endswith("_meaning") or p[-1].endswith("_source") or (isinstance(v, str) and len(v) > 120)


def sentences(text):
    # Every ". " ends a sentence, a split only before a capital kept "(Bader, Q061). testHost.imageMarginMinutes" in one
    return re.split(r"(?<=[.])\s+", text)


def named_in(name, text):
    # A dot before the name is allowed, a rule often spells the whole path such as addin.position.checkPointLimitMm
    return re.search(r"(?<![\w])%s(?![\w])" % re.escape(name), text) is not None


def value_at(data, dotted):
    """The value an entry of the block names by its dotted path, a key that holds a dot itself, such as rampTypes 0.5, taken whole
    before its parts. Raises KeyError when no value is there."""
    if dotted == "":
        return data
    if isinstance(data, list):
        head, _, rest = dotted.partition(".")
        if head.isdigit() and int(head) < len(data):
            return value_at(data[int(head)], rest)
        raise KeyError(dotted)
    if isinstance(data, dict):
        for key in sorted((k for k in data if dotted == k or dotted.startswith(k + ".")), key=len, reverse=True):
            try:
                return value_at(data[key], dotted[len(key) + 1:])
            except KeyError:
                continue
    raise KeyError(dotted)


def answer_cell(row):
    """The answer of a row | Qnnn | question | answer | date | of the table of the closed questions."""
    cells = [cell.strip() for cell in row.strip().strip("|").split(" | ")]
    return cells[2] if len(cells) >= 4 else ""


def review_list(root):
    """Q104. The values of tables 1 and 2 of the review list by (file, dotted path), each with the table and row it is listed at. The
    list is read only, it stays as Bader reviewed it. A repo with no list gives none."""
    path = Path(root) / REVIEW_FILE
    if not path.is_file():
        return {}
    listed = {}
    table = column = None
    for line in path.read_text(encoding="utf-8").splitlines():
        if line.startswith("## "):
            m = REVIEW_TABLE.match(line)
            table, column = (m.group(1) if m else None), None
            continue
        if table is None or not line.startswith("|") or set(line) <= set("|-: "):
            continue
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if column is None:
            column = cells.index(REVIEW_COLUMN) if REVIEW_COLUMN in cells else None
            continue
        fname, _, dotted = cells[column].partition(" ")
        listed[(fname, dotted)] = "%s table %s row %s" % (REVIEW_FILE, table, cells[0])
    return listed


def load(root, name, profile):
    """A file of the two as values: the block status of the rules profile is left out, it is the output, never a value or evidence."""
    data = json.loads((Path(root) / name).read_text(encoding="utf-8"))
    if name == profile:
        data.pop(STATUS, None)
    return data


class Audit:
    """The rows of every leaf of the two files and of every rule id, as the tables of steps/investigations/rule-status-audit.md give
    them, with the fields of a block entry beside each value row in extra."""

    def __init__(self, root):
        self.root = Path(root)
        settings = json.loads((self.root / SETTINGS_FILE).read_text(encoding="utf-8"))
        # R63, no project name in a Python file, so the rules profile is the one config/project.json names
        self.profile = settings["profile"]
        self.files = (self.profile, SETTINGS_FILE)
        # The Majority rule of CLAUDE.md, the strong share is read from the settings, never written here
        self.strong = settings["majority"]["strongShare"]
        self.data = {f: load(self.root, f, self.profile) for f in self.files}
        self._questions()
        self._rules()
        self.leaf = {}
        self.extra = {}
        self.note_text = []
        for f in self.files:
            for p, v in leaves(self.data[f], []):
                if isinstance(v, str) and is_note(p, v):
                    self.note_text.append((".".join(p), v))
        self.root_paths = set()
        self.name_count = collections.Counter()
        for f in self.files:
            for p, _ in leaves(self.data[f], []):
                for depth in range(1, len(p) + 1):
                    self.root_paths.add(".".join(p[:depth]))
        for path in self.root_paths:
            self.name_count[path.split(".")[-1]] += 1
        readme = self.root / Path(self.profile).parent / "README.md"
        self.page_records = [("README", readme.read_text(encoding="utf-8"))] if readme.is_file() else []
        self.page_records += list(self.rule_text.items()) + self.note_text
        self.review = review_list(self.root)
        self.reviewed = set()
        self.value_rows = {f: self.classify(f, self.data[f]) for f in self.files}
        # A listed path the files do not hold as a value would leave a row of Bader's review with no entry, so nothing is written
        missing = sorted(set(self.review) - self.reviewed)
        if missing:
            raise ValueError("the review list of Q104 in %s names what is no value of the two files: %s"
                             % (REVIEW_FILE, ", ".join("%s %s" % key for key in missing)))
        self._pages()
        self.rule_rows = self._rule_rows()

    def _questions(self):
        open_text = (self.root / "steps/questions-open.md").read_text(encoding="utf-8")
        # D13: a question is open when its full text, "Qnnn. ", stands in questions-open.md, since Bader's turn lists only the questions of
        # the phase. The title line "Qnnn Title." names it without the full stop. Not the five the audit asked of itself
        self.open = {m.group(1) for line in open_text.splitlines() for piece in re.split(QUESTION_START, line)
                     for m in [re.match(r"(Q\d{3})\. ", piece)] if m} - AUDIT_QUESTIONS
        closed_text = (self.root / "steps/questions-closed.md").read_text(encoding="utf-8")
        self.closed_rows = {m.group(1): m.group(0) for m in re.finditer(r"^\| (Q\d{3}) \|[^\n]*", closed_text, re.M)}
        self.closed = set(self.closed_rows) - AUDIT_QUESTIONS
        # An answer that names Bader is a choice of Bader, an answer that does not is closed for another reason, such as Q095 closing Q088 to Q094
        self.closed_bader = {q for q, row in self.closed_rows.items() if "Bader" in row and q not in AUDIT_QUESTIONS}
        self.open_by_rule = collections.defaultdict(set)
        for line in open_text.splitlines():
            for piece in re.split(QUESTION_START, line):
                m = re.match(r"(Q\d{3})\. ", piece)
                if m and m.group(1) in self.open:
                    for rid in RID.findall(piece):
                        self.open_by_rule[rid].add(m.group(1))
        # Each open question by its text, the short lines hold several, so a line is cut where "Qnnn. " starts a sentence
        self.question_pieces = []
        for line in open_text.splitlines():
            for piece in re.split(QUESTION_START, line):
                m = re.match(r"(Q\d{3})\. ", piece)
                if m and m.group(1) in self.open:
                    self.question_pieces.append((m.group(1), piece))
        # A closed question of Bader by the answer cell of its row of the table alone. The question cell and the old text moved under a
        # section below the table name what was asked, not what Bader chose, as the text of Q098 names Duplex_DP4 clusters, not D04
        self.question_pieces += [(q, answer_cell(row)) for q, row in self.closed_rows.items() if q in self.closed_bader]
        self.bader_by_rule = collections.defaultdict(set)
        for q, row in self.closed_rows.items():
            if q in self.closed_bader:
                for rid in RID.findall(row):
                    self.bader_by_rule[rid].add(q)

    def _rules(self):
        index = (self.root / "docs/rules/index.md").read_text(encoding="utf-8")
        self.ids = re.findall(r"^- (R\d+) (.*)$", index, re.M)
        self.rule_text = {}
        for part in sorted((self.root / "docs/rules").glob("0*.md")):
            for line in part.read_text(encoding="utf-8").splitlines():
                m = re.match(r"^- \*\*(R\d+) ", line)
                if m:
                    self.rule_text[m.group(1)] = line

    def short_names(self, p):
        """The names a rule line gives a key by: its last two keys, or three when the two are a path of its own in a file, such as
        levelProfiles.native2022 under majority.overrideRounding, and its own name when long, never a number such as rampTypes.0.5."""
        names = []
        if len(p) >= 2 and re.search(r"[A-Za-z]", p[-1]) and re.search(r"[A-Za-z]", p[-2]):
            two = ".".join(p[-2:])
            names.append(".".join(p[-3:]) if two in self.root_paths and len(p) > 2 else two)
        if len(p[-1]) >= 9 and re.search(r"[A-Za-z]", p[-1]) and self.name_count[p[-1]] == 1:
            names.append(p[-1])
        return names

    def pair_names(self, p, s):
        """The key list form of a rule, workerTimeLimitS, build, reportExcel: the last key counts as a name of the value when its
        parent, a key of 8 letters or more found once in the two files, is in the same sentence."""
        if len(p) >= 2 and len(p[-2]) >= 8 and self.name_count[p[-2]] == 1 and re.fullmatch(r"[A-Za-z]+", p[-1]) and named_in(p[-2], s):
            return [p[-1]] if named_in(p[-1], s) else []
        return []

    def near(self, s, names, at):
        """A mark counts when it sits within NEAR characters of a name of the value, or when the last camelCase key named before it is
        the value's, as a note writes sampleSeconds is how often ... every 5 s (CHOSEN), so a Q about another key of the sentence does not."""
        if any(abs(m.start() - at) <= NEAR for n in names for m in re.finditer(re.escape(n), s)):
            return True
        keys = [m for m in CAMEL.finditer(s[:at]) if m.group(0) in self.name_count]
        return bool(keys) and any(keys[-1].group(0) == n.split(".")[-1] for n in names)

    def marks(self, texts, names):
        for where, s, own, extra in texts:
            names = names + extra
            for m in Q.finditer(s):
                if own or self.near(s, names, m.start()):
                    yield where, m.group(0), s
            for m in re.finditer("CHOSEN", s):
                if own or self.near(s, names, m.start()):
                    yield where, "CHOSEN mark", s

    def by_text(self, texts, names):
        """A status from the text near a name of the value: a closed Q of Bader, else an open Q, else the mark CHOSEN, with the text."""
        found = list(self.marks(texts, names))
        for where, q, s in found:
            if q in self.closed_bader:
                return (BADER, q, where), s
        # A closed answer that does not name Bader still counts when the text gives him beside the Q, as R302 gives (Bader, Q061)
        for where, s, own, extra in texts:
            for m in Q.finditer(s):
                if m.group(0) in self.closed and "Bader" in s[max(0, m.start() - 40):m.end() + 40] and (own or self.near(s, names + extra, m.start())):
                    return (BADER, m.group(0), where), s
        for where, q, s in found:
            if q in self.open:
                return (RECOMMENDED, q, where), s
        for where, q, s in found:
            if q == "CHOSEN mark":
                return (CODE, q, where), s
        return None

    def tie(self, p, flat):
        """Q101. The keyRules entry of the same level profile that measures the field p[2], when the profile holds it with a share."""
        if len(p) < 3 or p[0] != "levelProfiles":
            return None
        found = TIES.get(".".join(p[2:4])) or TIES.get(p[2])
        if found is None:
            return None
        base = tuple(p[:2]) + ("keyRules", found[0])
        share = flat.get(base + ("share",))
        if share is None:
            return None
        value = flat.get(tuple(p))
        candidate = next((name for name in (value, p[2]) if isinstance(name, str) and base + ("candidates", name) in flat), None)
        extra = {}
        if candidate is not None:
            share = flat[base + ("candidates", candidate)]
            extra["candidate"] = candidate
        if found[1] and flat.get(base + ("rule",)) != found[2]:
            extra["note"] = found[1]
        return ".".join(base), share, extra

    def classify(self, fname, data):
        rows = []
        flat = {tuple(p): v for p, v in leaves(data, [])}
        self.leaf.update({(fname, ".".join(p)): v for p, v in flat.items()})

        def measured(path, kind, share, strength, by, evidence, tied=None):
            rows.append((path, kind, MEASURED, "share %s, %s" % (share, strength), evidence))
            self.extra[(fname, path)] = dict({"share": share, "strength": strength, "measuredBy": by},
                                             **(dict({"tiedBy": "Q101"}, **tied) if tied is not None else {}))

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
                        self.extra[(fname, path)] = {"why": "keyRules entry with no share"}
                    else:
                        measured(path, "value", share, "strong" if strong else "weak", ".".join(base), "keys " + " ".join(keys))
                else:
                    rows.append((path, "record", "measurement record of a keyRule", "", ""))
                continue
            if p[0] == "hands":
                if p[-1] == "rule":
                    share = flat.get(tuple(p[:-1]) + ("share",))
                    measured(path, "value", share, "strong" if share >= self.strong else "weak", ".".join(p[:-1]),
                             "hands.source, the Parsons IMC key, Round 17, R274")
                else:
                    rows.append((path, "record", "measurement record of a hand rule", "", ""))
                continue
            if (isinstance(v, str) and NAMEISH.search(v)) or (isinstance(v, list) and v and all(isinstance(x, str) and NAMEISH.search(x) for x in v)):
                rows.append((path, "input", "input name or location, no design value", "", ""))
                continue
            numbers = isinstance(v, list) and v and all(isinstance(x, (int, float)) for x in v)
            if path not in CHOOSERS and (any(path == b or path.startswith(b + ".") for b in BOOKKEEPING)
                                         or (p[0] in ("dataBook", "connect") and isinstance(v, (str, list)) and not numbers)):
                rows.append((path, "setting", "setting of a reader or check, no design value", "", ""))
                continue
            kind = "host" if HOST.search(path) else "value"
            listed = self.review.get((fname, path))
            if listed:
                rows.append((path, kind, BADER, "Q104", listed))
                self.extra[(fname, path)] = {"question": "Q104", "where": listed}
                self.reviewed.add((fname, path))
                continue
            inpath = [s for s in p if re.fullmatch(r"Q\d{3}", s)]
            if inpath:
                q = inpath[0]
                status = RECOMMENDED if q in self.open else BADER if q in self.closed_bader else UNKNOWN
                rows.append((path, kind, status, q, "the Q ID in the path"))
                self.extra[(fname, path)] = {"question": q, "where": "the Q ID in the path"} if status != UNKNOWN else {"why": "the Q ID in the path is not open and not closed by Bader"}
                continue
            if path == "majority.strongShare":
                rows.append((path, kind, BADER, "standing rule", "CLAUDE.md Majority rule, Bader's standing rule"))
                self.extra[(fname, path)] = {"line": "the Majority rule of CLAUDE.md", "where": "CLAUDE.md", "text": "Bader's standing rule since Round 17, for every round"}
                continue
            if path == "levelProfiles.default":
                rows.append((path, kind, BADER, "Q050", "CLAUDE.md and steps/questions-closed.md"))
                self.extra[(fname, path)] = {"question": "Q050", "where": "CLAUDE.md and steps/questions-closed.md",
                                             "text": "parsons2024Rev6 is the level profile of every new build, Rev 6 prevails"}
                continue
            if fname == self.profile and v is not None and v != [] and v != {}:
                tied = self.tie(p, flat)
                if tied:
                    measured(path, kind, tied[1], "strong" if tied[1] >= self.strong else "weak", tied[0], "tied by Q101 to " + tied[0], tied[2])
                    continue
            texts = []
            par = tuple(p[:-1])
            own = par + (p[-1] + "Note",)
            if own in flat:
                texts.append((".".join(own), flat[own], True, []))
            size = len([x for x in flat if x[:len(par)] == par])
            names = self.short_names(p)
            for k in NOTE_KEYS:
                if par + (k,) in flat and size <= 8 and isinstance(flat[par + (k,)], str):
                    note = flat[par + (k,)]
                    if size <= 3:
                        # The note of an object of at most 3 leaves speaks of that one value, as zoneFrom.Zone 1 holds from and why
                        texts.append((".".join(par + (k,)), note, True, []))
                    else:
                        # In the note of a small object a mark counts in the sentence that names the value, as addin.position gives CHOSEN
                        texts += [(".".join(par + (k,)), s, True, []) for s in sentences(note) if any(named_in(n, s) for n in names + [p[-1]])]
            for rid, t in list(self.rule_text.items()) + self.note_text:
                for s in sentences(t):
                    extra = self.pair_names(p, s)
                    if any(named_in(n, s) for n in names) or extra:
                        texts.append((rid, s, False, extra))
            # A question that names the value carries it, its ID is the subject of its own text, as Q086 names checkPointLimitMm
            pieces = {}
            for q, piece in self.question_pieces:
                if any(named_in(n, piece) for n in names):
                    texts.append((q, q + " " + " ".join(n for n in names if named_in(n, piece)), True, []))
                    pieces.setdefault(q, piece)
            found = self.by_text(texts, names)
            if found:
                (status, ref, where), text = found
                # The words of the question itself, its own ID alone is what gave the status
                text = pieces.get(where, text)
                rows.append((path, kind, status, ref, where))
                self.extra[(fname, path)] = dict({"question": ref} if status != CODE else {}, where=where, text=text)
                continue
            if v is None or v == [] or v == {}:
                rows.append((path, kind, UNKNOWN, "", "null or empty"))
                self.extra[(fname, path)] = {"why": "null or empty"}
                continue
            # The nearest source above the value names where it came from
            src = None
            for i in range(len(p) - 1, -1, -1):
                for cand in (tuple(p[:i]) + ("source",), tuple(p[:i]) + (p[i] + "_source",)):
                    if cand in flat and isinstance(flat[cand], str):
                        src = cand
                        break
                if src:
                    break
            # Q106 (Bader, 2026-10-27). A source that starts with UNKNOWN says the records name no document of the value
            if src and src[0] != "levelProfiles" and flat[src].startswith(UNKNOWN):
                rows.append((path, kind, UNKNOWN, "", "%s: %s" % (".".join(src), flat[src][:48])))
                self.extra[(fname, path)] = {"nearestSource": ".".join(src), "why": flat[src]}
                continue
            # Q100 (Bader, 2026-10-27). A source outside the level profiles names a client document the value is copied from, a level
            # profile source names a past model, which only a share of Q101 measures
            if src and src[0] != "levelProfiles":
                document = sentences(flat[src])[0].rstrip(".")
                rows.append((path, kind, DOCUMENT, "", "%s: %s" % (".".join(src), flat[src][:48])))
                self.extra[(fname, path)] = {"document": document, "pageOrSheet": "UNKNOWN", "source": ".".join(src), "path": p}
                continue
            why = ("%s: %s, " % (".".join(src), flat[src][:48]) if src else "") + WHY_NONE
            rows.append((path, kind, UNKNOWN, "", why))
            self.extra[(fname, path)] = dict({"nearestSource": ".".join(src)} if src else {}, why=WHY_NONE)
        return rows

    def _pages(self):
        """Q100. The page or sheet of each DOCUMENT value, only where the records write one: in the source of the value, or in a
        sentence of a rule line, a note or the README of the profile, after the revision of its document and nearest to a name of it."""
        docs = {k: e for k, e in self.extra.items() if "document" in e}
        names = collections.defaultdict(set)
        for key, e in docs.items():
            p = e["path"]
            for j in range(2, len(p) + 1):
                for i in range(0, j - 1):
                    names[".".join(p[i:j])].add(key)
            # A key alone names it only when it reads as a key, cluster_split_positions, never as a word such as foundation
            for seg in p:
                if len(seg) >= 9 and self.name_count[seg] == 1 and re.search(r"_|[a-z][A-Z]", seg):
                    names[seg].add(key)
        found = collections.defaultdict(set)

        def revs(text):
            return {int(r) for r in REV.findall(text)}

        def held(s, m, wanted):
            return any(int(r.group(1)) in wanted and 0 <= m.start() - r.start() <= NEAR for r in REV.finditer(s))

        for key, e in docs.items():
            source_text = self.leaf[(key[0], e["source"])]
            for s in sentences(source_text)[:1]:
                for m in PAGE.finditer(s):
                    if held(s, m, revs(e["document"])):
                        found[key].add(m.group(1))
        for _, text in self.page_records:
            for s in sentences(text):
                for m in PAGE.finditer(s):
                    spots = [(abs(n.start() - m.start()), name) for name in names for n in re.finditer(r"(?<![\w])%s(?![\w])" % re.escape(name), s)]
                    if not spots:
                        continue
                    best = min(d for d, _ in spots)
                    for d, name in spots:
                        if d == best:
                            for key in names[name]:
                                if held(s, m, revs(docs[key]["document"])):
                                    found[key].add(m.group(1))
        for key, e in docs.items():
            if found.get(key):
                e["pageOrSheet"] = " and ".join(sorted(found[key]))

    def _rule_rows(self):
        # The status a rule takes from the values: those it names, and those whose evidence names it
        key_paths = collections.defaultdict(list)
        for f, rows in self.value_rows.items():
            for row in rows:
                if row[1] in ("value", "host"):
                    p = row[0].split(".")
                    for depth in range(1, len(p) + 1):
                        for n in self.short_names(p[:depth]):
                            key_paths[n].append((f, row))

        def label(f, row):
            # A MEASURED value is shown by the entry that measures it, a tied field of Q101 by its keyRules entry
            if row[2] != MEASURED:
                return row[3]
            return self.extra[(f, row[0])]["measuredBy"].split(".")[-1] + " " + str(self.extra[(f, row[0])]["share"])
        key_rule_names = {}
        profiles = self.data[self.profile]["levelProfiles"]
        # The level profile of every new build, levelProfiles.default, parsons2024Rev6 by Q050
        for name, entry in profiles[profiles["default"]].get("keyRules", {}).items():
            if isinstance(entry, dict) and "share" in entry:
                key_rule_names[name] = entry
        value = re.compile(r"\b\d+(\.\d+)?\s?(mm|m|%|s|minutes|min|GiB|GB|MiB|degrees|deg|kPa)\b|config/project\.json|" + re.escape(self.profile) + "|keyRules")
        rule_rows = []
        for rid, title in self.ids:
            t = self.rule_text.get(rid, "")
            found = collections.defaultdict(set)
            for q in Q.findall(t):
                if q in self.closed_bader:
                    found[BADER].add(q)
                elif q in self.open:
                    found[RECOMMENDED].add(q)
            for q in self.open_by_rule.get(rid, ()):
                found[RECOMMENDED].add(q)
            for q in self.bader_by_rule.get(rid, ()):
                found[BADER].add(q)
            if "keyRules" in t:
                for name, entry in key_rule_names.items():
                    if (len(name) >= 6 and named_in(name, t)) or re.search(r"keyRules[ .]%s\b" % re.escape(name), t):
                        found[MEASURED].add("%s %s" % (name, entry["share"]))
            named = sorted({n for n in key_paths if named_in(n, t)})
            for n in named:
                for f, row in key_paths[n]:
                    if row[2] in ORDER:
                        found[row[2]].add(label(f, row))
            for f, rows in self.value_rows.items():
                for row in rows:
                    if row[2] in ORDER and rid in RID.findall(row[4]):
                        found[row[2]].add(label(f, row))
            if "CHOSEN" in t:
                found[CODE].add("CHOSEN mark")
            if not found and (value.search(t) or named):
                found[UNKNOWN].add("")
            if not found:
                rule_rows.append((rid, title, "process", "", ""))
                continue
            where = ", ".join(named[:4]) if named else "the rule text"
            status = ", ".join(s for s in ORDER if s in found)
            refs = " ".join(sorted(x for s in ORDER for x in found.get(s, ()) if x and s != MEASURED))
            shares = " ".join(sorted(found.get(MEASURED, ())))[:120]
            rule_rows.append((rid, title, where, status, " ".join(x for x in (refs, shares) if x)))
        return rule_rows

    def values(self):
        """Every value of the two files, kinds value and host, as (file, path, kind, status)."""
        return [(f, row[0], row[1], row[2]) for f in self.files for row in self.value_rows[f] if row[1] in ("value", "host")]

    def entry(self, f, path, status):
        """The block entry of a value: its status first, then the fields its status needs."""
        e = self.extra.get((f, path), {})
        keep = {BADER: ("question", "line", "where"), RECOMMENDED: ("question", "where"), CODE: ("where",),
                MEASURED: ("share", "strength", "measuredBy", "tiedBy", "candidate", "note"), DOCUMENT: ("document", "pageOrSheet", "source"),
                UNKNOWN: ("nearestSource", "why")}[status]
        return dict({"status": status}, **{k: e[k] for k in keep if k in e})

    def block(self):
        out = {"note": "R401. Written by python/rule_status.py --write from the records, never by hand. One entry per value of the two "
                       "files by file and dotted path, its status and its evidence. Never read as a value."}
        for f in self.files:
            out[f] = {path: self.entry(f, path, status) for g, path, _, status in self.values() if g == f}
        return out


def block_text(block):
    """The block as the last top level key of the rules profile, one entry per line."""
    parts = ["    %s: %s" % (json.dumps("note"), json.dumps(block["note"], ensure_ascii=False))]
    for f in (f for f in block if f != "note"):
        lines = ["      %s: %s" % (json.dumps(path, ensure_ascii=False), json.dumps(e, ensure_ascii=False)) for path, e in block[f].items()]
        parts.append("    %s: {\n%s\n    }" % (json.dumps(f), ",\n".join(lines)) if lines else "    %s: {}" % json.dumps(f))
    return '  "%s": {\n%s\n  }' % (STATUS, ",\n".join(parts))


def put_block(text, block):
    """The rules profile text with its block status replaced, or added as its last key, the rest of the text byte for byte. Refused
    when any value of the file would change."""
    made = block_text(block)
    head = text.find('\n  "%s": {' % STATUS)
    if head >= 0:
        opening = text.index("{", head)
        end = key_rules_block.object_span(text, opening)
        new = text[:head + 1] + made + text[end:]
    else:
        close = text.rstrip().rfind("}")
        body = text[:close].rstrip()
        new = body + ",\n" + made + "\n" + text[close:]
    before, after = json.loads(text), json.loads(new)
    before.pop(STATUS, None)
    after.pop(STATUS, None)
    if before != after:
        raise ValueError("the block status would change a value of the rules profile (R401)")
    return new


def write(root):
    """R401. Writes the block status into the rules profile, returns the counts by status."""
    found = Audit(root)
    path = Path(root) / found.profile
    text = path.read_bytes().decode("utf-8")
    new = put_block(text, found.block())
    if new != text:
        path.write_bytes(new.encode("utf-8"))
    return collections.Counter(status for _, _, _, status in found.values())


def check(root):
    """R401. The entries whose evidence changed since the block was written: values with no entry, entries of no value, and entries
    whose status or evidence a run on the records as they are now gives otherwise."""
    found = Audit(root)
    written = json.loads((Path(root) / found.profile).read_text(encoding="utf-8")).get(STATUS) or {}
    fresh = found.block()
    changes = []
    for f in found.files:
        old, new = written.get(f) or {}, fresh[f]
        changes += ["%s %s: no entry, now %s" % (f, p, json.dumps(new[p], ensure_ascii=False)) for p in new if p not in old]
        changes += ["%s %s: entry of no value" % (f, p) for p in old if p not in new]
        changes += ["%s %s: %s, now %s" % (f, p, json.dumps(old[p], ensure_ascii=False), json.dumps(new[p], ensure_ascii=False))
                    for p in new if p in old and old[p] != new[p]]
    return changes


def cell(text, n):
    text = str(text).replace("|", "/").replace("\n", " ")
    return text if len(text) <= n else text[:n - 3] + "..."


CODE_SEG = re.compile(r"^([A-Z][A-Z0-9]{1,3}(-IM)?|\d+(\.\d+)?|Zone \d+|[a-z]{3}|u1\d)$")


def pattern(path):
    parts = path.split(".")
    return ".".join("*" if i > 0 and CODE_SEG.match(s) and not re.fullmatch(r"Q\d{3}", s) else s for i, s in enumerate(parts))


def tables(found, out):
    """The tables of sections 3 to 5 of steps/investigations/rule-status-audit.md, as the audit of PR 68 wrote them."""
    main = ["| Rule | Title | Where the value lives | Status | Q ID or share |", "|---|---|---|---|---|"]
    proc = ["| Rule | Title | Value |", "|---|---|---|"]
    for rid, title, where, status, ref in found.rule_rows:
        if where == "process":
            proc.append("| %s | %s | process, no value |" % (rid, cell(title, 60)))
        else:
            main.append("| %s | %s | %s | %s | %s |" % (rid, cell(title, 50), cell(where, 70), status, cell(ref, 100)))
    (out / "rules_main.md").write_text("\n".join(main) + "\n", encoding="utf-8")
    (out / "rules_proc.md").write_text("\n".join(proc) + "\n", encoding="utf-8")
    values = []
    for f in found.files:
        counts = collections.Counter((r[1], r[2]) for r in found.value_rows[f])
        groups = collections.OrderedDict()
        for row in found.value_rows[f]:
            path, kind, status, ref, why = row
            if kind not in ("value", "host"):
                continue
            key = (pattern(path), kind, status, "" if status == MEASURED else ref, "" if status == MEASURED else why)
            groups.setdefault(key, []).append(row)
        values += ["### Values of %s" % f, "", "| Value | n | Kind | Status | Q ID or share | Evidence |", "|---|---|---|---|---|---|"]
        for (pat, kind, status, ref, why), members in groups.items():
            shown = pat if len(members) > 1 else "%s = %s" % (members[0][0], cell(json.dumps(found.leaf[(f, members[0][0])], ensure_ascii=False), 40))
            if status == MEASURED:
                ref, why = members[0][3], members[0][4]
            values.append("| %s | %d | %s | %s | %s | %s |" % (cell(shown, 110), len(members), kind, status, cell(ref, 40), cell(why, 58)))
        values += ["", "Every leaf of %s by kind and status (RAN, rule_status.py): %s." % (f, ", ".join("%s %s %d" % (k, s, n) for (k, s), n in sorted(counts.items()))), ""]
    (out / "values.md").write_text("\n".join(values) + "\n", encoding="utf-8")


def report(found):
    for f in found.files:
        counts = collections.Counter((r[1], r[2]) for r in found.value_rows[f])
        print(f, "leaves", len(found.value_rows[f]))
        for k, n in sorted(counts.items()):
            print("   ", k[0], k[1], n)
    rule_status = collections.Counter()
    for row in found.rule_rows:
        if row[2] != "process":
            for s in row[3].split(", "):
                rule_status[s] += 1
    print("rules", len(found.rule_rows), "process", sum(1 for r in found.rule_rows if r[2] == "process"), "with a value",
          sum(1 for r in found.rule_rows if r[2] != "process"))
    print("rules by status, a rule may hold several", dict(rule_status))
    status_total = collections.Counter(status for _, _, _, status in found.values())
    print("values by status", {s: status_total[s] for s in ORDER}, "total", sum(status_total.values()))
    rows = [r for f in found.files for r in found.value_rows[f]]
    unknown = collections.Counter(r[4].split(":")[0] if ":" in r[4] else r[4] for r in rows if r[2] == UNKNOWN)
    print("UNKNOWN by evidence", dict(unknown))
    named_q = collections.Counter(r[3] for r in rows if r[2] in (BADER, RECOMMENDED, CODE))
    print("Bader, RECOMMENDED and Code by reference", dict(named_q))
    pages = collections.Counter(e["pageOrSheet"] for e in found.extra.values() if "document" in e)
    print("DOCUMENT by page or sheet", dict(pages))
    print("open", len(found.open), "closed", len(found.closed), "closed naming Bader", len(found.closed_bader))


def main(argv=None):
    parser = argparse.ArgumentParser(description="The status of every value of the rules profile and the settings (R401).")
    parser.add_argument("root", help="the repo root")
    parser.add_argument("out", nargs="?", help="a folder for the audit tables")
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--write", action="store_true", help="write the block status of the rules profile")
    mode.add_argument("--check", action="store_true", help="list the entries whose evidence changed since the block was written")
    args = parser.parse_args(argv)
    if args.check:
        changes = check(args.root)
        for line in changes:
            print(line)
        print("entries whose evidence changed: %d" % len(changes))
        return 1 if changes else 0
    if args.write:
        counts = write(args.root)
        print("wrote the block status, values by status", {s: counts[s] for s in ORDER}, "total", sum(counts.values()))
        return 0
    found = Audit(args.root)
    if args.out:
        tables(found, Path(args.out))
    report(found)
    return 0


if __name__ == "__main__":
    sys.exit(main())
