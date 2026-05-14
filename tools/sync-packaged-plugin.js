"use strict";

const fs = require("node:fs");
const path = require("node:path");

const repoRoot = path.resolve(__dirname, "..");
const sourceDir = path.join(repoRoot, "dist", "mcp");
const targetDir = path.join(repoRoot, "plugins", "specforge-ai", "mcp");

if (!fs.existsSync(sourceDir)) {
  throw new Error(`Packaged MCP runtime not found at ${sourceDir}. Run compile:mcp first.`);
}

fs.rmSync(targetDir, { recursive: true, force: true });
fs.mkdirSync(path.dirname(targetDir), { recursive: true });
fs.cpSync(sourceDir, targetDir, { recursive: true });

process.stdout.write(`Synced packaged plugin MCP runtime from ${sourceDir} to ${targetDir}\n`);
