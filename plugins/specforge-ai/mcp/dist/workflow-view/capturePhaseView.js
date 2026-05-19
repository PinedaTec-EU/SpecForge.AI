"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.buildCapturePhaseSections = buildCapturePhaseSections;
function buildCapturePhaseSections(args) {
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
        ${buildArtifactPreviewSection(captureSourcePath, artifactPreviewHtml, selectedArtifactContent ?? "Artifact content unavailable.")}
      </section>
    `
        : "";
    return {
        beforeArtifact: [boundarySection, captureSourceSection].filter(Boolean),
        afterArtifact: []
    };
}
//# sourceMappingURL=capturePhaseView.js.map