import { execFileSync } from "node:child_process";
import * as fs from "node:fs";
import * as os from "node:os";
import * as path from "node:path";

const SETTINGS_PATH = path.join(".specs", "configuration", "settings.json");

export function getCurrentActor(workspaceRoot?: string): string {
  const configuredUser = workspaceRoot ? readConfiguredUser(workspaceRoot) : "";
  if (configuredUser.length > 0) {
    return configuredUser;
  }

  const gitUser = workspaceRoot ? detectGitUser(workspaceRoot) : "";
  if (gitUser.length > 0) {
    return gitUser;
  }

  try {
    const info = os.userInfo();
    if (info.username && info.username.trim().length > 0) {
      return info.username.trim();
    }
  } catch {
    // Fall back to environment-derived values.
  }

  const fallback = process.env.USER ?? process.env.USERNAME ?? "";
  return fallback.trim();
}

function readConfiguredUser(workspaceRoot: string): string {
  try {
    const settingsPath = path.join(workspaceRoot, SETTINGS_PATH);
    if (!fs.existsSync(settingsPath)) {
      return "";
    }

    const payload = JSON.parse(fs.readFileSync(settingsPath, "utf8")) as { defaultUser?: unknown };
    return typeof payload.defaultUser === "string" ? payload.defaultUser.trim() : "";
  } catch {
    return "";
  }
}

function detectGitUser(workspaceRoot: string): string {
  const userName = runGitConfig(workspaceRoot, "user.name");
  if (userName.length > 0) {
    return normalizeGitUser(userName);
  }

  const email = runGitConfig(workspaceRoot, "user.email");
  if (email.length === 0) {
    return "";
  }

  return normalizeGitUser(email.split("@", 1)[0] ?? "");
}

function runGitConfig(workspaceRoot: string, key: string): string {
  try {
    return execFileSync("git", ["config", "--get", key], {
      cwd: workspaceRoot,
      encoding: "utf8",
      stdio: ["ignore", "pipe", "ignore"]
    }).trim();
  } catch {
    return "";
  }
}

function normalizeGitUser(value: string): string {
  const normalized = value.trim();
  if (!normalized) {
    return "";
  }

  return normalized.toLowerCase().replaceAll(" ", "-").replaceAll("_", "-");
}
