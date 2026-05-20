import type { SpecForgeSettings } from "./extensionSettings";
import type { ModelResponseDiagnostic } from "./mcpDiagnostics";
import { StdioMcpBackendClient } from "./stdioMcpBackendClient";

export interface UserStorySummary {
  readonly usId: string;
  readonly title: string;
  readonly description?: string;
  readonly category: string;
  readonly tags?: readonly string[];
  readonly directoryPath: string;
  readonly mainArtifactPath: string;
  readonly currentPhase: string;
  readonly status: string;
  readonly workBranch: string | null;
  readonly dependencies?: readonly UserStoryDependencySummary[];
  readonly workflowKind?: string;
  readonly parentUsId?: string | null;
  readonly childUsIds?: readonly string[] | null;
}

export interface UserStoryDependencySummary {
  readonly usId: string;
  readonly title: string | null;
  readonly currentPhase: string | null;
  readonly status: string | null;
  readonly isSatisfied: boolean;
  readonly missingReason: string | null;
}

export interface UserStoryRuntimeStatus {
  readonly usId: string;
  readonly status: string;
  readonly activeOperation: string | null;
  readonly currentPhase: string;
  readonly startedAtUtc: string | null;
  readonly lastHeartbeatUtc: string | null;
  readonly lastOutcome: string | null;
  readonly lastCompletedAtUtc: string | null;
  readonly message: string | null;
  readonly isStale: boolean;
}

export interface ContinuePhaseResult {
  readonly usId: string;
  readonly currentPhase: string;
  readonly status: string;
  readonly generatedArtifactPath: string | null;
  readonly usage: TokenUsage | null;
  readonly execution?: PhaseExecutionMetadata | null;
  readonly commit?: PhaseCommitResult | null;
}

export interface PhaseCommitResult {
  readonly isGitWorkspace: boolean;
  readonly commitCreated: boolean;
  readonly commitSha: string | null;
  readonly message: string | null;
  readonly stagedPaths: readonly string[];
}

export interface TokenUsage {
  readonly inputTokens: number;
  readonly outputTokens: number;
  readonly totalTokens: number;
}

export interface PhaseExecutionMetadata {
  readonly providerKind: string;
  readonly model: string;
  readonly profileName: string | null;
  readonly agentName?: string | null;
  readonly agentRole?: string | null;
  readonly baseUrl: string | null;
  readonly warnings?: readonly string[] | null;
  readonly inputSha256?: string | null;
  readonly outputSha256?: string | null;
  readonly structuredOutputSha256?: string | null;
  readonly receiptPath?: string | null;
  readonly usedSkills?: readonly string[] | null;
}

type ModelResponseListener = (diagnostic: ModelResponseDiagnostic) => void;

const modelResponseListeners = new Set<ModelResponseListener>();

export function onModelResponseDiagnostic(listener: ModelResponseListener): () => void {
  modelResponseListeners.add(listener);
  return () => {
    modelResponseListeners.delete(listener);
  };
}

export function notifyModelResponseDiagnostic(diagnostic: ModelResponseDiagnostic): void {
  for (const listener of modelResponseListeners) {
    listener(diagnostic);
  }
}

export interface WorkflowLineageFinding {
  readonly severity: string;
  readonly confidence: string;
  readonly code: string;
  readonly summary: string;
  readonly phaseId: string | null;
  readonly eventTimestampUtc: string | null;
  readonly affectedArtifacts: readonly string[];
}

export interface WorkflowLineageAnalysisResult {
  readonly usId: string;
  readonly status: string;
  readonly findings: readonly WorkflowLineageFinding[];
  readonly deprecatedCandidatePaths: readonly string[];
  readonly recommendedTargetPhase: string | null;
}

export interface WorkflowLineageRepairResult {
  readonly usId: string;
  readonly status: string;
  readonly currentPhase: string;
  readonly archiveDirectoryPath: string;
  readonly archivedPaths: readonly string[];
  readonly analysis: WorkflowLineageAnalysisResult;
}

export interface CreateOrImportUserStoryResult {
  readonly usId: string;
  readonly rootDirectory: string;
  readonly mainArtifactPath: string;
}

export interface OperateCurrentPhaseArtifactResult {
  readonly usId: string;
  readonly currentPhase: string;
  readonly status: string;
  readonly operationLogPath: string;
  readonly sourceArtifactPath: string;
  readonly generatedArtifactPath: string;
  readonly usage: TokenUsage | null;
  readonly execution?: PhaseExecutionMetadata | null;
  readonly commit?: PhaseCommitResult | null;
}

export interface SubmitApprovalAnswerResult {
  readonly usId: string;
  readonly currentPhase: string;
  readonly status: string;
  readonly generatedArtifactPath: string;
}

export interface ApprovalAnswerSuggestionResult {
  readonly usId: string;
  readonly currentPhase: string;
  readonly status: string;
  readonly question: string;
  readonly answer: string | null;
  readonly usage: TokenUsage | null;
  readonly durationMs: number;
  readonly execution?: PhaseExecutionMetadata | null;
}

export interface InitializeRepoPromptsResult {
  readonly workspaceRoot: string;
  readonly configPath: string;
  readonly promptManifestPath: string;
  readonly promptSystemHashesPath: string;
  readonly createdFiles: readonly string[];
  readonly skippedFiles: readonly string[];
}

export interface RequestRegressionResult {
  readonly usId: string;
  readonly currentPhase: string;
  readonly status: string;
}

export interface RestartUserStoryResult {
  readonly usId: string;
  readonly currentPhase: string;
  readonly status: string;
  readonly generatedArtifactPath: string | null;
}

export interface ResetUserStoryResult {
  readonly usId: string;
  readonly currentPhase: string;
  readonly status: string;
  readonly deletedPaths: readonly string[];
  readonly preservedPaths: readonly string[];
}

export interface RewindWorkflowResult {
  readonly usId: string;
  readonly currentPhase: string;
  readonly status: string;
  readonly deletedPaths: readonly string[];
  readonly preservedPaths: readonly string[];
}

export interface WorkflowPhaseDetails {
  readonly phaseId: string;
  readonly title: string;
  readonly order: number;
  readonly requiresApproval: boolean;
  readonly expectsHumanIntervention: boolean;
  readonly isApproved: boolean;
  readonly isCurrent: boolean;
  readonly state: string;
  readonly artifactPath: string | null;
  readonly operationLogPath?: string | null;
  readonly executePromptPath: string | null;
  readonly approvePromptPath: string | null;
  readonly executeSystemPromptPath?: string | null;
  readonly approveSystemPromptPath?: string | null;
  readonly executionBoundary?: PhaseExecutionBoundarySummary | null;
  readonly captureRecord?: CaptureExecutionRecord | null;
  readonly executionReadiness?: PhaseExecutionReadiness | null;
  readonly latestExecutionInspection?: PhaseExecutionInspectionDetails | null;
}

export interface PhaseExecutionBoundarySummary {
  readonly boundaryKind: string;
  readonly isModelBacked: boolean;
  readonly summary: string;
}

export interface CaptureExecutionRecord {
  readonly actor: string;
  readonly createdAtUtc: string;
  readonly sourceKind: string;
  readonly sourceReference?: string | null;
  readonly materializedArtifacts: readonly string[];
}

export interface PhaseExecutionPromptSource {
  readonly role: string;
  readonly path: string;
  readonly isOverride: boolean;
  readonly contentSha256?: string | null;
  readonly embeddedContentSha256?: string | null;
}

export interface PhaseExecutionEffectivePrompt {
  readonly systemPrompt: string;
  readonly userPrompt: string;
  readonly warnings?: readonly string[] | null;
  readonly sourcePrompts?: readonly PhaseExecutionPromptSource[] | null;
}

export interface PhaseExecutionArtifactInput {
  readonly path: string;
  readonly sha256?: string | null;
  readonly phaseId?: string | null;
}

export interface PhaseExecutionEffectiveContext {
  readonly workspaceRoot: string;
  readonly userStoryPath: string;
  readonly workspaceGitHeadSha?: string | null;
  readonly previousArtifacts: readonly PhaseExecutionArtifactInput[];
  readonly contextFiles: readonly PhaseExecutionArtifactInput[];
  readonly currentArtifact?: PhaseExecutionArtifactInput | null;
  readonly operationPromptSha256?: string | null;
}

export interface PhaseExecutionInspectionDetails {
  readonly receiptPath?: string | null;
  readonly refinementPolicySnapshot?: RefinementPolicyDetails | null;
  readonly refinementSkillPreselection?: RefinementSkillPreselection | null;
  readonly refinementGraphScopeRequest?: RefinementGraphScopeRequest | null;
  readonly effectivePrompt?: PhaseExecutionEffectivePrompt | null;
  readonly effectiveContext?: PhaseExecutionEffectiveContext | null;
}

export interface PhaseExecutionRequirements {
  readonly modelExecutionRequired: boolean;
  readonly repositoryAccess: string;
  readonly workspaceWriteAccess: boolean;
}

export interface PhaseExecutionModelSecurity {
  readonly providerKind: string;
  readonly model: string;
  readonly profileName: string | null;
  readonly repositoryAccess: string;
  readonly nativeCliRequired: boolean;
  readonly nativeCliAvailable: boolean;
  readonly agentName?: string | null;
  readonly agentRole?: string | null;
}

export interface PhaseExecutionReadiness {
  readonly phaseId: string;
  readonly canExecute: boolean;
  readonly blockingReason: string | null;
  readonly requiredPermissions?: PhaseExecutionRequirements | null;
  readonly assignedModelSecurity?: PhaseExecutionModelSecurity | null;
  readonly validationMessage?: string | null;
}

export interface RefinementQuestionAnswerDetails {
  readonly index: number;
  readonly question: string;
  readonly answer: string | null;
}

export interface RefinementSessionDetails {
  readonly status: string;
  readonly tolerance: string;
  readonly reason: string | null;
  readonly items: readonly RefinementQuestionAnswerDetails[];
  readonly policy?: RefinementPolicyDetails | null;
}

export interface RefinementPolicyDetails {
  readonly tolerance: string;
  readonly pendingQuestionCount: number;
  readonly unansweredQuestionCount: number;
  readonly blockingConditions: readonly RefinementBlockingCondition[];
  readonly autoAnswer: RefinementAutoAnswerPolicy;
}

export interface RefinementBlockingCondition {
  readonly id: string;
  readonly description: string;
  readonly status: string;
  readonly isCurrentlyBlocking: boolean;
  readonly blockingReason?: string | null;
}

export interface RefinementAutoAnswerPolicy {
  readonly isEnabled: boolean;
  readonly mode: string;
  readonly summary: string;
  readonly profileName?: string | null;
  readonly agentName?: string | null;
  readonly agentRole?: string | null;
  readonly isCurrentlyEligible: boolean;
  readonly eligibilityStatus: string;
  readonly eligibilityReason?: string | null;
}

export interface RefinementSkillPreselection {
  readonly requiredSkills: readonly RefinementSkillSelectionItem[];
  readonly candidateSkills: readonly RefinementSkillSelectionItem[];
  readonly rejectedSkills: readonly RefinementSkillSelectionItem[];
  readonly contextGaps: readonly string[];
}

export interface RefinementSkillSelectionItem {
  readonly skillPath: string;
  readonly rationale: string;
}

export interface RefinementGraphScopeRequest {
  readonly depth: number;
  readonly seedNodes: readonly RefinementGraphSeedNode[];
  readonly seedFiles: readonly PhaseExecutionArtifactInput[];
  readonly unresolvedScopeQuestions: readonly string[];
}

export interface RefinementGraphSeedNode {
  readonly id: string;
  readonly label: string;
  readonly reason: string;
}

export interface ApprovalQuestionDetails {
  readonly index: number;
  readonly question: string;
  readonly status: string;
  readonly isResolved: boolean;
  readonly answer: string | null;
  readonly answeredBy: string | null;
  readonly answeredAtUtc: string | null;
}

export interface DecompositionChildDraft {
  readonly title: string;
  readonly objective: string;
  readonly acceptanceCriteria: readonly string[];
  readonly dependencies: readonly string[];
}

export interface DecompositionDetails {
  readonly state: string;
  readonly decision: string;
  readonly complexityScore: number;
  readonly threshold: number;
  readonly tolerance: number;
  readonly rationale: string;
  readonly artifactPath: string | null;
  readonly proposedChildren: readonly DecompositionChildDraft[];
  readonly createdChildUsIds: readonly string[];
}

export interface CurrentPhaseControls {
  readonly canContinue: boolean;
  readonly canApprove: boolean;
  readonly requiresApproval: boolean;
  readonly blockingReason: string | null;
  readonly canRestartFromSource: boolean;
  readonly regressionTargets: readonly string[];
  readonly rewindTargets?: readonly string[];
  readonly executionPhase?: string | null;
  readonly executionReadiness?: PhaseExecutionReadiness | null;
}

export interface TimelineEventDetails {
  readonly timestampUtc: string;
  readonly code: string;
  readonly actor: string | null;
  readonly phase: string | null;
  readonly summary: string | null;
  readonly artifacts: readonly string[];
  readonly usage: TokenUsage | null;
  readonly durationMs: number | null;
  readonly execution?: PhaseExecutionMetadata | null;
}

export interface PhaseIterationDetails {
  readonly iterationKey: string;
  readonly attempt: number;
  readonly phaseId: string;
  readonly timestampUtc: string;
  readonly code: string;
  readonly actor: string | null;
  readonly summary: string | null;
  readonly outputArtifactPath: string;
  readonly inputArtifactPath: string | null;
  readonly contextArtifactPaths: readonly string[];
  readonly operationLogPath: string | null;
  readonly operationPrompt: string | null;
  readonly usage: TokenUsage | null;
  readonly durationMs: number | null;
  readonly execution?: PhaseExecutionMetadata | null;
}

export interface UserStoryFileDetails {
  readonly name: string;
  readonly path: string;
}

export interface PullRequestDetails {
  readonly status: string;
  readonly title: string;
  readonly isDraft: boolean;
  readonly number: number | null;
  readonly url: string | null;
  readonly remoteBranch: string | null;
  readonly publishedAtUtc: string | null;
}

export interface UserStoryWorkflowDetails {
  readonly usId: string;
  readonly title: string;
  readonly kind?: string;
  readonly category: string;
  readonly tags?: readonly string[];
  readonly status: string;
  readonly currentPhase: string;
  readonly directoryPath: string;
  readonly workBranch: string | null;
  readonly mainArtifactPath: string;
  readonly timelinePath: string;
  readonly rawTimeline: string;
  readonly dependencies?: readonly UserStoryDependencySummary[];
  readonly workflowKind?: string;
  readonly parentUsId?: string | null;
  readonly childUserStories?: readonly UserStorySummary[];
  readonly decomposition?: DecompositionDetails | null;
  readonly pullRequest?: PullRequestDetails | null;
  readonly phases: readonly WorkflowPhaseDetails[];
  readonly controls: CurrentPhaseControls;
  readonly refinement: RefinementSessionDetails | null;
  readonly approvalQuestions?: readonly ApprovalQuestionDetails[];
  readonly events: readonly TimelineEventDetails[];
  readonly phaseIterations?: readonly PhaseIterationDetails[];
  readonly contextFilesDirectoryPath?: string;
  readonly contextFiles?: readonly UserStoryFileDetails[];
  readonly attachmentsDirectoryPath: string;
  readonly attachments: readonly UserStoryFileDetails[];
}

export interface SpecForgeBackendClient {
  listUserStories(visibility?: "active" | "dropped"): Promise<readonly UserStorySummary[]>;
  getUserStorySummary(usId: string): Promise<UserStorySummary>;
  getUserStoryWorkflow(usId: string): Promise<UserStoryWorkflowDetails>;
  getUserStoryRuntimeStatus(usId: string): Promise<UserStoryRuntimeStatus>;
  analyzeUserStoryLineage(usId: string): Promise<WorkflowLineageAnalysisResult>;
  repairUserStoryLineage(usId: string, actor?: string): Promise<WorkflowLineageRepairResult>;
  createUserStory(usId: string, title: string, kind: string, category: string, sourceText: string, actor?: string, tags?: readonly string[]): Promise<CreateOrImportUserStoryResult>;
  importUserStory(usId: string, sourcePath: string, title: string, kind: string, category: string, actor?: string, tags?: readonly string[]): Promise<CreateOrImportUserStoryResult>;
  initializeRepoPrompts(overwrite?: boolean): Promise<InitializeRepoPromptsResult>;
  exportPromptTemplate(promptPath: string, overwrite?: boolean): Promise<InitializeRepoPromptsResult>;
  continuePhase(usId: string, actor?: string): Promise<ContinuePhaseResult>;
  approveReviewAnyway(usId: string, reason: string, actor?: string): Promise<ContinuePhaseResult>;
  approveCurrentPhase(usId: string, baseBranch?: string, workBranch?: string, actor?: string): Promise<UserStorySummary>;
  requestRegression(usId: string, targetPhase: string, reason?: string, actor?: string, destructive?: boolean): Promise<RequestRegressionResult>;
  reopenCompletedWorkflow(usId: string, reasonKind: string, description: string, actor?: string): Promise<RequestRegressionResult>;
  restartUserStoryFromSource(usId: string, reason?: string, actor?: string): Promise<RestartUserStoryResult>;
  rewindWorkflow(usId: string, targetPhase: string, actor?: string, destructive?: boolean): Promise<RewindWorkflowResult>;
  resetUserStoryToCapture(usId: string): Promise<ResetUserStoryResult>;
  submitRefinementAnswers(usId: string, answers: readonly string[], actor?: string): Promise<void>;
  submitApprovalAnswer(usId: string, question: string, answer: string, actor?: string): Promise<SubmitApprovalAnswerResult>;
  suggestApprovalAnswer(usId: string, question: string, actor?: string): Promise<ApprovalAnswerSuggestionResult>;
  operateCurrentPhaseArtifact(
    usId: string,
    prompt: string,
    actor?: string,
    includeReviewArtifactInContext?: boolean
  ): Promise<OperateCurrentPhaseArtifactResult>;
  isBusy(): boolean;
  cancelActiveOperations(): void;
  dispose(): void;
}

export function createMcpBackendClient(
  workspaceRoot: string,
  hostRoot: string,
  settings: SpecForgeSettings
): SpecForgeBackendClient {
  return new StdioMcpBackendClient(workspaceRoot, hostRoot, settings);
}
