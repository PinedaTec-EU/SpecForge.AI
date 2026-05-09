import test from "node:test";
import assert from "node:assert/strict";
import * as fs from "node:fs";
import * as path from "node:path";

const programPath = path.join(process.cwd(), "src", "SpecForge.McpServer", "Program.cs");
const registryPath = path.join(process.cwd(), "src", "SpecForge.McpServer", "McpToolRegistry.cs");

test("MCP compact facades expose enum constrained operations", async () => {
  const source = await fs.promises.readFile(registryPath, "utf8");

  assert.match(source, /"query",\s*EnumProp\("Read operation\."/);
  assert.match(source, /"list_user_stories", "summary", "workflow", "current_phase", "runtime_status", "lineage", "files"/);
  assert.match(source, /"action",\s*EnumProp\("Mutation operation\./);
  assert.match(source, /"create_user_story", "create_user_stories_from_goal", "import_user_story", "advance_phase"/);
  assert.match(source, /"operation",\s*EnumProp\("Prompt operation\.", "initialize_repo_prompts", "export_prompt_template"\)/);
});

test("MCP schemas constrain common phase, kind, and reason values", async () => {
  const source = await fs.promises.readFile(registryPath, "utf8");

  assert.match(source, /JsonObject EnumProp\(string description, params string\[\] values\)/);
  assert.match(source, /JsonObject PhaseSlugProp\(string description\)/);
  assert.match(source, /"reasonKind",\s*EnumProp\("Typed reopen reason\.", "merge-conflict", "defect", "functional-issue", "technical-issue"\)/);
  assert.match(source, /"kind",\s*EnumProp\("File kind\.", "context", "attachment"\)/);
  assert.match(source, /"kind",\s*EnumProp\("User story kind\.", "feature", "bug", "hotfix"\)/);
});

test("MCP required array arguments fail fast when missing or empty", async () => {
  const source = await fs.promises.readFile(programPath, "utf8");

  assert.match(source, /throw new InvalidOperationException\(\$"Missing required array argument '\{key\}'\."\)/);
  assert.match(source, /Required array argument '\{key\}' must contain at least one non-empty value/);
  assert.doesNotMatch(source, /if \(arguments\[key\] is not JsonArray array\)\s*\{\s*return \[\];\s*\}/);
});
