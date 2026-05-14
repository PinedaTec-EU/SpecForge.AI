<p align="center">
  <a href="https://github.com/PinedaTec-EU/SpecForge.AI">
    <img loading="lazy" alt="SpecForge.AI" src="./doc/images/banner.png" width="85%"/>
  </a>
</p>

# SpecForge.AI

SpecForge.AI is a governed Spec-Driven Development runtime for AI-assisted software teams.

It turns a user story into a traceable delivery workflow with explicit phases, persisted artifacts, audit trail, human checkpoints, regression paths, and an MCP boundary that keeps workflow mutations out of ad hoc chat edits.

## What SpecForge Is

SpecForge is not just a code generator or prompt wrapper.

It is a product for running AI-assisted delivery with repository-local truth:

- specs and workflow artifacts live in the repo under `.specs/`
- phases are explicit and deterministic
- important transitions leave audit evidence
- humans can approve, regress, rewind, or reopen with traceability
- agents operate through MCP tools instead of manually editing workflow state

Today the product ships as:

- a VS Code extension
- a local MCP server over `stdio`
- a self-contained browser workflow portal
- a packaged Codex/MCP plugin bundle for repository-local use

## Why Teams Pick It

SpecForge is strongest when a team wants more than "an agent that writes code from a prompt".

The current differentiators are:

- governed SDD workflow instead of open-ended prompt execution
- repository-local source of truth instead of platform-only state
- explicit human checkpoints and regression semantics
- workflow auditability through artifacts and timeline history
- packaged MCP/plugin distribution for non-VS Code agent clients
- local browser portal for inspection and operation outside the IDE

## What Is Already Strong

The foundation is no longer hypothetical. The current product already includes:

- canonical workflow phases from capture through PR preparation
- .NET workflow/domain core with persisted YAML and Markdown artifacts
- VS Code workflow UX with graph, detail, audit, and contextual actions
- browser workflow portal for local operation outside VS Code
- local MCP server for agent-driven workflow operations
- packaged plugin bundle for repository-local Codex/MCP installation
- model and agent profile routing for phase execution

<p align="center">
  <img loading="lazy" alt="Workflow overview showing the constellation graph and spec detail" src="./doc/images/workflow-overview.png" width="92%"/>
</p>

## Market Position

SpecForge competes in the spec-driven development space, but with a narrower claim than most nearby tools.

Compared with tools such as GitHub Spec Kit, Colign, Planu, or Kiro, SpecForge is positioned as a governed, repository-local SDD runtime: explicit workflow, persisted artifacts, auditable transitions, MCP-constrained operations, and a path toward cross-repository governance through SpecForge Central.

Short version:

- choose lighter alternatives if you mainly want spec templates or a simpler agent workflow
- choose SpecForge if you want governed delivery workflow and repository truth

For the full competitive comparison, see [doc/market-positioning.md](doc/market-positioning.md).

## Roadmap

Near-term priorities:

- richer branch lifecycle and Git/PR metadata
- provider-neutral issue and PR integrations beyond the current GitHub-oriented path
- prompt diffing and effective prompt inspection
- sidebar search and completed-work visibility
- one-command plugin release and validation pipeline

Strategic product direction:

- SpecForge Central as the enterprise control plane for managed repositories, readiness checks, policy distribution, workflow visibility, drift detection, and audit

The fuller roadmap lives in [doc/roadmap.md](doc/roadmap.md) and the implementation history in [doc/implementation-plan.md](doc/implementation-plan.md).

## Start Here

- evaluating the product: [doc/getting-started.md](doc/getting-started.md)
- configuring model and agent routing: [doc/model-configuration.md](doc/model-configuration.md)
- understanding runtime behavior and persistence: [doc/runtime-and-persistence.md](doc/runtime-and-persistence.md)
- contributing to the codebase: [doc/developer-onboarding.md](doc/developer-onboarding.md)

## Core Documents

- [doc/product-vision.md](doc/product-vision.md)
- [doc/architecture.md](doc/architecture.md)
- [doc/workflow-canonico-fase-1.md](doc/workflow-canonico-fase-1.md)
- [doc/mcp-contract-fase-1.md](doc/mcp-contract-fase-1.md)
- [doc/spec-schema-fase-1.md](doc/spec-schema-fase-1.md)
- [doc/market-positioning.md](doc/market-positioning.md)

## License

[MIT](LICENSE)
