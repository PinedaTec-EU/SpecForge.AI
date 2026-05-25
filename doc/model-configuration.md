# SpecForge.AI · Model Configuration

By default, phase execution uses a deterministic local engine.

To enable model-backed execution, configure at least one model profile and one agent profile.

## Supported Providers

Current `provider` values:

- `openai-compatible`
- `codex`
- `copilot`
- `claude`

Notes:

- `codex` uses the native local Codex CLI.
- `openai-compatible` uses the HTTP chat-completions path.
- `copilot` and `claude` currently route through the same HTTP bridge while preserving provider identity in audit and routing metadata.

## Minimal Model Profile

```json
{
  "name": "light",
  "provider": "openai-compatible",
  "baseUrl": "http://localhost:11434/v1",
  "apiKey": "",
  "model": "llama3.1",
  "repositoryAccess": "none"
}
```

If `provider` is omitted, SpecForge defaults it to `openai-compatible`.

## Minimal Agent Profile

```json
{
  "name": "planner",
  "role": "Planning agent",
  "modelProfile": "light",
  "instructions": "Clarify scope, preserve traceability, and avoid code changes.",
  "repositoryAccess": "read"
}
```

## Phase Routing Example

```json
{
  "specForge.execution.modelProfiles": [
    {
      "name": "planner-model",
      "provider": "copilot",
      "baseUrl": "https://api.example.test/v1",
      "apiKey": "<your-api-key>",
      "model": "gpt-4.1-mini"
    },
    {
      "name": "codex-model",
      "provider": "codex"
    },
    {
      "name": "review-model",
      "provider": "claude",
      "baseUrl": "https://api.example.test/v1",
      "apiKey": "<your-api-key>",
      "model": "claude-sonnet"
    }
  ],
  "specForge.execution.agentProfiles": [
    {
      "name": "planner",
      "role": "Planning agent",
      "modelProfile": "planner-model",
      "instructions": "Clarify requirements and keep downstream phases grounded.",
      "repositoryAccess": "read"
    },
    {
      "name": "implementer",
      "role": "Implementation agent",
      "modelProfile": "codex-model",
      "instructions": "Edit the repository and keep changes scoped to the approved design.",
      "repositoryAccess": "read-write"
    },
    {
      "name": "reviewer",
      "role": "Review agent",
      "modelProfile": "review-model",
      "instructions": "Review behavior, tests, and release risk before approval.",
      "repositoryAccess": "read-write"
    }
  ],
  "specForge.execution.phaseAgents": {
    "defaultAgent": "planner",
    "implementationAgent": "implementer",
    "reviewAgent": "reviewer"
  }
}
```

## Repository Access Rules

Agent `repositoryAccess` is enforced:

- `none`
- `read`
- `read-write`

Typical expectations:

- refinement, spec, technical design, release approval, and PR preparation require `read`
- implementation and review require `read-write`

## Codex CLI Override

SpecForge auto-discovers Codex from `/Applications/Codex.app/Contents/Resources/codex` or `PATH`.

To force a specific binary:

```bash
export SPECFORGE_CODEX_CLI_PATH="/Applications/Codex.app/Contents/Resources/codex"
```

## Review And Refinement Tolerance

Supported tolerance levels:

- `strict`
- `balanced`
- `inferential`

Environment variables:

```bash
export SPECFORGE_REFINEMENT_TOLERANCE=balanced
export SPECFORGE_REVIEW_TOLERANCE=balanced
export SPECFORGE_REVIEW_EVIDENCE_POLICY=balanced
```

Evidence policy values:

- `strict`
- `balanced`
- `release`
- `advisory`

## Subagents

These phase-local subagent flags are off by default:

```json
{
  "specForge.execution.technicalDesignSubagentsEnabled": true,
  "specForge.execution.reviewSubagentsEnabled": true
}
```

Technical design uses repository, solution-planning, and validation-strategy subagents. Review uses functional, technical, and release-risk auditors before final artifact synthesis.

## Related Docs

- runtime behavior: [runtime-and-persistence.md](runtime-and-persistence.md)
- external contract: [reference/mcp-contract.md](reference/mcp-contract.md)
- product framing: [product-vision.md](product-vision.md)
