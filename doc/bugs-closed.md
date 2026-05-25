# Closed Bugs

Generated from GitHub issues in `PinedaTec-EU/SpecForge.AI` labeled `bug`. Do not edit manually; run `node tools/sync-bug-docs.js`.

Count: 26

## SFB-001

- Bug code: `SFB-001`
- GitHub issue: [#7](https://github.com/PinedaTec-EU/SpecForge.AI/issues/7)
- Discovery date: `2026-05-22`
- Status: `Fixed`
- Short description: Switching the repository branch in a single local checkout can hide or strand user stories created after returning to `main`, because user-story visibility currently depends on the active branch/worktree instead of a stable control workspace.
- Reproduction steps:
1. Start from `main` in one local checkout.

## SFB-002

- Bug code: `SFB-002`
- GitHub issue: [#8](https://github.com/PinedaTec-EU/SpecForge.AI/issues/8)
- Discovery date: `2026-05-22`
- Status: `Fixed`
- Short description: SpecForge lacks a documented fallback rule for tracking and repairing engine defects outside its own workflow engine, so a defect in the engine can block or delay the registration, visibility, and execution of the work needed to fix it.
- Reproduction steps:
1. Assume a defect affects local user-story intake, indexing, or workflow execution inside SpecForge itself.

## SFB-003

- Bug code: `SFB-003`
- GitHub issue: [#9](https://github.com/PinedaTec-EU/SpecForge.AI/issues/9)
- Discovery date: `2026-05-22`
- Status: `Fixed`
- Short description: The browser workflow portal renders ownership-visibility controls but does not honor them. It defaults to showing stories from other owners and does not handle watch, hide, or `Include other owners` commands.
- Reproduction steps:
1. Start the workflow portal against a workspace that contains at least one user story owned by `cli-user` and another owned by a different developer.

## SFB-004

- Bug code: `SFB-004`
- GitHub issue: [#10](https://github.com/PinedaTec-EU/SpecForge.AI/issues/10)
- Discovery date: `2026-05-22`
- Status: `Fixed`
- Short description: When ownership filtering leaves the sidebar with zero visible user stories, the sidebar falls back to the “Create your first user story” empty state and hides the compact action controls, preventing users from reopening view options and restoring other stories.
- Reproduction steps:
1. Open the workflow portal or extension sidebar in a repository where user stories exist but none match the current owner scope.

## SFB-005

- Bug code: `SFB-005`
- GitHub issue: [#11](https://github.com/PinedaTec-EU/SpecForge.AI/issues/11)
- Discovery date: `2026-05-22`
- Status: `Fixed`
- Short description: The sidebar user-story card used an ambiguous circle visibility control, left a visual gap before the action rail, duplicated hide/show state inside the context menu, and exposed repair/reset actions in the CLI portal without wiring them to runnable endpoints.
- Reproduction steps:
1. Open the workflow portal sidebar with at least one visible user story card.

## SFB-006

- Bug code: `SFB-006`
- GitHub issue: [#12](https://github.com/PinedaTec-EU/SpecForge.AI/issues/12)
- Discovery date: `2026-05-22`
- Status: `Fixed`
- Short description: The sidebar `Edit US info` action was wired as a navigation shortcut to the capture source instead of a real metadata edit flow, so users could not update owner, title, category, or tags from the story card actions.
- Reproduction steps:
1. Open the sidebar or CLI workflow portal on a repository with at least one user story.

## SFB-007

- Bug code: `SFB-007`
- GitHub issue: [#13](https://github.com/PinedaTec-EU/SpecForge.AI/issues/13)
- Discovery date: `2026-05-22`
- Status: `Fixed`
- Short description: The edit metadata flow reused the rendered story title as the edit default, so stories whose visible title already included the `US-xxxx` prefix could save that prefix back into metadata and duplicate it on the next render.
- Reproduction steps:
1. Open a story whose current title is rendered as `US-xxxx · ...`.

## SFB-008

- Bug code: `SFB-008`
- GitHub issue: [#14](https://github.com/PinedaTec-EU/SpecForge.AI/issues/14)
- Discovery date: `2026-05-22`
- Status: `Fixed`
- Short description: The CLI workflow portal implemented `Edit US info` with `window.prompt()`, which is not supported in the in-app browser, so the action failed before the user could edit or persist metadata.
- Reproduction steps:
1. Open the workflow portal in the in-app browser.

## SFB-009

- Bug code: `SFB-009`
- GitHub issue: [#15](https://github.com/PinedaTec-EU/SpecForge.AI/issues/15)
- Discovery date: `2026-05-22`
- Status: `Fixed`
- Short description: The CLI workflow portal stopped honoring `Include other owners` from the sidebar `view options` menu because the iframe menu click did not reliably reach the parent portal state handler, leaving the scope unchanged.
- Reproduction steps:
1. Open the workflow portal on a story whose owner is outside the current user scope or where the default scope hides the available stories.

## SFB-010

- Bug code: `SFB-010`
- GitHub issue: [#16](https://github.com/PinedaTec-EU/SpecForge.AI/issues/16)
- Discovery date: `2026-05-22`
- Status: `Fixed`
- Short description: The CLI workflow portal renderer could fail to boot with `500 Broken pipe` because the parent-side bridge for sidebar scope toggles introduced an invalid nested template literal, making `render-cli-workflow-html.js` unparsable by Node.
- Reproduction steps:
1. Start the CLI workflow portal after the sidebar toggle bridge change.

## SFB-011

- Bug code: `SFB-011`
- GitHub issue: [#17](https://github.com/PinedaTec-EU/SpecForge.AI/issues/17)
- Discovery date: `2026-05-22`
- Status: `Fixed`
- Short description: The portal empty-state shell referenced `currentActor` before defining it, so the parent-shell script failed during startup and `Sidebar view options` appeared visible but did not open from the in-app browser.
- Reproduction steps:
1. Open the workflow portal in a scope that resolves to no selected visible story.

## SFB-012

- Bug code: `SFB-012`
- GitHub issue: [#18](https://github.com/PinedaTec-EU/SpecForge.AI/issues/18)
- Discovery date: `2026-05-23`
- Status: `Fixed`
- Short description: The browser workflow portal rendered `Sidebar view options` twice: once in the parent shell and again inside the embedded sidebar `srcdoc`, creating duplicated controls for the same global scope behavior.
- Reproduction steps:
1. Open the workflow portal in the browser with the sidebar visible.

## SFB-013

- Bug code: `SFB-013`
- GitHub issue: [#19](https://github.com/PinedaTec-EU/SpecForge.AI/issues/19)
- Discovery date: `2026-05-23`
- Status: `Fixed`
- Short description: The workflow portal sidebar shell defined only three grid columns in its top rail while rendering four controls, causing the pin button to fall out of the intended top-right action group.
- Reproduction steps:
1. Open the workflow portal with the sidebar visible.

## SFB-014

- Bug code: `SFB-014`
- GitHub issue: [#20](https://github.com/PinedaTec-EU/SpecForge.AI/issues/20)
- Discovery date: `2026-05-23`
- Status: `Fixed`
- Short description: The workflow graph panel rendered explanatory helper copy under `Workflow Constellation`, adding visual noise without contributing meaningful action guidance.
- Reproduction steps:
1. Open the workflow portal on a user story with the graph visible.

## SFB-015

- Bug code: `SFB-015`
- GitHub issue: [#21](https://github.com/PinedaTec-EU/SpecForge.AI/issues/21)
- Discovery date: `2026-05-23`
- Status: `Fixed`
- Short description: The `Assign to me` action in the portal metadata modal could resolve to `cli-user` instead of the Git identity configured for the current repository, producing incorrect ownership assignments.
- Reproduction steps:
1. Open the workflow portal for any user story.

## SFB-016

- Bug code: `SFB-016`
- GitHub issue: [#22](https://github.com/PinedaTec-EU/SpecForge.AI/issues/22)
- Discovery date: `2026-05-23`
- Status: `Fixed`
- Short description: The top-level documentation did not clearly explain the methodological pillars behind SpecForge, so a developer could read the README and still miss that the product is built on governed SDD, harness engineering, structured criticism/reconstruction, human gates, and phase-specialized agents.
- Reproduction steps:
1. Open [README.md](../README.md).

## SFB-017

- Bug code: `SFB-017`
- GitHub issue: [#23](https://github.com/PinedaTec-EU/SpecForge.AI/issues/23)
- Discovery date: `2026-05-23`
- Status: `Fixed`
- Short description: The portal `Edit US info` modal exposed `Assign to me` even when the browser script had no usable current actor on `window`, so clicking it could blank the owner field; the modal also allowed an always-enabled save path without inline validity feedback for invalid metadata.
- Reproduction steps:
1. Open the workflow portal for any user story.

## SFB-018

- Bug code: `SFB-018`
- GitHub issue: [#28](https://github.com/PinedaTec-EU/SpecForge.AI/issues/28)
- Discovery date: `2026-05-23`
- Status: `Fixed`
- Short description: The portal `Edit US info` modal closed on backdrop click instead of only through its explicit controls, and its validation marker used inline red text that shifted field width instead of a fixed-width icon with tooltip-only detail.
- Reproduction steps:
1. Open the workflow portal for any user story.
2. Open `Edit US info`.
3. Click outside the modal and observe that it closes without using `Cancel` or `×`.
4. Reopen the modal with an invalid field state and observe that the validation marker expands as inline red text, causing the field row layout to move.

## SFB-019

- Bug code: `SFB-019`
- GitHub issue: [#29](https://github.com/PinedaTec-EU/SpecForge.AI/issues/29)
- Discovery date: `2026-05-23`
- Status: `Fixed`
- Short description: The workflow constellation hid the `completed` phase node until the workflow actually completed, which prevented operators from positioning that shape during graph layout editing and made the final target inconsistent with the rest of the editable graph.
- Reproduction steps:
1. Open any non-completed workflow in the workflow portal or VS Code workflow view.
2. Enable graph layout editing.
3. Observe that the `completed` node is missing from the constellation even though the graph layout schema already defines its coordinates and incoming edge.
4. Observe that the final node cannot be positioned until the workflow reaches completed state.

## SFB-020

- Bug code: `SFB-020`
- GitHub issue: [#30](https://github.com/PinedaTec-EU/SpecForge.AI/issues/30)
- Discovery date: `2026-05-23`
- Status: `Fixed`
- Short description: The workflow constellation did not expose a working horizontal/vertical mode toggle in the graph toolbar, so operators could not switch orientations from the active view and had to rely on external settings or stale persisted state.
- Reproduction steps:
1. Open the workflow portal or VS Code workflow view on a non-aggregate workflow.
2. Inspect the graph toolbar and attempt to switch between horizontal and vertical layouts.
3. Observe that no working mode toggle is available or that clicking the apparent toggle does not change `data-graph-layout-mode` in the active graph.

## SFB-021

- Bug code: `SFB-021`
- GitHub issue: [#3](https://github.com/PinedaTec-EU/SpecForge.AI/issues/3)
- Discovery date: `2026-05-24`
- Status: `Fixed`
- Short description: The embedded workflow portal sidebar exposes the + create-user-story button, but clicking it in the integrated browser does not open the create form or change the sidebar state.
- Reproduction steps:
1. Open the workflow portal in the integrated browser.
2. In the user-story sidebar, click the + button.
3. Observe that no create form opens and the sidebar remains unchanged.

## SFB-022

- Bug code: `SFB-022`
- GitHub issue: [#4](https://github.com/PinedaTec-EU/SpecForge.AI/issues/4)
- Discovery date: `2026-05-24`
- Status: `Fixed`
- Short description: Switching the embedded workflow portal sidebar to Dropped and then back to the active backlog can leave the main panel desynchronized from the visible sidebar scope, so the active user story cannot be reselected from the current URL.
- Reproduction steps:
1. Open the workflow portal in the integrated browser with a selected user story.
2. Switch the sidebar to Dropped user stories when no dropped items are available.
3. Return to the active backlog and click the visible user story.
4. Observe that the main panel stays on No user story selected even though the sidebar shows a selectable story.

## SFB-023

- Bug code: `SFB-023`
- GitHub issue: [#2](https://github.com/PinedaTec-EU/SpecForge.AI/issues/2)
- Discovery date: `2026-05-24`
- Status: `Fixed`
- Short description: The embedded configuration modal keeps a Reload action that is not needed, does not expose an in-page Close action, keeps Save enabled semantics inconsistent with actual dirty state expectations, and the modal close icon does not close the overlay in the integrated browser.
- Reproduction steps:
1. Open the workflow portal in the integrated browser.
2. Open Configuration from the sidebar.
3. Observe that the configuration surface exposes Save Configuration and Reload but no explicit Close action inside the page.
4. Observe that clicking the modal close icon (×) does not close the configuration overlay.
5. Make no changes and observe that Save should remain disabled until the form becomes dirty.

## SFB-024

- Bug code: `SFB-024`
- GitHub issue: [#5](https://github.com/PinedaTec-EU/SpecForge.AI/issues/5)
- Discovery date: `2026-05-24`
- Status: `Won't Fix`
- Short description: The embedded workflow portal still depends on iframe-based shells for the user-story sidebar and configuration modal, even though the refactor direction is to remove iframe coupling. This is now a product bug because it keeps reintroducing broken close behavior, fragile event wiring, and browser-integration regressions.
- Reproduction steps:
1. Open the embedded workflow portal.
2. Inspect the portal shell implementation and runtime DOM.
3. Observe that the sidebar and configuration modal are still rendered through iframe boundaries.
4. Observe follow-on regressions such as broken close behavior, stale wiring, or parent/child state desynchronization.

## SFB-025

- Bug code: `SFB-025`
- GitHub issue: [#6](https://github.com/PinedaTec-EU/SpecForge.AI/issues/6)
- Discovery date: `2026-05-24`
- Status: `Fixed`
- Short description: The embedded browser becomes unstable on the workflow portal because the initial document inlines every sidebar variant into one giant script payload, which can trigger rapid white flicker and make action-button validation unreliable.
- Reproduction steps:
1. Launch the workflow portal at `http://localhost:5128/` or any selected workflow URL.
2. Open it in the integrated browser.
3. Observe repeated flicker between a white frame and the portal, while unrelated URLs remain stable in the same browser.
4. Inspect the served HTML and observe that the initial document includes a multi-megabyte inline script with serialized sidebar variants.

## SFB-026

- Bug code: `SFB-026`
- GitHub issue: [#27](https://github.com/PinedaTec-EU/SpecForge.AI/issues/27)
- Discovery date: `2026-05-24`
- Status: `Fixed`
- Short description: The CLI workflow portal used query params as a transient SPA state store, so switching sidebar scopes such as active and dropped could keep stale URL-driven filters and phase selection that no longer matched the visible user story set.
- Reproduction steps:
1. Open the CLI workflow portal with a selected user story.
2. Toggle sidebar view options such as `Show dropped`, `Show completed`, or `Include other owners`.
3. Observe that the URL accumulates transient params such as `sidebarVisibility`, `sidebarCompleted`, `selectedPhaseId`, or related sidebar filters.
4. Switch to a different sidebar scope or user story and observe that stale query params continue to shape the rendered view instead of relying on internal SPA memory.
