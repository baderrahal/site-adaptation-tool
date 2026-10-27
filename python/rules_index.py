"""R12. docs/rules/index.md, one line per rule with its id and bold title, under the part file that holds it, so a session finds a
rule by id with no read of a whole part. Written by this script only, test_r12_the_rules_index_matches_the_rule_files holds it."""
import argparse

import record_parts

INDEX = record_parts.RULES / "index.md"
HEAD = ("# Rules index\n\nWritten by python/rules_index.py from the parts of docs/rules/, never by hand. The parts joined in name order are the "
        "rules. Find a rule here by id, then read its line in its part by grep, never a whole part.\n")


def build_index():
    """The text of the index from the parts as they are now."""
    out = [HEAD]
    for path in record_parts.parts(record_parts.RULES):
        out.append("\n## %s\n\n" % path.name)
        out.extend("- %s\n" % title for _, title in record_parts.rule_titles(path.read_text(encoding="utf-8")))
    return "".join(out)


def main(argv=None):
    parser = argparse.ArgumentParser(description="Write docs/rules/index.md from the parts of docs/rules/.")
    parser.parse_args(argv)
    INDEX.write_text(build_index(), encoding="utf-8", newline="\n")
    print("wrote %s" % INDEX)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
