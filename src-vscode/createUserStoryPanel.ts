import * as fs from "node:fs";
import * as path from "node:path";
import * as vscode from "vscode";
import type { UserStorySummary } from "./backendClient";
import { getSpecForgeSettings, getSpecForgeSettingsStatus } from "./extensionSettings";
import { DEFAULT_USER_STORY_CATEGORIES, nextUserStoryIdFromSummaries, parseYamlSequence } from "./explorerModel";
import { getRepoPromptsStatusAsync } from "./repoPromptsStatus";
import { readRuntimeVersionAsync } from "./runtimeVersion";
import { buildSidebarHtml } from "./sidebarViewContent";
import { findReferencedWorkspaceFilesAsync, type ReferencedWorkspaceFile } from "./sourceFileReferences";
import { getOrCreateBackendClient } from "./specsExplorer";
import { getCurrentActor } from "./userActor";
import {
  buildWizardSourceText,
  getWizardMissingFields,
  type CreateIntakeMode,
  type UserStoryWizardDraft
} from "./userStoryIntake";
import { asErrorMessage, getNextAttachmentPathAsync } from "./utils";
import { getEditorTypographyCssVars } from "./webviewTypography";

type DraftCreateFile = {
  readonly sourcePath: string;
  readonly name: string;
  readonly kind: "context" | "attachment";
};

type CreatePanelMessage =
  | { readonly command: "hideCreateForm" }
  | { readonly command: "openExecutionSettings" }
  | { readonly command: "initializeRepoPrompts" }
  | { readonly command: "openPromptTemplates" }
  | { readonly command: "setCreateFileMode"; readonly kind?: string }
  | { readonly command: "addCreateFiles"; readonly kind?: string }
  | { readonly command: "addCreateFilePaths"; readonly kind?: string; readonly paths?: readonly string[] }
  | { readonly command: "loadCreateSourceFromFile" }
  | { readonly command: "scanCreateSourceReferences"; readonly sourceText?: string }
  | { readonly command: "setCreateFileKind"; readonly sourcePath?: string; readonly kind?: string }
  | { readonly command: "removeCreateFile"; readonly sourcePath?: string }
  | {
    readonly command: "submitCreateForm";
    readonly title?: string;
    readonly kind?: string;
    readonly category?: string;
    readonly tags?: string;
    readonly intakeMode?: CreateIntakeMode;
    readonly sourceText?: string;
    readonly wizardDraft?: Partial<UserStoryWizardDraft>;
  };

let createPanelController: CreateUserStoryPanelController | null = null;

export async function openCreateUserStoryPanelAsync(
  extensionUri: vscode.Uri,
  onDidCreateUserStory: () => Promise<void>
): Promise<void> {
  if (!createPanelController) {
    createPanelController = new CreateUserStoryPanelController(extensionUri, onDidCreateUserStory, () => {
      createPanelController = null;
    });
  }

  await createPanelController.showAsync();
}

class CreateUserStoryPanelController {
  private readonly panel: vscode.WebviewPanel;
  private busyMessage: string | null = null;
  private createFileMode: "context" | "attachment" = "context";
  private createFiles: DraftCreateFile[] = [];
  private createReferenceScanVersion = 0;
  private createFormResetToken = 0;

  public constructor(
    extensionUri: vscode.Uri,
    private readonly onDidCreateUserStory: () => Promise<void>,
    private readonly onDidDispose: () => void
  ) {
    this.panel = vscode.window.createWebviewPanel(
      "specForge.createUserStory",
      "New user story",
      vscode.ViewColumn.Active,
      {
        enableScripts: true,
        retainContextWhenHidden: true,
        localResourceRoots: [extensionUri]
      }
    );

    this.panel.onDidDispose(() => {
      this.onDidDispose();
    });

    this.panel.webview.onDidReceiveMessage(async (message: CreatePanelMessage) => {
      await this.handleMessageAsync(message);
    });
  }

  public async showAsync(): Promise<void> {
    this.panel.reveal(vscode.ViewColumn.Active, false);
    this.createFileMode = "context";
    this.createFiles = [];
    this.createReferenceScanVersion += 1;
    this.createFormResetToken += 1;
    await this.safeRenderAsync();
  }

  private async handleMessageAsync(message: CreatePanelMessage): Promise<void> {
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
        await this.addCreateFilePathsAsync(
          message.kind === "attachment" ? "attachment" : "context",
          message.paths ?? []
        );
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

        this.createFiles = this.createFiles.map((file) =>
          file.sourcePath === message.sourcePath
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

  private async runBusyActionAsync(message: string, action: () => Promise<void>): Promise<void> {
    this.busyMessage = message;
    await this.safeRenderAsync();

    try {
      await action();
    } finally {
      this.busyMessage = null;
      if (this.panel.visible) {
        await this.safeRenderAsync();
      }
    }
  }

  private async submitCreateFormAsync(message: Extract<CreatePanelMessage, { command: "submitCreateForm" }>): Promise<void> {
    const workspaceRoot = getWorkspaceRoot();
    if (!workspaceRoot) {
      void vscode.window.showWarningMessage("Open a workspace folder before creating a user story.");
      return;
    }

    const title = message.title?.trim();
    const kind = message.kind?.trim();
    const category = message.category?.trim();
    const tags = parseCustomTags(message.tags);
    const intakeMode: CreateIntakeMode = message.intakeMode === "wizard" ? "wizard" : "freeform";
    const sourceText = intakeMode === "wizard"
      ? buildWizardSourceText(message.wizardDraft).trim()
      : message.sourceText?.trim();

    if (intakeMode === "wizard") {
      const missingFields = getWizardMissingFields(message.wizardDraft);
      if (missingFields.length > 0) {
        void vscode.window.showWarningMessage(`The guided wizard still needs ${missingFields.join(", ")}.`);
        return;
      }
    }

    if (!title || !kind || !category || !sourceText) {
      void vscode.window.showWarningMessage("Title, kind, category, and source are required.");
      return;
    }

    const backendClient = getOrCreateBackendClient(workspaceRoot);
    const summaries = await backendClient.listUserStories();
    const usId = nextUserStoryIdFromSummaries(summaries);
    const result = await backendClient.createUserStory(usId, title, kind, category, sourceText, getCurrentActor(workspaceRoot), tags);
    await this.materializeCreateFilesAsync(result.rootDirectory);
    this.createFiles = [];
    this.createFileMode = "context";
    await this.onDidCreateUserStory();
    const createdSummary: UserStorySummary = await backendClient.getUserStorySummary(usId);
    this.panel.dispose();
    await vscode.commands.executeCommand("specForge.openWorkflowView", createdSummary);
    await openTextDocument(result.mainArtifactPath);
  }

  private async addCreateFilesAsync(kind: "context" | "attachment"): Promise<void> {
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

  private async addCreateFilePathsAsync(kind: "context" | "attachment", paths: readonly string[]): Promise<void> {
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

  private async loadCreateSourceFromFileAsync(): Promise<void> {
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

  private async scanCreateSourceReferencesAsync(sourceText: string): Promise<void> {
    const workspaceRoot = getWorkspaceRoot();
    const scanVersion = ++this.createReferenceScanVersion;
    if (!workspaceRoot || sourceText.trim().length === 0) {
      await this.panel.webview.postMessage({
        command: "updateCreateSourceReferences",
        files: []
      });
      return;
    }

    const files = await findReferencedWorkspaceFilesAsync(
      workspaceRoot,
      sourceText,
      this.createFiles.map((file) => file.sourcePath)
    );

    if (scanVersion !== this.createReferenceScanVersion) {
      return;
    }

    await this.panel.webview.postMessage({
      command: "updateCreateSourceReferences",
      files: files.map((file) => serializeReferencedFile(file))
    });
  }

  private async materializeCreateFilesAsync(userStoryDirectoryPath: string): Promise<void> {
    for (const file of this.createFiles) {
      const targetDirectoryPath = path.join(userStoryDirectoryPath, file.kind === "context" ? "context" : "attachments");
      await fs.promises.mkdir(targetDirectoryPath, { recursive: true });
      const targetPath = await getNextAttachmentPathAsync(targetDirectoryPath, file.name);
      await fs.promises.copyFile(file.sourcePath, targetPath);
    }
  }

  private async initializeRepoPromptsAsync(): Promise<void> {
    const workspaceRoot = getWorkspaceRoot();
    if (!workspaceRoot) {
      return;
    }

    const promptsStatus = await getRepoPromptsStatusAsync(workspaceRoot);
    if (!promptsStatus.initialized) {
      await vscode.commands.executeCommand("specForge.initializeRepoPrompts", false);
      return;
    }

    const confirmLabel = "Overwrite Prompts";
    const selection = await vscode.window.showWarningMessage(
      "Repo prompts are already initialized. Overwriting them will discard any local prompt edits.",
      { modal: true },
      confirmLabel
    );

    if (selection !== confirmLabel) {
      return;
    }

    await vscode.commands.executeCommand("specForge.initializeRepoPrompts", true);
  }

  private async renderAsync(): Promise<void> {
    const workspaceRoot = getWorkspaceRoot();
    if (!workspaceRoot) {
      const settingsStatus = getSpecForgeSettingsStatus(getSpecForgeSettings());
      this.panel.webview.html = buildSidebarHtml({
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
        runtimeVersion: await readRuntimeVersionAsync(),
        showViewOptionsMenu: false,
        viewMode: "category",
        showDroppedUserStories: false,
        showCompletedUserStories: false,
        showBlockedUserStories: false,
        showHiddenUserStories: false,
        searchIncludesOtherOwners: false,
        currentActor: getCurrentActor(),
        watchingUserStoryIds: [],
        hiddenUserStoryIds: [],
        maxVisibleUserStories: null,
        totalUserStoryCount: 0,
        droppedUserStoryCount: 0,
        createFileMode: this.createFileMode,
        createFiles: this.createFiles,
        createFormResetToken: this.createFormResetToken,
        typographyCssVars: getEditorTypographyCssVars(),
        categories: [],
        userStories: []
      });
      return;
    }

    const promptsStatus = await getRepoPromptsStatusAsync(workspaceRoot);
    const settings = getSpecForgeSettings();
    const settingsStatus = getSpecForgeSettingsStatus(settings);
    const runtimeVersion = await readRuntimeVersionAsync();
    const categories = await getUserStoryCategoriesAsync(workspaceRoot);
    this.panel.webview.html = buildSidebarHtml({
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
      currentActor: getCurrentActor(workspaceRoot),
      watchingUserStoryIds: [],
      hiddenUserStoryIds: [],
      maxVisibleUserStories: null,
      totalUserStoryCount: 0,
      droppedUserStoryCount: 0,
      createFileMode: this.createFileMode,
      createFiles: this.createFiles,
      createFormResetToken: this.createFormResetToken,
      typographyCssVars: getEditorTypographyCssVars(),
      categories,
      userStories: []
    });
  }

  private async safeRenderAsync(): Promise<void> {
    try {
      await this.renderAsync();
    } catch (error) {
      this.panel.webview.html = buildSidebarHtml({
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
        runtimeVersion: await readRuntimeVersionAsync(),
        showViewOptionsMenu: false,
        viewMode: getSpecForgeSettings().userStoryListViewMode ?? "category",
        showDroppedUserStories: false,
        showCompletedUserStories: false,
        showBlockedUserStories: false,
        showHiddenUserStories: false,
        searchIncludesOtherOwners: false,
        currentActor: getCurrentActor(),
        watchingUserStoryIds: [],
        hiddenUserStoryIds: [],
        maxVisibleUserStories: null,
        totalUserStoryCount: 0,
        droppedUserStoryCount: 0,
        createFileMode: this.createFileMode,
        createFiles: this.createFiles,
        createFormResetToken: this.createFormResetToken,
        typographyCssVars: getEditorTypographyCssVars(),
        categories: [],
        userStories: []
      });
      void vscode.window.showErrorMessage(`SpecForge create panel failed to load: ${asErrorMessage(error)}`);
    }
  }
}

function parseCustomTags(value: string | undefined): readonly string[] {
  if (!value) {
    return [];
  }

  return [...new Set(value.split(",").map((entry) => entry.trim()).filter(Boolean))];
}

function serializeReferencedFile(file: ReferencedWorkspaceFile): ReferencedWorkspaceFile {
  return file;
}

async function getUserStoryCategoriesAsync(workspaceRoot: string): Promise<readonly string[]> {
  const configPath = path.join(workspaceRoot, ".specs", "config.yaml");
  if (!fs.existsSync(configPath)) {
    return DEFAULT_USER_STORY_CATEGORIES;
  }

  const raw = await fs.promises.readFile(configPath, "utf8");
  const categories = parseYamlSequence(raw, "categories");
  return categories.length > 0 ? categories : DEFAULT_USER_STORY_CATEGORIES;
}

function getWorkspaceRoot(): string | undefined {
  return vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;
}

async function openTextDocument(filePath: string): Promise<void> {
  const document = await vscode.workspace.openTextDocument(filePath);
  await vscode.window.showTextDocument(document, { preview: false });
}
