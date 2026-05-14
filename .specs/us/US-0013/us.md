# US-0013 · Enforce Central-locked policies in local runtimes

## Metadata
- Kind: `feature`
- Category: `configuration`
- Tags: `sf-central`

## Objective
As a platform owner, I want SpecForge Central to mark selected policy settings as mandatory and locked, so connected local runtimes cannot bypass company rules through local prompt overrides, model routing changes, relaxed review evidence settings, or workflow customization.

Problem to solve:
SpecForge Central already has planned protected configuration bundles and client synchronization. The missing enterprise control is runtime enforcement: a connected repository must know which settings are centrally locked, disable local editing for those settings, and refuse workflow actions that would violate mandatory Central policy.

Initial scope for discussion:
- represent Central policy locks for prompts, prompt overrides, category catalogs, provider/model allowlists, phase routing, review evidence policy, workflow customization, approve-review-anyway permissions, and release gates
- make locked settings visible in VS Code, the browser portal, and MCP responses
- disable or block local customization where Central marks a setting mandatory
- keep local/offline operation possible only when the last known Central policy allows it
- record policy enforcement and violations in local audit artifacts without moving workflow truth out of the repository

Questions before implementation:
- Which settings must be lockable in the first version: prompt overrides, reviewEvidencePolicy, provider routing, workflow customization, or release gates?
- Should a policy violation block the action, warn only, or leave the workflow in a waiting-admin state?
- How should offline clients behave when their cached Central policy is stale?
- Which roles can override a locked policy, if any?

## Initial Scope
- Includes:
  - ...
- Excludes:
  - ...
