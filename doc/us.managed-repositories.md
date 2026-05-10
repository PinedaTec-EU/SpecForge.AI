# SpecForge · Managed repositories user story

This user story defines the product intent for SpecForge Central as an enterprise control surface capable of registering and governing multiple repositories.

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

Excludes for the first iteration:

- cross-repository user stories that mutate several repositories in one workflow run
- automatic repository cloning credentials or secret management
- organization-wide analytics beyond repository list, counts, and current workflow status
- direct Git provider administration

## Business Rules

- SpecForge Central may index many repositories, but a user story has exactly one owning repository for phase-1 execution.
- The central catalog must not become the source of truth for repository-local workflow artifacts.
- Removing a repository from the central catalog must not delete the repository or its `.specs/` artifacts.
- A disabled repository remains visible for audit but cannot start new workflow phases from the central surface.
- Repository identity must be stable across path renames when an explicit repository id exists.
- Repository readiness must be derived from observable files and configuration, not from conversational memory.

## Initial Acceptance Criteria

- [ ] A platform owner can add a managed repository with name, path or remote URL, default branch, and enabled state.
- [ ] A platform owner can edit repository metadata without modifying repository-local user-story artifacts.
- [ ] A platform owner can disable or remove a repository from the central catalog without deleting local repository data.
- [ ] A user can choose a managed repository before creating or importing a user story from SpecForge Central.
- [ ] Repository lists show readiness and blocking configuration problems per repository.
- [ ] Opening a repository from the central surface uses that repository's own `.specs/` artifacts for user stories, workflow state, prompts, and timeline.

## Notes

- This capability turns SpecForge from a repository-local assistant into SpecForge Central without weakening the existing repository-as-source-of-truth rule.
- The first implementation should prefer a small central catalog and explicit repository selection over automatic discovery.
