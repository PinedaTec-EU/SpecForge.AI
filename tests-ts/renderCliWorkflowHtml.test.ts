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
