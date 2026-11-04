import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))

# test_folder.py is a check script, not a file of tests
collect_ignore = ["test_folder.py"]
