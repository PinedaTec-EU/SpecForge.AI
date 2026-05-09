#!/usr/bin/env node
"use strict";

const fs = require("node:fs");
const { buildWorkflowHtml } = require("../dist/workflowView");

const payload = JSON.parse(fs.readFileSync(0, "utf8"));
const workflow = payload.workflow;
const state = {
  selectedPhaseId: payload.selectedPhaseId ?? workflow.currentPhase,
  selectedArtifactContent: payload.selectedArtifactContent ?? null,
  selectedOperationContent: payload.selectedOperationContent ?? null,
  contextSuggestions: [],
  settingsConfigured: true,
  settingsMessage: null,
  executionSettingsPending: false,
  executionSettingsPendingMessage: null,
  maxImplementationReviewCycles: 5,
  completedUsLockOnCompleted: false,
  visualTimelineEnabled: false,
  debugMode: false,
  requireExplicitApprovalBranchAcceptance: false,
  graphLayoutMode: "vertical",
  graphInitialZoomMode: "fit-width",
  workflowGraphLayout: null
};

const browserShim = `
<script>
  window.__specForgeVsCodeApi = window.__specForgeVsCodeApi || {
    getState() {
      try {
        const state = JSON.parse(sessionStorage.getItem("specforge.workflow.state") || "{}");
        if (sessionStorage.getItem("specforge.workflow.userViewport") !== "true") {
          delete state.graphScrollTop;
          delete state.graphScrollLeft;
          delete state.graphStageOffsetX;
          delete state.graphStageOffsetY;
          delete state.graphInitialZoomMode;
          delete state.graphZoomMode;
          delete state.graphZoomScale;
        }

        return state;
      }
      catch { return {}; }
    },
    setState(value) {
      try { sessionStorage.setItem("specforge.workflow.state", JSON.stringify(value || {})); }
      catch {}
    },
    postMessage(message) {
      if (message?.command === "selectPhase" && message.phaseId) {
        const url = new URL(window.location.href);
        url.searchParams.set("selectedPhaseId", message.phaseId);
        window.location.href = url.toString();
        return;
      }

      if (message?.command === "suggestApprovalAnswer" && message.question) {
        fetch("/api/suggest-approval-answer", {
          method: "POST",
          headers: { "content-type": "application/json" },
          body: JSON.stringify({ question: message.question, actor: "cli-user" })
        })
          .then(response => response.ok ? response.json() : response.text().then(text => Promise.reject(new Error(text))))
          .then(result => {
            try {
              const state = JSON.parse(sessionStorage.getItem("specforge.workflow.state") || "{}");
              state.approvalAnswerDrafts = {
                ...(state.approvalAnswerDrafts || {}),
                [String(message.index)]: result.answer || ""
              };
              sessionStorage.setItem("specforge.workflow.state", JSON.stringify(state));
            } catch {}
            window.postMessage({
              command: "approvalAnswerSuggested",
              index: message.index,
              question: result.question,
              answer: result.answer || ""
            }, "*");
          })
          .catch(error => {
            window.postMessage({
              command: "approvalAnswerSuggested",
              index: message.index,
              question: message.question,
              answer: error instanceof Error ? error.message : String(error)
            }, "*");
          });
        return;
      }

      window.dispatchEvent(new CustomEvent("specforge-cli-command", { detail: message }));
    }
  };
  window.addEventListener("pointerdown", event => {
    if (event.target?.closest?.('[data-panel-scroll="graph"], [data-graph-zoom-in], [data-graph-zoom-out], [data-graph-fit-width], [data-graph-auto-fit]')) {
      sessionStorage.setItem("specforge.workflow.userViewport", "true");
    }
  }, true);
  window.addEventListener("wheel", event => {
    if (event.target?.closest?.('[data-panel-scroll="graph"]')) {
      sessionStorage.setItem("specforge.workflow.userViewport", "true");
    }
  }, { capture: true, passive: true });
</script>`;

const refreshShim = `
<script>
  (() => {
    let signature = ${JSON.stringify(payload.signature)};
    async function poll() {
      try {
        const response = await fetch("/api/workflow-signature", { cache: "no-store" });
        if (!response.ok) return;
        const next = await response.text();
        if (signature && next && next !== signature) {
          window.location.reload();
        }
      } catch {}
    }
    window.addEventListener("specforge-cli-command", event => {
      const command = event.detail?.command;
      if (command === "webviewReady" || command === "webviewDispatch" || command === "webviewClientError") {
        return;
      }
      if (command === "continue" || command === "approve" || command === "play" || command === "pause" || command === "stop") {
        poll();
      }
    });
    window.setInterval(poll, 1000);
  })();
</script>`;

const html = buildWorkflowHtml(workflow, state, "idle", "", "")
  .replace("<script", `${browserShim}\n<script`)
  .replace("</body>", `${refreshShim}\n</body>`);

process.stdout.write(html);
