# SpecForge · Portal State Contract

Last reviewed: 2026-05-22.

This document freezes the state-ownership contract for the browser workflow portal before deeper refactoring starts.

## Goal

The browser workflow portal must have explicit ownership for every important state category. The portal should stop depending on accidental inference across server defaults, URL fragments, embedded sidebar markup, and browser-local state.

## Design Principle

State must live in the highest layer that truly owns it.

- If a value changes the whole portal scope, the parent shell owns it.
- If a value exists only to render a local interaction widget, the local component owns it.
- If a value is repository truth, the backend/domain owns it.
- If a value is a local user preference, it must not be promoted into shared workflow metadata.

## State Ownership Map

### Backend or Domain Truth

These values are repository truth and must be loaded from workflow artifacts or application DTOs, not inferred in the browser:

- `workflow`
- `workflow phases`
- `timeline`
- `current phase`
- `selected artifact content`
- `selected operation content`
- `user story metadata`
  - `usId`
  - `title`
  - `kind`
  - `createdBy`
  - `owner`
  - `category`
  - `tags`
- workflow status
- dependency/blocking state
- branch metadata
- persisted review or approval state

### Parent Portal Shell State

These values define the global behavior of the portal page and must be owned by the parent shell, not by an embedded sidebar:

- selected user story id
- selected workflow phase id
- sidebar scope mode
  - active or dropped
- `include other owners`
- `show completed`
- `show blocked`
- `show hidden`
- modal open or closed state
- active modal payload
- route normalization result
- no-selection or fallback resolution state
- current actor identity used by browser-side actions

The parent shell is also responsible for synchronizing these values with:

- URL query parameters
- server requests
- browser refresh and reload behavior

### Local Browser Preference State

These values are per-user local preferences. They can live in browser storage or local preference files, but they must not become shared workflow metadata:

- watched user story ids
- hidden user story ids
- starred user story ids for the browser portal
- optional remembered UI density or collapsed state

These preferences may influence which stories are visible, but the parent shell still owns the final visible-scope decision.

### Sidebar Rendering State

The sidebar renderer may receive state, but it must not own portal-global rules.

Allowed sidebar-local concerns:

- hovered row
- temporary menu open or closed state
- local search input text
- keyboard focus within the sidebar
- button affordance rendering

Disallowed sidebar ownership:

- deciding the canonical selected user story
- owning global scope toggles
- deciding route normalization
- deciding modal lifecycle for portal-wide actions

## URL Contract

The browser portal URL is a transport and reopen contract, not the full source of truth.

The URL may carry:

- `usId`
- `selectedPhaseId`
- sidebar scope flags
- local-scope visibility flags that affect what the parent shell renders

The URL must not be the only place where the rules are defined. The parent shell and server must both understand the same normalization rules.

## Selection Contract

Selection must follow these rules:

1. If the URL contains a valid `usId`, use it.
2. If the URL contains an invalid or stale `usId`, normalize to a valid fallback without breaking the portal.
3. If the URL omits `usId`, resolve according to the current visible scope contract.
4. If no visible story is available, the portal must still load in a valid no-selection state.
5. The sidebar must not silently become the hidden owner of selection.

## Modal Contract

Portal-wide modal workflows are owned by the parent shell.

Examples:

- edit user story info
- assign to me
- reset workflow
- repair workflow
- destructive confirmations

The sidebar or workflow detail may request a modal, but they do not own the modal lifecycle.

## VS Code Extension Boundary

This contract does account for the VS Code extension.

The extension and the browser portal may share:

- DTOs
- render helpers where appropriate
- command names
- validation rules
- user-story summary semantics

The extension and the browser portal must not be forced to share:

- host-specific state ownership
- browser route normalization logic
- VS Code webview message assumptions
- iframe-specific portal workarounds

In practice:

- VS Code may keep its host-specific sidebar state inside the extension/webview boundary.
- The browser portal must own its own page-level state directly.
- Shared code is acceptable only when the ownership boundary remains explicit.

## Anti-Patterns To Avoid

- letting the iframe decide global scope
- using `postMessage` as the primary state store
- treating URL rewrite side effects as business logic
- hiding missing selection behind arbitrary reload behavior
- using browser-native dialogs for critical product actions
- assuming a working portal interaction is validated because a string-based renderer test passed

## Implementation Consequence

The first portal refactor steps should move global scope, route normalization, selection resolution, and modal orchestration into the parent shell before larger rendering consolidation work starts.

## Related Tasks

- `PORTAL-001` in [tasks/portal-modernization.md](tasks/portal-modernization.md)
- `PORTAL-002` in [tasks/portal-modernization.md](tasks/portal-modernization.md)
- `PORTAL-003` in [tasks/portal-modernization.md](tasks/portal-modernization.md)
