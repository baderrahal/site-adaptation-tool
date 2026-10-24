"""The checks of rules/README.md that are coded. Each returns a list of issues and an empty list when the data passes."""

UNKNOWN = "unknown"


def issue(rule, plots, text):
    return {"rule": rule, "plots": list(plots), "text": text}


def check_attachment(clusters, profile):
    """R65. Detached has no cluster. One side sits at a cluster end. Both sides sit inside a cluster."""
    typologies = profile.get("typologies")
    issues = []
    for cluster in clusters:
        # Ends and inside mean nothing for plots that are not one row, so only R66 reports them
        if not cluster.get("row", True):
            continue
        size = len(cluster["plots"])
        for index, (plot, typology) in enumerate(zip(cluster["plots"], cluster["typologies"])):
            attachment = (typologies.get(typology) or {}).get("attachment")
            at_end = index in (0, size - 1)
            where = " (cluster " + cluster["code"] + ", " + str(size) + " units)"
            head = "Plot " + plot + " is " + typology
            if attachment == "detached":
                issues.append(issue("R65", [plot], head + ", detached, but it is in a cluster" + where))
            elif attachment == "one_side" and not at_end:
                issues.append(issue("R65", [plot], head + ", attached on one side, but it is inside a cluster" + where))
            elif attachment == "both_sides" and at_end:
                issues.append(issue("R65", [plot], head + ", attached on both sides, but it is at the end of a cluster" + where))
            elif attachment not in ("detached", "one_side", "both_sides"):
                issues.append(issue("R65", [plot], head + ", which has no attachment in the profile" + where))
    return issues


def check_known_clusters(clusters):
    """R66. A cluster whose code is unknown is a conflict, named with its plots and typologies."""
    issues = []
    for cluster in clusters:
        if cluster["code"] != UNKNOWN:
            continue
        text = "Plots " + ", ".join(cluster["plots"]) + " (" + ", ".join(cluster["typologies"]) + ") match no cluster in the profile"
        if not cluster.get("row", True):
            text += ". The plots do not form one straight row"
        elif not cluster.get("orderSure", True):
            text += ". The order along the road is not sure"
        issues.append(issue("R66", cluster["plots"], text))
    return issues


def check_unmatched_codes(clusters, codes):
    """R75. A cluster written with an old client code that has no Rev 6 cluster is an issue, one for each cluster."""
    issues = []
    for cluster in clusters:
        if cluster["code"] in codes:
            plots = ", ".join(cluster["plots"])
            issues.append(issue("R75", cluster["plots"], "Plots " + plots + " (" + ", ".join(cluster["typologies"]) + ") form cluster " + cluster["code"] + ", an old client code with no Rev 6 cluster"))
    return issues


# Two FFLs closer than half a millimetre are one level, the FFL is written to the millimetre
FFL_SAME = 0.0005


def split_walls(levels):
    """The walls where the FFL changes along a cluster, a wall n being the wall after unit n, counted from 1."""
    return [index + 1 for index in range(len(levels) - 1) if abs(levels[index + 1] - levels[index]) > FFL_SAME]


def check_one_ffl(clusters, ffls, profile):
    """R125, check 3. One FFL per cluster. A split is only allowed inside a cluster of cluster_max_units units, at a wall n
    that is in cluster_split_positions counted from either end, so a wall after unit 1 and after unit 3 of four are one case.

    clusters list their plots in row order, ffls maps a plot to its FFL in metres. Returns the issues and one record per
    cluster with its walls, each wall a double wall with its case, ends for the wall next to an end unit and middle for the others.
    """
    size, allowed = profile.get("cluster_max_units"), set(profile.get("cluster_split_positions"))
    issues, records = [], []
    for cluster in clusters:
        plots = [str(plot) for plot in cluster["plots"]]
        missing = [plot for plot in plots if ffls.get(plot) is None]
        if missing:
            records.append({"code": cluster.get("code"), "plots": plots, "status": "no FFL", "walls": []})
            continue
        walls = split_walls([ffls[plot] for plot in plots])
        count = len(plots)
        wrong = [wall for wall in walls if count != size or not ({wall, count - wall} & allowed)]
        walls_out = [{"after": wall, "case": "end" if wall in (1, count - 1) else "middle", "doubleWall": True} for wall in walls]
        status = "one FFL" if not walls else "split not allowed" if wrong else "split allowed"
        records.append({"code": cluster.get("code"), "plots": plots, "status": status, "walls": walls_out})
        if wrong:
            text = "Plots " + ", ".join(plots) + " (cluster " + str(cluster.get("code")) + ", " + str(count) + " units) change FFL after unit " + ", ".join(str(wall) for wall in wrong)
            text += ", a cluster of " + str(count) + " never splits" if count != size else ", which is not a split position"
            issues.append(issue("R125", plots, text))
    return issues, records
