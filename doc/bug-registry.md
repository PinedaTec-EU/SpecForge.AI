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

### SFB-002

- Bug code: `SFB-002`
- Discovery date: `2026-05-22`
- Status: `Open`
- Short description: SpecForge lacks a documented fallback rule for tracking and repairing engine defects outside its own workflow engine, so a defect in the engine can block or delay the registration, visibility, and execution of the work needed to fix it.
- Reproduction steps:
  1. Assume a defect affects local user-story intake, indexing, or workflow execution inside SpecForge itself.
  2. Try to register and drive the repair work only through the same affected SpecForge engine/runtime.
  3. Observe that the defect can prevent or degrade task registration, visibility, ownership routing, or execution progress for the repair itself.
