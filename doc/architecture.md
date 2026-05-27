# SpecForge · Target Architecture

## Components

### 1. VS Code Extension

Responsibilities:

- present user stories and their state
- trigger user actions
- open markdown artifacts
- observe manual changes in relevant artifacts
- show the current flow and active phase
- act as the client for the MCP backend

Non-responsibilities:

- deciding transitions
- executing workflow logic
- persisting domain rules outside defined contracts

### 2. MCP Server

Responsibilities:

- govern the SDD workflow
- expose the workflow to non-VS Code clients through a local `stdio` MCP boundary
- expose managed-repository catalog operations for SpecForge Central
- validate transitions and regressions
- apply approval policies
- invoke LLM providers through an abstraction
- persist and recover technical state
- emit traceable results and events
- start the packaged browser workflow portal when an MCP client requests visual inspection

### 3. Self-Contained Workflow Portal

Responsibilities:

- provide a browser UI for user-story inspection and operation without requiring VS Code
- render the workflow graph, current phase, artifacts, audit trail, runtime state, and available actions
- collect human approval, refinement, regression, rewind, reopen, and configuration input
- persist execution settings under `.specs/configuration/settings.json`
- reuse the same domain/application services as the MCP server and VS Code extension

Non-responsibilities:

- replacing the MCP boundary for workflow mutations
- storing separate workflow truth outside the repository-local `.specs/` tree
- acting as a central multi-repository catalog in phase 1

### 4. SpecForge Central

Responsibilities:

- maintain the catalog of managed repositories
- register, edit, disable, and remove repository references
- show repository readiness and workflow status across the portfolio
- route create, import, inspect, and continue actions to the selected repository
- act as the primary managed gateway for shared execution tools that require cross-repository policy, centrally managed secrets, shared retrieval infrastructure, or portfolio-wide audit

Non-responsibilities:

- storing repository-local workflow artifacts as the primary truth
- deleting repositories or `.specs/` data when a catalog entry is removed
- executing a single user story across several repositories in phase 1
- becoming the mandatory path for trivial current-repository reads or basic local repository intelligence

### 5. Repository As Source Of Truth

Responsibilities:

- store human-facing artifacts in markdown
- store minimum technical state
- version workflows, templates, and decisions
- allow context reconstruction in another environment

## Main Design Rule

The extension, MCP clients, self-contained workflow portal, and central portal orchestrate interaction. The MCP/domain boundary decides lifecycle. SpecForge Central selects and monitors repositories. Each repository preserves traceability for its own workflows.

For the broader product boundary, treat SpecForge.AI as the local governed runtime and SpecForge Central as the managed control plane and governed gateway for shared organizational intelligence. See [specforge-and-central.md](specforge-and-central.md) and [execution-tool-packaging.md](execution-tool-packaging.md).

For the browser workflow portal specifically, page-level state ownership must stay explicit. Global portal state belongs to the parent shell, repository truth belongs to the backend/domain, and host-specific UI behavior must not leak browser-only routing or iframe assumptions into the VS Code extension. The frozen contract is documented in [portal-state-contract.md](portal-state-contract.md).

## Non-VS Code Runtime

The repository ships a packaged local plugin at `plugins/specforge-ai/` so agent environments that do not host the VS Code extension can still operate SpecForge.

The packaged runtime contains:

- the `SpecForge.McpServer` `stdio` server
- the `SpecForge.Runner.Cli` workflow portal server
- compiled workflow rendering assets
- Codex-style skills that instruct agents to use MCP tools instead of editing `.specs/**` manually
- a relative `.mcp.json` suitable for installation under `.agents/plugins/specforge-ai/`

For non-VS Code clients, the operational contract is:

1. The agent talks to `SpecForge.McpServer` over MCP.
2. Reads go through `specforge_query`.
3. Mutations go through `specforge_action`.
4. Prompt-template operations go through `specforge_prompts`.
5. Human visual inspection goes through `open_workflow_portal`, which starts `SpecForge.Runner.Cli serve-workflow` from the packaged runtime when possible.

This keeps `.specs/**` as repository truth while allowing Codex, terminal agents, or other MCP-capable tools to use the same workflow without VS Code.

## Initial Canonical Workflow

1. Create or import a user story.
2. Generate refinement if needed.
3. Generate the formalized spec during spec.
4. Approve the spec baseline and create the work branch.
5. Generate technical design.
6. Implement.
7. Review.
8. Regress or advance based on findings.

## Recommended Minimum Persistence

- markdown for human-readable artifacts
- `yaml` for transactional state, configuration, and user-story technical metadata
- `timeline.md` for human-readable audit history, with the option to evaluate `yaml` later if additional structured processing becomes necessary

Practical rule:

- do not duplicate phase inputs in `input.md` if the system can infer them from the previously approved phase and the active state pointers

## Open Stack Decision

Viable MCP options:

- `TypeScript`: lower initial friction and alignment with the extension
- `C#`: stronger support for complex domain logic and contracts

For a serious product base, the preferred option is `C#` in the backend and `TypeScript` in the extension, while keeping contract-level decoupling through MCP.
