# SpecForge.AI · Getting Started

This guide is for someone evaluating or trying SpecForge locally.

## What You Need

- .NET SDK 10
- Node.js 23+
- npm 10+
- VS Code 1.100+

## Quick Local Bootstrap

```bash
git clone <your-fork-or-repo-url>
cd SpecForge.AI
npm install
npm run compile
dotnet test SpecForge.AI.slnx
```

The TypeScript build does not require a global `tsc`.

## Fastest Product Tour

If you want to see the product before changing code:

```bash
./launch.sh
```

To open directly on a specific user story:

```bash
./launch.sh US-0001
```

This starts the local browser workflow portal on `http://localhost:5128/` by default.

From there:

1. Open the portal home page.
2. Inspect the user stories under `.specs/us/`.
3. Open one workflow and review graph, phase detail, and timeline.
4. Compare the same story later in the VS Code extension.

## VS Code Entry Point

Use this when you want the IDE workflow experience:

1. Open the repository in VS Code.
2. Run `npm run compile`.
3. Start the `Run SpecForge Extension` launch configuration from [../.vscode/launch.json](../.vscode/launch.json).
4. Open the `SpecForge.AI` activity bar view in the Extension Development Host.

## MCP-First Entry Point

Use this when you want to validate the external agent contract instead of the IDE UX.

The repository includes a packaged plugin bundle in `plugins/specforge-ai/`.

The expected non-VS Code flow is:

1. Copy or install `plugins/specforge-ai/` into the consumer repository as `.agents/plugins/specforge-ai/`.
2. Configure the MCP client to start `.agents/plugins/specforge-ai/mcp/SpecForge.McpServer`.
3. Use MCP tools for workflow operations.
4. Use `open_workflow_portal` when a human needs visual inspection or approval.

Minimal MCP config example:

```json
{
  "servers": {
    "specforge": {
      "type": "stdio",
      "command": "${workspaceFolder}/.agents/plugins/specforge-ai/mcp/SpecForge.McpServer",
      "args": []
    }
  }
}
```

## Consumer Repo In Debug

Use this when a sibling side project should consume the current local `SpecForge.AI` runtime instead of copying a standalone plugin snapshot.

1. In `SpecForge.AI`, run `npm run compile`.
2. In `SpecForge.AI`, run `./tools/sync-local-plugin-marketplace.sh`.
3. In the consumer repository, point direct MCP clients to `./.agents/plugins/specforge-ai/mcp/SpecForge.McpServer`.
4. Reload the consumer client after each runtime refresh when needed.

The detailed contract for this flow lives in [consumer-debug-runtime.md](consumer-debug-runtime.md).

## Product Surfaces

In practice, SpecForge currently ships across four surfaces:

- VS Code extension
- MCP server
- browser workflow portal
- packaged Codex/MCP plugin bundle

For the runtime boundary and responsibilities, see [architecture.md](architecture.md).

## Where To Go Next

- product framing: [product-vision.md](product-vision.md)
- model and agent setup: [model-configuration.md](model-configuration.md)
- runtime behavior and persistence: [runtime-and-persistence.md](runtime-and-persistence.md)
- contributor setup: [developer-onboarding.md](developer-onboarding.md)
