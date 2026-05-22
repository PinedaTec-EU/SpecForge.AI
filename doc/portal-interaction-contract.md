# SpecForge · Portal Interaction Contract

Last reviewed: 2026-05-22.

This document freezes the browser-portal interaction contract between the embedded sidebar surface and the parent portal shell.

## Goal

The portal must stop relying on ad hoc `postMessage` condition chains. Sidebar actions should travel through a small explicit command set that the parent shell owns and validates.

## Ownership Rule

- The sidebar may request an action.
- The parent shell decides how that action affects URL state, modal state, selection, and server calls.
- Repository truth still belongs to the backend.

This keeps the sidebar from becoming an accidental owner of portal-global behavior.

## Command Categories

### Navigation Commands

- `openWorkflow`
  - Select a story and clear phase-specific navigation when needed.
- `openMainArtifact`
  - Navigate to capture and focus the source section when appropriate.

### Modal Commands

- `showEditUserStoryForm`
  - Open the browser-safe metadata form in the parent shell.

### Local Preference Commands

- `toggleStarredUserStory`
- `toggleSidebarVisibilityUserStory`

These modify local browser preference state, but the parent shell still recalculates the resulting visible scope.

### Global Scope Commands

- `toggleDroppedUserStories`
- `toggleCompletedUserStories`
- `toggleBlockedUserStories`
- `toggleShowHiddenUserStories`
- `toggleSearchIncludesOtherOwners`

These are parent-shell commands. The sidebar may invoke them, but it must not own their state.

### Workflow Maintenance Commands

- `resetUserStoryToCapture`
- `analyzeRepairUserStory`
- `dropUserStory`
- `recoverUserStory`
- `openExecutionSettings`

These commands may trigger API calls, confirmations, or shell-owned modals.

## Dispatch Rule

The parent portal shell must dispatch sidebar requests through a single named handler map, not a growing chain of unrelated `if (message.command === "...")` clauses.

The intent is:

- one place to inspect the supported command surface
- one place to wire new commands
- fewer accidental regressions when adding or changing a sidebar action

## Validation Rule

Any portal task that changes the command contract must prove:

1. automated coverage for the changed command category
2. integrated-browser validation for the affected visible behavior

String-presence checks alone are not sufficient evidence for command wiring.

## Non-Goals

- This contract does not force the browser portal and VS Code host to share identical transport layers.
- This contract does not require removing all `postMessage` usage immediately.
- This contract does require that message semantics be explicit, small, and parent-owned.

## Related Tasks

- `PORTAL-004` in [tasks/portal-modernization.md](tasks/portal-modernization.md)
- `PORTAL-005` in [tasks/portal-modernization.md](tasks/portal-modernization.md)
- `PORTAL-009` in [tasks/portal-modernization.md](tasks/portal-modernization.md)
