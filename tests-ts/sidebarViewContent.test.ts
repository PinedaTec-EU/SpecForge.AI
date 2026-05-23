import test from "node:test";
import assert from "node:assert/strict";
import { buildSidebarHtml } from "../src-vscode/sidebarViewContent";
import type { SidebarViewModel } from "../src-vscode/sidebarViewContent";

function model(overrides: Record<string, unknown>): SidebarViewModel {
  const userStories = Array.isArray(overrides.userStories)
    ? overrides.userStories.map((story) => ({
        createdBy: "alice",
        owner: "alice",
        ...story
      }))
    : [];
  return {
    hasWorkspace: true,
    showCreateForm: false,
    busyMessage: null,
    promptsInitialized: true,
    settingsConfigured: true,
    settingsMessage: null,
    starredUserStoryId: null,
    activeWorkflowUsId: null,
    runtimeVersion: null,
    viewMode: "category",
    showDroppedUserStories: false,
    showCompletedUserStories: false,
    showBlockedUserStories: false,
    showHiddenUserStories: false,
    searchIncludesOtherOwners: false,
    currentActor: "alice",
    watchingUserStoryIds: [],
    hiddenUserStoryIds: [],
    maxVisibleUserStories: null,
    totalUserStoryCount: 0,
    droppedUserStoryCount: 0,
    categories: ["workflow"],
    ...overrides,
    userStories
  } as SidebarViewModel;
}

test("buildSidebarHtml does not block first user story creation when prompt overrides are absent", () => {
  const html = buildSidebarHtml(model({
    promptsInitialized: false,
    categories: ["workflow", "ux"],
    userStories: []
  }));

  assert.match(html, /Create your first user story/);
  assert.match(html, /Create User Story/);
  assert.doesNotMatch(html, /aria-label="Initialize repo prompts"/);
  assert.doesNotMatch(html, /Workflow backlog/);
});

test("buildSidebarHtml shows a single prominent create action when prompts are initialized and there are no user stories", () => {
  const html = buildSidebarHtml(model({
    promptsInitialized: true,
    categories: ["workflow", "ux"],
    userStories: []
  }));

  assert.match(html, /Create your first user story/);
  assert.match(html, /Create User Story/);
  assert.doesNotMatch(html, /aria-label="Reinitialize repo prompts"/);
  assert.doesNotMatch(html, /Workflow backlog/);
});

test("buildSidebarHtml keeps compact actions visible when the current ownership scope is empty", () => {
  const html = buildSidebarHtml(model({
    totalUserStoryCount: 4,
    currentActor: "alice",
    categories: ["workflow"],
    userStories: []
  }));

  assert.match(html, /No user stories in current scope/);
  assert.match(html, /Showing 0 of 4 stories in scope · owner alice\./);
  assert.match(html, /Sidebar view options/);
  assert.match(html, /Include other owners/);
  assert.doesNotMatch(html, /Create your first user story/);
});

test("buildSidebarHtml renders the embedded creation form inside the sidebar", () => {
  const html = buildSidebarHtml(model({
    showCreateForm: true,
    createFileMode: "context",
    createFiles: [
      {
        sourcePath: "/tmp/service.cs",
        name: "service.cs",
        kind: "context"
      }
    ],
    categories: ["workflow", "ux"],
    userStories: []
  }));

  assert.match(html, /Create from the sidebar/);
  assert.match(html, /create-user-story-form/);
  assert.match(html, /Guided Wizard/);
  assert.match(html, /Minimum/);
  assert.match(html, /Recommended/);
  assert.match(html, /<textarea name="sourceText"/);
  assert.match(html, /data-create-field="wizard\.actor"/);
  assert.match(html, /Generated Source Preview/);
  assert.match(html, /<option value="workflow">workflow<\/option>/);
  assert.match(html, /Drag &amp; Drop Files|Drag & Drop Files/);
  assert.match(html, /data-create-dropzone/);
  assert.match(html, /data-command="setCreateFileMode" data-kind="context"/);
  assert.match(html, /data-command="setCreateFileKind"/);
  assert.match(html, /data-command="removeCreateFile"/);
  assert.match(html, /Remove service\.cs/);
  assert.match(html, /service\.cs/);
});

test("buildSidebarHtml exposes a compact prompt customization action", () => {
  const html = buildSidebarHtml(model({
    categories: ["workflow"],
    userStories: [{
      usId: "US-0001",
      title: "Workflow graph",
      category: "workflow",
      currentPhase: "spec",
      status: "waiting-user",
      mainArtifactPath: "/tmp/us.md",
      directoryPath: "/tmp/us.US-0001",
      workBranch: null
    }],
  }));

  assert.match(html, /aria-label="Prompt actions"/);
  assert.match(html, /aria-label="Sidebar view options"/);
  assert.match(html, /Export All Prompts/);
  assert.match(html, /Customize Prompt Templates/);
  assert.match(html, /aria-label="Create new user story"/);
  assert.doesNotMatch(html, /aria-label="Configure execution providers"/);
  assert.match(html, /data-story-search/);
  assert.match(html, /Search by title, description, category, owner, or #tag/);
  assert.match(html, /data-command="toggleSearchIncludesOtherOwners"[\s\S]*Include other owners/);
  assert.doesNotMatch(html, /data-command="toggleViewMode"/);
  assert.doesNotMatch(html, /Repo prompts ready/);
});

test("buildSidebarHtml can hide local view options when the host owns them", () => {
  const html = buildSidebarHtml(model({
    showViewOptionsMenu: false,
    categories: ["workflow"],
    userStories: [{
      usId: "US-0001",
      title: "Workflow graph",
      category: "workflow",
      currentPhase: "spec",
      status: "active",
      mainArtifactPath: "/tmp/us.md",
      directoryPath: "/tmp/us.US-0001",
      workBranch: null
    }],
  }));

  assert.doesNotMatch(html, /aria-label="Sidebar view options"/);
  assert.match(html, /aria-label="Prompt actions"/);
  assert.match(html, /aria-label="Create new user story"/);
});

test("buildSidebarHtml trims commit metadata from the displayed runtime version", () => {
  const html = buildSidebarHtml(model({
    runtimeVersion: "0.1.4.415+71ff1a243f81f3eea815e2df4bcb1c39be185a98",
    categories: ["workflow"],
    userStories: [{
      usId: "US-0001",
      title: "Workflow graph",
      category: "workflow",
      currentPhase: "spec",
      status: "active",
      mainArtifactPath: "/tmp/us.md",
      directoryPath: "/tmp/us.US-0001",
      workBranch: null
    }],
  }));

  assert.match(html, /v\.0\.1\.4\.415/);
  assert.doesNotMatch(html, /71ff1a243f81f3eea815e2df4bcb1c39be185a98/);
});

test("buildSidebarHtml uses compact actions instead of a separate create card when stories already exist", () => {
  const html = buildSidebarHtml(model({
    categories: ["workflow"],
    userStories: [{
      usId: "US-0001",
      title: "Workflow graph",
      category: "workflow",
      currentPhase: "spec",
      status: "active",
      mainArtifactPath: "/tmp/us.md",
      directoryPath: "/tmp/us.US-0001",
      workBranch: null
    }],
  }));

  assert.match(html, /compact-actions/);
  assert.match(html, /aria-label="Create new user story"/);
  assert.match(html, /aria-label="Star US-0001"/);
  assert.match(html, /aria-label="User story actions for US-0001"/);
  assert.match(html, /Edit US info/);
  assert.match(html, /Analyze \/ Repair/);
  assert.match(html, /data-command="resetUserStoryToCapture"/);
  assert.match(html, /Reset workflow/);
  assert.doesNotMatch(html, /data-command="deleteUserStory"/);
  assert.match(html, /story-card--active story-card--phase-spec/);
  assert.match(html, /story-card__phase-label">SPEC</);
  assert.match(html, /data-story-search-text="[^"]*Workflow graph[^"]*workflow/);
  assert.doesNotMatch(html, /Start another user story/);
  assert.doesNotMatch(html, /Keep the backlog focused on active work/);
});

test("buildSidebarHtml includes user story descriptions in the local search index", () => {
  const html = buildSidebarHtml(model({
    categories: ["workflow"],
    userStories: [{
      usId: "US-0005",
      title: "Compact cards",
      description: "Allow fast filtering from the sidebar command area.",
      category: "workflow",
      currentPhase: "spec",
      status: "active",
      mainArtifactPath: "/tmp/us.md",
      directoryPath: "/tmp/us.US-0005",
      workBranch: null
    }],
  }));

  assert.match(html, /data-story-search-text="[^"]*fast filtering[^"]*workflow/);
});

test("buildSidebarHtml renders one prefixed custom tag beside the id and includes every prefixed tag in search", () => {
  const html = buildSidebarHtml(model({
    categories: ["workflow"],
    userStories: [{
      usId: "US-0006",
      title: "Tagged cards",
      category: "workflow",
      tags: ["mcp", "ux", "search"],
      currentPhase: "spec",
      status: "active",
      mainArtifactPath: "/tmp/us.md",
      directoryPath: "/tmp/us.US-0006",
      workBranch: null
    }],
  }));

  assert.match(html, /<span class="story-card__head">\s*<span class="story-card__id">US-0006<\/span>\s*<span class="story-card__tags"><span class="story-card__tag">#mcp<\/span><\/span>\s*<\/span>/);
  assert.doesNotMatch(html, /<span class="story-card__tag">#ux<\/span>/);
  assert.doesNotMatch(html, /<span class="story-card__tag">#search<\/span>/);
  assert.match(html, /data-story-search-text="[^"]*#mcp #ux #search/);
});

test("buildSidebarHtml surfaces blocked dependency state in story rows", () => {
  const html = buildSidebarHtml(model({
    showBlockedUserStories: true,
    categories: ["workflow"],
    userStories: [{
      usId: "US-0002",
      title: "Dependent workflow",
      category: "workflow",
      currentPhase: "capture",
      status: "active",
      mainArtifactPath: "/tmp/us.md",
      directoryPath: "/tmp/us.US-0002",
      workBranch: null,
      dependencies: [{
        usId: "US-0001",
        title: "First workflow",
        currentPhase: "capture",
        status: "active",
        isSatisfied: false,
        missingReason: null
      }]
    }],
  }));

  assert.match(html, /story-row--status-blocked/);
  assert.match(html, /story-card--status-blocked/);
  assert.match(html, /capture · blocked/);
  assert.match(html, /<span class="story-card__phase-label">🔒 BLOCK<\/span>/);
  assert.match(html, /blocked by US-0001/);
  assert.match(html, /data-story-search-text="[^"]*US-0001[^"]*First workflow/);
});

test("buildSidebarHtml keeps blocked filter toggle available when blocked stories are in scope", () => {
  const html = buildSidebarHtml(model({
    categories: ["workflow"],
    userStories: [{
      usId: "US-0004",
      title: "Blocked story",
      category: "workflow",
      currentPhase: "capture",
      status: "blocked",
      mainArtifactPath: "/tmp/us.md",
      directoryPath: "/tmp/us.US-0004",
      workBranch: null
    }],
  }));

  assert.match(html, /story-row--status-blocked/);
  assert.match(html, /<span class="story-card__phase-label">🔒 BLOCK<\/span>/);
  assert.match(html, />Show blocked<\/span>/);
  assert.doesNotMatch(html, /Show blocked \(1\)/);
  assert.match(html, /data-command="toggleBlockedUserStories"[^>]*aria-checked="false"/);
  assert.doesNotMatch(html, /data-command="toggleBlockedUserStories"[^>]*disabled/);
});

test("buildSidebarHtml shows blocked user stories when enabled", () => {
  const html = buildSidebarHtml(model({
    showBlockedUserStories: true,
    categories: ["workflow"],
    userStories: [{
      usId: "US-0004",
      title: "Blocked story",
      category: "workflow",
      currentPhase: "capture",
      status: "blocked",
      mainArtifactPath: "/tmp/us.md",
      directoryPath: "/tmp/us.US-0004",
      workBranch: null
    }],
  }));

  assert.match(html, /story-row--status-blocked/);
  assert.match(html, /<span class="story-card__phase-label">🔒 BLOCK<\/span>/);
  assert.match(html, />Show blocked<\/span>/);
  assert.doesNotMatch(html, /Show blocked \(1\)/);
  assert.match(html, /aria-checked="true"/);
});

test("buildSidebarHtml keeps the phase rail for user stories that are still in progress", () => {
  const html = buildSidebarHtml(model({
    categories: ["workflow"],
    userStories: [{
      usId: "US-0002",
      title: "Waiting story",
      category: "workflow",
      currentPhase: "technical-design",
      status: "waiting-user",
      mainArtifactPath: "/tmp/us.md",
      directoryPath: "/tmp/us.US-0002",
      workBranch: null
    }],
  }));

  assert.match(html, /story-card--active story-card--phase-technical-design/);
  assert.match(html, /story-card--status-waiting-user/);
  assert.match(html, /<span class="story-card__phase-label">TECH<\/span>/);
});

test("buildSidebarHtml uses the paused phase rail tone when a story is paused by the user", () => {
  const html = buildSidebarHtml(model({
    categories: ["workflow"],
    userStories: [{
      usId: "US-0004",
      title: "Paused story",
      category: "workflow",
      currentPhase: "implementation",
      status: "paused",
      mainArtifactPath: "/tmp/us.md",
      directoryPath: "/tmp/us.US-0004",
      workBranch: null
    }],
  }));

  assert.match(html, /story-card--status-paused/);
});

test("buildSidebarHtml labels error stories explicitly on the phase rail", () => {
  const html = buildSidebarHtml(model({
    categories: ["workflow"],
    userStories: [{
      usId: "US-0006",
      title: "Errored story",
      category: "workflow",
      currentPhase: "unknown-phase",
      status: "error",
      mainArtifactPath: "/tmp/us.md",
      directoryPath: "/tmp/us.US-0006",
      workBranch: null
    }],
  }));

  assert.match(html, /story-card--status-error/);
  assert.match(html, /<span class="story-card__phase-label">ERROR<\/span>/);
});

test("buildSidebarHtml keeps completed filter toggle available when completed stories are in scope", () => {
  const html = buildSidebarHtml(model({
    categories: ["workflow"],
    userStories: [{
      usId: "US-0003",
      title: "Completed story",
      category: "workflow",
      currentPhase: "pr-preparation",
      status: "completed",
      mainArtifactPath: "/tmp/us.md",
      directoryPath: "/tmp/us.US-0003",
      workBranch: null
    }],
  }));

  assert.match(html, /story-row--status-completed/);
  assert.match(html, /<span class="story-card__phase-label">DONE<\/span>/);
  assert.match(html, />Show completed<\/span>/);
  assert.doesNotMatch(html, /Show completed \(1\)/);
  assert.match(html, /data-command="toggleCompletedUserStories"[^>]*aria-checked="false"/);
  assert.doesNotMatch(html, /data-command="toggleCompletedUserStories"[^>]*disabled/);
});

test("buildSidebarHtml disables sidebar filter options when there are no matching stories", () => {
  const html = buildSidebarHtml(model({
    categories: ["workflow"],
    userStories: [{
      usId: "US-0001",
      title: "Active story",
      category: "workflow",
      currentPhase: "spec",
      status: "active",
      mainArtifactPath: "/tmp/us.md",
      directoryPath: "/tmp/us.US-0001",
      workBranch: null
    }],
  }));

  assert.match(html, /data-command="toggleCompletedUserStories"[^>]*aria-checked="false"[^>]*disabled[\s\S]*Show completed/);
  assert.match(html, /data-command="toggleBlockedUserStories"[^>]*aria-checked="false"[^>]*disabled[\s\S]*Show blocked/);
});

test("buildSidebarHtml shows completed user stories with a purple completed rail when enabled", () => {
  const html = buildSidebarHtml(model({
    showCompletedUserStories: true,
    categories: ["workflow"],
    userStories: [{
      usId: "US-0003",
      title: "Completed story",
      category: "workflow",
      currentPhase: "completed",
      status: "completed",
      mainArtifactPath: "/tmp/us.md",
      directoryPath: "/tmp/us.US-0003",
      workBranch: null
    }],
  }));

  assert.match(html, /story-row--status-completed/);
  assert.match(html, /story-card--active story-card--phase-completed story-card--status-completed/);
  assert.match(html, /<span class="story-card__phase-label">DONE<\/span>/);
  assert.match(html, /aria-checked="true"/);
  assert.match(html, /rgba\(190, 136, 255/);
});

test("buildSidebarHtml shows prompt override guidance above the backlog when reported", () => {
  const html = buildSidebarHtml(model({
    promptsInitialized: false,
    promptsMessage: "Missing 2 required prompt file(s): .specs/prompts/prompts.yaml, .specs/prompts/shared/system.md.",
    categories: ["workflow"],
    userStories: [{
      usId: "US-0001",
      title: "Workflow graph",
      category: "workflow",
      currentPhase: "spec",
      status: "active",
      mainArtifactPath: "/tmp/us.md",
      directoryPath: "/tmp/us.US-0001",
      workBranch: null
    }],
  }));

  assert.match(html, /Export embedded prompts when needed/);
  assert.match(html, /Customize Prompt Templates/);
  assert.match(html, /Missing 2 required prompt file\(s\)/);
  assert.match(html, /aria-label="Create new user story"/);
});

test("buildSidebarHtml exposes a visible settings warning when execution is not configured", () => {
  const html = buildSidebarHtml(model({
    promptsInitialized: false,
    settingsConfigured: false,
    settingsMessage: "SpecForge.AI is not configured for the current provider. Missing base URL, API key, model.",
    categories: [],
    userStories: []
  }));

  assert.match(html, /Configuration Required/);
  assert.match(html, /SpecForge\.AI settings are incomplete/);
  assert.match(html, /Open Execution Form/);
  assert.match(html, /⚠/);
});

test("buildSidebarHtml surfaces the model warning when the deterministic fallback is active", () => {
  const html = buildSidebarHtml(model({
    promptsInitialized: false,
    settingsConfigured: false,
    settingsMessage: "SpecForge.AI needs at least one configured model profile before workflow stages can run.",
    categories: [],
    userStories: [{
      usId: "US-0001",
      title: "Workflow graph",
      category: "workflow",
      currentPhase: "capture",
      status: "active",
      mainArtifactPath: "/tmp/us.md",
      directoryPath: "/tmp/us.US-0001",
      workBranch: null
    }]
  }));

  assert.match(html, /configured model profile/);
  assert.match(html, /Open Execution Form/);
});

test("buildSidebarHtml shows a busy indicator and disables actions while a sidebar operation is running", () => {
  const html = buildSidebarHtml(model({
    busyMessage: "Exporting prompt templates...",
    promptsInitialized: false,
    categories: ["workflow"],
    userStories: []
  }));

  assert.match(html, /Working/);
  assert.match(html, /Exporting prompt templates\.\.\./);
  assert.match(html, /const busy = true/);
});

test("buildSidebarHtml marks the starred user story with a highlighted star action", () => {
  const html = buildSidebarHtml(model({
    starredUserStoryId: "US-0009",
    categories: ["workflow"],
    userStories: [{
      usId: "US-0009",
      title: "Pinned workflow",
      category: "workflow",
      currentPhase: "implementation",
      status: "active",
      mainArtifactPath: "/tmp/us.md",
      directoryPath: "/tmp/us.US-0009",
      workBranch: null
    }]
  }));

  assert.match(html, /story-star--active/);
  assert.match(html, /class="icon-action story-star story-star--active"\s+type="button"\s+data-command="toggleStarredUserStory"\s+data-us-id="US-0009"/);
  assert.match(html, /aria-label="Unstar US-0009"/);
  assert.match(html, />★</);
});

test("buildSidebarHtml renders owner scope, watch action, and owner scope view option", () => {
  const html = buildSidebarHtml(model({
    searchIncludesOtherOwners: false,
    currentActor: "alice",
    watchingUserStoryIds: ["US-0012"],
    totalUserStoryCount: 3,
    categories: ["workflow"],
    userStories: [{
      usId: "US-0012",
      title: "Owned workflow",
      description: "Track user-specific scope in the sidebar.",
      createdBy: "alice",
      owner: "alice",
      category: "workflow",
      currentPhase: "implementation",
      status: "waiting-user",
      mainArtifactPath: "/tmp/us.md",
      directoryPath: "/tmp/us.US-0012",
      workBranch: null
    }]
  }));

  assert.match(html, /Showing 1 of 3 stories in scope · owner alice\./);
  assert.match(html, /placeholder="Search by title, description, category, owner, or #tag"/);
  assert.match(html, /data-command="toggleSearchIncludesOtherOwners"[\s\S]*Include other owners/);
  assert.match(html, /aria-label="Hide US-0012 from my sidebar"/);
  assert.match(html, /story-watch--active/);
  assert.match(html, /owner alice/);
  assert.match(html, /data-story-search-text="[^"]*Track user-specific scope[^"]*alice/);
});

test("buildSidebarHtml uses the eye control for local sidebar visibility and keeps hidden toggle in view options only", () => {
  const html = buildSidebarHtml(model({
    hiddenUserStoryIds: ["US-0013"],
    showHiddenUserStories: true,
    categories: ["workflow"],
    userStories: [{
      usId: "US-0013",
      title: "Hidden workflow",
      createdBy: "alice",
      owner: "alice",
      category: "workflow",
      currentPhase: "capture",
      status: "active",
      mainArtifactPath: "/tmp/us.md",
      directoryPath: "/tmp/us.US-0013",
      workBranch: null
    }]
  }));

  assert.match(html, /data-command="toggleSidebarVisibilityUserStory" data-us-id="US-0013" data-owner="alice"/);
  assert.match(html, /aria-label="Show US-0013 in my sidebar"/);
  assert.match(html, /data-command="toggleShowHiddenUserStories"[\s\S]*Show hidden/);
  assert.doesNotMatch(html, /Hide from my list|Unhide from my list/);
});

test("buildSidebarHtml wires user story row actions to selectable commands", () => {
  const html = buildSidebarHtml(model({
    categories: ["workflow"],
    userStories: [{
      usId: "US-0010",
      title: "US-0010 · Edit metadata",
      category: "workflow",
      tags: ["ux", "cli"],
      currentPhase: "capture",
      status: "active",
      mainArtifactPath: "/tmp/us.md",
      directoryPath: "/tmp/us.US-0010",
      workBranch: null
    }]
  }));

  assert.match(html, /class="story-card[^"]*" type="button" data-command="openWorkflow" data-us-id="US-0010"/);
  assert.match(html, /data-command="showEditUserStoryForm"[\s\S]*data-us-id="US-0010"[\s\S]*data-title="Edit metadata"[\s\S]*data-owner="alice"[\s\S]*data-category="workflow"[\s\S]*data-tags="ux, cli"[\s\S]*role="menuitem">[\s\S]*<span class="action-menu__item-icon" aria-hidden="true">✎<\/span>[\s\S]*<span>Edit US info<\/span>/);
  assert.match(html, /data-command="toggleSidebarVisibilityUserStory" data-us-id="US-0010" data-owner="alice"/);
  assert.doesNotMatch(html, /<button class="action-menu__item" type="button" role="menuitem" disabled>\s+<span class="action-menu__item-icon" aria-hidden="true">✎<\/span>\s+<span>Edit US info<\/span>/);
});

test("buildSidebarHtml keeps dropped story cards selectable", () => {
  const html = buildSidebarHtml(model({
    activeWorkflowUsId: "US-0011",
    showDroppedUserStories: true,
    categories: ["workflow"],
    userStories: [{
      usId: "US-0011",
      title: "Dropped workflow",
      category: "workflow",
      currentPhase: "capture",
      status: "active",
      mainArtifactPath: "/tmp/us.md",
      directoryPath: "/tmp/us.US-0011",
      workBranch: null
    }]
  }));

  assert.match(html, /Dropped backlog/);
  assert.match(html, /story-row--selected/);
  assert.match(html, /class="story-card[^"]*" type="button" data-command="openWorkflow" data-us-id="US-0011"/);
  assert.doesNotMatch(html, /<button class="story-card[^"]*" type="button" disabled>/);
});

test("buildSidebarHtml scrolls the selected user story row as high as possible", () => {
  const html = buildSidebarHtml(model({
    activeWorkflowUsId: "US-0011",
    categories: ["workflow"],
    userStories: [{
      usId: "US-0011",
      title: "Selected workflow",
      category: "workflow",
      currentPhase: "capture",
      status: "active",
      mainArtifactPath: "/tmp/us.md",
      directoryPath: "/tmp/us.US-0011",
      workBranch: null
    }]
  }));

  assert.match(html, /story-row--selected/);
  assert.match(html, /function keepSelectedStoryVisible\(\)/);
  assert.match(html, /querySelector\("\.story-row--selected"\)/);
  assert.match(html, /scrollIntoView\(\{ block: "start" \}\)/);
  assert.match(html, /requestAnimationFrame\(keepSelectedStoryVisible\)/);
});

test("buildSidebarHtml raises the active story row above neighboring cards while its menu is open", () => {
  const html = buildSidebarHtml(model({
    categories: ["workflow"],
    userStories: [{
      usId: "US-0011",
      title: "Stacked workflow",
      category: "workflow",
      currentPhase: "capture",
      status: "active",
      mainArtifactPath: "/tmp/us.md",
      directoryPath: "/tmp/us.US-0011",
      workBranch: null
    }]
  }));

  assert.match(html, /\.story-row--menu-open\s*\{\s*z-index: 120;\s*\}/);
  assert.match(html, /closest\("\.story-row"\)\?\.classList\.add\("story-row--menu-open"\)/);
  assert.match(html, /closest\("\.story-row"\)\?\.classList\.remove\("story-row--menu-open"\)/);
});
