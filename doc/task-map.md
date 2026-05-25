# SpecForge · Task Map

Last reviewed: 2026-05-25.

This file is now a planning index, not the canonical backlog.

Active non-bug backlog tracking lives in GitHub Issues:

- features: `enhancement`
- technical debt: `tech-debt`
- bugs: `bug`

If you need a single local cache of what is currently open, use [github-backlog.md](github-backlog.md).

Use this document to understand how the work is grouped and where the planning context lives. Do not treat it as the live execution ledger.

## Work Blocks

| Block | Planning context | Canonical GitHub issues | Scope |
| --- | --- | --- | --- |
| `HARNESS` | [harness-implementation-plan.md](harness-implementation-plan.md), [harness-engineering-checklist.md](harness-engineering-checklist.md), [semantic-code-graph-design.md](semantic-code-graph-design.md) | [#51](https://github.com/PinedaTec-EU/SpecForge.AI/issues/51), [#52](https://github.com/PinedaTec-EU/SpecForge.AI/issues/52), [#53](https://github.com/PinedaTec-EU/SpecForge.AI/issues/53), [#54](https://github.com/PinedaTec-EU/SpecForge.AI/issues/54), [#55](https://github.com/PinedaTec-EU/SpecForge.AI/issues/55) | Harness governance, eval packs, stronger execution boundaries, reusable profiles, Central governance, and semantic graph rollout |
| `MVP` | [implementation-plan.md](implementation-plan.md), [roadmap.md](roadmap.md), [tasks/portal-modernization.md](tasks/portal-modernization.md), [tasks/ownership-visibility-and-self-hosting.md](tasks/ownership-visibility-and-self-hosting.md) | [#38](https://github.com/PinedaTec-EU/SpecForge.AI/issues/38), [#39](https://github.com/PinedaTec-EU/SpecForge.AI/issues/39), [#40](https://github.com/PinedaTec-EU/SpecForge.AI/issues/40), [#41](https://github.com/PinedaTec-EU/SpecForge.AI/issues/41), [#43](https://github.com/PinedaTec-EU/SpecForge.AI/issues/43), [#44](https://github.com/PinedaTec-EU/SpecForge.AI/issues/44), [#45](https://github.com/PinedaTec-EU/SpecForge.AI/issues/45), [#46](https://github.com/PinedaTec-EU/SpecForge.AI/issues/46), [#47](https://github.com/PinedaTec-EU/SpecForge.AI/issues/47) | Product MVP sequencing, extension/MCP/portal gaps, review/export surfaces, and integration packaging |
| `BRANCH` | [tasks/branch-lifecycle.md](tasks/branch-lifecycle.md) | [#36](https://github.com/PinedaTec-EU/SpecForge.AI/issues/36), [#37](https://github.com/PinedaTec-EU/SpecForge.AI/issues/37) | Branch lifecycle, branch activation, Git context switching, and operator visibility |

## Current Focus

- `HARNESS`: keep eval packs, stronger execution boundaries, reusable profiles, Central governance, and semantic graph rollout aligned with the harness backlog.
- `BRANCH`: finish automatic branch switching and consolidate the safe activation path before adding more branch-aware behaviors.
- `MVP`: keep portal hardening, prompt inspection, integration, packaging, and review/export improvements visible as the main near-term product gaps.

## Intake Rule For New Work

When a new task appears:

1. decide which block owns it;
2. create or update the canonical GitHub issue first;
3. update the relevant planning document only when architectural context or roadmap framing changed;
4. only add a new planning document when the work needs durable context that GitHub issue text alone should not carry.
