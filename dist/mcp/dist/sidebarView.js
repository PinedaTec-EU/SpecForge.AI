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
exports.SidebarViewProvider = void 0;
const fs = __importStar(require("node:fs"));
const path = __importStar(require("node:path"));
const vscode = __importStar(require("vscode"));
const extensionSettings_1 = require("./extensionSettings");
const explorerModel_1 = require("./explorerModel");
const outputChannel_1 = require("./outputChannel");
const repoPromptsStatus_1 = require("./repoPromptsStatus");
const runtimeVersion_1 = require("./runtimeVersion");
const specsExplorer_1 = require("./specsExplorer");
const sidebarViewContent_1 = require("./sidebarViewContent");
const sourceFileReferences_1 = require("./sourceFileReferences");
const userActor_1 = require("./userActor");
const userStoryIntake_1 = require("./userStoryIntake");
const userWorkspacePreferences_1 = require("./userWorkspacePreferences");
const utils_1 = require("./utils");
const webviewTypography_1 = require("./webviewTypography");
const workflowPanel_1 = require("./workflowPanel");
const USER_STORY_KINDS = ["feature", "bug", "hotfix", "chore", "refactor", "spike"];
class SidebarViewProvider {
    extensionUri;
    onDidCreateUserStory;
    webviewView;
    showCreateForm = false;
    busyMessage = null;
    activeWorkflowUsId = null;
    showDroppedUserStories = false;
    showCompletedUserStories = false;
    showBlockedUserStories = false;
    showHiddenUserStories = false;
    createFileMode = "context";
    createFiles = [];
    createReferenceScanVersion = 0;
    createFormResetToken = 0;
    constructor(extensionUri, onDidCreateUserStory) {
        this.extensionUri = extensionUri;
        this.onDidCreateUserStory = onDidCreateUserStory;
    }
    refresh() {
        void this.renderAsync();
    }
    setActiveWorkflowUsId(usId) {
        if (this.activeWorkflowUsId === usId) {
            return;
        }
        this.activeWorkflowUsId = usId;
        void this.safeRenderAsync();
    }
    resolveWebviewView(webviewView) {
        this.webviewView = webviewView;
        webviewView.webview.options = {
            enableScripts: true,
            localResourceRoots: [this.extensionUri]
        };
        webviewView.webview.onDidReceiveMessage(async (message) => {
            await this.handleMessageAsync(message);
        });
        return this.safeRenderAsync();
    }
    async handleMessageAsync(message) {
        if (this.busyMessage) {
            return;
        }
        switch (message.command) {
            case "showCreateForm":
                await vscode.commands.executeCommand("specForge.openCreateUserStoryPanel");
                return;
            case "hideCreateForm":
                this.showCreateForm = false;
                this.createFiles = [];
                await this.safeRenderAsync();
                return;
            case "showEditUserStoryForm":
                if (!message.usId) {
                    return;
                }
                await this.showEditUserStoryFormAsync(message.usId);
                return;
            case "openExecutionSettings":
                await vscode.commands.executeCommand("specForge.openExecutionSettings");
                return;
            case "setCreateFileMode":
                this.createFileMode = message.kind === "attachment" ? "attachment" : "context";
                await this.safeRenderAsync();
                return;
            case "addCreateFiles":
                await this.addCreateFilesAsync(message.kind === "attachment" ? "attachment" : "context");
                return;
            case "addCreateFilePaths":
                await this.addCreateFilePathsAsync(message.kind === "attachment" ? "attachment" : "context", message.paths ?? []);
                return;
            case "loadCreateSourceFromFile":
                await this.loadCreateSourceFromFileAsync();
                return;
            case "scanCreateSourceReferences":
                await this.scanCreateSourceReferencesAsync(message.sourceText ?? "");
                return;
            case "setCreateFileKind":
                if (!message.sourcePath) {
                    return;
                }
                this.createFiles = this.createFiles.map((file) => file.sourcePath === message.sourcePath
                    ? { ...file, kind: message.kind === "attachment" ? "attachment" : "context" }
                    : file);
                await this.safeRenderAsync();
                return;
            case "removeCreateFile":
                if (!message.sourcePath) {
                    return;
                }
                this.createFiles = this.createFiles.filter((file) => file.sourcePath !== message.sourcePath);
                await this.safeRenderAsync();
                return;
            case "openWorkflow":
                if (!message.usId) {
                    return;
                }
                await this.openWorkflowAsync(message.usId);
                return;
            case "openMainArtifact":
                if (!message.usId) {
                    return;
                }
                await this.openMainArtifactAsync(message.usId);
                return;
            case "dropUserStory":
                if (!message.usId) {
                    return;
                }
                await this.dropUserStoryAsync(message.usId);
                return;
            case "recoverUserStory":
                if (!message.usId) {
                    return;
                }
                await this.recoverUserStoryAsync(message.usId);
                return;
            case "resetUserStoryToCapture":
                if (!message.usId) {
                    return;
                }
                await this.resetUserStoryToCaptureAsync(message.usId);
                return;
            case "analyzeRepairUserStory":
                if (!message.usId) {
                    return;
                }
                await this.analyzeRepairUserStoryAsync(message.usId);
                return;
            case "toggleStarredUserStory":
                if (!message.usId) {
                    return;
                }
                await this.toggleStarredUserStoryAsync(message.usId);
                return;
            case "toggleDroppedUserStories":
                this.showDroppedUserStories = !this.showDroppedUserStories;
                await this.safeRenderAsync();
                return;
            case "toggleWatchingUserStory":
                if (!message.usId) {
                    return;
                }
                await this.toggleWatchingUserStoryAsync(message.usId);
                return;
            case "toggleSidebarVisibilityUserStory":
                if (!message.usId) {
                    return;
                }
                await this.toggleSidebarVisibilityUserStoryAsync(message.usId, message.owner);
                return;
            case "toggleHiddenUserStory":
                if (!message.usId) {
                    return;
                }
                await this.toggleHiddenUserStoryAsync(message.usId);
                return;
            case "toggleSearchIncludesOtherOwners":
                await this.toggleSearchIncludesOtherOwnersAsync();
                return;
            case "toggleShowHiddenUserStories":
                this.showHiddenUserStories = !this.showHiddenUserStories;
                await this.safeRenderAsync();
                return;
            case "toggleCompletedUserStories":
                this.showCompletedUserStories = !this.showCompletedUserStories;
                await this.safeRenderAsync();
                return;
            case "toggleBlockedUserStories":
                this.showBlockedUserStories = !this.showBlockedUserStories;
                await this.safeRenderAsync();
                return;
            case "initializeRepoPrompts":
                await this.runBusyActionAsync("Exporting prompt templates...", async () => {
                    await this.initializeRepoPromptsFromSidebarAsync();
                    await this.safeRenderAsync();
                });
                return;
            case "openPromptTemplates":
                await vscode.commands.executeCommand("specForge.openPromptTemplates");
                return;
            case "openSettings":
                await vscode.commands.executeCommand("specForge.openExecutionSettings");
                return;
            case "submitCreateForm":
                await this.runBusyActionAsync("Creating user story...", async () => {
                    await this.submitCreateFormAsync(message);
                });
                return;
        }
    }
    async runBusyActionAsync(message, action) {
        this.busyMessage = message;
        await this.safeRenderAsync();
        try {
            await action();
        }
        finally {
            this.busyMessage = null;
            await this.safeRenderAsync();
        }
    }
    async submitCreateFormAsync(message) {
        const workspaceRoot = getWorkspaceRoot();
        if (!workspaceRoot) {
            void vscode.window.showWarningMessage("Open a workspace folder before creating a user story.");
            return;
        }
        const title = message.title?.trim();
        const kind = message.kind?.trim();
        const category = message.category?.trim();
        const tags = parseCustomTags(message.tags);
        const externalReferenceUrl = message.externalReferenceUrl?.trim();
        const intakeMode = message.intakeMode === "wizard" ? "wizard" : "freeform";
        const sourceText = intakeMode === "wizard"
            ? (0, userStoryIntake_1.buildWizardSourceText)(message.wizardDraft).trim()
            : message.sourceText?.trim();
        if (intakeMode === "wizard") {
            const missingFields = (0, userStoryIntake_1.getWizardMissingFields)(message.wizardDraft);
            if (missingFields.length > 0) {
                void vscode.window.showWarningMessage(`The guided wizard still needs ${missingFields.join(", ")}.`);
                return;
            }
        }
        if (!title || !kind || !category || !sourceText) {
            void vscode.window.showWarningMessage("Title, kind, category, and source are required.");
            return;
        }
        const backendClient = (0, specsExplorer_1.getOrCreateBackendClient)(workspaceRoot);
        const summaries = await backendClient.listUserStories();
        const usId = (0, explorerModel_1.nextUserStoryIdFromSummaries)(summaries);
        const result = await backendClient.createUserStory(usId, title, kind, category, sourceText, (0, userActor_1.getCurrentActor)(workspaceRoot), tags, externalReferenceUrl ? [{ url: externalReferenceUrl, label: "", provider: "" }] : undefined);
        await this.materializeCreateFilesAsync(result.rootDirectory);
        this.showCreateForm = false;
        this.createFiles = [];
        this.createFileMode = "context";
        await this.onDidCreateUserStory();
        const createdSummary = await backendClient.getUserStorySummary(usId);
        await vscode.commands.executeCommand("specForge.openWorkflowView", createdSummary);
        await openTextDocument(result.mainArtifactPath);
    }
    async openWorkflowAsync(usId) {
        const workspaceRoot = getWorkspaceRoot();
        if (!workspaceRoot) {
            return;
        }
        const summary = await (0, specsExplorer_1.getOrCreateBackendClient)(workspaceRoot).getUserStorySummary(usId);
        await vscode.commands.executeCommand("specForge.openWorkflowView", summary);
    }
    async openMainArtifactAsync(usId) {
        const workspaceRoot = getWorkspaceRoot();
        if (!workspaceRoot) {
            return;
        }
        const summary = await (0, specsExplorer_1.getOrCreateBackendClient)(workspaceRoot).getUserStorySummary(usId);
        await vscode.commands.executeCommand("specForge.openMainArtifact", summary);
    }
    async showEditUserStoryFormAsync(usId) {
        const workspaceRoot = getWorkspaceRoot();
        if (!workspaceRoot) {
            return;
        }
        const backendClient = (0, specsExplorer_1.getOrCreateBackendClient)(workspaceRoot);
        const summary = await backendClient.getUserStorySummary(usId);
        const categories = [...new Set([...(await getUserStoryCategoriesAsync(workspaceRoot)), summary.category])];
        const title = await vscode.window.showInputBox({
            title: `Edit ${usId}`,
            prompt: "User story title",
            value: editableUserStoryTitle(summary.usId, summary.title),
            ignoreFocusOut: true,
            validateInput: (value) => value.trim().length === 0 ? "Title is required." : null
        });
        if (title === undefined) {
            return;
        }
        const owner = await vscode.window.showInputBox({
            title: `Edit ${usId}`,
            prompt: "Owner",
            value: summary.owner,
            ignoreFocusOut: true,
            validateInput: (value) => value.trim().length === 0 ? "Owner is required." : null
        });
        if (owner === undefined) {
            return;
        }
        const category = await vscode.window.showQuickPick(categories.map((item) => ({
            label: item,
            picked: item === summary.category
        })), {
            title: `Edit ${usId}`,
            placeHolder: "Category",
            ignoreFocusOut: true
        });
        if (!category) {
            return;
        }
        const workflow = await backendClient.getUserStoryWorkflow(usId);
        const kind = await vscode.window.showQuickPick(USER_STORY_KINDS.map((item) => ({
            label: item,
            picked: item === (workflow.kind ?? "feature")
        })), {
            title: `Edit ${usId}`,
            placeHolder: "Kind",
            ignoreFocusOut: true
        });
        if (!kind) {
            return;
        }
        const tags = await vscode.window.showInputBox({
            title: `Edit ${usId}`,
            prompt: "Tags (comma-separated)",
            value: (summary.tags ?? []).join(", "),
            ignoreFocusOut: true
        });
        if (tags === undefined) {
            return;
        }
        const externalReferenceUrl = await vscode.window.showInputBox({
            title: `Edit ${usId}`,
            prompt: "External issue URL",
            value: summary.externalReferences?.[0]?.url ?? "",
            ignoreFocusOut: true,
            validateInput: (value) => {
                const normalized = value.trim();
                if (normalized.length === 0) {
                    return null;
                }
                try {
                    const candidate = new URL(normalized);
                    return candidate.protocol === "http:" || candidate.protocol === "https:"
                        ? null
                        : "Use an absolute HTTP or HTTPS URL.";
                }
                catch {
                    return "Use an absolute HTTP or HTTPS URL.";
                }
            }
        });
        if (externalReferenceUrl === undefined) {
            return;
        }
        await this.runBusyActionAsync(`Updating ${usId} info...`, async () => {
            await backendClient.updateUserStoryInfo(usId, {
                title: title.trim(),
                kind: kind.label,
                owner: owner.trim(),
                category: category.label,
                tags: parseCustomTags(tags),
                externalReferences: externalReferenceUrl.trim().length > 0
                    ? [{ url: externalReferenceUrl.trim(), label: "", provider: "" }]
                    : [],
                actor: (0, userActor_1.getCurrentActor)(workspaceRoot)
            });
            await this.onDidCreateUserStory();
            void vscode.window.showInformationMessage(`${usId} info updated.`);
        });
    }
    async dropUserStoryAsync(usId) {
        const workspaceRoot = getWorkspaceRoot();
        if (!workspaceRoot) {
            return;
        }
        const summary = await (0, specsExplorer_1.getOrCreateBackendClient)(workspaceRoot).getUserStorySummary(usId);
        const confirmation = await vscode.window.showWarningMessage(`Drop ${usId}? It will be marked as deleted and hidden from the SpecForge panel.`, { modal: true, detail: summary.directoryPath }, "Drop US");
        if (confirmation !== "Drop US") {
            (0, outputChannel_1.appendSpecForgeLog)(`Drop US for '${usId}' was cancelled by the user.`);
            return;
        }
        const storiesRoot = path.resolve(workspaceRoot, ".specs", "us") + path.sep;
        const targetPath = path.resolve(summary.directoryPath);
        if (!targetPath.startsWith(storiesRoot)) {
            void vscode.window.showErrorMessage(`Refusing to drop '${usId}' because its path is outside .specs/us.`);
            return;
        }
        await this.runBusyActionAsync(`Dropping ${usId}...`, async () => {
            (0, workflowPanel_1.closeWorkflowView)(workspaceRoot, usId);
            await fs.promises.writeFile(path.join(targetPath, ".dropped"), `Dropped at ${new Date().toISOString()} by ${(0, userActor_1.getCurrentActor)(workspaceRoot)}.\n`, "utf8");
            const preferences = await (0, userWorkspacePreferences_1.readUserWorkspacePreferences)(workspaceRoot);
            if (preferences.starredUserStoryId === usId) {
                await (0, userWorkspacePreferences_1.setStarredUserStory)(workspaceRoot, null);
            }
            await this.onDidCreateUserStory();
            void vscode.window.showInformationMessage(`${usId} dropped.`);
        });
    }
    async recoverUserStoryAsync(usId) {
        const workspaceRoot = getWorkspaceRoot();
        if (!workspaceRoot) {
            return;
        }
        const summary = await (0, specsExplorer_1.getOrCreateBackendClient)(workspaceRoot).getUserStorySummary(usId);
        const confirmation = await vscode.window.showWarningMessage(`Recover ${usId}? It will appear again in the active SpecForge panel.`, { modal: true, detail: summary.directoryPath }, "Recover US");
        if (confirmation !== "Recover US") {
            (0, outputChannel_1.appendSpecForgeLog)(`Recover US for '${usId}' was cancelled by the user.`);
            return;
        }
        const storiesRoot = path.resolve(workspaceRoot, ".specs", "us") + path.sep;
        const targetPath = path.resolve(summary.directoryPath);
        if (!targetPath.startsWith(storiesRoot)) {
            void vscode.window.showErrorMessage(`Refusing to recover '${usId}' because its path is outside .specs/us.`);
            return;
        }
        await this.runBusyActionAsync(`Recovering ${usId}...`, async () => {
            await fs.promises.rm(path.join(targetPath, ".dropped"), { force: true });
            await this.onDidCreateUserStory();
            void vscode.window.showInformationMessage(`${usId} recovered.`);
        });
    }
    async resetUserStoryToCaptureAsync(usId) {
        const workspaceRoot = getWorkspaceRoot();
        if (!workspaceRoot) {
            return;
        }
        const confirmation = await vscode.window.showWarningMessage(`Reset ${usId} to capture and delete all generated artifacts after the source?`, { modal: true }, "Reset Workflow");
        if (confirmation !== "Reset Workflow") {
            (0, outputChannel_1.appendSpecForgeLog)(`Sidebar reset to capture for '${usId}' was cancelled by the user.`);
            return;
        }
        await this.runBusyActionAsync(`Resetting ${usId} to capture...`, async () => {
            (0, outputChannel_1.appendSpecForgeLog)(`Sidebar reset to capture for '${usId}' confirmed by the user.`);
            const result = await (0, specsExplorer_1.getOrCreateBackendClient)(workspaceRoot).resetUserStoryToCapture(usId);
            (0, outputChannel_1.appendSpecForgeLog)(`Workflow '${usId}' was reset to '${result.currentPhase}' with status '${result.status}' from sidebar.`);
            (0, outputChannel_1.appendSpecForgeLog)(`Workflow '${usId}' reset deleted paths: ${result.deletedPaths.length > 0 ? result.deletedPaths.join(", ") : "(none)"}.`);
            (0, outputChannel_1.appendSpecForgeLog)(`Workflow '${usId}' reset preserved paths: ${result.preservedPaths.length > 0 ? result.preservedPaths.join(", ") : "(none)"}.`);
            await this.onDidCreateUserStory();
            void vscode.window.showInformationMessage(`${usId} reset to capture.`);
        });
    }
    async analyzeRepairUserStoryAsync(usId) {
        const workspaceRoot = getWorkspaceRoot();
        if (!workspaceRoot) {
            return;
        }
        let shouldOfferRepair = false;
        let candidateCount = 0;
        let targetPhase = null;
        await this.runBusyActionAsync("Analyzing user story lineage...", async () => {
            const analysis = await (0, specsExplorer_1.getOrCreateBackendClient)(workspaceRoot).analyzeUserStoryLineage(usId);
            candidateCount = analysis.deprecatedCandidatePaths.length;
            targetPhase = analysis.recommendedTargetPhase;
            shouldOfferRepair = analysis.status === "inconsistent" && candidateCount > 0 && targetPhase !== null;
            (0, outputChannel_1.appendSpecForgeLog)(`Lineage analysis for '${usId}': status=${analysis.status}, findings=${analysis.findings.length}, deprecatedCandidates=${analysis.deprecatedCandidatePaths.length}.`);
            const firstFinding = analysis.findings[0];
            const message = analysis.status === "clean"
                ? `${usId} lineage is clean.`
                : `${usId} lineage is ${analysis.status}: ${firstFinding?.summary ?? "Review the SpecForge output for details."}`;
            if (analysis.status === "clean") {
                void vscode.window.showInformationMessage(message);
            }
            else if (!shouldOfferRepair) {
                void vscode.window.showWarningMessage(`${message} Candidate artifacts: ${analysis.deprecatedCandidatePaths.length}.`);
            }
        });
        if (!shouldOfferRepair || targetPhase === null) {
            return;
        }
        const repairLabel = "Repair";
        const selection = await vscode.window.showWarningMessage(`${usId} lineage is inconsistent. Repair will move ${candidateCount} generated artifact(s) to deprecated/ and return the workflow to ${targetPhase}.`, { modal: true }, repairLabel);
        if (selection !== repairLabel) {
            (0, outputChannel_1.appendSpecForgeLog)(`Lineage repair for '${usId}' was cancelled by the user.`);
            return;
        }
        await this.runBusyActionAsync("Repairing user story lineage...", async () => {
            const repair = await (0, specsExplorer_1.getOrCreateBackendClient)(workspaceRoot).repairUserStoryLineage(usId, (0, userActor_1.getCurrentActor)(workspaceRoot));
            (0, outputChannel_1.appendSpecForgeLog)(`Lineage repair for '${usId}': status=${repair.status}, currentPhase=${repair.currentPhase}, archived=${repair.archivedPaths.length}, archive='${repair.archiveDirectoryPath}'.`);
            await this.onDidCreateUserStory();
            void vscode.window.showInformationMessage(`${usId} repaired. Archived ${repair.archivedPaths.length} artifact(s) and returned to ${repair.currentPhase}.`);
        });
    }
    async toggleStarredUserStoryAsync(usId) {
        const workspaceRoot = getWorkspaceRoot();
        if (!workspaceRoot) {
            return;
        }
        const preferences = await (0, userWorkspacePreferences_1.readUserWorkspacePreferences)(workspaceRoot);
        const nextStarredUserStoryId = preferences.starredUserStoryId === usId ? null : usId;
        await (0, userWorkspacePreferences_1.setStarredUserStory)(workspaceRoot, nextStarredUserStoryId);
        await this.safeRenderAsync();
    }
    async toggleWatchingUserStoryAsync(usId) {
        const workspaceRoot = getWorkspaceRoot();
        if (!workspaceRoot) {
            return;
        }
        const preferences = await (0, userWorkspacePreferences_1.readUserWorkspacePreferences)(workspaceRoot);
        const watching = new Set(preferences.watchingUserStoryIds);
        if (watching.has(usId)) {
            watching.delete(usId);
        }
        else {
            watching.add(usId);
        }
        await (0, userWorkspacePreferences_1.setWatchingUserStoryIds)(workspaceRoot, [...watching]);
        await this.safeRenderAsync();
    }
    async toggleSidebarVisibilityUserStoryAsync(usId, owner) {
        const workspaceRoot = getWorkspaceRoot();
        if (!workspaceRoot) {
            return;
        }
        const preferences = await (0, userWorkspacePreferences_1.readUserWorkspacePreferences)(workspaceRoot);
        const watching = new Set(preferences.watchingUserStoryIds);
        const hidden = new Set(preferences.hiddenUserStoryIds);
        const normalizedUsId = usId.trim().toUpperCase();
        const normalizedOwner = (owner ?? "").trim().toLowerCase();
        const normalizedActor = (0, userActor_1.getCurrentActor)(workspaceRoot).trim().toLowerCase();
        const isOwnedByCurrentActor = normalizedOwner.length > 0 && normalizedOwner === normalizedActor;
        const isHidden = hidden.has(normalizedUsId);
        const isWatched = watching.has(normalizedUsId);
        const isVisibleInSidebar = !isHidden
            && (preferences.searchIncludesOtherOwners || isWatched || isOwnedByCurrentActor);
        if (isVisibleInSidebar) {
            hidden.add(normalizedUsId);
            watching.delete(normalizedUsId);
        }
        else {
            hidden.delete(normalizedUsId);
            if (!isOwnedByCurrentActor) {
                watching.add(normalizedUsId);
            }
        }
        await (0, userWorkspacePreferences_1.writeUserWorkspacePreferences)(workspaceRoot, {
            ...preferences,
            hiddenUserStoryIds: [...hidden],
            watchingUserStoryIds: [...watching]
        });
        await this.safeRenderAsync();
    }
    async toggleHiddenUserStoryAsync(usId) {
        const workspaceRoot = getWorkspaceRoot();
        if (!workspaceRoot) {
            return;
        }
        const preferences = await (0, userWorkspacePreferences_1.readUserWorkspacePreferences)(workspaceRoot);
        const hidden = new Set(preferences.hiddenUserStoryIds);
        if (hidden.has(usId)) {
            hidden.delete(usId);
        }
        else {
            hidden.add(usId);
        }
        await (0, userWorkspacePreferences_1.setHiddenUserStoryIds)(workspaceRoot, [...hidden]);
        await this.safeRenderAsync();
    }
    async toggleSearchIncludesOtherOwnersAsync() {
        const workspaceRoot = getWorkspaceRoot();
        if (!workspaceRoot) {
            return;
        }
        const preferences = await (0, userWorkspacePreferences_1.readUserWorkspacePreferences)(workspaceRoot);
        await (0, userWorkspacePreferences_1.setSearchIncludesOtherOwners)(workspaceRoot, !preferences.searchIncludesOtherOwners);
        await this.safeRenderAsync();
    }
    async addCreateFilesAsync(kind) {
        const selection = await vscode.window.showOpenDialog({
            canSelectFiles: true,
            canSelectFolders: false,
            canSelectMany: true,
            openLabel: kind === "context" ? "Add context files" : "Add user story files"
        });
        if (!selection || selection.length === 0) {
            return;
        }
        const nextFiles = new Map(this.createFiles.map((file) => [file.sourcePath, file]));
        for (const source of selection) {
            nextFiles.set(source.fsPath, {
                sourcePath: source.fsPath,
                name: path.basename(source.fsPath),
                kind
            });
        }
        this.createFiles = [...nextFiles.values()].sort((left, right) => left.name.localeCompare(right.name));
        await this.safeRenderAsync();
    }
    async addCreateFilePathsAsync(kind, paths) {
        const normalizedPaths = paths
            .map((entry) => entry.trim())
            .filter((entry) => entry.length > 0);
        if (normalizedPaths.length === 0) {
            return;
        }
        const nextFiles = new Map(this.createFiles.map((file) => [file.sourcePath, file]));
        for (const sourcePath of normalizedPaths) {
            nextFiles.set(sourcePath, {
                sourcePath,
                name: path.basename(sourcePath),
                kind
            });
        }
        this.createFiles = [...nextFiles.values()].sort((left, right) => left.name.localeCompare(right.name));
        await this.safeRenderAsync();
    }
    async loadCreateSourceFromFileAsync() {
        const selection = await vscode.window.showOpenDialog({
            canSelectFiles: true,
            canSelectFolders: false,
            canSelectMany: false,
            openLabel: "Load user story source"
        });
        const sourceUri = selection?.[0];
        if (!sourceUri || !this.webviewView) {
            return;
        }
        const sourceText = await fs.promises.readFile(sourceUri.fsPath, "utf8");
        const firstHeading = sourceText.split(/\r?\n/).find((line) => /^#\s+/.test(line)) ?? "";
        const suggestedTitle = firstHeading.replace(/^#\s+/, "").trim();
        await this.webviewView.webview.postMessage({
            command: "loadedCreateSourceFile",
            sourceText,
            suggestedTitle,
            sourcePath: sourceUri.fsPath
        });
    }
    async scanCreateSourceReferencesAsync(sourceText) {
        if (!this.webviewView) {
            return;
        }
        const workspaceRoot = getWorkspaceRoot();
        const scanVersion = ++this.createReferenceScanVersion;
        if (!workspaceRoot || sourceText.trim().length === 0) {
            await this.webviewView.webview.postMessage({
                command: "updateCreateSourceReferences",
                files: []
            });
            return;
        }
        const files = await (0, sourceFileReferences_1.findReferencedWorkspaceFilesAsync)(workspaceRoot, sourceText, this.createFiles.map((file) => file.sourcePath));
        if (scanVersion !== this.createReferenceScanVersion) {
            return;
        }
        await this.webviewView.webview.postMessage({
            command: "updateCreateSourceReferences",
            files: files.map((file) => serializeReferencedFile(file))
        });
    }
    async materializeCreateFilesAsync(userStoryDirectoryPath) {
        for (const file of this.createFiles) {
            const targetDirectoryPath = path.join(userStoryDirectoryPath, file.kind === "context" ? "context" : "attachments");
            await fs.promises.mkdir(targetDirectoryPath, { recursive: true });
            const targetPath = await (0, utils_1.getNextAttachmentPathAsync)(targetDirectoryPath, file.name);
            await fs.promises.copyFile(file.sourcePath, targetPath);
        }
    }
    async initializeRepoPromptsFromSidebarAsync() {
        const workspaceRoot = getWorkspaceRoot();
        if (!workspaceRoot) {
            return;
        }
        const promptsStatus = await (0, repoPromptsStatus_1.getRepoPromptsStatusAsync)(workspaceRoot);
        if (!promptsStatus.initialized) {
            await vscode.commands.executeCommand("specForge.initializeRepoPrompts", false);
            return;
        }
        const confirmLabel = "Overwrite Prompts";
        const selection = await vscode.window.showWarningMessage("Repo prompts are already initialized. Overwriting them will discard any local prompt edits.", { modal: true }, confirmLabel);
        if (selection !== confirmLabel) {
            return;
        }
        await vscode.commands.executeCommand("specForge.initializeRepoPrompts", true);
    }
    async renderAsync() {
        if (!this.webviewView) {
            return;
        }
        const workspaceRoot = getWorkspaceRoot();
        if (!workspaceRoot) {
            const settingsStatus = (0, extensionSettings_1.getSpecForgeSettingsStatus)((0, extensionSettings_1.getSpecForgeSettings)());
            const settings = (0, extensionSettings_1.getSpecForgeSettings)();
            const runtimeVersion = await (0, runtimeVersion_1.readRuntimeVersionAsync)();
            this.webviewView.webview.html = (0, sidebarViewContent_1.buildSidebarHtml)({
                hasWorkspace: false,
                showCreateForm: false,
                createSurface: "main-window",
                busyMessage: this.busyMessage,
                promptsInitialized: false,
                promptsMessage: null,
                settingsConfigured: settingsStatus.executionConfigured,
                settingsMessage: settingsStatus.message,
                starredUserStoryId: null,
                activeWorkflowUsId: this.activeWorkflowUsId,
                runtimeVersion,
                viewMode: settings.userStoryListViewMode ?? "category",
                showDroppedUserStories: this.showDroppedUserStories,
                showCompletedUserStories: this.showCompletedUserStories,
                showBlockedUserStories: this.showBlockedUserStories,
                showHiddenUserStories: this.showHiddenUserStories,
                searchIncludesOtherOwners: false,
                currentActor: (0, userActor_1.getCurrentActor)(),
                watchingUserStoryIds: [],
                hiddenUserStoryIds: [],
                maxVisibleUserStories: null,
                totalUserStoryCount: 0,
                droppedUserStoryCount: 0,
                createFileMode: this.createFileMode,
                createFiles: this.createFiles,
                createFormResetToken: this.createFormResetToken,
                typographyCssVars: (0, webviewTypography_1.getEditorTypographyCssVars)(),
                categories: [],
                userStories: []
            });
            return;
        }
        const hasPersistedStories = await hasPersistedUserStoriesAsync(workspaceRoot);
        (0, outputChannel_1.appendSpecForgeLog)(`Sidebar persisted user story probe for '${workspaceRoot}': ${hasPersistedStories}.`);
        const backendClient = (0, specsExplorer_1.getOrCreateBackendClient)(workspaceRoot);
        const allVisibleUserStories = hasPersistedStories
            ? await backendClient.listUserStories(this.showDroppedUserStories ? "dropped" : "active")
            : [];
        const droppedUserStoryCount = hasPersistedStories
            ? (this.showDroppedUserStories ? allVisibleUserStories.length : (await backendClient.listUserStories("dropped")).length)
            : 0;
        const categories = await getUserStoryCategoriesAsync(workspaceRoot);
        const promptsStatus = await (0, repoPromptsStatus_1.getRepoPromptsStatusAsync)(workspaceRoot);
        const settings = (0, extensionSettings_1.getSpecForgeSettings)();
        const settingsStatus = (0, extensionSettings_1.getSpecForgeSettingsStatus)(settings);
        if (!settingsStatus.executionConfigured) {
            (0, outputChannel_1.appendSpecForgeLog)(`Sidebar settings warning for '${workspaceRoot}': ${settingsStatus.message}. Diagnostics: ${settingsStatus.diagnostics}`);
        }
        if (!promptsStatus.initialized) {
            (0, outputChannel_1.appendSpecForgeLog)(`Sidebar prompt override warning for '${workspaceRoot}': ${promptsStatus.message ?? "prompt overrides not materialized"}. Checked: ${promptsStatus.checkedPaths.join(", ")}`);
        }
        const preferences = await (0, userWorkspacePreferences_1.readUserWorkspacePreferences)(workspaceRoot);
        const currentActor = (0, userActor_1.getCurrentActor)(workspaceRoot);
        const filteredUserStories = filterSidebarUserStories(allVisibleUserStories, preferences, currentActor, {
            showDroppedUserStories: this.showDroppedUserStories,
            showCompletedUserStories: this.showCompletedUserStories,
            showBlockedUserStories: this.showBlockedUserStories,
            showHiddenUserStories: this.showHiddenUserStories
        });
        const runtimeVersion = await (0, runtimeVersion_1.readRuntimeVersionAsync)();
        this.webviewView.webview.html = (0, sidebarViewContent_1.buildSidebarHtml)({
            hasWorkspace: true,
            showCreateForm: false,
            createSurface: "main-window",
            busyMessage: this.busyMessage,
            promptsInitialized: promptsStatus.initialized,
            promptsMessage: promptsStatus.message,
            settingsConfigured: settingsStatus.executionConfigured,
            settingsMessage: settingsStatus.message,
            starredUserStoryId: preferences.starredUserStoryId,
            activeWorkflowUsId: this.activeWorkflowUsId,
            runtimeVersion,
            viewMode: settings.userStoryListViewMode ?? "category",
            showDroppedUserStories: this.showDroppedUserStories,
            showCompletedUserStories: this.showCompletedUserStories,
            showBlockedUserStories: this.showBlockedUserStories,
            showHiddenUserStories: this.showHiddenUserStories,
            searchIncludesOtherOwners: preferences.searchIncludesOtherOwners,
            currentActor,
            watchingUserStoryIds: preferences.watchingUserStoryIds,
            hiddenUserStoryIds: preferences.hiddenUserStoryIds,
            maxVisibleUserStories: preferences.maxVisibleUserStories,
            droppedUserStoryCount,
            createFileMode: this.createFileMode,
            createFiles: this.createFiles,
            createFormResetToken: this.createFormResetToken,
            typographyCssVars: (0, webviewTypography_1.getEditorTypographyCssVars)(),
            categories,
            userStories: filteredUserStories.visibleStories,
            totalUserStoryCount: filteredUserStories.totalInScope
        });
    }
    async safeRenderAsync() {
        try {
            await this.renderAsync();
        }
        catch (error) {
            if (!this.webviewView) {
                return;
            }
            this.webviewView.webview.html = (0, sidebarViewContent_1.buildSidebarHtml)({
                hasWorkspace: true,
                showCreateForm: false,
                createSurface: "main-window",
                busyMessage: this.busyMessage,
                promptsInitialized: false,
                promptsMessage: null,
                settingsConfigured: false,
                settingsMessage: "SpecForge.AI settings could not be evaluated.",
                starredUserStoryId: null,
                activeWorkflowUsId: this.activeWorkflowUsId,
                runtimeVersion: await (0, runtimeVersion_1.readRuntimeVersionAsync)(),
                viewMode: (0, extensionSettings_1.getSpecForgeSettings)().userStoryListViewMode ?? "category",
                showDroppedUserStories: this.showDroppedUserStories,
                showCompletedUserStories: this.showCompletedUserStories,
                showBlockedUserStories: this.showBlockedUserStories,
                showHiddenUserStories: this.showHiddenUserStories,
                searchIncludesOtherOwners: false,
                currentActor: (0, userActor_1.getCurrentActor)(),
                watchingUserStoryIds: [],
                hiddenUserStoryIds: [],
                maxVisibleUserStories: null,
                totalUserStoryCount: 0,
                droppedUserStoryCount: 0,
                createFileMode: this.createFileMode,
                createFiles: this.createFiles,
                createFormResetToken: this.createFormResetToken,
                typographyCssVars: (0, webviewTypography_1.getEditorTypographyCssVars)(),
                categories: [],
                userStories: []
            });
            void vscode.window.showErrorMessage(`SpecForge sidebar failed to load: ${(0, utils_1.asErrorMessage)(error)}`);
        }
    }
}
exports.SidebarViewProvider = SidebarViewProvider;
function filterSidebarUserStories(userStories, preferences, currentActor, options) {
    const normalizedActor = currentActor.trim().toLowerCase();
    const hiddenIds = new Set(preferences.hiddenUserStoryIds);
    const watchingIds = new Set(preferences.watchingUserStoryIds);
    const maxVisible = preferences.maxVisibleUserStories ?? 100;
    const filtered = userStories.filter((summary) => {
        if (!options.showCompletedUserStories && summary.currentPhase === "completed") {
            return false;
        }
        if (!options.showBlockedUserStories && summary.status === "blocked") {
            return false;
        }
        if (!options.showHiddenUserStories && hiddenIds.has(summary.usId)) {
            return false;
        }
        if (options.showDroppedUserStories) {
            return preferences.searchIncludesOtherOwners
                || watchingIds.has(summary.usId)
                || summary.owner.trim().toLowerCase() === normalizedActor;
        }
        return preferences.searchIncludesOtherOwners
            || watchingIds.has(summary.usId)
            || summary.owner.trim().toLowerCase() === normalizedActor;
    });
    const prioritized = [...filtered].sort((left, right) => compareSidebarStories(left, right, preferences, normalizedActor));
    return {
        visibleStories: preferences.searchIncludesOtherOwners
            ? prioritized
            : prioritized.slice(0, maxVisible),
        totalInScope: prioritized.length
    };
}
function editableUserStoryTitle(usId, title) {
    const normalizedTitle = title.trim();
    if (!normalizedTitle) {
        return "";
    }
    return normalizedTitle.startsWith(`${usId} `) || normalizedTitle.startsWith(`${usId}·`)
        || normalizedTitle.startsWith(`${usId}-`) || normalizedTitle.startsWith(`${usId}:`)
        ? normalizedTitle.slice(usId.length).trimStart().replace(/^[·\-:]\s*/, "")
        : normalizedTitle;
}
function compareSidebarStories(left, right, preferences, currentActor) {
    const watching = new Set(preferences.watchingUserStoryIds);
    const leftScore = sidebarPriority(left, preferences.starredUserStoryId, watching, currentActor);
    const rightScore = sidebarPriority(right, preferences.starredUserStoryId, watching, currentActor);
    if (leftScore !== rightScore) {
        return rightScore - leftScore;
    }
    return left.usId.localeCompare(right.usId);
}
function sidebarPriority(summary, starredUserStoryId, watchingUserStoryIds, currentActor) {
    let score = 0;
    if (summary.usId === starredUserStoryId) {
        score += 100;
    }
    if (watchingUserStoryIds.has(summary.usId)) {
        score += 50;
    }
    if (summary.owner.trim().toLowerCase() === currentActor) {
        score += 25;
    }
    if (summary.status === "waiting-user") {
        score += 10;
    }
    if (summary.status === "blocked") {
        score += 5;
    }
    return score;
}
function parseCustomTags(value) {
    return [...new Set((value ?? "")
            .split(",")
            .map((tag) => tag.trim().replace(/^#/, "").toLowerCase())
            .filter((tag) => tag.length > 0))];
}
function serializeReferencedFile(file) {
    return {
        sourcePath: file.sourcePath,
        workspaceRelativePath: file.workspaceRelativePath,
        name: file.name
    };
}
async function getUserStoryCategoriesAsync(workspaceRoot) {
    const configPath = path.join(workspaceRoot, ".specs", "config.yaml");
    if (!await pathExistsAsync(configPath)) {
        return explorerModel_1.DEFAULT_USER_STORY_CATEGORIES;
    }
    const yaml = await fs.promises.readFile(configPath, "utf8");
    const categories = (0, explorerModel_1.parseYamlSequence)(yaml, "categories");
    return categories.length === 0 ? explorerModel_1.DEFAULT_USER_STORY_CATEGORIES : categories;
}
function getWorkspaceRoot() {
    return vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;
}
async function pathExistsAsync(filePath) {
    try {
        await fs.promises.access(filePath, fs.constants.F_OK);
        return true;
    }
    catch {
        return false;
    }
}
async function hasPersistedUserStoriesAsync(workspaceRoot) {
    const storiesRoot = path.join(workspaceRoot, ".specs", "us");
    if (!await pathExistsAsync(storiesRoot)) {
        return false;
    }
    const categoryEntries = await fs.promises.readdir(storiesRoot, { withFileTypes: true });
    for (const categoryEntry of categoryEntries) {
        if (!categoryEntry.isDirectory() || /^US-/i.test(categoryEntry.name)) {
            continue;
        }
        const categoryPath = path.join(storiesRoot, categoryEntry.name);
        const userStoryEntries = await fs.promises.readdir(categoryPath, { withFileTypes: true });
        if (userStoryEntries.some((entry) => entry.isDirectory() && /^US-/i.test(entry.name))) {
            return true;
        }
    }
    return categoryEntries.some((entry) => entry.isDirectory() && /^US-/i.test(entry.name));
}
async function openTextDocument(filePath) {
    const document = await vscode.workspace.openTextDocument(filePath);
    await vscode.window.showTextDocument(document, { preview: false });
}
//# sourceMappingURL=sidebarView.js.map