---
name: advance-user-story
description: Advance or approve a SpecForge user-story phase through MCP.
---

# Advance User Story

Before advancing, call `specforge_query` with `query: "current_phase"` and inspect `canExecute` or blocking details when present.

Before calling any mutation, briefly tell the user which SpecForge phase or gate you are about to operate and what the next expected step is. If the current phase is blocked on user validation, branch creation details, or approval, do not call `advance_phase` and do not implement locally. Submit only the explicit user answer through the matching gate action, or ask the user for the missing input.

To run the current phase:

```json
{
  "workspaceRoot": "<absolute repo path>",
  "usId": "US-123",
  "action": "advance_phase",
  "params": { "actor": "user" }
}
```

To approve the current phase:

```json
{
  "workspaceRoot": "<absolute repo path>",
  "usId": "US-123",
  "action": "approve_phase",
  "params": {
    "baseBranch": "main",
    "workBranch": "feature/us-123",
    "actor": "user"
  }
}
```

Report phase, status, generated artifact path, token usage, and commit result when returned.
