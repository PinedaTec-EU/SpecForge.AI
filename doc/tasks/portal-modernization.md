# SpecForge · Portal Modernization Tasks

Last reviewed: 2026-05-25.

This file keeps the portal-modernization context, target architecture, and completed milestones.

Canonical open work for this block now lives in GitHub Issues, not in this Markdown file.

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

## Completed Milestones

- [x] `PORTAL-001` Status: `done`
  Write and freeze the portal state ownership contract.
  Output: one documented source-of-truth map for selection, filters, visible scope, modal state, local preferences, and persisted URL state.
  Notes: must explicitly state what belongs to the parent shell, what belongs to sidebar markup, and what must never be inferred inside the iframe.
  Evidence: [portal-state-contract.md](../portal-state-contract.md)

- [x] `PORTAL-002` Status: `done`
  Move global scope and visibility controls to the parent shell.
  Output: `include other owners`, `show completed`, `show blocked`, `show hidden`, and similar scope controls are rendered and managed by the parent portal, not by embedded sidebar-only wiring.
  Notes: this is the first structural cut because it removes the most failure-prone bridge.
  Evidence: parent-shell view controls are rendered and handled in `tools/render-cli-workflow-html.js`; iframe commands now delegate into parent-owned scope state instead of owning the toggle logic.

- [x] `PORTAL-003` Status: `done`
  Consolidate portal selection resolution into one reusable server and client contract.
  Output: the same resolution rules apply to `/`, partial URLs, invalid `usId`, changed filters, and hidden stories.
  Notes: selection must support either an explicit empty detail/workflow state or a deterministic visible fallback; no more accidental divergence between route handling and client reload behavior.
  Evidence: server-side selection and no-selection resolution now live in `src/SpecForge.Runner.Cli/Program.cs`, with renderer support in `tools/render-cli-workflow-html.js` and coverage in `tests-ts/runnerCliProgram.test.ts`.

- [x] `PORTAL-004` Status: `done`
  Replace portal-local ad hoc event bridging with a smaller explicit interaction contract.
  Output: sidebar card actions, modal launches, and workflow navigation use a documented command set with fewer implicit `postMessage` dependencies.
  Notes: the goal is not zero messaging, but predictable messaging with fewer fragile hooks.
  Evidence: parent-shell sidebar command dispatch is centralized in `tools/render-cli-workflow-html.js` and documented in [portal-interaction-contract.md](../portal-interaction-contract.md).

- [x] `PORTAL-005` Status: `done`
  Normalize browser-safe modal flows.
  Output: edit, assign, confirm, repair, reset, and future portal actions use one modal/dialog pattern compatible with the in-app browser and normal browsers.
  Notes: no new use of `alert`, `confirm`, or `prompt` for product-critical actions.
  Evidence: `Edit US info` and `Assign to me` run through the parent-shell HTML dialog, replacing unsupported prompt-based flows in `tools/render-cli-workflow-html.js`.

- [x] `PORTAL-006` Status: `done`
  Create a proper no-selection and empty-state experience.
  Output: if the URL or current scope does not resolve a selected story, the portal still loads with a meaningful empty detail panel, selection guidance, and recoverable controls.
  Notes: fallback to first visible is acceptable only where explicitly defined by the selection contract.
  Evidence: the portal now renders `No user story selected` from the main route instead of failing, with parent-shell recovery controls available in `tools/render-cli-workflow-html.js`.

## Canonical Open Issues

- [#38](https://github.com/PinedaTec-EU/SpecForge.AI/issues/38) `SFT-038: Reduce duplicated portal UI logic`
  Covers reduction of duplicated UI logic and clearer shared command semantics across the browser portal and VS Code host.

- [#39](https://github.com/PinedaTec-EU/SpecForge.AI/issues/39) `SFT-039: Expand portal behavior validation`
  Covers stronger automated validation beyond string-presence assertions.

- [#40](https://github.com/PinedaTec-EU/SpecForge.AI/issues/40) `SFT-040: Exhaustive in-app portal validation`
  Covers the mandatory integrated-browser validation pass for the migrated portal.

- [#41](https://github.com/PinedaTec-EU/SpecForge.AI/issues/41) `SFT-041: Portal release validation gate`
  Covers the process gate requiring browser proof plus automated coverage updates for portal-affecting changes.

## Required Validation Scope For `SFT-040`

The exhaustive browser pass tracked in [#40](https://github.com/PinedaTec-EU/SpecForge.AI/issues/40) should still prove these flows:

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
