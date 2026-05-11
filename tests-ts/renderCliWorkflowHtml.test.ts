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
  assert.match(script, /JSON\.stringify\(\{ answers: message\.answers, actor: "cli-user" \}\)/);
  assert.match(script, /window\.location\.reload\(\)/);
});

test("CLI workflow shim rejects malformed refinement answer commands locally", async () => {
  const script = await fs.promises.readFile(scriptPath, "utf8");

  assert.match(script, /Array\.isArray\(message\.answers\)/);
  assert.doesNotMatch(script, /submitRefinementAnswers" && message\.answers/);
});

test("CLI workflow renderer passes runtime version into workflow state", async () => {
  const script = await fs.promises.readFile(scriptPath, "utf8");

  assert.match(script, /runtimeVersion: payload\.runtimeVersion \?\? workflow\.lastRuntimeVersion \?\? workflow\.createdWithRuntimeVersion \?\? null/);
});

test("CLI workflow renderer embeds the reusable user-story sidebar with collapsed actions", async () => {
  const script = await fs.promises.readFile(scriptPath, "utf8");

  assert.match(script, /buildSidebarHtml/);
  assert.match(script, /data-cli-sidebar-stories/);
  assert.match(script, /data-cli-sidebar-settings/);
  assert.match(script, /configurationProvidersUrl = payload\.configurationProvidersUrl \|\| configurationPortalUrl/);
  assert.match(script, /configurationAdvancedUrl = payload\.configurationAdvancedUrl \|\| configurationPortalUrl/);
  assert.match(script, /data-cli-config-overlay/);
  assert.match(script, /openConfiguration\(\$\{JSON\.stringify\(configurationAdvancedUrl\)\}\)/);
  assert.match(script, /openConfiguration\(\$\{JSON\.stringify\(configurationProvidersUrl\)\}\)/);
  assert.doesNotMatch(script, /window\.open\(/);
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

  assert.match(script, /message\.command === "openWorkflow" && message\.usId/);
  assert.match(script, /url\.searchParams\.set\("usId", message\.usId\)/);
  assert.match(script, /activeWorkflowUsId: workflow\.usId/);
});

test("CLI workflow renderer routes sidebar edit action to the user story source phase", async () => {
  const script = await fs.promises.readFile(scriptPath, "utf8");
  const packagedScript = await fs.promises.readFile(packagedScriptPath, "utf8");
  const packagedSidebar = await fs.promises.readFile(
    path.join(process.cwd(), "plugins", "specforge-ai", "mcp", "dist", "sidebarViewContent.js"),
    "utf8");

  for (const content of [script, packagedScript]) {
    assert.match(content, /message\.command === "openMainArtifact" && message\.usId/);
    assert.match(content, /url\.searchParams\.set\("selectedPhaseId", "capture"\)/);
  }

  assert.match(packagedSidebar, /data-command="openMainArtifact" data-us-id="\$\{[\s\S]*?summary\.usId[\s\S]*?\}" role="menuitem"/);
  assert.doesNotMatch(packagedSidebar, /<button class="action-menu__item" type="button" role="menuitem" disabled>\s+<span class="action-menu__item-icon" aria-hidden="true">✎<\/span>\s+<span>Edit US info<\/span>/);
});

test("CLI workflow renderer handles sidebar starred story toggles locally", async () => {
  const script = await fs.promises.readFile(scriptPath, "utf8");
  const packagedScript = await fs.promises.readFile(packagedScriptPath, "utf8");

  for (const content of [script, packagedScript]) {
    assert.match(content, /starredUserStoryStorageKey = "specforge\.cli\.sidebar\.starredUserStoryId"/);
    assert.match(content, /message\.command === "toggleStarredUserStory" && message\.usId/);
    assert.match(content, /setStarredUserStoryId\(current === message\.usId \? null : message\.usId\)/);
    assert.match(content, /button\.classList\.toggle\("story-star--active", active\)/);
    assert.match(content, /const label = \(active \? "Unstar " : "Star "\) \+ usId/);
    assert.match(content, /icon\.textContent = active \? "★" : "☆"/);
  }
});
