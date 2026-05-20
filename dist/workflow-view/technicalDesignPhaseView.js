"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.buildTechnicalDesignPhaseSections = buildTechnicalDesignPhaseSections;
function buildTechnicalDesignPhaseSections(args) {
    const { workflow, selectedPhase, escapeHtml, escapeHtmlAttribute } = args;
    const effectivePrompt = selectedPhase.latestExecutionInspection?.effectivePrompt ?? null;
    const effectiveContext = selectedPhase.latestExecutionInspection?.effectiveContext ?? null;
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
    return {
        beforeArtifact: technicalDesignInspectionSection ? [technicalDesignInspectionSection] : [],
        afterArtifact: []
    };
}
//# sourceMappingURL=technicalDesignPhaseView.js.map