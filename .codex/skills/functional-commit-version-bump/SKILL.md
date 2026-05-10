---
name: functional-commit-version-bump
description: Use in this repository after completing any new functionality or functional subtask. Enforces a functional commit for the delivered change, followed by a separate version bump using the repository's versionbumper tool and its own independent commit.
---

# Functional Commit And Version Bump

This repository requires every completed functional change to close with two traceable commits. Do not consider a functional change complete until both commits exist:

1. A functional commit for the delivered change.
2. A separate version bump commit created after the functional commit.

## Functional Commit

- Before committing, run the relevant validation for the touched area.
- Stage only files that belong to the delivered change.
- Commit with a message that includes `done` and clearly maps to the delivered outcome.
- Do not include version bump files in the functional commit.

## Version Bump Commit

After the functional commit succeeds:

1. Run the repository version bump tool from the repo root:

```bash
dotnet versionbumper
```

2. Review the changed version files.
3. Stage only files changed by the version bump.
4. Commit them separately with a message that includes `done`, for example:

```bash
git commit -m "done bump version after <outcome>"
```

## Guardrails

- Do not run the version bump before the functional commit.
- Do not stop after only the functional commit when the task delivered a functional change.
- Do not mix functional code/docs changes with version bump changes.
- If the functional task is intentionally not committed, do not run the version bump.
- If `dotnet versionbumper` fails, stop and report the failure instead of hand-editing version files.
