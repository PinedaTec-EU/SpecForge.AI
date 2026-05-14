# SpecForge.AI · Developer Onboarding

This guide is for contributors working on the product itself.

## What You Are Working On

SpecForge is not a single app. The main development surfaces are:

- `src-vscode/` for the VS Code extension
- `src/SpecForge.Domain/` for workflow/domain logic
- `src/SpecForge.McpServer/` for the MCP server
- `src/SpecForge.Runner.Cli/` for the browser workflow portal
- `plugins/specforge-ai/` for the packaged local plugin bundle

The source of truth for workflow state remains the repository under `.specs/`.

## Bootstrap

Typical setup:

```bash
git clone git@github.com:<your-user>/SpecForge.AI.git
cd SpecForge.AI
git remote add upstream git@github.com:PinedaTec-EU/SpecForge.AI.git
git fetch upstream
npm install
npm run compile
dotnet test SpecForge.AI.slnx
```

Optional TypeScript tests:

```bash
npm run test:ts
```

## Read In This Order

1. [../README.md](../README.md)
2. [architecture.md](architecture.md)
3. [workflow-canonico-fase-1.md](workflow-canonico-fase-1.md)
4. [mcp-contract-fase-1.md](mcp-contract-fase-1.md)

That sequence gives the product boundary before implementation details.

## Main Local Entry Points

### VS Code extension development

Use this for explorer UX, workflow views, or extension wiring.

1. Open the repo in VS Code.
2. Run `npm run compile`.
3. Start `Run SpecForge Extension` from [../.vscode/launch.json](../.vscode/launch.json).

### Browser workflow portal

Use this for workflow presentation, approval flows, or portal UX.

```bash
./launch.sh
```

### Backend or contract work

Use this for domain, MCP, or runtime behavior changes.

```bash
dotnet test SpecForge.AI.slnx
```

If the change affects the public MCP-facing behavior, also validate the packaged plugin bundle under `plugins/specforge-ai/`.

## Working Rules

- sync with remote before new edit blocks
- change workflow state through SpecForge operations, not by manually editing `.specs/`, unless the task is explicit repair work
- keep docs aligned when workflow semantics change
- validate the narrowest real subsystem that matches the change

Repository process also expects completed functional work to end with a functional commit and a separate version bump commit.

## Files Worth Knowing

- [../README.md](../README.md)
- [../AGENTS.md](../AGENTS.md)
- [../CODEX.md](../CODEX.md)
- [../package.json](../package.json)
- [../launch.sh](../launch.sh)
- [../.vscode/launch.json](../.vscode/launch.json)
- [../src-vscode/extension.ts](../src-vscode/extension.ts)
- [../src/SpecForge.McpServer/Program.cs](../src/SpecForge.McpServer/Program.cs)
- [../src/SpecForge.Runner.Cli/Program.cs](../src/SpecForge.Runner.Cli/Program.cs)
