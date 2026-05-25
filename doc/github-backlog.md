# GitHub Backlog

Generated from open GitHub issues in `PinedaTec-EU/SpecForge.AI`. Do not edit manually; run `npm run backlog:sync`.

Count: 20

## Bugs

### SFB-027: Portal boot logs vscode redeclaration error

- GitHub issue: [#33](https://github.com/PinedaTec-EU/SpecForge.AI/issues/33)
- Type: `bug`
- State: `OPEN`
- Summary: 1. Open the workflow portal at `http://localhost:5128/?usId=US-0001`. 2. Open the browser developer console. 3. Refresh the page if needed. 4. Observe the startup error `SyntaxError: redeclaration of const vscode`.
- Severity: `medium`
- Assignees: unassigned
- Milestone: none

### SFB-035: Duplicate Show dropped control

- GitHub issue: [#35](https://github.com/PinedaTec-EU/SpecForge.AI/issues/35)
- Type: `bug`
- State: `OPEN`
- Summary: 1. Open the SpecForge workflow portal or sidebar. 2. Inspect the top-level sidebar controls. 3. Observe the standalone `Show dropped` button. 4. Open the sidebar contextual menu. 5. Observe that `Show dropped` already exists there as a men…
- Severity: `low`
- Assignees: unassigned
- Milestone: none

## Features

### SFF-032: Move user story form to main window

- GitHub issue: [#32](https://github.com/PinedaTec-EU/SpecForge.AI/issues/32)
- Type: `enhancement`
- State: `OPEN`
- Summary: 1. Triggering `Create new user story` from the sidebar no longer renders the intake form inside the sidebar. 2. The action opens a main-window creation experience. 3. The main-window experience supports the required fields and current crea…
- Priority: `P1`
- Assignees: unassigned
- Milestone: none

### SFF-036: Automatic user-story branch activation

- GitHub issue: [#36](https://github.com/PinedaTec-EU/SpecForge.AI/issues/36)
- Type: `enhancement`
- State: `OPEN`
- Summary: SpecForge should switch the repository branch automatically when the active user story changes across its main operator surfaces, so the working checkout stays aligned with the selected workflow context instead of relying on manual branch…
- Priority: `P1`
- Assignees: unassigned
- Milestone: none

### SFF-045: Review findings workflow

- GitHub issue: [#45](https://github.com/PinedaTec-EU/SpecForge.AI/issues/45)
- Type: `enhancement`
- State: `OPEN`
- Summary: SpecForge should turn failed review findings into tracked remediation work with status, owner, and retry history instead of leaving them only in free-form Markdown.
- Priority: `P1`
- Assignees: unassigned
- Milestone: none

### SFF-046: PR evidence pack

- GitHub issue: [#46](https://github.com/PinedaTec-EU/SpecForge.AI/issues/46)
- Type: `enhancement`
- State: `OPEN`
- Summary: SpecForge should generate a compact PR evidence pack that summarizes the workflow decision surface for reviewers from the artifacts, review verdict, validation evidence, timeline, and changed files.
- Priority: `P1`
- Assignees: unassigned
- Milestone: none

### SFF-047: Definition-of-Ready dashboard

- GitHub issue: [#47](https://github.com/PinedaTec-EU/SpecForge.AI/issues/47)
- Type: `enhancement`
- State: `OPEN`
- Summary: SpecForge should provide a Definition-of-Ready dashboard for refinement so operators can see which MVP dimensions are still missing, which questions still block progress, and why the selected rigor level is not yet satisfied.
- Priority: `P1`
- Assignees: unassigned
- Milestone: none

### SFF-052: Eval packs and reusable gate outputs

- GitHub issue: [#52](https://github.com/PinedaTec-EU/SpecForge.AI/issues/52)
- Type: `enhancement`
- State: `OPEN`
- Summary: Define and implement the first reusable eval-pack model for SpecForge so phase gates stop depending only on free-form markdown judgments and gain explicit pass criteria plus machine-readable outputs.
- Priority: `P1`
- Assignees: unassigned
- Milestone: none

### SFF-053: Stronger execution envelopes and permissions

- GitHub issue: [#53](https://github.com/PinedaTec-EU/SpecForge.AI/issues/53)
- Type: `enhancement`
- State: `OPEN`
- Summary: Strengthen SpecForge execution envelopes and permission enforcement so per-phase boundaries become real operational controls instead of primarily descriptive metadata.
- Priority: `P1`
- Assignees: unassigned
- Milestone: none

### SFF-043: Provider-neutral PR and issue integration

- GitHub issue: [#43](https://github.com/PinedaTec-EU/SpecForge.AI/issues/43)
- Type: `enhancement`
- State: `OPEN`
- Summary: SpecForge should broaden its external collaboration integration layer beyond the current GitHub-oriented PR publication path and support a provider-neutral issue/PR strategy.
- Priority: `P2`
- Assignees: unassigned
- Milestone: none

### SFF-044: Plugin packaging and release command

- GitHub issue: [#44](https://github.com/PinedaTec-EU/SpecForge.AI/issues/44)
- Type: `enhancement`
- State: `OPEN`
- Summary: SpecForge should provide a single packaging and release command for its plugin/runtime bundle so releases stop depending on a fragile sequence of manual compile, sync, validation, and packaging steps.
- Priority: `P2`
- Assignees: unassigned
- Milestone: none

### SFF-051: Harness backlog master issue

- GitHub issue: [#51](https://github.com/PinedaTec-EU/SpecForge.AI/issues/51)
- Type: `enhancement`
- State: `OPEN`
- Summary: Create a single canonical master issue for the remaining harness-engineering backlog so pending work stops living primarily in repository-local task Markdown and becomes traceable through linked GitHub issues.
- Priority: `P2`
- Assignees: unassigned
- Milestone: none

### SFF-054: Reusable harness profiles and bootstrap

- GitHub issue: [#54](https://github.com/PinedaTec-EU/SpecForge.AI/issues/54)
- Type: `enhancement`
- State: `OPEN`
- Summary: Package the current low-level harness settings into reusable named profiles so repositories can adopt a coherent operating mode with explicit overrides and governance locks.
- Priority: `P2`
- Assignees: unassigned
- Milestone: none

### SFF-034: Vertical default graph layout

- GitHub issue: [#34](https://github.com/PinedaTec-EU/SpecForge.AI/issues/34)
- Type: `enhancement`
- State: `OPEN`
- Summary: 1. Opening a workflow with no prior custom preference uses vertical layout. 2. The configuration UI exposes a dropdown/select with `Vertical` and `Horizontal` options. 3. Changing the configuration persists the selected default for later w…
- Priority: `P3`
- Assignees: unassigned
- Milestone: none

### SFF-055: SpecForge Central harness governance

- GitHub issue: [#55](https://github.com/PinedaTec-EU/SpecForge.AI/issues/55)
- Type: `enhancement`
- State: `OPEN`
- Summary: Extend SpecForge Central into a real harness-governance control plane that can publish harness policies and profiles, detect stale states, and expose portfolio-wide compliance and audit views.
- Priority: `P3`
- Assignees: unassigned
- Milestone: none

## Technical Debt

### SFT-037: Shared branch activation path and visibility

- GitHub issue: [#37](https://github.com/PinedaTec-EU/SpecForge.AI/issues/37)
- Type: `tech-debt`
- State: `OPEN`
- Summary: Branch activation should be consolidated into one reusable safe path with explicit operator-facing visibility, instead of being spread across ad hoc activation points with uneven diagnostics.
- Priority: `P1`
- Assignees: unassigned
- Milestone: none

### SFT-039: Expand portal behavior validation

- GitHub issue: [#39](https://github.com/PinedaTec-EU/SpecForge.AI/issues/39)
- Type: `tech-debt`
- State: `OPEN`
- Summary: The portal test suite should move beyond string-presence assertions and start validating behavior categories such as scope changes, selection fallback, modal lifecycle, persistence, and route normalization.
- Priority: `P1`
- Assignees: unassigned
- Milestone: none

### SFT-040: Exhaustive in-app portal validation

- GitHub issue: [#40](https://github.com/PinedaTec-EU/SpecForge.AI/issues/40)
- Type: `tech-debt`
- State: `OPEN`
- Summary: The migrated browser workflow portal still needs a full in-app browser validation pass with explicit evidence across its critical flows before the surface can be treated as stable.
- Priority: `P1`
- Assignees: unassigned
- Milestone: none

### SFT-038: Reduce duplicated portal UI logic

- GitHub issue: [#38](https://github.com/PinedaTec-EU/SpecForge.AI/issues/38)
- Type: `tech-debt`
- State: `OPEN`
- Summary: The portal and VS Code workflow surfaces still carry duplicated UI and interaction logic that should be reduced into cleaner shared semantics with explicit host ownership boundaries.
- Priority: `P2`
- Assignees: unassigned
- Milestone: none

### SFT-041: Portal release validation gate

- GitHub issue: [#41](https://github.com/PinedaTec-EU/SpecForge.AI/issues/41)
- Type: `tech-debt`
- State: `OPEN`
- Summary: Portal-affecting work should be blocked from completion until it includes explicit browser validation notes and updated automated coverage for the touched behavior class.
- Priority: `P2`
- Assignees: unassigned
- Milestone: none
