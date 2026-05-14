# SpecForge.AI · Runtime And Persistence

This guide explains how SpecForge behaves at runtime and where workflow state lives.

## Core Runtime Rule

Workflow state should be changed through SpecForge operations, not by manually editing `.specs/` files, unless the task is explicit low-level repair work.

In practice, use:

- the VS Code extension
- MCP tools
- the browser workflow portal

## Domain Capabilities

The current core already supports:

- creating a user story root
- persisting `state.yaml` and `branch.yaml`
- validating categories against `.specs/config.yaml`
- advancing to the next valid phase
- approving phases that require approval
- creating work branch metadata from approved spec baseline
- generating phase artifacts and timeline entries
- serving embedded prompts with lazy disk overrides
- composing effective prompts from runtime state and templates

## Current VS Code Surface

The extension currently provides:

- user-story intake and import
- workflow graph and per-phase detail
- artifact preview and prompt access
- file management split between `context files` and `user story info`
- persisted runtime status to avoid duplicate long-running executions
- audit timeline and playback controls
- settings for model profiles, agent profiles, routing, and selected feature flags

Current limitations:

- `stop` is still best-effort, not durable job control
- prompt diffing and effective prompt inspection are not finished
- completed-work visibility and sidebar search are still pending

## Packaged MCP Surface

The packaged bundle exposes:

- `specforge_query` for reads
- `specforge_action` for workflow mutations
- `specforge_prompts` for prompt-template operations
- `open_workflow_portal` for browser inspection

This is the intended boundary for non-VS Code agent clients.

## User Story Layout

Each user story lives under:

```text
.specs/us/<category>/<US-ID>/
```

Typical contents:

```text
.specs/us/workflow/US-0001/
  us.md
  refinement.md
  state.yaml
  runtime.yaml
  branch.yaml
  timeline.md
  context/
  attachments/
  restarts/
  phases/
    00-refinement.md
    01-spec.md
    02-technical-design.md
    03-implementation.md
    04-review.md
```

Per-user VS Code preferences are stored separately:

```text
.specs/users/<local-user>/vscode-preferences.json
```

## Workflow Readability

The workflow distinguishes between:

- automatic phases the system can execute
- explicit human checkpoints that require approval

Today the key checkpoints are:

- `spec` as the approved baseline
- `release-approval` as the final human release gate

For the canonical workflow semantics, see [workflow-canonico-fase-1.md](workflow-canonico-fase-1.md).

## Related Docs

- getting started: [getting-started.md](getting-started.md)
- model setup: [model-configuration.md](model-configuration.md)
- target architecture: [architecture.md](architecture.md)
