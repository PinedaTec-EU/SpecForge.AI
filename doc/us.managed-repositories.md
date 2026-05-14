# SpecForge · Managed repositories user story

This user story defines the product intent for SpecForge Central as an enterprise control surface capable of registering and governing multiple repositories.

SpecForge Central is not a Scrum board or an epic tracker. It is a governance control plane for spec-driven delivery. Its primary objects are repositories, governed user stories, specs, phase evidence, policy compliance, approvals, regressions, and audit trails.

## Objective

Allow SpecForge Central to create, register, inspect, and manage multiple repositories so a team can run the SpecForge SDD workflow across a portfolio instead of only inside the currently opened repository.

## User Story

As a platform owner,
I want SpecForge Central to maintain a catalog of managed repositories,
so I can choose where a user story belongs, see workflow state per repository, and operate each repository without losing its local source-of-truth artifacts.

## User Story State

- State: `draft`
- Priority: `high`
- Type: `platform-capability`
- Primary source of truth: this `doc/` folder

## Initial Scope

Includes:

- define a managed repository catalog with stable repository identity, display name, local path or remote URL, default branch, enabled state, and optional grouping metadata
- create or register a repository from SpecForge Central before creating user stories for it
- list managed repositories and show their SpecForge readiness state
- select the target repository when creating, importing, or inspecting a user story from the central surface
- keep user-story artifacts, workflow state, prompts, and config persisted inside the selected repository
- surface repository-level health warnings when the repository is missing SpecForge bootstrap files, has invalid configuration, or cannot be reached
- show Central policy compliance for each managed repository, including stale runtime, missing MCP/plugin setup, prompt drift, unlocked mandatory settings, invalid provider routing, and review evidence policy mismatch
- expose portfolio-level workflow status by phase, blocked reason, waiting-user state, review failure, release pending state, PR-preparation state, and completed workflows
- keep Central-created and locally-created user stories visible as a governed work queue without making Scrum epics, sprints, or story points the primary organizing model

Excludes for the first iteration:

- cross-repository user stories that mutate several repositories in one workflow run
- automatic repository cloning credentials or secret management
- organization-wide analytics beyond repository list, readiness, compliance, decision queues, counts, and current workflow status
- direct Git provider administration
- Scrum planning features such as sprint boards, velocity tracking, story-point estimation, and epic hierarchy as first-class concepts

## Business Rules

- SpecForge Central may index many repositories, but a user story has exactly one owning repository for phase-1 execution.
- The central catalog must not become the source of truth for repository-local workflow artifacts.
- Removing a repository from the central catalog must not delete the repository or its `.specs/` artifacts.
- A disabled repository remains visible for audit but cannot start new workflow phases from the central surface.
- Repository identity must be stable across path renames when an explicit repository id exists.
- Repository readiness must be derived from observable files and configuration, not from conversational memory.
- Central can define mandatory policies that local runtimes must enforce, including disabling local prompt customization, locking review evidence policy, restricting model/provider routing, requiring evidence packs, requiring PR links before completion, or restricting forced review approvals.
- Locked Central policies must be visible in local clients and MCP responses so users know why an action or setting is disabled.
- Local/offline work must use the last known Central policy when connected policy enforcement is enabled; stale policy state must be visible and auditable.
- Central must organize work around spec workflow state, evidence, blockers, decisions, and repository ownership rather than Scrum ceremony.

## Initial Acceptance Criteria

- [ ] A platform owner can add a managed repository with name, path or remote URL, default branch, and enabled state.
- [ ] A platform owner can edit repository metadata without modifying repository-local user-story artifacts.
- [ ] A platform owner can disable or remove a repository from the central catalog without deleting local repository data.
- [ ] A user can choose a managed repository before creating or importing a user story from SpecForge Central.
- [ ] Repository lists show readiness and blocking configuration problems per repository.
- [ ] Opening a repository from the central surface uses that repository's own `.specs/` artifacts for user stories, workflow state, prompts, and timeline.
- [ ] Central shows whether each repository is compliant with mandatory policy locks.
- [ ] A locked Central policy disables or blocks the corresponding local customization path.
- [ ] Central exposes a decision queue for waiting-user gates, review failures, release approvals, policy violations, and regressions.
- [ ] Central work views filter by repository, owner, phase, blocked reason, evidence state, policy compliance, and PR/issue link state.

## Notes

- This capability turns SpecForge from a repository-local assistant into SpecForge Central without weakening the existing repository-as-source-of-truth rule.
- The first implementation should prefer a small central catalog and explicit repository selection over automatic discovery.
- The product can feel familiar to teams that use Jira, but its organizing model is governed SDD: specs, evidence, gates, policies, and traceability.
