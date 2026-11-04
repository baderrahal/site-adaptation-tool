# Acceptance tests

Owned by the lead. /round writes them first and merges them before lanes start, each must fail for the right reason until its task is done. A lane reads them and never changes them, a hook blocks it. pytest files named test_*.py, run by job "tests".
