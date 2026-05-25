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
- `../ai-skills-shared/.shared-skills/skills/github-issue-authoring/SKILL.md`
- This global skill applies whenever creating or updating GitHub issues.
- In this repository, it must be used with the local repository acronym `SF`.
- `./.codex/skills/specforge-frontend-runtime-guardrails/SKILL.md`
- This local skill applies to SpecForge webviews, workflow rendering, CLI portal runtime, and browser bridge entrypoints.
- It exists to prevent repo-specific renderer and runtime monoliths that generic shared skills do not describe in enough detail.
- `./.codex/skills/functional-commit-version-bump/SKILL.md`
- This local skill applies after completing any new functionality or functional subtask.
- It enforces a functional commit followed by a separate version bump commit using the repository version bumper.

## Local Rules

- In local development, runtime environment variables must come from the `.env` file referenced by `.vscode/launch.json`. Do not duplicate those variables in `launchSettings.json`, `tasks.json`, or tracked configuration files unless there is an exceptional and explicit need.
- Technical articles intended for GitHub Pages or public documentation must be written in English unless the user explicitly requests another language.
- This repository must not use SpecForge-on-itself workflow tracking for its own product development. Do not create, import, advance, approve, regress, or maintain repository-development user stories under this repository's `.specs/**`.
- The only approved place to develop SpecForge through SpecForge workflow artifacts is `specforge-ai-central`. When SpecForge product work is managed through the product itself, that management must live there, not in this repository.
- In this repository, any `.specs/**` content is product runtime data, test/sample fixture material, or implementation evidence for the engine itself. It is not the canonical backlog, task tracker, or feature register for changes to this repository.
- Embedded workflow portal surfaces must not use `iframe` boundaries for first-party UI such as the user-story sidebar, configuration modal, or other portal-owned interactive shells. Keep them in a single DOM with a single state owner to avoid cross-frame event bridging, close-state bugs, and browser-integration regressions.
- Backlog tracking in this repository must use GitHub Issues as the canonical tracker. Use `bug` for defects, `enhancement` for features, and `tech-debt` for technical debt.
- Open bug issues in this repository must carry a `Severity` property using `critical`, `high`, `medium`, or `low`.
- Open non-bug backlog issues in this repository must carry a `Priority` property using `P0`, `P1`, `P2`, or `P3`.
- When a local open-work cache is needed in this repository, use the generated unified backlog file `doc/github-backlog.md` via `node tools/sync-github-backlog.js`. Do not maintain that file by hand.
- The repository-local script `tools/sync-github-backlog.js` must stay aligned with the shared template at `../ai-skills-shared/rules/conventions/sync-github-backlog.js`.
- Do not maintain separate local backlog mirrors by issue type or status such as `bugs-open.md`, `features-open.md`, `tech-debt-open.md`, `bug-registry.md`, or any `*-closed.md` files.
- Local Markdown under `doc/` must remain roadmap, architecture, reference, planning context, or the generated `github-backlog.md` cache, not a parallel execution ledger.
- Bug issue titles must stay short. Keep the detailed defect description in the issue body.
- GitHub issue authoring in this repository must use the global skill `../ai-skills-shared/.shared-skills/skills/github-issue-authoring/SKILL.md`.
- The issue body structure must come from the matching global final template under `../ai-skills-shared/.shared-skills/skills/github-issue-authoring/references/final-templates/`.
- Do not maintain repository-local GitHub issue templates for this repository. The global templates are the only drafting source of truth.
- Issue titles must stay short and start with the repository issue code in fixed-width numeric form: `AAX-000: <brief title>`.
- In this repository, use `SF` as the repository acronym, so valid prefixes are `SFB`, `SFF`, `SFT`, `SFD`, and `SFI`.
- The final letter in the code identifies the issue type.
- Semantic slug codes are forbidden. Do not create codes like `BUG-PORTAL-LOCAL-IMAGES-NOT-LOADING`.
- GitHub issue bodies and subsequent issue comments must be written in English unless the user explicitly requests another language.
- GitHub issue bodies and subsequent issue comments must use rich Markdown, not plain text, when recording analysis, evidence, decisions, progress, or structured follow-up.
- When issue metadata is atomic, write it as a property in the form `Key: value`, not as a markdown section with a single value underneath.
- The issue code must appear in the title only, not repeated in the body.
- Use property-style metadata for fields such as `Discovery date`, `Reported by`, `Audience`, `Environment`, and similar single-value fields.
- When a local file in this repository mirrors, registers, or references a GitHub issue, include the GitHub issue number and the full GitHub issue URL in that local entry so the issue can be followed from either side.
- From this repository onward, each completed functional task or subtask must be closed with both required commits: first a functional git commit that maps clearly to the delivered change, then a separate version bump commit produced with `dotnet versionbumper`. Each commit message must include the corresponding `done` outcome so the repository history can be traced back to the task checklist.
- From this repository onward, any change set that reaches a local compile or validation milestone (`npm run compile`, `npm run compile:ts`, `npm run test:ts`, `dotnet build`, or equivalent) must be followed by a version bump through `dotnet versionbumper`. Do not defer the bump to a later session or batch it with unrelated compiled changes.
- Do not treat those commit/version rules as optional cleanup. If a task reaches a functional outcome, the functional commit is required. If the task also reaches a compile or validation milestone, the separate version bump commit is required in the same delivery flow before the task is considered complete.
- If the repository is left with local changes for a functional task, state explicitly whether the mandatory functional commit and the mandatory version bump commit are still pending. Do not imply the work is complete while those required steps remain undone.
- When a task touches production code, add or adjust tests where reasonably possible and run the narrowest meaningful validation for the changed area. If tests are not added, explain why.
- When a task touches VS Code UI, workflow views, portal surfaces, webviews, or other interactive flows, leave at least a minimal UI proof when feasible: browser validation, smoke interaction, reproducible manual verification, screenshot evidence, or equivalent. If that proof is not feasible in the session, say so explicitly.

## Priority Order

1. System or tool-session instructions.
2. Provider-specific instructions (`CLAUDE.md`, `COPILOT.md`, `CODEX.md`, `.codex/AGENTS.md`).
3. This `AGENTS.md` file.
4. `../ai-skills-shared/AGENTS.md`.
5. Applicable shared skills in `../ai-skills-shared/.shared-skills/skills/*`.
6. The user prompt for the current task.
