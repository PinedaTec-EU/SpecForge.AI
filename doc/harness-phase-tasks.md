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

- [ ] `H-SHARED-04` Status: `todo`
  Define the shared phase-policy contract.
  Output: explicit structure for repository access, allowed tools, writable paths, forbidden paths, evidence requirements, and eligibility rules.
  Notes: policy must be inspectable before it becomes strongly enforced.

- [ ] `H-SHARED-05` Status: `todo`
  Define the structured evidence record contract.
  Output: actor, inputs, outputs, settings, tools used, blocking reason, validation summary, and evidence links.

- [ ] `H-SHARED-06` Status: `todo`
  Define the execution-envelope contract.
  Output: per-phase declared boundaries for tools, write scope, repo boundaries, and budget.

## Phase Tasks

### Capture

Current stance:
`capture` is the workflow entry phase. It is not a prompt-driven model execution phase and does not need the same prompt pipeline as the others.

- [ ] `H-CAP-01` Status: `todo`
  Make the `capture` boundary explicit in docs and read models.
  Output: operator surfaces must clearly show that `capture` is a non-model entry phase.

- [ ] `H-CAP-02` Status: `todo`
  Define the minimum observable execution record for `capture`.
  Output: who created the US, when, from what source, and which initial artifacts were materialized.
  Notes: do not force `effectivePrompt` onto this phase.

### Refinement

- [x] `H-REF-01` Status: `done`
  Implement a first prompt inspector for `refinement`.
  Output: an operator can visualize the effective prompt actually sent during the latest refinement execution.
  Notes: this must include system prompt, user prompt, warnings, and source prompt paths.

- [ ] `H-REF-02` Status: `todo`
  Expose the effective runtime context for `refinement`.
  Output: visible list of injected artifacts and context files with paths and hashes.

- [ ] `P-REF-01` Status: `todo`
  Define visible refinement policy inputs.
  Output: inspectable refinement tolerance, blocking conditions, and auto-answer eligibility.

- [ ] `P-REF-02` Status: `todo`
  Persist a policy snapshot for refinement execution.
  Output: receipt-linked record of the governing refinement policy.

### Spec

- [ ] `H-SPEC-01` Status: `todo`
  Expose the effective prompt and context for `spec` execution.
  Output: latest-execution inspector available through workflow detail DTOs and MCP.

- [ ] `H-SPEC-02` Status: `todo`
  Make spec approval inputs more inspectable.
  Output: visible link between generated spec artifact, approval prompt paths, and latest execution receipt.

- [ ] `P-SPEC-01` Status: `todo`
  Define spec-phase eligibility and policy checks.
  Output: explicit rules for when spec can execute and when approval can proceed.

- [ ] `P-SPEC-02` Status: `todo`
  Persist the effective spec policy snapshot.
  Output: audit-ready record of the rules that governed spec execution and approval.

### Technical Design

- [ ] `H-TD-01` Status: `todo`
  Expose the effective prompt and context for `technical-design`.
  Output: latest-execution inspector with prompt, warnings, artifacts, and context files.

- [ ] `H-TD-02` Status: `todo`
  Define the first design evidence record.
  Output: structured summary of design inputs, output artifact, and any orchestration metadata.

- [ ] `P-TD-01` Status: `todo`
  Define design policy visibility.
  Output: inspectable rules for repository access, subagent usage, and design-quality gating when required.

- [ ] `P-TD-02` Status: `todo`
  Prepare an explicit design gate contract.
  Output: reusable criteria for repositories that require design approval before implementation.

### Implementation

- [ ] `H-IMP-01` Status: `todo`
  Unify implementation evidence into a structured execution evidence record.
  Output: implementation evidence should be queryable beyond appended markdown sections.
  Notes: preserve current evidence markdown/json outputs while introducing the structured substrate.

- [ ] `H-IMP-02` Status: `todo`
  Expose the effective prompt and context for `implementation`.
  Output: latest-execution inspector showing prompt, warnings, injected artifacts, context files, and evidence links.

- [ ] `P-IMP-01` Status: `todo`
  Define implementation phase policy requirements.
  Output: explicit evidence requirements, writable scope rules, forbidden mutation zones, and repository-access semantics.
  Notes: this is a priority item and aligns with the separate policy thread already in motion.

- [ ] `P-IMP-02` Status: `todo`
  Persist and expose the effective implementation policy snapshot.
  Output: operator can see which implementation policy governed the run and why an action was allowed or blocked.

- [ ] `P-IMP-03` Status: `todo`
  Introduce the first implementation execution envelope.
  Output: declared tool permissions, writable paths, repo boundaries, and budget model for implementation runs.

### Review

- [ ] `H-REV-01` Status: `todo`
  Expose the effective prompt and context for `review`.
  Output: latest-execution inspector including implementation evidence inputs.

- [ ] `H-REV-02` Status: `todo`
  Promote review outputs toward reusable structured gate results.
  Output: machine-readable verdict, findings summary, correction targets, and linked evidence.

- [ ] `P-REV-01` Status: `todo`
  Define review policy visibility.
  Output: inspectable review evidence policy, approval override conditions, and force-approval rationale capture.

- [ ] `P-REV-02` Status: `todo`
  Persist the effective review policy snapshot.
  Output: audit-ready record of the governing review rules.

### Release Approval

- [ ] `H-RA-01` Status: `todo`
  Expose the effective prompt and context for `release-approval`.
  Output: latest-execution inspector including `branch.yaml`, `timeline.md`, and prior review evidence inputs.

- [ ] `H-RA-02` Status: `todo`
  Create the first structured release evidence pack.
  Output: bundled review verdict, changed files, validation results, release risk summary, and supporting artifact links.

- [ ] `P-RA-01` Status: `todo`
  Define release-approval eligibility and evidence policy.
  Output: explicit rules for what must exist before release approval can run or be approved.

- [ ] `P-RA-02` Status: `todo`
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

## Immediate Next Slice

If work starts now, take these tasks first:

1. `H-SHARED-01`
2. `H-SHARED-02`
3. `H-SHARED-03`
4. `H-REF-01`
5. `P-IMP-01`
