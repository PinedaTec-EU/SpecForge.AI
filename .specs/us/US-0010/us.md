# US-0010 · Link SpecForge workflow truth with issues and PRs

## Metadata
- Kind: `feature`
- Created By: `unknown`
- Owner: `jmrpineda`
- Category: `integrations`

## Objective
As a platform or engineering lead using SpecForge with AI coding agents, I want repository-local SpecForge workflow truth to connect to external issue and pull request systems, so chat does not become the only bridge between approved intent, generated code, review evidence, and delivery records.

Problem to solve:
SpecForge already persists user-story truth inside the repository through artifacts, state, timeline, context files, prompts, MCP actions, and the workflow portal. The remaining gap is outside the workspace: issue trackers and pull requests still need an explicit link to the approved SpecForge artifacts and phase evidence.

Initial scope for discussion:
- identify the minimum useful integration between a SpecForge user story and an external issue or PR
- decide which direction should come first: export/publish SpecForge evidence to a PR, import issue metadata into a user story, or bidirectional linking
- preserve repository-local .specs artifacts as the source of truth
- avoid turning chat history into the delivery record
- make the reviewer able to answer what was approved, what changed, which evidence exists, and why the workflow moved forward or backwards

Out of scope until clarified:
- organization-wide reporting
- automatic merge or release decisions
- replacing GitHub, GitLab, Jira, or Azure DevOps
- storing authoritative workflow state outside .specs

Questions we should discuss before implementation:
- What external system should be supported first: GitHub PRs, GitHub Issues, Jira, Azure DevOps, or an export-only Markdown/JSON bridge?
- Should the first version create/update PR descriptions, attach a review evidence block, link commits, or only publish a SpecForge summary?
- What is the minimum evidence a reviewer needs next to the PR diff?
- Should SpecForge require a PR link before release approval or keep it optional?
- What data may safely leave the repository-local .specs tree?

## Initial Scope
- Includes:
  - ...
- Excludes:
  - ...
