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

### Technical Design

First phase that should materially benefit from graph-backed narrowing.

Expected usage:

- seed from refinement graph scope request
- derive or load impact graph
- use bounded follow-up queries when design ambiguity remains

### Implementation

Should use the approved design plus impact graph to reduce broad file reading.

Expected usage:

- target file narrowing
- neighbor discovery
- test adjacency discovery
- graph refresh only if allowed by feature flags and required by changed scope

### Review

Should use final impact graph and graph-derived adjacency to inspect likely regression surface more precisely.

Expected usage:

- changed file neighborhood
- downstream consumers
- nearby tests
- graph delta when available later

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
   - global graph reuse
   - impact graph reuse
   - refresh versus rebuild

5. failure fallback policy by phase

6. cost attribution model and ledger schema

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
