import test from "node:test";
import assert from "node:assert/strict";
import * as fs from "node:fs";
import * as path from "node:path";

const scriptPath = path.join(process.cwd(), "tools", "render-cli-workflow-html.js");
const packagedScriptPath = path.join(process.cwd(), "plugins", "specforge-ai", "mcp", "tools", "render-cli-workflow-html.js");

test("CLI workflow shim posts refinement answers to the workflow portal API", async () => {
  const script = await fs.promises.readFile(scriptPath, "utf8");

  assert.match(script, /message\?\.command === "submitRefinementAnswers"/);
  assert.match(script, /fetch\("\/api\/refinement-answers"/);
  assert.match(script, /JSON\.stringify\(\{ answers: message\.answers, actor: specForgeCliCurrentActor \}\)/);
  assert.match(script, /window\.location\.reload\(\)/);
});

test("CLI workflow shim rejects malformed refinement answer commands locally", async () => {
  const script = await fs.promises.readFile(scriptPath, "utf8");

  assert.match(script, /Array\.isArray\(message\.answers\)/);
  assert.doesNotMatch(script, /submitRefinementAnswers" && message\.answers/);
});

test("CLI workflow renderer shows runtime version only in the sidebar header", async () => {
  const script = await fs.promises.readFile(scriptPath, "utf8");

  assert.match(script, /displayRuntimeVersion = formatRuntimeVersion\(payload\.runtimeVersion \?\? workflow\?\.lastRuntimeVersion \?\? workflow\?\.createdWithRuntimeVersion \?\? null\)/);
  assert.match(script, /runtimeVersion: null/);
  assert.doesNotMatch(script, /runtimeVersion: payload\.runtimeVersion \?\? workflow\?\.lastRuntimeVersion \?\? workflow\?\.createdWithRuntimeVersion \?\? null/);
});

test("CLI workflow renderer embeds the reusable user-story sidebar with collapsed actions", async () => {
  const script = await fs.promises.readFile(scriptPath, "utf8");

  assert.match(script, /buildSidebarHtml/);
  assert.match(script, /showViewOptionsMenu: false/);
  assert.match(script, /grid-template-columns: minmax\(0, 1fr\) auto auto auto/);
  assert.match(script, /SpecForge\.AI/);
  assert.match(script, /data-cli-sidebar-pin/);
  assert.match(script, /data-cli-sidebar-settings/);
  assert.match(script, /runtimeVersion: null/);
  assert.match(script, /sidebarPin\.setAttribute\("aria-pressed", collapsed \? "false" : "true"\)/);
  assert.match(script, /configurationProvidersUrl = payload\.configurationProvidersUrl \|\| configurationPortalUrl/);
  assert.match(script, /configurationAdvancedUrl = payload\.configurationAdvancedUrl \|\| configurationPortalUrl/);
  assert.match(script, /data-cli-config-overlay/);
  assert.match(script, /openConfiguration\(\$\{JSON\.stringify\(configurationPortalUrl\)\}\)/);
  assert.match(script, /openConfiguration\(\$\{JSON\.stringify\(configurationProvidersUrl\)\}\)/);
  assert.doesNotMatch(script, /openConfiguration\(\$\{JSON\.stringify\(configurationAdvancedUrl\)\}\)/);
});

test("CLI workflow renderer groups sidebar stories by category", async () => {
  const script = await fs.promises.readFile(scriptPath, "utf8");

  assert.match(script, /viewMode: "category"/);
  assert.doesNotMatch(script, /viewMode: "phase"/);
  assert.match(script, /searchIncludesOtherOwners: options\.includeOtherOwners/);
});

test("CLI workflow renderer falls back to the embedded workflow configuration route", async () => {
  const script = await fs.promises.readFile(scriptPath, "utf8");
  const packagedScript = await fs.promises.readFile(packagedScriptPath, "utf8");

  assert.match(script, /configurationPortalUrl = payload\.configurationPortalUrl \|\| "http:\/\/localhost:5128\/configuration"/);
  assert.match(packagedScript, /configurationPortalUrl = payload\.configurationPortalUrl \|\| "http:\/\/localhost:5128\/configuration"/);
  assert.doesNotMatch(script, /configurationPortalUrl = payload\.configurationPortalUrl \|\| "http:\/\/localhost:5127\//);
  assert.doesNotMatch(packagedScript, /configurationPortalUrl = payload\.configurationPortalUrl \|\| "http:\/\/localhost:5127\//);
});

test("CLI workflow renderer routes sidebar story selection through the current portal", async () => {
  const script = await fs.promises.readFile(scriptPath, "utf8");

  assert.match(script, /const sidebarMessageHandlers = \{/);
  assert.match(script, /openWorkflow\(message\) \{/);
  assert.match(script, /navigateToUserStory\(message\.usId, null, null\)/);
  assert.match(script, /url\.searchParams\.set\("usId", message\.usId\)/);
  assert.match(script, /activeWorkflowUsId: workflow\?\.usId \|\| null/);
});

test("CLI workflow renderer supports a no-selection portal state", async () => {
  const script = await fs.promises.readFile(scriptPath, "utf8");

  assert.match(script, /const workflow = payload\.workflow \|\| null/);
  assert.match(script, /function buildEmptyWorkflowPageHtml\(reason\)/);
  assert.match(script, /<h1>No user story selected<\/h1>/);
  assert.match(script, /workflow \? buildWorkflowHtml\(workflow, state, "idle", "", ""\) : buildEmptyWorkflowPageHtml\(payload\.noSelectionReason\)/);
  assert.match(script, /const renderedWorkflowUsId = \$\{JSON\.stringify\(workflow\?\.usId \?\? null\)\}/);
});

test("CLI workflow renderer routes sidebar edit action through metadata update prompts", async () => {
  const script = await fs.promises.readFile(scriptPath, "utf8");
  const packagedScript = await fs.promises.readFile(packagedScriptPath, "utf8");
  const packagedSidebar = await fs.promises.readFile(
    path.join(process.cwd(), "plugins", "specforge-ai", "mcp", "dist", "sidebarViewContent.js"),
    "utf8");

  for (const content of [script, packagedScript]) {
    assert.match(content, /showEditUserStoryForm\(message\) \{/);
    assert.match(content, /data-cli-edit-overlay/);
    assert.match(content, /const openEditUserStoryForm = \(message\) =>/);
    assert.match(content, /editForm\?\.addEventListener\("submit", event =>/);
    assert.match(content, /requestJson\("\/api\/update-user-story-info", \{ usId, title, owner, category, tags, actor: currentActor \}\)/);
    assert.match(content, /setEditError\("Title, owner, and category are required\."\)/);
    assert.doesNotMatch(content, /window\.prompt\(/);
    assert.match(content, /window\.location\.reload\(\)/);
    assert.match(content, /data-cli-edit-assign-to-me/);
    assert.match(content, /editAssignToMe\?\.addEventListener\("click"/);
    assert.match(content, /editAssignToMe\.hidden = normalizedOwner === normalizedCurrentActor/);
    assert.match(content, /const specForgeCliCurrentActor = /);
    assert.match(content, /const currentActor = window\.specForgeCliCurrentActor \|\| "cli-user"/);
  }

  assert.match(packagedSidebar, /data-command="showEditUserStoryForm"[\s\S]*data-us-id="\$\{[\s\S]*summary\.usId[\s\S]*\}"[\s\S]*data-title="\$\{[\s\S]*editableUserStoryTitle\(summary\.usId, summary\.title\)[\s\S]*\}"[\s\S]*data-owner="\$\{[\s\S]*summary\.owner[\s\S]*\}"[\s\S]*data-category="\$\{[\s\S]*summary\.category[\s\S]*\}"[\s\S]*data-tags="\$\{[\s\S]*\(summary\.tags \?\? \[\]\)\.join\(", "\)[\s\S]*\}"/);
  assert.doesNotMatch(packagedSidebar, /<button class="action-menu__item" type="button" role="menuitem" disabled>\s+<span class="action-menu__item-icon" aria-hidden="true">✎<\/span>\s+<span>Edit US info<\/span>/);
});

test("CLI workflow renderer handles sidebar starred story toggles locally", async () => {
  const script = await fs.promises.readFile(scriptPath, "utf8");
  const packagedScript = await fs.promises.readFile(packagedScriptPath, "utf8");

  for (const content of [script, packagedScript]) {
    assert.match(content, /starredUserStoryStorageKey = "specforge\.cli\.sidebar\.starredUserStoryId"/);
    assert.match(content, /toggleStarredUserStory\(message\) \{/);
    assert.match(content, /setStarredUserStoryId\(current === message\.usId \? null : message\.usId\)/);
    assert.match(content, /button\.classList\.toggle\("story-star--active", active\)/);
    assert.match(content, /const label = \(active \? "Unstar " : "Star "\) \+ usId/);
    assert.match(content, /icon\.textContent = active \? "★" : "☆"/);
  }
});

test("CLI workflow renderer confirms before dropping user stories", async () => {
  const script = await fs.promises.readFile(scriptPath, "utf8");

  assert.match(script, /dropUserStory\(message\) \{/);
  assert.match(script, /if \(!window\.confirm\("Drop " \+ message\.usId \+ "\? It will be marked as deleted and hidden from the SpecForge panel\."\)\) \{/);
  assert.match(script, /requestJson\("\/api\/drop-user-story", \{ usId: message\.usId \}\)/);
  assert.match(script, /recoverUserStory\(message\) \{/);
  assert.match(script, /requestJson\("\/api\/recover-user-story", \{ usId: message\.usId \}\)/);
});

test("CLI workflow renderer switches sidebar visibility without navigating the workflow", async () => {
  const script = await fs.promises.readFile(scriptPath, "utf8");
  const packagedScript = await fs.promises.readFile(packagedScriptPath, "utf8");

  for (const content of [script, packagedScript]) {
    assert.match(content, /activeSidebarUserStories = Array\.isArray\(payload\.activeSidebarUserStories\)/);
    assert.match(content, /droppedSidebarUserStories = Array\.isArray\(payload\.droppedSidebarUserStories\)/);
    assert.match(content, /buildSidebarHtmlModes\(includeOtherOwners, showHiddenUserStories, watchingUserStoryIds, hiddenUserStoryIds\)/);
    assert.match(content, /const scope = sidebarShowsOtherOwners \? sidebarHtmlByScope\.all : sidebarHtmlByScope\.mine/);
    assert.match(content, /const visibilityScope = sidebarShowsHidden \? scope\.hidden : scope\.visible/);
    assert.match(content, /sidebarFrame\.srcdoc = sidebarShowsDropped/);
    assert.match(content, /window\.history\.replaceState\(window\.history\.state, "", url\.toString\(\)\)/);
    assert.doesNotMatch(content, /resolveTargetUserStoryId/);
    assert.doesNotMatch(content, /navigateTo\(url, true\)/);
  }
});

test("CLI workflow renderer persists completed story visibility in the portal query", async () => {
  const script = await fs.promises.readFile(scriptPath, "utf8");
  const packagedScript = await fs.promises.readFile(packagedScriptPath, "utf8");

  for (const content of [script, packagedScript]) {
    assert.match(content, /showCompletedUserStories = payload\.showCompletedUserStories === true/);
    assert.match(content, /showCompletedUserStories,/);
    assert.match(content, /toggleCompletedUserStories\(\) \{/);
    assert.match(content, /sidebarShowsCompleted = !sidebarShowsCompleted/);
    assert.match(content, /url\.searchParams\.set\("sidebarCompleted", "true"\)/);
    assert.match(content, /replaceSidebarFrame\(\)/);
  }
});

test("CLI workflow renderer persists blocked story visibility in the portal query", async () => {
  const script = await fs.promises.readFile(scriptPath, "utf8");
  const packagedScript = await fs.promises.readFile(packagedScriptPath, "utf8");

  for (const content of [script, packagedScript]) {
    assert.match(content, /showBlockedUserStories = payload\.showBlockedUserStories === true/);
    assert.match(content, /showBlockedUserStories,/);
    assert.match(content, /toggleBlockedUserStories\(\) \{/);
    assert.match(content, /sidebarShowsBlocked = !sidebarShowsBlocked/);
    assert.match(content, /url\.searchParams\.set\("sidebarBlocked", "true"\)/);
    assert.match(content, /activeCompletedBlocked/);
    assert.match(content, /replaceSidebarFrame\(\)/);
  }
});

test("CLI workflow renderer keeps other owners hidden by default and toggles them from view options", async () => {
  const script = await fs.promises.readFile(scriptPath, "utf8");
  const packagedScript = await fs.promises.readFile(packagedScriptPath, "utf8");

  for (const content of [script, packagedScript]) {
    assert.match(content, /const sidebarHtmlByScope = \{/);
    assert.match(content, /mine: \{\s*visible: buildSidebarHtmlModes\(false, false, watchingUserStoryIds, hiddenUserStoryIds\),\s*hidden: buildSidebarHtmlModes\(false, true, watchingUserStoryIds, hiddenUserStoryIds\)/);
    assert.match(content, /all: \{\s*visible: buildSidebarHtmlModes\(true, false, watchingUserStoryIds, hiddenUserStoryIds\),\s*hidden: buildSidebarHtmlModes\(true, true, watchingUserStoryIds, hiddenUserStoryIds\)/);
    assert.match(content, /let sidebarShowsOtherOwners = false/);
    assert.match(content, /new URL\(window\.location\.href\)\.searchParams\.get\("sidebarOtherOwners"\) === "true"/);
    assert.match(content, /toggleSearchIncludesOtherOwners\(\) \{/);
    assert.match(content, /sidebarShowsOtherOwners = !sidebarShowsOtherOwners/);
    assert.match(content, /url\.searchParams\.set\("sidebarOtherOwners", "true"\)/);
    assert.match(content, /url\.searchParams\.delete\("sidebarOtherOwners"\)/);
  }
});

test("CLI workflow renderer bridges iframe view-option toggles in the parent portal", async () => {
  const script = await fs.promises.readFile(scriptPath, "utf8");
  const packagedScript = await fs.promises.readFile(packagedScriptPath, "utf8");

  for (const content of [script, packagedScript]) {
    assert.match(content, /data-cli-sidebar-view-options/);
    assert.match(content, /data-cli-parent-command="toggleSearchIncludesOtherOwners"/);
    assert.match(content, /const applySidebarScopeCommand = \(command\) =>/);
    assert.match(content, /sidebarViewOptionsPanel\?\.addEventListener\("click", event =>/);
    assert.match(content, /const bridgeSidebarScopeControls = \(\) =>/);
    assert.match(content, /const commandButtons = \[/);
    assert.match(content, /button\.dataset\.portalBound === "true"/);
    assert.match(content, /case "toggleSearchIncludesOtherOwners":/);
    assert.match(content, /replaceSidebarUrlState\(\);[\s\S]*replaceSidebarFrame\(\);[\s\S]*updateSidebarViewOptionsUi\(\);/);
    assert.match(content, /sidebarFrame\?\.addEventListener\("load", \(\) => \{/);
  }
});

test("CLI workflow renderer persists local sidebar visibility state for watched and hidden stories", async () => {
  const script = await fs.promises.readFile(scriptPath, "utf8");
  const packagedScript = await fs.promises.readFile(packagedScriptPath, "utf8");

  for (const content of [script, packagedScript]) {
    assert.match(content, /watchingUserStoryIdsStorageKey = "specforge\.cli\.sidebar\.watchingUserStoryIds"/);
    assert.match(content, /hiddenUserStoryIdsStorageKey = "specforge\.cli\.sidebar\.hiddenUserStoryIds"/);
    assert.match(content, /showHiddenStorageKey = "specforge\.cli\.sidebar\.showHiddenUserStories"/);
    assert.match(content, /toggleSidebarVisibilityUserStory\(message\) \{/);
    assert.match(content, /url\.searchParams\.set\("sidebarWatching", sidebarWatchingUserStoryIds\.join\(","\)\)/);
    assert.match(content, /url\.searchParams\.set\("sidebarHidden", sidebarHiddenUserStoryIds\.join\(","\)\)/);
    assert.match(content, /toggleShowHiddenUserStories\(\) \{/);
    assert.match(content, /url\.searchParams\.set\("sidebarHiddenVisible", "true"\)/);
  }
});

test("CLI workflow renderer wires lineage repair and reset actions through portal endpoints", async () => {
  const script = await fs.promises.readFile(scriptPath, "utf8");
  const packagedScript = await fs.promises.readFile(packagedScriptPath, "utf8");

  for (const content of [script, packagedScript]) {
    assert.match(content, /resetUserStoryToCapture\(message\) \{/);
    assert.match(content, /requestJson\("\/api\/reset-user-story-to-capture", \{ usId: message\.usId, actor: currentActor \}\)/);
    assert.match(content, /analyzeRepairUserStory\(message\) \{/);
    assert.match(content, /requestJson\("\/api\/analyze-user-story-lineage", \{ usId: message\.usId, actor: currentActor \}\)/);
    assert.match(content, /requestJson\("\/api\/repair-user-story-lineage", \{ usId: message\.usId, actor: currentActor \}\)/);
  }
});

test("CLI workflow renderer polls the signature for the current portal query", async () => {
  const script = await fs.promises.readFile(scriptPath, "utf8");
  const packagedScript = await fs.promises.readFile(packagedScriptPath, "utf8");

  for (const content of [script, packagedScript]) {
    assert.match(content, /fetch\("\/api\/workflow-signature" \+ window\.location\.search, \{ cache: "no-store" \}\)/);
    assert.doesNotMatch(content, /fetch\("\/api\/workflow-signature", \{ cache: "no-store" \}\)/);
  }
});

test("CLI workflow renderer canonicalizes the URL to the rendered workflow story", async () => {
  const script = await fs.promises.readFile(scriptPath, "utf8");
  const packagedScript = await fs.promises.readFile(packagedScriptPath, "utf8");

  for (const content of [script, packagedScript]) {
    assert.match(content, /const renderedWorkflowUsId = \$\{JSON\.stringify\(workflow\?\.usId \?\? null\)\}/);
    assert.match(content, /url\.searchParams\.get\("usId"\) !== renderedWorkflowUsId/);
    assert.match(content, /url\.searchParams\.set\("usId", renderedWorkflowUsId\)/);
    assert.match(content, /window\.history\.replaceState\(window\.history\.state, "", url\.toString\(\)\)/);
  }
});
