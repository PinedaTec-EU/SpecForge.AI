# US-0011 · Inspect effective prompts and prompt override diffs

## Metadata
- Kind: `feature`
- Category: `prompts`

## Objective
As a SpecForge maintainer or platform lead, I want to inspect the effective prompt used for a phase and compare repository overrides against embedded templates, so prompt changes are reviewable instead of hidden inside model execution.

Problem to solve:
SpecForge already embeds phase prompts, supports lazy repository overrides, composes effective prompts, and records warnings when overrides differ from embedded templates. The missing product surface is a clear inspection and diff workflow that lets users see what the agent will actually receive before or after a phase runs.

Initial scope for discussion:
- show the embedded template, repository override, and composed effective prompt for a selected phase
- show template-vs-override differences without forcing users to inspect raw files manually
- make prompt warnings actionable from VS Code and the browser workflow portal
- keep prompt overrides as repository-local files under .specs/prompts
- avoid turning prompt editing into a separate prompt management platform

Questions before implementation:
- Should the first version be read-only inspection, diff-only, or allow editing from the same surface?
- Should effective prompts be persisted as execution receipts, generated on demand, or both?
- Which users need this most: maintainers, reviewers, or phase operators?
- What prompt content must be redacted before display or export?

## Initial Scope
- Includes:
  - ...
- Excludes:
  - ...
