"""Cuts an old part out of a record byte for byte and proves it. Usage: split_record.py <record> <older file> <first marker> <last marker> <pointer line>.

The part from the line that starts with the first marker, up to the line that starts with the last marker, moves to the older file. One pointer
line goes in where it was. The proof is the file put back together: its SHA-256 and its count of every R and Q id equal the original.
"""
import hashlib
import re
import sys
from pathlib import Path

R_ID = re.compile(rb"\bR\d{1,3}\b")
Q_ID = re.compile(rb"\bQ\d{3}\b")


def sha(data):
    return hashlib.sha256(data).hexdigest()


def counts(data):
    return {"R total": len(R_ID.findall(data)), "R distinct": len(set(R_ID.findall(data))), "Q total": len(Q_ID.findall(data)), "Q distinct": len(set(Q_ID.findall(data)))}


def line_start(data, marker):
    """The offset of the first line that starts with the marker."""
    at = data.find(b"\n" + marker)
    if at < 0:
        raise SystemExit("no line starts with %r" % marker)
    return at + 1


def split(data, first, last, pointer):
    start = line_start(data, first)
    end = len(data) if last == b"<end>" else line_start(data, last)
    if end <= start:
        raise SystemExit("the last marker comes before the first")
    if R_ID.search(pointer) or Q_ID.search(pointer):
        raise SystemExit("the pointer line must name no id, it would change the count")
    return data[:start] + pointer + data[end:], data[start:end], start


def put_back(kept, older, start, pointer):
    if kept[start:start + len(pointer)] != pointer:
        raise SystemExit("the pointer line is not where it was put")
    return kept[:start] + older + kept[start + len(pointer):]


def main(argv=None):
    record, older_file, first, last, pointer = (argv or sys.argv[1:])[:5]
    path = Path(record)
    data = path.read_bytes()
    line = pointer.encode("utf-8") + b"\n\n"
    kept, older, start = split(data, first.encode("utf-8"), last.encode("utf-8"), line)
    target = Path(older_file)
    if target.exists():
        raise SystemExit("%s is there already" % target)
    target.write_bytes(older)
    path.write_bytes(kept)
    back = put_back(path.read_bytes(), target.read_bytes(), start, line)
    same = sha(back) == sha(data)
    print("%s: %d bytes, sha256 %s" % (path.as_posix(), len(data), sha(data)))
    print("kept %d bytes, moved %d bytes to %s, sha256 %s" % (len(kept), len(older), target.as_posix(), sha(older)))
    print("put back: sha256 %s, equal to the original: %s" % (sha(back), same))
    before = counts(data)
    after = counts(path.read_bytes() + target.read_bytes())
    print("ids before %s" % before)
    print("ids after  %s, equal: %s" % (after, before == after))
    if not same or before != after:
        raise SystemExit("the proof failed")
    return 0


if __name__ == "__main__":
    sys.exit(main())
