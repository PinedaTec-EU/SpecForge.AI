import test from "node:test";
import assert from "node:assert/strict";
import * as fs from "node:fs";
import * as path from "node:path";

test("SpecForge plugin manifest version matches repository runtime version", async () => {
  const versionDefinitionPath = path.join(process.cwd(), "version_definition.json");
  const pluginManifestPath = path.join(process.cwd(), "plugins", "specforge-ai", ".codex-plugin", "plugin.json");
  const versionDefinition = JSON.parse(await fs.promises.readFile(versionDefinitionPath, "utf8"));
  const pluginManifest = JSON.parse(await fs.promises.readFile(pluginManifestPath, "utf8"));

  assert.equal(pluginManifest.version, versionDefinition.currentVersion);
});
