# SpecForge.AI · SpecForge And SpecForge Central

Last reviewed: 2026-05-26.

This document defines the relationship between the public SpecForge.AI repository-local runtime and the private SpecForge Central side project.

It exists to make the product boundary explicit:

- SpecForge.AI must remain valuable as a public open-source local runtime.
- SpecForge Central must add legitimate organization-grade value instead of merely hiding basic capabilities behind a closed product.

## Short Version

SpecForge.AI is the local governed SDD runtime.

SpecForge Central is the managed control plane and governed gateway for organization-scale usage.

The rule is:

- local workflow truth stays in the repository
- local workflow execution must remain useful without Central
- shared, governed, or organization-grade intelligence may route through Central

## Product Boundary

### What SpecForge.AI Must Be

SpecForge.AI should be fully useful on its own for:

- repository-local workflow execution
- persisted `.specs/**` truth
- phase execution and approvals
- local MCP-backed operations
- local repository inspection and prompt context assembly
- local operator inspection and evidence
- local execution tools that only depend on the active workspace

The public runtime must not feel like a crippled teaser for a private platform.

### What SpecForge Central Must Be

SpecForge Central should provide capabilities that make sense as a managed organizational layer:

- managed repository catalog
- policy distribution and policy locks
- readiness and compliance visibility
- cross-repository governance
- organization-wide audit and approval surfaces
- governed shared retrieval and knowledge access
- connector and secret management
- portfolio-level budgets, quotas, and drift detection

Central should be valuable because it provides governance and shared infrastructure, not because it withholds trivial local capabilities.

## Architectural Rule

SpecForge Central must not replace repository-local truth.

Central coordinates, governs, audits, and routes. The managed repository still owns:

- workflow artifacts
- prompts
- state
- timeline
- repository-local evidence

This means the product split is:

- `SpecForge.AI`: local runtime
- `SpecForge Central`: control plane and managed gateway

## Managed Gateway Role

Central should be the primary managed gateway for execution tools that require shared infrastructure, shared policy, or centrally managed secrets.

This is especially relevant for:

- governed enterprise RAG
- private multi-repository retrieval
- shared code or semantic graph access
- external SaaS connectors
- centrally managed credentials
- centrally audited knowledge access

The point is not to route all tool use through Central.

The point is to route the high-value and high-risk capabilities through Central so organizations gain:

- guardrails
- policy enforcement
- source scoping
- auditing
- budget control
- trust and compliance controls

## Packaging Rule

Tool support should be split into three packaging tiers:

### 1. OSS Local

Capabilities that should remain available in the public runtime:

- current-repository file reads
- current-repository text search
- local `.specs/**` inspection
- local Git, build, test, and runtime inspection
- local prompt-context assembly
- repo-local semantic or graph features when they do not require shared infrastructure

### 2. Compatible Self-Hosted Gateway

Capabilities that may exist through the same contracts but outside SpecForge Central:

- self-hosted private repository retrieval
- self-hosted document retrieval
- self-hosted vector search
- self-hosted graph services
- self-hosted connector gateways

This keeps the architecture open and avoids making the public interfaces a Central-only façade.

### 3. SpecForge Central Commercial

Capabilities that should primarily or mandatorily route through Central:

- retrieval outside the active repository
- organization-wide RAG
- shared knowledge or code indexes
- connectors that use centrally managed secrets
- cross-repository policy enforcement
- organization-wide audit and approval flows
- budget, quota, and rate governance
- enterprise trust controls such as redaction or content restrictions

## Why Governed RAG Fits Central

Enterprises often need retrieval more than generic tool calling.

The real value is not simply "the model can query documents." The value is:

- governed retrieval
- approved source scopes
- phase-aware access rules
- audit of what was queried and why
- centrally managed credentials
- budgeted and observable usage
- policy-backed redaction or blocking

That makes RAG a strong anchor capability for Central because it combines:

- high business value
- real security and compliance needs
- shared infrastructure
- monetizable governance

## What Central Should Not Become

Central should not be framed as:

- the place where tool calling finally works
- a generic paywall around basic repository intelligence
- the owner of repository-local workflow truth
- another Scrum board

If the product split is designed badly, the open-source runtime will look intentionally crippled. That weakens both adoption and credibility.

## Positioning Sentence

Use this product framing consistently:

> SpecForge.AI is the repository-local governed SDD runtime. SpecForge Central is the managed control plane and governed gateway for shared organizational intelligence.

## Related Documents

- [product-vision.md](product-vision.md)
- [architecture.md](architecture.md)
- [execution-tool-packaging.md](execution-tool-packaging.md)
- [roadmap.md](roadmap.md)
