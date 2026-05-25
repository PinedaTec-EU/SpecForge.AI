# SpecForge · Branch Lifecycle Tasks

Last reviewed: 2026-05-25.

This file keeps the branch-lifecycle planning context and historical milestones.

Canonical open work for this block now lives in GitHub Issues, not in this Markdown file.

## Block Goal

SpecForge should keep the repository branch aligned with the active user story context instead of leaving branch state as a manual side effect.

## Completed Milestones

- [x] `BR-001` Status: `done`
  Create and persist the work branch when `spec` approval establishes the branch contract.
  Output: `branch.yaml` records base branch, work branch, strategy, and PR metadata placeholders.

- [x] `BR-002` Status: `done`
  Protect phase execution by activating the recorded work branch before workflow continuation.
  Output: workflow execution switches to the recorded work branch and stashes out-of-scope workspace changes when needed.

## Canonical Open Issues

- [#36](https://github.com/PinedaTec-EU/SpecForge.AI/issues/36) `SFF-036: Automatic user-story branch activation`
  Covers branch activation when the active user story changes from VS Code, MCP, or the CLI workflow portal, with `main` fallback when no work branch exists yet.

- [#37](https://github.com/PinedaTec-EU/SpecForge.AI/issues/37) `SFT-037: Shared branch activation path and visibility`
  Covers consolidation of the safe activation path plus clearer operator visibility when SpecForge switches branch automatically.

## Links

- Parent map: [task-map.md](../task-map.md)
- Product backlog anchor: [implementation-plan.md](../implementation-plan.md)
- Product summary: [roadmap.md](../roadmap.md)
