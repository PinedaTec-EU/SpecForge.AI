# SpecForge.AI · Roadmap

Last reviewed: 2026-05-14.

This document is the product-facing roadmap summary. For implementation sequencing and history, see [implementation-plan.md](implementation-plan.md).

## Current Strength

The current product already provides:

- canonical workflow phases with explicit approvals and regression paths
- repository-local persistence under `.specs/`
- VS Code workflow UX with graph, detail, and audit visibility
- local MCP server for agent-driven operations
- browser workflow portal for non-IDE operation
- packaged plugin bundle for repository-local distribution
- model and agent routing for phase execution

## Next

Near-term priorities:

- richer branch lifecycle and Git/PR metadata
- provider-neutral issue and PR integrations beyond the current GitHub-oriented path
- prompt diffing and effective prompt inspection UX
- completed-work visibility and sidebar search
- one-command plugin release, sync, and validation pipeline

## Later

Product expansions under consideration:

- customizable workflows and more advanced agent strategies
- review evidence packs for PR descriptions and release review
- review findings workflow with tracked remediation status
- Definition-of-Ready dashboard for refinement completeness

## Strategic Direction

The major strategic move is SpecForge Central.

SpecForge Central is the planned enterprise control plane for governed SDD across repositories, including:

- managed repository catalog
- readiness checks
- workflow portfolio visibility
- policy distribution and mandatory policy locks
- drift detection between central policy and local overrides
- role-aware governance and audit exports
- work queues centered on specs, evidence, blockers, and approvals

The core rule remains unchanged: repository-local artifacts stay the source of truth.
