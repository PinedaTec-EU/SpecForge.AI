# SpecForge · Portal Modernization Tasks

Last reviewed: 2026-05-22.

This file tracks the migration from the current CLI-served workflow portal to a more stable browser product surface with lower coupling and stronger validation.

## Status Model

- `todo`: not started
- `doing`: in progress
- `blocked`: waiting on a decision or prerequisite
- `done`: implemented and locally validated

## Block Goal

SpecForge should have a workflow portal that behaves like a real product surface instead of a fragile compatibility layer. Portal changes must stop causing unrelated regressions in selection, sidebar scope, modal actions, and browser interaction.

## Problem Statement

The current portal works, but it still carries avoidable technical debt:

- workflow state is split across server-side route defaults, URL params, local storage, iframe `srcdoc`, and parent-page event bridges;
- global portal scope controls and local card actions are mixed across iframe and parent responsibilities;
- browser-only behavior has repeatedly diverged from VS Code behavior;
- manual browser validation has been too implicit, too narrow, or skipped after apparently small changes;
- fixes have tended to be local patches instead of architectural simplifications.

This is why small portal edits have repeatedly broken unrelated flows.

## Non-Goals

- Do not rewrite the whole workflow detail UI from scratch in one jump.
- Do not block current MVP usage waiting for a perfect React or SPA migration.
- Do not create a second parallel portal with duplicated behavior for long.

## Target Architecture

- The portal parent shell owns global state.
- Global scope controls do not depend on iframe message wiring.
- A user-story selection contract exists for complete URLs, partial URLs, invalid URLs, and no-selection startup.
- Browser-safe modal workflows replace ad hoc native browser dialogs.
- Shared interaction contracts between VS Code sidebar and portal are explicit and testable.
- The portal can render a valid empty or fallback state without HTTP failure or silent UI dead ends.

## Migration Plan

- [ ] `PORTAL-001` Status: `todo`
  Write and freeze the portal state ownership contract.
  Output: one documented source-of-truth map for selection, filters, visible scope, modal state, local preferences, and persisted URL state.
  Notes: must explicitly state what belongs to the parent shell, what belongs to sidebar markup, and what must never be inferred inside the iframe.

- [ ] `PORTAL-002` Status: `todo`
  Move global scope and visibility controls to the parent shell.
  Output: `include other owners`, `show completed`, `show blocked`, `show hidden`, and similar scope controls are rendered and managed by the parent portal, not by embedded sidebar-only wiring.
  Notes: this is the first structural cut because it removes the most failure-prone bridge.

- [ ] `PORTAL-003` Status: `todo`
  Consolidate portal selection resolution into one reusable server and client contract.
  Output: the same resolution rules apply to `/`, partial URLs, invalid `usId`, changed filters, and hidden stories.
  Notes: selection must support either an explicit empty detail/workflow state or a deterministic visible fallback; no more accidental divergence between route handling and client reload behavior.

- [ ] `PORTAL-004` Status: `todo`
  Replace portal-local ad hoc event bridging with a smaller explicit interaction contract.
  Output: sidebar card actions, modal launches, and workflow navigation use a documented command set with fewer implicit `postMessage` dependencies.
  Notes: the goal is not zero messaging, but predictable messaging with fewer fragile hooks.

- [ ] `PORTAL-005` Status: `todo`
  Normalize browser-safe modal flows.
  Output: edit, assign, confirm, repair, reset, and future portal actions use one modal/dialog pattern compatible with the in-app browser and normal browsers.
  Notes: no new use of `alert`, `confirm`, or `prompt` for product-critical actions.

- [ ] `PORTAL-006` Status: `todo`
  Create a proper no-selection and empty-state experience.
  Output: if the URL or current scope does not resolve a selected story, the portal still loads with a meaningful empty detail panel, selection guidance, and recoverable controls.
  Notes: fallback to first visible is acceptable only where explicitly defined by the selection contract.

- [ ] `PORTAL-007` Status: `todo`
  Reduce duplicated UI logic between VS Code sidebar and browser portal.
  Output: shared rendering and shared command semantics are clearer, with less host-specific branching and fewer portal-only patches.
  Notes: this may still use shared builders, but the ownership boundaries must become cleaner than they are now.

- [ ] `PORTAL-008` Status: `todo`
  Expand automated portal validation beyond string-presence tests.
  Output: tests assert behavior categories such as scope changes, selection fallback, modal submit/cancel, persistence, and route normalization.
  Notes: do not stop at snapshot-like checks that only prove generated HTML contains expected literals.

- [ ] `PORTAL-009` Status: `todo`
  Execute an exhaustive in-app browser validation pass for the migrated portal.
  Output: Codex manually validates the portal through the integrated browser and records explicit pass/fail evidence for all critical flows.
  Notes: this task is mandatory and cannot be closed by unit tests, renderer assertions, endpoint checks, or shell-only verification.
  Required flows:
  1. Open the portal at `/`, with a full URL, with a partial URL, and with an invalid `usId`.
  2. Verify selected-story inference or empty-state behavior matches the contract.
  3. Verify `view options` toggles for completed, blocked, hidden, and other owners.
  4. Verify watch/unwatch and hide/show behavior from the story card.
  5. Verify context menu actions that remain exposed are actually wired.
  6. Verify `Edit US info` opens, validates, saves, reloads, and persists values.
  7. Verify `Assign to me` uses the logged Git user and persists the owner transfer.
  8. Verify timeline evidence exists for owner transfer when ownership changes.
  9. Verify reload, hard refresh, and direct-link reopen preserve coherent state.
  10. Verify at least one scenario with another owner and at least one scenario with no owner.
  11. Verify at least one negative-path validation case and one cancel path.
  12. Verify no critical action depends on unsupported browser APIs.

- [ ] `PORTAL-010` Status: `todo`
  Add a release gate for portal changes.
  Output: portal-affecting tasks cannot be considered done without explicit browser validation notes and updated automated coverage for the touched behavior class.
  Notes: this is process hardening, not just code.

## Sequencing

Recommended order:

1. `PORTAL-001`
2. `PORTAL-002`
3. `PORTAL-003`
4. `PORTAL-004`
5. `PORTAL-005`
6. `PORTAL-006`
7. `PORTAL-007`
8. `PORTAL-008`
9. `PORTAL-009`
10. `PORTAL-010`

Reasoning:

- first reduce state ambiguity;
- then remove the most fragile global-control coupling;
- then lock selection and modal behavior;
- then harden testing and release discipline after the architecture is cleaner.

## Completion Rule

This block is not complete when the portal merely "works again."

It is complete when:

- the portal can survive partial or stale URLs without operator confusion;
- global scope changes do not rely on fragile iframe wiring;
- browser actions are demonstrably wired end to end;
- Codex has revalidated the final flows manually in the integrated browser;
- the next small portal change does not require another round of speculative patching.

## Links

- Parent map: [task-map.md](../task-map.md)
- Product backlog anchor: [implementation-plan.md](../implementation-plan.md)
- Related MVP work: [ownership-visibility-and-self-hosting.md](ownership-visibility-and-self-hosting.md)
- Runtime context: [runtime-and-persistence.md](../runtime-and-persistence.md)
