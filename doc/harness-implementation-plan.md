# SpecForge · Harness Implementation Plan

Last reviewed: 2026-05-25.

This document tracks the implementation plan derived from the harness-engineering baseline in [harness-engineering-checklist.md](harness-engineering-checklist.md).

Execution tasks live in [harness-phase-tasks.md](harness-phase-tasks.md).

Active non-bug backlog tracking now lives in GitHub Issues. This document keeps the harness rollout framing, rationale, and design context; it is not the canonical execution ledger.

Its purpose is operational:

- keep the execution plan visible in one place
- prioritize quick wins first
- make cost and dependency tradeoffs explicit
- provide a simple tracking surface for progress and decisions

## Planning Rules

- Prefer quick wins that improve operator visibility and control without large architecture changes.
- Do not optimize for headline percentage. Optimize for real control, auditability, and operator trust.
- Avoid starting Central-specific work until local harness capabilities justify it.
- Treat each wave as decision-gated: finish scope review before opening the next one.
- Treat the semantic code-graph as a first-class harness accelerator, not as an optional side note, because it directly affects token cost, context selection quality, and auditability for `technical-design`, `implementation`, and `review`.

## Canonical Near-Term Issues

- Wave 1: [#42](https://github.com/PinedaTec-EU/SpecForge.AI/issues/42) `SFF-042: Effective prompt and context inspection`
- Wave 2: [#48](https://github.com/PinedaTec-EU/SpecForge.AI/issues/48) `SFT-048: Structured execution evidence substrate`
- Wave 3: [#49](https://github.com/PinedaTec-EU/SpecForge.AI/issues/49) `SFF-049: Phase metrics and operator visibility`
- Wave 4: [#50](https://github.com/PinedaTec-EU/SpecForge.AI/issues/50) `SFF-050: Policy visibility and explainability`
- Waves 5-8 remain planning context here and have not been promoted to canonical GitHub issues yet.

## Wave Plan

### Graph Delivery Track · Semantic Code Graph

- Status: `planned`
- Priority: `highest`
- Cost: `M-L`
- Expected value: `very high`
- Why now: this is the main lever for reducing token-heavy file discovery and improving context precision in later phases.

Scope:

- settle the graph architecture before implementing graph tooling and runtime integration;
- formalize `global graph` and `impact graph` persistence;
- define operator controls and feature flags for graph usage and graph mutation;
- expose graph build, refresh, status, and bounded query operations through MCP and CLI;
- make graph build cost auditable and reviewable;
- feed `technical-design`, `implementation`, and `review` from graph-guided scope instead of broad repository reading.

Primary outcomes:

- models stop exploring the repository blindly in expensive phases;
- graph-backed context selection becomes inspectable and optional by policy;
- expensive rebuilds become traceable, confirmable, and measurable;
- local or on-prem model capacity can be preferred for graph creation when available.

Mandatory controls:

- feature flag: `use semantic graph when available`
- feature flag: `allow graph build or refresh for touched user-story scope`
- confirmation gate before rebuilding the global graph from zero when one already exists
- explicit overwrite semantics for full rebuild versus incremental refresh

Required operator surfaces:

- settings panel switches for graph usage and graph mutation
- workflow-visible graph status and freshness
- MCP and CLI entrypoints to create the graph from zero, refresh it, materialize an impact graph, and query it
- receipt or audit record of who triggered graph work, why, when, with which model, and at what cost

Required observability:

- token usage when graph construction or graph-assisted queries use model calls
- elapsed time and throughput for graph build and refresh
- graph size and freshness metadata
- overwrite, reuse, refresh, and fallback outcomes

Recommended sequencing:

1. `H-SHARED-07`
2. `G-DESIGN-01`
3. `G-DESIGN-02`
4. `G-DESIGN-03`
5. `G-DESIGN-04`
6. `H-SHARED-08`
7. `H-SHARED-09`
8. `H-SHARED-10`
9. `H-TD-03`
10. `H-TD-04`
11. graph-guided `implementation`
12. graph-guided `review`

Model strategy direction:

- prefer local or on-prem configured model capacity first when graph extraction requires model assistance
- do not assume embeddings are automatically required; confirm extractor needs before locking architecture
- if embeddings are needed, keep the embedding model explicitly configurable and independently auditable from phase-execution models
- separate graph-construction cost accounting from normal phase-execution cost accounting
- do not implement graph MCP/CLI mutation tools as if the builder were settled before the design track closes
- current recommended first-cut builder posture is `.NET-first`, `Roslyn/SCIP`-first for C#, with parser-based fallback and no embeddings in v1

### Wave 1 · Effective Prompt And Context Inspection

- Status: `planned`
- Priority: `highest`
- Cost: `S-M`
- Expected value: `high`
- Why first: it improves inspectability immediately and reduces guesswork before changing deeper runtime behavior

Scope:

- expose the effective prompt per phase
- expose the effective runtime context per phase
- distinguish embedded template, repo override, and final composed input
- make the result inspectable from operator surfaces

Primary outcomes:

- an operator can see what the phase actually ran with
- prompt drift becomes inspectable instead of inferred
- later eval and policy work has a visible substrate

Open tracking:

- Canonical issue: [#42](https://github.com/PinedaTec-EU/SpecForge.AI/issues/42)
- Planning questions:
  - define the minimum effective-prompt data shape
  - define the minimum effective-context data shape
  - decide where this is surfaced first: portal, VS Code, MCP, or multiple
  - implement the first operator-facing view

Current validation against the codebase:

- [x] Effective prompt composition already exists for normal phase execution in the OpenAI-compatible provider.
- [x] The composed runtime prompt already embeds phase context, previous artifacts, repository context files, and phase-specific rules.
- [x] Prompt override drift warnings already exist through embedded-template hash comparison.
- [x] Per-execution receipts already persist input and output manifests plus execution metadata.
- [x] Workflow DTOs already expose prompt file paths and timeline execution metadata to operator surfaces.
- [ ] The runtime does not persist the effective prompt text for standard phase execution.
- [ ] The runtime does not persist the effective context as an inspectable structured object for standard phase execution.
- [ ] Operator surfaces do not expose a first-class effective prompt or effective context inspector.
- [ ] The current implementation is asymmetric: artifact operation prompts are preserved as text, but normal phase execution prompts are not.

Validated current phase coverage:

- [x] `refinement`: effective prompt built and phase-specific refinement tolerance and contract injected.
- [x] `spec`: effective prompt built and spec-specific contract injected.
- [x] `technical-design`: effective prompt built and design-specific planning expectations injected.
- [x] `implementation`: effective prompt built and review-learning rules injected when applicable.
- [x] `review`: effective prompt built and validation checklist plus evidence-policy rules injected.
- [x] `release-approval`: effective prompt built and release-approval contract injected.
- [x] `pr-preparation`: effective prompt built and PR artifact contract injected.
- [x] `auto-refinement answers`: separate effective prompt built with its own system and task layers.
- [x] `approval-answer suggestion`: dedicated prompt flow exists for spec approval questions.
- [x] `capture`: this is the workflow entry phase and does not require a model-backed prompt pipeline.

Validated current context assembly:

- [x] All model-backed phases receive `us.md`.
- [x] All model-backed phases may receive `refinement.md` when present.
- [x] Standard phase execution receives previous artifacts through `BuildPreviousArtifactMap(...)`.
- [x] Standard phase execution receives repository context files from `context/`.
- [x] `review` additionally injects implementation evidence markdown into context.
- [x] `release-approval` and `pr-preparation` additionally inject `branch.yaml` and `timeline.md`.
- [ ] Attachments under `attachments/` are visible in workflow DTOs but are not part of the normal model runtime context by default.

Validated current persistence and exposure:

- [x] `PhaseExecutionReceipt` persists input manifest, output manifest, token usage, and execution metadata under `execution-receipts/`.
- [x] Timeline execution metadata persists provider, model, profile, agent, warnings, skills, hashes, and receipt path.
- [x] Workflow DTOs expose execute and approve prompt paths per phase when prompt files exist.
- [ ] Receipts persist hashes and manifests, not the effective prompt text itself.
- [ ] Timeline metadata persists hashes and warnings, not the effective prompt text itself.
- [ ] Current portal and MCP surfaces expose prompt paths and execution metadata, but not a unified effective-prompt/effective-context view.

Recommended first cut:

- persist effective prompt and effective context first in the execution receipt, not in the timeline markdown
- expose the new data through workflow detail DTOs and MCP before building a richer portal inspector
- the first operator-facing inspector must let an operator visualize the effective prompt actually sent for a phase execution
- keep the first scope read-only and phase-execution focused; do not mix it yet with policy visibility or eval semantics

Minimum contract proposal:

- `effectivePrompt`:
  - `systemPrompt`
  - `userPrompt`
  - `warnings`
  - `sourcePromptPaths`
- `effectiveContext`:
  - `userStoryPath`
  - `previousArtifacts[]` with `phaseId`, `path`, `sha256`
  - `contextFiles[]` with `path`, `sha256`
  - `currentArtifact` when operating on an existing artifact
  - `operationPromptSha256`
  - `workspaceGitHeadSha`

Why this cut:

- it reuses data already assembled in `PhaseExecutionContext` and `BuildEffectivePromptAsync(...)`
- it avoids timeline bloat and keeps markdown readable
- it preserves a clean separation: receipt for detailed truth, timeline for summarized trace

Known code anchors for implementation:

- `src/SpecForge.OpenAICompatible/OpenAiCompatiblePhaseExecutionProvider.cs`
  - `BuildEffectivePromptAsync(...)` already materializes the composed prompt
  - native CLI execution support may now also live in `src/SpecForge.OpenAICompatible/OpenAiCompatibleNativeCli.cs`
- `src/SpecForge.Domain/Application/PhaseExecutionContext.cs`
  - already defines the runtime context ingredients that should become inspectable
- `src/SpecForge.Domain/Application/WorkflowRunner.cs`
  - `MaterializePhaseArtifactAsync(...)` already builds the input manifest and persists the execution receipt
- `src/SpecForge.Domain/Application/PhaseExecutionReceipt.cs`
  - current receipt contract is the natural storage point for the first cut
- `src/SpecForge.Domain/Application/SpecForgeApplicationService.cs`
  - current workflow DTO building is the likely first exposure path for MCP and portal consumers
  - supporting application responsibilities may be extracted into adjacent helpers under `src/SpecForge.Domain/Application/`

Suggested delivery slices:

- Slice 1A: Receipt enrichment
  - extend the receipt contract with effective prompt and effective context payloads
  - persist them only for normal model-backed phase execution
  - keep auto-refinement answers as a follow-up unless the same abstraction falls out naturally
- Slice 1B: Read model exposure
  - expose the latest receipt-backed effective prompt and context in workflow detail responses
  - keep the first API shape phase-local and latest-execution-only
- Slice 1C: Operator surface
  - show a compact phase inspector with prompt, warnings, and included inputs
  - start with a plain text or collapsible raw view before investing in richer diff UX
- Slice 1D: Drift usability
  - surface prompt override warnings beside the effective prompt, not only in timeline metadata
  - reserve prompt-to-prompt diffing for a later iteration

Explicit non-goals for Wave 1:

- no cross-execution comparison UI
- no policy snapshot visualization
- no attachment ingestion redesign
- no timeline markdown expansion beyond linking the richer receipt when needed

### Wave 2 · Structured Execution Evidence

- Status: `planned`
- Priority: `highest`
- Cost: `M`
- Expected value: `high`
- Depends on: Wave 1 is helpful but not strictly required

Scope:

- produce a structured evidence record per phase execution
- capture actor, inputs, outputs, settings, tools used, and blocking reason when applicable
- align structured evidence with existing markdown artifacts and timeline

Primary outcomes:

- execution evidence becomes queryable and comparable
- audit moves beyond free-form artifact reading
- later exports and eval packs have a reusable evidence substrate

Open tracking:

- Canonical issue: [#48](https://github.com/PinedaTec-EU/SpecForge.AI/issues/48)
- Planning questions:
  - define the execution evidence record contract
  - decide storage model relative to `timeline.md`, `runtime.yaml`, and phase artifacts
  - define minimum MCP and UI exposure
  - implement the first evidence capture path

### Wave 3 · Basic Phase Metrics And Operator Visibility

- Status: `planned`
- Priority: `high`
- Cost: `S-M`
- Expected value: `high`
- Depends on: Wave 2 preferred

Scope:

- expose attempt count, lead time, retries, blocked duration, and waiting-user duration
- show these metrics in the operator-facing workflow surfaces
- keep metrics simple and derived from persisted facts when possible

Primary outcomes:

- operators can spot unstable or slow phases
- harness maturity becomes measurable with real operational data
- the product gains a practical visibility layer before heavier policy work

Open tracking:

- Canonical issue: [#49](https://github.com/PinedaTec-EU/SpecForge.AI/issues/49)
- Planning questions:
  - lock the first metric set
  - define derivation rules from persisted state and timeline
  - decide which metrics are phase-local vs workflow-wide
  - implement the first surface rendering

### Wave 4 · Policy Visibility And Explainability

- Status: `planned`
- Priority: `high`
- Cost: `M`
- Expected value: `high`
- Depends on: Wave 1 and Wave 2 recommended

Scope:

- expose the effective policy that governs a phase
- show why an action is blocked, downgraded, or requires extra evidence
- surface repository access, evidence policy, and relevant locks in operator terms

Primary outcomes:

- operators understand the control surface without reading source code
- policy stops feeling implicit
- later enforcement work becomes reviewable and safer to introduce

Open tracking:

- Canonical issue: [#50](https://github.com/PinedaTec-EU/SpecForge.AI/issues/50)
- Planning questions:
  - define the minimum effective-policy view
  - list the first policy dimensions to surface
  - align blocking reasons with policy explanations
  - implement the first operator-facing policy summary

### Wave 5 · Eval Packs And Reusable Gate Outputs

- Status: `planned`
- Priority: `medium`
- Cost: `M-L`
- Expected value: `very high`
- Depends on: Waves 1-4 recommended

Scope:

- define explicit eval packs per phase
- define pass criteria and failure outputs
- move review and gate semantics toward reusable, structured checks

Primary outcomes:

- gates become more deterministic and reusable
- review stops depending only on free-form markdown judgment
- phase quality becomes more portable across repositories

Open tracking:

- [ ] Define eval-pack shape
- [ ] Decide which phases get eval packs first
- [ ] Define machine-readable gate outputs
- [ ] Implement the first reusable phase eval

### Wave 6 · Execution Envelopes And Stronger Permission Enforcement

- Status: `planned`
- Priority: `medium`
- Cost: `L`
- Expected value: `very high`
- Depends on: Waves 1-5 recommended

Scope:

- define per-phase execution envelopes
- formalize allowed tools, writable scope, repository boundaries, and budget
- strengthen permission enforcement beyond routing metadata

Primary outcomes:

- execution boundaries become explicit and auditable
- repository mutation risk is reduced
- SpecForge starts behaving like a true productized engineering harness

Open tracking:

- [ ] Define execution-envelope contract
- [ ] Decide enforcement boundary: domain, runner, provider, or layered
- [ ] Define minimum budget model
- [ ] Implement first enforced envelope

### Wave 7 · Reusable Harness Profiles

- Status: `planned`
- Priority: `medium`
- Cost: `M`
- Expected value: `high`
- Depends on: Waves 4-6 recommended

Scope:

- package current low-level settings into named harness profiles
- define profile inheritance, override visibility, and lock behavior
- make bootstrap selection coherent for repositories

Primary outcomes:

- repositories adopt a harness mode instead of manually tuning many knobs
- policy and execution defaults become portable
- Central later gets a meaningful unit to distribute and audit

Open tracking:

- [ ] Define profile model
- [ ] Choose initial built-in profiles
- [ ] Define override and lock rules
- [ ] Implement bootstrap/profile selection path

### Wave 8 · SpecForge Central Harness Governance

- Status: `planned`
- Priority: `later`
- Cost: `L-XL`
- Expected value: `strategic`
- Depends on: Waves 4-7 strongly recommended

Scope:

- publish harness policies and profiles from Central
- expose portfolio-wide harness compliance
- track stale runtime, profile, and policy conditions
- add queueing and export for harness failures and audit

Primary outcomes:

- Central becomes a real harness control plane, not only a workflow catalog
- platform owners gain enforceable cross-repository governance
- local repository truth remains intact while governance scales outward

Open tracking:

- [ ] Define Central-local harness contract
- [ ] Define compliance states and stale conditions
- [ ] Define queue categories for harness failures
- [ ] Define first audit export surface

## Decision Log

- 2026-05-18: Prefer quick wins first instead of optimizing for a higher harness-completion percentage.
- 2026-05-18: Keep this plan separate from the baseline checklist so one document tracks posture and the other tracks execution.

## Notes

- This document is intentionally a control surface, not a full technical design.
- Each wave should later be expanded into a concrete implementation plan before development starts.
- Cost labels are rough order-of-magnitude estimates for planning only:
  - `S`: small
  - `M`: medium
  - `L`: large
  - `XL`: very large
