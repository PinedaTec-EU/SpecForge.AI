# Bug Registry

## Open Bugs

### SFB-001

- Bug code: `SFB-001`
- Discovery date: `2026-05-22`
- Status: `Open`
- Short description: Switching the repository branch in a single local checkout can hide or strand user stories created after returning to `main`, because user-story visibility currently depends on the active branch/worktree instead of a stable control workspace.
- Reproduction steps:
  1. Start from `main` in one local checkout.
  2. Create a user story and advance it until SpecForge creates or activates its work branch.
  3. Return manually to `main`.
  4. Create a second user story from that same checkout.
  5. Switch back to the first user story branch.
  6. Observe that the second user story may no longer appear from that branch context, even though it was created later in the same local repository.

### SFB-002

- Bug code: `SFB-002`
- Discovery date: `2026-05-22`
- Status: `Open`
- Short description: SpecForge lacks a documented fallback rule for tracking and repairing engine defects outside its own workflow engine, so a defect in the engine can block or delay the registration, visibility, and execution of the work needed to fix it.
- Reproduction steps:
  1. Assume a defect affects local user-story intake, indexing, or workflow execution inside SpecForge itself.
  2. Try to register and drive the repair work only through the same affected SpecForge engine/runtime.
  3. Observe that the defect can prevent or degrade task registration, visibility, ownership routing, or execution progress for the repair itself.

### SFB-003

- Bug code: `SFB-003`
- Discovery date: `2026-05-22`
- Status: `Fixed`
- Short description: The browser workflow portal renders ownership-visibility controls but does not honor them. It defaults to showing stories from other owners and does not handle watch, hide, or `Include other owners` commands.
- Reproduction steps:
  1. Start the workflow portal against a workspace that contains at least one user story owned by `cli-user` and another owned by a different developer.
  2. Open the portal sidebar and observe that stories from both owners are shown by default.
  3. Toggle `Include other owners`, watch, or hide controls in the sidebar.
  4. Observe that the portal view does not change because the CLI portal script does not handle those commands, even though the sidebar markup renders them.

### SFB-004

- Bug code: `SFB-004`
- Discovery date: `2026-05-22`
- Status: `Fixed`
- Short description: When ownership filtering leaves the sidebar with zero visible user stories, the sidebar falls back to the “Create your first user story” empty state and hides the compact action controls, preventing users from reopening view options and restoring other stories.
- Reproduction steps:
  1. Open the workflow portal or extension sidebar in a repository where user stories exist but none match the current owner scope.
  2. Observe that the sidebar shows “Create your first user story”.
  3. Observe that the usual compact action buttons, including sidebar view options, are missing.
  4. Observe that the user cannot recover the hidden stories from the sidebar without leaving that state.

### SFB-005

- Bug code: `SFB-005`
- Discovery date: `2026-05-22`
- Status: `Fixed`
- Short description: The sidebar user-story card used an ambiguous circle visibility control, left a visual gap before the action rail, duplicated hide/show state inside the context menu, and exposed repair/reset actions in the CLI portal without wiring them to runnable endpoints.
- Reproduction steps:
  1. Open the workflow portal sidebar with at least one visible user story card.
  2. Observe that the second action button shows a circle instead of an eye and does not clearly reflect effective sidebar visibility.
  3. Open the story context menu and observe that `Hide from my list` duplicates the intended visibility action.
  4. Trigger `Analyze / Repair` or `Reset workflow` from the CLI portal and observe that nothing happens because the renderer is missing the corresponding portal handlers.

### SFB-006

- Bug code: `SFB-006`
- Discovery date: `2026-05-22`
- Status: `Fixed`
- Short description: The sidebar `Edit US info` action was wired as a navigation shortcut to the capture source instead of a real metadata edit flow, so users could not update owner, title, category, or tags from the story card actions.
- Reproduction steps:
  1. Open the sidebar or CLI workflow portal on a repository with at least one user story.
  2. Open the context menu for that story and click `Edit US info`.
  3. Observe that the UI either just navigates to the capture/source view or appears to do nothing when already there.
  4. Observe that no metadata editing flow is presented for owner, title, category, or tags.

### SFB-007

- Bug code: `SFB-007`
- Discovery date: `2026-05-22`
- Status: `Fixed`
- Short description: The edit metadata flow reused the rendered story title as the edit default, so stories whose visible title already included the `US-xxxx` prefix could save that prefix back into metadata and duplicate it on the next render.
- Reproduction steps:
  1. Open a story whose current title is rendered as `US-xxxx · ...`.
  2. Trigger `Edit US info` and accept the default title without removing the prefix.
  3. Save the metadata update.
  4. Observe that the story can later render as `US-xxxx · US-xxxx · ...`.

### SFB-008

- Bug code: `SFB-008`
- Discovery date: `2026-05-22`
- Status: `Fixed`
- Short description: The CLI workflow portal implemented `Edit US info` with `window.prompt()`, which is not supported in the in-app browser, so the action failed before the user could edit or persist metadata.
- Reproduction steps:
  1. Open the workflow portal in the in-app browser.
  2. Open a user-story context menu and click `Edit US info`.
  3. Observe the browser error `prompt() is not supported.`
  4. Observe that no metadata form appears and no edits can be saved.

### SFB-009

- Bug code: `SFB-009`
- Discovery date: `2026-05-22`
- Status: `Fixed`
- Short description: The CLI workflow portal stopped honoring `Include other owners` from the sidebar `view options` menu because the iframe menu click did not reliably reach the parent portal state handler, leaving the scope unchanged.
- Reproduction steps:
  1. Open the workflow portal on a story whose owner is outside the current user scope or where the default scope hides the available stories.
  2. Open `Sidebar view options`.
  3. Click `Include other owners`.
  4. Observe that the sidebar remains in the same scope and the portal URL does not reflect `sidebarOtherOwners=true`.

### SFB-010

- Bug code: `SFB-010`
- Discovery date: `2026-05-22`
- Status: `Fixed`
- Short description: The CLI workflow portal renderer could fail to boot with `500 Broken pipe` because the parent-side bridge for sidebar scope toggles introduced an invalid nested template literal, making `render-cli-workflow-html.js` unparsable by Node.
- Reproduction steps:
  1. Start the CLI workflow portal after the sidebar toggle bridge change.
  2. Request `http://localhost:5128/?usId=US-0001`.
  3. Observe that the portal returns `500`.
  4. Inspect the renderer script with `node -c tools/render-cli-workflow-html.js` and observe the syntax error near `querySelectorAll`.
