# SpecForge.AI · Roadmap

Last reviewed: 2026-05-26.

This document is the product-facing roadmap summary. For implementation sequencing and history, see [implementation-plan.md](implementation-plan.md).

For the detailed harness-adoption baseline and capability checklist, see [harness-engineering-checklist.md](harness-engineering-checklist.md).

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
- branch auto-switch with `main` fallback before work-branch activation
- one-command plugin release, sync, and validation pipeline

## Governed Tool Access

New near-term feature track for governed execution tools and knowledge access:

Packaging rule:

- SpecForge OSS must remain useful for local repository intelligence and local execution tools.
- SpecForge Central should monetize governed shared tools, organization-grade connectors, shared retrieval infrastructure, and cross-repository policy or audit.
- The initial packaging and routing matrix is defined in [execution-tool-packaging.md](execution-tool-packaging.md).

- [#68](https://github.com/PinedaTec-EU/SpecForge.AI/issues/68) `SFF-060: Governed execution tool catalog and policy`
  Define the first-class contract for model-facing execution tools, separate them from workflow MCP tools, and make allowed-tool policy part of the enforced phase envelope.
- [#69](https://github.com/PinedaTec-EU/SpecForge.AI/issues/69) `SFF-061: Private repository and knowledge retrieval adapters`
  Add read-only adapters for private repositories, RAG/CAG-style retrieval, internal docs, and graph-backed knowledge sources behind governed SpecForge tool contracts.
- [#70](https://github.com/PinedaTec-EU/SpecForge.AI/issues/70) `SFF-062: Tool-use evidence and operator inspection`
  Persist structured evidence for governed tool use and expose operator-facing inspection so tool access remains auditable, explainable, and reviewable.
- [#71](https://github.com/PinedaTec-EU/SpecForge.AI/issues/71) `SFF-063: Provider-neutral tool orchestration bridge`
  Add a harness-level orchestration bridge so tool-enabled execution works across providers without making one provider API the product contract.

This track extends existing harness work rather than replacing it:

- [#53](https://github.com/PinedaTec-EU/SpecForge.AI/issues/53) `SFF-053: Stronger execution envelopes and permissions`
- [#42](https://github.com/PinedaTec-EU/SpecForge.AI/issues/42) `SFF-042: Effective prompt and context inspection`
- [#48](https://github.com/PinedaTec-EU/SpecForge.AI/issues/48) `SFT-048: Structured execution evidence substrate`
- [#72](https://github.com/PinedaTec-EU/SpecForge.AI/issues/72) `SFI-072: Execution tool inventory and packaging matrix`

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

Commercial posture:

- SpecForge Central is currently a private side project, not a public/open product line.
- Enterprise use is expected to be commercial.
- A limited self-hosted offering may exist later, but only in a controlled form with explicit governance boundaries.
