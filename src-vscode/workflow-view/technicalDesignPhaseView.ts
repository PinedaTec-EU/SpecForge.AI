import type { UserStoryWorkflowDetails, WorkflowPhaseDetails } from "../backendClient";
import type { PhaseSectionFragments } from "./models";

interface TechnicalDesignPhaseViewArgs {
  readonly workflow: UserStoryWorkflowDetails;
  readonly selectedPhase: WorkflowPhaseDetails;
  readonly escapeHtml: (value: string) => string;
  readonly escapeHtmlAttribute: (value: string) => string;
}

export function buildTechnicalDesignPhaseSections(args: TechnicalDesignPhaseViewArgs): PhaseSectionFragments {
  const { workflow, selectedPhase, escapeHtml, escapeHtmlAttribute } = args;
  const effectivePrompt = selectedPhase.latestExecutionInspection?.effectivePrompt ?? null;
  const effectiveContext = selectedPhase.latestExecutionInspection?.effectiveContext ?? null;
  const evidenceRecord = selectedPhase.latestExecutionInspection?.evidenceRecord ?? null;
  const receiptPath = selectedPhase.latestExecutionInspection?.receiptPath?.trim() || null;

  const technicalDesignInspectionSection = selectedPhase.phaseId === "technical-design"
    ? `
      <section class="detail-card">
        <h3>Inspect Last Technical Design Execution</h3>
        <p class="panel-copy">
          Review the latest persisted technical-design prompt and injected context before adjusting design prompts, context files, or downstream implementation expectations.
        </p>
        ${effectivePrompt || effectiveContext
          ? `
            <div class="detail-grid">
              <div><strong>Effective Prompt</strong><div><code>${effectivePrompt ? "available" : "unavailable"}</code></div></div>
              <div><strong>Warnings</strong><div><code>${effectivePrompt?.warnings?.length ?? 0}</code></div></div>
              <div><strong>Previous Artifacts</strong><div><code>${effectiveContext?.previousArtifacts.length ?? 0}</code></div></div>
              <div><strong>Context Files</strong><div><code>${effectiveContext?.contextFiles.length ?? 0}</code></div></div>
              <div><strong>Current Artifact</strong><div><code>${effectiveContext?.currentArtifact ? "available" : "unavailable"}</code></div></div>
              <div><strong>Workspace Git HEAD</strong><div><code>${escapeHtml(effectiveContext?.workspaceGitHeadSha ?? "unavailable")}</code></div></div>
            </div>
            <div class="detail-actions">
              ${effectivePrompt
                ? `<button class="workflow-action-button workflow-action-button--document" type="button" data-open-effective-prompt-modal>View Last Technical Design Prompt</button>`
                : ""}
              ${effectiveContext
                ? `<button class="workflow-action-button workflow-action-button--document" type="button" data-open-effective-context-modal>View Last Technical Design Context</button>`
                : ""}
              ${receiptPath
                ? `<button class="workflow-action-button workflow-action-button--document" data-command="openArtifact" data-path="${escapeHtmlAttribute(receiptPath)}">Open Receipt</button>`
                : ""}
            </div>
            <div class="detail-grid">
              <div>
                <strong>User Story</strong>
                <div><code>${escapeHtml(workflow.usId)}</code></div>
              </div>
              <div>
                <strong>User Story Artifact</strong>
                <div><code>${escapeHtml(effectiveContext?.userStoryPath ?? workflow.mainArtifactPath)}</code></div>
              </div>
            </div>
          `
          : `<p class="muted">No persisted technical-design execution inspection is available yet for this user story.</p>`}
      </section>
    `
    : "";

  const technicalDesignEvidenceSection = selectedPhase.phaseId === "technical-design" && evidenceRecord
    ? `
      <section class="detail-card">
        <h3>Design Evidence Record</h3>
        <p class="panel-copy">
          This is the structured evidence summary persisted with the latest technical-design receipt: inputs, outputs, orchestration settings, tools used, and validation signals.
        </p>
        <div class="detail-grid">
          <div><strong>Actor</strong><div><code>${escapeHtml(evidenceRecord.actor.agentName ?? evidenceRecord.actor.kind)}</code></div></div>
          <div><strong>Model</strong><div><code>${escapeHtml(evidenceRecord.actor.model ?? evidenceRecord.actor.providerKind ?? "runtime-managed")}</code></div></div>
          <div><strong>Inputs</strong><div><code>${evidenceRecord.inputs.length}</code></div></div>
          <div><strong>Outputs</strong><div><code>${evidenceRecord.outputs.length}</code></div></div>
          <div><strong>Tools Used</strong><div><code>${evidenceRecord.toolsUsed.length}</code></div></div>
          <div><strong>Validation</strong><div><code>${escapeHtml(evidenceRecord.validationSummary.status)}</code></div></div>
        </div>
        <div class="refinement-suggestions">
          <div class="refinement-suggestion refinement-suggestion--static">
            <div class="refinement-suggestion__body">
              <strong>Inputs</strong>
              ${renderEvidenceReferenceList(evidenceRecord.inputs, escapeHtml)}
            </div>
          </div>
          <div class="refinement-suggestion refinement-suggestion--static">
            <div class="refinement-suggestion__body">
              <strong>Outputs</strong>
              ${renderEvidenceReferenceList(evidenceRecord.outputs, escapeHtml)}
            </div>
          </div>
          <div class="refinement-suggestion refinement-suggestion--static">
            <div class="refinement-suggestion__body">
              <strong>Orchestration Metadata</strong>
              ${renderEvidenceSettings(evidenceRecord.settings, escapeHtml)}
            </div>
          </div>
          <div class="refinement-suggestion refinement-suggestion--static">
            <div class="refinement-suggestion__body">
              <strong>Validation Summary</strong>
              <span>${escapeHtml(evidenceRecord.validationSummary.summary)}</span>
              <span>Checks: <code>${escapeHtml(evidenceRecord.validationSummary.checks.join(", ") || "none")}</code></span>
              ${evidenceRecord.blockingReason ? `<span>Blocking reason: <code>${escapeHtml(evidenceRecord.blockingReason)}</code></span>` : ""}
            </div>
          </div>
        </div>
      </section>
    `
    : "";

  return {
    beforeArtifact: [
      ...(technicalDesignInspectionSection ? [technicalDesignInspectionSection] : []),
      ...(technicalDesignEvidenceSection ? [technicalDesignEvidenceSection] : [])
    ],
    afterArtifact: []
  };
}

function renderEvidenceReferenceList(
  items: readonly {
    readonly kind: string;
    readonly path: string;
    readonly sha256?: string | null;
    readonly phaseId?: string | null;
  }[],
  escapeHtml: (value: string) => string
): string {
  if (items.length === 0) {
    return "<span>No evidence references recorded.</span>";
  }

  return items
    .map((item) =>
      `<span><code>${escapeHtml(item.kind)}</code>${item.phaseId ? ` · <code>${escapeHtml(item.phaseId)}</code>` : ""} · ${escapeHtml(item.path)}${item.sha256 ? ` · sha256 <code>${escapeHtml(item.sha256)}</code>` : ""}</span>`)
    .join("");
}

function renderEvidenceSettings(
  items: readonly {
    readonly name: string;
    readonly value: string;
  }[],
  escapeHtml: (value: string) => string
): string {
  if (items.length === 0) {
    return "<span>No orchestration settings recorded.</span>";
  }

  return items
    .map((item) => `<span><code>${escapeHtml(item.name)}</code>: ${escapeHtml(item.value)}</span>`)
    .join("");
}
