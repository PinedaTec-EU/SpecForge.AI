# AGENTS

This repository consumes shared skills from `../ai-skills-shared`.

## Official Source

- The valid shared rules live in `../ai-skills-shared/AGENTS.md`.
- The shared skills live in `../ai-skills-shared/.shared-skills/skills/*`.
- Do not duplicate or edit domain rules in this repository unless the deviation is local and explicit.

## Active Skills For This Repository

- `../ai-skills-shared/.shared-skills/skills/terraform/SKILL.md`
- `../ai-skills-shared/.shared-skills/skills/terraform/k3s-environments.md`
- `../ai-skills-shared/.shared-skills/skills/terraform/k8s-modules.md`

## Local Process Skill

- `./.codex/skills/sdd-phase-agents/SKILL.md`
- This local skill applies only to the repository's SDD engineering workflow.
- It does not replace or duplicate shared domain skills.
- `./.codex/skills/functional-commit-version-bump/SKILL.md`
- This local skill applies after completing any new functionality or functional subtask.
- It enforces a functional commit followed by a separate version bump commit using the repository version bumper.

## Local Rules

- In local development, runtime environment variables must come from the `.env` file referenced by `.vscode/launch.json`. Do not duplicate those variables in `launchSettings.json`, `tasks.json`, or tracked configuration files unless there is an exceptional and explicit need.
- All SpecForge Central work must be created and evolved through the SpecForge tool itself. User stories, workflow state, generated specs, technical designs, implementation/review artifacts, and other `.specs/**` workflow outputs for SpecForge Central must be created, imported, advanced, approved, regressed, or repaired through the SpecForge MCP/actions instead of manual file edits, except for explicit low-level repair after the tool cannot perform the operation and the user confirms that risk.
- From this repository onward, each completed functional task or subtask must be closed with both required commits: first a functional git commit that maps clearly to the delivered change, then a separate version bump commit produced with `dotnet versionbumper`. Each commit message must include the corresponding `done` outcome so the repository history can be traced back to the task checklist.

## Priority Order

1. System or tool-session instructions.
2. Provider-specific instructions (`CLAUDE.md`, `COPILOT.md`, `CODEX.md`, `.codex/AGENTS.md`).
3. This `AGENTS.md` file.
4. `../ai-skills-shared/AGENTS.md`.
5. Applicable shared skills in `../ai-skills-shared/.shared-skills/skills/*`.
6. The user prompt for the current task.
