# Changelog

All notable changes to SpecForge.AI are documented in this file.

## [0.1.4.432] - 2026-05-09

This first changelog entry consolidates the recent work since `0.1.4.403`.
Source range: commits after `9ecf2b0` through `c4dacf6`.

### Added

- Added a CLI workflow portal (`serve-workflow`) with graph-based workflow inspection, phase selection, fit-width graph defaults, stable centering until user viewport changes, and model-assisted spec approval answer suggestions.
- Added a VS Code user-story context menu action, **Open CLI Workflow Portal**, to launch the local browser portal directly for the selected story.
- Added a packaged-plugin quick prompt for opening the SpecForge CLI workflow portal from Codex.
- Wired the CLI workflow portal to real workflow actions on `main`, including continue/play, approval answer submission, refinement answer submission, and phase approval from the browser shim.
- Added automatic workflow portal opening before phase iteration so model-driven workflow runs surface the constellation view when needed.
- Added SpecForge goal intake support for Codex-style broad goals, including user-gated workflow rules before creating goal-derived user stories.
- Added configurable MVP rigor (`low`, `medium`, `high`) for refinement so teams can tune how demanding the refinement gate is before a story is treated as buildable.
- Added a packaged SpecForge MCP plugin bundle under `plugins/specforge-ai/mcp`, including the compiled VS Code webview assets and packaged MCP server artifacts.
- Added a realistic documentation workflow fixture at `.specs/us/workflow/US-DOC-001` for capturing workflow screenshots from actual SpecForge UI state.
- Added workflow documentation screenshot updates and cache-busting for playback/refinement visual states.

### Changed

- Updated the packaged MCP/plugin bundle so consumer repositories can run the local `SpecForge.McpServer` from `.agents/plugins/specforge-ai/mcp` instead of depending on the development repository path.
- Hardened refinement readiness so vague or under-specified ideas must be clarified before spec generation; refinement now pushes for enough product detail to support an efficient MVP implementation without over-engineering.
- Required concrete intake before goal story creation, with shared skill guidance updated to make the model ask clarifying questions before decomposing broad goals into small user stories.
- Aligned CLI workflow commands with `SpecForgeApplicationService` instead of bypassing application-level workflow behavior.
- Shared OpenAI-compatible phase provider construction between CLI and MCP, reducing duplicated provider setup and keeping runtime configuration consistent.
- Extracted MCP JSON-RPC stdio transport, MCP tool registry, CLI portal settings storage, and CLI workflow render cache into dedicated units to improve SRP and make behavior directly testable.
- Regenerated plugin distribution artifacts after the MCP/plugin changes, including compiled webview JavaScript, CLI workflow renderer shim, and packaged MCP binaries.

### Fixed

- Constrained MCP tool schemas with explicit enum values for query/action/prompt operations, phase slugs, file kinds, reopen reasons, and user story kinds.
- Made MCP array arguments fail fast when missing or empty instead of silently accepting invalid inputs.
- Cached CLI workflow portal rendering by workflow signature, selected phase, and artifact timestamps to avoid unnecessary Node renderer invocations while still invalidating stale HTML.
- Reworked CLI workflow portal render caching into a timestamp-aware cache that invalidates on signature, phase, artifact, operation log, or file deletion changes.
- Updated static TypeScript tests to inspect extracted helper files after SRP refactors instead of relying on stale `Program.cs` regex locations.

### Tests

- Expanded MCP and workflow portal coverage from the extracted units, including:
  - consecutive MCP stdio frames, invalid headers, truncated payloads, invalid JSON, and case-insensitive `Content-Length`;
  - MCP tool registry contracts for required properties, enum constraints, array schemas, compact facades, and duplicate tool names;
  - workflow portal render cache misses for signature, phase, artifact timestamp, operation log timestamp, tracked file deletion, overwrite, and LRU trim behavior;
  - portal settings migration/defaults and provider factory behavior.
- Current validation at this entry:
  - `dotnet test SpecForge.AI.slnx`: 204 tests passing.
  - `npm run test:ts`: 174 tests passing.
