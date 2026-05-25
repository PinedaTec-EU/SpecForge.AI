#!/usr/bin/env node

const fs = require("fs");
const os = require("os");
const path = require("path");
const { execFileSync } = require("child_process");

const repoRoot = path.resolve(__dirname, "..");
const repoFullName = "PinedaTec-EU/SpecForge.AI";
const legacyRegistryPath =
  process.env.BUG_REGISTRY_SOURCE ||
  path.join(repoRoot, "doc", "bug-registry.md");
const openBugsPath = path.join(repoRoot, "doc", "bugs-open.md");
const closedBugsPath = path.join(repoRoot, "doc", "bugs-closed.md");
const compatibilityIndexPath = path.join(repoRoot, "doc", "bug-registry.md");
const args = new Set(process.argv.slice(2));

function runGh(args, options = {}) {
  return execFileSync("gh", args, {
    cwd: repoRoot,
    encoding: "utf8",
    stdio: ["pipe", "pipe", "pipe"],
    ...options,
  }).trim();
}

function normalizeLineEndings(value) {
  return value.replace(/\r\n/g, "\n");
}

function parseLegacyEntries(markdown) {
  const content = normalizeLineEndings(markdown);
  const parts = content.split(/^### (SFB-\d+)\n/m);
  const entries = [];
  for (let index = 1; index < parts.length; index += 2) {
    entries.push(parseLegacyEntry(parts[index], parts[index + 1] || ""));
  }

  return entries;
}

function parseLegacyEntry(codeFromHeader, section) {
  const bugCode = captureField(section, "Bug code") || codeFromHeader;
  const discoveryDate = captureField(section, "Discovery date") || "";
  const status = captureField(section, "Status") || "";
  const githubIssue = captureGitHubIssue(section);
  const shortDescription = captureMultilineBlock(section, "Short description");
  const reproductionSteps = captureMultilineBlock(section, "Reproduction steps");

  return {
    bugCode,
    discoveryDate,
    status,
    githubIssue,
    shortDescription,
    reproductionSteps,
  };
}

function captureField(section, label) {
  const regex = new RegExp(
    "^- " + escapeRegex(label) + ": `?([^\\n`]+)`?$",
    "m",
  );
  const match = section.match(regex);
  return match ? match[1].trim() : "";
}

function captureGitHubIssue(section) {
  const match = section.match(/^- GitHub issue: \[#(\d+)\]\(([^)]+)\)$/m);
  if (!match) {
    return null;
  }

  return {
    number: Number(match[1]),
    url: match[2],
  };
}

function captureMultilineBlock(section, label) {
  const inlineRegex = new RegExp(
    "^- " + escapeRegex(label) + ":\\s*(.+)$",
    "m",
  );
  const inlineMatch = section.match(inlineRegex);
  if (inlineMatch) {
    return inlineMatch[1].trim();
  }

  return captureBlockByLabel(
    section,
    `- ${label}:`,
    /^- [A-Z][^:]+:/,
  );
}

function escapeRegex(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function listGitHubIssues() {
  const output = runGh([
    "issue",
    "list",
    "--repo",
    repoFullName,
    "--state",
    "all",
    "--limit",
    "200",
    "--json",
    "number,title,body,state,labels,url,createdAt,closedAt",
  ]);

  return JSON.parse(output);
}

function parseIssue(issue) {
  const body = normalizeLineEndings(issue.body || "");
  const bugCodeMatch =
    issue.title.match(/^(SFB-\d+)\b/) ||
    body.match(/^Bug code:\s*(SFB-\d+)\s*$/m);

  const bugCode = bugCodeMatch ? bugCodeMatch[1] : null;
  const discoveryDate =
    captureSimpleBodyField(body, "Discovery date") ||
    (issue.createdAt ? issue.createdAt.slice(0, 10) : "");
  const bodyStatus = captureSimpleBodyField(body, "Status");
  const shortDescription =
    captureIssueBlock(body, "Short description") ||
    issue.title.replace(/^(SFB-\d+):?\s*/, "").trim();
  const reproductionSteps = captureIssueBlock(body, "Reproduction steps");
  const labelNames = issue.labels.map((label) => label.name);
  const derivedStatus =
    issue.state === "OPEN"
      ? bodyStatus || "Open"
      : labelNames.includes("wontfix")
        ? "Won't Fix"
        : bodyStatus || "Closed";

  return {
    number: issue.number,
    title: issue.title,
    url: issue.url,
    state: issue.state,
    labels: labelNames,
    bugCode,
    discoveryDate,
    status: derivedStatus,
    shortDescription,
    reproductionSteps,
    createdAt: issue.createdAt,
    closedAt: issue.closedAt,
  };
}

function captureSimpleBodyField(body, label) {
  const regex = new RegExp(`^${escapeRegex(label)}:\\s*(.+)$`, "m");
  const match = body.match(regex);
  return match ? match[1].trim() : "";
}

function captureIssueBlock(body, label) {
  return captureBlockByLabel(body, `${label}:`, /^[A-Z][A-Za-z ]+:/);
}

function captureBlockByLabel(content, marker, nextFieldPattern) {
  const lines = normalizeLineEndings(content).split("\n");
  const startIndex = lines.findIndex((line) => line.trim() === marker);
  if (startIndex < 0) {
    return "";
  }

  const collected = [];
  for (let index = startIndex + 1; index < lines.length; index += 1) {
    const line = lines[index];
    if (nextFieldPattern.test(line) && line.trim() === line) {
      break;
    }

    collected.push(line);
  }

  return collected.join("\n").trim();
}

function validateUniqueCodes(entries, sourceLabel) {
  const seen = new Map();
  for (const entry of entries) {
    if (!entry.bugCode) {
      continue;
    }

    if (seen.has(entry.bugCode)) {
      throw new Error(
        `${sourceLabel} contains duplicate bug code ${entry.bugCode}.`,
      );
    }

    seen.set(entry.bugCode, entry);
  }
}

function compareBugCodes(left, right) {
  return bugCodeNumber(left.bugCode) - bugCodeNumber(right.bugCode);
}

function bugCodeNumber(code) {
  const match = code.match(/(\d+)$/);
  return match ? Number(match[1]) : Number.MAX_SAFE_INTEGER;
}

function ensureBugLabels(githubIssues) {
  for (const issue of githubIssues) {
    const parsed = parseIssue(issue);
    if (!parsed.bugCode) {
      continue;
    }

    if (parsed.labels.includes("duplicate")) {
      continue;
    }

    if (parsed.labels.includes("bug")) {
      continue;
    }

    runGh([
      "issue",
      "edit",
      String(issue.number),
      "--repo",
      repoFullName,
      "--add-label",
      "bug",
    ]);
  }
}

function formatIssueBody(entry) {
  const lines = [
    `Bug code: ${entry.bugCode}`,
    `Discovery date: ${entry.discoveryDate}`,
    `Status: ${entry.status}`,
    "",
    "Short description:",
    entry.shortDescription.trim(),
  ];

  if (entry.reproductionSteps.trim()) {
    lines.push("", "Reproduction steps:", entry.reproductionSteps.trim());
  }

  return `${lines.join("\n").trim()}\n`;
}

function createGitHubIssue(entry) {
  const title = `${entry.bugCode}: ${shortIssueTitle(entry.shortDescription)}`;
  const body = formatIssueBody(entry);
  const bodyFile = writeTempBodyFile(entry.bugCode, body);
  const output = runGh([
    "issue",
    "create",
    "--repo",
    repoFullName,
    "--title",
    title,
    "--label",
    "bug",
    "--body-file",
    bodyFile,
  ]);
  fs.unlinkSync(bodyFile);
  const issueNumberMatch = output.match(/\/issues\/(\d+)$/);
  if (!issueNumberMatch) {
    throw new Error(`Could not parse created issue URL: ${output}`);
  }

  const issueNumber = Number(issueNumberMatch[1]);
  if (entry.status === "Fixed" || entry.status === "Closed") {
    runGh([
      "issue",
      "close",
      String(issueNumber),
      "--repo",
      repoFullName,
      "--reason",
      "completed",
    ]);
  } else if (entry.status === "Won't Fix") {
    runGh([
      "issue",
      "edit",
      String(issueNumber),
      "--repo",
      repoFullName,
      "--add-label",
      "wontfix",
    ]);
    runGh([
      "issue",
      "close",
      String(issueNumber),
      "--repo",
      repoFullName,
      "--reason",
      "not planned",
    ]);
  }

  return issueNumber;
}

function syncExistingIssue(issue, entry) {
  const title = `${entry.bugCode}: ${shortIssueTitle(entry.shortDescription)}`;
  const body = formatIssueBody(entry);
  const bodyFile = writeTempBodyFile(entry.bugCode, body);

  runGh([
    "issue",
    "edit",
    String(issue.number),
    "--repo",
    repoFullName,
    "--title",
    title,
    "--body-file",
    bodyFile,
    "--add-label",
    "bug",
  ]);
  fs.unlinkSync(bodyFile);

  if (entry.status === "Won't Fix") {
    runGh([
      "issue",
      "edit",
      String(issue.number),
      "--repo",
      repoFullName,
      "--add-label",
      "wontfix",
    ]);
    if (issue.state !== "CLOSED") {
      runGh([
        "issue",
        "close",
        String(issue.number),
        "--repo",
        repoFullName,
        "--reason",
        "not planned",
      ]);
    }
    return;
  }

  const shouldBeClosed = entry.status === "Fixed" || entry.status === "Closed";
  if (shouldBeClosed && issue.state !== "CLOSED") {
    runGh([
      "issue",
      "close",
      String(issue.number),
      "--repo",
      repoFullName,
      "--reason",
      "completed",
    ]);
  }

  if (!shouldBeClosed && issue.state === "CLOSED") {
    runGh([
      "issue",
      "reopen",
      String(issue.number),
      "--repo",
      repoFullName,
    ]);
  }
}

function writeTempBodyFile(bugCode, body) {
  const tempPath = path.join(
    os.tmpdir(),
    `specforge-bug-${bugCode.toLowerCase()}-${process.pid}.md`,
  );
  fs.writeFileSync(tempPath, body, "utf8");
  return tempPath;
}

function shortIssueTitle(description) {
  const normalized = description.replace(/\s+/g, " ").trim();
  if (!normalized) {
    return "Bug";
  }

  const firstClause = normalized.split(/,|;| which | because | so that | so | but /i)[0].trim();
  const candidate = firstClause || normalized;
  if (candidate.length <= 90) {
    return candidate;
  }

  return `${candidate.slice(0, 87).trimEnd()}...`;
}

function importLegacyEntries() {
  const legacyContent = fs.readFileSync(legacyRegistryPath, "utf8");
  const legacyEntries = parseLegacyEntries(legacyContent);
  validateUniqueCodes(legacyEntries, "Legacy registry");

  const githubIssues = listGitHubIssues();
  ensureBugLabels(githubIssues);
  const githubBugIssuesByCode = new Map(
    githubIssues
      .map((issue) => parseIssue(issue))
      .filter((issue) => issue.bugCode)
      .map((issue) => [issue.bugCode, issue]),
  );

  const created = [];
  for (const entry of legacyEntries) {
    const existingIssue = githubBugIssuesByCode.get(entry.bugCode);
    if (existingIssue) {
      syncExistingIssue(existingIssue, entry);
      continue;
    }

    const issueNumber = createGitHubIssue(entry);
    created.push({ bugCode: entry.bugCode, issueNumber });
    githubBugIssuesByCode.set(entry.bugCode, { number: issueNumber, state: entry.status === "Open" ? "OPEN" : "CLOSED" });
  }

  return created;
}

function renderBugList(title, issues) {
  const lines = [
    `# ${title}`,
    "",
    `Generated from GitHub issues in \`${repoFullName}\` labeled \`bug\`. Do not edit manually; run \`node tools/sync-bug-docs.js\`.`,
    "",
    `Count: ${issues.length}`,
    "",
  ];

  if (issues.length === 0) {
    lines.push("No bugs in this state.");
    lines.push("");
    return lines.join("\n");
  }

  for (const issue of issues) {
    lines.push(`## ${issue.bugCode}`);
    lines.push("");
    lines.push(`- Bug code: \`${issue.bugCode}\``);
    lines.push(`- GitHub issue: [#${issue.number}](${issue.url})`);
    lines.push(`- Discovery date: \`${issue.discoveryDate}\``);
    lines.push(`- Status: \`${issue.status}\``);
    lines.push(`- Short description: ${issue.shortDescription}`);
    if (issue.reproductionSteps) {
      lines.push("- Reproduction steps:");
      lines.push(issue.reproductionSteps);
    }
    lines.push("");
  }

  return lines.join("\n");
}

function writeMirrorFiles() {
  const githubIssues = listGitHubIssues();
  ensureBugLabels(githubIssues);

  const bugIssues = githubIssues
    .map((issue) => parseIssue(issue))
    .filter((issue) => issue.bugCode && issue.labels.includes("bug"));

  validateUniqueCodes(bugIssues, "GitHub bug issues");
  bugIssues.sort(compareBugCodes);

  const openIssues = bugIssues.filter((issue) => issue.state === "OPEN");
  const closedIssues = bugIssues.filter((issue) => issue.state === "CLOSED");

  fs.writeFileSync(openBugsPath, renderBugList("Open Bugs", openIssues));
  fs.writeFileSync(closedBugsPath, renderBugList("Closed Bugs", closedIssues));
  fs.writeFileSync(
    compatibilityIndexPath,
    [
      "# Bug Registry",
      "",
      "This index is kept for compatibility.",
      "",
      "- Open bugs: [bugs-open.md](./bugs-open.md)",
      "- Closed bugs: [bugs-closed.md](./bugs-closed.md)",
      "",
      "Both files are synchronized from GitHub bug issues via `node tools/sync-bug-docs.js`.",
      "",
    ].join("\n"),
  );
}

function main() {
  if (args.has("--import-legacy")) {
    const created = importLegacyEntries();
    if (created.length > 0) {
      process.stdout.write(
        `${created.length} legacy bug issues imported: ${created
          .map((entry) => `${entry.bugCode}->#${entry.issueNumber}`)
          .join(", ")}\n`,
      );
    } else {
      process.stdout.write("No legacy bug issues needed import.\n");
    }
  }

  writeMirrorFiles();
}

main();
