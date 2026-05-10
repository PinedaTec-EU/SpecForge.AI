# SpecForge · Product Vision

## Problem

AI-assisted development often degrades into:

- isolated prompts without traceability
- non-persisted decisions
- ambiguous handoffs
- rework caused by late validation
- weak governance for teams larger than one person

## Value Proposition

SpecForge does not aim only to generate code. It aims to govern how the result is produced through an explicit, persisted, and auditable SDD workflow.

## Target User

Development teams that need:

- consistency across artifacts
- process control
- versioned living documentation
- progress visibility
- the ability to intervene without breaking traceability
- central governance of several repositories from one operational surface
- a local MCP/plugin boundary that agents can use without depending on conversational memory

## Expected Outcome

Starting from a user story, the system must allow a governed flow through:

1. initial definition
2. approved spec baseline
3. technical design
4. implementation
5. review
6. PR preparation

The same workflow should be inspectable from VS Code, a local browser portal, and MCP clients, with all three surfaces reading the same persisted repository artifacts.

SpecForge Central must also allow teams to register and manage multiple repositories. The central surface owns repository discovery, selection, readiness, and portfolio visibility; each managed repository remains the owner of its local `.specs/` artifacts, prompts, state, and workflow history.

## Principles

- Chat is not the final source of truth.
- All relevant information is persisted in repository artifacts.
- The tool must be usable from another workstation by cloning the repository only.
- The UX must prioritize operational clarity.
- The system must allow checkpoints and human intervention between phases, but only where the checkpoint meaningfully changes risk.
- Multi-repository management must not move workflow truth out of the managed repository.
- Local plugin distribution must keep the workflow portable across repositories: clone the repository, install or copy the plugin bundle, and run the same MCP-backed process.

## Non-Goals For Phase 1

- advanced visual workflow editor
- intra-user-story parallelization
- full PR and issue integration
- multi-provider optimization beyond a minimal abstraction
