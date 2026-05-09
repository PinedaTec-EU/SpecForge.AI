import test from "node:test";
import assert from "node:assert/strict";
import * as fs from "node:fs";
import * as path from "node:path";

const runnerProjectPath = path.join(process.cwd(), "src", "SpecForge.Runner.Cli", "SpecForge.Runner.Cli.csproj");

test("CLI runner publishes workflow renderer assets with the executable", async () => {
  const project = await fs.promises.readFile(runnerProjectPath, "utf8");

  assert.match(project, /tools\\render-cli-workflow-html\.js/);
  assert.match(project, /CopyToOutputDirectory="PreserveNewest"/);
  assert.match(project, /CopyToPublishDirectory="PreserveNewest"/);
  assert.match(project, /LinkBase="dist"/);
  assert.match(project, /LinkBase="dist\\workflow-view"/);
});
