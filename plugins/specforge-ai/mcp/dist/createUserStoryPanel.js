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
exports.openCreateUserStoryPanelAsync = openCreateUserStoryPanelAsync;
const fs = __importStar(require("node:fs"));
const path = __importStar(require("node:path"));
const vscode = __importStar(require("vscode"));
const extensionSettings_1 = require("./extensionSettings");
const explorerModel_1 = require("./explorerModel");
const repoPromptsStatus_1 = require("./repoPromptsStatus");
const runtimeVersion_1 = require("./runtimeVersion");
const sidebarViewContent_1 = require("./sidebarViewContent");
const sourceFileReferences_1 = require("./sourceFileReferences");
const specsExplorer_1 = require("./specsExplorer");
const userActor_1 = require("./userActor");
const userStoryIntake_1 = require("./userStoryIntake");
const utils_1 = require("./utils");
const webviewTypography_1 = require("./webviewTypography");
let createPanelController = null;
async function openCreateUserStoryPanelAsync(extensionUri, onDidCreateUserStory) {
    if (!createPanelController) {
        createPanelController = new CreateUserStoryPanelController(extensionUri, onDidCreateUserStory, () => {
            createPanelController = null;
        });
    }
    await createPanelController.showAsync();
}
class CreateUserStoryPanelController {
    onDidCreateUserStory;
    onDidDispose;
    panel;
    busyMessage = null;
    createFileMode = "context";
    createFiles = [];
    createReferenceScanVersion = 0;
    createFormResetToken = 0;
    constructor(extensionUri, onDidCreateUserStory, onDidDispose) {
        this.onDidCreateUserStory = onDidCreateUserStory;
        this.onDidDispose = onDidDispose;
        this.panel = vscode.window.createWebviewPanel("specForge.createUserStory", "New user story", vscode.ViewColumn.Active, {
            enableScripts: true,
            retainContextWhenHidden: true,
            localResourceRoots: [extensionUri]
        });
        this.panel.onDidDispose(() => {
            this.onDidDispose();
        });
        this.panel.webview.onDidReceiveMessage(async (message) => {
            await this.handleMessageAsync(message);
        });
    }
    async showAsync() {
        this.panel.reveal(vscode.ViewColumn.Active, false);
        this.createFileMode = "context";
        this.createFiles = [];
        this.createReferenceScanVersion += 1;
        this.createFormResetToken += 1;
        await this.safeRenderAsync();
    }
    async handleMessageAsync(message) {
        if (this.busyMessage) {
            return;
        }
        switch (message.command) {
            case "hideCreateForm":
                this.panel.dispose();
                return;
            case "openExecutionSettings":
                await vscode.commands.executeCommand("specForge.openExecutionSettings");
                return;
            case "initializeRepoPrompts":
                await this.runBusyActionAsync("Exporting prompt templates...", async () => {
                    await this.initializeRepoPromptsAsync();
                    await this.safeRenderAsync();
                });
                return;
            case "openPromptTemplates":
                await vscode.commands.executeCommand("specForge.openPromptTemplates");
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
            if (this.panel.visible) {
                await this.safeRenderAsync();
            }
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
        const result = await backendClient.createUserStory(usId, title, kind, category, sourceText, (0, userActor_1.getCurrentActor)(workspaceRoot), tags);
        await this.materializeCreateFilesAsync(result.rootDirectory);
        this.createFiles = [];
        this.createFileMode = "context";
        await this.onDidCreateUserStory();
        const createdSummary = await backendClient.getUserStorySummary(usId);
        this.panel.dispose();
        await vscode.commands.executeCommand("specForge.openWorkflowView", createdSummary);
        await openTextDocument(result.mainArtifactPath);
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
        if (!sourceUri) {
            return;
        }
        const sourceText = await fs.promises.readFile(sourceUri.fsPath, "utf8");
        const firstHeading = sourceText.split(/\r?\n/).find((line) => /^#\s+/.test(line)) ?? "";
        const suggestedTitle = firstHeading.replace(/^#\s+/, "").trim();
        await this.panel.webview.postMessage({
            command: "loadedCreateSourceFile",
            sourceText,
            suggestedTitle,
            sourcePath: sourceUri.fsPath
        });
    }
    async scanCreateSourceReferencesAsync(sourceText) {
        const workspaceRoot = getWorkspaceRoot();
        const scanVersion = ++this.createReferenceScanVersion;
        if (!workspaceRoot || sourceText.trim().length === 0) {
            await this.panel.webview.postMessage({
                command: "updateCreateSourceReferences",
                files: []
            });
            return;
        }
        const files = await (0, sourceFileReferences_1.findReferencedWorkspaceFilesAsync)(workspaceRoot, sourceText, this.createFiles.map((file) => file.sourcePath));
        if (scanVersion !== this.createReferenceScanVersion) {
            return;
        }
        await this.panel.webview.postMessage({
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
    async initializeRepoPromptsAsync() {
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
        const workspaceRoot = getWorkspaceRoot();
        if (!workspaceRoot) {
            const settingsStatus = (0, extensionSettings_1.getSpecForgeSettingsStatus)((0, extensionSettings_1.getSpecForgeSettings)());
            this.panel.webview.html = (0, sidebarViewContent_1.buildSidebarHtml)({
                hasWorkspace: false,
                showCreateForm: true,
                createSurface: "main-window",
                showCreateAction: false,
                showStoryList: false,
                busyMessage: this.busyMessage,
                promptsInitialized: false,
                promptsMessage: null,
                settingsConfigured: settingsStatus.executionConfigured,
                settingsMessage: settingsStatus.message,
                starredUserStoryId: null,
                activeWorkflowUsId: null,
                runtimeVersion: await (0, runtimeVersion_1.readRuntimeVersionAsync)(),
                showViewOptionsMenu: false,
                viewMode: "category",
                showDroppedUserStories: false,
                showCompletedUserStories: false,
                showBlockedUserStories: false,
                showHiddenUserStories: false,
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
        const promptsStatus = await (0, repoPromptsStatus_1.getRepoPromptsStatusAsync)(workspaceRoot);
        const settings = (0, extensionSettings_1.getSpecForgeSettings)();
        const settingsStatus = (0, extensionSettings_1.getSpecForgeSettingsStatus)(settings);
        const runtimeVersion = await (0, runtimeVersion_1.readRuntimeVersionAsync)();
        const categories = await getUserStoryCategoriesAsync(workspaceRoot);
        this.panel.webview.html = (0, sidebarViewContent_1.buildSidebarHtml)({
            hasWorkspace: true,
            showCreateForm: true,
            createSurface: "main-window",
            showCreateAction: false,
            showStoryList: false,
            busyMessage: this.busyMessage,
            promptsInitialized: promptsStatus.initialized,
            promptsMessage: promptsStatus.message,
            settingsConfigured: settingsStatus.executionConfigured,
            settingsMessage: settingsStatus.message,
            starredUserStoryId: null,
            activeWorkflowUsId: null,
            runtimeVersion,
            showViewOptionsMenu: false,
            viewMode: settings.userStoryListViewMode ?? "category",
            showDroppedUserStories: false,
            showCompletedUserStories: false,
            showBlockedUserStories: false,
            showHiddenUserStories: false,
            searchIncludesOtherOwners: false,
            currentActor: (0, userActor_1.getCurrentActor)(workspaceRoot),
            watchingUserStoryIds: [],
            hiddenUserStoryIds: [],
            maxVisibleUserStories: null,
            totalUserStoryCount: 0,
            droppedUserStoryCount: 0,
            createFileMode: this.createFileMode,
            createFiles: this.createFiles,
            createFormResetToken: this.createFormResetToken,
            typographyCssVars: (0, webviewTypography_1.getEditorTypographyCssVars)(),
            categories,
            userStories: []
        });
    }
    async safeRenderAsync() {
        try {
            await this.renderAsync();
        }
        catch (error) {
            this.panel.webview.html = (0, sidebarViewContent_1.buildSidebarHtml)({
                hasWorkspace: true,
                showCreateForm: true,
                createSurface: "main-window",
                showCreateAction: false,
                showStoryList: false,
                busyMessage: this.busyMessage,
                promptsInitialized: false,
                promptsMessage: null,
                settingsConfigured: false,
                settingsMessage: "SpecForge.AI settings could not be evaluated.",
                starredUserStoryId: null,
                activeWorkflowUsId: null,
                runtimeVersion: await (0, runtimeVersion_1.readRuntimeVersionAsync)(),
                showViewOptionsMenu: false,
                viewMode: (0, extensionSettings_1.getSpecForgeSettings)().userStoryListViewMode ?? "category",
                showDroppedUserStories: false,
                showCompletedUserStories: false,
                showBlockedUserStories: false,
                showHiddenUserStories: false,
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
            void vscode.window.showErrorMessage(`SpecForge create panel failed to load: ${(0, utils_1.asErrorMessage)(error)}`);
        }
    }
}
function parseCustomTags(value) {
    if (!value) {
        return [];
    }
    return [...new Set(value.split(",").map((entry) => entry.trim()).filter(Boolean))];
}
function serializeReferencedFile(file) {
    return file;
}
async function getUserStoryCategoriesAsync(workspaceRoot) {
    const configPath = path.join(workspaceRoot, ".specs", "config.yaml");
    if (!fs.existsSync(configPath)) {
        return explorerModel_1.DEFAULT_USER_STORY_CATEGORIES;
    }
    const raw = await fs.promises.readFile(configPath, "utf8");
    const categories = (0, explorerModel_1.parseYamlSequence)(raw, "categories");
    return categories.length > 0 ? categories : explorerModel_1.DEFAULT_USER_STORY_CATEGORIES;
}
function getWorkspaceRoot() {
    return vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;
}
async function openTextDocument(filePath) {
    const document = await vscode.workspace.openTextDocument(filePath);
    await vscode.window.showTextDocument(document, { preview: false });
}
//# sourceMappingURL=createUserStoryPanel.js.map