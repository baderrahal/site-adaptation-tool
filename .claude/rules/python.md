---
paths:
  - "python/**/*.py"
  - "scripts/**/*.py"
---

# Python files

- Python is the test checker, the user's PC runs no Python.
- Parity with C# on the 13 steps of the plan build (R345, docs/design/16-design-basis.md:44). A change to a step changes both languages in one PR, and addin.parity.steps of config/project.json names every field. That block has grown to 30 entries, the count 13 is the older word for it.
- No project name in any Python file (R63). No project value in code.
