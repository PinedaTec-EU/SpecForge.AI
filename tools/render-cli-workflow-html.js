#!/usr/bin/env node
"use strict";

const fs = require("node:fs");
const { buildWorkflowHtml } = require("../dist/workflowView");

const payload = JSON.parse(fs.readFileSync(0, "utf8"));
const workflow = payload.workflow;
const state = {
  selectedPhaseId: workflow.currentPhase,
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
      try { return JSON.parse(sessionStorage.getItem("specforge.workflow.state") || "{}"); }
      catch { return {}; }
    },
    setState(value) {
      try { sessionStorage.setItem("specforge.workflow.state", JSON.stringify(value || {})); }
      catch {}
    },
    postMessage(message) {
      window.dispatchEvent(new CustomEvent("specforge-cli-command", { detail: message }));
    }
  };
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
