#!/usr/bin/env node

const fs = require("fs");
const path = require("path");
const { execFileSync } = require("child_process");

function run(command, args, cwd) {
  return execFileSync(command, args, {
    cwd,
    encoding: "utf8",
    stdio: ["pipe", "pipe", "pipe"],
  }).trim();
}

function resolveRepoRoot() {
  const candidates = [process.cwd(), path.resolve(__dirname, "..")];
  for (const candidate of candidates) {
    try {
      return run("git", ["rev-parse", "--show-toplevel"], candidate);
    } catch {
      continue;
    }
  }

  throw new Error("Unable to resolve the Git repository root for backlog sync.");
}

const effectiveRepoRoot = resolveRepoRoot();
const outputPath = path.join(effectiveRepoRoot, "doc", "backlog-open.md");

function inferRepoFullName() {
  const explicitRepo = process.env.BACKLOG_REPO?.trim();
  if (explicitRepo) {
    return explicitRepo;
  }

  const remoteUrl = run("git", ["remote", "get-url", "origin"], effectiveRepoRoot);
  const match = remoteUrl.match(/github\.com[:/](.+?)(?:\.git)?$/i);
  if (!match) {
    throw new Error(
      "Unable to infer the GitHub repository from remote.origin.url. Set BACKLOG_REPO=owner/name if this repository uses a non-standard remote."
    );
  }

  return match[1];
}

function runGh(repoFullName, args) {
  return run("gh", ["issue", "list", "--repo", repoFullName, ...args], effectiveRepoRoot);
}

function listOpenIssues(repoFullName) {
  const output = runGh(repoFullName, [
    "--state",
    "open",
    "--limit",
    "500",
    "--json",
    "number,title,labels,url,assignees,milestone,body",
  ]);

  return JSON.parse(output);
}

function issueGroup(issue) {
  const labels = new Set((issue.labels || []).map((label) => label.name));
  if (labels.has("bug")) {
    return "Bugs";
  }

  if (labels.has("enhancement")) {
    return "Features";
  }

  if (labels.has("tech-debt")) {
    return "Technical Debt";
  }

  return "Other Open Work";
}

function bodyField(body, fieldName) {
  const regex = new RegExp(`^${fieldName}:\\s*(.+)$`, "mi");
  const match = (body || "").match(regex);
  return match ? match[1].trim() : "";
}

function issueCode(issue) {
  const match = issue.title.match(/^([A-Z]{3}-\d{3})\b/);
  return match ? match[1] : `ZZZ-${String(issue.number).padStart(3, "0")}`;
}

function priorityRank(priority) {
  const order = {
    P0: 0,
    P1: 1,
    P2: 2,
    P3: 3,
  };

  return order[priority] ?? 99;
}

function severityRank(severity) {
  const order = {
    critical: 0,
    high: 1,
    medium: 2,
    low: 3,
  };

  return order[severity] ?? 99;
}

function sortIssues(issues) {
  return [...issues].sort((left, right) => {
    const leftGroup = issueGroup(left);
    const rightGroup = issueGroup(right);

    if (leftGroup === "Bugs" && rightGroup === "Bugs") {
      const leftSeverity = bodyField(left.body, "Severity");
      const rightSeverity = bodyField(right.body, "Severity");
      const severityDiff = severityRank(leftSeverity) - severityRank(rightSeverity);
      if (severityDiff !== 0) {
        return severityDiff;
      }
    } else if (leftGroup !== "Bugs" && rightGroup !== "Bugs") {
      const leftPriority = bodyField(left.body, "Priority");
      const rightPriority = bodyField(right.body, "Priority");
      const priorityDiff = priorityRank(leftPriority) - priorityRank(rightPriority);
      if (priorityDiff !== 0) {
        return priorityDiff;
      }
    }

    return issueCode(left).localeCompare(issueCode(right), "en");
  });
}

function renderIssue(issue) {
  const labels = (issue.labels || []).map((label) => `\`${label.name}\``).join(", ") || "`none`";
  const assignees = (issue.assignees || []).map((assignee) => assignee.login).join(", ") || "unassigned";
  const milestone = issue.milestone?.title || "none";
  const group = issueGroup(issue);
  const severity = bodyField(issue.body, "Severity") || "unknown";
  const priority = bodyField(issue.body, "Priority") || "unknown";

  const extraField = group === "Bugs"
    ? `- Severity: \`${severity}\``
    : `- Priority: \`${priority}\``;

  return [
    `### ${issue.title}`,
    "",
    `- GitHub issue: [#${issue.number}](${issue.url})`,
    `- Type: ${labels}`,
    `- State: \`OPEN\``,
    extraField,
    `- Assignees: ${assignees}`,
    `- Milestone: ${milestone}`,
    "",
  ].join("\n");
}

function renderBacklog(repoFullName, issues) {
  const groups = new Map();
  for (const issue of issues) {
    const group = issueGroup(issue);
    if (!groups.has(group)) {
      groups.set(group, []);
    }

    groups.get(group).push(issue);
  }

  const orderedGroups = ["Bugs", "Features", "Technical Debt", "Other Open Work"];
  const lines = [
    "# Open Backlog",
    "",
    `Generated from open GitHub issues in \`${repoFullName}\`. Do not edit manually; run \`npm run backlog:sync\`.`,
    "",
    `Count: ${issues.length}`,
    "",
  ];

  if (issues.length === 0) {
    lines.push("No open issues.");
    lines.push("");
    return lines.join("\n");
  }

  for (const group of orderedGroups) {
    const groupIssues = groups.get(group);
    if (!groupIssues || groupIssues.length === 0) {
      continue;
    }

    lines.push(`## ${group}`);
    lines.push("");
    for (const issue of sortIssues(groupIssues)) {
      lines.push(renderIssue(issue));
    }
  }

  return lines.join("\n");
}

function main() {
  const repoFullName = inferRepoFullName();
  const issues = listOpenIssues(repoFullName);
  fs.mkdirSync(path.dirname(outputPath), { recursive: true });
  fs.writeFileSync(outputPath, renderBacklog(repoFullName, issues), "utf8");
}

main();
