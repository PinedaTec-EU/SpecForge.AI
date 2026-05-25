"use strict";
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    var desc = Object.getOwnPropertyDescriptor(m, k);
    if (!desc || ("get" in desc ? !m.__esModule : desc.writable || desc.configurable)) {
      desc = { enumerable: true, get: function() { return m[k]; } };
    }
    Object.defineProperty(o, k2, desc);
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __setModuleDefault = (this && this.__setModuleDefault) || (Object.create ? (function(o, v) {
    Object.defineProperty(o, "default", { enumerable: true, value: v });
}) : function(o, v) {
    o["default"] = v;
});
var __importStar = (this && this.__importStar) || (function () {
    var ownKeys = function(o) {
        ownKeys = Object.getOwnPropertyNames || function (o) {
            var ar = [];
            for (var k in o) if (Object.prototype.hasOwnProperty.call(o, k)) ar[ar.length] = k;
            return ar;
        };
        return ownKeys(o);
    };
    return function (mod) {
        if (mod && mod.__esModule) return mod;
        var result = {};
        if (mod != null) for (var k = ownKeys(mod), i = 0; i < k.length; i++) if (k[i] !== "default") __createBinding(result, mod, k[i]);
        __setModuleDefault(result, mod);
        return result;
    };
})();
Object.defineProperty(exports, "__esModule", { value: true });
exports.getCurrentActor = getCurrentActor;
const node_child_process_1 = require("node:child_process");
const fs = __importStar(require("node:fs"));
const os = __importStar(require("node:os"));
const path = __importStar(require("node:path"));
const SETTINGS_PATH = path.join(".specs", "configuration", "settings.json");
function getCurrentActor(workspaceRoot) {
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
    }
    catch {
        // Fall back to environment-derived values.
    }
    const fallback = process.env.USER ?? process.env.USERNAME ?? "";
    return fallback.trim();
}
function readConfiguredUser(workspaceRoot) {
    try {
        const settingsPath = path.join(workspaceRoot, SETTINGS_PATH);
        if (!fs.existsSync(settingsPath)) {
            return "";
        }
        const payload = JSON.parse(fs.readFileSync(settingsPath, "utf8"));
        return typeof payload.defaultUser === "string" ? payload.defaultUser.trim() : "";
    }
    catch {
        return "";
    }
}
function detectGitUser(workspaceRoot) {
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
function runGitConfig(workspaceRoot, key) {
    try {
        return (0, node_child_process_1.execFileSync)("git", ["config", "--get", key], {
            cwd: workspaceRoot,
            encoding: "utf8",
            stdio: ["ignore", "pipe", "ignore"]
        }).trim();
    }
    catch {
        return "";
    }
}
function normalizeGitUser(value) {
    const normalized = value.trim();
    if (!normalized) {
        return "";
    }
    return normalized.toLowerCase().replaceAll(" ", "-").replaceAll("_", "-");
}
//# sourceMappingURL=userActor.js.map