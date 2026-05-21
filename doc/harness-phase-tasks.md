# SpecForge · Harness And Policies Phase Tasks

Last reviewed: 2026-05-19.

This is the single execution task list for harness and phase-policy work.

Use this document as the operational backlog for implementation.

## Working Rules

- Keep tasks phase-oriented and implementation-ready.
- Prefer read-only visibility work before stronger enforcement work.
- Do not duplicate the same rule in multiple places when one shared contract is enough.
- When implementation uncovers technical debt, record it here before continuing.
- If a task changes compile-time or validated behavior, follow the repository version-bump workflow after validation.

## Status Model

- `todo`: not started
- `doing`: in progress
- `blocked`: waiting on a decision or prerequisite
- `done`: implemented and locally validated

## Execution Order

1. `capture` boundary clarification
2. `refinement` prompt inspector
3. shared receipt and read-model substrate
4. `implementation` evidence and policy foundation
5. `review` and `release-approval` reusable gate hardening
6. execution envelopes and stronger permissions
7. cross-phase metrics and profiles

## Shared Tasks

### Shared Foundation

- [x] `H-SHARED-01` Status: `done`
  Define the shared contract for `effectivePrompt` and `effectiveContext`.
  Output: domain model and receipt payload shape.
  Notes: this is the base for `refinement`, `spec`, `technical-design`, `implementation`, `review`, `release-approval`, and `pr-preparation`.

- [x] `H-SHARED-02` Status: `done`
  Persist `effectivePrompt` and `effectiveContext` in execution receipts for normal model-backed phase execution.
  Output: receipt contract, serialization, backward-compatible read path.
  Notes: do not persist prompt text in `timeline.md`.

- [x] `H-SHARED-03` Status: `done`
  Expose latest effective prompt and effective context through workflow detail DTOs and MCP.
  Output: read model consumable by portal and non-portal operator surfaces.

- [x] `H-SHARED-04` Status: `done`
  Define the shared phase-policy contract.
  Output: explicit structure for repository access, allowed tools, writable paths, forbidden paths, evidence requirements, and eligibility rules.
  Notes: policy must be inspectable before it becomes strongly enforced.

- [x] `H-SHARED-05` Status: `done`
  Define the structured evidence record contract.
  Output: actor, inputs, outputs, settings, tools used, blocking reason, validation summary, and evidence links.

- [x] `H-SHARED-06` Status: `done`
  Define the execution-envelope contract.
  Output: per-phase declared boundaries for tools, write scope, repo boundaries, and budget.

- [x] `H-SHARED-07` Status: `done`
  Define the repository-global graph and per-user-story impact-graph lifecycle.
  Output: persistence contract, freshness model, fallback path, and ownership boundaries between workflow runtime and graph service.
  Notes: must include `use graph if available` and `allow graph materialization/refresh for this user story` feature flags, overwrite semantics, and the contract for preserving or replacing an existing global graph.

- [x] `G-DESIGN-01` Status: `done`
  Define the semantic code-graph architecture.
  Output: approved design for global graph, impact graph, fallback mini-graph pack, and subsystem boundaries.
  Notes: this is a design task, not an implementation task.

- [x] `G-DESIGN-02` Status: `done`
  Define graph builder strategy and model posture.
  Output: agreed first-cut stance for parser extraction, optional model assistance, local/on-prem preference, and whether embeddings are required at all.
  Notes: implementation must not assume embeddings or remote model dependence before this is closed.

- [x] `G-DESIGN-03` Status: `done`
  Define graph query and phase-consumption semantics.
  Output: approved first query families, bounded query rules, and how `technical-design`, `implementation`, and `review` consume graph artifacts.
  Notes: this closes what the graph is actually allowed to answer before MCP and CLI tools are implemented.

- [x] `G-DESIGN-04` Status: `done`
  Define graph governance, freshness, and audit semantics.
  Output: overwrite confirmation policy, failure fallback policy, freshness model, and the event model for graph cost and build traceability.
  Notes: `H-SHARED-10` depends on this being explicit.

- [x] `H-SHARED-08` Status: `done`
  Define the graph MCP tool family.
  Output: first contract for global graph status, build, refresh, impact-graph materialization, and bounded graph queries.
  Notes: MCP and CLI must support first-time graph creation from zero, explicit rebuild, dry-run status, and confirmation before overwriting an existing graph.
  Notes: depends on `G-DESIGN-01`, `G-DESIGN-02`, and `G-DESIGN-03`.

- [x] `H-SHARED-09` Status: `done`
  Define graph runtime controls and configuration surfaces.
  Output: settings model, portal configuration switches, MCP/CLI flag mapping, and default behavior for when graph artifacts exist versus when they must be created.
  Notes: minimum switches are `use semantic graph when available` and `allow graph build/refresh for touched US scope`.
  Notes: depends on `G-DESIGN-01` and `G-DESIGN-04`.

- [x] `H-SHARED-10` Status: `done`
  Define the graph build audit and cost ledger contract.
  Output: persisted record of who triggered graph creation or refresh, when, why, which mode ran, whether existing graph state was reused or replaced, token usage, latency, and resulting artifacts.
  Notes: must inventory expensive full rebuilds and be reusable from portal, MCP, and CLI.
  Notes: depends on `G-DESIGN-04`.

## Phase Tasks

### Capture

Current stance:
`capture` is the workflow entry phase. It is not a prompt-driven model execution phase and does not need the same prompt pipeline as the others.

- [x] `H-CAP-01` Status: `done`
  Make the `capture` boundary explicit in docs and read models.
  Output: operator surfaces must clearly show that `capture` is a non-model entry phase.

- [x] `H-CAP-02` Status: `done`
  Define the minimum observable execution record for `capture`.
  Output: who created the US, when, from what source, and which initial artifacts were materialized.
  Notes: do not force `effectivePrompt` onto this phase.

### Refinement

- [x] `H-REF-01` Status: `done`
  Implement a first prompt inspector for `refinement`.
  Output: an operator can visualize the effective prompt actually sent during the latest refinement execution.
  Notes: this must include system prompt, user prompt, warnings, and source prompt paths.

- [x] `H-REF-02` Status: `done`
  Expose the effective runtime context for `refinement`.
  Output: visible list of injected artifacts and context files with paths and hashes.

- [x] `P-REF-01` Status: `done`
  Define visible refinement policy inputs.
  Output: inspectable refinement tolerance, blocking conditions, and auto-answer eligibility.

- [x] `P-REF-02` Status: `done`
  Persist a policy snapshot for refinement execution.
  Output: receipt-linked record of the governing refinement policy.

- [x] `H-REF-03` Status: `done`
  Add skill preselection outputs to `refinement`.
  Output: persisted `required`, `candidate`, and `rejected` skills plus rationale and context gaps for the user story.

- [x] `H-REF-04` Status: `done`
  Add the first graph-scope handoff to `refinement`.
  Output: persisted graph scope request with seed nodes, seed files, depth, and unresolved scope questions for `technical-design`.

### Spec

- [x] `H-SPEC-01` Status: `done`
  Expose the effective prompt and context for `spec` execution.
  Output: latest-execution inspector available through workflow detail DTOs and MCP.

- [x] `H-SPEC-02` Status: `done`
  Make spec approval inputs more inspectable.
  Output: visible link between generated spec artifact, approval prompt paths, and latest execution receipt.

- [x] `P-SPEC-01` Status: `done`
  Define spec-phase eligibility and policy checks.
  Output: explicit rules for when spec can execute and when approval can proceed.

- [x] `P-SPEC-02` Status: `done`
  Persist the effective spec policy snapshot.
  Output: audit-ready record of the rules that governed spec execution and approval.

### Technical Design

- [x] `H-TD-01` Status: `done`
  Expose the effective prompt and context for `technical-design`.
  Output: latest-execution inspector with prompt, warnings, artifacts, and context files.

- [x] `H-TD-02` Status: `done`
  Define the first design evidence record.
  Output: structured summary of design inputs, output artifact, and any orchestration metadata.

- [x] `P-TD-01` Status: `done`
  Define design policy visibility.
  Output: inspectable rules for repository access, subagent usage, and design-quality gating when required.

- [ ] `P-TD-02` Status: `todo`
  Prepare an explicit design gate contract.
  Output: reusable criteria for repositories that require design approval before implementation.

- [x] `H-TD-03` Status: `done`
  Feed `technical-design` from selected skills plus impact-graph context.
  Output: first design context pack that uses selected skills, graph scope, impact summary, and graph-backed expansions when available.
  Notes: must respect graph feature flags and fall back cleanly when graph usage is disabled, missing, stale, or failed.

- [x] `H-TD-04` Status: `done`
  Define the first bounded graph-query evidence contract for `technical-design`.
  Output: traceable persistence of follow-up graph queries and returned summaries when they influenced the design artifact.
  Notes: should capture query purpose, actor, selected model/tooling, token usage if any, latency, and whether the answer came from global graph, impact graph, or fallback analysis.

### Implementation

- [x] `H-IMP-01` Status: `done`
  Unify implementation evidence into a structured execution evidence record.
  Output: implementation evidence should be queryable beyond appended markdown sections.
  Notes: preserve current evidence markdown/json outputs while introducing the structured substrate.
  Notes: implementation evidence should also be able to reference graph-guided file selection and graph refresh actions when they influenced change scope.

- [x] `H-IMP-02` Status: `done`
  Expose the effective prompt and context for `implementation`.
  Output: latest-execution inspector showing prompt, warnings, injected artifacts, context files, and evidence links.

- [x] `P-IMP-01` Status: `done`
  Define implementation phase policy requirements.
  Output: explicit evidence requirements, writable scope rules, forbidden mutation zones, and repository-access semantics.
  Notes: this is a priority item and aligns with the separate policy thread already in motion.

- [x] `P-IMP-02` Status: `done`
  Persist and expose the effective implementation policy snapshot.
  Output: operator can see which implementation policy governed the run and why an action was allowed or blocked.

- [x] `P-IMP-03` Status: `done`
  Introduce the first implementation execution envelope.
  Output: declared tool permissions, writable paths, repo boundaries, and budget model for implementation runs.

### Review

- [x] `H-REV-01` Status: `done`
  Expose the effective prompt and context for `review`.
  Output: latest-execution inspector including implementation evidence inputs.

- [x] `H-REV-02` Status: `done`
  Promote review outputs toward reusable structured gate results.
  Output: machine-readable verdict, findings summary, correction targets, and linked evidence.
  Notes: review should be able to reference final impact-graph slices or graph deltas when they were part of the decision path.

- [x] `P-REV-01` Status: `done`
  Define review policy visibility.
  Output: inspectable review evidence policy, approval override conditions, and force-approval rationale capture.

- [x] `P-REV-02` Status: `done`
  Persist the effective review policy snapshot.
  Output: audit-ready record of the governing review rules.

### Release Approval

- [x] `H-RA-01` Status: `done`
  Expose the effective prompt and context for `release-approval`.
  Output: latest-execution inspector including `branch.yaml`, `timeline.md`, and prior review evidence inputs.

- [x] `H-RA-02` Status: `done`
  Create the first structured release evidence pack.
  Output: bundled review verdict, changed files, validation results, release risk summary, and supporting artifact links.

- [x] `P-RA-01` Status: `done`
  Define release-approval eligibility and evidence policy.
  Output: explicit rules for what must exist before release approval can run or be approved.

- [x] `P-RA-02` Status: `done`
  Persist the effective release-approval policy snapshot.
  Output: audit-ready record of the governing release rules.

### PR Preparation

- [ ] `H-PR-01` Status: `todo`
  Expose the effective prompt and context for `pr-preparation`.
  Output: latest-execution inspector tied to the generated PR artifact.

- [ ] `H-PR-02` Status: `todo`
  Link PR preparation output to the structured evidence substrate.
  Output: reusable evidence references for PR description generation and later audit.

- [ ] `P-PR-01` Status: `todo`
  Define PR preparation policy visibility.
  Output: inspectable policy for PR metadata requirements and publication readiness.

### Auto Refinement Answers

- [ ] `H-ARA-01` Status: `todo`
  Decide whether auto-refinement answers enter the same effective-prompt inspection model in wave one or wave two.
  Output: explicit scope decision recorded before implementation diverges.

- [ ] `P-ARA-01` Status: `todo`
  Define visible policy for auto-answer eligibility.
  Output: inspectable rules for when the system may answer refinement questions automatically.

## Cross-Phase Tasks

### Metrics

- [ ] `H-MET-01` Status: `todo`
  Define the first metric set.
  Output: attempt count, lead time, retries, blocked duration, waiting-user duration.

- [ ] `H-MET-02` Status: `todo`
  Expose metrics in workflow operator surfaces.
  Output: phase-local and workflow-wide views derived from persisted facts.

### Profiles

- [ ] `P-PROF-01` Status: `todo`
  Define reusable harness profiles.
  Output: first built-in profiles such as `strict`, `balanced`, and `regulated`.

- [ ] `P-PROF-02` Status: `todo`
  Define override and lock behavior for profiles.
  Output: explicit inheritance and governance rules.

## Technical Debt Watch

Record debt here as soon as it is discovered during implementation.

- [ ] `TD-001` Status: `todo`
  No technical-debt items recorded yet.
  Update this placeholder with concrete debt once something real appears.

## UI Bug Backlog

- [ ] `BUG-REF-001` Status: `todo`
  `Add Context Files` in the refinement detail does not trigger the expected file-attach flow.
  Impact: operator cannot add context files from the refinement card as intended.
  Notes: validate both workflow detail and any browser-served portal path before fixing.

- [ ] `BUG-GRAPH-001` Status: `todo`
  Workflow graph layout editing needs snap-to-grid or equivalent alignment assistance.
  Impact: aligning phases manually is unnecessarily difficult and visually inconsistent.
  Notes: evaluate grid snapping, guide lines, or phase-to-phase magnetic alignment.

- [ ] `BUG-GRAPH-002` Status: `todo`
  Edited workflow graph layout resets to the default layout and loses user changes.
  Impact: layout editing is not trustworthy because persisted changes are not preserved.
  Notes: verify save path, restore path, and any re-render/cache invalidation behavior in both VS Code and CLI-served portal flows.
