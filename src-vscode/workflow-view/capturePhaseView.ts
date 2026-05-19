import type { UserStoryWorkflowDetails, WorkflowPhaseDetails } from "../backendClient";
import type { PhaseSectionFragments } from "./models";

interface CapturePhaseViewArgs {
  readonly workflow: UserStoryWorkflowDetails;
  readonly selectedPhase: WorkflowPhaseDetails;
  readonly selectedArtifactContent: string | null;
  readonly artifactPreviewHtml: string | null;
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
  const { workflow, selectedPhase, selectedArtifactContent, artifactPreviewHtml, buildArtifactPreviewSection } = args;
  const captureSourcePath = selectedPhase.phaseId === "capture"
    ? workflow.mainArtifactPath
    : null;
  const boundarySection = selectedPhase.phaseId === "capture" && selectedPhase.executionBoundary
    ? `
      <section class="detail-card">
        <h3>Capture Boundary</h3>
        <p>${selectedPhase.executionBoundary.summary}</p>
        <div class="token-strip">
          <span class="token token--neutral">${selectedPhase.executionBoundary.boundaryKind}</span>
          <span class="token token--attention">non-model</span>
        </div>
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
    beforeArtifact: [boundarySection, captureSourceSection].filter(Boolean),
    afterArtifact: []
  };
}
