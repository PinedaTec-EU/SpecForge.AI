# US-0012 · Package review evidence for human decision making

## Metadata
- Kind: `feature`
- Category: `review`

## Objective
As a reviewer or engineering lead, I want SpecForge to package review evidence into a concise decision view, so I can approve, reject, regress, or accept residual risk without reconstructing the workflow from raw artifacts.

Problem to solve:
SpecForge already has review artifacts, review evidence policy, validation strategy classification, timeline events, runtime receipts, implementation artifacts, and explicit regression or override actions. The missing product surface is a review evidence pack that gathers the relevant information next to the decision.

Initial scope for discussion:
- summarize the approved spec, technical design validation strategy, implementation result, review result, evidence gaps, and risk notes
- link to the exact artifacts and timeline events used for the decision
- make blocking vs non-blocking evidence gaps visible according to the configured review evidence policy
- support human actions: approve, regress, request rework, or approve review anyway with a reason
- keep the pack generated from repository-local .specs artifacts rather than becoming a separate source of truth

Questions before implementation:
- Should the first evidence pack be a generated Markdown artifact, a portal view, or both?
- Which evidence belongs in the first version: validation checklist, changed files, commits, test output, prompt/runtime receipts, or only phase artifacts?
- Should a review evidence pack be required before release approval?
- How should the pack later connect to PR descriptions once issue/PR integration exists?

## Initial Scope
- Includes:
  - ...
- Excludes:
  - ...
