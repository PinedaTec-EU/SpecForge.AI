# SpecForge · Product Vision

## Problem

AI-assisted development often degrades into:

- isolated prompts without traceability
- non-persisted decisions
- ambiguous handoffs
- rework caused by late validation
- weak governance for teams larger than one person
- local deviations from company-approved AI delivery rules
- central work tracking that shows tickets but not spec readiness, evidence, gates, or policy compliance

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
- mandatory policy enforcement for repositories that must not allow local bypass of company rules
- a work hub centered on governed specs, evidence, decisions, and repository ownership rather than Scrum ceremony

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

Central must also act as a policy control plane. A platform owner can publish mandatory rules, and connected local runtimes must enforce them. Examples include disabling custom prompt overrides, locking review evidence policy, restricting provider/model routing, requiring review evidence packs, requiring PR links before completion, and restricting forced review approvals to authorized roles.

The Central work surface should be familiar enough for teams that use issue trackers, but it should not become a Scrum clone. The central objects are governed user stories, specs, evidence, workflow gates, blockers, policy state, repositories, and audit decisions.

## Principles

- Chat is not the final source of truth.
- All relevant information is persisted in repository artifacts.
- The tool must be usable from another workstation by cloning the repository only.
- The UX must prioritize operational clarity.
- The system must allow checkpoints and human intervention between phases, but only where the checkpoint meaningfully changes risk.
- Multi-repository management must not move workflow truth out of the managed repository.
- Local plugin distribution must keep the workflow portable across repositories: clone the repository, install or copy the plugin bundle, and run the same MCP-backed process.
- Central policy locks must be explicit, visible, and enforceable by local runtimes.
- Central should organize work around SDD flow and governance, not around sprints, story points, or epic hierarchy.

## Non-Goals For Phase 1

- advanced visual workflow editor
- intra-user-story parallelization
- full PR and issue integration
- multi-provider optimization beyond a minimal abstraction
- replacing Jira or Azure DevOps as a generic planning suite
- Scrum planning, velocity management, or enterprise portfolio accounting as first-class product goals
