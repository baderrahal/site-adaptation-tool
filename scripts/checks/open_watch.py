"""Runs a Python file and logs each path it opens, for clean_folder.py. Usage: open_watch.py <log> <file> <args>."""
import os
import runpy
import sys


def main():
    log_path, target, args = sys.argv[1], sys.argv[2], sys.argv[3:]
    seen = set()
    log = open(log_path, "w", encoding="utf-8")

    def hook(event, details):
        if event != "open" or not details or not isinstance(details[0], (str, bytes, os.PathLike)):
            return
        path = os.fsdecode(details[0])
        if path not in seen and path != log_path:
            seen.add(path)
            log.write(path + "\n")
            log.flush()

    sys.addaudithook(hook)
    sys.argv = [target, *args]
    sys.path.insert(0, os.path.dirname(target))
    runpy.run_path(target, run_name="__main__")


if __name__ == "__main__":
    main()
