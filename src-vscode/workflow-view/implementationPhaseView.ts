import type { WorkflowPhaseDetails } from "../backendClient";
import type { PhaseSectionFragments } from "./models";

interface ImplementationPhaseViewArgs {
  readonly selectedPhase: WorkflowPhaseDetails;
  readonly escapeHtml: (value: string) => string;
  readonly escapeHtmlAttribute: (value: string) => string;
}

export function buildImplementationPhaseSections(args: ImplementationPhaseViewArgs): PhaseSectionFragments {
  const { selectedPhase, escapeHtml, escapeHtmlAttribute } = args;
  const executionReadiness = selectedPhase.executionReadiness ?? null;
  const implementationPolicySnapshot = selectedPhase.latestExecutionInspection?.implementationPolicySnapshot ?? null;
  const executionPolicy = implementationPolicySnapshot ?? selectedPhase.executionPolicy ?? null;
  const executionEnvelope = selectedPhase.executionEnvelope ?? null;
  const implementationStructuredEvidence = selectedPhase.latestExecutionInspection?.implementationStructuredEvidence ?? null;
  const effectivePrompt = selectedPhase.latestExecutionInspection?.effectivePrompt ?? null;
  const effectiveContext = selectedPhase.latestExecutionInspection?.effectiveContext ?? null;
  const evidenceRecord = selectedPhase.latestExecutionInspection?.evidenceRecord ?? null;
  const receiptPath = selectedPhase.latestExecutionInspection?.receiptPath?.trim() || null;

  const implementationInspectionSection = selectedPhase.phaseId === "implementation"
    ? `
      <section class="detail-card">
        <h3>Inspect Last Implementation Execution</h3>
        <p class="panel-copy">
          Review the latest persisted effective prompt and injected runtime context for implementation before changing prompt templates, retrying after review, or adjusting attached context files.
        </p>
        ${effectivePrompt || effectiveContext
          ? `
            <div class="detail-grid">
              <div><strong>Effective Prompt</strong><div><code>${effectivePrompt ? "available" : "unavailable"}</code></div></div>
              <div><strong>Warnings</strong><div><code>${effectivePrompt?.warnings?.length ?? 0}</code></div></div>
              <div><strong>Previous Artifacts</strong><div><code>${effectiveContext?.previousArtifacts.length ?? 0}</code></div></div>
              <div><strong>Context Files</strong><div><code>${effectiveContext?.contextFiles.length ?? 0}</code></div></div>
              <div><strong>Current Artifact</strong><div><code>${effectiveContext?.currentArtifact ? "available" : "unavailable"}</code></div></div>
              <div><strong>Evidence Links</strong><div><code>${evidenceRecord?.evidenceLinks.length ?? 0}</code></div></div>
            </div>
            <div class="detail-actions">
              ${effectivePrompt
                ? `<button class="workflow-action-button workflow-action-button--document" type="button" data-open-effective-prompt-modal>View Last Implementation Prompt</button>`
                : ""}
              ${effectiveContext
                ? `<button class="workflow-action-button workflow-action-button--document" type="button" data-open-effective-context-modal>View Last Implementation Context</button>`
                : ""}
              ${receiptPath
                ? `<button class="workflow-action-button workflow-action-button--document" data-command="openArtifact" data-path="${escapeHtmlAttribute(receiptPath)}">Open Receipt</button>`
                : ""}
            </div>
          `
          : `<p class="muted">No persisted implementation execution inspection is available yet for this user story.</p>`}
      </section>
    `
    : "";

  const implementationPolicySection = selectedPhase.phaseId === "implementation" && (executionReadiness || executionPolicy)
    ? `
      <section class="detail-card">
        <h3>Implementation Policy</h3>
        <p class="panel-copy">
          Inspect the explicit repository-access semantics, writable scope rules, forbidden mutation zones, and evidence requirements that govern implementation runs.
        </p>
        <div class="detail-grid">
          <div><strong>Execution Readiness</strong><div><code>${implementationPolicySnapshot ? (implementationPolicySnapshot.executionAllowed ? "ready" : "blocked") : executionReadiness?.canExecute ? "ready" : "blocked"}</code></div></div>
          <div><strong>Blocking Reason</strong><div><code>${escapeHtml(implementationPolicySnapshot?.executionBlockingReason ?? executionReadiness?.blockingReason ?? "none")}</code></div></div>
          <div><strong>Repository Access</strong><div><code>${escapeHtml(executionPolicy?.permissions.repositoryAccess ?? executionReadiness?.requiredPermissions?.repositoryAccess ?? "unknown")}</code></div></div>
          <div><strong>Workspace Writes</strong><div><code>${(executionPolicy?.permissions.workspaceWriteAccess ?? executionReadiness?.requiredPermissions?.workspaceWriteAccess) ? "allowed" : "not allowed"}</code></div></div>
          <div><strong>Writable Paths</strong><div><code>${executionPolicy?.writablePaths.length ?? 0}</code></div></div>
          <div><strong>Forbidden Paths</strong><div><code>${executionPolicy?.forbiddenPaths.length ?? 0}</code></div></div>
          <div><strong>Evidence Requirements</strong><div><code>${executionPolicy?.evidenceRequirements.length ?? 0}</code></div></div>
          <div><strong>Eligibility Rules</strong><div><code>${executionPolicy?.eligibilityRules.length ?? 0}</code></div></div>
        </div>
        ${implementationPolicySnapshot
          ? `<p class="muted">Showing the latest receipt-linked implementation policy snapshot that governed the persisted run.</p>`
          : ""}
        ${executionPolicy
          ? `
            <div class="refinement-suggestions">
              <div class="refinement-suggestion refinement-suggestion--static">
                <div class="refinement-suggestion__body">
                  <strong>Writable Scope</strong>
                  ${renderPathPolicyList(executionPolicy.writablePaths, escapeHtml, "No writable scope rules declared.")}
                </div>
              </div>
              <div class="refinement-suggestion refinement-suggestion--static">
                <div class="refinement-suggestion__body">
                  <strong>Forbidden Mutation Zones</strong>
                  ${renderPathPolicyList(executionPolicy.forbiddenPaths, escapeHtml, "No forbidden mutation zones declared.")}
                </div>
              </div>
              <div class="refinement-suggestion refinement-suggestion--static">
                <div class="refinement-suggestion__body">
                  <strong>Evidence Requirements</strong>
                  ${renderEvidenceRequirementList(executionPolicy.evidenceRequirements, escapeHtml)}
                </div>
              </div>
              <div class="refinement-suggestion refinement-suggestion--static">
                <div class="refinement-suggestion__body">
                  <strong>Eligibility Rules</strong>
                  ${renderEligibilityRuleList(executionPolicy.eligibilityRules, escapeHtml)}
                </div>
              </div>
            </div>
          `
          : ""}
      </section>
    `
    : "";

  const implementationEnvelopeSection = selectedPhase.phaseId === "implementation" && executionEnvelope
    ? `
      <section class="detail-card">
        <h3>Implementation Execution Envelope</h3>
        <p class="panel-copy">
          Inspect the declared execution mode, sandbox, tool permissions, write scopes, repository boundaries, and budget model that constrain implementation runs.
        </p>
        <div class="detail-grid">
          <div><strong>Execution Mode</strong><div><code>${escapeHtml(executionEnvelope.executionMode)}</code></div></div>
          <div><strong>Sandbox Mode</strong><div><code>${escapeHtml(executionEnvelope.sandboxMode)}</code></div></div>
          <div><strong>Tool Permissions</strong><div><code>${executionEnvelope.toolPermissions.length}</code></div></div>
          <div><strong>Write Scopes</strong><div><code>${executionEnvelope.writeScopes.length}</code></div></div>
          <div><strong>Repository Boundaries</strong><div><code>${executionEnvelope.repositoryBoundaries.length}</code></div></div>
          <div><strong>Compute Tier</strong><div><code>${escapeHtml(executionEnvelope.budget.computeTier)}</code></div></div>
          <div><strong>Token Budget</strong><div><code>${escapeHtml(executionEnvelope.budget.tokenBudget)}</code></div></div>
          <div><strong>Mutation Budget</strong><div><code>${escapeHtml(executionEnvelope.budget.mutationBudget)}</code></div></div>
        </div>
        <div class="refinement-suggestions">
          <div class="refinement-suggestion refinement-suggestion--static">
            <div class="refinement-suggestion__body">
              <strong>Tool Permissions</strong>
              ${renderEnvelopeToolPermissions(executionEnvelope.toolPermissions, escapeHtml)}
            </div>
          </div>
          <div class="refinement-suggestion refinement-suggestion--static">
            <div class="refinement-suggestion__body">
              <strong>Write Scopes</strong>
              ${renderEnvelopeWriteScopes(executionEnvelope.writeScopes, escapeHtml)}
            </div>
          </div>
          <div class="refinement-suggestion refinement-suggestion--static">
            <div class="refinement-suggestion__body">
              <strong>Repository Boundaries</strong>
              ${renderEnvelopeBoundaries(executionEnvelope.repositoryBoundaries, escapeHtml)}
            </div>
          </div>
          <div class="refinement-suggestion refinement-suggestion--static">
            <div class="refinement-suggestion__body">
              <strong>Budget Model</strong>
              <span>Time budget: <code>${escapeHtml(executionEnvelope.budget.timeBudget)}</code></span>
              <span>${escapeHtml(executionEnvelope.budget.notes)}</span>
            </div>
          </div>
        </div>
      </section>
    `
    : "";

  const implementationEvidenceSummarySection = selectedPhase.phaseId === "implementation" && (implementationStructuredEvidence || evidenceRecord)
    ? `
      <section class="detail-card">
        <h3>Implementation Evidence Summary</h3>
        <p class="panel-copy">
          The latest execution receipt persists implementation evidence separately; this summary shows whether the run produced the expected evidence substrate for downstream review.
        </p>
        <div class="detail-grid">
          <div><strong>Actor</strong><div><code>${escapeHtml(evidenceRecord?.actor.agentName ?? evidenceRecord?.actor.kind ?? "specforge-runtime")}</code></div></div>
          <div><strong>Touched Files</strong><div><code>${implementationStructuredEvidence?.touchedFiles.length ?? 0}</code></div></div>
          <div><strong>Evidence Links</strong><div><code>${evidenceRecord?.evidenceLinks.length ?? 0}</code></div></div>
          <div><strong>Graph References</strong><div><code>${implementationStructuredEvidence?.graphEvidence?.operationReferences.length ?? 0}</code></div></div>
          <div><strong>Validation</strong><div><code>${escapeHtml(evidenceRecord?.validationSummary.status ?? "captured")}</code></div></div>
          <div><strong>Generated At</strong><div><code>${escapeHtml(implementationStructuredEvidence?.generatedAtUtc ?? "unavailable")}</code></div></div>
        </div>
        ${implementationStructuredEvidence
          ? `
            <div class="refinement-suggestions">
              <div class="refinement-suggestion refinement-suggestion--static">
                <div class="refinement-suggestion__body">
                  <strong>Structured Summary</strong>
                  ${implementationStructuredEvidence.summary.length === 0
                    ? "<span>No structured implementation evidence summary was recorded.</span>"
                    : implementationStructuredEvidence.summary.map((item) => `<span>${escapeHtml(item)}</span>`).join("")}
                  <span>Evidence json: <code>${escapeHtml(implementationStructuredEvidence.evidenceJsonPath)}</code></span>
                  <span>Evidence markdown: <code>${escapeHtml(implementationStructuredEvidence.evidenceMarkdownPath)}</code></span>
                </div>
              </div>
              <div class="refinement-suggestion refinement-suggestion--static">
                <div class="refinement-suggestion__body">
                  <strong>Touched Files</strong>
                  ${renderImplementationTouchedFiles(implementationStructuredEvidence.touchedFiles, escapeHtml)}
                </div>
              </div>
              <div class="refinement-suggestion refinement-suggestion--static">
                <div class="refinement-suggestion__body">
                  <strong>Graph Scope References</strong>
                  ${renderImplementationGraphEvidence(implementationStructuredEvidence.graphEvidence, escapeHtml)}
                </div>
              </div>
            </div>
          `
          : ""}
      </section>
    `
    : "";

  return {
    beforeArtifact: [
      ...(implementationInspectionSection ? [implementationInspectionSection] : []),
      ...(implementationPolicySection ? [implementationPolicySection] : []),
      ...(implementationEnvelopeSection ? [implementationEnvelopeSection] : []),
      ...(implementationEvidenceSummarySection ? [implementationEvidenceSummarySection] : [])
    ],
    afterArtifact: []
  };
}

function renderImplementationTouchedFiles(
  items: readonly {
    readonly path: string;
    readonly changeKind: string;
    readonly baselineStatusCode?: string | null;
    readonly currentStatusCode: string;
  }[],
  escapeHtml: (value: string) => string
): string {
  if (items.length === 0) {
    return "<span>No repository touched files were captured for this implementation delta.</span>";
  }

  return items
    .map((item) => `<span><code>${escapeHtml(item.path)}</code> · kind <code>${escapeHtml(item.changeKind)}</code> · baseline <code>${escapeHtml(item.baselineStatusCode ?? "none")}</code> · current <code>${escapeHtml(item.currentStatusCode)}</code></span>`)
    .join("");
}

function renderImplementationGraphEvidence(
  graphEvidence: {
    readonly graphScopeRequestAvailable: boolean;
    readonly graphScopeRequestPath?: string | null;
    readonly impactGraphPath?: string | null;
    readonly impactGraphMetadataPath?: string | null;
    readonly impactSummaryPath?: string | null;
    readonly impactGraphState?: string | null;
    readonly operationReferences: readonly {
      readonly eventFamily: string;
      readonly requestedMode: string;
      readonly actualMode: string;
      readonly triggerSurface: string;
      readonly fallbackUsed: boolean;
      readonly latencyMs: number;
    }[];
    readonly warnings: readonly string[];
  } | null | undefined,
  escapeHtml: (value: string) => string
): string {
  if (!graphEvidence) {
    return "<span>No graph-backed scope references were recorded for this implementation evidence snapshot.</span>";
  }

  const lines = new Array<string>();
  lines.push(`<span>Graph scope request: <code>${graphEvidence.graphScopeRequestAvailable ? "available" : "missing"}</code>${graphEvidence.graphScopeRequestPath ? ` · <code>${escapeHtml(graphEvidence.graphScopeRequestPath)}</code>` : ""}</span>`);
  if (graphEvidence.impactGraphPath) {
    lines.push(`<span>Impact graph: <code>${escapeHtml(graphEvidence.impactGraphPath)}</code></span>`);
  }
  if (graphEvidence.impactGraphMetadataPath) {
    lines.push(`<span>Impact graph metadata: <code>${escapeHtml(graphEvidence.impactGraphMetadataPath)}</code>${graphEvidence.impactGraphState ? ` · state <code>${escapeHtml(graphEvidence.impactGraphState)}</code>` : ""}</span>`);
  }
  if (graphEvidence.impactSummaryPath) {
    lines.push(`<span>Impact summary: <code>${escapeHtml(graphEvidence.impactSummaryPath)}</code></span>`);
  }
  if (graphEvidence.operationReferences.length > 0) {
    lines.push(...graphEvidence.operationReferences.map((item) => `<span><code>${escapeHtml(item.eventFamily)}</code> · requested <code>${escapeHtml(item.requestedMode)}</code> · actual <code>${escapeHtml(item.actualMode)}</code> · surface <code>${escapeHtml(item.triggerSurface)}</code> · fallback <code>${item.fallbackUsed ? "true" : "false"}</code> · latency <code>${item.latencyMs} ms</code></span>`));
  }
  if (graphEvidence.warnings.length > 0) {
    lines.push(...graphEvidence.warnings.map((item) => `<span>${escapeHtml(item)}</span>`));
  }

  return lines.join("");
}

function renderPathPolicyList(
  items: readonly {
    readonly path: string;
    readonly access: string;
    readonly actor: string;
    readonly enforcement: string;
    readonly reason: string;
  }[],
  escapeHtml: (value: string) => string,
  emptyMessage: string
): string {
  if (items.length === 0) {
    return `<span>${escapeHtml(emptyMessage)}</span>`;
  }

  return items
    .map((item) => `<span><code>${escapeHtml(item.path)}</code> · <code>${escapeHtml(item.access)}</code> · actor <code>${escapeHtml(item.actor)}</code> · ${escapeHtml(item.reason)}</span>`)
    .join("");
}

function renderEvidenceRequirementList(
  items: readonly {
    readonly id: string;
    readonly description: string;
    readonly enforcement: string;
    readonly policyInput?: string | null;
  }[],
  escapeHtml: (value: string) => string
): string {
  if (items.length === 0) {
    return "<span>No evidence requirements declared.</span>";
  }

  return items
    .map((item) => `<span><code>${escapeHtml(item.id)}</code> · ${escapeHtml(item.description)} · enforcement <code>${escapeHtml(item.enforcement)}</code>${item.policyInput ? ` · input <code>${escapeHtml(item.policyInput)}</code>` : ""}</span>`)
    .join("");
}

function renderEligibilityRuleList(
  items: readonly {
    readonly id: string;
    readonly description: string;
    readonly enforcement: string;
    readonly blockingReason?: string | null;
    readonly isCurrentlySatisfied?: boolean | null;
    readonly currentStatusMessage?: string | null;
  }[],
  escapeHtml: (value: string) => string
): string {
  if (items.length === 0) {
    return "<span>No eligibility rules declared.</span>";
  }

  return items
    .map((item) => `<span><code>${escapeHtml(item.id)}</code> · ${escapeHtml(item.description)} · status <code>${item.isCurrentlySatisfied === false ? "blocked" : "ready"}</code>${item.currentStatusMessage ? ` · ${escapeHtml(item.currentStatusMessage)}` : ""}${item.blockingReason ? ` · reason <code>${escapeHtml(item.blockingReason)}</code>` : ""}</span>`)
    .join("");
}

function renderEnvelopeToolPermissions(
  items: readonly {
    readonly actor: string;
    readonly tool: string;
    readonly access: string;
    readonly enforcement: string;
  }[],
  escapeHtml: (value: string) => string
): string {
  if (items.length === 0) {
    return "<span>No tool permissions declared.</span>";
  }

  return items
    .map((item) => `<span>actor <code>${escapeHtml(item.actor)}</code> · tool <code>${escapeHtml(item.tool)}</code> · access <code>${escapeHtml(item.access)}</code> · enforcement <code>${escapeHtml(item.enforcement)}</code></span>`)
    .join("");
}

function renderEnvelopeWriteScopes(
  items: readonly {
    readonly actor: string;
    readonly path: string;
    readonly access: string;
    readonly enforcement: string;
  }[],
  escapeHtml: (value: string) => string
): string {
  if (items.length === 0) {
    return "<span>No write scopes declared.</span>";
  }

  return items
    .map((item) => `<span>actor <code>${escapeHtml(item.actor)}</code> · <code>${escapeHtml(item.path)}</code> · access <code>${escapeHtml(item.access)}</code> · enforcement <code>${escapeHtml(item.enforcement)}</code></span>`)
    .join("");
}

function renderEnvelopeBoundaries(
  items: readonly {
    readonly kind: string;
    readonly path: string;
    readonly access: string;
    readonly summary: string;
  }[],
  escapeHtml: (value: string) => string
): string {
  if (items.length === 0) {
    return "<span>No repository boundaries declared.</span>";
  }

  return items
    .map((item) => `<span>kind <code>${escapeHtml(item.kind)}</code> · <code>${escapeHtml(item.path)}</code> · access <code>${escapeHtml(item.access)}</code> · ${escapeHtml(item.summary)}</span>`)
    .join("");
}
