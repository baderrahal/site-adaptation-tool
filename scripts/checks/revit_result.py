"""A PR that touches Revit add-in files holds a file in steps/revit-results/, unless it has the label revit-later."""
import sys

import check_common

NAME = "revit result"
REVIT_FOLDERS = ("dotnet/SiteAdaptation.AddIn/", "dotnet/SiteAdaptation.Placer/", "dotnet/RevitPrototypeReader/", "dotnet/SiteAdaptation.TestHost/")
RESULTS = "steps/revit-results/"
LATER = "revit-later"


def failures_of(changed, labels):
    touched = [path for path in changed if path.startswith(REVIT_FOLDERS)]
    results = [path for path in changed if path.startswith(RESULTS) and not path.endswith("README.md")]
    if touched and not results and LATER not in labels:
        return ["%d Revit add-in files changed, starting %s, with no file in %s and no label %s" % (len(touched), touched[0], RESULTS, LATER)]
    return []


def main(argv=None):
    event = check_common.pr_event()
    if event is None:
        check_common.say(NAME, "NOT RUN", "not a pull request")
        return 0
    changed = check_common.changed_files(event["base"], event["head"])
    return check_common.finish(NAME, failures_of(changed, event["labels"]), "no Revit result owed, or held")


if __name__ == "__main__":
    sys.exit(main())
