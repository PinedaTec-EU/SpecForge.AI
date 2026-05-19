# SpecForge · Harness Engineering Checklist

Last reviewed: 2026-05-18.

This document is the canonical internal checklist for SpecForge's harness-engineering posture.

It serves two purposes:

- capture which harness capabilities SpecForge already has today, with evidence grounded in the current runtime and documentation
- track the capabilities still missing before SpecForge becomes a stronger governed harness for AI-assisted software delivery

## Definition

In SpecForge terms, harness engineering is the engineering of the control layer around model execution: workflow, state, tools, permissions, evaluations, evidence, and policy.

The point is not to ask only whether a model can produce an answer. The point is to ensure the answer is produced inside a governed runtime with persisted truth, explicit checkpoints, auditable transitions, and enforceable operational boundaries.

Positioning sentence:

- SpecForge is a governed harness for AI-assisted software delivery.

## Current Posture

- [x] Workflow governance is explicit and deterministic.
  Evidence: the canonical workflow defines ordered phases, checkpoints, regression paths, and operational invariants in [workflow-canonico-fase-1.md](workflow-canonico-fase-1.md).
- [x] Repository-local artifacts remain the source of truth.
  Evidence: runtime and product documents keep `.specs/` as the persisted workflow state and artifact boundary in [runtime-and-persistence.md](runtime-and-persistence.md) and [product-vision.md](product-vision.md).
- [x] Workflow mutation is constrained behind an MCP boundary.
  Evidence: non-VS Code clients are expected to use `specforge_query`, `specforge_action`, `specforge_prompts`, and `open_workflow_portal` instead of manually editing workflow files, as documented in [runtime-and-persistence.md](runtime-and-persistence.md) and [architecture.md](architecture.md).
- [x] Human checkpoints and approvals are first-class controls.
  Evidence: `spec` and `release-approval` are mandatory approval points in [workflow-canonico-fase-1.md](workflow-canonico-fase-1.md).
- [x] Audit timeline and persisted phase artifacts already exist.
  Evidence: user stories persist `state.yaml`, `runtime.yaml`, `branch.yaml`, `timeline.md`, and phase artifacts under `.specs/us/...` as described in [runtime-and-persistence.md](runtime-and-persistence.md).
- [x] Regression, rewind, restart, and reopen semantics already exist.
  Evidence: the current workflow and product posture explicitly support regression and safe restart, with rewind/reopen called out across [README.md](../README.md), [workflow-canonico-fase-1.md](workflow-canonico-fase-1.md), and [runtime-and-persistence.md](runtime-and-persistence.md).
- [x] Model and agent routing are configurable runtime concepts.
  Evidence: model profiles, agent profiles, repository access, subagents, and phase routing are already documented in [model-configuration.md](model-configuration.md).
- [x] Browser and non-IDE operation already exist.
  Evidence: the self-contained workflow portal and packaged MCP/plugin runtime are current product surfaces in [README.md](../README.md), [architecture.md](architecture.md), and [runtime-and-persistence.md](runtime-and-persistence.md).

## Target Harness Capabilities

### Execution Harness

- [x] Phase execution already runs through a governed workflow instead of open-ended chat continuation.
- [x] The runtime already persists workflow state, phase outputs, and branch metadata in repository-local artifacts.
- [x] Model-backed execution already supports phase routing, provider identity, and repository-access levels.
- [x] Phase-local subagent orchestration already exists for selected phases.
  Current note: this is available for `technical-design` and `review`, but not yet as a broad, policy-driven orchestration surface.
- [ ] Each phase should have an explicit execution envelope that declares allowed tools, writable scope, repository boundaries, and time or cost budget.
- [ ] The runtime should expose effective execution context per phase, including which artifacts, prompts, settings, and context files were actually injected.
- [ ] The runtime should support durable stop, retry, and resume semantics beyond best-effort execution interruption.
- [ ] The runtime should support phase-level environment requirements and preflight checks before execution begins.

### Evaluation And Gates

- [x] The workflow already has mandatory human gates where risk meaningfully changes.
- [x] Review already produces an explicit `pass` or `fail` verdict with correction targeting.
- [x] Review and refinement already have configurable tolerance and evidence-policy settings.
- [x] Spec generation already requires structured criticism and reconstruction before baseline approval.
- [ ] Each phase should support an explicit eval pack with declared checks, pass criteria, and failure outputs.
- [ ] Review should evolve from a single artifact verdict into reusable review oracles that can be applied consistently across user stories.
- [ ] Technical design should support explicit design-quality gates before implementation when a repository or policy requires them.
- [ ] Release approval should support evidence-pack requirements that bundle review verdict, changed files, validation results, and release-risk summary.
- [ ] The runtime should expose machine-readable gate outcomes in addition to human-readable markdown artifacts.

### Policy And Permissions

- [x] Agent repository access is already modeled and documented as `none`, `read`, or `read-write`.
- [x] Product vision already defines Central policy locks for prompt overrides, evidence policy, provider routing, PR requirements, and approval restrictions.
- [ ] Repository-access levels should be enforced as part of a broader execution-permissions model rather than only as a routing property.
- [ ] Phase policies should support mandatory tool restrictions, writable-path restrictions, and forbidden mutation zones.
- [ ] The runtime should support policy-driven phase eligibility rules, such as requiring tests, review evidence, or PR metadata before advancing.
- [ ] The runtime should make effective policy visible per phase so an operator can see why an action is blocked or downgraded.
- [ ] Local runtimes should persist the effective policy snapshot that governed each executed phase for later audit.

### Observability And Evidence

- [x] Timeline entries and persisted artifacts already provide a baseline audit trail.
- [x] Skill usage reporting already exists as execution metadata when enabled.
- [x] Provider identity and routing metadata are already part of the model-configuration surface.
- [ ] The runtime should expose an effective-prompt and effective-context inspector so operators can compare template, override, and final composed input.
- [ ] Prompt drift and policy drift should be visible as first-class operational warnings, not only as implicit file differences.
- [ ] Phase execution should produce a structured evidence record with actor, inputs, outputs, tools used, settings, and blocking reason when applicable.
- [ ] The runtime should surface phase metrics such as attempt count, lead time, retry history, and blocked duration in operator views.
- [ ] The product should generate exportable evidence packs for PR descriptions, release review, or external audit.

### Reusable Harness Profiles

- [x] The current runtime already has configurable refinement tolerance, review tolerance, review evidence policy, model profiles, and agent profiles.
  Current note: these settings exist today, but they are still individual knobs rather than reusable harness packages.
- [ ] SpecForge should provide named harness profiles such as `strict`, `balanced`, and `regulated` that bundle phase behavior, evidence requirements, and policy defaults.
- [ ] Harness profiles should support repository-local inheritance with explicit override rules and visible lock status.
- [ ] Harness profiles should be selectable during repository bootstrap so a team starts from a coherent operating model instead of many low-level settings.
- [ ] Harness profiles should be portable across repositories through packaged distribution, not copied manually by prompt text.

### SpecForge Central Governance

- [x] The product direction already defines SpecForge Central as a governance control plane across repositories.
- [x] Managed repositories, readiness checks, policy locks, drift detection, and decision queues are already documented target capabilities.
- [ ] Central should expose portfolio-wide harness compliance, not only workflow status, across managed repositories.
- [ ] Central should let platform owners publish harness policies and reusable harness profiles to connected repositories.
- [ ] Central should track which repositories are operating under stale runtime, stale profile, or stale policy conditions.
- [ ] Central should provide queueing and filtering for harness failures such as blocked approvals, failed review gates, missing evidence packs, or policy violations.
- [ ] Central should provide audit exports that show which harness rules governed each completed workflow.

## Adoption Sequence

1. [x] Formalize the current harness baseline in this checklist.
2. [ ] Add explicit eval packs and reusable gate outputs.
3. [ ] Add policy-driven execution envelopes and stronger permission enforcement.
4. [ ] Add effective-context inspection, drift visibility, metrics, and evidence exports.
5. [ ] Add reusable harness profiles with repository bootstrap support.
6. [ ] Extend harness governance into SpecForge Central.

## Notes

- This checklist is intentionally broader than the current MVP roadmap. It captures the control surface around model execution, not only the next feature queue.
- Checked items mean the capability already exists in a documented or implemented form. They do not mean the capability is fully mature or enterprise-complete.
- Unchecked items should be treated as candidates for future user stories, roadmap entries, or Central policy work rather than informal ideas.
