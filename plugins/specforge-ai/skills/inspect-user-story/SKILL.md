---
name: inspect-user-story
description: Inspect SpecForge user stories, workflow state, current phase, runtime status, lineage, and files through MCP.
---

# Inspect User Story

Use `specforge_query`.

## Default Visual Inspection

When the user asks to see, view, open, review, or inspect a specific user story, prefer the SpecForge workflow portal over raw file reads:

1. Start the local workflow portal with the CLI:

```bash
dotnet run --project src/SpecForge.Runner.Cli/SpecForge.Runner.Cli.csproj -- serve-workflow <workspaceRoot> <usId> <urlPrefix>
```

Use an available localhost port for `<urlPrefix>`, for example `http://localhost:5128/`.

2. Open the resulting local URL in the browser/in-app browser.
3. Use `specforge_query` or direct file reads only as supporting context when the user asks for a textual summary, when the portal cannot start, or when troubleshooting.

Do not treat a Markdown dump of `.specs/**/us.md` as sufficient for "show me the US" unless the user explicitly asks for the raw file contents.

Common calls:

```json
{ "workspaceRoot": "<absolute repo path>", "query": "list_user_stories" }
```

```json
{ "workspaceRoot": "<absolute repo path>", "query": "workflow", "usId": "US-123" }
```

```json
{ "workspaceRoot": "<absolute repo path>", "query": "current_phase", "usId": "US-123" }
```

Prefer concise summaries: current phase, status, blocking reason, pending questions, artifact paths, and latest timeline events.
