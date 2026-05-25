# SpecForge · Ownership, Visibility, And Self-Hosting Tasks

Last reviewed: 2026-05-22.

This file tracks user-story ownership metadata, per-user visibility preferences, scalable default listing behavior, and the self-hosting fallback rule for engine defects.

## Status Model

- `todo`: not started
- `doing`: in progress
- `blocked`: waiting on a decision or prerequisite
- `done`: implemented and locally validated

## Block Goal

SpecForge should scale to large repositories by separating shared ownership from local visibility, showing each developer a focused default queue, and ensuring engine defects can still be tracked and repaired even when the engine itself is degraded.

## Architectural Decision

- `createdBy` is shared metadata. It identifies who originally created the user story.
- `owner` is shared metadata. It identifies the current responsible developer or assignee.
- Local visibility is not shared metadata. It belongs to each user and must not collide across collaborators.
- Per-user visibility preferences must live outside tracked repository state and outside Git-shared workflow metadata.
- The default user-story list must not load or render the entire repository backlog before filtering by owner and local preferences.

## Self-Hosting Rule

SpecForge must not rely on its own workflow engine as the only place where engine defects are registered or coordinated.

When the defect affects SpecForge itself:

1. register the bug as a GitHub issue labeled `bug`;
2. synchronize the local bug mirrors in `doc/bugs-open.md` or `doc/bugs-closed.md`;
3. update the relevant planning documents only when architectural context or roadmap framing changed;
4. only then implement the repair work inside product code and UI surfaces.

This rule exists so an engine defect cannot erase, delay, or hide the repair backlog for its own failure.

## Product Rules

- When a local user creates a user story, set `createdBy = currentUser` and `owner = currentUser` by default.
- When SpecForge Central delivers a user story assigned to the current user, preserve `owner = currentUser` from the incoming assignment.
- The default list should show only active user stories where `owner == currentUser`, except those explicitly hidden by that user.
- Completed-work visibility remains opt-in through existing completed filters.
- Search should default to the current user's visible scope and expose a switch to include other owners.
- Search across other owners may be heavier than the default view, but it must be explicit and user-controlled.

## Local Preference Rules

Store these per user, not in shared repository workflow metadata:

- `hiddenUsIds`
- `watchingUsIds`
- `starredUsIds` or equivalent starred model
- `maxVisibleUserStories`
- optional user defaults for search scope such as `includeOtherOwners`

## Tasks

- [x] `OVS-001` Status: `done`
  Add shared user-story metadata for `createdBy` and `owner`.
  Output: user-story create/import/update flows persist and expose both fields through domain, application, MCP, and UI summary models.

- [x] `OVS-002` Status: `done`
  Define the local per-user visibility model.
  Output: local preferences support hidden stories, watched stories, starred stories, and a maximum visible story limit without polluting shared workflow metadata.

- [x] `OVS-003` Status: `done`
  Change the default list pipeline to filter early by active status, owner, and local visibility before render.
  Output: the sidebar and portal no longer need to enumerate and render the whole backlog before showing "my active stories."

- [x] `OVS-004` Status: `done`
  Add explicit watch visibility in the UI.
  Output: a per-user "eye" control can include a story in the visible queue even when it is not owned by the current user.

- [x] `OVS-005` Status: `done`
  Extend search with an explicit `include other owners` switch.
  Output: users can discover other stories on demand without making the default queue heavy.

- [x] `OVS-006` Status: `done`
  Document and enforce the self-hosting fallback rule for engine defects.
  Output: engine defects are always registered in docs and an external tracker before implementation starts.

## Links

- Parent map: [task-map.md](../task-map.md)
- Product backlog anchor: [implementation-plan.md](../implementation-plan.md)
- Runtime context: [runtime-and-persistence.md](../runtime-and-persistence.md)
