# SpecForge.AI · Execution Tool Packaging

Last reviewed: 2026-05-26.

This document defines the product packaging boundary for model-facing execution tools.

The goal is to keep SpecForge.AI useful as a public open-source local runtime while reserving legitimate organization-grade value for SpecForge Central.

## Packaging Principle

SpecForge OSS must not feel artificially crippled.

The open-source runtime should remain fully useful for:

- governed workflow execution
- repository-local truth
- local repository context retrieval
- local operator inspection and evidence
- local execution tools that do not require shared secrets, shared indexes, or cross-repository governance

SpecForge Central should monetize organization-grade capabilities instead of basic tool calling as a concept.

Central value should come from:

- cross-repository governance
- shared secrets and connector management
- organization-wide policies and budgets
- shared retrieval infrastructure
- cross-repository knowledge and code intelligence
- enterprise audit, compliance, and approval flows

## Packaging Tiers

### OSS Local

Capabilities that should remain usable in the public local runtime:

- read files from the current repository
- search text in the current repository
- inspect `.specs/**` and local workflow artifacts
- inspect local Git state
- inspect local build, test, and runtime outputs
- assemble prompt context from local workspace evidence
- optionally run repo-local semantic search or repo-local code graph when no shared infrastructure is required

### Compatible Self-Hosted Gateway

Capabilities that may be implemented by third parties or self-hosters through the same contracts:

- gateway-routed access to approved private repositories
- self-hosted document retrieval or vector retrieval
- self-hosted code graph services
- organization connectors managed outside SpecForge Central

This preserves architectural openness and avoids making the public interface a Central-only façade.

### SpecForge Central Commercial

Capabilities that should primarily or mandatorily route through Central:

- cross-repository private repo access outside the active workspace
- organization-wide RAG or document retrieval
- shared code graph or semantic graph across repositories
- external SaaS connectors using centrally managed secrets
- organization-wide issue, PR, project, or work-management integrations
- global policy enforcement for execution tools
- budgets, quotas, rate limits, and cost attribution
- audit exports, compliance views, and approval queues
- data redaction, content filtering, and other centralized trust controls

## Routing Classes

Each execution tool should declare one routing class:

- `local-only`
- `local-preferred`
- `central-preferred`
- `central-required`

### Default Rules

- Current-repository reads and searches should default to `local-only`.
- Shared or external knowledge access should default to `central-required`.
- Current-repository semantic services may default to `local-preferred`.
- Connector-backed integrations that need secrets or organizational policy should default to `central-required`.

## Decision Heuristics

An execution tool should route through Central when one or more of these are true:

- it requires secrets that should not live in each repository runtime
- it accesses data outside the active workspace
- it depends on shared organizational indexes or retrieval stores
- it needs organization-wide policy enforcement
- it needs portfolio-wide audit or compliance visibility
- it incurs shared cost that should be budgeted centrally
- it exposes a premium organization-grade capability that is part of the commercial product boundary

An execution tool should stay local when all of these are true:

- it only reads the active workspace or local runtime state
- it does not require shared secrets
- it does not require shared infrastructure
- it does not require organization-wide governance to be safe or useful

## Initial Capability Matrix

| Capability | Default routing | Packaging |
| --- | --- | --- |
| `repo_read_file(current-repo)` | `local-only` | OSS Local |
| `repo_search(current-repo)` | `local-only` | OSS Local |
| `workflow_artifact_read` | `local-only` | OSS Local |
| `local_git_inspect` | `local-only` | OSS Local |
| `local_build_inspect` | `local-only` | OSS Local |
| `semantic_search(current-repo, local-index)` | `local-preferred` | OSS Local |
| `graph_query(current-repo, local-graph)` | `local-preferred` | OSS Local |
| `private_repo_query(other-repo)` | `central-required` | Central Commercial / Compatible Self-Hosted Gateway |
| `knowledge_retrieve(shared-rag)` | `central-required` | Central Commercial / Compatible Self-Hosted Gateway |
| `graph_query(shared-org-graph)` | `central-required` | Central Commercial / Compatible Self-Hosted Gateway |
| `issue_lookup(org-integration)` | `central-required` | Central Commercial / Compatible Self-Hosted Gateway |
| `create_external_ticket` | `central-required` | Central Commercial / Compatible Self-Hosted Gateway |

## Product Positioning Rule

SpecForge Central should not be presented as "where tool calling finally works."

It should be presented as the managed control plane and gateway for:

- shared tools
- governed tools
- enterprise connectors
- organization-wide intelligence

SpecForge OSS should still be able to say:

- local tool use works
- repository-local intelligence works
- governed workflow works without Central

That product boundary is easier to defend technically and commercially than a generic open-core restriction on tool calling itself.
