import type { UserStoryWorkflowDetails, WorkflowPhaseDetails } from "../backendClient";
import type { PhaseSectionFragments } from "./models";

interface CapturePhaseViewArgs {
  readonly workflow: UserStoryWorkflowDetails;
  readonly selectedPhase: WorkflowPhaseDetails;
  readonly selectedArtifactContent: string | null;
  readonly artifactPreviewHtml: string | null;
  readonly escapeHtml: (value: string) => string;
  readonly buildArtifactPreviewSection: (
    artifactPath: string,
    artifactPreviewHtml: string | null,
    artifactContent: string,
    options?: {
      readonly rawArtifact?: boolean;
      readonly footerNote?: string;
    }
  ) => string;
}

export function buildCapturePhaseSections(args: CapturePhaseViewArgs): PhaseSectionFragments {
  const { workflow, selectedPhase, selectedArtifactContent, artifactPreviewHtml, escapeHtml, buildArtifactPreviewSection } = args;
  const captureSourcePath = selectedPhase.phaseId === "capture"
    ? workflow.mainArtifactPath
    : null;
  const boundarySection = selectedPhase.phaseId === "capture" && selectedPhase.executionBoundary
    ? `
      <section class="detail-card">
        <h3>Capture Boundary</h3>
        <p>${escapeHtml(selectedPhase.executionBoundary.summary)}</p>
        <div class="token-strip">
          <span class="token token--neutral">${escapeHtml(selectedPhase.executionBoundary.boundaryKind)}</span>
          <span class="token token--attention">non-model</span>
        </div>
      </section>
    `
    : "";
  const captureRecordSection = selectedPhase.phaseId === "capture" && selectedPhase.captureRecord
    ? `
      <section class="detail-card">
        <h3>Capture Record</h3>
        <div class="detail-grid">
          <div><strong>Actor</strong><div><code>${escapeHtml(selectedPhase.captureRecord.actor)}</code></div></div>
          <div><strong>Created</strong><div><code>${escapeHtml(selectedPhase.captureRecord.createdAtUtc)}</code></div></div>
          <div><strong>Source</strong><div><code>${escapeHtml(selectedPhase.captureRecord.sourceKind)}</code></div></div>
          <div><strong>Source Reference</strong><div><code>${escapeHtml(selectedPhase.captureRecord.sourceReference ?? "n/a")}</code></div></div>
        </div>
        <h4>Materialized Artifacts</h4>
        <ul class="detail-list">
          ${selectedPhase.captureRecord.materializedArtifacts.map((path) => `<li><code>${escapeHtml(path)}</code></li>`).join("")}
        </ul>
      </section>
    `
    : "";
  const captureSourceSection = captureSourcePath
    ? `
      <section class="detail-card">
        <h3>User Story Source</h3>
        ${buildArtifactPreviewSection(
          captureSourcePath,
          artifactPreviewHtml,
          selectedArtifactContent ?? "Artifact content unavailable."
        )}
      </section>
    `
    : "";

  return {
    beforeArtifact: [boundarySection, captureRecordSection, captureSourceSection].filter(Boolean),
    afterArtifact: []
  };
}
