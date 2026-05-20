# SpecForge · Semantic Code Graph Design

Last reviewed: 2026-05-21.

## Purpose

Define the semantic code-graph as a first-class subsystem of SpecForge before implementing its MCP, CLI, portal, and phase-runtime integration.

This document exists to prevent premature implementation of:

- graph tools with no settled execution semantics;
- feature flags with unclear behavior;
- audit and cost ledgers with no stable event model;
- graph-assisted `technical-design`, `implementation`, and `review` flows that cannot yet explain what they consumed and why.

## Problem Statement

Today, later workflow phases still need too much direct repository reading to find relevant files, symbols, tests, and dependencies.

That creates three problems:

1. token-heavy discovery
2. noisy or overly broad context injection
3. weak auditability around why a file or symbol was considered relevant

The semantic code-graph should solve those problems by making repository structure queryable and reusable across user stories.

## Design Goals

- reduce token consumption in `technical-design`, `implementation`, and `review`
- reduce blind repository exploration
- keep graph usage optional and inspectable
- keep graph mutation separately controllable from graph consumption
- support local or on-prem graph construction when available
- make expensive graph builds auditable, confirmable, and measurable
- degrade cleanly when graph data is missing, stale, disabled, or failed

## Non Goals

- do not require graph availability for all workflows
- do not hard-couple graph construction to one model provider
- do not assume embeddings are mandatory
- do not let graph artifacts silently bypass existing phase policy and evidence controls
- do not let a global rebuild overwrite existing graph state without confirmation

## Core Concepts

### Global Graph

Repository-wide semantic graph reusable across user stories.

Expected traits:

- long-lived
- incrementally refreshable
- build-costly relative to normal phase execution
- source of truth for graph-backed impact derivation

### Impact Graph

Per-user-story scoped graph derived from the global graph when available, or from local fallback analysis when not.

Expected traits:

- phase-oriented
- smaller than the global graph
- tied to a concrete workflow context
- consumable by `technical-design`, `implementation`, and `review`

### Fallback Mini Graph Pack

Compact non-global fallback when graph usage is disabled, missing, stale beyond tolerance, or failed.

Expected traits:

- fast to assemble
- intentionally lossy
- enough to keep workflow phases moving without pretending full graph quality

## Proposed Architecture

The graph subsystem should be split into four logical layers.

1. `graph contract layer`
   - artifact schemas
   - status model
   - freshness model
   - event model

2. `graph build layer`
   - create global graph from zero
   - refresh global graph incrementally
   - derive user-story impact graph

3. `graph query layer`
   - bounded graph queries
   - query provenance and explainability
   - source graph declaration in responses

4. `graph orchestration layer`
   - feature flags
   - CLI and MCP surfaces
   - portal visibility
   - phase-runtime consumption rules

## Design Decisions Closed

The following design decisions are now recommended as the first implementation baseline.

### Decision 1: Implementation posture is `.NET-first`

The architecture should remain extensible, but the first builder should optimize for this repository class instead of pretending equal maturity across stacks.

Reasoning:

- this repository is `.NET` heavy
- value comes faster from high-quality C# support than from early multi-stack abstraction
- premature cross-stack generalization would likely weaken correctness and slow delivery

### Decision 2: Builder strategy is deterministic-first, not LLM-first

The graph should not depend on embeddings or free-form model inference to exist.

Reasoning:

- the main goal is to reduce token-heavy repository exploration
- a graph that itself depends on high token consumption undermines that goal
- structural graph extraction is more explainable, cheaper, and easier to govern

### Decision 3: First builder is hybrid deterministic

Recommended builder order:

1. `Roslyn/SCIP`-backed extraction for C# when available
2. parser-based structural extraction as fallback
3. optional local or on-prem model assistance later only if deterministic extraction leaves proven gaps

This means the first SpecForge graph builder should be:

- `.NET-first`
- `Roslyn/SCIP-first` for C#
- parser fallback for incomplete or unsupported areas

### Decision 4: Embeddings are deferred

Embeddings are not part of the first baseline architecture.

They may be added later only if:

- deterministic graph quality is insufficient for real workflow questions
- the benefit is measurable
- configuration, cost, and audit surfaces are ready

### Decision 5: External graph databases are deferred

The first implementation should not require an external graph database such as Neo4j or Kuzu as an architectural dependency.

Recommended v1 persistence posture:

- lifecycle artifacts and metadata remain under `.specs/cache/graphs/`
- audit and ledger remain file-backed
- SQLite may be used internally as an implementation detail if needed for fast local queries

Reasoning:

- lower operational complexity
- better fit with current SpecForge artifact and receipt model
- easier local portability and auditability

### Decision 6: Storage and exchange formats are separated

The local storage implementation and the Central exchange contract must not be treated as the same thing.

Recommended posture:

- local persistence may evolve for performance
- exchange with SpecForge Central should stay stable and portable
- Central should depend on graph snapshot and ledger contracts, not on the local storage engine

## Artifact Model

Artifacts already anchored by lifecycle contract:

- `.specs/cache/graphs/global-graph.json`
- `.specs/cache/graphs/global-graph.meta.json`
- `.specs/cache/graphs/graph-build-log.jsonl`
- `.specs/cache/graphs/graph-cost-ledger.json`
- `.specs/us/<US>/context/graph-scope-request.json`
- `.specs/us/<US>/context/impact-graph.json`
- `.specs/us/<US>/context/impact-graph.meta.json`
- `.specs/us/<US>/context/impact-summary.md`

### Global Graph Contents

Minimum expected content:

- files
- modules or packages
- symbols
- declarations
- references
- implementation edges
- call edges when derivable
- import/reference edges
- test adjacency when derivable

Optional later content:

- runtime flow edges
- configuration ownership edges
- cross-service protocol edges

### Impact Graph Contents

Minimum expected content:

- selected root files or symbols
- bounded neighboring files or symbols
- reason for inclusion
- source of inclusion:
  - graph derivation
  - fallback analysis
  - explicit operator seed
  - phase carry-forward

## Persistence Strategy

The persistence strategy should evolve in stages.

### V1: Fragmented JSON Artifacts

Recommended first cut:

- file-backed graph artifacts under `.specs/cache/graphs/`
- per-user-story graph artifacts under `.specs/us/<US>/context/`
- JSON as the canonical machine-readable contract
- Markdown only for operator summaries

This avoids a single oversized monolithic file while keeping the graph inspectable and easy to move between local runtime, MCP, CLI, portal, and future Central APIs.

### V2: Hybrid Local Store

If graph size or query cost becomes a real issue:

- SQLite may become the local operational store
- JSON remains the export and interchange contract
- Markdown remains the operator-facing summary format

In that model:

- local runtime optimizes for fast queries
- Central synchronization still receives stable JSON snapshots or event exports

### Storage Rule

For design purposes, treat:

- JSON as the canonical exchange format
- SQLite as an optional future local execution optimization
- external graph databases as out of scope for v1

## Build Modes

The design should support four modes.

### 1. Inspect Only

No graph mutation allowed.

Allowed:

- read status
- inspect metadata
- inspect freshness
- inspect prior build ledger
- inspect whether impact graph exists for a user story

### 2. Reuse Existing

Reuse a fresh enough global graph or impact graph without mutation.

### 3. Incremental Refresh

Refresh changed graph slices without replacing the full global graph.

Expected triggers:

- touched repository files
- explicit user or operator request
- stale metadata with refreshable source

### 4. Rebuild From Zero

Create a new global graph baseline from scratch and replace the current one.

Constraints:

- high-cost operation
- explicit actor attribution required
- explicit reason required
- explicit confirmation required if a global graph already exists

## Governance Model

The graph subsystem must behave like governed infrastructure, not like an opaque cache.

### Governance Principles

- graph consumption and graph mutation are separate permissions
- expensive mutation must be attributable and explainable
- overwrite of existing durable graph state must be explicit
- fallback behavior must be visible, not silent
- graph-assisted outputs must declare confidence boundaries when fallback or stale data was used

### Mutation Authority

The system should distinguish four authority levels.

1. `inspect`
   - may read status, freshness, metadata, and prior ledger events
   - may not mutate graph artifacts

2. `derive impact`
   - may derive or refresh per-user-story impact graph when mutation is allowed
   - may not rebuild global graph from zero

3. `refresh global`
   - may incrementally refresh global graph and dependent impact graphs
   - may not replace existing global graph baseline from zero without explicit overwrite confirmation

4. `rebuild global`
   - may request full replacement of global graph baseline
   - requires explicit reason and explicit overwrite confirmation

### Overwrite Confirmation Policy

If a global graph already exists, `rebuild from zero` must require:

- visible warning that existing graph state will be replaced
- explicit confirmation token or flag
- actor identity
- reason
- persisted audit event before or during execution

The system must record:

- whether overwrite was requested
- whether overwrite was confirmed
- whether overwrite actually occurred

### Human Versus Model Authority

For v1:

- a model may recommend refresh or rebuild
- a model may not silently replace existing global graph state
- rebuild-from-zero of an existing global graph should require human confirmation

## Freshness Model

Freshness must be metadata-driven and deterministic enough to explain.

### Freshness States

Both global graph and impact graph should report one of:

- `missing`
- `fresh`
- `stale-refreshable`
- `stale-rebuild-required`
- `incompatible`
- `failed`

### Global Graph Freshness Inputs

Minimum metadata inputs:

- graph build timestamp
- builder version or schema version
- repository root fingerprint
- repository HEAD when available
- selected extractor strategy
- selected model or enrichment profile when applicable

### Impact Graph Freshness Inputs

Minimum metadata inputs:

- impact graph build timestamp
- parent global graph identity or fingerprint when applicable
- `graph_scope_request` fingerprint
- touched file fingerprint when available
- selected derivation mode:
  - global graph derived
  - fallback derived

### Freshness Evaluation Rules

Recommended v1 posture:

- `missing` when artifact does not exist
- `fresh` when all required fingerprints are compatible and no refresh trigger is present
- `stale-refreshable` when inputs changed but incremental refresh is still semantically valid
- `stale-rebuild-required` when compatibility or baseline guarantees no longer hold
- `incompatible` when schema, builder family, or source assumptions no longer match
- `failed` when the last attempted build or refresh did not complete successfully

### Reuse Rules

Global graph may be reused when:

- state is `fresh`
- graph usage is enabled

Impact graph may be reused when:

- state is `fresh`
- graph usage is enabled
- current user-story scope still matches its scope fingerprint

### Refresh Rules

Incremental refresh is preferred over rebuild when:

- a graph exists
- state is `stale-refreshable`
- mutation is allowed for the caller

### Rebuild Rules

Rebuild-from-zero is required when:

- no global graph exists and creation is requested
- state is `stale-rebuild-required`
- state is `incompatible`
- operator explicitly chooses full replacement

## Failure And Fallback Policy

Failure must be explicit and phase-aware.

### Failure Classes

- `build_failed`
- `refresh_failed`
- `query_failed`
- `schema_incompatible`
- `scope_incompatible`
- `permission_denied`
- `confirmation_missing`

### Fallback Policy By Phase

#### Refinement

- graph failure must not block refinement completion
- refinement may still persist `graph_scope_request`

#### Technical Design

- may fall back to mini-graph pack when graph usage is disabled, missing, stale beyond allowed tolerance, or failed
- fallback use must be declared in evidence and query responses

#### Implementation

- may fall back to mini-graph pack or direct bounded repository inspection when graph usage is unavailable
- must not pretend graph-backed narrowing occurred when it did not

#### Review

- may fall back to mini-graph pack or direct bounded inspection
- review outputs must not present graph-assisted coverage as exhaustive when fallback mode was active

### Fallback Visibility Rule

Every graph-assisted phase or query response should declare:

- whether fallback was used
- why fallback was used
- what source was used instead

## Audit Event Model

Every graph mutation and every graph-relevant failure should be representable as an event.

### Event Families

- `graph.inspect`
- `graph.derive-impact.requested`
- `graph.derive-impact.completed`
- `graph.derive-impact.failed`
- `graph.refresh.requested`
- `graph.refresh.completed`
- `graph.refresh.failed`
- `graph.rebuild.requested`
- `graph.rebuild.confirmed`
- `graph.rebuild.completed`
- `graph.rebuild.failed`
- `graph.query.executed`
- `graph.query.failed`

### Required Event Fields

- `eventId`
- `timestamp`
- `eventFamily`
- `actor`
- `triggerSurface`
- `workspaceRoot`
- `usId` when applicable
- `phase` when applicable
- `reason`
- `requestedMode`
- `actualMode`
- `sourcePreference`
- `graphStateBefore`
- `graphStateAfter`
- `overwriteRequested`
- `overwriteConfirmed`
- `fallbackUsed`
- `fallbackReason`
- `builderStrategy`
- `modelProfile` when applicable
- `embeddingProfile` when applicable
- `latencyMs`
- `tokenUsage` when applicable
- `artifactsRead`
- `artifactsWritten`
- `warnings`
- `errorCode` when failed
- `errorSummary` when failed

### Cost Ledger Rule

The cost ledger should be derivable from events, but persisted as an operator-friendly summary surface.

Minimum ledger rollups:

- total builds
- total refreshes
- total rebuilds-from-zero
- total impact derivations
- token totals
- latency totals and averages
- last successful global graph build
- last failed graph mutation

### Query Audit Rule

Not every query needs the same weight as a rebuild, but graph queries that influence workflow phases should still emit traceable events with:

- actor
- phase
- query kind
- source graph used
- fallback used
- latency
- token usage when any

## Builder Strategy

The builder strategy is now constrained by the closed decisions above.

### Builder Inputs

- repository files
- language/runtime metadata
- project manifests
- optional parser outputs
- optional model assistance
- optional embeddings if the chosen extractor actually requires them

### First-Cut Builder Recommendation

The first builder should combine:

- `.sln` and `.csproj` discovery
- Roslyn or `scip-dotnet` extraction when available
- parser-based structural fallback for files or scopes not fully covered by the primary extractor
- deterministic relationship derivation for:
  - declarations
  - references
  - implementations
  - calls when derivable
  - imports/usings
  - adjacent tests

### Preferred Execution Order

1. `Roslyn/SCIP` extraction first for C#
2. parser-based structural fallback second
3. local or on-prem model assistance third when needed
4. approved remote model assistance only when local capacity is unavailable or insufficient

### Embedding Posture

Embeddings are not assumed mandatory.

For v1:

- embeddings are explicitly out of scope
- no builder contract should require them

If later required:

- embedding model must be explicitly configurable
- embedding cost must be tracked separately
- embedding artifacts must be attributable to a concrete build event

## Query Model

Bounded graph queries must be explicit and phase-safe.

First expected query families:

- `status`
- `explain freshness`
- `derive impact graph`
- `neighbors of file or symbol`
- `implementers of symbol`
- `callers of symbol`
- `tests adjacent to file or symbol`
- `why is this file included`

This first query family is intentionally structural and explainable.

Queries such as full functional impact inference, semantic intent clustering, or embedding-led similarity should be treated as later extensions, not as baseline graph requirements.

### First-Cut Query Contract

Each query should declare:

- `queryKind`
- `scope`
- `actor`
- `phase` when applicable
- `reason`
- `sourcePreference`
- `maxDepth` when traversal applies
- `includeTests` when relevant

Each response should declare:

- `sourceGraphUsed`
- `freshnessState`
- `fallbackUsed`
- `includedNodes`
- `includedFiles`
- `includedEdges`
- `inclusionReasons`
- `warnings`
- `latency`
- `tokenUsage` when applicable

### Supported Query Semantics For V1

#### `status`

Answers:

- whether global graph exists
- whether impact graph exists for the requested user story
- freshness metadata
- last build or refresh metadata

Does not answer:

- semantic neighborhood questions
- impact scope

#### `explain freshness`

Answers:

- why a graph is fresh, stale, missing, or incompatible with current scope
- which fingerprints or timestamps were used

Does not answer:

- whether a rebuild should be automatically approved

#### `derive impact graph`

Answers:

- bounded scoped subgraph for a user story from current seeds
- whether derivation came from global graph or fallback analysis

Does not answer:

- arbitrary repository-wide semantic search

#### `neighbors(file|symbol)`

Answers:

- directly or depth-bounded adjacent files or symbols
- inclusion reasons per neighbor

Does not answer:

- free-form explanations of system behavior beyond returned structure

#### `implementers(symbol)`

Answers:

- concrete implementing types, methods, or handlers when derivable

#### `callers(symbol)` and `callees(symbol)`

Answers:

- direct or depth-bounded call relationships when derivable

Constraints:

- depth must stay bounded
- recursion or huge fan-out must be surfaced as warning, not silently expanded forever

#### `tests adjacent to file or symbol`

Answers:

- nearby test files, projects, or symbols with reason for adjacency

#### `why included(file|symbol)`

Answers:

- exact provenance for inclusion into an impact graph or query response
- whether inclusion came from seed, traversal, framework enrichment, fallback analysis, or carry-forward

### Explicitly Deferred Query Semantics

Not part of v1:

- embedding-led similarity search
- semantic “intent” clustering
- unconstrained natural-language graph reasoning
- full business-impact inference with no bounded structural basis

Every graph query should declare:

- actor
- phase when applicable
- query intent
- source graph used:
  - global graph
  - impact graph
  - fallback mini graph pack
- whether model tokens were consumed
- latency

## Phase Consumption Rules

### Refinement

Should not consume the full graph by default.

Responsibilities:

- produce `graph_scope_request`
- persist seeds, depth, and open questions
- identify whether later phases would benefit from graph use
- do not treat graph output as mandatory to let refinement complete

### Technical Design

First phase that should materially benefit from graph-backed narrowing.

Expected usage:

- seed from refinement graph scope request
- derive or load impact graph
- use bounded follow-up queries when design ambiguity remains

Allowed first-cut queries:

- `status`
- `derive impact graph`
- `neighbors`
- `implementers`
- `callers`
- `tests adjacent`
- `why included`

Expected output use:

- design context pack
- module and symbol scoping
- affected implementation surfaces
- candidate test surfaces

Must not do in v1:

- delegate broad repository exploration to graph queries with no seed discipline
- claim business semantics not grounded in returned structure

### Implementation

Should use the approved design plus impact graph to reduce broad file reading.

Expected usage:

- target file narrowing
- neighbor discovery
- test adjacency discovery
- graph refresh only if allowed by feature flags and required by changed scope

Allowed first-cut queries:

- `status`
- `neighbors`
- `callers`
- `callees`
- `tests adjacent`
- `why included`

Expected output use:

- narrowing editable file set
- finding nearby contracts and implementations
- finding likely tests before broad repository reads

Must not do in v1:

- auto-expand to repository-wide “impact” with no bounded scope
- silently refresh graph state when mutation is not allowed

### Review

Should use final impact graph and graph-derived adjacency to inspect likely regression surface more precisely.

Expected usage:

- changed file neighborhood
- downstream consumers
- nearby tests
- graph delta when available later

Allowed first-cut queries:

- `status`
- `neighbors`
- `callers`
- `callees`
- `tests adjacent`
- `why included`

Expected output use:

- identify probable regression surface
- identify likely missing validation zones
- justify why a file or downstream consumer is being considered

Must not do in v1:

- present graph-assisted findings as if they were exhaustive proof
- hide fallback mode when no valid graph was available

## Runtime Controls

Two minimum shared switches are required.

### `use semantic graph when available`

Meaning:

- later phases may consume an existing valid graph
- if disabled, phases must skip graph-backed context even when artifacts exist

### `allow graph build or refresh for touched US scope`

Meaning:

- workflow or operator actions may materialize or refresh graph artifacts for the current user story
- if disabled, runtime may only reuse existing graph artifacts

These controls must map consistently across:

- VS Code settings
- portal configuration
- CLI
- MCP

## Governance Rules

### Overwrite Protection

If a global graph already exists and a caller requests rebuild-from-zero:

- the system must warn
- the system must require explicit confirmation
- the resulting event must record that existing graph state was replaced

### Explainability

The system must be able to answer:

- why a graph build happened
- why an impact graph was created
- why a file or symbol entered the scoped context
- whether the answer came from global graph, impact graph, or fallback

### Failure Policy

Graph failure must not automatically block all workflow progress.

Default fallback posture:

- preserve audit event
- return explicit failure status
- allow fallback mini graph pack when phase policy permits

## Audit And Cost Model

Every graph mutation event should emit a build event and contribute to the cost ledger.

Required event fields:

- event id
- timestamp
- actor
- trigger surface:
  - portal
  - CLI
  - MCP
  - workflow runtime
- requested mode:
  - inspect
  - reuse
  - refresh
  - rebuild-from-zero
- actual mode executed
- reason
- source graph state before execution
- result graph state after execution
- reused or replaced
- affected user story when applicable
- selected builder strategy
- selected model profile when applicable
- embedding profile when applicable
- token usage when applicable
- latency
- throughput or files processed when derivable
- artifact outputs
- warnings
- failure summary when failed

## Freshness And Invalidation

This still needs implementation, but the design stance should be:

- freshness is metadata-driven, not guessed from file existence alone
- global graph freshness and impact graph freshness are related but independent
- an impact graph may be stale because its user-story seeds changed even if the global graph is still fresh

Minimum freshness inputs:

- graph build time
- repository HEAD or equivalent source fingerprint
- touched files fingerprint when available
- graph scope request fingerprint for impact graphs

## Decision Gates Before Implementation

The following decisions must be explicitly closed before full implementation of graph tools and runtime integration:

1. builder architecture
   - closed recommendation: `Roslyn/SCIP + parser fallback`, deterministic-first

2. graph schema minimum viable fields

3. supported query families for first implementation

4. freshness heuristics for:
   - closed recommendation: metadata-driven `missing|fresh|stale-refreshable|stale-rebuild-required|incompatible|failed`
   - global graph reuse
   - impact graph reuse
   - refresh versus rebuild

5. failure fallback policy by phase
   - closed recommendation: explicit fallback visibility, no silent graph substitution

6. cost attribution model and ledger schema
   - closed recommendation: event-driven ledger with persisted operator rollups

## Recommended Implementation Sequence

1. design approval of this graph architecture
2. `H-SHARED-08`
   - graph MCP/CLI tool family over settled status and operation semantics
3. `H-SHARED-09`
   - runtime controls and settings mapping
4. `H-SHARED-10`
   - audit and cost ledger contracts
5. `H-TD-03`
   - graph-backed design context pack
6. `H-TD-04`
   - bounded design query evidence
7. graph-guided `implementation`
8. graph-guided `review`

## Open Questions

- which language/runtime extractors are mandatory for the first builder
- whether the first implementation supports only `.NET` well or aims cross-stack immediately
- whether graph storage stays pure JSON initially or needs chunked artifacts early
- whether impact graph derivation is fully deterministic in the first cut
- whether graph queries can stay local-only at first or need model assistance immediately
