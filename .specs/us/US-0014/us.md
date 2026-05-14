# US-0014 · Provide a spec-governance work hub in Central

## Metadata
- Kind: `feature`
- Category: `user-stories`
- Tags: `sf-central`

## Objective
As an engineering lead or platform owner, I want SpecForge Central to provide a central work hub for governed spec-driven delivery, so teams can coordinate AI-assisted work without turning the product into a Scrum board or epic tracker.

Problem to solve:
SpecForge Central should feel familiar as a shared operational surface, but its organizing unit is not the sprint, epic, or backlog column. The center of the product is the governed user story/spec workflow: readiness, phase, evidence, blockers, approvals, regressions, policy compliance, and repository ownership.

Initial scope for discussion:
- show work by repository, category, phase, status, owner, policy compliance, blocked reason, and review/release risk
- make waiting-user, review-failed, policy-blocked, release-pending, and PR-preparation states first-class filters
- show which specs need decisions, which reviews need evidence, and which repositories are out of policy
- support assignment and visibility without importing Scrum concepts as the primary model
- keep repository-local .specs artifacts as the source of truth for workflow details

Questions before implementation:
- What is the first Central view: portfolio dashboard, work queue, repository readiness, decision queue, or evidence queue?
- Which roles need which queues: platform owner, team lead, reviewer, developer, compliance reviewer?
- Should Central create user stories directly or primarily route users into the selected repository workflow?
- Which Jira-like concepts should be deliberately excluded from the first version?

## Initial Scope
- Includes:
  - ...
- Excludes:
  - ...
