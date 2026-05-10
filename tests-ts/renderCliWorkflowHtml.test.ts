import test from "node:test";
import assert from "node:assert/strict";
import * as fs from "node:fs";
import * as path from "node:path";

const scriptPath = path.join(process.cwd(), "tools", "render-cli-workflow-html.js");

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
  assert.match(script, /window\.open\(\$\{JSON\.stringify\(configurationAdvancedUrl\)\}, "_blank", "noopener"\)/);
  assert.match(script, /window\.open\(\$\{JSON\.stringify\(configurationProvidersUrl\)\}, "_blank", "noopener"\)/);
});

test("CLI workflow renderer routes sidebar story selection through the current portal", async () => {
  const script = await fs.promises.readFile(scriptPath, "utf8");

  assert.match(script, /message\.command === "openWorkflow" && message\.usId/);
  assert.match(script, /url\.searchParams\.set\("usId", message\.usId\)/);
  assert.match(script, /activeWorkflowUsId: workflow\.usId/);
});
