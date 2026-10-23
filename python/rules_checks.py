"""The checks of rules/README.md that are coded. Each returns a list of issues and an empty list when the data passes."""

UNKNOWN = "unknown"


def issue(rule, plots, text):
    return {"rule": rule, "plots": list(plots), "text": text}


def check_attachment(clusters, profile):
    """R65. Detached has no cluster. One side sits at a cluster end. Both sides sit inside a cluster."""
    typologies = profile.get("typologies")
    issues = []
    for cluster in clusters:
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
        if not cluster.get("orderSure", True):
            text += ". The order along the road is not sure"
        issues.append(issue("R66", cluster["plots"], text))
    return issues
