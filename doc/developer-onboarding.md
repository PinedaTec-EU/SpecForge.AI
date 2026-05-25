# SpecForge.AI · Developer Onboarding

This guide is for contributors working on the product itself.

## What You Are Working On

SpecForge is not a single app. The main development surfaces are:

- `src-vscode/` for the VS Code extension
- `src/SpecForge.Domain/` for workflow/domain logic
- `src/SpecForge.McpServer/` for the MCP server
- `src/SpecForge.Runner.Cli/` for the browser workflow portal
- `plugins/specforge-ai/` for the packaged local plugin bundle

The source of truth for SpecForge runtime behavior remains the repository under `.specs/`, but that is a product/runtime concern, not the task-tracking method for developing this repository itself.

This repo must not be managed through its own `.specs/**` workflow artifacts. If SpecForge product development is handled through SpecForge workflow, the approved place for that is `specforge-ai-central`, not this repository.

## Methodology First

Before changing code, internalize the operating model behind the product:

- `SDD`: the spec is the governing contract and phases advance only through explicit workflow rules.
- `Harness engineering`: SpecForge is the execution-control layer around models, tools, permissions, evidence, and audit.
- `Structured criticism before commitment`: the `spec` phase requires criticism and reconstruction before approval; review and release also operate through explicit gates rather than informal trust.
- `Human checkpoints with reversible flow`: approval, regression, rewind, restart, and reopen are part of the method, not exceptional escape hatches.
- `Phase-specialized agents`: model routing, agent profiles, repository access, and optional subagents are phase controls, not cosmetic configuration.

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
2. [sdd-seven-layers.md](sdd-seven-layers.md)
3. [harness-engineering-checklist.md](harness-engineering-checklist.md)
4. [architecture.md](architecture.md)
5. [workflow-canonico-fase-1.md](workflow-canonico-fase-1.md)
6. [mcp-contract-fase-1.md](mcp-contract-fase-1.md)

That sequence gives the product boundary, methodology, and control model before implementation details.

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
- treat `.specs/**` here as engine/runtime data, not as the backlog or feature tracker for this repository
- change workflow state through SpecForge operations, not by manually editing `.specs/`, unless the task is explicit repair work on the product/runtime behavior itself
- keep docs aligned when workflow semantics change
- validate the narrowest real subsystem that matches the change
- close every completed functional change with a functional commit first, then a separate `dotnet versionbumper` commit
- after `npm run compile`, `npm run compile:ts`, `npm run test:ts`, `dotnet build`, or equivalent validation milestones, the version bump is mandatory and must not be deferred
- if a task is intentionally left uncommitted, say so explicitly and call out that the required commit/version-bump flow is still pending

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
