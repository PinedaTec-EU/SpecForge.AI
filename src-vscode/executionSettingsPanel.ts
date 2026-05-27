import * as vscode from "vscode";
import { escapeHtml, escapeHtmlAttr } from "./htmlEscape";
import { requiresDefaultFallback, validatePhasePermissionAssignments } from "./executionSettingsModel";
import {
  getSpecForgeSettings,
  recommendedBootstrapAgentProfiles,
  recommendedBootstrapPhaseAgentAssignments,
  shouldBootstrapRecommendedAgentProfiles,
  shouldBootstrapRecommendedPhaseAgentAssignments,
  type SpecForgeAgentProfile,
  type SpecForgePhaseHarnessProfiles,
  type SpecForgeModelProfile,
  type SpecForgePhaseAgentAssignments
} from "./extensionSettings";
import { automationPhaseIcon, workflowPhaseIcon } from "./workflow-view/icons";
import { buildWebviewTypographyRootCss, getEditorTypographyCssVars } from "./webviewTypography";

type ExecutionSettingsMessage =
  | {
      readonly command: "saveExecutionSettings";
      readonly modelProfiles?: readonly Partial<SpecForgeModelProfile>[];
      readonly agentProfiles?: readonly Partial<SpecForgeAgentProfile>[];
      readonly phaseAgentAssignments?: Partial<SpecForgePhaseAgentAssignments>;
      readonly defaultHarnessProfile?: string;
      readonly phaseHarnessProfiles?: Partial<SpecForgePhaseHarnessProfiles>;
      readonly harnessProfileAuthority?: "workspace" | "central";
      readonly harnessProfileLockMode?: "none" | "phase" | "all";
      readonly lockedHarnessPhaseIds?: readonly string[];
      readonly allowPerUserStoryHarnessProfileOverrides?: boolean;
      readonly refinementTolerance?: string;
      readonly mvpRigor?: "low" | "medium" | "high";
      readonly reviewTolerance?: string;
      readonly reviewEvidencePolicy?: string;
      readonly technicalDesignSubagentsEnabled?: boolean;
      readonly reviewSubagentsEnabled?: boolean;
      readonly watcherEnabled?: boolean;
      readonly attentionNotificationsEnabled?: boolean;
      readonly contextSuggestionsEnabled?: boolean;
      readonly workflowGraphLayoutMode?: "horizontal" | "vertical";
      readonly workflowGraphInitialZoomMode?: "actual-size" | "fit-width";
      readonly userStoryListViewMode?: "category" | "phase";
      readonly visualTimelineEnabled?: boolean;
      readonly requireExplicitApprovalBranchAcceptance?: boolean;
      readonly autoRefinementAnswersEnabled?: boolean;
      readonly autoRefinementAnswersProfile?: string | null;
      readonly autoPlayEnabled?: boolean;
      readonly autoReviewEnabled?: boolean;
      readonly maxRefinementCycles?: number | null;
      readonly maxImplementationReviewCycles?: number | null;
      readonly phaseQualityGateThresholdPercent?: number | null;
      readonly refinementQualityGateMaxRetries?: number | null;
      readonly reviewQualityGateMaxRetries?: number | null;
      readonly keepBestPhaseArtifactOnQualityRegression?: boolean;
      readonly destructiveRewindEnabled?: boolean;
      readonly pauseOnFailedReview?: boolean;
      readonly useSemanticGraphWhenAvailable?: boolean;
      readonly allowGraphBuildRefreshForTouchedUserStoryScope?: boolean;
      readonly reviewLearningEnabled?: boolean;
      readonly reviewLearningSkillPath?: string | null;
      readonly completedUsLockOnCompleted?: boolean;
    }
  | { readonly command: "openRawSettings"; };

type ExecutionSettingsSaveRequest = {
  readonly modelProfiles: readonly Partial<SpecForgeModelProfile>[];
  readonly agentProfiles: readonly Partial<SpecForgeAgentProfile>[];
  readonly phaseAgentAssignments: Partial<SpecForgePhaseAgentAssignments>;
  readonly defaultHarnessProfile: string;
  readonly phaseHarnessProfiles: Partial<SpecForgePhaseHarnessProfiles>;
  readonly harnessProfileAuthority: "workspace" | "central";
  readonly harnessProfileLockMode: "none" | "phase" | "all";
  readonly lockedHarnessPhaseIds: readonly string[];
  readonly allowPerUserStoryHarnessProfileOverrides: boolean;
  readonly refinementTolerance: string;
  readonly mvpRigor: "low" | "medium" | "high";
  readonly reviewTolerance: string;
  readonly reviewEvidencePolicy: string;
  readonly technicalDesignSubagentsEnabled: boolean;
  readonly reviewSubagentsEnabled: boolean;
  readonly watcherEnabled: boolean;
  readonly attentionNotificationsEnabled: boolean;
  readonly contextSuggestionsEnabled: boolean;
  readonly workflowGraphLayoutMode: "horizontal" | "vertical";
  readonly workflowGraphInitialZoomMode: "actual-size" | "fit-width";
  readonly userStoryListViewMode: "category" | "phase";
  readonly visualTimelineEnabled: boolean;
  readonly requireExplicitApprovalBranchAcceptance: boolean;
  readonly autoRefinementAnswersEnabled: boolean;
  readonly autoRefinementAnswersProfile?: string | null;
  readonly autoPlayEnabled: boolean;
  readonly autoReviewEnabled: boolean;
  readonly maxRefinementCycles?: number | null;
  readonly maxImplementationReviewCycles?: number | null;
  readonly phaseQualityGateThresholdPercent?: number | null;
  readonly refinementQualityGateMaxRetries?: number | null;
  readonly reviewQualityGateMaxRetries?: number | null;
  readonly keepBestPhaseArtifactOnQualityRegression: boolean;
  readonly destructiveRewindEnabled: boolean;
  readonly pauseOnFailedReview: boolean;
  readonly useSemanticGraphWhenAvailable: boolean;
  readonly allowGraphBuildRefreshForTouchedUserStoryScope: boolean;
  readonly reviewLearningEnabled: boolean;
  readonly reviewLearningSkillPath?: string | null;
  readonly completedUsLockOnCompleted: boolean;
};

let currentPanel: ExecutionSettingsPanelController | null = null;

export async function openExecutionSettingsPanelAsync(
  extensionUri: vscode.Uri,
  onDidSave: () => Promise<void>
): Promise<void> {
  if (currentPanel) {
    currentPanel.reveal();
    await currentPanel.refreshAsync();
    return;
  }

  currentPanel = new ExecutionSettingsPanelController(extensionUri, onDidSave, () => {
    currentPanel = null;
  });
  currentPanel.reveal();
  await currentPanel.refreshAsync();
}

class ExecutionSettingsPanelController {
  private readonly panel: vscode.WebviewPanel;

  public constructor(
    private readonly extensionUri: vscode.Uri,
    private readonly onDidSave: () => Promise<void>,
    private readonly onDidDisposePanel: () => void
  ) {
    this.panel = vscode.window.createWebviewPanel(
      "specForge.executionSettings",
      "SpecForge Configuration",
      vscode.ViewColumn.Active,
      {
        enableScripts: true,
        retainContextWhenHidden: true,
        localResourceRoots: [extensionUri]
      }
    );

    this.panel.onDidDispose(() => {
      this.onDidDisposePanel();
    });

    this.panel.webview.onDidReceiveMessage(async (message: ExecutionSettingsMessage) => {
      switch (message.command) {
        case "openRawSettings":
          await vscode.commands.executeCommand("workbench.action.openSettings", "@ext:local.specforge-ai specForge");
          return;
        case "saveExecutionSettings":
          try {
            await saveExecutionSettingsAsync({
              modelProfiles: message.modelProfiles ?? [],
              agentProfiles: message.agentProfiles ?? [],
              phaseAgentAssignments: message.phaseAgentAssignments ?? {},
              defaultHarnessProfile: message.defaultHarnessProfile ?? "balanced",
              phaseHarnessProfiles: message.phaseHarnessProfiles ?? {},
              harnessProfileAuthority: message.harnessProfileAuthority ?? "workspace",
              harnessProfileLockMode: message.harnessProfileLockMode ?? "none",
              lockedHarnessPhaseIds: message.lockedHarnessPhaseIds ?? [],
              allowPerUserStoryHarnessProfileOverrides: message.allowPerUserStoryHarnessProfileOverrides ?? true,
              refinementTolerance: message.refinementTolerance ?? "balanced",
              mvpRigor: message.mvpRigor ?? "medium",
              reviewTolerance: message.reviewTolerance ?? "balanced",
              reviewEvidencePolicy: message.reviewEvidencePolicy ?? "balanced",
              technicalDesignSubagentsEnabled: message.technicalDesignSubagentsEnabled ?? false,
              reviewSubagentsEnabled: message.reviewSubagentsEnabled ?? false,
              watcherEnabled: message.watcherEnabled ?? true,
              attentionNotificationsEnabled: message.attentionNotificationsEnabled ?? true,
              contextSuggestionsEnabled: message.contextSuggestionsEnabled ?? true,
              workflowGraphLayoutMode: message.workflowGraphLayoutMode ?? "horizontal",
              workflowGraphInitialZoomMode: message.workflowGraphInitialZoomMode ?? "actual-size",
              userStoryListViewMode: message.userStoryListViewMode ?? "category",
              visualTimelineEnabled: message.visualTimelineEnabled ?? false,
              requireExplicitApprovalBranchAcceptance: message.requireExplicitApprovalBranchAcceptance ?? false,
              autoRefinementAnswersEnabled: message.autoRefinementAnswersEnabled ?? false,
              autoRefinementAnswersProfile: message.autoRefinementAnswersProfile,
              autoPlayEnabled: message.autoPlayEnabled ?? false,
              autoReviewEnabled: message.autoReviewEnabled ?? false,
              maxRefinementCycles: message.maxRefinementCycles ?? null,
              maxImplementationReviewCycles: message.maxImplementationReviewCycles ?? null,
              phaseQualityGateThresholdPercent: message.phaseQualityGateThresholdPercent ?? null,
              refinementQualityGateMaxRetries: message.refinementQualityGateMaxRetries ?? null,
              reviewQualityGateMaxRetries: message.reviewQualityGateMaxRetries ?? null,
              keepBestPhaseArtifactOnQualityRegression: message.keepBestPhaseArtifactOnQualityRegression ?? true,
              destructiveRewindEnabled: message.destructiveRewindEnabled ?? false,
              pauseOnFailedReview: message.pauseOnFailedReview ?? false,
              useSemanticGraphWhenAvailable: message.useSemanticGraphWhenAvailable ?? true,
              allowGraphBuildRefreshForTouchedUserStoryScope: message.allowGraphBuildRefreshForTouchedUserStoryScope ?? false,
              reviewLearningEnabled: message.reviewLearningEnabled ?? true,
              reviewLearningSkillPath: message.reviewLearningSkillPath,
              completedUsLockOnCompleted: message.completedUsLockOnCompleted ?? true
            });
            await this.onDidSave();
            await this.refreshAsync();
          } catch (error) {
            const messageText = error instanceof Error ? error.message : String(error);
            void vscode.window.showErrorMessage(messageText);
          }
          return;
      }
    });
  }

  public reveal(): void {
    this.panel.reveal(vscode.ViewColumn.Active);
  }

  public async refreshAsync(): Promise<void> {
    await ensureRecommendedAgentProfilesBootstrapAsync();
    const settings = getSpecForgeSettings();
    this.panel.webview.html = buildExecutionSettingsHtml({
      modelProfiles: settings.modelProfiles,
      agentProfiles: settings.agentProfiles ?? [],
      phaseAgentAssignments: settings.phaseAgentAssignments,
      defaultHarnessProfile: settings.defaultHarnessProfile,
      phaseHarnessProfiles: settings.phaseHarnessProfiles,
      harnessProfileAuthority: settings.harnessProfileAuthority,
      harnessProfileLockMode: settings.harnessProfileLockMode,
      lockedHarnessPhaseIds: settings.lockedHarnessPhaseIds,
      allowPerUserStoryHarnessProfileOverrides: settings.allowPerUserStoryHarnessProfileOverrides,
      refinementTolerance: settings.refinementTolerance,
      mvpRigor: settings.mvpRigor ?? "medium",
      reviewTolerance: settings.reviewTolerance,
      reviewEvidencePolicy: settings.reviewEvidencePolicy ?? "balanced",
      technicalDesignSubagentsEnabled: settings.technicalDesignSubagentsEnabled === true,
      reviewSubagentsEnabled: settings.reviewSubagentsEnabled === true,
      watcherEnabled: settings.watcherEnabled,
      attentionNotificationsEnabled: settings.attentionNotificationsEnabled,
      contextSuggestionsEnabled: settings.contextSuggestionsEnabled,
      workflowGraphLayoutMode: settings.workflowGraphLayoutMode,
      workflowGraphInitialZoomMode: settings.workflowGraphInitialZoomMode,
      userStoryListViewMode: settings.userStoryListViewMode ?? "category",
      visualTimelineEnabled: settings.visualTimelineEnabled,
      requireExplicitApprovalBranchAcceptance: settings.requireExplicitApprovalBranchAcceptance,
      autoRefinementAnswersEnabled: settings.autoRefinementAnswersEnabled,
      autoRefinementAnswersProfile: settings.autoRefinementAnswersProfile,
      autoPlayEnabled: settings.autoPlayEnabled,
      autoReviewEnabled: settings.autoReviewEnabled,
      maxRefinementCycles: settings.maxRefinementCycles,
      maxImplementationReviewCycles: settings.maxImplementationReviewCycles,
      phaseQualityGateThresholdPercent: settings.phaseQualityGateThresholdPercent,
      refinementQualityGateMaxRetries: settings.refinementQualityGateMaxRetries,
      reviewQualityGateMaxRetries: settings.reviewQualityGateMaxRetries,
      keepBestPhaseArtifactOnQualityRegression: settings.keepBestPhaseArtifactOnQualityRegression,
      destructiveRewindEnabled: settings.destructiveRewindEnabled,
      pauseOnFailedReview: settings.pauseOnFailedReview,
      useSemanticGraphWhenAvailable: settings.useSemanticGraphWhenAvailable,
      allowGraphBuildRefreshForTouchedUserStoryScope: settings.allowGraphBuildRefreshForTouchedUserStoryScope,
      reviewLearningEnabled: settings.reviewLearningEnabled !== false,
      reviewLearningSkillPath: settings.reviewLearningSkillPath ?? ".codex/skills/sdd-phase-agents/SKILL.md",
      completedUsLockOnCompleted: settings.completedUsLockOnCompleted,
      typographyCssVars: getEditorTypographyCssVars()
    });
  }
}

async function ensureRecommendedAgentProfilesBootstrapAsync(): Promise<void> {
  const configuration = vscode.workspace.getConfiguration("specForge");
  if (!shouldBootstrapRecommendedAgentProfiles(configuration)) {
    return;
  }

  await configuration.update(
    "execution.agentProfiles",
    recommendedBootstrapAgentProfiles,
    vscode.ConfigurationTarget.Workspace);

  if (shouldBootstrapRecommendedPhaseAgentAssignments(configuration)) {
    await configuration.update(
      "execution.phaseAgents",
      recommendedBootstrapPhaseAgentAssignments,
      vscode.ConfigurationTarget.Workspace);
  }
}

type ExecutionSettingsViewModel = {
  readonly modelProfiles: readonly SpecForgeModelProfile[];
  readonly agentProfiles: readonly SpecForgeAgentProfile[];
  readonly phaseAgentAssignments: SpecForgePhaseAgentAssignments;
  readonly defaultHarnessProfile: string;
  readonly phaseHarnessProfiles: SpecForgePhaseHarnessProfiles;
  readonly harnessProfileAuthority: "workspace" | "central";
  readonly harnessProfileLockMode: "none" | "phase" | "all";
  readonly lockedHarnessPhaseIds: readonly string[];
  readonly allowPerUserStoryHarnessProfileOverrides: boolean;
  readonly refinementTolerance: string;
  readonly mvpRigor: "low" | "medium" | "high";
  readonly reviewTolerance: string;
  readonly reviewEvidencePolicy: string;
  readonly technicalDesignSubagentsEnabled: boolean;
  readonly reviewSubagentsEnabled: boolean;
  readonly watcherEnabled: boolean;
  readonly attentionNotificationsEnabled: boolean;
  readonly contextSuggestionsEnabled: boolean;
  readonly workflowGraphLayoutMode: "horizontal" | "vertical";
  readonly workflowGraphInitialZoomMode: "actual-size" | "fit-width";
  readonly userStoryListViewMode: "category" | "phase";
  readonly visualTimelineEnabled: boolean;
  readonly requireExplicitApprovalBranchAcceptance: boolean;
  readonly autoRefinementAnswersEnabled: boolean;
  readonly autoRefinementAnswersProfile: string | null;
  readonly autoPlayEnabled: boolean;
  readonly autoReviewEnabled: boolean;
  readonly maxRefinementCycles: number | null;
  readonly maxImplementationReviewCycles: number | null;
  readonly phaseQualityGateThresholdPercent: number | null;
  readonly refinementQualityGateMaxRetries: number | null;
  readonly reviewQualityGateMaxRetries: number | null;
  readonly keepBestPhaseArtifactOnQualityRegression: boolean;
  readonly destructiveRewindEnabled: boolean;
  readonly pauseOnFailedReview: boolean;
  readonly useSemanticGraphWhenAvailable: boolean;
  readonly allowGraphBuildRefreshForTouchedUserStoryScope: boolean;
  readonly reviewLearningEnabled: boolean;
  readonly reviewLearningSkillPath: string;
  readonly completedUsLockOnCompleted: boolean;
  readonly typographyCssVars?: string;
};

const executionPhases: ReadonlyArray<{
  key: keyof SpecForgePhaseAgentAssignments;
  label: string;
  phaseId: string | null;
  kind: "default" | "phase";
}> = [
  { key: "defaultAgent", label: "Default / fallback", phaseId: null, kind: "default" },
  { key: "captureAgent", label: "Capture", phaseId: "capture", kind: "phase" },
  { key: "refinementAgent", label: "Refinement", phaseId: "refinement", kind: "phase" },
  { key: "specAgent", label: "Spec", phaseId: "spec", kind: "phase" },
  { key: "technicalDesignAgent", label: "Technical Design", phaseId: "technical-design", kind: "phase" },
  { key: "implementationAgent", label: "Implementation", phaseId: "implementation", kind: "phase" },
  { key: "reviewAgent", label: "Review", phaseId: "review", kind: "phase" },
  { key: "releaseApprovalAgent", label: "Release Approval", phaseId: "release-approval", kind: "phase" },
  { key: "prPreparationAgent", label: "PR Preparation", phaseId: "pr-preparation", kind: "phase" }
];

const harnessProfileOptions = [
  { value: "balanced", label: "Balanced" },
  { value: "strict", label: "Strict" },
  { value: "regulated", label: "Regulated" }
] as const;

function renderExecutionSettingsPhaseIcon(phase: typeof executionPhases[number]): string {
  const icon = phase.phaseId ? workflowPhaseIcon(phase.phaseId) : automationPhaseIcon();
  const toneClass = phase.kind === "default"
    ? " phase-field__icon-shell--default"
    : "";

  return `<span class="phase-field__icon-shell${toneClass}" aria-hidden="true">${icon}</span>`;
}

export function buildExecutionSettingsHtml(model: ExecutionSettingsViewModel): string {
  const permissionIssues = validatePhasePermissionAssignments(model.agentProfiles, model.phaseAgentAssignments);
  return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <style>
    :root {
      ${buildWebviewTypographyRootCss(model.typographyCssVars ?? "")}
    }
    * { box-sizing: border-box; }
    body {
      margin: 0;
      padding: 24px;
      background:
        radial-gradient(120% 90% at 12% -8%, rgba(114, 241, 184, 0.08), transparent 42%),
        radial-gradient(120% 90% at 88% 108%, rgba(92, 181, 255, 0.08), transparent 40%),
        linear-gradient(180deg, rgba(8, 14, 20, 0.985), rgba(10, 15, 21, 1));
      color: var(--vscode-editor-foreground);
      min-height: 100vh;
    }
    .shell {
      max-width: 1080px;
      margin: 0 auto;
      display: grid;
      gap: 18px;
    }
    .hero, .panel {
      border: 1px solid rgba(114, 241, 184, 0.12);
      border-radius: 22px;
      background: rgba(14, 20, 26, 0.92);
      box-shadow: 0 18px 36px rgba(0, 0, 0, 0.24);
    }
    .hero {
      padding: 24px;
      display: grid;
      gap: 10px;
    }
    .eyebrow {
      margin: 0;
      text-transform: uppercase;
      letter-spacing: 0.16em;
      font-size: 0.72rem;
      color: #72f1b8;
    }
    h1, h2, h3, p { margin: 0; }
    h1 { font-size: 2rem; line-height: 1.02; }
    h2 { font-size: 1.12rem; }
    .copy {
      color: rgba(255, 255, 255, 0.76);
      line-height: 1.55;
      max-width: 76ch;
    }
    .actions {
      display: flex;
      gap: 10px;
      flex-wrap: wrap;
    }
    .primary-action, .ghost-action, .secondary-action, .danger-action {
      border-radius: 14px;
      border: 1px solid rgba(114, 241, 184, 0.18);
      cursor: pointer;
      font: inherit;
    }
    .primary-action {
      padding: 12px 16px;
      background: linear-gradient(180deg, rgba(114, 241, 184, 0.24), rgba(16, 36, 28, 0.96));
      color: #f3fff9;
      font-weight: 700;
    }
    .ghost-action, .secondary-action {
      padding: 10px 14px;
      background: rgba(255, 255, 255, 0.04);
      color: inherit;
    }
    .danger-action {
      width: 38px;
      height: 38px;
      padding: 0;
      display: inline-grid;
      place-items: center;
      background: rgba(255, 255, 255, 0.04);
      color: #ffb0b0;
      border-color: rgba(255, 139, 139, 0.18);
    }
    .panel {
      padding: 18px;
      display: grid;
      gap: 14px;
    }
    .section-header {
      display: flex;
      justify-content: space-between;
      gap: 12px;
      align-items: flex-start;
      flex-wrap: wrap;
    }
    form, .profiles, .phase-grid, .profile-grid {
      display: grid;
      gap: 12px;
    }
    .profiles, .phase-grid {
      grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
    }
    .phase-grid {
      align-items: start;
    }
    .feature-grid {
      display: grid;
      gap: 12px;
      grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
    }
    .profile-card {
      grid-column: 1 / -1;
      padding: 14px;
      border-radius: 18px;
      background: rgba(255, 255, 255, 0.03);
      border: 1px solid rgba(255, 255, 255, 0.06);
      display: grid;
      gap: 12px;
    }
    .profile-card[open] {
      border-color: rgba(114, 241, 184, 0.22);
      box-shadow: inset 0 0 0 1px rgba(114, 241, 184, 0.08);
    }
    .profile-card__header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 10px;
    }
    .profile-card__summary {
      list-style: none;
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 12px;
      cursor: pointer;
      user-select: none;
    }
    .profile-card__summary::-webkit-details-marker {
      display: none;
    }
    .profile-card__summary-main {
      display: grid;
      gap: 3px;
      min-width: 0;
    }
    .profile-card__summary-title {
      font-weight: 700;
      color: #f3fff9;
    }
    .profile-card__summary-meta {
      font-size: 0.8rem;
      color: rgba(255, 255, 255, 0.62);
    }
    .profile-card__summary-actions {
      display: flex;
      align-items: center;
      gap: 8px;
      flex-shrink: 0;
    }
    .profile-card__chevron {
      width: 34px;
      height: 34px;
      display: inline-grid;
      place-items: center;
      border-radius: 12px;
      border: 1px solid rgba(255, 255, 255, 0.08);
      background: rgba(255, 255, 255, 0.04);
      color: rgba(255, 255, 255, 0.76);
      transition: transform 120ms ease;
    }
    .profile-card[open] .profile-card__chevron {
      transform: rotate(180deg);
    }
    .profile-grid {
      grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
    }
    label {
      display: grid;
      gap: 6px;
    }
    label span {
      font-size: 0.82rem;
      color: rgba(255, 255, 255, 0.78);
    }
    input, select {
      width: 100%;
      border-radius: 12px;
      border: 1px solid rgba(255, 255, 255, 0.08);
      background: rgba(255, 255, 255, 0.04);
      color: inherit;
      padding: 10px 12px;
      font: inherit;
    }
    .hidden-field {
      display: none;
    }
    .phase-field {
      padding: 12px;
      border-radius: 16px;
      background: rgba(255, 255, 255, 0.03);
      border: 1px solid rgba(255, 255, 255, 0.06);
    }
    .phase-grid .phase-field {
      display: grid;
      gap: 10px;
      align-content: start;
      min-height: 100%;
    }
    .phase-field__heading {
      display: flex;
      align-items: center;
      gap: 10px;
      min-width: 0;
    }
    .phase-field__icon-shell {
      width: 34px;
      height: 34px;
      flex: 0 0 34px;
      display: inline-grid;
      place-items: center;
      border-radius: 12px;
      border: 1px solid rgba(114, 241, 184, 0.18);
      background:
        radial-gradient(circle at 30% 28%, rgba(255, 255, 255, 0.08), transparent 38%),
        linear-gradient(180deg, rgba(18, 34, 31, 0.94), rgba(11, 21, 20, 0.98));
      color: rgba(177, 255, 224, 0.92);
      box-shadow:
        inset 0 1px 0 rgba(255, 255, 255, 0.05),
        0 8px 22px rgba(4, 10, 18, 0.18);
    }
    .phase-field__icon-shell svg {
      width: 18px;
      height: 18px;
      fill: currentColor;
    }
    .phase-field__icon-shell--default {
      border-color: rgba(190, 198, 214, 0.16);
      background:
        radial-gradient(circle at 30% 28%, rgba(255, 255, 255, 0.06), transparent 38%),
        linear-gradient(180deg, rgba(34, 38, 45, 0.94), rgba(19, 23, 29, 0.98));
      color: rgba(193, 201, 214, 0.82);
    }
    .phase-field__title-stack {
      display: grid;
      gap: 3px;
      min-width: 0;
    }
    .phase-field__title {
      font-size: 0.82rem;
      color: rgba(255, 255, 255, 0.9);
      font-weight: 700;
    }
    .phase-field__inline-hint {
      font-size: 0.74rem;
      color: rgba(255, 255, 255, 0.52);
      line-height: 1.35;
    }
    .phase-field--default-route {
      grid-column: 1 / -1;
      grid-template-columns: minmax(0, 1fr) minmax(260px, 320px);
      align-items: center;
      gap: 16px;
    }
    .phase-field--default-route .phase-field__hint {
      grid-column: 1 / -1;
    }
    .phase-field--invalid {
      border-color: rgba(255, 139, 139, 0.42);
      background: rgba(88, 28, 28, 0.22);
      box-shadow: inset 0 0 0 1px rgba(255, 139, 139, 0.14);
    }
    .phase-field__hint {
      font-size: 0.76rem;
      color: rgba(255, 176, 176, 0.9);
      line-height: 1.4;
      display: none;
    }
    .phase-field--invalid .phase-field__hint {
      display: block;
    }
    .switch-field {
      grid-template-columns: minmax(0, 1fr) auto;
      align-items: center;
      gap: 12px;
    }
    .switch-field__copy {
      display: grid;
      gap: 4px;
      min-width: 0;
    }
    .switch-control {
      width: 54px;
      height: 30px;
      border: 1px solid rgba(255, 255, 255, 0.12);
      border-radius: 999px;
      background: rgba(255, 255, 255, 0.08);
      padding: 3px;
      cursor: pointer;
      transition: background 120ms ease, border-color 120ms ease;
    }
    .switch-control::before {
      content: "";
      display: block;
      width: 22px;
      height: 22px;
      border-radius: 999px;
      background: rgba(255, 255, 255, 0.84);
      box-shadow: 0 2px 8px rgba(0, 0, 0, 0.28);
      transition: transform 120ms ease, background 120ms ease;
    }
    .switch-control[aria-checked="true"] {
      border-color: rgba(114, 241, 184, 0.46);
      background: rgba(36, 116, 82, 0.92);
    }
    .switch-control[aria-checked="true"]::before {
      transform: translateX(24px);
      background: #f3fff9;
    }
    .warning-banner {
      display: none;
      gap: 8px;
      padding: 14px 16px;
      border-radius: 16px;
      border: 1px solid rgba(255, 139, 139, 0.32);
      background: linear-gradient(180deg, rgba(70, 20, 20, 0.96), rgba(42, 16, 16, 0.98));
      color: #ffd9d9;
    }
    .warning-banner--visible {
      display: grid;
    }
    .save-error {
      font-size: 0.76rem;
      color: rgba(255, 176, 176, 0.92);
      line-height: 1.45;
      display: none;
    }
    .save-error--visible {
      display: block;
    }
    .empty {
      padding: 14px;
      border-radius: 16px;
      background: rgba(255, 255, 255, 0.03);
      border: 1px dashed rgba(255, 255, 255, 0.08);
      color: rgba(255, 255, 255, 0.7);
    }
    code {
      font-family: var(--specforge-mono-font-family);
    }
    @media (max-width: 720px) {
      body { padding: 16px; }
      .hero, .panel { padding: 16px; }
      .phase-field--default-route {
        grid-template-columns: minmax(0, 1fr);
      }
    }
  </style>
</head>
<body>
  <div class="shell">
    <section class="hero">
      <p class="eyebrow">SpecForge Configuration</p>
      <h1>One panel, one source of truth</h1>
      <p class="copy">Keep SpecForge settings together here instead of scattering workflow behavior across raw VS Code settings. Shared workflow rules persist in workspace settings, while personal UX preferences persist in VS Code user settings under <code>specForge.*</code>.</p>
      <div class="actions">
        <button class="ghost-action" type="button" data-command="openRawSettings">Open Raw VS Code Settings</button>
      </div>
    </section>
    <form id="execution-settings-form" class="panel">
      <div class="section-header">
        <div>
          <p class="eyebrow">Provider Profiles</p>
          <h2>Execution catalog</h2>
        </div>
        <button class="secondary-action" type="button" data-add-profile>Add Profile</button>
      </div>
      <div class="profiles" data-profiles></div>
      <div class="section-header">
        <div>
          <p class="eyebrow">Agent Profiles</p>
          <h2>Roles, instructions, and permissions</h2>
          <p class="copy">Agents define the operational role SpecForge injects into prompts, the repository permissions enforced for phases, and which model profile powers the agent.</p>
        </div>
        <button class="secondary-action" type="button" data-add-agent>Add Agent</button>
      </div>
      <div class="profiles" data-agents></div>
      <div class="section-header">
        <div>
          <p class="eyebrow">Phase Routing</p>
          <h2>Per-phase selection</h2>
        </div>
      </div>
      <div class="warning-banner" data-default-warning>
        <strong>Default / fallback missing</strong>
        <span>With multiple agents, define a fallback agent or keep a single-agent setup.</span>
      </div>
      <div class="phase-grid" data-phase-grid>
        ${executionPhases.map((phase) => `
          <label class="phase-field${phase.kind === "default" ? " phase-field--default-route" : ""}" data-phase-wrapper="${escapeHtmlAttr(String(phase.key))}">
            <span class="phase-field__heading">
              ${renderExecutionSettingsPhaseIcon(phase)}
              <span class="phase-field__title-stack">
                <span class="phase-field__title">${escapeHtml(phase.label)}</span>
                ${phase.key === "defaultAgent"
                  ? '<span class="phase-field__inline-hint">Required when you have multiple agents and no single implicit fallback.</span>'
                  : ""}
              </span>
            </span>
            <select data-phase-field="${escapeHtmlAttr(String(phase.key))}"></select>
            ${phase.key === "defaultAgent"
              ? '<span class="phase-field__hint">Required when you have multiple agents and no single implicit fallback.</span>'
              : '<span class="phase-field__hint"></span>'}
          </label>
        `).join("")}
      </div>
      <div class="section-header">
        <div>
          <p class="eyebrow">Harness Profiles</p>
          <h2>Governance posture by phase</h2>
          <p class="copy">Assign reusable harness postures per phase, decide who owns those assignments, and declare whether profiles may be overridden later by a user story or are governance-locked.</p>
        </div>
      </div>
      <div class="feature-grid">
        <label class="phase-field">
          <span>Default harness profile</span>
          <select data-default-harness-profile>
            ${harnessProfileOptions.map((option) => `<option value="${option.value}"${model.defaultHarnessProfile === option.value ? " selected" : ""}>${option.label}</option>`).join("")}
          </select>
          <span class="phase-field__hint">Fallback posture when a phase does not declare a specific harness profile.</span>
        </label>
        <label class="phase-field">
          <span>Assignment authority</span>
          <select data-harness-profile-authority>
            <option value="workspace"${model.harnessProfileAuthority === "workspace" ? " selected" : ""}>Workspace</option>
            <option value="central"${model.harnessProfileAuthority === "central" ? " selected" : ""}>SpecForge Central</option>
          </select>
          <span class="phase-field__hint">Declares whether this repository currently treats harness profile assignments as local or centrally-governed.</span>
        </label>
        <label class="phase-field">
          <span>Lock mode</span>
          <select data-harness-profile-lock-mode>
            <option value="none"${model.harnessProfileLockMode === "none" ? " selected" : ""}>None</option>
            <option value="phase"${model.harnessProfileLockMode === "phase" ? " selected" : ""}>Selected phases</option>
            <option value="all"${model.harnessProfileLockMode === "all" ? " selected" : ""}>All phases</option>
          </select>
          <span class="phase-field__hint">Controls whether assignments are advisory, phase-locked, or globally locked against later override.</span>
        </label>
        <label class="phase-field">
          <span>Allow per-user-story overrides</span>
          <select data-allow-per-us-harness-profile-overrides>
            <option value="true"${model.allowPerUserStoryHarnessProfileOverrides ? " selected" : ""}>Enabled</option>
            <option value="false"${model.allowPerUserStoryHarnessProfileOverrides ? "" : " selected"}>Disabled</option>
          </select>
          <span class="phase-field__hint">Allows a workflow or Central to request a per-user-story profile change when the resolved phase is not locked.</span>
        </label>
      </div>
      <div class="phase-grid" data-harness-phase-grid>
        ${executionPhases
          .filter((phase) => phase.key !== "defaultAgent")
          .map((phase) => `
            <label class="phase-field" data-harness-phase-wrapper="${escapeHtmlAttr(phase.phaseId ?? "")}">
              <span class="phase-field__heading">
                ${renderExecutionSettingsPhaseIcon(phase)}
                <span class="phase-field__title-stack">
                  <span class="phase-field__title">${escapeHtml(phase.label)}</span>
                </span>
              </span>
              <select data-harness-phase-field="${escapeHtmlAttr(phase.phaseId ?? "")}"></select>
              <span class="phase-field__hint"></span>
            </label>
          `).join("")}
      </div>
      <div class="feature-grid">
        ${executionPhases
          .filter((phase) => phase.key !== "defaultAgent")
          .map((phase) => `
            <label class="phase-field">
              <span>Lock ${escapeHtml(phase.label)}</span>
              <select data-harness-lock-phase="${escapeHtmlAttr(phase.phaseId ?? "")}">
                <option value="false"${model.lockedHarnessPhaseIds.includes(phase.phaseId ?? "") ? "" : " selected"}>No</option>
                <option value="true"${model.lockedHarnessPhaseIds.includes(phase.phaseId ?? "") ? " selected" : ""}>Yes</option>
              </select>
            </label>
          `).join("")}
      </div>
      <div class="section-header">
        <div>
          <p class="eyebrow">Refinement Automation</p>
          <h2>Model-assisted answers</h2>
          <p class="copy">When refinement blocks spec, let a selected model try to answer the pending questions once before handing the phase back to the user.</p>
        </div>
      </div>
      <div class="feature-grid">
        <label class="phase-field">
          <span>Refinement tolerance</span>
          <select data-refinement-tolerance>
            <option value="strict"${model.refinementTolerance === "strict" ? " selected" : ""}>Strict</option>
            <option value="balanced"${model.refinementTolerance === "balanced" ? " selected" : ""}>Balanced</option>
            <option value="inferential"${model.refinementTolerance === "inferential" ? " selected" : ""}>Inferential</option>
          </select>
          <span class="phase-field__hint">Controls how much ambiguity refinement tolerates before spec can continue.</span>
        </label>
        <label class="phase-field">
          <span>MVP rigor</span>
          <select data-mvp-rigor>
            <option value="low"${model.mvpRigor === "low" ? " selected" : ""}>Low</option>
            <option value="medium"${model.mvpRigor === "medium" ? " selected" : ""}>Medium</option>
            <option value="high"${model.mvpRigor === "high" ? " selected" : ""}>High</option>
          </select>
          <span class="phase-field__hint">Controls how much product detail refinement requires before a story can become a buildable MVP slice.</span>
        </label>
        <label class="phase-field">
          <span>Review tolerance</span>
          <select data-review-tolerance>
            <option value="strict"${model.reviewTolerance === "strict" ? " selected" : ""}>Strict</option>
            <option value="balanced"${model.reviewTolerance === "balanced" ? " selected" : ""}>Balanced</option>
            <option value="inferential"${model.reviewTolerance === "inferential" ? " selected" : ""}>Inferential</option>
          </select>
          <span class="phase-field__hint">Controls how demanding the review phase is before it passes or fails delivered work.</span>
        </label>
        <label class="phase-field">
          <span>Review evidence policy</span>
          <select data-review-evidence-policy>
            <option value="strict"${model.reviewEvidencePolicy === "strict" ? " selected" : ""}>Strict</option>
            <option value="balanced"${model.reviewEvidencePolicy === "balanced" ? " selected" : ""}>Balanced</option>
            <option value="release"${model.reviewEvidencePolicy === "release" ? " selected" : ""}>Release</option>
            <option value="advisory"${model.reviewEvidencePolicy === "advisory" ? " selected" : ""}>Advisory</option>
          </select>
          <span class="phase-field__hint">Controls how missing validation evidence affects review pass/fail readiness.</span>
        </label>
        <label class="phase-field switch-field">
          <span class="switch-field__copy">
            <span>Technical design subagents</span>
            <span class="phase-field__inline-hint">Run specialist design scouts before the final technical design artifact is synthesized.</span>
          </span>
          <button class="switch-control" type="button" role="switch" aria-checked="${model.technicalDesignSubagentsEnabled ? "true" : "false"}" data-technical-design-subagents></button>
        </label>
        <label class="phase-field switch-field">
          <span class="switch-field__copy">
            <span>Review subagents</span>
            <span class="phase-field__inline-hint">Run specialist review auditors before the final review verdict is synthesized.</span>
          </span>
          <button class="switch-control" type="button" role="switch" aria-checked="${model.reviewSubagentsEnabled ? "true" : "false"}" data-review-subagents></button>
        </label>
        <label class="phase-field">
          <span>Enable auto answers</span>
          <select data-auto-refinement-enabled>
            <option value="false"${model.autoRefinementAnswersEnabled ? "" : " selected"}>Disabled</option>
            <option value="true"${model.autoRefinementAnswersEnabled ? " selected" : ""}>Enabled</option>
          </select>
        </label>
        <label class="phase-field" data-auto-refinement-profile-wrapper>
          <span>Auto-answer profile</span>
          <select data-auto-refinement-profile></select>
        </label>
        <label class="phase-field">
          <span>Context suggestions</span>
          <select data-context-suggestions-enabled>
            <option value="true"${model.contextSuggestionsEnabled ? " selected" : ""}>Enabled</option>
            <option value="false"${model.contextSuggestionsEnabled ? "" : " selected"}>Disabled</option>
          </select>
          <span class="phase-field__hint">Suggest nearby repository files during refinement to improve local context selection.</span>
        </label>
        <label class="phase-field">
          <span>Require approval branch acceptance</span>
          <select data-require-approval-branch-acceptance>
            <option value="false"${model.requireExplicitApprovalBranchAcceptance ? "" : " selected"}>Disabled</option>
            <option value="true"${model.requireExplicitApprovalBranchAcceptance ? " selected" : ""}>Enabled</option>
          </select>
          <span class="phase-field__hint">Force explicit confirmation of the proposed base branch before approving spec.</span>
        </label>
        <label class="phase-field">
          <span>Use semantic graph when available</span>
          <select data-use-semantic-graph-when-available>
            <option value="true"${model.useSemanticGraphWhenAvailable ? " selected" : ""}>Enabled</option>
            <option value="false"${model.useSemanticGraphWhenAvailable ? "" : " selected"}>Disabled</option>
          </select>
          <span class="phase-field__hint">Reuse a valid semantic code graph for later phases when graph artifacts already exist.</span>
        </label>
        <label class="phase-field">
          <span>Allow graph build or refresh for touched US scope</span>
          <select data-allow-graph-build-refresh-for-touched-us-scope>
            <option value="false"${model.allowGraphBuildRefreshForTouchedUserStoryScope ? "" : " selected"}>Disabled</option>
            <option value="true"${model.allowGraphBuildRefreshForTouchedUserStoryScope ? " selected" : ""}>Enabled</option>
          </select>
          <span class="phase-field__hint">Allow runtime or operator flows to materialize or refresh graph state for the touched user story when needed.</span>
        </label>
      </div>
      <div class="section-header">
        <div>
          <p class="eyebrow">Automation</p>
          <h2>Playback and review loop</h2>
          <p class="copy">Control when SpecForge resumes automatically after manual checkpoints and how far the refinement and implementation/review loops are allowed to run without intervention.</p>
        </div>
      </div>
      <div class="feature-grid">
        <label class="phase-field">
          <span>Enable auto play</span>
          <select data-auto-play-enabled>
            <option value="false"${model.autoPlayEnabled ? "" : " selected"}>Disabled</option>
            <option value="true"${model.autoPlayEnabled ? " selected" : ""}>Enabled</option>
          </select>
          <span class="phase-field__hint">Resume workflow playback automatically after qualifying manual actions such as approvals.</span>
        </label>
        <label class="phase-field">
          <span>Enable auto review</span>
          <select data-auto-review-enabled>
            <option value="false"${model.autoReviewEnabled ? "" : " selected"}>Disabled</option>
            <option value="true"${model.autoReviewEnabled ? " selected" : ""}>Enabled</option>
          </select>
        </label>
        <label class="phase-field">
          <span>Max refinement cycles</span>
          <input type="number" min="1" step="1" data-max-refinement-cycles value="${escapeHtmlAttr(String(model.maxRefinementCycles ?? 5))}" />
          <span class="phase-field__hint">Automatic refinement stops when this many refinement artifacts have been produced without human intervention.</span>
        </label>
        <label class="phase-field">
          <span>Max implementation/review cycles</span>
          <input type="number" min="1" step="1" data-max-implementation-review-cycles value="${escapeHtmlAttr(String(model.maxImplementationReviewCycles ?? 5))}" />
          <span class="phase-field__hint">Automatic review stops when this many implementation attempts have been recorded.</span>
        </label>
        <label class="phase-field">
          <span>Phase quality threshold (%)</span>
          <input type="number" min="0" max="100" step="1" data-phase-quality-gate-threshold-percent value="${escapeHtmlAttr(String(model.phaseQualityGateThresholdPercent ?? 85))}" />
          <span class="phase-field__hint">Minimum quality score required for refinement and review to pass the orchestration quality gate.</span>
        </label>
        <label class="phase-field">
          <span>Refinement quality max retries</span>
          <input type="number" min="1" step="1" data-refinement-quality-gate-max-retries value="${escapeHtmlAttr(String(model.refinementQualityGateMaxRetries ?? 5))}" />
          <span class="phase-field__hint">Maximum low-quality refinement retries before automatic progression stops and waits for the user.</span>
        </label>
        <label class="phase-field">
          <span>Review quality max retries</span>
          <input type="number" min="1" step="1" data-review-quality-gate-max-retries value="${escapeHtmlAttr(String(model.reviewQualityGateMaxRetries ?? 3))}" />
          <span class="phase-field__hint">Maximum low-quality review retries before automatic progression stops and waits for the user.</span>
        </label>
        <label class="phase-field">
          <span>Keep previous best artifact</span>
          <select data-keep-best-phase-artifact-on-quality-regression>
            <option value="true"${model.keepBestPhaseArtifactOnQualityRegression ? " selected" : ""}>Enabled</option>
            <option value="false"${model.keepBestPhaseArtifactOnQualityRegression ? "" : " selected"}>Disabled</option>
          </select>
          <span class="phase-field__hint">When quality drops below threshold, keep the best comparable previous artifact selected instead of the latest one when it is safer to do so.</span>
        </label>
        <label class="phase-field">
          <span>Pause on failed review</span>
          <select data-pause-on-failed-review>
            <option value="false"${model.pauseOnFailedReview ? "" : " selected"}>Disabled</option>
            <option value="true"${model.pauseOnFailedReview ? " selected" : ""}>Enabled</option>
          </select>
          <span class="phase-field__hint">Pause playback automatically when review fails so the developer can inspect before continuing.</span>
        </label>
        <label class="phase-field">
          <span>Review learning</span>
          <select data-review-learning-enabled>
            <option value="true"${model.reviewLearningEnabled ? " selected" : ""}>Enabled</option>
            <option value="false"${model.reviewLearningEnabled ? "" : " selected"}>Disabled</option>
          </select>
          <span class="phase-field__hint">Persist generalized implementation lessons after failed reviews.</span>
        </label>
        <label class="phase-field">
          <span>Review learning skill path</span>
          <input type="text" data-review-learning-skill-path value="${escapeHtmlAttr(model.reviewLearningSkillPath)}" />
          <span class="phase-field__hint">Workspace-relative skill file used for review-learning guardrails.</span>
        </label>
      </div>
      <div class="section-header">
        <div>
          <p class="eyebrow">Workflow Safety</p>
          <h2>Rewind and completion policy</h2>
          <p class="copy">Define how aggressive rewinds are, and whether completed user stories stay locked until they are explicitly reopened from the completed phase.</p>
        </div>
      </div>
      <div class="feature-grid">
        <label class="phase-field">
          <span>Destructive rewind</span>
          <select data-destructive-rewind-enabled>
            <option value="false"${model.destructiveRewindEnabled ? "" : " selected"}>Disabled</option>
            <option value="true"${model.destructiveRewindEnabled ? " selected" : ""}>Enabled</option>
          </select>
          <span class="phase-field__hint">When enabled, rewinds and regressions delete later derived artifacts instead of only moving workflow state.</span>
        </label>
        <label class="phase-field">
          <span>Lock completed workflows</span>
          <select data-completed-us-lock-on-completed>
            <option value="true"${model.completedUsLockOnCompleted ? " selected" : ""}>Enabled</option>
            <option value="false"${model.completedUsLockOnCompleted ? "" : " selected"}>Disabled</option>
          </select>
          <span class="phase-field__hint">Disable this if completed workflows should remain directly mutable instead of requiring explicit reopen.</span>
        </label>
      </div>
      <div class="section-header">
        <div>
          <p class="eyebrow">User Preferences</p>
          <h2>Personal workflow UX</h2>
          <p class="copy">These preferences are saved to your VS Code user settings. They affect your local experience without changing the team's shared workspace configuration.</p>
        </div>
      </div>
      <div class="feature-grid">
        <label class="phase-field">
          <span>Workflow graph layout</span>
          <select data-workflow-graph-layout-mode>
            <option value="vertical"${model.workflowGraphLayoutMode === "vertical" ? " selected" : ""}>Vertical</option>
            <option value="horizontal"${model.workflowGraphLayoutMode === "horizontal" ? " selected" : ""}>Horizontal</option>
          </select>
          <span class="phase-field__hint">Fallback graph orientation used when a user story does not have its own saved layout override.</span>
        </label>
        <label class="phase-field">
          <span>Workflow graph initial zoom</span>
          <select data-workflow-graph-initial-zoom-mode>
            <option value="actual-size"${model.workflowGraphInitialZoomMode === "fit-width" ? "" : " selected"}>100%</option>
            <option value="fit-width"${model.workflowGraphInitialZoomMode === "fit-width" ? " selected" : ""}>Fit to width</option>
          </select>
          <span class="phase-field__hint">Default zoom mode used when opening a workflow graph.</span>
        </label>
        <label class="phase-field">
          <span>User story list view</span>
          <select data-user-story-list-view-mode>
            <option value="category"${model.userStoryListViewMode === "phase" ? "" : " selected"}>Category</option>
            <option value="phase"${model.userStoryListViewMode === "phase" ? " selected" : ""}>Phase</option>
          </select>
          <span class="phase-field__hint">Group user stories by category or current workflow phase in the sidebar.</span>
        </label>
        <label class="phase-field">
          <span>Visual timeline</span>
          <select data-visual-timeline-enabled>
            <option value="false"${model.visualTimelineEnabled ? "" : " selected"}>Hidden</option>
            <option value="true"${model.visualTimelineEnabled ? " selected" : ""}>Visible</option>
          </select>
          <span class="phase-field__hint">Show or hide the visual workflow timeline dock in the workflow detail view.</span>
        </label>
        <label class="phase-field">
          <span>Workspace watcher</span>
          <select data-watcher-enabled>
            <option value="true"${model.watcherEnabled ? " selected" : ""}>Enabled</option>
            <option value="false"${model.watcherEnabled ? "" : " selected"}>Disabled</option>
          </select>
          <span class="phase-field__hint">Refresh the explorer and workflow views automatically when <code>.specs</code> files change on disk.</span>
        </label>
        <label class="phase-field">
          <span>Attention notifications</span>
          <select data-attention-notifications-enabled>
            <option value="true"${model.attentionNotificationsEnabled ? " selected" : ""}>Enabled</option>
            <option value="false"${model.attentionNotificationsEnabled ? "" : " selected"}>Disabled</option>
          </select>
          <span class="phase-field__hint">Show notifications when a user story becomes waiting-user, blocked, or completed.</span>
        </label>
      </div>
      <div class="actions">
        <button class="primary-action" type="submit">Save SpecForge Configuration</button>
      </div>
      <p class="save-error" data-save-error></p>
    </form>
  </div>
  <script>
    const vscode = acquireVsCodeApi();
    const executionPhases = ${JSON.stringify(executionPhases)};
    const permissionRequirements = ${JSON.stringify([
      { assignmentKey: "refinementAgent", label: "Refinement", requiredRepositoryAccess: "read" },
      { assignmentKey: "specAgent", label: "Spec", requiredRepositoryAccess: "read" },
      { assignmentKey: "technicalDesignAgent", label: "Technical Design", requiredRepositoryAccess: "read" },
      { assignmentKey: "implementationAgent", label: "Implementation", requiredRepositoryAccess: "read-write" },
      { assignmentKey: "reviewAgent", label: "Review", requiredRepositoryAccess: "read" },
      { assignmentKey: "releaseApprovalAgent", label: "Release Approval", requiredRepositoryAccess: "read" },
      { assignmentKey: "prPreparationAgent", label: "PR Preparation", requiredRepositoryAccess: "read" }
    ])};
    let state = {
      modelProfiles: ${JSON.stringify(model.modelProfiles)},
      agentProfiles: ${JSON.stringify(model.agentProfiles)},
      phaseAgentAssignments: ${JSON.stringify(model.phaseAgentAssignments)},
      defaultHarnessProfile: ${JSON.stringify(model.defaultHarnessProfile)},
      phaseHarnessProfiles: ${JSON.stringify(model.phaseHarnessProfiles)},
      harnessProfileAuthority: ${JSON.stringify(model.harnessProfileAuthority)},
      harnessProfileLockMode: ${JSON.stringify(model.harnessProfileLockMode)},
      lockedHarnessPhaseIds: ${JSON.stringify(model.lockedHarnessPhaseIds)},
      allowPerUserStoryHarnessProfileOverrides: ${JSON.stringify(model.allowPerUserStoryHarnessProfileOverrides)},
      refinementTolerance: ${JSON.stringify(model.refinementTolerance)},
      mvpRigor: ${JSON.stringify(model.mvpRigor)},
      reviewTolerance: ${JSON.stringify(model.reviewTolerance)},
      reviewEvidencePolicy: ${JSON.stringify(model.reviewEvidencePolicy)},
      technicalDesignSubagentsEnabled: ${JSON.stringify(model.technicalDesignSubagentsEnabled)},
      reviewSubagentsEnabled: ${JSON.stringify(model.reviewSubagentsEnabled)},
      watcherEnabled: ${JSON.stringify(model.watcherEnabled)},
      attentionNotificationsEnabled: ${JSON.stringify(model.attentionNotificationsEnabled)},
      contextSuggestionsEnabled: ${JSON.stringify(model.contextSuggestionsEnabled)},
      workflowGraphLayoutMode: ${JSON.stringify(model.workflowGraphLayoutMode)},
      workflowGraphInitialZoomMode: ${JSON.stringify(model.workflowGraphInitialZoomMode)},
      userStoryListViewMode: ${JSON.stringify(model.userStoryListViewMode)},
      visualTimelineEnabled: ${JSON.stringify(model.visualTimelineEnabled)},
      requireExplicitApprovalBranchAcceptance: ${JSON.stringify(model.requireExplicitApprovalBranchAcceptance)},
      autoRefinementAnswersEnabled: ${JSON.stringify(model.autoRefinementAnswersEnabled)},
      autoRefinementAnswersProfile: ${JSON.stringify(model.autoRefinementAnswersProfile)},
      autoPlayEnabled: ${JSON.stringify(model.autoPlayEnabled)},
      autoReviewEnabled: ${JSON.stringify(model.autoReviewEnabled)},
      maxRefinementCycles: ${JSON.stringify(model.maxRefinementCycles ?? 5)},
      maxImplementationReviewCycles: ${JSON.stringify(model.maxImplementationReviewCycles ?? 5)},
      phaseQualityGateThresholdPercent: ${JSON.stringify(model.phaseQualityGateThresholdPercent ?? 85)},
      refinementQualityGateMaxRetries: ${JSON.stringify(model.refinementQualityGateMaxRetries ?? 5)},
      reviewQualityGateMaxRetries: ${JSON.stringify(model.reviewQualityGateMaxRetries ?? 3)},
      keepBestPhaseArtifactOnQualityRegression: ${JSON.stringify(model.keepBestPhaseArtifactOnQualityRegression)},
      destructiveRewindEnabled: ${JSON.stringify(model.destructiveRewindEnabled)},
      pauseOnFailedReview: ${JSON.stringify(model.pauseOnFailedReview)},
      useSemanticGraphWhenAvailable: ${JSON.stringify(model.useSemanticGraphWhenAvailable)},
      allowGraphBuildRefreshForTouchedUserStoryScope: ${JSON.stringify(model.allowGraphBuildRefreshForTouchedUserStoryScope)},
      reviewLearningEnabled: ${JSON.stringify(model.reviewLearningEnabled)},
      reviewLearningSkillPath: ${JSON.stringify(model.reviewLearningSkillPath)},
      completedUsLockOnCompleted: ${JSON.stringify(model.completedUsLockOnCompleted)},
      initialPermissionIssues: ${JSON.stringify(permissionIssues)},
      expandedProfileIndexes: ${JSON.stringify(model.modelProfiles.map((_, index) => index === 0))},
      expandedAgentIndexes: ${JSON.stringify(model.agentProfiles.map((_, index) => index === 0))},
      pendingFocusProfileIndex: null,
      pendingFocusAgentIndex: null
    };

    function escapeHtml(value) {
      return String(value)
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;")
        .replaceAll("'", "&#39;");
    }

    function providerOptions(selectedProvider) {
      return ["codex", "copilot", "claude", "openai-compatible"]
        .map((provider) => '<option value="' + provider + '"' + (provider === selectedProvider ? " selected" : "") + '>' + escapeHtml(provider) + '</option>')
        .join("");
    }

    function isNativeCliProvider(provider) {
      return provider === "codex" || provider === "copilot" || provider === "claude";
    }

    function repositoryAccessOptions(selectedValue) {
      return ["none", "read", "read-write"]
        .map((value) => '<option value="' + value + '"' + (value === selectedValue ? " selected" : "") + '>' + escapeHtml(value) + '</option>')
        .join("");
    }

    function reasoningEffortOptions(selectedValue) {
      return [
        ["", "Provider default"],
        ["none", "none"],
        ["minimal", "minimal"],
        ["low", "low"],
        ["medium", "medium"],
        ["high", "high"],
        ["xhigh", "xhigh"]
      ]
        .map(([value, label]) => '<option value="' + value + '"' + (value === selectedValue ? " selected" : "") + '>' + escapeHtml(label) + '</option>')
        .join("");
    }

    function phaseOptions(selectedValue) {
      const options = ['<option value="">Use default</option>'];
      for (const agent of state.agentProfiles) {
        options.push('<option value="' + escapeHtml(agent.name || "") + '"' + ((agent.name || "") === selectedValue ? " selected" : "") + '>' + escapeHtml(agent.name || "") + '</option>');
      }
      return options.join("");
    }

    function harnessProfileSelectOptions(selectedValue) {
      return ['<option value="">Use default</option>']
        .concat([
          ['balanced', 'Balanced'],
          ['strict', 'Strict'],
          ['regulated', 'Regulated']
        ].map(([value, label]) => '<option value="' + value + '"' + (value === selectedValue ? " selected" : "") + '>' + escapeHtml(label) + '</option>'))
        .join("");
    }

    function autoRefinementProfileOptions(selectedValue) {
      const options = ['<option value="">Select an agent</option>'];
      for (const agent of state.agentProfiles) {
        options.push('<option value="' + escapeHtml(agent.name || "") + '"' + ((agent.name || "") === selectedValue ? " selected" : "") + '>' + escapeHtml(agent.name || "") + '</option>');
      }
      return options.join("");
    }

    function phaseIdToHarnessAssignmentKey(phaseId) {
      switch (phaseId) {
        case "capture":
          return "captureProfile";
        case "refinement":
          return "refinementProfile";
        case "spec":
          return "specProfile";
        case "technical-design":
          return "technicalDesignProfile";
        case "implementation":
          return "implementationProfile";
        case "review":
          return "reviewProfile";
        case "release-approval":
          return "releaseApprovalProfile";
        case "pr-preparation":
          return "prPreparationProfile";
        default:
          return "defaultProfile";
      }
    }

    function modelProfileOptions(selectedValue) {
      const options = ['<option value="">Select a model profile</option>'];
      for (const profile of state.modelProfiles) {
        options.push('<option value="' + escapeHtml(profile.name || "") + '"' + ((profile.name || "") === selectedValue ? " selected" : "") + '>' + escapeHtml(profile.name || "") + '</option>');
      }
      return options.join("");
    }

    function hasFallbackProblem() {
      const nonEmptyAgents = state.agentProfiles.filter((agent) => String(agent.name || "").trim().length > 0);
      return nonEmptyAgents.length > 1 && !String(state.phaseAgentAssignments.defaultAgent || "").trim();
    }

    function hasAutoRefinementProblem() {
      return state.autoRefinementAnswersEnabled && !String(state.autoRefinementAnswersProfile || "").trim();
    }

    function validatePermissionIssues() {
      const profilesByName = new Map(
        state.agentProfiles
          .map((agent) => ({
            name: String(agent.name || "").trim(),
            repositoryAccess: String(agent.repositoryAccess || "none").trim() || "none"
          }))
          .filter((agent) => agent.name.length > 0)
          .map((agent) => [agent.name, agent])
      );
      const implicitDefaultAgent = state.agentProfiles.length === 1
        ? String(state.agentProfiles[0]?.name || "").trim() || null
        : null;
      const defaultAgent = String(state.phaseAgentAssignments.defaultAgent || "").trim() || implicitDefaultAgent;
      const issues = [];

      for (const requirement of permissionRequirements) {
        const assignedProfile = String(state.phaseAgentAssignments[requirement.assignmentKey] || "").trim() || defaultAgent;
        if (!assignedProfile) {
          continue;
        }

        const profile = profilesByName.get(assignedProfile);
        if (!profile) {
          continue;
        }

        const actual = profile.repositoryAccess || "none";
        const okay = requirement.requiredRepositoryAccess === "read"
          ? actual === "read" || actual === "read-write"
          : actual === "read-write";
        if (okay) {
          continue;
        }

        issues.push({
          assignmentKey: requirement.assignmentKey,
          label: requirement.label,
            message: requirement.label + " requires repository access '" + requirement.requiredRepositoryAccess + "', but agent '" + assignedProfile + "' only grants '" + actual + "'."
        });
      }

      return issues;
    }

    function render() {
      const profilesHost = document.querySelector("[data-profiles]");
      const agentsHost = document.querySelector("[data-agents]");
      const phaseGrid = document.querySelector("[data-phase-grid]");
      const harnessPhaseGrid = document.querySelector("[data-harness-phase-grid]");
      const warning = document.querySelector("[data-default-warning]");
      const defaultHarnessProfile = document.querySelector("[data-default-harness-profile]");
      const harnessProfileAuthority = document.querySelector("[data-harness-profile-authority]");
      const harnessProfileLockMode = document.querySelector("[data-harness-profile-lock-mode]");
      const allowPerUserStoryHarnessProfileOverrides = document.querySelector("[data-allow-per-us-harness-profile-overrides]");
      const autoRefinementProfile = document.querySelector("[data-auto-refinement-profile]");
      const autoRefinementWrapper = document.querySelector("[data-auto-refinement-profile-wrapper]");
      const refinementTolerance = document.querySelector("[data-refinement-tolerance]");
      const mvpRigor = document.querySelector("[data-mvp-rigor]");
      const reviewTolerance = document.querySelector("[data-review-tolerance]");
      const reviewEvidencePolicy = document.querySelector("[data-review-evidence-policy]");
      const technicalDesignSubagents = document.querySelector("[data-technical-design-subagents]");
      const reviewSubagents = document.querySelector("[data-review-subagents]");
      const watcherEnabled = document.querySelector("[data-watcher-enabled]");
      const attentionNotificationsEnabled = document.querySelector("[data-attention-notifications-enabled]");
      const contextSuggestionsEnabled = document.querySelector("[data-context-suggestions-enabled]");
      const workflowGraphLayoutMode = document.querySelector("[data-workflow-graph-layout-mode]");
      const workflowGraphInitialZoomMode = document.querySelector("[data-workflow-graph-initial-zoom-mode]");
      const userStoryListViewMode = document.querySelector("[data-user-story-list-view-mode]");
      const visualTimelineEnabled = document.querySelector("[data-visual-timeline-enabled]");
      const requireApprovalBranchAcceptance = document.querySelector("[data-require-approval-branch-acceptance]");
      const autoRefinementEnabled = document.querySelector("[data-auto-refinement-enabled]");
      const autoPlayEnabled = document.querySelector("[data-auto-play-enabled]");
      const autoReviewEnabled = document.querySelector("[data-auto-review-enabled]");
      const maxRefinementCycles = document.querySelector("[data-max-refinement-cycles]");
      const maxImplementationReviewCycles = document.querySelector("[data-max-implementation-review-cycles]");
      const phaseQualityGateThresholdPercent = document.querySelector("[data-phase-quality-gate-threshold-percent]");
      const refinementQualityGateMaxRetries = document.querySelector("[data-refinement-quality-gate-max-retries]");
      const reviewQualityGateMaxRetries = document.querySelector("[data-review-quality-gate-max-retries]");
      const keepBestPhaseArtifactOnQualityRegression = document.querySelector("[data-keep-best-phase-artifact-on-quality-regression]");
      const destructiveRewindEnabled = document.querySelector("[data-destructive-rewind-enabled]");
      const pauseOnFailedReview = document.querySelector("[data-pause-on-failed-review]");
      const useSemanticGraphWhenAvailable = document.querySelector("[data-use-semantic-graph-when-available]");
      const allowGraphBuildRefreshForTouchedUserStoryScope = document.querySelector("[data-allow-graph-build-refresh-for-touched-us-scope]");
      const reviewLearningEnabled = document.querySelector("[data-review-learning-enabled]");
      const reviewLearningSkillPath = document.querySelector("[data-review-learning-skill-path]");
      const completedUsLockOnCompleted = document.querySelector("[data-completed-us-lock-on-completed]");
      const saveButton = document.querySelector('button[type="submit"]');
      const saveError = document.querySelector("[data-save-error]");
      if (!(profilesHost instanceof HTMLElement) || !(agentsHost instanceof HTMLElement) || !(phaseGrid instanceof HTMLElement) || !(harnessPhaseGrid instanceof HTMLElement)) {
        return;
      }

      if (state.modelProfiles.length === 0) {
        profilesHost.innerHTML = '<div class="empty">No provider profiles configured yet.</div>';
      } else {
        profilesHost.innerHTML = state.modelProfiles.map((profile, index) => {
          const showEndpointFields = !isNativeCliProvider(profile.provider);
          const isExpanded = Array.isArray(state.expandedProfileIndexes) ? Boolean(state.expandedProfileIndexes[index]) : index === 0;
          const summaryTitle = escapeHtml(profile.name || ('Profile ' + (index + 1)));
          const summaryMeta = [
            String(profile.provider || "openai-compatible").trim() || "openai-compatible",
            String(profile.repositoryAccess || "none").trim() || "none"
          ].join(" · ");
          return '<details class="profile-card" data-profile-index="' + index + '"' + (isExpanded ? ' open' : '') + '>'
            + '<summary class="profile-card__summary">'
            + '<div class="profile-card__summary-main">'
            + '<strong class="profile-card__summary-title">' + summaryTitle + '</strong>'
            + '<span class="profile-card__summary-meta">' + escapeHtml(summaryMeta) + '</span>'
            + '</div>'
            + '<div class="profile-card__summary-actions">'
            + '<button class="danger-action" type="button" data-remove-profile="' + index + '" title="Remove profile ' + (index + 1) + '" aria-label="Remove profile ' + (index + 1) + '">×</button>'
            + '<span class="profile-card__chevron" aria-hidden="true">⌄</span>'
            + '</div>'
            + '</summary>'
            + '<div class="profile-grid">'
            + fieldMarkup("Name", '<input type="text" data-profile-field="name" value="' + escapeHtml(profile.name || "") + '" placeholder="codex-main" />')
            + fieldMarkup("Provider", '<select data-profile-field="provider">' + providerOptions(profile.provider || "openai-compatible") + '</select>')
            + fieldMarkup("Repository Access", '<select data-profile-field="repositoryAccess">' + repositoryAccessOptions(profile.repositoryAccess || "none") + '</select>')
            + fieldMarkup("Base URL", '<input type="text" data-profile-field="baseUrl" value="' + escapeHtml(profile.baseUrl || "") + '" placeholder="https://api.example.test/v1" />', !showEndpointFields)
            + fieldMarkup("API Key", '<input type="password" data-profile-field="apiKey" value="' + escapeHtml(profile.apiKey || "") + '" placeholder="secret" />', !showEndpointFields)
            + fieldMarkup("Model", '<input type="text" data-profile-field="model" value="' + escapeHtml(profile.model || "") + '" placeholder="' + escapeHtml(profile.provider === "openai-compatible" ? "gpt-5.4" : "gpt-5.3-codex") + '" />')
            + fieldMarkup("Reasoning Effort", '<select data-profile-field="reasoningEffort">' + reasoningEffortOptions(profile.reasoningEffort || "") + '</select>')
            + '</div>'
            + '</details>';
        }).join("");
      }

      if (state.agentProfiles.length === 0) {
        agentsHost.innerHTML = '<div class="empty">No agent profiles configured yet.</div>';
      } else {
        agentsHost.innerHTML = state.agentProfiles.map((agent, index) => {
          const isExpanded = Array.isArray(state.expandedAgentIndexes) ? Boolean(state.expandedAgentIndexes[index]) : index === 0;
          const summaryTitle = escapeHtml(agent.name || ('Agent ' + (index + 1)));
          const summaryMeta = [
            String(agent.role || "agent").trim() || "agent",
            String(agent.modelProfile || "").trim() || "no model",
            String(agent.repositoryAccess || "none").trim() || "none"
          ].join(" · ");
          return '<details class="profile-card" data-agent-index="' + index + '"' + (isExpanded ? ' open' : '') + '>'
            + '<summary class="profile-card__summary">'
            + '<div class="profile-card__summary-main">'
            + '<strong class="profile-card__summary-title">' + summaryTitle + '</strong>'
            + '<span class="profile-card__summary-meta">' + escapeHtml(summaryMeta) + '</span>'
            + '</div>'
            + '<div class="profile-card__summary-actions">'
            + '<button class="danger-action" type="button" data-remove-agent="' + index + '" title="Remove agent ' + (index + 1) + '" aria-label="Remove agent ' + (index + 1) + '">×</button>'
            + '<span class="profile-card__chevron" aria-hidden="true">⌄</span>'
            + '</div>'
            + '</summary>'
            + '<div class="profile-grid">'
            + fieldMarkup("Name", '<input type="text" data-agent-field="name" value="' + escapeHtml(agent.name || "") + '" placeholder="planner" />')
            + fieldMarkup("Role", '<input type="text" data-agent-field="role" value="' + escapeHtml(agent.role || "") + '" placeholder="Planner" />')
            + fieldMarkup("Model Profile", '<select data-agent-field="modelProfile">' + modelProfileOptions(agent.modelProfile || "") + '</select>')
            + fieldMarkup("Repository Access", '<select data-agent-field="repositoryAccess">' + repositoryAccessOptions(agent.repositoryAccess || "none") + '</select>')
            + fieldMarkup("Reasoning Effort", '<select data-agent-field="reasoningEffort">' + reasoningEffortOptions(agent.reasoningEffort || "") + '</select>')
            + fieldMarkup("Instructions", '<input type="text" data-agent-field="instructions" value="' + escapeHtml(agent.instructions || "") + '" placeholder="Follow the phase contract exactly." />')
            + '</div>'
            + '</details>';
        }).join("");
      }

      for (const select of phaseGrid.querySelectorAll("[data-phase-field]")) {
        if (!(select instanceof HTMLSelectElement) || !select.dataset.phaseField) {
          continue;
        }
        const value = state.phaseAgentAssignments[select.dataset.phaseField] || "";
        select.innerHTML = phaseOptions(value);
        select.value = value;
        select.addEventListener("change", () => {
          state.phaseAgentAssignments[select.dataset.phaseField] = select.value;
        });
      }

      for (const select of harnessPhaseGrid.querySelectorAll("[data-harness-phase-field]")) {
        if (!(select instanceof HTMLSelectElement) || !select.dataset.harnessPhaseField) {
          continue;
        }
        const phaseId = select.dataset.harnessPhaseField;
        const stateKey = phaseIdToHarnessAssignmentKey(phaseId);
        const value = state.phaseHarnessProfiles[stateKey] || "";
        select.innerHTML = harnessProfileSelectOptions(value);
        select.value = value;
        select.addEventListener("change", () => {
          state.phaseHarnessProfiles[stateKey] = select.value || null;
        });
      }

      if (defaultHarnessProfile instanceof HTMLSelectElement) {
        defaultHarnessProfile.value = state.defaultHarnessProfile || "balanced";
        defaultHarnessProfile.addEventListener("change", () => {
          state.defaultHarnessProfile = defaultHarnessProfile.value || "balanced";
        });
      }

      if (harnessProfileAuthority instanceof HTMLSelectElement) {
        harnessProfileAuthority.value = state.harnessProfileAuthority || "workspace";
        harnessProfileAuthority.addEventListener("change", () => {
          state.harnessProfileAuthority = harnessProfileAuthority.value === "central" ? "central" : "workspace";
        });
      }

      if (harnessProfileLockMode instanceof HTMLSelectElement) {
        harnessProfileLockMode.value = state.harnessProfileLockMode || "none";
        harnessProfileLockMode.addEventListener("change", () => {
          state.harnessProfileLockMode = harnessProfileLockMode.value === "all"
            ? "all"
            : harnessProfileLockMode.value === "phase"
              ? "phase"
              : "none";
        });
      }

      if (allowPerUserStoryHarnessProfileOverrides instanceof HTMLSelectElement) {
        allowPerUserStoryHarnessProfileOverrides.value = state.allowPerUserStoryHarnessProfileOverrides ? "true" : "false";
        allowPerUserStoryHarnessProfileOverrides.addEventListener("change", () => {
          state.allowPerUserStoryHarnessProfileOverrides = allowPerUserStoryHarnessProfileOverrides.value === "true";
        });
      }

      for (const select of document.querySelectorAll("[data-harness-lock-phase]")) {
        if (!(select instanceof HTMLSelectElement) || !select.dataset.harnessLockPhase) {
          continue;
        }
        const phaseId = select.dataset.harnessLockPhase;
        select.value = state.lockedHarnessPhaseIds.includes(phaseId) ? "true" : "false";
        select.addEventListener("change", () => {
          const locked = new Set(state.lockedHarnessPhaseIds || []);
          if (select.value === "true") {
            locked.add(phaseId);
          } else {
            locked.delete(phaseId);
          }
          state.lockedHarnessPhaseIds = Array.from(locked);
        });
      }

      if (autoRefinementProfile instanceof HTMLSelectElement) {
        autoRefinementProfile.innerHTML = autoRefinementProfileOptions(state.autoRefinementAnswersProfile || "");
        autoRefinementProfile.value = state.autoRefinementAnswersProfile || "";
        autoRefinementProfile.addEventListener("change", () => {
          state.autoRefinementAnswersProfile = autoRefinementProfile.value;
        });
      }

      if (refinementTolerance instanceof HTMLSelectElement) {
        refinementTolerance.value = state.refinementTolerance || "balanced";
        refinementTolerance.addEventListener("change", () => {
          state.refinementTolerance = refinementTolerance.value || "balanced";
        });
      }

      if (mvpRigor instanceof HTMLSelectElement) {
        mvpRigor.value = state.mvpRigor || "medium";
        mvpRigor.addEventListener("change", () => {
          state.mvpRigor = mvpRigor.value || "medium";
        });
      }

      if (reviewTolerance instanceof HTMLSelectElement) {
        reviewTolerance.value = state.reviewTolerance || "balanced";
        reviewTolerance.addEventListener("change", () => {
          state.reviewTolerance = reviewTolerance.value || "balanced";
        });
      }

      if (reviewEvidencePolicy instanceof HTMLSelectElement) {
        reviewEvidencePolicy.value = state.reviewEvidencePolicy || "balanced";
        reviewEvidencePolicy.addEventListener("change", () => {
          state.reviewEvidencePolicy = reviewEvidencePolicy.value || "balanced";
        });
      }

      bindSwitch(technicalDesignSubagents, "technicalDesignSubagentsEnabled");
      bindSwitch(reviewSubagents, "reviewSubagentsEnabled");

      if (watcherEnabled instanceof HTMLSelectElement) {
        watcherEnabled.value = state.watcherEnabled ? "true" : "false";
        watcherEnabled.addEventListener("change", () => {
          state.watcherEnabled = watcherEnabled.value === "true";
        });
      }

      if (attentionNotificationsEnabled instanceof HTMLSelectElement) {
        attentionNotificationsEnabled.value = state.attentionNotificationsEnabled ? "true" : "false";
        attentionNotificationsEnabled.addEventListener("change", () => {
          state.attentionNotificationsEnabled = attentionNotificationsEnabled.value === "true";
        });
      }

      if (contextSuggestionsEnabled instanceof HTMLSelectElement) {
        contextSuggestionsEnabled.value = state.contextSuggestionsEnabled ? "true" : "false";
        contextSuggestionsEnabled.addEventListener("change", () => {
          state.contextSuggestionsEnabled = contextSuggestionsEnabled.value === "true";
        });
      }

      if (workflowGraphLayoutMode instanceof HTMLSelectElement) {
        workflowGraphLayoutMode.value = state.workflowGraphLayoutMode === "horizontal" ? "horizontal" : "vertical";
        workflowGraphLayoutMode.addEventListener("change", () => {
          state.workflowGraphLayoutMode = workflowGraphLayoutMode.value === "horizontal" ? "horizontal" : "vertical";
        });
      }

      if (workflowGraphInitialZoomMode instanceof HTMLSelectElement) {
        workflowGraphInitialZoomMode.value = state.workflowGraphInitialZoomMode === "fit-width" ? "fit-width" : "actual-size";
        workflowGraphInitialZoomMode.addEventListener("change", () => {
          state.workflowGraphInitialZoomMode = workflowGraphInitialZoomMode.value === "fit-width" ? "fit-width" : "actual-size";
        });
      }

      if (userStoryListViewMode instanceof HTMLSelectElement) {
        userStoryListViewMode.value = state.userStoryListViewMode === "phase" ? "phase" : "category";
        userStoryListViewMode.addEventListener("change", () => {
          state.userStoryListViewMode = userStoryListViewMode.value === "phase" ? "phase" : "category";
        });
      }

      if (visualTimelineEnabled instanceof HTMLSelectElement) {
        visualTimelineEnabled.value = state.visualTimelineEnabled ? "true" : "false";
        visualTimelineEnabled.addEventListener("change", () => {
          state.visualTimelineEnabled = visualTimelineEnabled.value === "true";
        });
      }

      if (requireApprovalBranchAcceptance instanceof HTMLSelectElement) {
        requireApprovalBranchAcceptance.value = state.requireExplicitApprovalBranchAcceptance ? "true" : "false";
        requireApprovalBranchAcceptance.addEventListener("change", () => {
          state.requireExplicitApprovalBranchAcceptance = requireApprovalBranchAcceptance.value === "true";
        });
      }

      if (autoRefinementEnabled instanceof HTMLSelectElement) {
        autoRefinementEnabled.value = state.autoRefinementAnswersEnabled ? "true" : "false";
        autoRefinementEnabled.addEventListener("change", () => {
          state.autoRefinementAnswersEnabled = autoRefinementEnabled.value === "true";
          render();
        });
      }

      if (autoPlayEnabled instanceof HTMLSelectElement) {
        autoPlayEnabled.value = state.autoPlayEnabled ? "true" : "false";
        autoPlayEnabled.addEventListener("change", () => {
          state.autoPlayEnabled = autoPlayEnabled.value === "true";
        });
      }

      if (autoReviewEnabled instanceof HTMLSelectElement) {
        autoReviewEnabled.value = state.autoReviewEnabled ? "true" : "false";
        autoReviewEnabled.addEventListener("change", () => {
          state.autoReviewEnabled = autoReviewEnabled.value === "true";
        });
      }

      if (maxRefinementCycles instanceof HTMLInputElement) {
        maxRefinementCycles.value = String(state.maxRefinementCycles || 5);
        const syncMaxRefinementCycles = () => {
          const parsed = Number.parseInt(maxRefinementCycles.value, 10);
          state.maxRefinementCycles = Number.isFinite(parsed) && parsed > 0 ? parsed : 5;
          maxRefinementCycles.value = String(state.maxRefinementCycles);
        };
        maxRefinementCycles.addEventListener("input", syncMaxRefinementCycles);
        maxRefinementCycles.addEventListener("change", syncMaxRefinementCycles);
      }

      if (maxImplementationReviewCycles instanceof HTMLInputElement) {
        maxImplementationReviewCycles.value = String(state.maxImplementationReviewCycles || 5);
        const syncMaxCycles = () => {
          const parsed = Number.parseInt(maxImplementationReviewCycles.value, 10);
          state.maxImplementationReviewCycles = Number.isFinite(parsed) && parsed > 0 ? parsed : 5;
          maxImplementationReviewCycles.value = String(state.maxImplementationReviewCycles);
        };
        maxImplementationReviewCycles.addEventListener("input", syncMaxCycles);
        maxImplementationReviewCycles.addEventListener("change", syncMaxCycles);
      }

      if (phaseQualityGateThresholdPercent instanceof HTMLInputElement) {
        phaseQualityGateThresholdPercent.value = String(state.phaseQualityGateThresholdPercent || 85);
        const syncPhaseQualityGateThresholdPercent = () => {
          const parsed = Number.parseInt(phaseQualityGateThresholdPercent.value, 10);
          state.phaseQualityGateThresholdPercent = Number.isFinite(parsed)
            ? Math.max(0, Math.min(100, parsed))
            : 85;
          phaseQualityGateThresholdPercent.value = String(state.phaseQualityGateThresholdPercent);
        };
        phaseQualityGateThresholdPercent.addEventListener("input", syncPhaseQualityGateThresholdPercent);
        phaseQualityGateThresholdPercent.addEventListener("change", syncPhaseQualityGateThresholdPercent);
      }

      if (refinementQualityGateMaxRetries instanceof HTMLInputElement) {
        refinementQualityGateMaxRetries.value = String(state.refinementQualityGateMaxRetries || 5);
        const syncRefinementQualityGateMaxRetries = () => {
          const parsed = Number.parseInt(refinementQualityGateMaxRetries.value, 10);
          state.refinementQualityGateMaxRetries = Number.isFinite(parsed) && parsed > 0 ? parsed : 5;
          refinementQualityGateMaxRetries.value = String(state.refinementQualityGateMaxRetries);
        };
        refinementQualityGateMaxRetries.addEventListener("input", syncRefinementQualityGateMaxRetries);
        refinementQualityGateMaxRetries.addEventListener("change", syncRefinementQualityGateMaxRetries);
      }

      if (reviewQualityGateMaxRetries instanceof HTMLInputElement) {
        reviewQualityGateMaxRetries.value = String(state.reviewQualityGateMaxRetries || 3);
        const syncReviewQualityGateMaxRetries = () => {
          const parsed = Number.parseInt(reviewQualityGateMaxRetries.value, 10);
          state.reviewQualityGateMaxRetries = Number.isFinite(parsed) && parsed > 0 ? parsed : 3;
          reviewQualityGateMaxRetries.value = String(state.reviewQualityGateMaxRetries);
        };
        reviewQualityGateMaxRetries.addEventListener("input", syncReviewQualityGateMaxRetries);
        reviewQualityGateMaxRetries.addEventListener("change", syncReviewQualityGateMaxRetries);
      }

      if (keepBestPhaseArtifactOnQualityRegression instanceof HTMLSelectElement) {
        keepBestPhaseArtifactOnQualityRegression.value = state.keepBestPhaseArtifactOnQualityRegression ? "true" : "false";
        keepBestPhaseArtifactOnQualityRegression.addEventListener("change", () => {
          state.keepBestPhaseArtifactOnQualityRegression = keepBestPhaseArtifactOnQualityRegression.value === "true";
        });
      }

      if (destructiveRewindEnabled instanceof HTMLSelectElement) {
        destructiveRewindEnabled.value = state.destructiveRewindEnabled ? "true" : "false";
        destructiveRewindEnabled.addEventListener("change", () => {
          state.destructiveRewindEnabled = destructiveRewindEnabled.value === "true";
        });
      }

      if (pauseOnFailedReview instanceof HTMLSelectElement) {
        pauseOnFailedReview.value = state.pauseOnFailedReview ? "true" : "false";
        pauseOnFailedReview.addEventListener("change", () => {
          state.pauseOnFailedReview = pauseOnFailedReview.value === "true";
        });
      }

      if (useSemanticGraphWhenAvailable instanceof HTMLSelectElement) {
        useSemanticGraphWhenAvailable.value = state.useSemanticGraphWhenAvailable ? "true" : "false";
        useSemanticGraphWhenAvailable.addEventListener("change", () => {
          state.useSemanticGraphWhenAvailable = useSemanticGraphWhenAvailable.value === "true";
        });
      }

      if (allowGraphBuildRefreshForTouchedUserStoryScope instanceof HTMLSelectElement) {
        allowGraphBuildRefreshForTouchedUserStoryScope.value = state.allowGraphBuildRefreshForTouchedUserStoryScope ? "true" : "false";
        allowGraphBuildRefreshForTouchedUserStoryScope.addEventListener("change", () => {
          state.allowGraphBuildRefreshForTouchedUserStoryScope = allowGraphBuildRefreshForTouchedUserStoryScope.value === "true";
        });
      }

      if (reviewLearningEnabled instanceof HTMLSelectElement) {
        reviewLearningEnabled.value = state.reviewLearningEnabled ? "true" : "false";
        reviewLearningEnabled.addEventListener("change", () => {
          state.reviewLearningEnabled = reviewLearningEnabled.value === "true";
        });
      }

      if (reviewLearningSkillPath instanceof HTMLInputElement) {
        reviewLearningSkillPath.value = state.reviewLearningSkillPath || ".codex/skills/sdd-phase-agents/SKILL.md";
        reviewLearningSkillPath.addEventListener("input", () => {
          state.reviewLearningSkillPath = reviewLearningSkillPath.value;
        });
        reviewLearningSkillPath.addEventListener("change", () => {
          state.reviewLearningSkillPath = reviewLearningSkillPath.value.trim() || ".codex/skills/sdd-phase-agents/SKILL.md";
          reviewLearningSkillPath.value = state.reviewLearningSkillPath;
        });
      }

      if (completedUsLockOnCompleted instanceof HTMLSelectElement) {
        completedUsLockOnCompleted.value = state.completedUsLockOnCompleted ? "true" : "false";
        completedUsLockOnCompleted.addEventListener("change", () => {
          state.completedUsLockOnCompleted = completedUsLockOnCompleted.value === "true";
        });
      }

      const fallbackProblem = hasFallbackProblem();
      const autoRefinementProblem = hasAutoRefinementProblem();
      const permissionIssues = validatePermissionIssues();
      if (warning instanceof HTMLElement) {
        warning.classList.toggle("warning-banner--visible", fallbackProblem);
      }
      const defaultWrapper = document.querySelector('[data-phase-wrapper="defaultAgent"]');
      if (defaultWrapper instanceof HTMLElement) {
        defaultWrapper.classList.toggle("phase-field--invalid", fallbackProblem);
      }
      if (autoRefinementWrapper instanceof HTMLElement) {
        autoRefinementWrapper.classList.toggle("phase-field--invalid", autoRefinementProblem);
      }
      for (const phase of executionPhases) {
        if (phase.key === "defaultAgent") {
          continue;
        }
        const wrapper = document.querySelector('[data-phase-wrapper="' + phase.key + '"]');
        const hint = wrapper instanceof HTMLElement ? wrapper.querySelector(".phase-field__hint") : null;
        const issue = permissionIssues.find((candidate) => candidate.assignmentKey === phase.key);
        if (wrapper instanceof HTMLElement) {
          wrapper.classList.toggle("phase-field--invalid", Boolean(issue));
        }
        if (hint instanceof HTMLElement) {
          hint.textContent = issue ? issue.message : "";
        }
      }
      if (saveButton instanceof HTMLButtonElement) {
        saveButton.disabled = fallbackProblem || autoRefinementProblem || permissionIssues.length > 0;
        saveButton.title = fallbackProblem
          ? "Define the default fallback agent before saving."
          : autoRefinementProblem
            ? "Select the agent that should answer refinement questions."
            : permissionIssues.length > 0
              ? permissionIssues[0].message
            : "";
      }
      if (saveError instanceof HTMLElement) {
        const errorMessage = fallbackProblem
          ? "Define the default fallback agent before saving."
          : autoRefinementProblem
            ? "Select the agent that should answer refinement questions."
            : permissionIssues[0]?.message || "";
        saveError.textContent = errorMessage;
        saveError.classList.toggle("save-error--visible", errorMessage.length > 0);
      }

      for (const button of profilesHost.querySelectorAll("[data-remove-profile]")) {
        if (!(button instanceof HTMLButtonElement)) {
          continue;
        }
        button.addEventListener("click", () => {
          const index = Number(button.dataset.removeProfile ?? "-1");
          if (index < 0) {
            return;
          }
          state.modelProfiles.splice(index, 1);
          if (Array.isArray(state.expandedProfileIndexes)) {
            state.expandedProfileIndexes.splice(index, 1);
          }
          pruneMissingAssignments();
          render();
        });
      }

      for (const card of profilesHost.querySelectorAll("[data-profile-index]")) {
        if (!(card instanceof HTMLDetailsElement)) {
          continue;
        }
        const index = Number(card.dataset.profileIndex ?? "-1");
        if (index < 0) {
          continue;
        }
        card.addEventListener("toggle", () => {
          if (!Array.isArray(state.expandedProfileIndexes)) {
            state.expandedProfileIndexes = [];
          }
          state.expandedProfileIndexes[index] = card.open;
        });
      }

      for (const card of profilesHost.querySelectorAll("[data-profile-index]")) {
        for (const input of card.querySelectorAll("[data-profile-field]")) {
          if (!(input instanceof HTMLInputElement || input instanceof HTMLSelectElement)) {
            continue;
          }
          input.addEventListener("input", syncFromDomSilently);
          input.addEventListener("change", syncFromDom);
        }
      }

      for (const button of agentsHost.querySelectorAll("[data-remove-agent]")) {
        if (!(button instanceof HTMLButtonElement)) {
          continue;
        }
        button.addEventListener("click", () => {
          const index = Number(button.dataset.removeAgent ?? "-1");
          if (index < 0) {
            return;
          }
          state.agentProfiles.splice(index, 1);
          if (Array.isArray(state.expandedAgentIndexes)) {
            state.expandedAgentIndexes.splice(index, 1);
          }
          pruneMissingAssignments();
          render();
        });
      }

      for (const card of agentsHost.querySelectorAll("[data-agent-index]")) {
        if (!(card instanceof HTMLDetailsElement)) {
          continue;
        }
        const index = Number(card.dataset.agentIndex ?? "-1");
        if (index < 0) {
          continue;
        }
        card.addEventListener("toggle", () => {
          if (!Array.isArray(state.expandedAgentIndexes)) {
            state.expandedAgentIndexes = [];
          }
          state.expandedAgentIndexes[index] = card.open;
        });
      }

      for (const card of agentsHost.querySelectorAll("[data-agent-index]")) {
        for (const input of card.querySelectorAll("[data-agent-field]")) {
          if (!(input instanceof HTMLInputElement || input instanceof HTMLSelectElement)) {
            continue;
          }
          input.addEventListener("input", syncFromDomSilently);
          input.addEventListener("change", syncFromDom);
        }
      }

      if (typeof state.pendingFocusProfileIndex === "number" && state.pendingFocusProfileIndex >= 0) {
        const targetCard = profilesHost.querySelector('[data-profile-index="' + state.pendingFocusProfileIndex + '"]');
        if (targetCard instanceof HTMLDetailsElement) {
          targetCard.open = true;
          requestAnimationFrame(() => {
            targetCard.scrollIntoView({ behavior: "smooth", block: "center" });
            const firstField = targetCard.querySelector('[data-profile-field="name"]');
            if (firstField instanceof HTMLInputElement || firstField instanceof HTMLSelectElement) {
              firstField.focus();
              if (firstField instanceof HTMLInputElement) {
                firstField.select();
              }
            }
          });
        }
        state.pendingFocusProfileIndex = null;
      }

      if (typeof state.pendingFocusAgentIndex === "number" && state.pendingFocusAgentIndex >= 0) {
        const targetCard = agentsHost.querySelector('[data-agent-index="' + state.pendingFocusAgentIndex + '"]');
        if (targetCard instanceof HTMLDetailsElement) {
          targetCard.open = true;
          requestAnimationFrame(() => {
            targetCard.scrollIntoView({ behavior: "smooth", block: "center" });
            const firstField = targetCard.querySelector('[data-agent-field="name"]');
            if (firstField instanceof HTMLInputElement || firstField instanceof HTMLSelectElement) {
              firstField.focus();
              if (firstField instanceof HTMLInputElement) {
                firstField.select();
              }
            }
          });
        }
        state.pendingFocusAgentIndex = null;
      }
    }

    function fieldMarkup(label, controlMarkup, hidden) {
      return '<label class="' + (hidden ? 'hidden-field' : '') + '"><span>' + escapeHtml(label) + '</span>' + controlMarkup + '</label>';
    }

    function bindSwitch(control, stateKey) {
      if (!(control instanceof HTMLButtonElement)) {
        return;
      }

      const sync = () => {
        control.setAttribute("aria-checked", state[stateKey] ? "true" : "false");
      };
      sync();
      control.addEventListener("click", () => {
        state[stateKey] = !state[stateKey];
        sync();
      });
    }

    function syncFromDom() {
      const previousProfiles = state.modelProfiles.slice();
      const previousAgents = state.agentProfiles.slice();
      const nextProfiles = [];
      for (const card of document.querySelectorAll("[data-profile-index]")) {
        nextProfiles.push({
          name: readProfileField(card, "name"),
          provider: readProfileField(card, "provider") || "openai-compatible",
          baseUrl: readProfileField(card, "baseUrl"),
          apiKey: readProfileField(card, "apiKey"),
          model: readProfileField(card, "model"),
          reasoningEffort: readProfileField(card, "reasoningEffort"),
          repositoryAccess: readProfileField(card, "repositoryAccess") || "none"
        });
      }
      const nextAgents = readAgentsFromDom();
      remapAgentModelProfilesForRenamedModels(previousProfiles, nextProfiles, nextAgents);
      remapAssignmentsForRenamedAgents(previousAgents, nextAgents);
      state.modelProfiles = nextProfiles;
      state.agentProfiles = nextAgents;
      pruneMissingAssignments();
      render();
    }

    function syncFromDomSilently() {
      const previousProfiles = state.modelProfiles.slice();
      const previousAgents = state.agentProfiles.slice();
      const nextProfiles = [];
      for (const card of document.querySelectorAll("[data-profile-index]")) {
        nextProfiles.push({
          name: readProfileField(card, "name"),
          provider: readProfileField(card, "provider") || "openai-compatible",
          baseUrl: readProfileField(card, "baseUrl"),
          apiKey: readProfileField(card, "apiKey"),
          model: readProfileField(card, "model"),
          reasoningEffort: readProfileField(card, "reasoningEffort"),
          repositoryAccess: readProfileField(card, "repositoryAccess") || "none"
        });
      }
      const nextAgents = readAgentsFromDom();
      remapAgentModelProfilesForRenamedModels(previousProfiles, nextProfiles, nextAgents);
      remapAssignmentsForRenamedAgents(previousAgents, nextAgents);
      state.modelProfiles = nextProfiles;
      state.agentProfiles = nextAgents;
      pruneMissingAssignments();
    }

    function readAgentsFromDom() {
      const nextAgents = [];
      for (const card of document.querySelectorAll("[data-agent-index]")) {
        nextAgents.push({
          name: readAgentField(card, "name"),
          role: readAgentField(card, "role"),
          modelProfile: readAgentField(card, "modelProfile"),
          instructions: readAgentField(card, "instructions"),
          repositoryAccess: readAgentField(card, "repositoryAccess") || "none",
          reasoningEffort: readAgentField(card, "reasoningEffort")
        });
      }
      return nextAgents;
    }

    function remapAgentModelProfilesForRenamedModels(previousProfiles, nextProfiles, nextAgents) {
      const renameMap = new Map();
      for (let index = 0; index < Math.min(previousProfiles.length, nextProfiles.length); index += 1) {
        const previousName = String(previousProfiles[index]?.name || "").trim();
        const nextName = String(nextProfiles[index]?.name || "").trim();
        if (!previousName || !nextName || previousName === nextName) {
          continue;
        }

        renameMap.set(previousName, nextName);
      }

      if (renameMap.size === 0) {
        return;
      }

      for (const agent of nextAgents) {
        const current = String(agent.modelProfile || "").trim();
        if (current && renameMap.has(current)) {
          agent.modelProfile = renameMap.get(current);
        }
      }
    }

    function remapAssignmentsForRenamedAgents(previousAgents, nextAgents) {
      const renameMap = new Map();
      for (let index = 0; index < Math.min(previousAgents.length, nextAgents.length); index += 1) {
        const previousName = String(previousAgents[index]?.name || "").trim();
        const nextName = String(nextAgents[index]?.name || "").trim();
        if (!previousName || !nextName || previousName === nextName) {
          continue;
        }

        renameMap.set(previousName, nextName);
      }

      if (renameMap.size === 0) {
        return;
      }

      for (const phase of executionPhases) {
        const current = String(state.phaseAgentAssignments[phase.key] || "").trim();
        if (current && renameMap.has(current)) {
          state.phaseAgentAssignments[phase.key] = renameMap.get(current);
        }
      }

      const autoRefinementProfile = String(state.autoRefinementAnswersProfile || "").trim();
      if (autoRefinementProfile && renameMap.has(autoRefinementProfile)) {
        state.autoRefinementAnswersProfile = renameMap.get(autoRefinementProfile);
      }
    }

    function pruneMissingAssignments() {
      const names = new Set(state.agentProfiles.map((agent) => agent.name).filter(Boolean));
      for (const phase of executionPhases) {
        const current = state.phaseAgentAssignments[phase.key];
        if (current && !names.has(current)) {
          state.phaseAgentAssignments[phase.key] = "";
        }
      }
      if (state.autoRefinementAnswersProfile && !names.has(state.autoRefinementAnswersProfile)) {
        state.autoRefinementAnswersProfile = "";
      }
    }

    function readProfileField(card, field) {
      const input = card.querySelector('[data-profile-field="' + field + '"]');
      return input instanceof HTMLInputElement || input instanceof HTMLSelectElement
        ? input.value
        : "";
    }

    function readAgentField(card, field) {
      const input = card.querySelector('[data-agent-field="' + field + '"]');
      return input instanceof HTMLInputElement || input instanceof HTMLSelectElement
        ? input.value
        : "";
    }

    document.querySelector("[data-command='openRawSettings']")?.addEventListener("click", () => {
      vscode.postMessage({ command: "openRawSettings" });
    });

    document.querySelector("[data-add-profile]")?.addEventListener("click", () => {
      const nextIndex = state.modelProfiles.length;
      state.modelProfiles.push({
        name: "",
        provider: "codex",
        baseUrl: "",
        apiKey: "",
        model: "",
        reasoningEffort: "",
        repositoryAccess: "none"
      });
      if (!Array.isArray(state.expandedProfileIndexes)) {
        state.expandedProfileIndexes = [];
      }
      state.expandedProfileIndexes = state.modelProfiles.map((_, index) => index === nextIndex);
      state.pendingFocusProfileIndex = nextIndex;
      render();
    });

    document.querySelector("[data-add-agent]")?.addEventListener("click", () => {
      const nextIndex = state.agentProfiles.length;
      state.agentProfiles.push({
        name: "",
        role: "",
        modelProfile: state.modelProfiles[0]?.name || "",
        instructions: "",
        repositoryAccess: "none",
        reasoningEffort: ""
      });
      if (!Array.isArray(state.expandedAgentIndexes)) {
        state.expandedAgentIndexes = [];
      }
      state.expandedAgentIndexes = state.agentProfiles.map((_, index) => index === nextIndex);
      state.pendingFocusAgentIndex = nextIndex;
      render();
    });

    document.getElementById("execution-settings-form")?.addEventListener("submit", (event) => {
      event.preventDefault();
      syncFromDom();
      if (hasFallbackProblem() || hasAutoRefinementProblem() || validatePermissionIssues().length > 0) {
        return;
      }
      vscode.postMessage({
        command: "saveExecutionSettings",
        modelProfiles: state.modelProfiles,
        agentProfiles: state.agentProfiles,
        phaseAgentAssignments: state.phaseAgentAssignments,
        defaultHarnessProfile: state.defaultHarnessProfile,
        phaseHarnessProfiles: state.phaseHarnessProfiles,
        harnessProfileAuthority: state.harnessProfileAuthority,
        harnessProfileLockMode: state.harnessProfileLockMode,
        lockedHarnessPhaseIds: state.lockedHarnessPhaseIds,
        allowPerUserStoryHarnessProfileOverrides: state.allowPerUserStoryHarnessProfileOverrides,
        refinementTolerance: state.refinementTolerance,
        mvpRigor: state.mvpRigor,
        reviewTolerance: state.reviewTolerance,
        reviewEvidencePolicy: state.reviewEvidencePolicy,
        technicalDesignSubagentsEnabled: state.technicalDesignSubagentsEnabled,
        reviewSubagentsEnabled: state.reviewSubagentsEnabled,
        watcherEnabled: state.watcherEnabled,
        attentionNotificationsEnabled: state.attentionNotificationsEnabled,
        contextSuggestionsEnabled: state.contextSuggestionsEnabled,
        workflowGraphLayoutMode: state.workflowGraphLayoutMode,
        workflowGraphInitialZoomMode: state.workflowGraphInitialZoomMode,
        userStoryListViewMode: state.userStoryListViewMode,
        visualTimelineEnabled: state.visualTimelineEnabled,
        requireExplicitApprovalBranchAcceptance: state.requireExplicitApprovalBranchAcceptance,
        autoRefinementAnswersEnabled: state.autoRefinementAnswersEnabled,
        autoRefinementAnswersProfile: state.autoRefinementAnswersProfile,
        autoPlayEnabled: state.autoPlayEnabled,
        autoReviewEnabled: state.autoReviewEnabled,
        maxRefinementCycles: state.maxRefinementCycles,
        maxImplementationReviewCycles: state.maxImplementationReviewCycles,
        phaseQualityGateThresholdPercent: state.phaseQualityGateThresholdPercent,
        refinementQualityGateMaxRetries: state.refinementQualityGateMaxRetries,
        reviewQualityGateMaxRetries: state.reviewQualityGateMaxRetries,
        keepBestPhaseArtifactOnQualityRegression: state.keepBestPhaseArtifactOnQualityRegression,
        destructiveRewindEnabled: state.destructiveRewindEnabled,
        pauseOnFailedReview: state.pauseOnFailedReview,
        useSemanticGraphWhenAvailable: state.useSemanticGraphWhenAvailable,
        allowGraphBuildRefreshForTouchedUserStoryScope: state.allowGraphBuildRefreshForTouchedUserStoryScope,
        reviewLearningEnabled: state.reviewLearningEnabled,
        reviewLearningSkillPath: state.reviewLearningSkillPath,
        completedUsLockOnCompleted: state.completedUsLockOnCompleted
      });
    });

    render();
  </script>
</body>
</html>`;
}

async function saveExecutionSettingsAsync(request: ExecutionSettingsSaveRequest): Promise<void> {
  const {
    modelProfiles,
    agentProfiles,
    phaseAgentAssignments,
    defaultHarnessProfile,
    phaseHarnessProfiles,
    harnessProfileAuthority,
    harnessProfileLockMode,
    lockedHarnessPhaseIds,
    allowPerUserStoryHarnessProfileOverrides,
    refinementTolerance,
    mvpRigor,
    reviewTolerance,
    reviewEvidencePolicy,
    technicalDesignSubagentsEnabled,
    reviewSubagentsEnabled,
    watcherEnabled,
    attentionNotificationsEnabled,
    contextSuggestionsEnabled,
    workflowGraphLayoutMode,
    workflowGraphInitialZoomMode,
    userStoryListViewMode,
    visualTimelineEnabled,
    requireExplicitApprovalBranchAcceptance,
    autoRefinementAnswersEnabled,
    autoRefinementAnswersProfile,
    autoPlayEnabled,
    autoReviewEnabled,
    maxRefinementCycles,
    maxImplementationReviewCycles,
    phaseQualityGateThresholdPercent,
    refinementQualityGateMaxRetries,
    reviewQualityGateMaxRetries,
    keepBestPhaseArtifactOnQualityRegression,
    destructiveRewindEnabled,
    pauseOnFailedReview,
    useSemanticGraphWhenAvailable,
    allowGraphBuildRefreshForTouchedUserStoryScope,
    reviewLearningEnabled,
    reviewLearningSkillPath,
    completedUsLockOnCompleted
  } = request;
  const configuration = vscode.workspace.getConfiguration("specForge");
  const normalizedProfiles = modelProfiles
    .map((profile) => ({
      name: typeof profile.name === "string" ? profile.name.trim() : "",
      provider: typeof profile.provider === "string" ? profile.provider.trim() : "openai-compatible",
      baseUrl: typeof profile.baseUrl === "string" ? profile.baseUrl.trim() : "",
      apiKey: typeof profile.apiKey === "string" ? profile.apiKey.trim() : "",
      model: typeof profile.model === "string" ? profile.model.trim() : "",
      reasoningEffort: typeof profile.reasoningEffort === "string" ? profile.reasoningEffort.trim().toLowerCase() : "",
      repositoryAccess: typeof profile.repositoryAccess === "string" ? profile.repositoryAccess.trim() : "none"
    }))
    .filter((profile) =>
      profile.name.length > 0
      || profile.baseUrl.length > 0
      || profile.apiKey.length > 0
      || profile.model.length > 0
      || profile.reasoningEffort.length > 0
      || profile.provider !== "openai-compatible"
      || profile.repositoryAccess !== "none");

  const normalizedAgents = agentProfiles
    .map((agent) => ({
      name: typeof agent.name === "string" ? agent.name.trim() : "",
      role: typeof agent.role === "string" ? agent.role.trim() : "",
      modelProfile: typeof agent.modelProfile === "string" ? agent.modelProfile.trim() : "",
      instructions: typeof agent.instructions === "string" ? agent.instructions.trim() : "",
      repositoryAccess: typeof agent.repositoryAccess === "string" ? agent.repositoryAccess.trim() : "none",
      reasoningEffort: typeof agent.reasoningEffort === "string" ? agent.reasoningEffort.trim().toLowerCase() : ""
    }))
    .filter((agent) =>
      agent.name.length > 0
      || agent.role.length > 0
      || agent.modelProfile.length > 0
      || agent.instructions.length > 0
      || agent.reasoningEffort.length > 0
      || agent.repositoryAccess !== "none");

  const normalizedAssignments: SpecForgePhaseAgentAssignments = {
    defaultAgent: normalizeOptionalAssignment(phaseAgentAssignments.defaultAgent),
    captureAgent: normalizeOptionalAssignment(phaseAgentAssignments.captureAgent),
    refinementAgent: normalizeOptionalAssignment(phaseAgentAssignments.refinementAgent),
    specAgent: normalizeOptionalAssignment(phaseAgentAssignments.specAgent),
    technicalDesignAgent: normalizeOptionalAssignment(phaseAgentAssignments.technicalDesignAgent),
    implementationAgent: normalizeOptionalAssignment(phaseAgentAssignments.implementationAgent),
    reviewAgent: normalizeOptionalAssignment(phaseAgentAssignments.reviewAgent),
    releaseApprovalAgent: normalizeOptionalAssignment(phaseAgentAssignments.releaseApprovalAgent),
    prPreparationAgent: normalizeOptionalAssignment(phaseAgentAssignments.prPreparationAgent)
  };
  const normalizedHarnessPhaseProfiles: SpecForgePhaseHarnessProfiles = {
    defaultProfile: normalizeHarnessProfileAssignment(phaseHarnessProfiles.defaultProfile),
    captureProfile: normalizeHarnessProfileAssignment(phaseHarnessProfiles.captureProfile),
    refinementProfile: normalizeHarnessProfileAssignment(phaseHarnessProfiles.refinementProfile),
    specProfile: normalizeHarnessProfileAssignment(phaseHarnessProfiles.specProfile),
    technicalDesignProfile: normalizeHarnessProfileAssignment(phaseHarnessProfiles.technicalDesignProfile),
    implementationProfile: normalizeHarnessProfileAssignment(phaseHarnessProfiles.implementationProfile),
    reviewProfile: normalizeHarnessProfileAssignment(phaseHarnessProfiles.reviewProfile),
    releaseApprovalProfile: normalizeHarnessProfileAssignment(phaseHarnessProfiles.releaseApprovalProfile),
    prPreparationProfile: normalizeHarnessProfileAssignment(phaseHarnessProfiles.prPreparationProfile)
  };
  const normalizedDefaultHarnessProfile = normalizeHarnessProfileSelection(defaultHarnessProfile);
  const normalizedHarnessAuthority = harnessProfileAuthority === "central" ? "central" : "workspace";
  const normalizedHarnessLockMode = harnessProfileLockMode === "all"
    ? "all"
    : harnessProfileLockMode === "phase"
      ? "phase"
      : "none";
  const normalizedLockedHarnessPhaseIds = Array.from(new Set(
    lockedHarnessPhaseIds
      .map((phaseId) => normalizeHarnessLockedPhaseId(phaseId))
      .filter((phaseId): phaseId is string => phaseId !== null)
  ));
  const modelNames = new Set(normalizedProfiles.map((profile) => profile.name));
  const agentWithoutModel = normalizedProfiles.length > 0
    ? normalizedAgents.find((agent) => !agent.modelProfile)
    : undefined;
  if (agentWithoutModel) {
    throw new Error(`Agent '${agentWithoutModel.name || "<unnamed>"}' must reference a model profile.`);
  }

  const missingModelAgent = normalizedAgents.find((agent) => agent.modelProfile && !modelNames.has(agent.modelProfile));
  if (missingModelAgent) {
    throw new Error(`Agent '${missingModelAgent.name || "<unnamed>"}' references unknown model profile '${missingModelAgent.modelProfile}'.`);
  }

  const agentNames = new Set(normalizedAgents.map((agent) => agent.name));
  const autoAnswerAgent = normalizeOptionalAssignment(autoRefinementAnswersProfile);
  if (autoRefinementAnswersEnabled && autoAnswerAgent && !agentNames.has(autoAnswerAgent)) {
    throw new Error(`Auto-refinement answers agent '${autoAnswerAgent}' was not configured.`);
  }

  const permissionIssues = validatePhasePermissionAssignments(normalizedAgents, normalizedAssignments);
  if (requiresDefaultFallback(normalizedAgents, normalizedAssignments)) {
    throw new Error("Define the default fallback agent before saving execution settings.");
  }
  if (autoRefinementAnswersEnabled && !normalizeOptionalAssignment(autoRefinementAnswersProfile)) {
    throw new Error("Select the agent that should answer refinement questions before saving execution settings.");
  }
  if (permissionIssues.length > 0) {
    throw new Error(permissionIssues[0]?.message ?? "Execution settings include a phase model permission mismatch.");
  }

  await configuration.update("execution.modelProfiles", normalizedProfiles, vscode.ConfigurationTarget.Workspace);
  await configuration.update("execution.agentProfiles", normalizedAgents, vscode.ConfigurationTarget.Workspace);
  await configuration.update("execution.phaseAgents", normalizedAssignments, vscode.ConfigurationTarget.Workspace);
  await configuration.update("execution.defaultHarnessProfile", normalizedDefaultHarnessProfile, vscode.ConfigurationTarget.Workspace);
  await configuration.update("execution.phaseHarnessProfiles", normalizedHarnessPhaseProfiles, vscode.ConfigurationTarget.Workspace);
  await configuration.update("execution.harnessProfileAuthority", normalizedHarnessAuthority, vscode.ConfigurationTarget.Workspace);
  await configuration.update("execution.harnessProfileLockMode", normalizedHarnessLockMode, vscode.ConfigurationTarget.Workspace);
  await configuration.update("execution.lockedHarnessPhaseIds", normalizedLockedHarnessPhaseIds, vscode.ConfigurationTarget.Workspace);
  await configuration.update("execution.allowPerUserStoryHarnessProfileOverrides", allowPerUserStoryHarnessProfileOverrides, vscode.ConfigurationTarget.Workspace);
  await configuration.update("execution.phaseModels", undefined, vscode.ConfigurationTarget.Workspace);
  await configuration.update("execution.refinementTolerance", refinementTolerance, vscode.ConfigurationTarget.Workspace);
  await configuration.update("execution.mvpRigor", mvpRigor, vscode.ConfigurationTarget.Workspace);
  await configuration.update("execution.reviewTolerance", reviewTolerance, vscode.ConfigurationTarget.Workspace);
  await configuration.update("execution.reviewEvidencePolicy", reviewEvidencePolicy, vscode.ConfigurationTarget.Workspace);
  await configuration.update("execution.technicalDesignSubagentsEnabled", technicalDesignSubagentsEnabled, vscode.ConfigurationTarget.Workspace);
  await configuration.update("execution.reviewSubagentsEnabled", reviewSubagentsEnabled, vscode.ConfigurationTarget.Workspace);
  await configuration.update("ui.workflowGraphLayoutMode", workflowGraphLayoutMode, vscode.ConfigurationTarget.Global);
  await configuration.update("ui.workflowGraphInitialZoomMode", workflowGraphInitialZoomMode, vscode.ConfigurationTarget.Global);
  await configuration.update("ui.userStoryListViewMode", userStoryListViewMode, vscode.ConfigurationTarget.Global);
  await configuration.update("ui.visualTimelineEnabled", visualTimelineEnabled, vscode.ConfigurationTarget.Global);
  await configuration.update("ui.enableWatcher", watcherEnabled, vscode.ConfigurationTarget.Global);
  await configuration.update("ui.notifyOnAttention", attentionNotificationsEnabled, vscode.ConfigurationTarget.Global);
  await configuration.update("features.enableContextSuggestions", contextSuggestionsEnabled, vscode.ConfigurationTarget.Global);
  await configuration.update("features.requireApprovalBranchAcceptance", requireExplicitApprovalBranchAcceptance, vscode.ConfigurationTarget.Workspace);
  await configuration.update("features.autoRefinementAnswersEnabled", autoRefinementAnswersEnabled, vscode.ConfigurationTarget.Workspace);
  await configuration.update(
    "execution.autoRefinementAnswersProfile",
    normalizeOptionalAssignment(autoRefinementAnswersProfile),
    vscode.ConfigurationTarget.Workspace);
  await configuration.update("features.autoPlayEnabled", autoPlayEnabled, vscode.ConfigurationTarget.Workspace);
  await configuration.update("features.autoReviewEnabled", autoReviewEnabled, vscode.ConfigurationTarget.Workspace);
  await configuration.update(
    "features.maxRefinementCycles",
    normalizePositiveInteger(maxRefinementCycles) ?? 5,
    vscode.ConfigurationTarget.Workspace);
  await configuration.update(
    "features.maxImplementationReviewCycles",
    normalizePositiveInteger(maxImplementationReviewCycles) ?? 5,
    vscode.ConfigurationTarget.Workspace);
  await configuration.update(
    "features.phaseQualityGateThresholdPercent",
    normalizePercentage(phaseQualityGateThresholdPercent) ?? 85,
    vscode.ConfigurationTarget.Workspace);
  await configuration.update(
    "features.refinementQualityGateMaxRetries",
    normalizePositiveInteger(refinementQualityGateMaxRetries) ?? 5,
    vscode.ConfigurationTarget.Workspace);
  await configuration.update(
    "features.reviewQualityGateMaxRetries",
    normalizePositiveInteger(reviewQualityGateMaxRetries) ?? 3,
    vscode.ConfigurationTarget.Workspace);
  await configuration.update(
    "features.keepBestPhaseArtifactOnQualityRegression",
    keepBestPhaseArtifactOnQualityRegression,
    vscode.ConfigurationTarget.Workspace);
  await configuration.update("features.destructiveRewindEnabled", destructiveRewindEnabled, vscode.ConfigurationTarget.Workspace);
  await configuration.update("features.pauseOnFailedReview", pauseOnFailedReview, vscode.ConfigurationTarget.Workspace);
  await configuration.update("features.useSemanticGraphWhenAvailable", useSemanticGraphWhenAvailable, vscode.ConfigurationTarget.Workspace);
  await configuration.update("features.allowGraphBuildRefreshForTouchedUserStoryScope", allowGraphBuildRefreshForTouchedUserStoryScope, vscode.ConfigurationTarget.Workspace);
  await configuration.update("features.reviewLearningEnabled", reviewLearningEnabled, vscode.ConfigurationTarget.Workspace);
  await configuration.update(
    "features.reviewLearningSkillPath",
    normalizeOptionalAssignment(reviewLearningSkillPath) ?? ".codex/skills/sdd-phase-agents/SKILL.md",
    vscode.ConfigurationTarget.Workspace);
  await configuration.update(
    "features.completedUsLockOnCompleted",
    completedUsLockOnCompleted,
    vscode.ConfigurationTarget.Workspace);
  await configuration.update("ui.workflowGraphLayoutMode", undefined, vscode.ConfigurationTarget.Workspace);
  await configuration.update("ui.workflowGraphInitialZoomMode", undefined, vscode.ConfigurationTarget.Workspace);
  await configuration.update("ui.userStoryListViewMode", undefined, vscode.ConfigurationTarget.Workspace);
  await configuration.update("ui.visualTimelineEnabled", undefined, vscode.ConfigurationTarget.Workspace);
  await configuration.update("ui.enableWatcher", undefined, vscode.ConfigurationTarget.Workspace);
  await configuration.update("ui.notifyOnAttention", undefined, vscode.ConfigurationTarget.Workspace);
  await configuration.update("features.enableContextSuggestions", undefined, vscode.ConfigurationTarget.Workspace);
}

function normalizeOptionalAssignment(value: string | null | undefined): string | null {
  const trimmed = value?.trim();
  return trimmed ? trimmed : null;
}

function normalizeHarnessProfileSelection(value: string | null | undefined): string {
  const normalized = value?.trim().toLowerCase();
  return normalized === "strict" || normalized === "regulated" ? normalized : "balanced";
}

function normalizeHarnessProfileAssignment(value: string | null | undefined): string | null {
  const normalized = value?.trim().toLowerCase();
  return normalized === "strict" || normalized === "balanced" || normalized === "regulated"
    ? normalized
    : null;
}

function normalizeHarnessLockedPhaseId(value: string | null | undefined): string | null {
  const normalized = value?.trim().toLowerCase();
  return normalized === "capture"
    || normalized === "refinement"
    || normalized === "spec"
    || normalized === "technical-design"
    || normalized === "implementation"
    || normalized === "review"
    || normalized === "release-approval"
    || normalized === "pr-preparation"
    ? normalized
    : null;
}

function normalizePositiveInteger(value: number | null | undefined): number | null {
  if (typeof value !== "number" || !Number.isFinite(value)) {
    return null;
  }

  const normalized = Math.trunc(value);
  return normalized > 0 ? normalized : null;
}

function normalizePercentage(value: number | null | undefined): number | null {
  if (typeof value !== "number" || !Number.isFinite(value)) {
    return null;
  }

  return Math.max(0, Math.min(100, Math.round(value)));
}
