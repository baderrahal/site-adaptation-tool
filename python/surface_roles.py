from statistics import median

# A surface that covers at least this share of the ramp corners and lies below them is the ground
GROUND_COVER = 0.95
# A corner counts as matching a surface when the floor corner level is this close to it, in metres
NEAR = 0.03
EPS = 1e-9
# What a role in the config means for a proposal. A design surface is not confirmed, so the proposal may leave it unknown
AGREE = {("ground", "ground"), ("road", "road"), ("design", "unknown"), (None, "unknown")}


def measure(corner_levels, surface_levels, invisible_share):
    """Numbers for one surface. Both lists run over the same corners and a surface level is None where the surface does not cover the corner."""
    diffs = [corner - level for corner, level in zip(corner_levels, surface_levels) if level is not None]
    return {
        "corners": len(corner_levels),
        "covered": len(diffs),
        "coveredShare": len(diffs) / len(corner_levels) if corner_levels else 0.0,
        "within30mm": sum(1 for diff in diffs if abs(diff) <= NEAR + EPS),
        "medianAbsDifference": median(abs(diff) for diff in diffs) if diffs else None,
        "invisibleShare": invisible_share,
        "medianFloorMinusSurface": median(diffs) if diffs else None,
    }


def propose(stats):
    """Ground when the surface covers nearly every corner and lies below them. Road when it has the most corners within 30 mm of the rest. Else unknown."""
    roles = {}
    for name, item in stats.items():
        below = item["medianFloorMinusSurface"]
        if item["coveredShare"] >= GROUND_COVER and below is not None and below > 0:
            roles[name] = "ground"
    rest = {name: item for name, item in stats.items() if name not in roles}
    best = max((item["within30mm"] for item in rest.values()), default=0)
    winners = [name for name, item in rest.items() if item["within30mm"] == best]
    for name in stats:
        roles.setdefault(name, "unknown")
    if best > 0 and len(winners) == 1:
        roles[winners[0]] = "road"
    return roles


def warnings(config_roles, proposal):
    """One line per surface where the config role and the proposal do not fit, with both names. The config is never changed."""
    lines = []
    for name, proposed in proposal.items():
        configured = config_roles.get(name)
        if (configured, proposed) not in AGREE:
            lines.append("warning: role proposal differs from config for " + name + ": config " + str(configured or "none") + ", proposal " + proposed)
    return lines


def report(stats, config_roles):
    proposal = propose(stats)
    return {
        "thresholds": {"groundCover": GROUND_COVER, "nearMetres": NEAR},
        "surfaces": {name: {**item, "configRole": config_roles.get(name), "proposed": proposal[name]} for name, item in stats.items()},
        "warnings": warnings(config_roles, proposal),
    }
