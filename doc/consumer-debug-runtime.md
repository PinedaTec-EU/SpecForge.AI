# SpecForge.AI Local Debug Runtime For Side Projects

This guide explains how another local repository can consume the current `SpecForge.AI` runtime in debug-oriented local development without copying ad hoc files by hand.

## Purpose

Use this flow when:

- `SpecForge.AI` is checked out locally as the source runtime;
- a side project in the same local organization workspace wants to consume the latest packaged plugin and MCP server;
- you want changes in `SpecForge.AI` to become available to consumer repositories with one refresh cycle.

Use the packaged plugin copy flow instead when you need a portable snapshot that can move independently from the source repository.

## Runtime Artifacts

`SpecForge.AI` refreshes the consumer-facing runtime through:

- `npm run compile`

That command:

- compiles the TypeScript extension code;
- publishes `SpecForge.McpServer` and `SpecForge.Runner.Cli` into `dist/mcp/`;
- synchronizes the packaged plugin bundle under `plugins/specforge-ai/`.

The packaged plugin is the consumer boundary. Side projects should not point directly at `src/` projects.

## Local Distribution Flow

After `npm run compile`, run:

```bash
./tools/sync-local-plugin-marketplace.sh
```

That script:

- copies the packaged plugin into the shared local plugin cache at `../.agents/plugins/specforge-ai/`;
- refreshes the shared marketplace file at `../.agents/plugins/marketplace.json`;
- links each sibling Git repository under the same organization root to that shared plugin through `.agents/plugins/specforge-ai`;
- refreshes each sibling repository marketplace file so Codex can discover the plugin locally.

## Consumer Repository Contract

A local consumer repository should expose these references:

- `.agents/plugins/specforge-ai` as a symlink to the shared local plugin cache;
- `.agents/plugins/marketplace.json` with the `specforge-ai` local plugin entry;
- an optional workspace `.mcp.json` that points to `./.agents/plugins/specforge-ai/mcp/SpecForge.McpServer` for direct MCP clients.

Minimal workspace `.mcp.json` example:

```json
{
  "mcpServers": {
    "specforge": {
      "command": "./.agents/plugins/specforge-ai/mcp/SpecForge.McpServer",
      "args": [],
      "env": {}
    }
  }
}
```

## Recommended Refresh Cycle

When the runtime changes in `SpecForge.AI`:

1. Run `npm run compile` in `SpecForge.AI`.
2. Run `./tools/sync-local-plugin-marketplace.sh` in `SpecForge.AI`.
3. Reopen or reload the consumer repository MCP/plugin client if it cached the previous runtime.

This keeps side projects aligned with the latest local debug runtime while preserving the packaged-plugin boundary.

## SpecForge Central

`SpecForge.AI Central` is the current guinea pig repository for this flow.

Its expected local setup is:

- `.agents/plugins/specforge-ai` linked to the shared local plugin cache;
- workspace `.mcp.json` pointing to that linked plugin runtime;
- repository documentation explaining that the runtime is owned by the sibling `SpecForge.AI` checkout.
