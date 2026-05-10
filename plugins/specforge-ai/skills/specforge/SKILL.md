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

## Viewing User Stories

When the user asks to see, view, open, review, or inspect a specific user story, the default SF behavior is to open the workflow portal, not just read `.specs` files:

```bash
dotnet run --project src/SpecForge.Runner.Cli/SpecForge.Runner.Cli.csproj -- serve-workflow <workspaceRoot> <usId> <urlPrefix>
```

Choose an available localhost port for `<urlPrefix>` and open the URL in the browser/in-app browser. Use `specforge_query` and Markdown file reads only as supporting context, or when the user explicitly asks for raw text.

## User-Gated Workflow

When the user asks to use SpecForge, keep the model inside the SpecForge workflow. Tell the user what will happen before each major step: user-story creation, refinement, specification, technical design, implementation, review, release approval, branch creation, and PR preparation.

Do not skip user validation gates. If the workflow is waiting for user approval, branch details, or any other user answer, stop and ask for that input through the relevant SpecForge gate instead of continuing locally.

Do not implement the requested change outside SpecForge just because an artifact exists or a gate is pending. Implementation may begin only after the SpecForge workflow reaches the implementation phase and the required user approval or branch gate has been completed through the MCP action.

## Intake Depth Gate

When the user gives a broad, vague, or low-detail request, do not immediately create a user story or run implementation. First run an intake conversation and ask the minimum set of concrete questions needed to reach implementation-ready detail.

Ask targeted questions until you can state, without guessing:

- the target user or actor;
- the business outcome and workflow trigger;
- the exact behavior expected in the product;
- inputs, outputs, states, data, and integrations involved;
- scope boundaries and explicit out-of-scope items;
- acceptance criteria and important failure or edge cases;
- priority order and dependencies between slices.

Keep the questions practical and grouped. Prefer 3-7 high-signal questions per round; do another round only when answers still leave blocking ambiguity. Do not invent client requirements to avoid asking.

After the intake is concrete enough, split the goal into small, independently reviewable user stories. Each story must deliver one narrow functional increment, have clear acceptance intent, avoid mixing unrelated concerns, and be small enough to pass through SpecForge without over-engineering.

Refinement applies the configured MVP rigor:

- `low`: lean MVP detail; still requires actor, outcome, main flow, and at least one observable acceptance criterion.
- `medium`: professional MVP detail; requires behavior, data/state impact, boundaries, dependencies, edge cases, and acceptance criteria.
- `high`: exacting MVP detail; requires client-ready clarity on UI/API contracts, alternate paths, integrations, non-goals, validation evidence, and dependencies.

Auto-refinement answers are disabled by default. If disabled, warn the user that unresolved refinement questions will need their direct answers; do not treat that as a blocker or enable it automatically.

## Common Reads

- List stories: `specforge_query` with `query: "list_user_stories"`.
- Inspect one story: `specforge_query` with `query: "workflow"` and `usId`.
- Check readiness: `specforge_query` with `query: "current_phase"` and `usId`.
- Check runtime: `specforge_query` with `query: "runtime_status"` and `usId`.
- Analyze inconsistencies: `specforge_query` with `query: "lineage"` and `usId`.

## Common Mutations

Call `specforge_action` with `workspaceRoot`, `action`, optional top-level `usId`, and action-specific `params`.

- Create: `action: "create_user_story"`.
- Create multiple small stories from a concrete goal: `action: "create_user_stories_from_goal"`.
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
