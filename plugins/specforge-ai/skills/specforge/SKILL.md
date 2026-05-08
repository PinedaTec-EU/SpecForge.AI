---
name: specforge
description: Use SpecForge.AI from Codex through the compact MCP facade. Trigger for creating, inspecting, advancing, approving, regressing, or operating on SpecForge user stories.
---

# SpecForge.AI

Use the SpecForge MCP server as the authoritative boundary for workflow state.

## Hard Rule

Do not manually edit `.specs/**` workflow state, phase artifacts, generated Markdown, `state.yaml`, `branch.yaml`, or `timeline.md` to perform workflow actions. Use the compact MCP facade:

- `specforge_query` for reads.
- `specforge_action` for mutations.
- `specforge_prompts` for prompt-template operations.

Manual file edits are allowed only for explicit low-level repair when the MCP cannot perform the requested operation and the user confirms that risk.

## Workspace

Use the current repository root as `workspaceRoot` unless the user provides another absolute path.

## User-Gated Workflow

When the user asks to use SpecForge, keep the model inside the SpecForge workflow. Tell the user what will happen before each major step: user-story creation, refinement, specification, technical design, implementation, review, release approval, branch creation, and PR preparation.

Do not skip user validation gates. If the workflow is waiting for user approval, branch details, or any other user answer, stop and ask for that input through the relevant SpecForge gate instead of continuing locally.

Do not implement the requested change outside SpecForge just because an artifact exists or a gate is pending. Implementation may begin only after the SpecForge workflow reaches the implementation phase and the required user approval or branch gate has been completed through the MCP action.

## Common Reads

- List stories: `specforge_query` with `query: "list_user_stories"`.
- Inspect one story: `specforge_query` with `query: "workflow"` and `usId`.
- Check readiness: `specforge_query` with `query: "current_phase"` and `usId`.
- Check runtime: `specforge_query` with `query: "runtime_status"` and `usId`.
- Analyze inconsistencies: `specforge_query` with `query: "lineage"` and `usId`.

## Common Mutations

Call `specforge_action` with `workspaceRoot`, `action`, optional top-level `usId`, and action-specific `params`.

- Create: `action: "create_user_story"`.
- Import: `action: "import_user_story"`.
- Continue: `action: "advance_phase"`.
- Approve: `action: "approve_phase"`.
- Answer refinement: `action: "submit_refinement_answers"`.
- Answer approval: `action: "submit_approval_answer"`.
- Apply artifact operation: `action: "operate_artifact"`.
- Regress: `action: "request_regression"`.
- Rewind: `action: "rewind_workflow"`.
- Restart from edited source: `action: "restart_from_source"`.
- Reopen completed work: `action: "reopen_completed"`.

Always report returned artifact paths, phase, status, blocking reason, and commit outcome when present.
