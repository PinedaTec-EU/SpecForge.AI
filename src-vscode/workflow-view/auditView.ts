import type { UserStoryWorkflowDetails } from "../backendClient";
import { escapeHtml, escapeHtmlAttr as escapeHtmlAttribute } from "../htmlEscape";
import { buildWebviewTypographyRootCss } from "../webviewTypography";
import { workflowPhaseIcon } from "./icons";
import { findConfiguredModelForProfile, formatExecutionLabel } from "./executionLabels";
import { formatDuration, formatMetricNumber, formatTokensPerSecond } from "./metricFormatters";
import type { WorkflowViewState } from "./models";

function auditPhaseClassName(phaseId: string | null): string | null {
  if (!phaseId) {
    return null;
  }

  const normalizedPhaseId = phaseId.toLowerCase().replace(/[^a-z0-9_-]+/g, "-").replace(/^-+|-+$/g, "");

  return normalizedPhaseId ? `audit-row--${normalizedPhaseId}` : null;
}

function buildWorkflowAuditRowsHtml(
  workflow: UserStoryWorkflowDetails,
  state: WorkflowViewState
): string {
  return workflow.events.length > 0
    ? workflow.events.map((event) => {
      const phaseClassName = auditPhaseClassName(event.phase);
      const phaseIcon = event.phase
        ? `
          <span class="audit-row__phase-icon" aria-hidden="true">
            ${workflowPhaseIcon(event.phase)}
          </span>
        `
        : "";
      const executionLabel = formatExecutionLabel(event.execution, {
        actor: event.actor,
        configuredModel: findConfiguredModelForProfile(state, event.execution?.profileName)
      });
      const badges = [
        event.actor ? `<span class="badge">${escapeHtml(event.actor)}</span>` : "",
        event.phase ? `<span class="badge">${escapeHtml(event.phase)}</span>` : "",
        executionLabel ? `<span class="badge">model ${escapeHtml(executionLabel)}</span>` : "",
        event.usage ? `<span class="badge">in/out ${escapeHtml(`${formatMetricNumber(event.usage.inputTokens)}/${formatMetricNumber(event.usage.outputTokens)}`)}</span>` : "",
        event.usage ? `<span class="badge">total ${escapeHtml(formatMetricNumber(event.usage.totalTokens))}</span>` : "",
        event.durationMs !== null ? `<span class="badge">${escapeHtml(formatDuration(event.durationMs))}</span>` : "",
        event.usage && event.durationMs !== null ? `<span class="badge">${escapeHtml(formatTokensPerSecond(event.usage.outputTokens, event.durationMs))}</span>` : ""
      ].filter((badge) => badge.length > 0).join("");
      return `
      <div class="audit-row${phaseClassName ? ` ${escapeHtmlAttribute(phaseClassName)}` : ""}">
        ${phaseIcon}
        <div class="audit-row__content">
          <div class="audit-head">
            <span class="audit-head__title">${escapeHtml(event.timestampUtc)} · ${escapeHtml(event.code)}</span>
            ${badges.length > 0 ? `<div class="audit-head__meta">${badges}</div>` : ""}
          </div>
          <div class="audit-body">${escapeHtml(event.summary ?? "")}</div>
        </div>
      </div>
    `;
    }).join("")
    : `<pre class="audit-log">${escapeHtml(workflow.rawTimeline)}</pre>`;
}

function createWebviewNonce(): string {
  const alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
  let nonce = "";
  for (let index = 0; index < 32; index += 1) {
    nonce += alphabet[Math.floor(Math.random() * alphabet.length)];
  }
  return nonce;
}

export function buildWorkflowAuditHtml(
  workflow: UserStoryWorkflowDetails,
  state: WorkflowViewState,
  typographyCssVars = "",
  cspSource = ""
): string {
  const scriptNonce = createWebviewNonce();
  const cspMeta = cspSource.trim().length > 0
    ? `<meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src ${escapeHtmlAttribute(cspSource)} 'unsafe-inline'; script-src 'nonce-${scriptNonce}';">`
    : "";
  const auditRows = buildWorkflowAuditRowsHtml(workflow, state);
  return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  ${cspMeta}
  <style>
    :root {
      ${buildWebviewTypographyRootCss(typographyCssVars)}
    }
    * {
      box-sizing: border-box;
    }
    body {
      margin: 0;
      min-height: 100vh;
      height: 100vh;
      overflow: hidden;
      color: var(--vscode-editor-foreground);
      background:
        radial-gradient(circle at 8% 10%, rgba(114, 241, 184, 0.08), transparent 20%),
        radial-gradient(circle at 88% 18%, rgba(72, 131, 255, 0.09), transparent 24%),
        linear-gradient(180deg, rgba(10, 20, 24, 0.96), rgba(10, 14, 20, 1));
    }
    .audit-stream {
      display: flex;
      flex-direction: column;
      gap: 12px;
      min-height: 100vh;
      height: 100vh;
      overflow: auto;
      padding: 12px;
    }
    .audit-row {
      display: grid;
      grid-template-columns: 46px minmax(0, 1fr);
      gap: 10px;
      align-items: start;
      --audit-phase-start: #39d7d6;
      --audit-phase-end: #2564ff;
      --audit-phase-glow: rgba(28, 106, 255, 0.24);
      --audit-phase-border: rgba(28, 106, 255, 0.28);
      --audit-phase-wash: rgba(28, 106, 255, 0.12);
      padding: 14px 16px;
      border-radius: 16px;
      border: 1px solid var(--audit-phase-border);
      background:
        linear-gradient(90deg, var(--audit-phase-wash), transparent 28%),
        linear-gradient(180deg, rgba(255, 255, 255, 0.025), rgba(255, 255, 255, 0.01)),
        rgba(12, 18, 24, 0.92);
    }
    .audit-row__phase-icon {
      position: relative;
      width: 42px;
      height: 42px;
      border-radius: 14px;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      color: #fff;
      background:
        linear-gradient(180deg, rgba(255, 255, 255, 0.32), rgba(255, 255, 255, 0.08) 24%, rgba(255, 255, 255, 0) 100%),
        linear-gradient(145deg, var(--audit-phase-start), var(--audit-phase-end));
      border: 1px solid rgba(255, 255, 255, 0.28);
      box-shadow:
        inset 0 1px 0 rgba(255, 255, 255, 0.24),
        inset 0 -7px 16px rgba(0, 0, 0, 0.16),
        0 12px 20px var(--audit-phase-glow);
      overflow: hidden;
    }
    .audit-row__phase-icon::before {
      content: "";
      position: absolute;
      inset: 0;
      background:
        radial-gradient(circle at 30% 18%, rgba(255, 255, 255, 0.34), transparent 36%),
        linear-gradient(180deg, rgba(255, 255, 255, 0.14), transparent 38%);
      pointer-events: none;
    }
    .audit-row__phase-icon svg {
      position: relative;
      z-index: 1;
      width: 23px;
      height: 23px;
      fill: currentColor;
      filter: drop-shadow(0 3px 6px rgba(0, 0, 0, 0.18));
    }
    .audit-row__content {
      min-width: 0;
    }
    .audit-row--capture {
      --audit-phase-start: #23d0c7;
      --audit-phase-end: #1987ff;
      --audit-phase-glow: rgba(28, 106, 255, 0.24);
      --audit-phase-border: rgba(28, 106, 255, 0.28);
      --audit-phase-wash: rgba(28, 106, 255, 0.12);
    }
    .audit-row--refinement {
      --audit-phase-start: #4de1d6;
      --audit-phase-end: #3978ff;
      --audit-phase-glow: rgba(38, 118, 255, 0.24);
      --audit-phase-border: rgba(38, 118, 255, 0.28);
      --audit-phase-wash: rgba(38, 118, 255, 0.12);
    }
    .audit-row--spec {
      --audit-phase-start: #47dfb6;
      --audit-phase-end: #12aa72;
      --audit-phase-glow: rgba(20, 150, 95, 0.22);
      --audit-phase-border: rgba(20, 150, 95, 0.28);
      --audit-phase-wash: rgba(20, 150, 95, 0.12);
    }
    .audit-row--technical-design {
      --audit-phase-start: #78c8ff;
      --audit-phase-end: #4562ff;
      --audit-phase-glow: rgba(52, 92, 255, 0.22);
      --audit-phase-border: rgba(52, 92, 255, 0.28);
      --audit-phase-wash: rgba(52, 92, 255, 0.12);
    }
    .audit-row--implementation {
      --audit-phase-start: #8e78ff;
      --audit-phase-end: #4568ff;
      --audit-phase-glow: rgba(72, 88, 255, 0.22);
      --audit-phase-border: rgba(72, 88, 255, 0.28);
      --audit-phase-wash: rgba(72, 88, 255, 0.12);
    }
    .audit-row--review {
      --audit-phase-start: #58b9ff;
      --audit-phase-end: #2462d9;
      --audit-phase-glow: rgba(36, 98, 217, 0.22);
      --audit-phase-border: rgba(36, 98, 217, 0.28);
      --audit-phase-wash: rgba(36, 98, 217, 0.12);
    }
    .audit-row--release-approval {
      --audit-phase-start: #4cdbb6;
      --audit-phase-end: #1aaf8d;
      --audit-phase-glow: rgba(20, 150, 95, 0.22);
      --audit-phase-border: rgba(20, 150, 95, 0.28);
      --audit-phase-wash: rgba(20, 150, 95, 0.12);
    }
    .audit-row--pr-preparation {
      --audit-phase-start: #73d6ff;
      --audit-phase-end: #2588f7;
      --audit-phase-glow: rgba(37, 136, 247, 0.22);
      --audit-phase-border: rgba(37, 136, 247, 0.28);
      --audit-phase-wash: rgba(37, 136, 247, 0.12);
    }
    .audit-row--completed {
      --audit-phase-start: #b578ff;
      --audit-phase-end: #6a47ff;
      --audit-phase-glow: rgba(96, 58, 182, 0.24);
      --audit-phase-border: rgba(96, 58, 182, 0.28);
      --audit-phase-wash: rgba(96, 58, 182, 0.12);
    }
    .audit-head {
      display: flex;
      justify-content: space-between;
      gap: 12px;
      align-items: flex-start;
      flex-wrap: wrap;
      font-size: 0.82rem;
      color: rgba(255, 255, 255, 0.74);
    }
    .audit-head__title {
      padding-top: 6px;
    }
    .audit-head__meta {
      display: flex;
      gap: 8px;
      flex-wrap: wrap;
      justify-content: flex-end;
    }
    .audit-body {
      font-size: 0.92rem;
      line-height: 1.5;
      white-space: pre-wrap;
    }
    .audit-log {
      margin: 0;
      min-height: 100%;
      padding: 14px 16px;
      border-radius: 16px;
      border: 1px solid rgba(255, 255, 255, 0.06);
      background: rgba(0, 0, 0, 0.24);
      overflow: auto;
      font-size: 0.84rem;
      line-height: 1.55;
      white-space: pre-wrap;
      word-break: break-word;
    }
    .badge {
      border-radius: 999px;
      padding: 6px 12px;
      font-size: 0.78rem;
      background: rgba(255, 255, 255, 0.06);
      color: rgba(255, 255, 255, 0.9);
      border: 1px solid rgba(255, 255, 255, 0.06);
      backdrop-filter: blur(8px);
    }
  </style>
</head>
<body>
  <div class="audit-stream" data-audit-stream>${auditRows}</div>
  <script nonce="${scriptNonce}">
    const auditStream = document.querySelector("[data-audit-stream]");
    const scrollAuditStreamToLatest = () => {
      if (!(auditStream instanceof HTMLElement)) {
        return;
      }

      auditStream.scrollTop = auditStream.scrollHeight;
    };

    window.requestAnimationFrame(() => scrollAuditStreamToLatest());
    window.setTimeout(() => scrollAuditStreamToLatest(), 60);
  </script>
</body>
</html>`;
}
