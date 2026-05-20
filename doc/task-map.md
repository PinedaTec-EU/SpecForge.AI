# SpecForge · Task Map

Last reviewed: 2026-05-20.

This is the central task entrypoint for repository work.

Use this file to see:

- which work blocks exist;
- where each block is tracked in detail;
- what is active now;
- what is already done versus still open.

## Status Model

- `todo`: not started
- `doing`: in progress
- `blocked`: waiting on a decision or prerequisite
- `done`: implemented and locally validated

## Working Rule

- Do not open a new orphan task file when an existing block already owns that scope.
- Add new tasks first to this map, then to the detailed file for the owning block.
- If a block becomes too large, split it into its own `doc/tasks/<block>.md` file and link it here.

## Task Tree

| Block | Status | Detailed tracking | Scope |
| --- | --- | --- | --- |
| `HARNESS` | `doing` | [harness-phase-tasks.md](harness-phase-tasks.md), [harness-implementation-plan.md](harness-implementation-plan.md), [harness-engineering-checklist.md](harness-engineering-checklist.md), [semantic-code-graph-design.md](semantic-code-graph-design.md) | Harness governance, effective prompt/context inspection, policy visibility, evidence, metrics, profiles, and semantic code-graph design plus rollout |
| `MVP` | `doing` | [implementation-plan.md](implementation-plan.md), [roadmap.md](roadmap.md) | Product MVP sequencing, extension/MCP/portal gaps, medium-term product delivery |
| `BRANCH` | `doing` | [tasks/branch-lifecycle.md](tasks/branch-lifecycle.md) | Branch lifecycle, branch activation, Git context switching, PR-oriented branch metadata |

## Current Focus

- `HARNESS`: finish semantic code-graph design first, then implement lifecycle, flags, MCP/CLI creation, and graph-guided context orchestration on top of explicit decisions.
- `BRANCH`: add automatic Git branch switching when the active user story changes.
- `MVP`: keep branch lifecycle and prompt inspection visible as the main near-term product gaps.

## Intake Rule For New Work

When a new task appears:

1. decide which block owns it;
2. add or update the block status here;
3. record the concrete task in the linked detailed file;
4. only create a new detailed file if the task does not fit any existing block.
