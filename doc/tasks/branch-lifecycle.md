# SpecForge · Branch Lifecycle Tasks

Last reviewed: 2026-05-19.

This file tracks branch lifecycle work as one coherent block.

Use it for branch creation, branch activation, branch metadata, PR linkage, and user-story-driven Git context switching.

## Status Model

- `todo`: not started
- `doing`: in progress
- `blocked`: waiting on a decision or prerequisite
- `done`: implemented and locally validated

## Block Goal

SpecForge should keep the repository branch aligned with the active user story context instead of leaving branch state as a manual side effect.

## Tasks

- [x] `BR-001` Status: `done`
  Create and persist the work branch when `spec` approval establishes the branch contract.
  Output: `branch.yaml` records base branch, work branch, strategy, and PR metadata placeholders.

- [x] `BR-002` Status: `done`
  Protect phase execution by activating the recorded work branch before workflow continuation.
  Output: workflow execution switches to the recorded work branch and stashes out-of-scope workspace changes when needed.

- [ ] `BR-003` Status: `todo`
  Activate the Git branch when the active user story changes from the UI.
  Output: opening or selecting a user story in the VS Code workflow surfaces switches to that user story work branch.
  Notes: if the user story does not yet have a recorded work branch, switch to `main`.

- [ ] `BR-004` Status: `todo`
  Activate the Git branch when the active user story changes through MCP or the CLI workflow portal.
  Output: user-story-targeted MCP/CLI context changes switch to the recorded work branch.
  Notes: if the user story does not yet have a recorded work branch, switch to `main`.

- [ ] `BR-005` Status: `todo`
  Consolidate branch switching into one reusable safe activation path.
  Output: shared branch activation logic supports both work-branch activation and fallback activation to `main`, with the same stash and safety behavior.

- [ ] `BR-006` Status: `todo`
  Extend branch lifecycle tracking with clearer operator visibility.
  Output: logs or workflow-visible signals explain when SpecForge switched branch automatically and why.

## Links

- Parent map: [task-map.md](../task-map.md)
- Product backlog anchor: [implementation-plan.md](../implementation-plan.md)
- Product summary: [roadmap.md](../roadmap.md)
