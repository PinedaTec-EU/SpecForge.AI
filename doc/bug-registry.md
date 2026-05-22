# Bug Registry

## Open Bugs

### SFB-001

- Bug code: `SFB-001`
- Discovery date: `2026-05-22`
- Status: `Open`
- Short description: Switching the repository branch in a single local checkout can hide or strand user stories created after returning to `main`, because user-story visibility currently depends on the active branch/worktree instead of a stable control workspace.
- Reproduction steps:
  1. Start from `main` in one local checkout.
  2. Create a user story and advance it until SpecForge creates or activates its work branch.
  3. Return manually to `main`.
  4. Create a second user story from that same checkout.
  5. Switch back to the first user story branch.
  6. Observe that the second user story may no longer appear from that branch context, even though it was created later in the same local repository.
