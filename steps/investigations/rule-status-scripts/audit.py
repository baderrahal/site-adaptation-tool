"""Moved to python/rule_status.py in Round ADDIN 4g1 (R401), this runs it with the same arguments: audit.py <repo root> <output folder>."""
import runpy
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[3] / "python"))
runpy.run_module("rule_status", run_name="__main__")
