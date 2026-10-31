"""R411, round 4i. The cluster and mirror proposals of a build with designBasis.clusters.propose under the mode of the manual, by Rev 06 2.1.A to
2.1.E: a detached unit alone with no hand, an attached unit in the one split of its row of attached units into the clusters of the rules
profile, each half of a cluster the hand keyRules hand cluster gives the prototype file for its side, else no proposal with the reason. A
proposal sets no value of the plan. ClusterProposals.cs of the C# gives the same. docs/design/16-design-basis.md"""

# The attachment words of typologies of the rules profile, 2.1.A a detached unit, 2.1.B and 2.1.C the units that stand against others
DETACHED = "detached"
ATTACHED = ("one_side", "both_sides")
SIDES = ("left", "right")
HAND_TABLE = "hand cluster"

DETACHED_WHY = "unit %s is detached, Rev 06 2.1.A sets a detached unit alone and gives it no hand, so no mirror flag, Q226 (R411)"
NO_ATTACHMENT = "typologies of the rules profile give unit %s no attachment Rev 06 2.1 names, so no proposal, Q229 (R411)"
CROWDED = "plot %s of the row %s has %d neighbours on its %s side, so the row is no single line of units, no proposal, Q229 (R411)"
NO_SPLIT = "the row %s of units %s splits into no clusters of the rules profile, Rev 06 2.1.E, so no proposal, Q229 (R411)"
SEVERAL = "the row %s of units %s splits %d ways into clusters of the rules profile, Rev 06 2.1.E, so no proposal, Q229 (R411)"
PROPOSED = "cluster %s of the rules profile, the one split of the row %s by Rev 06 2.1.E, the %s half, %s (R411)"
HAND = "the hand of %s for that side in keyRules hand cluster, the halves opposite hands by 2.1.B and 2.1.C"
NO_FILE = "unit %s links no prototype file, so no mirror flag"
NO_HAND = "keyRules hand cluster holds no %s hand of %s, so no mirror flag"
MIDDLE = "the middle unit is in neither half of 2.1.B and 2.1.C, so no mirror flag, Q229"


def sequences(profile):
    """clusters of the rules profile as unit sequences, each once under the first code that holds it, so a code of cluster_aliases, the
    same sequence under another name, counts once."""
    found, seen = [], set()
    for code, units in (profile.get("clusters") or {}).items():
        if tuple(units) not in seen:
            seen.add(tuple(units))
            found.append((code, tuple(units)))
    return found


def attachment(unit, profile):
    return ((profile.get("typologies") or {}).get(unit) or {}).get("attachment")


def row_of(plot, neighbours, attached):
    """The row of attached unit plots of plot, left to right, walked by the side neighbours of the hands step until a side holds no single
    attached unit plot, and the first plot of it with more than one neighbour on a side, with that side and count, or None."""
    row, seen = [plot], {plot}
    for side in SIDES:
        at = plot
        while True:
            near = (neighbours.get(at) or {}).get(side) or []
            if len(near) != 1 or near[0] in seen or not attached(near[0]):
                break
            at = near[0]
            seen.add(at)
            if side == "left":
                row.insert(0, at)
            else:
                row.append(at)
    crowded = next(((item, side, len((neighbours.get(item) or {}).get(side) or [])) for item in row for side in SIDES
                    if len((neighbours.get(item) or {}).get(side) or []) > 1), None)
    return row, crowded


def splits(units, known):
    """How many ways units splits into consecutive sequences of known, and the one split as (start, end, code) when there is exactly one."""
    ways = [0] * len(units) + [1]
    for start in range(len(units) - 1, -1, -1):
        ways[start] = sum(ways[start + len(sequence)] for _, sequence in known if tuple(units[start:start + len(sequence)]) == sequence)
    if ways[0] != 1:
        return ways[0], None
    found, start = [], 0
    while start < len(units):
        code, sequence = next((code, sequence) for code, sequence in known
                              if tuple(units[start:start + len(sequence)]) == sequence and ways[start + len(sequence)] > 0)
        found.append((start, start + len(sequence), code))
        start += len(sequence)
    return 1, found


def mirror_of(index, count, unit, files, key):
    """The side of a unit in its cluster, left the first half as cad_links.cluster_roles names it, the mirror flag keyRules hand cluster gives
    the file of the unit for that side and the words of the reason."""
    side = "left" if index < (count - 1) / 2.0 else "right" if index > (count - 1) / 2.0 else "middle"
    if side == "middle":
        return side, None, MIDDLE
    link = files.get(unit)
    if link is None:
        return side, None, NO_FILE % unit
    hand = (((key or {}).get(HAND_TABLE) or {}).get("byFile") or {}).get(link) or {}
    if hand.get(side) is None:
        return side, None, NO_HAND % (side, link)
    return side, hand[side], HAND % link


def proposals(numbers, rows, neighbours, profile, key, files):
    """R411. Per unit plot of numbers its proposal, cluster the plot numbers or None, mirrored and reason, by the attachment of its unit, its
    row of side neighbours, the clusters of the rules profile and keyRules hand cluster."""
    known = sequences(profile)

    def unit_of(plot):
        return (rows.get(plot) or {}).get("typologyRev9") or None

    def attached(plot):
        return attachment(unit_of(plot), profile) in ATTACHED

    found = {}
    for number in numbers:
        unit = unit_of(number)
        if unit is None:
            continue
        kind = attachment(unit, profile)
        if kind == DETACHED:
            found[number] = {"cluster": [number], "mirrored": None, "reason": DETACHED_WHY % unit}
            continue
        if kind not in ATTACHED:
            found[number] = {"cluster": None, "mirrored": None, "reason": NO_ATTACHMENT % unit}
            continue
        row, crowded = row_of(number, neighbours, attached)
        units = [unit_of(plot) for plot in row]
        named = " ".join(row)
        if crowded is not None:
            found[number] = {"cluster": None, "mirrored": None, "reason": CROWDED % (crowded[0], named, crowded[2], crowded[1])}
            continue
        count, split = splits(units, known)
        if split is None:
            why = NO_SPLIT % (named, " ".join(units)) if count == 0 else SEVERAL % (named, " ".join(units), count)
            found[number] = {"cluster": None, "mirrored": None, "reason": why}
            continue
        at = row.index(number)
        start, end, code = next(piece for piece in split if piece[0] <= at < piece[1])
        side, mirrored, why = mirror_of(at - start, end - start, unit, files, key)
        found[number] = {"cluster": row[start:end], "mirrored": mirrored, "reason": PROPOSED % (code, named, side, why)}
    return found


def counts(found):
    """R411. The count line of the summary: the plots with a proposed cluster and those with none."""
    return {"proposed": sum(1 for item in found.values() if item["cluster"] is not None), "none": sum(1 for item in found.values() if item["cluster"] is None)}
