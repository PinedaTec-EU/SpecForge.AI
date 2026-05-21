import type { WorkflowPhaseDetails } from "../backendClient";
import type { PhaseSectionFragments } from "./models";

type ReleaseApprovalPhaseViewArgs = {
  readonly selectedPhase: WorkflowPhaseDetails;
  readonly escapeHtml: (value: string) => string;
  readonly escapeHtmlAttribute: (value: string) => string;
};

export function buildReleaseApprovalPhaseSections(args: ReleaseApprovalPhaseViewArgs): PhaseSectionFragments {
  const effectivePrompt = args.selectedPhase.latestExecutionInspection?.effectivePrompt ?? null;
  const effectiveContext = args.selectedPhase.latestExecutionInspection?.effectiveContext ?? null;
  const evidenceRecord = args.selectedPhase.latestExecutionInspection?.evidenceRecord ?? null;
  const receiptPath = args.selectedPhase.latestExecutionInspection?.receiptPath?.trim() || null;

  const releaseApprovalInspectionSection = args.selectedPhase.phaseId === "release-approval"
    ? `
      <section class="detail-card">
        <h3>Inspect Last Release Approval Execution</h3>
        <p class="panel-copy">
          Review the latest persisted effective prompt and injected runtime context for release approval before changing approval posture or advancing toward PR preparation.
        </p>
        ${effectivePrompt || effectiveContext
          ? `
            <div class="detail-grid">
              <div><strong>Effective Prompt</strong><div><code>${effectivePrompt ? "available" : "unavailable"}</code></div></div>
              <div><strong>Warnings</strong><div><code>${effectivePrompt?.warnings?.length ?? 0}</code></div></div>
              <div><strong>Previous Artifacts</strong><div><code>${effectiveContext?.previousArtifacts.length ?? 0}</code></div></div>
              <div><strong>Context Files</strong><div><code>${effectiveContext?.contextFiles.length ?? 0}</code></div></div>
              <div><strong>Branch Context</strong><div><code>${effectiveContext?.contextFiles.some(item => item.path.endsWith("/branch.yaml")) ? "available" : "unavailable"}</code></div></div>
              <div><strong>Timeline Context</strong><div><code>${effectiveContext?.contextFiles.some(item => item.path.endsWith("/timeline.md")) ? "available" : "unavailable"}</code></div></div>
              <div><strong>Evidence Links</strong><div><code>${evidenceRecord?.evidenceLinks.length ?? 0}</code></div></div>
            </div>
            <div class="detail-actions">
              ${effectivePrompt
                ? `<button class="workflow-action-button workflow-action-button--document" type="button" data-open-effective-prompt-modal>View Last Release Approval Prompt</button>`
                : ""}
              ${effectiveContext
                ? `<button class="workflow-action-button workflow-action-button--document" type="button" data-open-effective-context-modal>View Last Release Approval Context</button>`
                : ""}
              ${receiptPath
                ? `<button class="workflow-action-button workflow-action-button--document" data-command="openArtifact" data-path="${args.escapeHtmlAttribute(receiptPath)}">Open Receipt</button>`
                : ""}
            </div>
          `
          : `<p class="muted">No persisted release-approval execution inspection is available yet for this user story.</p>`}
      </section>
    `
    : "";

  return {
    beforeArtifact: releaseApprovalInspectionSection ? [releaseApprovalInspectionSection] : [],
    afterArtifact: []
  };
}
