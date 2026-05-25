#!/usr/bin/env node
"use strict";

const fs = require("node:fs");
const { buildWorkflowHtml } = require("../dist/workflowView");
const { buildSidebarHtml } = require("../dist/sidebarViewContent");
const { readWorkflowGraphLayoutConfigAsync } = require("./workflow-graph-layout-portable");

function formatRuntimeVersion(runtimeVersion) {
  const value = String(runtimeVersion ?? "").trim();
  return value ? value.split("+", 1)[0] : null;
}

async function main() {
  const payload = JSON.parse(fs.readFileSync(0, "utf8"));
  const currentActor = String(payload.currentActor || "").trim();
  const workflow = payload.workflow || null;
  const selectedUsId = String(payload.selectedUsId || workflow?.usId || "").trim() || null;
  const userStories = Array.isArray(payload.userStories) ? payload.userStories : [];
  const sidebarUserStories = Array.isArray(payload.sidebarUserStories) ? payload.sidebarUserStories : userStories;
  const activeSidebarUserStories = Array.isArray(payload.activeSidebarUserStories) ? payload.activeSidebarUserStories : userStories;
  const droppedSidebarUserStories = Array.isArray(payload.droppedSidebarUserStories) ? payload.droppedSidebarUserStories : [];
  const showDroppedUserStories = payload.showDroppedUserStories === true;
  const showCompletedUserStories = payload.showCompletedUserStories === true;
  const showBlockedUserStories = payload.showBlockedUserStories === true;
  const showHiddenUserStories = payload.showHiddenUserStories === true;
  const includeOtherOwners = payload.includeOtherOwners === true;
  const showCreateForm = payload.showCreateForm === true;
  const createFileMode = payload.createFileMode === "attachment" ? "attachment" : "context";
  const createFiles = Array.isArray(payload.createFiles) ? payload.createFiles : [];
  const categories = Array.isArray(payload.categories) ? payload.categories : [];
  const watchingUserStoryIds = Array.isArray(payload.watchingUserStoryIds) ? payload.watchingUserStoryIds : [];
  const hiddenUserStoryIds = Array.isArray(payload.hiddenUserStoryIds) ? payload.hiddenUserStoryIds : [];
  const droppedUserStoryCount = Number.isFinite(payload.droppedUserStoryCount) ? payload.droppedUserStoryCount : 0;
  const configurationPortalUrl = payload.configurationPortalUrl || "http://localhost:5128/configuration";
  const configurationProvidersUrl = payload.configurationProvidersUrl || configurationPortalUrl;
  const configurationAdvancedUrl = payload.configurationAdvancedUrl || configurationPortalUrl;
  const displayRuntimeVersion = formatRuntimeVersion(payload.runtimeVersion ?? workflow?.lastRuntimeVersion ?? workflow?.createdWithRuntimeVersion ?? null);
  const workflowGraphLayout = typeof payload.workspaceRoot === "string" && payload.workspaceRoot.length > 0
    ? await readWorkflowGraphLayoutConfigAsync(payload.workspaceRoot)
    : null;
  const state = {
    selectedPhaseId: payload.selectedPhaseId ?? workflow?.currentPhase ?? null,
    selectedArtifactContent: payload.selectedArtifactContent ?? null,
    selectedOperationContent: payload.selectedOperationContent ?? null,
    runtimeVersion: null,
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
    graphLayoutMode: "horizontal",
    graphInitialZoomMode: "fit-width",
    workflowGraphLayout
  };
  const createFormResetToken = Number.isFinite(payload.createFormResetToken) ? payload.createFormResetToken : 1;

const createApiShim = `
<script>
  window.acquireVsCodeApi = window.acquireVsCodeApi || (() => ({
    getState() {
      const storage = window.__specforgeSafeStorage;
      try { return JSON.parse(storage?.getSessionItem("specforge.cli.create.state") || "{}"); }
      catch { return {}; }
    },
    setState(value) {
      window.__specforgeSafeStorage?.setSessionItem("specforge.cli.create.state", JSON.stringify(value || {}));
    },
    postMessage(message) {
      if (typeof window.__specforgeCliCreateHostDispatch === "function") {
        window.__specforgeCliCreateHostDispatch(message);
        return;
      }
      window.parent.postMessage({ source: "specforge-cli-create", message }, "*");
    }
  }));
</script>`;

const browserShim = `
<script>
  const specForgeCliCurrentActor = ${JSON.stringify(currentActor)};
  window.specForgeCliCurrentActor = specForgeCliCurrentActor;
  const specForgePortalStateStorageKey = "specforge.cli.portal.state";
  const specForgePortalTransientQueryKeys = [
    "selectedPhaseId",
    "artifactFocus",
    "create",
    "sidebarVisibility",
    "sidebarCompleted",
    "sidebarBlocked",
    "sidebarHiddenVisible",
    "sidebarOtherOwners",
    "sidebarWatching",
    "sidebarHidden"
  ];
  const normalizePortalUserStoryId = (value) => String(value || "").trim().toUpperCase();
  const parsePortalUserStoryIdList = (value) => Array.from(new Set(
    (Array.isArray(value) ? value : String(value || "").split(","))
      .map(normalizePortalUserStoryId)
      .filter(Boolean)
  ));
  const safeStorage = {
    getSessionItem(key) {
      try { return sessionStorage.getItem(key); }
      catch { return null; }
    },
    setSessionItem(key, value) {
      try { sessionStorage.setItem(key, value); } catch {}
    },
    removeSessionItem(key) {
      try { sessionStorage.removeItem(key); } catch {}
    },
    getLocalItem(key) {
      try { return localStorage.getItem(key); }
      catch { return null; }
    },
    setLocalItem(key, value) {
      try { localStorage.setItem(key, value); } catch {}
    },
    removeLocalItem(key) {
      try { localStorage.removeItem(key); } catch {}
    }
  };
  window.__specforgeSafeStorage = safeStorage;
  const normalizePortalState = (value) => {
    const state = value && typeof value === "object" ? value : {};
    return {
      usId: normalizePortalUserStoryId(state.usId) || null,
      selectedPhaseId: typeof state.selectedPhaseId === "string" && state.selectedPhaseId.trim().length > 0
        ? state.selectedPhaseId.trim()
        : null,
      artifactFocus: state.artifactFocus === "source" ? "source" : null,
      createForm: state.createForm === true,
      sidebarVisibility: state.sidebarVisibility === "dropped" ? "dropped" : "active",
      sidebarCompleted: state.sidebarCompleted === true,
      sidebarBlocked: state.sidebarBlocked === true,
      sidebarHiddenVisible: state.sidebarHiddenVisible === true,
      sidebarOtherOwners: state.sidebarOtherOwners === true,
      sidebarWatching: parsePortalUserStoryIdList(state.sidebarWatching),
      sidebarHidden: parsePortalUserStoryIdList(state.sidebarHidden)
    };
  };
  const readPortalState = () => {
    try {
      return normalizePortalState(JSON.parse(safeStorage.getSessionItem(specForgePortalStateStorageKey) || "{}"));
    } catch {
      return normalizePortalState({});
    }
  };
  const writePortalState = (value) => {
    const normalized = normalizePortalState(value);
    safeStorage.setSessionItem(specForgePortalStateStorageKey, JSON.stringify(normalized));
    return normalized;
  };
  const updatePortalState = (patch) => writePortalState({
    ...readPortalState(),
    ...(patch && typeof patch === "object" ? patch : {})
  });
  const buildCanonicalPortalUrl = (usId) => {
    const url = new URL(window.location.href);
    for (const key of specForgePortalTransientQueryKeys) {
      url.searchParams.delete(key);
    }
    const normalizedUsId = normalizePortalUserStoryId(usId);
    if (normalizedUsId) {
      url.searchParams.set("usId", normalizedUsId);
    } else {
      url.searchParams.delete("usId");
    }
    return url;
  };
  const buildWorkflowRequestUrl = (patch) => {
    const state = normalizePortalState({
      ...readPortalState(),
      ...(patch && typeof patch === "object" ? patch : {})
    });
    const url = buildCanonicalPortalUrl(state.usId);
    if (state.selectedPhaseId) {
      url.searchParams.set("selectedPhaseId", state.selectedPhaseId);
    }
    if (state.artifactFocus) {
      url.searchParams.set("artifactFocus", state.artifactFocus);
    }
    if (state.createForm) {
      url.searchParams.set("create", "true");
    }
    if (state.sidebarVisibility === "dropped") {
      url.searchParams.set("sidebarVisibility", "dropped");
    } else {
      if (state.sidebarCompleted) {
        url.searchParams.set("sidebarCompleted", "true");
      }
      if (state.sidebarBlocked) {
        url.searchParams.set("sidebarBlocked", "true");
      }
    }
    if (state.sidebarHiddenVisible) {
      url.searchParams.set("sidebarHiddenVisible", "true");
    }
    if (state.sidebarOtherOwners) {
      url.searchParams.set("sidebarOtherOwners", "true");
    }
    if (state.sidebarWatching.length > 0) {
      url.searchParams.set("sidebarWatching", state.sidebarWatching.join(","));
    }
    if (state.sidebarHidden.length > 0) {
      url.searchParams.set("sidebarHidden", state.sidebarHidden.join(","));
    }
    return { url, state };
  };
  const syncCanonicalPortalUrl = (usId) => {
    const canonicalUrl = buildCanonicalPortalUrl(usId);
    window.history.replaceState(window.history.state, "", canonicalUrl.toString());
    return canonicalUrl;
  };
  const hydratePortalStateFromLocation = () => {
    const url = new URL(window.location.href);
    const currentState = readPortalState();
    const nextState = {
      ...currentState,
      usId: normalizePortalUserStoryId(url.searchParams.get("usId"))
        || currentState.usId
        || normalizePortalUserStoryId(${JSON.stringify(workflow?.usId ?? null)})
        || null
    };
    if (url.searchParams.has("selectedPhaseId")) {
      nextState.selectedPhaseId = url.searchParams.get("selectedPhaseId") || null;
    }
    if (url.searchParams.has("artifactFocus")) {
      nextState.artifactFocus = url.searchParams.get("artifactFocus") === "source" ? "source" : null;
    }
    if (url.searchParams.has("create") || url.searchParams.has("sidebarCreate")) {
      nextState.createForm = url.searchParams.get("create") === "true" || url.searchParams.get("sidebarCreate") === "true";
    }
    if (url.searchParams.has("sidebarVisibility")) {
      nextState.sidebarVisibility = url.searchParams.get("sidebarVisibility") === "dropped" ? "dropped" : "active";
    }
    if (url.searchParams.has("sidebarCompleted")) {
      nextState.sidebarCompleted = url.searchParams.get("sidebarCompleted") === "true";
    }
    if (url.searchParams.has("sidebarBlocked")) {
      nextState.sidebarBlocked = url.searchParams.get("sidebarBlocked") === "true";
    }
    if (url.searchParams.has("sidebarHiddenVisible")) {
      nextState.sidebarHiddenVisible = url.searchParams.get("sidebarHiddenVisible") === "true";
    }
    if (url.searchParams.has("sidebarOtherOwners")) {
      nextState.sidebarOtherOwners = url.searchParams.get("sidebarOtherOwners") === "true";
    }
    if (url.searchParams.has("sidebarWatching")) {
      nextState.sidebarWatching = parsePortalUserStoryIdList(url.searchParams.get("sidebarWatching") || "");
    }
    if (url.searchParams.has("sidebarHidden")) {
      nextState.sidebarHidden = parsePortalUserStoryIdList(url.searchParams.get("sidebarHidden") || "");
    }
    const hydrated = writePortalState(nextState);
    syncCanonicalPortalUrl(hydrated.usId);
    return hydrated;
  };
  hydratePortalStateFromLocation();
  window.__specforgePortalState = {
    read: readPortalState,
    write: writePortalState,
    update: updatePortalState,
    buildCanonicalUrl: buildCanonicalPortalUrl,
    buildRequestUrl: buildWorkflowRequestUrl,
    syncCanonicalUrl: syncCanonicalPortalUrl
  };
  window.__specforgePortalLifecycle = window.__specforgePortalLifecycle || {
    report(action, reason, extra) {
      try {
        const state = readPortalState();
        const payload = {
          action,
          reason,
          url: window.location.href,
          selectedPhaseId: state.selectedPhaseId,
          renderedWorkflowUsId: ${JSON.stringify(workflow?.usId ?? null)},
          timestampUtc: new Date().toISOString(),
          ...(extra || {})
        };
        return fetch("/api/client-log", {
          method: "POST",
          keepalive: true,
          headers: { "content-type": "application/json" },
          body: JSON.stringify(payload)
        }).catch(() => undefined);
      } catch {
        return Promise.resolve();
      }
    },
    reload(reason, extra) {
      const { url } = buildWorkflowRequestUrl();
      void this.report("reload", reason, { ...(extra || {}), targetUrl: url.toString() });
      window.location.href = url.toString();
    },
    navigate(targetUrl, reason, extra) {
      const resolvedTargetUrl = typeof targetUrl === "string" ? targetUrl : String(targetUrl || "");
      try {
        const parsedUrl = new URL(resolvedTargetUrl, window.location.href);
        const patch = {
          usId: normalizePortalUserStoryId(parsedUrl.searchParams.get("usId")) || readPortalState().usId,
          selectedPhaseId: parsedUrl.searchParams.has("selectedPhaseId")
            ? (parsedUrl.searchParams.get("selectedPhaseId") || null)
            : undefined,
          artifactFocus: parsedUrl.searchParams.has("artifactFocus")
            ? (parsedUrl.searchParams.get("artifactFocus") === "source" ? "source" : null)
            : undefined,
          createForm: parsedUrl.searchParams.has("create")
            ? parsedUrl.searchParams.get("create") === "true"
            : undefined,
          sidebarVisibility: parsedUrl.searchParams.has("sidebarVisibility")
            ? (parsedUrl.searchParams.get("sidebarVisibility") === "dropped" ? "dropped" : "active")
            : undefined,
          sidebarCompleted: parsedUrl.searchParams.has("sidebarCompleted")
            ? parsedUrl.searchParams.get("sidebarCompleted") === "true"
            : undefined,
          sidebarBlocked: parsedUrl.searchParams.has("sidebarBlocked")
            ? parsedUrl.searchParams.get("sidebarBlocked") === "true"
            : undefined,
          sidebarHiddenVisible: parsedUrl.searchParams.has("sidebarHiddenVisible")
            ? parsedUrl.searchParams.get("sidebarHiddenVisible") === "true"
            : undefined,
          sidebarOtherOwners: parsedUrl.searchParams.has("sidebarOtherOwners")
            ? parsedUrl.searchParams.get("sidebarOtherOwners") === "true"
            : undefined,
          sidebarWatching: parsedUrl.searchParams.has("sidebarWatching")
            ? parsePortalUserStoryIdList(parsedUrl.searchParams.get("sidebarWatching") || "")
            : undefined,
          sidebarHidden: parsedUrl.searchParams.has("sidebarHidden")
            ? parsePortalUserStoryIdList(parsedUrl.searchParams.get("sidebarHidden") || "")
            : undefined
        };
        const { url, state } = buildWorkflowRequestUrl(patch);
        writePortalState(state);
        void this.report("navigate", reason, { ...(extra || {}), targetUrl: url.toString() });
        window.location.href = url.toString();
        return;
      } catch {}
      void this.report("navigate", reason, { ...(extra || {}), targetUrl: resolvedTargetUrl });
      window.location.href = resolvedTargetUrl;
    }
  };
  window.__specForgeVsCodeApi = window.__specForgeVsCodeApi || {
    getState() {
      try {
        const state = JSON.parse(safeStorage.getSessionItem("specforge.workflow.state") || "{}");
        if (safeStorage.getSessionItem("specforge.workflow.userViewport") !== "true") {
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
      safeStorage.setSessionItem("specforge.workflow.state", JSON.stringify(value || {}));
    },
    postMessage(message) {
      if (message?.command === "openWorkflow" && message.usId) {
        updatePortalState({ usId: message.usId, selectedPhaseId: null, artifactFocus: null });
        window.__specforgePortalLifecycle.reload("openWorkflow", {
          triggerCommand: message.command,
          detail: "Open workflow from CLI portal shell."
        });
        return;
      }

      if (message?.command === "selectPhase" && message.phaseId) {
        updatePortalState({ selectedPhaseId: message.phaseId });
        window.__specforgePortalLifecycle.reload("selectPhase", {
          triggerCommand: message.command,
          detail: "Select workflow phase from CLI portal shell."
        });
        return;
      }

      if (message?.command === "suggestApprovalAnswer" && message.question) {
        fetch("/api/suggest-approval-answer", {
          method: "POST",
          headers: { "content-type": "application/json" },
          body: JSON.stringify({ question: message.question, actor: specForgeCliCurrentActor })
        })
          .then(response => response.ok ? response.json() : response.text().then(text => Promise.reject(new Error(text))))
          .then(result => {
            try {
              const state = JSON.parse(safeStorage.getSessionItem("specforge.workflow.state") || "{}");
              state.approvalAnswerDrafts = {
                ...(state.approvalAnswerDrafts || {}),
                [String(message.index)]: result.answer || ""
              };
              safeStorage.setSessionItem("specforge.workflow.state", JSON.stringify(state));
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

      if (message?.command === "continue" || message?.command === "play") {
        fetch("/api/continue", { method: "POST" })
          .then(response => response.ok ? response.json() : response.text().then(text => Promise.reject(new Error(text))))
          .then(() => {
            window.__specforgePortalLifecycle.reload("post-continue", {
              triggerCommand: message.command,
              detail: "Workflow continue/play completed."
            });
          })
          .catch(error => {
            window.postMessage({
              command: "workflowActionFailed",
              action: message.command,
              detail: error instanceof Error ? error.message : String(error)
            }, "*");
          });
        return;
      }

      if (message?.command === "submitApprovalAnswer" && message.question && message.answer) {
        fetch("/api/approval-answer", {
          method: "POST",
          headers: { "content-type": "application/json" },
          body: JSON.stringify({ question: message.question, answer: message.answer, actor: specForgeCliCurrentActor })
        })
          .then(response => response.ok ? response.json() : response.text().then(text => Promise.reject(new Error(text))))
          .then(() => {
            window.__specforgePortalLifecycle.reload("post-approval-answer", {
              triggerCommand: message.command,
              detail: "Approval answer submitted."
            });
          })
          .catch(error => {
            window.postMessage({
              command: "workflowActionFailed",
              action: message.command,
              detail: error instanceof Error ? error.message : String(error)
            }, "*");
          });
        return;
      }

      if (message?.command === "submitRefinementAnswers" && Array.isArray(message.answers)) {
        fetch("/api/refinement-answers", {
          method: "POST",
          headers: { "content-type": "application/json" },
          body: JSON.stringify({ answers: message.answers, actor: specForgeCliCurrentActor })
        })
          .then(response => response.ok ? response.json() : response.text().then(text => Promise.reject(new Error(text))))
          .then(() => {
            window.__specforgePortalLifecycle.reload("post-refinement-answers", {
              triggerCommand: message.command,
              detail: "Refinement answers submitted."
            });
          })
          .catch(error => {
            window.postMessage({
              command: "workflowActionFailed",
              action: message.command,
              detail: error instanceof Error ? error.message : String(error)
            }, "*");
          });
        return;
      }

      if (message?.command === "saveWorkflowGraphLayout") {
        fetch("/api/workflow-graph-layout", {
          method: "POST",
          headers: { "content-type": "application/json" },
          body: JSON.stringify(message)
        })
          .then(response => response.ok ? response.json() : response.text().then(text => Promise.reject(new Error(text))))
          .catch(error => {
            window.postMessage({
              command: "workflowActionFailed",
              action: message.command,
              detail: error instanceof Error ? error.message : String(error)
            }, "*");
          });
        return;
      }

      if (message?.command === "attachFiles") {
        const kind = message.kind === "context" ? "context" : "attachment";
        const input = document.createElement("input");
        input.type = "file";
        input.multiple = true;
        input.onchange = () => {
          const files = Array.from(input.files || []);
          if (files.length === 0) {
            return;
          }

          Promise.all(files.map(async file => {
            const buffer = await file.arrayBuffer();
            const bytes = new Uint8Array(buffer);
            let binary = "";
            const chunkSize = 0x8000;
            for (let index = 0; index < bytes.length; index += chunkSize) {
              const chunk = bytes.subarray(index, index + chunkSize);
              binary += String.fromCharCode(...chunk);
            }
            return {
              name: file.name,
              base64Content: btoa(binary)
            };
          }))
            .then(payloadFiles => fetch("/api/attach-files", {
              method: "POST",
              headers: { "content-type": "application/json" },
              body: JSON.stringify({ kind, files: payloadFiles, actor: specForgeCliCurrentActor })
            }))
            .then(response => response.ok ? response.json() : response.text().then(text => Promise.reject(new Error(text))))
            .then(() => {
              window.__specforgePortalLifecycle.reload("post-attach-files", {
                triggerCommand: message.command,
                detail: "Workflow files attached."
              });
            })
            .catch(error => {
              window.postMessage({
                command: "workflowActionFailed",
                action: message.command,
                detail: error instanceof Error ? error.message : String(error)
              }, "*");
            });
        };
        input.click();
        return;
      }

      if ((message?.command === "addSuggestedContextFile" && message.path)
        || (message?.command === "addSuggestedContextFiles" && Array.isArray(message.paths) && message.paths.length > 0)) {
        const paths = message.command === "addSuggestedContextFile"
          ? [message.path]
          : message.paths;
        fetch("/api/add-context-files", {
          method: "POST",
          headers: { "content-type": "application/json" },
          body: JSON.stringify({ paths, actor: specForgeCliCurrentActor })
        })
          .then(response => response.ok ? response.json() : response.text().then(text => Promise.reject(new Error(text))))
          .then(() => {
            window.__specforgePortalLifecycle.reload("post-add-context-files", {
              triggerCommand: message.command,
              detail: "Context files added."
            });
          })
          .catch(error => {
            window.postMessage({
              command: "workflowActionFailed",
              action: message.command,
              detail: error instanceof Error ? error.message : String(error)
            }, "*");
          });
        return;
      }

      if (message?.command === "approve") {
        fetch("/api/approve", {
          method: "POST",
          headers: { "content-type": "application/json" },
          body: JSON.stringify({
            baseBranch: message.baseBranch || null,
            workBranch: message.workBranch || null,
            actor: specForgeCliCurrentActor
          })
        })
          .then(response => response.ok ? response.json() : response.text().then(text => Promise.reject(new Error(text))))
          .then(() => {
            window.__specforgePortalLifecycle.reload("post-approve", {
              triggerCommand: message.command,
              detail: "Workflow phase approved."
            });
          })
          .catch(error => {
            window.postMessage({
              command: "workflowActionFailed",
              action: message.command,
              detail: error instanceof Error ? error.message : String(error)
            }, "*");
          });
        return;
      }

      if (message?.command === "approveDecomposition" || message?.command === "rejectDecomposition") {
        fetch("/api/decomposition-approval", {
          method: "POST",
          headers: { "content-type": "application/json" },
          body: JSON.stringify({
            decision: message.command === "approveDecomposition" ? "approve" : "reject",
            actor: specForgeCliCurrentActor
          })
        })
          .then(response => response.ok ? response.json() : response.text().then(text => Promise.reject(new Error(text))))
          .then((result) => {
            if (message.command === "approveDecomposition" && Array.isArray(result.childUsIds) && result.childUsIds.length > 0) {
              updatePortalState({ selectedPhaseId: "spec" });
              window.__specforgePortalLifecycle.reload("post-decomposition-approval:navigate-to-spec", {
                triggerCommand: message.command,
                detail: "Decomposition approval created child user stories."
              });
              return;
            }
            window.__specforgePortalLifecycle.reload("post-decomposition-approval", {
              triggerCommand: message.command,
              detail: "Decomposition decision applied."
            });
          })
          .catch(error => {
            window.postMessage({
              command: "workflowActionFailed",
              action: message.command,
              detail: error instanceof Error ? error.message : String(error)
            }, "*");
          });
        return;
      }

      if (message?.command === "openWorkflowTab" && message.usId) {
        updatePortalState({ usId: message.usId, selectedPhaseId: null, artifactFocus: null });
        window.open(buildCanonicalPortalUrl(message.usId).toString(), "_blank", "noopener");
        return;
      }

      window.dispatchEvent(new CustomEvent("specforge-cli-command", { detail: message }));
    }
  };
  window.addEventListener("pointerdown", event => {
    if (event.target?.closest?.('[data-panel-scroll="graph"], [data-graph-zoom-in], [data-graph-zoom-out], [data-graph-fit-width], [data-graph-auto-fit]')) {
      safeStorage.setSessionItem("specforge.workflow.userViewport", "true");
    }
  }, true);
  window.addEventListener("wheel", event => {
    if (event.target?.closest?.('[data-panel-scroll="graph"]')) {
      safeStorage.setSessionItem("specforge.workflow.userViewport", "true");
    }
  }, { capture: true, passive: true });
</script>`;

const refreshShim = `
<script>
  (() => {
    const renderedWorkflowUsId = ${JSON.stringify(workflow?.usId ?? null)};
    try {
      window.__specforgePortalState?.update?.({ usId: renderedWorkflowUsId || null });
      window.__specforgePortalState?.syncCanonicalUrl?.(renderedWorkflowUsId || null);
    } catch {}
    let signature = ${JSON.stringify(payload.signature)};
    async function poll() {
      try {
        const requestUrl = window.__specforgePortalState?.buildRequestUrl?.({ usId: renderedWorkflowUsId || null })?.url
          || new URL(window.location.href);
        const response = await fetch("/api/workflow-signature" + requestUrl.search, { cache: "no-store" });
        if (!response.ok) return;
        const next = await response.text();
        if (signature && next && next !== signature) {
          window.__specforgePortalLifecycle.reload("signature-changed", {
            signature,
            nextSignature: next,
            detail: "Workflow portal render signature changed during polling."
          });
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

const sidebarApiShim = `
<script>
  window.acquireVsCodeApi = window.acquireVsCodeApi || (() => ({
    getState() {
      const storage = window.__specforgeSafeStorage;
      try { return JSON.parse(storage?.getSessionItem("specforge.cli.sidebar.state") || "{}"); }
      catch { return {}; }
    },
    setState(value) {
      window.__specforgeSafeStorage?.setSessionItem("specforge.cli.sidebar.state", JSON.stringify(value || {}));
    },
    postMessage(message) {
      if (typeof window.__specforgeCliSidebarHostDispatch === "function") {
        window.__specforgeCliSidebarHostDispatch(message);
        return;
      }
      window.parent.postMessage({ source: "specforge-cli-sidebar", message }, "*");
    }
  }));
</script>`;

function buildCliSidebarHtml(items, options) {
  const currentActor = String(options.currentActor || "").trim();
  const normalizedCurrentActor = currentActor.toLowerCase();
  const watchingIds = new Set(options.watchingUserStoryIds.map(normalizeUserStoryId));
  const hiddenIds = new Set(options.hiddenUserStoryIds.map(normalizeUserStoryId));
  const scopedItems = options.includeOtherOwners
    ? items.filter(item => options.showHiddenUserStories || !hiddenIds.has(normalizeUserStoryId(item.usId)))
    : items.filter(item => {
      const normalizedUsId = normalizeUserStoryId(item.usId);
      if (!options.showHiddenUserStories && hiddenIds.has(normalizedUsId)) {
        return false;
      }

      const normalizedOwner = String(item.owner || "").trim().toLowerCase();
      return watchingIds.has(normalizedUsId) || normalizedOwner === normalizedCurrentActor;
    });

  return buildSidebarHtml({
    hasWorkspace: true,
    showCreateForm: false,
    createSurface: "main-window",
    busyMessage: null,
    promptsInitialized: true,
    promptsMessage: null,
    settingsConfigured: true,
    settingsMessage: null,
    starredUserStoryId: null,
    activeWorkflowUsId: selectedUsId,
    runtimeVersion: null,
    showViewOptionsMenu: false,
    viewMode: "category",
    showDroppedUserStories: options.showDroppedUserStories,
    showCompletedUserStories: options.showCompletedUserStories,
    showBlockedUserStories: options.showBlockedUserStories,
    showHiddenUserStories: options.showHiddenUserStories,
    searchIncludesOtherOwners: options.includeOtherOwners,
    currentActor,
    watchingUserStoryIds: options.watchingUserStoryIds,
    hiddenUserStoryIds: options.hiddenUserStoryIds,
    maxVisibleUserStories: null,
    totalUserStoryCount: scopedItems.length,
    droppedUserStoryCount,
    categories: [...new Set(scopedItems.map(item => item.category).filter(Boolean))],
    userStories: scopedItems,
    createFileMode: "context",
    createFiles: [],
    createFormResetToken: 0
  }).replace("<script>", `${sidebarApiShim}\n<script>`);
}

function normalizeCreateFiles(items) {
  return (Array.isArray(items) ? items : [])
    .map((item, index) => {
      const sourcePath = String(item?.sourcePath || `upload-${index + 1}`).trim();
      const name = String(item?.name || "").trim();
      const kind = item?.kind === "attachment" ? "attachment" : "context";
      const base64Content = typeof item?.base64Content === "string" ? item.base64Content : "";
      return sourcePath && name ? { sourcePath, name, kind, base64Content } : null;
    })
    .filter(Boolean);
}

function buildCliCreateMainHtml(options) {
  const normalizedFiles = normalizeCreateFiles(options.createFiles);
  const createCategories = Array.isArray(options.categories) && options.categories.length > 0
    ? options.categories.map((category) => String(category || "").trim()).filter(Boolean)
    : [...new Set(activeSidebarUserStories.map(item => item.category).filter(Boolean))];

  return buildSidebarHtml({
    hasWorkspace: true,
    showCreateForm: true,
    createSurface: "main-window",
    showCreateAction: false,
    showStoryList: false,
    busyMessage: null,
    promptsInitialized: true,
    promptsMessage: null,
    settingsConfigured: true,
    settingsMessage: null,
    starredUserStoryId: null,
    activeWorkflowUsId: selectedUsId,
    runtimeVersion: displayRuntimeVersion,
    showViewOptionsMenu: false,
    viewMode: "category",
    showDroppedUserStories: false,
    showCompletedUserStories: false,
    showBlockedUserStories: false,
    showHiddenUserStories: false,
    searchIncludesOtherOwners: false,
    currentActor,
    watchingUserStoryIds: [],
    hiddenUserStoryIds: [],
    maxVisibleUserStories: null,
    totalUserStoryCount: 0,
    droppedUserStoryCount: 0,
    categories: createCategories,
    userStories: [],
    createFileMode: options.createFileMode === "attachment" ? "attachment" : "context",
    createFiles: normalizedFiles,
    createFormResetToken: Number.isFinite(options.createFormResetToken) ? options.createFormResetToken : 1
  }).replace("<script>", `${createApiShim}\n<script>`);
}

function safeScriptJson(value) {
  return JSON.stringify(value).replaceAll("</", "<\\/");
}

function normalizeUserStoryId(value) {
  return String(value || "").trim().toUpperCase();
}

function buildCurrentCliSidebarHtml() {
  return buildCliSidebarHtml(showDroppedUserStories ? droppedSidebarUserStories : activeSidebarUserStories, {
    showDroppedUserStories,
    showCompletedUserStories: showDroppedUserStories ? false : showCompletedUserStories,
    showBlockedUserStories: showDroppedUserStories ? false : showBlockedUserStories,
    showHiddenUserStories,
    includeOtherOwners,
    watchingUserStoryIds,
    hiddenUserStoryIds,
    currentActor,
    showCreateForm: showDroppedUserStories ? false : showCreateForm
  });
}

if (payload.renderSidebarOnly === true) {
  process.stdout.write(buildCurrentCliSidebarHtml());
  return;
}

if (payload.renderCreateFormOnly === true) {
  process.stdout.write(buildCliCreateMainHtml({
    createFileMode,
    createFiles,
    categories,
    createFormResetToken
  }));
  return;
}

const sidebarHtml = buildCurrentCliSidebarHtml();
const createHtml = buildCliCreateMainHtml({
  createFileMode,
  createFiles,
  categories,
  createFormResetToken
});

const sidebarShell = `
<style>
  body.specforge-cli-with-sidebar {
    --specforge-cli-sidebar-width: 340px;
    --specforge-cli-sidebar-min-width: 280px;
    --specforge-cli-sidebar-max-width: 520px;
    --specforge-cli-sidebar-handle-width: 10px;
    display: grid;
    grid-template-columns: var(--specforge-cli-sidebar-width) var(--specforge-cli-sidebar-handle-width) minmax(0, 1fr);
    min-height: 100vh;
    overflow: hidden;
  }
  body.specforge-cli-with-sidebar.specforge-cli-sidebar-collapsed { grid-template-columns: 58px 0 minmax(0, 1fr); }
  body.specforge-cli-with-sidebar.specforge-cli-sidebar-resizing,
  body.specforge-cli-with-sidebar.specforge-cli-sidebar-resizing * { cursor: col-resize !important; user-select: none !important; }
  .specforge-cli-sidebar { position: sticky; top: 0; height: 100vh; min-width: 0; border-right: 1px solid rgba(114, 241, 184, 0.16); background: #080e14; display: grid; grid-template-rows: auto minmax(0, 1fr); z-index: 50; }
  .specforge-cli-sidebar-resizer { position: relative; width: 100%; min-width: 0; height: 100vh; padding: 0; border: 0; border-radius: 0; background: transparent; cursor: col-resize; touch-action: none; }
  .specforge-cli-sidebar-resizer::before { content: ""; position: absolute; inset: 0; background: linear-gradient(180deg, rgba(114, 241, 184, 0.04), rgba(114, 241, 184, 0.12), rgba(114, 241, 184, 0.04)); opacity: 0.42; transition: opacity 140ms ease, background 140ms ease; }
  .specforge-cli-sidebar-resizer::after { content: ""; position: absolute; left: 50%; top: 50%; width: 4px; height: 72px; border-radius: 999px; transform: translate(-50%, -50%); background: linear-gradient(180deg, rgba(114, 241, 184, 0.18), rgba(114, 241, 184, 0.72), rgba(114, 241, 184, 0.18)); box-shadow: 0 0 0 1px rgba(114, 241, 184, 0.12); transition: height 140ms ease, background 140ms ease; }
  .specforge-cli-sidebar-resizer:hover::before,
  .specforge-cli-sidebar-resizer:focus-visible::before,
  body.specforge-cli-with-sidebar.specforge-cli-sidebar-resizing .specforge-cli-sidebar-resizer::before { opacity: 1; background: linear-gradient(180deg, rgba(114, 241, 184, 0.10), rgba(114, 241, 184, 0.18), rgba(114, 241, 184, 0.10)); }
  .specforge-cli-sidebar-resizer:hover::after,
  .specforge-cli-sidebar-resizer:focus-visible::after,
  body.specforge-cli-with-sidebar.specforge-cli-sidebar-resizing .specforge-cli-sidebar-resizer::after { height: 110px; background: linear-gradient(180deg, rgba(114, 241, 184, 0.34), rgba(200, 255, 230, 0.98), rgba(114, 241, 184, 0.34)); }
  .specforge-cli-sidebar-resizer:focus-visible { outline: none; }
  .specforge-cli-sidebar__rail { display: grid; grid-template-columns: minmax(0, 1fr) auto auto auto; align-items: center; gap: 8px; padding: 10px; border-bottom: 1px solid rgba(114, 241, 184, 0.12); }
  .specforge-cli-sidebar__button { width: 38px; height: 38px; border-radius: 12px; border: 1px solid rgba(114, 241, 184, 0.18); background: rgba(255, 255, 255, 0.04); color: #72f1b8; font: 700 1rem/1 ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; cursor: pointer; display: inline-grid; place-items: center; }
  .specforge-cli-sidebar__button:hover { background: rgba(114, 241, 184, 0.12); border-color: rgba(114, 241, 184, 0.34); }
  .specforge-cli-sidebar__button--active { background: rgba(114, 241, 184, 0.14); border-color: rgba(114, 241, 184, 0.36); }
  .specforge-cli-sidebar__menu { position: relative; }
  .specforge-cli-sidebar__menu-panel { position: absolute; right: 0; top: calc(100% + 8px); z-index: 90; min-width: 220px; padding: 8px; border-radius: 14px; border: 1px solid rgba(114, 241, 184, 0.18); background: rgba(8, 14, 20, 0.96); box-shadow: 0 18px 56px rgba(0, 0, 0, 0.42); display: grid; gap: 4px; }
  .specforge-cli-sidebar__menu-panel[hidden] { display: none; }
  .specforge-cli-sidebar__menu-item { display: grid; grid-template-columns: 18px minmax(0, 1fr); align-items: center; gap: 10px; width: 100%; padding: 10px 12px; border-radius: 10px; border: 0; background: transparent; color: rgba(255, 255, 255, 0.86); font: 700 0.83rem/1.2 ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; text-align: left; cursor: pointer; }
  .specforge-cli-sidebar__menu-item:hover { background: rgba(114, 241, 184, 0.10); }
  .specforge-cli-sidebar__menu-item--active { color: #dfffee; background: rgba(114, 241, 184, 0.12); }
  .specforge-cli-sidebar__menu-check { color: #72f1b8; font-weight: 900; }
  .specforge-cli-sidebar__brand { min-width: 0; display: flex; align-items: baseline; gap: 8px; white-space: nowrap; overflow: hidden; }
  .specforge-cli-sidebar__title { min-width: 0; color: #72f1b8; font: 900 1.12rem/1.2 ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; overflow: hidden; text-overflow: ellipsis; letter-spacing: 0.02em; }
  .specforge-cli-sidebar__version { flex-shrink: 0; color: rgba(176, 180, 176, 0.76); font: 700 0.7rem/1.2 ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; }
  .specforge-cli-sidebar__surface { width: 100%; height: 100%; min-width: 0; overflow: auto; background: transparent; }
  body.specforge-cli-sidebar-collapsed .specforge-cli-sidebar { grid-template-rows: 1fr; }
  body.specforge-cli-sidebar-collapsed .specforge-cli-sidebar__rail { display: flex; flex-direction: column; align-items: center; justify-content: flex-start; border-bottom: 0; padding-top: 12px; }
  body.specforge-cli-sidebar-collapsed .specforge-cli-sidebar__brand,
  body.specforge-cli-sidebar-collapsed .specforge-cli-sidebar__surface { display: none; }
  body.specforge-cli-sidebar-collapsed .specforge-cli-sidebar-resizer { display: none; }
  body.specforge-cli-with-sidebar > .workflow-page { min-width: 0; height: 100vh; overflow: hidden; }
  .specforge-cli-config-overlay { position: fixed; inset: 0; z-index: 200; display: grid; place-items: center; padding: 28px; background: rgba(3, 8, 12, 0.72); backdrop-filter: blur(8px); }
  .specforge-cli-config-overlay[hidden] { display: none; }
  .specforge-cli-config-dialog { width: min(1100px, 100%); height: min(820px, calc(100vh - 56px)); border: 1px solid rgba(114, 241, 184, 0.18); border-radius: 12px; background: #0f1720; box-shadow: 0 28px 90px rgba(0, 0, 0, 0.52); display: grid; grid-template-rows: auto minmax(0, 1fr); overflow: hidden; }
  .specforge-cli-config-head { display: flex; align-items: center; justify-content: space-between; gap: 16px; padding: 12px 14px; border-bottom: 1px solid rgba(114, 241, 184, 0.14); background: #080e14; color: rgba(255, 255, 255, 0.86); font: 800 0.82rem/1.2 ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; }
  .specforge-cli-config-close { width: 34px; height: 34px; border-radius: 10px; border: 1px solid rgba(114, 241, 184, 0.2); background: rgba(255, 255, 255, 0.05); color: #72f1b8; font: 900 1.1rem/1 ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; cursor: pointer; }
  .specforge-cli-config-surface { width: 100%; height: 100%; overflow: auto; background: #0f1720; }
  .specforge-cli-edit-overlay { position: fixed; inset: 0; z-index: 220; display: grid; place-items: center; padding: 28px; background: rgba(3, 8, 12, 0.72); backdrop-filter: blur(8px); }
  .specforge-cli-edit-overlay[hidden] { display: none; }
  .specforge-cli-edit-dialog { width: min(560px, 100%); border: 1px solid rgba(114, 241, 184, 0.18); border-radius: 18px; background: #0f1720; box-shadow: 0 28px 90px rgba(0, 0, 0, 0.52); overflow: hidden; }
  .specforge-cli-edit-form { display: grid; gap: 14px; padding: 18px; }
  .specforge-cli-edit-head { display: flex; align-items: flex-start; justify-content: space-between; gap: 16px; }
  .specforge-cli-edit-kicker { display: block; color: rgba(114, 241, 184, 0.86); font: 800 0.72rem/1.2 ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; letter-spacing: 0.14em; text-transform: uppercase; }
  .specforge-cli-edit-head h2 { margin: 6px 0 0; color: rgba(255, 255, 255, 0.94); font: 800 1.05rem/1.2 ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; }
  .specforge-cli-edit-close, .specforge-cli-edit-secondary, .specforge-cli-edit-primary { border-radius: 10px; border: 1px solid rgba(114, 241, 184, 0.2); cursor: pointer; font: 700 0.9rem/1 ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; }
  .specforge-cli-edit-close { width: 34px; height: 34px; background: rgba(255, 255, 255, 0.05); color: #72f1b8; font-size: 1.1rem; }
  .specforge-cli-edit-field { display: grid; gap: 6px; }
  .specforge-cli-edit-field span { color: rgba(255, 255, 255, 0.78); font: 700 0.78rem/1.2 ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; text-transform: uppercase; letter-spacing: 0.08em; }
  .specforge-cli-edit-field-row { display: grid; grid-template-columns: minmax(0, 1fr) 26px; gap: 10px; align-items: center; }
  .specforge-cli-edit-field input { width: 100%; min-width: 0; border-radius: 12px; border: 1px solid rgba(255, 255, 255, 0.08); background: rgba(255, 255, 255, 0.04); color: rgba(255, 255, 255, 0.94); padding: 12px 14px; font: 500 0.94rem/1.4 ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; transition: border-color 160ms ease, box-shadow 160ms ease; }
  .specforge-cli-edit-field input:focus { outline: none; border-color: rgba(114, 241, 184, 0.54); box-shadow: 0 0 0 3px rgba(114, 241, 184, 0.12); }
  .specforge-cli-edit-field--invalid span { color: #ffb8b8; }
  .specforge-cli-edit-field--invalid input { border-color: rgba(255, 139, 139, 0.72); box-shadow: 0 0 0 3px rgba(120, 29, 29, 0.16); }
  .specforge-cli-edit-field-error { display: inline-grid; place-items: center; width: 26px; height: 26px; border-radius: 999px; border: 1px solid transparent; background: transparent; color: transparent; font: 800 0.72rem/1 ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; visibility: hidden; pointer-events: none; user-select: none; }
  .specforge-cli-edit-field-error::before { content: "!"; }
  .specforge-cli-edit-field-error[data-visible="true"] { border-color: rgba(255, 139, 139, 0.48); background: rgba(120, 29, 29, 0.3); color: #ffd3d3; visibility: visible; pointer-events: auto; cursor: help; }
  .specforge-cli-edit-owner-row { display: grid; grid-template-columns: minmax(0, 1fr) auto; gap: 10px; align-items: center; }
  .specforge-cli-edit-owner-assign { border-radius: 12px; border: 1px solid rgba(114, 241, 184, 0.22); background: rgba(20, 53, 40, 0.9); color: #dfffee; padding: 12px 14px; font: 700 0.84rem/1 ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; cursor: pointer; white-space: nowrap; }
  .specforge-cli-edit-owner-assign:disabled { opacity: 0.45; cursor: not-allowed; }
  .specforge-cli-edit-owner-assign[hidden] { display: none; }
  .specforge-cli-edit-error { margin: 0; padding: 10px 12px; border-radius: 12px; border: 1px solid rgba(255, 139, 139, 0.2); background: rgba(120, 29, 29, 0.18); color: #ffb8b8; font: 600 0.85rem/1.4 ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; }
  .specforge-cli-edit-actions { display: flex; justify-content: flex-end; gap: 10px; }
  .specforge-cli-edit-secondary { background: rgba(255, 255, 255, 0.05); color: rgba(255, 255, 255, 0.86); padding: 11px 14px; }
  .specforge-cli-edit-primary { background: linear-gradient(180deg, rgba(114, 241, 184, 0.24), rgba(16, 36, 28, 0.96)); color: #f3fff9; padding: 11px 16px; }
  .specforge-cli-edit-primary:disabled { opacity: 0.5; cursor: not-allowed; }
  .specforge-cli-source-focus { outline: 2px solid rgba(114, 241, 184, 0.78); outline-offset: 6px; border-radius: 16px; transition: outline-color 180ms ease; }
  @media (max-width: 860px) {
    body.specforge-cli-with-sidebar { grid-template-columns: 58px minmax(0, 1fr); }
    body.specforge-cli-with-sidebar:not(.specforge-cli-sidebar-collapsed) { grid-template-columns: minmax(280px, 86vw) minmax(0, 1fr); }
    .specforge-cli-sidebar-resizer { display: none; }
    .specforge-cli-config-overlay { padding: 10px; }
    .specforge-cli-config-dialog { height: calc(100vh - 20px); }
  }
</style>
<aside class="specforge-cli-sidebar" aria-label="SpecForge user stories">
  <div class="specforge-cli-sidebar__rail">
    <div class="specforge-cli-sidebar__brand">
      <span class="specforge-cli-sidebar__title">SpecForge.AI</span>
      ${displayRuntimeVersion ? `<span class="specforge-cli-sidebar__version">v.${escapeHtmlAttr(displayRuntimeVersion)}</span>` : ""}
    </div>
    <div class="specforge-cli-sidebar__menu" data-cli-sidebar-view-menu>
      <button class="specforge-cli-sidebar__button" type="button" data-cli-sidebar-view-options title="Sidebar view options" aria-label="Sidebar view options" aria-haspopup="menu" aria-expanded="false">☷</button>
      <div class="specforge-cli-sidebar__menu-panel" data-cli-sidebar-view-panel role="menu" hidden>
        <button class="specforge-cli-sidebar__menu-item" type="button" data-cli-parent-command="toggleDroppedUserStories" role="menuitemcheckbox" aria-checked="false"><span class="specforge-cli-sidebar__menu-check" aria-hidden="true"></span><span>Show dropped</span></button>
        <button class="specforge-cli-sidebar__menu-item" type="button" data-cli-parent-command="toggleCompletedUserStories" role="menuitemcheckbox" aria-checked="false"><span class="specforge-cli-sidebar__menu-check" aria-hidden="true"></span><span>Show completed</span></button>
        <button class="specforge-cli-sidebar__menu-item" type="button" data-cli-parent-command="toggleBlockedUserStories" role="menuitemcheckbox" aria-checked="false"><span class="specforge-cli-sidebar__menu-check" aria-hidden="true"></span><span>Show blocked</span></button>
        <button class="specforge-cli-sidebar__menu-item" type="button" data-cli-parent-command="toggleShowHiddenUserStories" role="menuitemcheckbox" aria-checked="false"><span class="specforge-cli-sidebar__menu-check" aria-hidden="true"></span><span>Show hidden</span></button>
        <button class="specforge-cli-sidebar__menu-item" type="button" data-cli-parent-command="toggleSearchIncludesOtherOwners" role="menuitemcheckbox" aria-checked="false"><span class="specforge-cli-sidebar__menu-check" aria-hidden="true"></span><span>Include other owners</span></button>
      </div>
    </div>
    <button class="specforge-cli-sidebar__button" type="button" data-cli-sidebar-settings title="Configuration" aria-label="Configuration">⚙</button>
    <button class="specforge-cli-sidebar__button specforge-cli-sidebar__button--active" type="button" data-cli-sidebar-pin title="Unpin sidebar" aria-label="Unpin sidebar" aria-pressed="true">📌</button>
  </div>
  <div class="specforge-cli-sidebar__surface" data-cli-sidebar-surface aria-live="polite"></div>
</aside>
<div
  class="specforge-cli-sidebar-resizer"
  data-cli-sidebar-resizer
  role="separator"
  aria-orientation="vertical"
  aria-label="Resize sidebar"
  aria-valuemin="280"
  aria-valuemax="520"
  tabindex="0"></div>
<div class="specforge-cli-config-overlay" data-cli-config-overlay hidden>
  <section class="specforge-cli-config-dialog" role="dialog" aria-modal="true" aria-labelledby="specforge-cli-config-title">
    <div class="specforge-cli-config-head">
      <span id="specforge-cli-config-title">SpecForge Configuration</span>
      <button class="specforge-cli-config-close" type="button" data-cli-config-close aria-label="Close configuration">×</button>
    </div>
    <div class="specforge-cli-config-surface" data-cli-config-surface></div>
  </section>
</div>
<div class="specforge-cli-edit-overlay" data-cli-edit-overlay hidden>
  <section class="specforge-cli-edit-dialog" role="dialog" aria-modal="true" aria-labelledby="specforge-cli-edit-title">
    <form class="specforge-cli-edit-form" data-cli-edit-form>
      <div class="specforge-cli-edit-head">
        <div>
          <span class="specforge-cli-edit-kicker">User Story Metadata</span>
          <h2 id="specforge-cli-edit-title">Edit US info</h2>
        </div>
        <button class="specforge-cli-edit-close" type="button" data-cli-edit-close aria-label="Close edit form">×</button>
      </div>
      <input type="hidden" name="usId" />
      <label class="specforge-cli-edit-field">
        <span>Title</span>
        <div class="specforge-cli-edit-field-row">
          <input name="title" type="text" required />
          <span class="specforge-cli-edit-field-error" data-cli-edit-field-error-for="title" hidden></span>
        </div>
      </label>
      <label class="specforge-cli-edit-field">
        <span>Owner</span>
        <div class="specforge-cli-edit-field-row">
          <div class="specforge-cli-edit-owner-row">
            <input name="owner" type="text" required />
            <button class="specforge-cli-edit-owner-assign" type="button" data-cli-edit-assign-to-me>Assign to me</button>
          </div>
          <span class="specforge-cli-edit-field-error" data-cli-edit-field-error-for="owner" hidden></span>
        </div>
      </label>
      <label class="specforge-cli-edit-field">
        <span>Category</span>
        <div class="specforge-cli-edit-field-row">
          <input name="category" type="text" required />
          <span class="specforge-cli-edit-field-error" data-cli-edit-field-error-for="category" hidden></span>
        </div>
      </label>
      <label class="specforge-cli-edit-field">
        <span>Tags</span>
        <input name="tags" type="text" />
      </label>
      <p class="specforge-cli-edit-error" data-cli-edit-error hidden></p>
      <div class="specforge-cli-edit-actions">
        <button class="specforge-cli-edit-secondary" type="button" data-cli-edit-cancel>Cancel</button>
        <button class="specforge-cli-edit-primary" type="submit" data-cli-edit-submit disabled>Save</button>
      </div>
    </form>
  </section>
</div>
<script>
  (() => {
    document.body.classList.add("specforge-cli-with-sidebar");
    const safeStorage = window.__specforgeSafeStorage || {
      getSessionItem: () => null,
      setSessionItem: () => {},
      removeSessionItem: () => {},
      getLocalItem: () => null,
      setLocalItem: () => {},
      removeLocalItem: () => {}
    };
    const collapsedKey = "specforge.cli.sidebar.collapsed";
    const sidebarWidthKey = "specforge.cli.sidebar.width";
    const starredUserStoryStorageKey = "specforge.cli.sidebar.starredUserStoryId";
    const watchingUserStoryIdsStorageKey = "specforge.cli.sidebar.watchingUserStoryIds";
    const hiddenUserStoryIdsStorageKey = "specforge.cli.sidebar.hiddenUserStoryIds";
    const showHiddenStorageKey = "specforge.cli.sidebar.showHiddenUserStories";
    const sidebarMinWidth = 280;
    const sidebarDefaultWidth = 340;
    const sidebarMaxWidth = 520;
    const sidebarMainMinWidth = 480;
    const portalStateApi = window.__specforgePortalState || null;
    const configOverlay = document.querySelector("[data-cli-config-overlay]");
    const configSurface = document.querySelector("[data-cli-config-surface]");
    const editOverlay = document.querySelector("[data-cli-edit-overlay]");
    const editForm = document.querySelector("[data-cli-edit-form]");
    const editError = document.querySelector("[data-cli-edit-error]");
    const editAssignToMe = document.querySelector("[data-cli-edit-assign-to-me]");
    const editSubmit = document.querySelector("[data-cli-edit-submit]");
    const sidebarSurface = document.querySelector("[data-cli-sidebar-surface]");
    const createSurface = document.querySelector("[data-cli-create-surface]");
    const sidebarPin = document.querySelector("[data-cli-sidebar-pin]");
    const sidebarResizer = document.querySelector("[data-cli-sidebar-resizer]");
    const sidebarViewOptionsToggle = document.querySelector("[data-cli-sidebar-view-options]");
    const sidebarViewOptionsPanel = document.querySelector("[data-cli-sidebar-view-panel]");
    const compactSidebarMedia = window.matchMedia("(max-width: 860px)");
    const initialSidebarHtml = ${safeScriptJson(sidebarHtml)};
    const initialCreateHtml = ${safeScriptJson(createHtml)};
    const currentActor = ${safeScriptJson(currentActor)}.trim();
    const renderedWorkflowUsId = ${JSON.stringify(workflow?.usId ?? null)};
    const initialPortalState = portalStateApi?.read?.() || {};
    let sidebarShowsDropped = initialPortalState.sidebarVisibility === "dropped"
      ? true
      : ${showDroppedUserStories ? "true" : "false"};
    let sidebarShowsCompleted = typeof initialPortalState.sidebarCompleted === "boolean"
      ? initialPortalState.sidebarCompleted === true
      : ${showCompletedUserStories ? "true" : "false"};
    let sidebarShowsBlocked = typeof initialPortalState.sidebarBlocked === "boolean"
      ? initialPortalState.sidebarBlocked === true
      : ${showBlockedUserStories ? "true" : "false"};
    let sidebarShowsHidden = typeof initialPortalState.sidebarHiddenVisible === "boolean"
      ? initialPortalState.sidebarHiddenVisible === true
      : ${showHiddenUserStories ? "true" : "false"};
    let sidebarShowsOtherOwners = initialPortalState.sidebarOtherOwners === true;
    let sidebarShowsCreateForm = initialPortalState.createForm === true || ${showCreateForm ? "true" : "false"};
    let createFormFileMode = ${safeScriptJson(createFileMode)};
    let createFormFiles = ${safeScriptJson(normalizeCreateFiles(createFiles))};
    let createFormResetTokenState = ${createFormResetToken};
    let sidebarWatchingUserStoryIds = Array.isArray(initialPortalState.sidebarWatching) && initialPortalState.sidebarWatching.length > 0
      ? initialPortalState.sidebarWatching.map(value => String(value || "").trim().toUpperCase()).filter(Boolean)
      : ${safeScriptJson(watchingUserStoryIds)};
    let sidebarHiddenUserStoryIds = Array.isArray(initialPortalState.sidebarHidden) && initialPortalState.sidebarHidden.length > 0
      ? initialPortalState.sidebarHidden.map(value => String(value || "").trim().toUpperCase()).filter(Boolean)
      : ${safeScriptJson(hiddenUserStoryIds)};
    let pendingSidebarSelectionSync = false;
    let sidebarRenderNonce = 0;
    let createRenderNonce = 0;
    let editInitialState = null;
    let editTouchedFields = new Set();
    let editSubmitAttempted = false;
    let sidebarResizeCleanup = null;
    const normalizedCurrentActor = currentActor.toLowerCase();
    const readStoredSidebarWidth = () => {
      const parsed = Number.parseFloat(String(safeStorage.getLocalItem(sidebarWidthKey) || ""));
      return Number.isFinite(parsed) ? parsed : sidebarDefaultWidth;
    };
    const resolveSidebarMaxWidth = () => {
      const viewportWidth = Number.isFinite(window.innerWidth) ? window.innerWidth : 0;
      return Math.max(sidebarMinWidth, Math.min(sidebarMaxWidth, viewportWidth - sidebarMainMinWidth));
    };
    const clampSidebarWidth = (value) => Math.min(resolveSidebarMaxWidth(), Math.max(sidebarMinWidth, Math.round(value)));
    const applySidebarWidth = (value, persist = true) => {
      const width = clampSidebarWidth(value);
      document.body.style.setProperty("--specforge-cli-sidebar-width", width + "px");
      document.body.style.setProperty("--specforge-cli-sidebar-min-width", sidebarMinWidth + "px");
      const maxWidth = resolveSidebarMaxWidth();
      document.body.style.setProperty("--specforge-cli-sidebar-max-width", maxWidth + "px");
      if (sidebarResizer instanceof HTMLElement) {
        sidebarResizer.setAttribute("aria-valuemin", String(sidebarMinWidth));
        sidebarResizer.setAttribute("aria-valuemax", String(maxWidth));
        sidebarResizer.setAttribute("aria-valuenow", String(width));
        sidebarResizer.setAttribute("aria-valuetext", width + " pixels");
      }
      if (persist) {
        safeStorage.setLocalItem(sidebarWidthKey, String(width));
      }
      return width;
    };
    const stopSidebarResize = () => {
      document.body.classList.remove("specforge-cli-sidebar-resizing");
      if (typeof sidebarResizeCleanup === "function") {
        sidebarResizeCleanup();
      }
      sidebarResizeCleanup = null;
    };
    const startSidebarResize = (pointerId) => {
      if (!(sidebarResizer instanceof HTMLElement) || compactSidebarMedia.matches) {
        return;
      }
      stopSidebarResize();
      document.body.classList.add("specforge-cli-sidebar-resizing");
      if (typeof pointerId === "number" && typeof sidebarResizer.setPointerCapture === "function") {
        try {
          sidebarResizer.setPointerCapture(pointerId);
        } catch {}
      }
      const move = (event) => {
        if (!(event instanceof PointerEvent)) {
          return;
        }
        applySidebarWidth(event.clientX);
      };
      const finish = () => {
        if (typeof pointerId === "number" && typeof sidebarResizer.releasePointerCapture === "function") {
          try {
            sidebarResizer.releasePointerCapture(pointerId);
          } catch {}
        }
        stopSidebarResize();
      };
      window.addEventListener("pointermove", move);
      window.addEventListener("pointerup", finish, { once: true });
      window.addEventListener("pointercancel", finish, { once: true });
      sidebarResizeCleanup = () => {
        window.removeEventListener("pointermove", move);
        window.removeEventListener("pointerup", finish);
        window.removeEventListener("pointercancel", finish);
      };
    };
    const mountInlineDocument = (host, html) => {
      if (!(host instanceof HTMLElement)) {
        return null;
      }
      const parser = new DOMParser();
      const parsed = parser.parseFromString(String(html || ""), "text/html");
      const styles = [
        ...parsed.head.querySelectorAll("style"),
        ...parsed.body.querySelectorAll("style")
      ].map((element) => element.outerHTML).join("");
      const scripts = [
        ...parsed.head.querySelectorAll("script"),
        ...parsed.body.querySelectorAll("script")
      ];
      const bodyClone = parsed.body.cloneNode(true);
      bodyClone.querySelectorAll("script, style").forEach((element) => element.remove());
      host.innerHTML = styles + bodyClone.innerHTML;
      for (const script of scripts) {
        const runtimeScript = document.createElement("script");
        for (const attribute of script.attributes) {
          runtimeScript.setAttribute(attribute.name, attribute.value);
        }
        runtimeScript.textContent = "(function(){\\n" + (script.textContent || "") + "\\n})();";
        host.appendChild(runtimeScript);
      }
      return host;
    };
    window.__specforgeCliSidebarHostDispatch = (message) => {
      window.dispatchEvent(new CustomEvent("specforge-cli-sidebar-message", { detail: message || {} }));
    };
    window.__specforgeCliCreateHostDispatch = (message) => {
      window.dispatchEvent(new CustomEvent("specforge-cli-create-form-message", { detail: message || {} }));
    };
    window.__specforgeCloseConfiguration = () => {
      closeConfiguration();
    };
    const openConfiguration = async (url) => {
      const requestedUrl = new URL(String(url || ${JSON.stringify(configurationPortalUrl)}), window.location.href);
      window.__specforgeEmbeddedConfigurationState = {
        activeTab: requestedUrl.hash.replace(/^#/, "") || "providers"
      };
      const response = await fetch(requestedUrl.pathname, { cache: "no-store" });
      if (!response.ok) {
        throw new Error(await response.text());
      }
      const html = await response.text();
      mountInlineDocument(configSurface, html);
      configOverlay?.removeAttribute("hidden");
    };
    const closeConfiguration = () => {
      configOverlay?.setAttribute("hidden", "");
    };
    const setEditError = (message) => {
      if (!(editError instanceof HTMLElement)) {
        return;
      }
      const normalized = String(message || "").trim();
      editError.textContent = normalized;
      editError.hidden = normalized.length === 0;
    };
    const closeEditUserStoryForm = () => {
      editOverlay?.setAttribute("hidden", "");
      setEditError("");
      if (editForm instanceof HTMLFormElement) {
        editForm.reset();
        editForm.dataset.busy = "false";
      }
      editInitialState = null;
      editTouchedFields = new Set();
      editSubmitAttempted = false;
      updateEditFormValidity();
    };
    const getEditFieldContainer = (name) => {
      const input = editForm instanceof HTMLFormElement ? editForm.elements.namedItem(name) : null;
      return input instanceof HTMLElement ? input.closest(".specforge-cli-edit-field") : null;
    };
    const getEditFieldError = (name) => document.querySelector('[data-cli-edit-field-error-for="' + name + '"]');
    const setEditFieldError = (name, message) => {
      const container = getEditFieldContainer(name);
      const error = getEditFieldError(name);
      const normalized = String(message || "").trim();
      container?.classList.toggle("specforge-cli-edit-field--invalid", normalized.length > 0);
      if (error instanceof HTMLElement) {
        error.textContent = "";
        error.title = normalized;
        error.dataset.visible = normalized.length > 0 ? "true" : "false";
        error.setAttribute("aria-label", normalized || "");
        error.setAttribute("aria-hidden", normalized.length === 0 ? "true" : "false");
      }
    };
    const readEditFormState = () => ({
      title: String(editForm?.elements?.namedItem("title")?.value || "").trim(),
      owner: String(editForm?.elements?.namedItem("owner")?.value || "").trim(),
      category: String(editForm?.elements?.namedItem("category")?.value || "").trim(),
      tags: String(editForm?.elements?.namedItem("tags")?.value || "").trim()
    });
    const validateEditForm = () => {
      const state = readEditFormState();
      const errors = {};
      if (!state.title) {
        errors.title = "Title is required.";
      } else if (state.title.length < 8) {
        errors.title = "Title must be at least 8 characters.";
      }

      if (!state.owner) {
        errors.owner = "Owner is required.";
      } else if (state.owner.length < 4) {
        errors.owner = "Owner must be at least 4 characters.";
      }

      if (!state.category) {
        errors.category = "Category is required.";
      } else if (state.category.length < 2) {
        errors.category = "Category must be at least 2 characters.";
      }

      return { state, errors };
    };
    const updateEditFormValidity = () => {
      const { state, errors } = validateEditForm();
      const visibleTitleError = editSubmitAttempted || editTouchedFields.has("title") ? (errors.title || "") : "";
      const visibleOwnerError = editSubmitAttempted || editTouchedFields.has("owner") ? (errors.owner || "") : "";
      const visibleCategoryError = editSubmitAttempted || editTouchedFields.has("category") ? (errors.category || "") : "";
      setEditFieldError("title", visibleTitleError);
      setEditFieldError("owner", visibleOwnerError);
      setEditFieldError("category", visibleCategoryError);
      const isDirty = editInitialState !== null
        && (state.title !== editInitialState.title
          || state.owner !== editInitialState.owner
          || state.category !== editInitialState.category
          || state.tags !== editInitialState.tags);
      const isBusy = editForm instanceof HTMLFormElement && editForm.dataset.busy === "true";
      if (editSubmit instanceof HTMLButtonElement) {
        editSubmit.disabled = isBusy || !isDirty || Object.keys(errors).length > 0;
        editSubmit.title = !isDirty
          ? "Change at least one field to save."
          : (errors.title || errors.owner || errors.category || "");
      }
      return { state, errors, isDirty };
    };
    const updateAssignToMeVisibility = () => {
      if (!(editForm instanceof HTMLFormElement) || !(editAssignToMe instanceof HTMLElement)) {
        return;
      }
      const ownerInput = editForm.elements.namedItem("owner");
      const normalizedOwner = String(ownerInput?.value || "").trim().toLowerCase();
      editAssignToMe.disabled = normalizedCurrentActor.length === 0 || normalizedOwner === normalizedCurrentActor;
    };
    const openEditUserStoryForm = (message) => {
      if (!(editForm instanceof HTMLFormElement)) {
        return;
      }
      editForm.dataset.busy = "false";
      editForm.elements.namedItem("usId").value = String(message.usId || "");
      editForm.elements.namedItem("title").value = String(message.title || "");
      editForm.elements.namedItem("owner").value = String(message.owner || currentActor);
      editForm.elements.namedItem("category").value = String(message.category || "");
      editForm.elements.namedItem("tags").value = String(message.tags || "");
      editInitialState = readEditFormState();
      editTouchedFields = new Set();
      editSubmitAttempted = false;
      setEditError("");
      updateAssignToMeVisibility();
      updateEditFormValidity();
      editOverlay?.removeAttribute("hidden");
      editForm.elements.namedItem("title")?.focus();
    };
    const getStarredUserStoryId = () => {
      try { return localStorage.getItem(starredUserStoryStorageKey) || null; }
      catch { return safeStorage.getLocalItem(starredUserStoryStorageKey) || null; }
    };
    const setStarredUserStoryId = (usId) => {
      if (usId) safeStorage.setLocalItem(starredUserStoryStorageKey, usId);
      else safeStorage.removeLocalItem(starredUserStoryStorageKey);
    };
    const normalizeUserStoryId = (value) => String(value || "").trim().toUpperCase();
    const closeSidebarViewOptions = () => {
      sidebarViewOptionsPanel?.setAttribute("hidden", "");
      sidebarViewOptionsToggle?.setAttribute("aria-expanded", "false");
    };
    const toggleSidebarViewOptions = () => {
      if (!(sidebarViewOptionsPanel instanceof HTMLElement)) {
        return;
      }
      const willOpen = sidebarViewOptionsPanel.hasAttribute("hidden");
      if (willOpen) {
        sidebarViewOptionsPanel.removeAttribute("hidden");
        sidebarViewOptionsToggle?.setAttribute("aria-expanded", "true");
      } else {
        closeSidebarViewOptions();
      }
    };
    const parseUserStoryIdList = (value) => Array.from(new Set(String(value || "")
      .split(",")
      .map(normalizeUserStoryId)
      .filter(Boolean)));
    const readUserStoryIdList = (storageKey) => {
      return parseUserStoryIdList(safeStorage.getLocalItem(storageKey) || "");
    };
    const writeUserStoryIdList = (storageKey, usIds) => {
      if (Array.isArray(usIds) && usIds.length > 0) {
        safeStorage.setLocalItem(storageKey, usIds.join(","));
      } else {
        safeStorage.removeLocalItem(storageKey);
      }
    };
    const readCurrentPortalUsId = () => normalizeUserStoryId(new URL(window.location.href).searchParams.get("usId")) || normalizeUserStoryId(renderedWorkflowUsId);
    const syncPortalUiState = (overrides) => {
      const nextState = {
        usId: readCurrentPortalUsId() || null,
        sidebarVisibility: sidebarShowsDropped ? "dropped" : "active",
        sidebarCompleted: sidebarShowsCompleted,
        sidebarBlocked: sidebarShowsBlocked,
        createForm: sidebarShowsCreateForm,
        sidebarHiddenVisible: sidebarShowsHidden,
        sidebarOtherOwners: sidebarShowsOtherOwners,
        sidebarWatching: sidebarWatchingUserStoryIds,
        sidebarHidden: sidebarHiddenUserStoryIds,
        ...(overrides && typeof overrides === "object" ? overrides : {})
      };
      return portalStateApi?.update?.(nextState) || nextState;
    };
    const buildPortalRequestUrl = (overrides) =>
      portalStateApi?.buildRequestUrl?.(syncPortalUiState(overrides))?.url
      || new URL(window.location.href);
    const persistSidebarLists = () => {
      writeUserStoryIdList(watchingUserStoryIdsStorageKey, sidebarWatchingUserStoryIds);
      writeUserStoryIdList(hiddenUserStoryIdsStorageKey, sidebarHiddenUserStoryIds);
      safeStorage.setLocalItem(showHiddenStorageKey, sidebarShowsHidden ? "true" : "false");
    };
    const hydrateSidebarListsFromStorage = () => {
      let shouldReload = false;
      const storedPortalState = portalStateApi?.read?.() || {};

      if (Array.isArray(storedPortalState.sidebarWatching)) {
        const nextWatching = parseUserStoryIdList(storedPortalState.sidebarWatching);
        if (nextWatching.join(",") !== sidebarWatchingUserStoryIds.join(",")) {
          sidebarWatchingUserStoryIds = nextWatching;
          shouldReload = true;
        }
      } else {
        const storedWatching = readUserStoryIdList(watchingUserStoryIdsStorageKey);
        if (storedWatching.length > 0) {
          sidebarWatchingUserStoryIds = storedWatching;
          shouldReload = true;
        }
      }

      if (Array.isArray(storedPortalState.sidebarHidden)) {
        const nextHidden = parseUserStoryIdList(storedPortalState.sidebarHidden);
        if (nextHidden.join(",") !== sidebarHiddenUserStoryIds.join(",")) {
          sidebarHiddenUserStoryIds = nextHidden;
          shouldReload = true;
        }
      } else {
        const storedHidden = readUserStoryIdList(hiddenUserStoryIdsStorageKey);
        if (storedHidden.length > 0) {
          sidebarHiddenUserStoryIds = storedHidden;
          shouldReload = true;
        }
      }

      if (typeof storedPortalState.sidebarHiddenVisible === "boolean") {
        if (storedPortalState.sidebarHiddenVisible !== sidebarShowsHidden) {
          sidebarShowsHidden = storedPortalState.sidebarHiddenVisible;
          shouldReload = true;
        }
      } else {
        if (safeStorage.getLocalItem(showHiddenStorageKey) === "true") {
          sidebarShowsHidden = true;
          shouldReload = true;
        }
      }

      persistSidebarLists();
      if (!shouldReload) {
        return false;
      }

      window.location.replace(buildPortalRequestUrl().toString());
      return true;
    };
    const buildSidebarRequestUrl = () => {
      const url = new URL("/api/sidebar-html", window.location.href);
      url.searchParams.set("sidebarVisibility", sidebarShowsDropped ? "dropped" : "active");
      if (sidebarShowsCompleted) {
        url.searchParams.set("sidebarCompleted", "true");
      }
      if (sidebarShowsBlocked) {
        url.searchParams.set("sidebarBlocked", "true");
      }
      if (sidebarShowsHidden) {
        url.searchParams.set("sidebarHiddenVisible", "true");
      }
      if (sidebarShowsOtherOwners) {
        url.searchParams.set("sidebarOtherOwners", "true");
      }
      if (sidebarWatchingUserStoryIds.length > 0) {
        url.searchParams.set("sidebarWatching", sidebarWatchingUserStoryIds.join(","));
      }
      if (sidebarHiddenUserStoryIds.length > 0) {
        url.searchParams.set("sidebarHidden", sidebarHiddenUserStoryIds.join(","));
      }
      return url;
    };
    const replaceSidebarFrame = async () => {
      if (!(sidebarSurface instanceof HTMLElement)) {
        return false;
      }
      const nonce = ++sidebarRenderNonce;
      const response = await fetch(buildSidebarRequestUrl(), { cache: "no-store" });
      if (!response.ok) {
        throw new Error(await response.text());
      }
      const nextHtml = await response.text();
      if (nonce !== sidebarRenderNonce) {
        return false;
      }
      mountInlineDocument(sidebarSurface, nextHtml);
      refreshSidebarBindings();
      return true;
    };
    const replaceCreateSurface = async () => {
      if (!(createSurface instanceof HTMLElement)) {
        return false;
      }
      const nonce = ++createRenderNonce;
      const response = await fetch("/api/create-form-html", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({
          createFileMode: createFormFileMode,
          createFiles: createFormFiles,
          createFormResetToken: createFormResetTokenState
        })
      });
      if (!response.ok) {
        throw new Error(await response.text());
      }
      const nextHtml = await response.text();
      if (nonce !== createRenderNonce) {
        return false;
      }
      mountInlineDocument(createSurface, nextHtml);
      return true;
    };
    const dispatchCreateSurfaceMessage = (message) => {
      window.postMessage(message, "*");
    };
    const normalizeCreateFileKind = (value) => value === "attachment" ? "attachment" : "context";
    const openLocalFiles = ({ multiple, kind, readAsText }) => new Promise((resolve) => {
      const input = document.createElement("input");
      input.type = "file";
      input.multiple = multiple === true;
      input.style.display = "none";
      document.body.appendChild(input);
      input.addEventListener("change", async () => {
        try {
          const files = Array.from(input.files || []);
          if (readAsText) {
            const file = files[0];
            if (!file) {
              resolve(null);
              return;
            }
            const sourceText = await file.text();
            resolve({
              sourceText,
              suggestedTitle: (/^#\s+(.+)$/m.exec(sourceText)?.[1] || "").trim(),
              sourcePath: file.name
            });
            return;
          }
          const loaded = await Promise.all(files.map((file, index) => new Promise((fileResolve, fileReject) => {
            const reader = new FileReader();
            reader.onload = () => {
              const result = String(reader.result || "");
              const marker = "base64,";
              const base64Index = result.indexOf(marker);
              fileResolve({
                sourcePath: "upload-" + Date.now() + "-" + index + "-" + file.name,
                name: file.name,
                kind: normalizeCreateFileKind(kind),
                base64Content: base64Index >= 0 ? result.slice(base64Index + marker.length) : ""
              });
            };
            reader.onerror = () => fileReject(reader.error || new Error("File could not be read."));
            reader.readAsDataURL(file);
          })));
          resolve(loaded);
        } finally {
          input.remove();
        }
      }, { once: true });
      input.click();
    });
    const getSidebarDocument = () => sidebarSurface instanceof HTMLElement ? sidebarSurface : null;
    const buildSidebarSynchronizedUrl = (usId) => {
      return buildPortalRequestUrl({
        usId: usId || null,
        selectedPhaseId: null,
        artifactFocus: null
      });
    };
    const syncPortalSelectionWithSidebarScope = () => {
      pendingSidebarSelectionSync = false;
      const doc = getSidebarDocument();
      if (!doc) {
        return false;
      }
      const visibleStoryButtons = Array.from(
        doc.querySelectorAll('button[data-command="openWorkflow"][data-us-id]')
      ).filter((button) => button instanceof HTMLButtonElement && !button.hidden);
      const firstVisibleUsId = visibleStoryButtons[0]?.getAttribute("data-us-id") || null;
      const url = new URL(window.location.href);
      const currentUsId = normalizeUserStoryId(url.searchParams.get("usId"));
      const hasCurrentSelectionVisible = currentUsId.length > 0
        && visibleStoryButtons.some((button) => normalizeUserStoryId(button.getAttribute("data-us-id")) === currentUsId);

      if (!firstVisibleUsId) {
        if (currentUsId || renderedWorkflowUsId) {
          window.__specforgePortalLifecycle.navigate(
            buildSidebarSynchronizedUrl(null).toString(),
            "sidebar-scope-cleared-selection",
            { detail: "Sidebar scope no longer exposes any selectable user story." }
          );
          return true;
        }
        return false;
      }

      if (hasCurrentSelectionVisible) {
        if (normalizeUserStoryId(renderedWorkflowUsId) !== currentUsId) {
          window.__specforgePortalLifecycle.reload("sidebar-scope-rehydrated-selection", {
            renderedWorkflowUsId: currentUsId || null,
            detail: "Reload visible user story selection after sidebar scope swap."
          });
          return true;
        }
        return false;
      }

      window.__specforgePortalLifecycle.navigate(
        buildSidebarSynchronizedUrl(firstVisibleUsId).toString(),
        "sidebar-scope-selected-first-visible",
        {
          renderedWorkflowUsId: firstVisibleUsId,
          detail: "Select the first user story visible in the active sidebar scope."
        }
      );
      return true;
    };
    const queueSidebarSelectionSync = () => {
      pendingSidebarSelectionSync = true;
      window.setTimeout(() => {
        if (!pendingSidebarSelectionSync) {
          return;
        }
        syncPortalSelectionWithSidebarScope();
      }, 0);
    };
    const updateSidebarViewOptionsUi = () => {
      const activeMap = {
        toggleDroppedUserStories: sidebarShowsDropped,
        toggleCompletedUserStories: sidebarShowsCompleted,
        toggleBlockedUserStories: sidebarShowsBlocked,
        toggleShowHiddenUserStories: sidebarShowsHidden,
        toggleSearchIncludesOtherOwners: sidebarShowsOtherOwners
      };
      let hasActiveFilter = false;
      for (const button of document.querySelectorAll("[data-cli-parent-command]")) {
        if (!(button instanceof HTMLButtonElement)) {
          continue;
        }
        const command = String(button.dataset.cliParentCommand || "");
        const active = activeMap[command] === true;
        hasActiveFilter = hasActiveFilter || active;
        button.classList.toggle("specforge-cli-sidebar__menu-item--active", active);
        button.setAttribute("aria-checked", active ? "true" : "false");
        const check = button.querySelector(".specforge-cli-sidebar__menu-check");
        if (check) {
          check.textContent = active ? "✓" : "";
        }
      }
      sidebarViewOptionsToggle?.classList.toggle("specforge-cli-sidebar__button--active", hasActiveFilter);
    };
    const applySidebarScopeCommand = (command) => {
      switch (command) {
        case "toggleDroppedUserStories":
          sidebarShowsDropped = !sidebarShowsDropped;
          sidebarShowsCompleted = false;
          sidebarShowsBlocked = false;
          if (sidebarShowsDropped) {
            sidebarShowsCreateForm = false;
          }
          break;
        case "toggleCompletedUserStories":
          sidebarShowsDropped = false;
          sidebarShowsCompleted = !sidebarShowsCompleted;
          break;
        case "toggleBlockedUserStories":
          sidebarShowsDropped = false;
          sidebarShowsBlocked = !sidebarShowsBlocked;
          break;
        case "toggleShowHiddenUserStories":
          sidebarShowsHidden = !sidebarShowsHidden;
          persistSidebarLists();
          break;
        case "toggleSearchIncludesOtherOwners":
          sidebarShowsDropped = false;
          sidebarShowsOtherOwners = !sidebarShowsOtherOwners;
          break;
        default:
          return false;
      }
      syncPortalUiState();
      void replaceSidebarFrame().then((rendered) => {
        if (rendered) {
          queueSidebarSelectionSync();
        }
      });
      updateSidebarViewOptionsUi();
      return true;
    };
    const applySidebarStarredUserStory = () => {
      const starredUserStoryId = getStarredUserStoryId();
      const doc = getSidebarDocument();
      if (!doc) return;
      for (const button of doc.querySelectorAll('[data-command="toggleStarredUserStory"][data-us-id]')) {
        const usId = button.getAttribute("data-us-id") || "";
        const active = usId === starredUserStoryId;
        const label = (active ? "Unstar " : "Star ") + usId;
        button.classList.toggle("story-star--active", active);
        button.setAttribute("title", label);
        button.setAttribute("aria-label", label);
        const icon = button.querySelector("[aria-hidden='true']");
        if (icon) icon.textContent = active ? "★" : "☆";
      }
    };
    const bridgeSidebarScopeControls = () => {
      const doc = getSidebarDocument();
      if (!doc) return;
      const commandButtons = [
        "toggleDroppedUserStories",
        "toggleCompletedUserStories",
        "toggleBlockedUserStories",
        "toggleShowHiddenUserStories",
        "toggleSearchIncludesOtherOwners"
      ];
      for (const command of commandButtons) {
        for (const button of doc.querySelectorAll('[data-command="' + command + '"]')) {
          if (!(button instanceof HTMLButtonElement) || button.dataset.portalBound === "true") {
            continue;
          }
          button.dataset.portalBound = "true";
          button.addEventListener("click", (event) => {
            event.preventDefault();
            event.stopPropagation();
            applySidebarScopeCommand(command);
          });
        }
      }
      for (const button of doc.querySelectorAll('[data-command="showCreateForm"]')) {
        if (!(button instanceof HTMLButtonElement) || button.dataset.portalBound === "true") {
          continue;
        }
        button.dataset.portalBound = "true";
        button.addEventListener("click", (event) => {
          event.preventDefault();
          event.stopPropagation();
          sidebarShowsDropped = false;
          sidebarShowsCreateForm = true;
          window.__specforgePortalLifecycle.navigate(buildPortalRequestUrl({ createForm: true }).toString(), "open-create-user-story", {
            detail: "Open create user story in main portal surface."
          });
        });
      }
      for (const button of doc.querySelectorAll('[data-command="hideCreateForm"]')) {
        if (!(button instanceof HTMLButtonElement) || button.dataset.portalBound === "true") {
          continue;
        }
        button.dataset.portalBound = "true";
        button.addEventListener("click", (event) => {
          event.preventDefault();
          event.stopPropagation();
          sidebarShowsCreateForm = false;
          window.__specforgePortalLifecycle.navigate(buildPortalRequestUrl({ createForm: false }).toString(), "close-create-user-story", {
            detail: "Close create user story main portal surface."
          });
        });
      }
      for (const button of doc.querySelectorAll('[data-command="openWorkflow"][data-us-id]')) {
        if (!(button instanceof HTMLButtonElement) || button.dataset.portalBound === "true") {
          continue;
        }
        button.dataset.portalBound = "true";
        button.addEventListener("click", (event) => {
          event.preventDefault();
          event.stopPropagation();
          const usId = button.getAttribute("data-us-id");
          if (usId) {
            navigateToUserStory(usId, null, null);
          }
        });
      }
    };
    const refreshSidebarBindings = () => {
      applySidebarStarredUserStory();
      bridgeSidebarScopeControls();
      if (pendingSidebarSelectionSync) {
        syncPortalSelectionWithSidebarScope();
      }
    };
    const applyCollapsed = (collapsed) => {
      document.body.classList.toggle("specforge-cli-sidebar-collapsed", collapsed);
      if (collapsed) {
        stopSidebarResize();
      } else {
        applySidebarWidth(readStoredSidebarWidth(), false);
      }
      if (sidebarPin) {
        sidebarPin.classList.toggle("specforge-cli-sidebar__button--active", !collapsed);
        sidebarPin.setAttribute("title", collapsed ? "Pin sidebar" : "Unpin sidebar");
        sidebarPin.setAttribute("aria-label", collapsed ? "Pin sidebar" : "Unpin sidebar");
        sidebarPin.setAttribute("aria-pressed", collapsed ? "false" : "true");
      }
      safeStorage.setLocalItem(collapsedKey, collapsed ? "true" : "false");
    };
    applySidebarWidth(readStoredSidebarWidth(), false);
    applyCollapsed(safeStorage.getLocalItem(collapsedKey) === "true");
    if (hydrateSidebarListsFromStorage()) {
      return;
    }
    mountInlineDocument(sidebarSurface, initialSidebarHtml);
    updateSidebarViewOptionsUi();
    queueSidebarSelectionSync();
    sidebarPin?.addEventListener("click", () => applyCollapsed(!document.body.classList.contains("specforge-cli-sidebar-collapsed")));
    sidebarResizer?.addEventListener("pointerdown", event => {
      if (!(event instanceof PointerEvent) || event.button !== 0) {
        return;
      }
      event.preventDefault();
      startSidebarResize(event.pointerId);
    });
    sidebarResizer?.addEventListener("keydown", event => {
      if (!(event instanceof KeyboardEvent)) {
        return;
      }
      const currentWidth = clampSidebarWidth(readStoredSidebarWidth());
      if (event.key === "ArrowLeft") {
        event.preventDefault();
        applySidebarWidth(currentWidth - 24);
        return;
      }
      if (event.key === "ArrowRight") {
        event.preventDefault();
        applySidebarWidth(currentWidth + 24);
        return;
      }
      if (event.key === "Home") {
        event.preventDefault();
        applySidebarWidth(sidebarMinWidth);
        return;
      }
      if (event.key === "End") {
        event.preventDefault();
        applySidebarWidth(resolveSidebarMaxWidth());
      }
    });
    window.addEventListener("resize", () => {
      stopSidebarResize();
      applySidebarWidth(readStoredSidebarWidth(), false);
    });
    sidebarViewOptionsToggle?.addEventListener("click", toggleSidebarViewOptions);
    sidebarViewOptionsPanel?.addEventListener("click", event => {
      const button = event.target instanceof Element
        ? event.target.closest("[data-cli-parent-command]")
        : null;
      if (!(button instanceof HTMLButtonElement)) {
        return;
      }
      event.preventDefault();
      const command = String(button.dataset.cliParentCommand || "");
      if (applySidebarScopeCommand(command)) {
        closeSidebarViewOptions();
      }
    });
    document.addEventListener("pointerdown", event => {
      if (!(event.target instanceof Node)) {
        return;
      }
      if (sidebarViewOptionsPanel?.contains(event.target) || sidebarViewOptionsToggle?.contains(event.target)) {
        return;
      }
      closeSidebarViewOptions();
    });
    document.querySelector("[data-cli-sidebar-settings]")?.addEventListener("click", () => {
      openConfiguration(${JSON.stringify(configurationPortalUrl)}).catch(error => {
        window.alert(error instanceof Error ? error.message : String(error));
      });
    });
    refreshSidebarBindings();
    document.querySelector("[data-cli-config-close]")?.addEventListener("click", closeConfiguration);
    configOverlay?.addEventListener("click", event => {
      if (event.target === configOverlay) closeConfiguration();
    });
    window.addEventListener("message", event => {
      if (event.data?.source !== "specforge-cli-configuration") {
        return;
      }
      const message = event.data.message || {};
      if (message.command === "closeConfiguration") {
        closeConfiguration();
      }
    });
    document.querySelector("[data-cli-edit-close]")?.addEventListener("click", closeEditUserStoryForm);
    document.querySelector("[data-cli-edit-cancel]")?.addEventListener("click", closeEditUserStoryForm);
    editForm?.addEventListener("submit", event => {
      event.preventDefault();
      if (!(editForm instanceof HTMLFormElement) || editForm.dataset.busy === "true") {
        return;
      }
      const usId = String(editForm.elements.namedItem("usId")?.value || "").trim();
      const title = String(editForm.elements.namedItem("title")?.value || "").trim();
      const owner = String(editForm.elements.namedItem("owner")?.value || "").trim();
      const category = String(editForm.elements.namedItem("category")?.value || "").trim();
      const tags = String(editForm.elements.namedItem("tags")?.value || "")
        .split(",")
        .map(item => item.trim().toLowerCase())
        .filter(Boolean);
      const validation = updateEditFormValidity();
      if (!usId) {
        setEditError("User story id is required.");
        return;
      }
      if (!validation.isDirty) {
        setEditError("Change at least one field before saving.");
        return;
      }
      if (Object.keys(validation.errors).length > 0) {
        editSubmitAttempted = true;
        updateEditFormValidity();
        setEditError(validation.errors.title || validation.errors.owner || validation.errors.category || "Please fix the highlighted fields.");
        return;
      }
      editForm.dataset.busy = "true";
      updateEditFormValidity();
      setEditError("");
      requestJson("/api/update-user-story-info", { usId, title, owner, category, tags, actor: currentActor })
        .then(() => {
          closeEditUserStoryForm();
          window.__specforgePortalLifecycle.reload("post-update-user-story-info", {
            detail: "User story metadata updated.",
            renderedWorkflowUsId: usId
          });
        })
        .catch(error => {
          editForm.dataset.busy = "false";
          updateEditFormValidity();
          setEditError(error instanceof Error ? error.message : String(error));
        });
    });
    ["title", "owner", "category"].forEach((fieldName) => {
      editForm?.elements?.namedItem(fieldName)?.addEventListener?.("input", () => {
        editTouchedFields.add(fieldName);
        if (fieldName === "owner") {
          updateAssignToMeVisibility();
        }
        setEditError("");
        updateEditFormValidity();
      });
    });
    editAssignToMe?.addEventListener("click", () => {
      if (!(editForm instanceof HTMLFormElement)) {
        return;
      }
      const ownerInput = editForm.elements.namedItem("owner");
      if (!(ownerInput instanceof HTMLInputElement) || currentActor.length === 0) {
        return;
      }
      ownerInput.value = currentActor;
      editTouchedFields.add("owner");
      updateAssignToMeVisibility();
      setEditError("");
      updateEditFormValidity();
      ownerInput.focus();
      ownerInput.select();
    });
    window.addEventListener("keydown", event => {
      if (event.key === "Escape" && !configOverlay?.hasAttribute("hidden")) closeConfiguration();
      if (event.key === "Escape" && !editOverlay?.hasAttribute("hidden")) closeEditUserStoryForm();
    });
    const requestJson = (endpoint, body) => fetch(endpoint, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify(body)
    }).then(response => response.ok ? response.json() : response.text().then(text => Promise.reject(new Error(text))));
    const focusUserStorySourceSection = () => {
      const section = document.querySelector("[data-user-story-source-section]");
      if (!(section instanceof HTMLElement)) {
        return false;
      }
      section.scrollIntoView({ behavior: "smooth", block: "start" });
      section.classList.add("specforge-cli-source-focus");
      window.setTimeout(() => section.classList.remove("specforge-cli-source-focus"), 1600);
      return true;
    };
    const applyArtifactFocusFromState = () => {
      if (portalStateApi?.read?.()?.artifactFocus !== "source") {
        return;
      }
      window.requestAnimationFrame(() => {
        focusUserStorySourceSection();
        portalStateApi?.update?.({ artifactFocus: null });
      });
    };
    const reloadWithSidebarState = () => {
      persistSidebarLists();
      syncPortalUiState();
      window.__specforgePortalLifecycle.navigate(buildPortalRequestUrl().toString(), "sidebar-state-changed", {
        detail: "Sidebar scope or visibility changed."
      });
    };
    const navigateToUserStory = (usId, selectedPhaseId, artifactFocus) => {
      const targetUrl = buildPortalRequestUrl({
        usId,
        selectedPhaseId: selectedPhaseId || null,
        artifactFocus: artifactFocus || null
      }).toString();
      if (targetUrl === window.location.href) {
        window.__specforgePortalLifecycle.reload("navigate-to-user-story-same-url", {
          renderedWorkflowUsId: usId,
          selectedPhaseId: selectedPhaseId || null,
          detail: "Reload current user story because the main shell is stale."
        });
        return;
      }
      window.__specforgePortalLifecycle.navigate(targetUrl, "navigate-to-user-story", {
        renderedWorkflowUsId: usId,
        selectedPhaseId: selectedPhaseId || null,
        detail: artifactFocus ? "Navigate to user story with artifact focus." : "Navigate to user story."
      });
    };
    const sidebarMessageHandlers = {
      openWorkflow(message) {
        if (!message.usId) {
          return false;
        }
        navigateToUserStory(message.usId, null, null);
        return true;
      },
      openMainArtifact(message) {
        if (!message.usId) {
          return false;
        }
        const currentState = portalStateApi?.read?.() || {};
        if (readCurrentPortalUsId() === normalizeUserStoryId(message.usId) && currentState.selectedPhaseId === "capture") {
          if (focusUserStorySourceSection()) {
            return true;
          }
        }
        navigateToUserStory(message.usId, "capture", "source");
        return true;
      },
      showEditUserStoryForm(message) {
        if (!message.usId) {
          return false;
        }
        openEditUserStoryForm(message);
        return true;
      },
      showCreateForm() {
        if (sidebarShowsDropped) {
          sidebarShowsDropped = false;
        }
        sidebarShowsCreateForm = true;
        window.__specforgePortalLifecycle.navigate(buildPortalRequestUrl({ createForm: true }).toString(), "open-create-user-story", {
          detail: "Open create user story in main portal surface."
        });
        return true;
      },
      hideCreateForm() {
        if (!sidebarShowsCreateForm) {
          return false;
        }
        sidebarShowsCreateForm = false;
        window.__specforgePortalLifecycle.navigate(buildPortalRequestUrl({ createForm: false }).toString(), "close-create-user-story", {
          detail: "Close create user story main portal surface."
        });
        return true;
      },
      toggleStarredUserStory(message) {
        if (!message.usId) {
          return false;
        }
        const current = getStarredUserStoryId();
        setStarredUserStoryId(current === message.usId ? null : message.usId);
        applySidebarStarredUserStory();
        return true;
      },
      toggleDroppedUserStories() {
        return applySidebarScopeCommand("toggleDroppedUserStories");
      },
      toggleCompletedUserStories() {
        return applySidebarScopeCommand("toggleCompletedUserStories");
      },
      toggleBlockedUserStories() {
        return applySidebarScopeCommand("toggleBlockedUserStories");
      },
      toggleShowHiddenUserStories() {
        return applySidebarScopeCommand("toggleShowHiddenUserStories");
      },
      toggleSearchIncludesOtherOwners() {
        return applySidebarScopeCommand("toggleSearchIncludesOtherOwners");
      },
      toggleSidebarVisibilityUserStory(message) {
        if (!message.usId) {
          return false;
        }
        const normalizedUsId = normalizeUserStoryId(message.usId);
        const normalizedOwner = String(message.owner || "").trim().toLowerCase();
        const isOwnedByCurrentActor = normalizedOwner === normalizedCurrentActor;
        const isHidden = sidebarHiddenUserStoryIds.includes(normalizedUsId);
        const isWatched = sidebarWatchingUserStoryIds.includes(normalizedUsId);
        const isVisibleInSidebar = !isHidden && (sidebarShowsOtherOwners || isWatched || isOwnedByCurrentActor);
        if (isVisibleInSidebar) {
          sidebarHiddenUserStoryIds = sidebarHiddenUserStoryIds.includes(normalizedUsId)
            ? sidebarHiddenUserStoryIds
            : [...sidebarHiddenUserStoryIds, normalizedUsId];
          sidebarWatchingUserStoryIds = sidebarWatchingUserStoryIds.filter(usId => usId !== normalizedUsId);
        } else {
          sidebarHiddenUserStoryIds = sidebarHiddenUserStoryIds.filter(usId => usId !== normalizedUsId);
          if (!isOwnedByCurrentActor && !sidebarWatchingUserStoryIds.includes(normalizedUsId)) {
            sidebarWatchingUserStoryIds = [...sidebarWatchingUserStoryIds, normalizedUsId];
          }
        }
        reloadWithSidebarState();
        return true;
      },
      resetUserStoryToCapture(message) {
        if (!message.usId) {
          return false;
        }
        if (!window.confirm("Reset " + message.usId + " to capture and delete all derived artifacts after the source?")) {
          return true;
        }
        requestJson("/api/reset-user-story-to-capture", { usId: message.usId, actor: currentActor })
          .then(() => {
            window.__specforgePortalLifecycle.reload("post-reset-user-story-to-capture", {
              renderedWorkflowUsId: message.usId,
              detail: "User story reset to capture."
            });
          })
          .catch(error => {
            window.alert(error instanceof Error ? error.message : String(error));
          });
        return true;
      },
      analyzeRepairUserStory(message) {
        if (!message.usId) {
          return false;
        }
        requestJson("/api/analyze-user-story-lineage", { usId: message.usId, actor: currentActor })
          .then((analysis) => {
            if (analysis.status === "clean") {
              window.alert(message.usId + " lineage is clean.");
              return null;
            }
            if (analysis.status !== "inconsistent" || !Array.isArray(analysis.deprecatedCandidatePaths) || analysis.deprecatedCandidatePaths.length === 0 || !analysis.recommendedTargetPhase) {
              const firstFinding = Array.isArray(analysis.findings) ? analysis.findings[0] : null;
              throw new Error(firstFinding?.summary || (message.usId + " lineage needs manual review."));
            }
            const confirmation = window.confirm(
              message.usId + " lineage is inconsistent. Repair will archive "
              + analysis.deprecatedCandidatePaths.length
              + " artifact(s) and return the workflow to "
              + analysis.recommendedTargetPhase
              + "."
            );
            if (!confirmation) {
              return null;
            }
            return requestJson("/api/repair-user-story-lineage", { usId: message.usId, actor: currentActor });
          })
          .then((result) => {
            if (!result) {
              return;
            }
            window.__specforgePortalLifecycle.reload("post-repair-user-story-lineage", {
              renderedWorkflowUsId: message.usId,
              detail: "User story lineage repaired."
            });
          })
          .catch(error => {
            window.alert(error instanceof Error ? error.message : String(error));
          });
        return true;
      },
      dropUserStory(message) {
        if (!message.usId) {
          return false;
        }
        if (!window.confirm("Drop " + message.usId + "? It will be marked as deleted and hidden from the SpecForge panel.")) {
          return true;
        }
        requestJson("/api/drop-user-story", { usId: message.usId })
          .then(() => {
            sidebarShowsDropped = false;
            syncPortalUiState({ sidebarVisibility: "active" });
            window.__specforgePortalLifecycle.navigate(buildPortalRequestUrl().toString(), "post-drop-user-story", {
              renderedWorkflowUsId: message.usId,
              detail: "User story dropped from sidebar."
            });
          })
          .catch(error => {
            window.alert(error instanceof Error ? error.message : String(error));
          });
        return true;
      },
      recoverUserStory(message) {
        if (!message.usId) {
          return false;
        }
        requestJson("/api/recover-user-story", { usId: message.usId })
          .then(() => {
            syncPortalUiState();
            window.__specforgePortalLifecycle.navigate(buildPortalRequestUrl().toString(), "post-recover-user-story", {
              renderedWorkflowUsId: message.usId,
              detail: "User story recovered into sidebar."
            });
          })
          .catch(error => {
            window.alert(error instanceof Error ? error.message : String(error));
          });
        return true;
      },
      openExecutionSettings() {
        openConfiguration(${JSON.stringify(configurationProvidersUrl)}).catch(error => {
          window.alert(error instanceof Error ? error.message : String(error));
        });
        return true;
      },
      setCreateFileMode(message) {
        createFormFileMode = normalizeCreateFileKind(message.kind);
        void replaceCreateSurface().then(() => {
          dispatchCreateSurfaceMessage({ command: "updateCreateSourceReferences", files: [] });
        });
        return true;
      },
      addCreateFiles(message) {
        openLocalFiles({ multiple: true, kind: message.kind, readAsText: false })
          .then((files) => {
            if (!Array.isArray(files) || files.length === 0) {
              return;
            }
            const fileMap = new Map(createFormFiles.map((file) => [file.sourcePath, file]));
            for (const file of files) {
              fileMap.set(file.sourcePath, file);
            }
            createFormFiles = [...fileMap.values()].sort((left, right) => left.name.localeCompare(right.name));
            void replaceCreateSurface();
          })
          .catch(error => window.alert(error instanceof Error ? error.message : String(error)));
        return true;
      },
      addCreateFilePaths() {
        return true;
      },
      loadCreateSourceFromFile() {
        openLocalFiles({ multiple: false, kind: "context", readAsText: true })
          .then((file) => {
            if (!file) {
              return;
            }
            dispatchCreateSurfaceMessage({ command: "loadedCreateSourceFile", ...file });
          })
          .catch(error => window.alert(error instanceof Error ? error.message : String(error)));
        return true;
      },
      scanCreateSourceReferences() {
        dispatchCreateSurfaceMessage({ command: "updateCreateSourceReferences", files: [] });
        return true;
      },
      setCreateFileKind(message) {
        if (!message.sourcePath) {
          return false;
        }
        createFormFiles = createFormFiles.map((file) =>
          file.sourcePath === message.sourcePath
            ? { ...file, kind: normalizeCreateFileKind(message.kind) }
            : file);
        void replaceCreateSurface();
        return true;
      },
      removeCreateFile(message) {
        if (!message.sourcePath) {
          return false;
        }
        createFormFiles = createFormFiles.filter((file) => file.sourcePath !== message.sourcePath);
        void replaceCreateSurface();
        return true;
      },
      submitCreateForm(message) {
        requestJson("/api/create-user-story", {
          title: String(message.title || "").trim(),
          kind: String(message.kind || "feature").trim(),
          category: String(message.category || "").trim(),
          tags: Array.isArray(message.tags)
            ? message.tags
            : String(message.tags || "").split(",").map(item => item.trim()).filter(Boolean),
          sourceText: String(message.sourceText || "").trim(),
          actor: currentActor,
          files: createFormFiles.map((file) => ({
            name: file.name,
            kind: normalizeCreateFileKind(file.kind),
            base64Content: file.base64Content || ""
          }))
        })
          .then((result) => {
            sidebarShowsCreateForm = false;
            createFormFileMode = "context";
            createFormFiles = [];
            createFormResetTokenState += 1;
            window.__specforgePortalLifecycle.navigate(buildPortalRequestUrl({
              createForm: false,
              usId: result.usId,
              selectedPhaseId: null,
              artifactFocus: null
            }).toString(), "post-create-user-story", {
              renderedWorkflowUsId: result.usId,
              detail: "User story created from main portal surface."
            });
          })
          .catch(error => window.alert(error instanceof Error ? error.message : String(error)));
        return true;
      }
    };
    const handleSidebarMessage = (message) => {
      const command = String(message?.command || "");
      const handler = sidebarMessageHandlers[command];
      if (typeof handler === "function") {
        handler(message);
      }
    };
    window.addEventListener("message", event => {
      if (event.data?.source !== "specforge-cli-sidebar") return;
      handleSidebarMessage(event.data.message || {});
    });
    window.addEventListener("message", event => {
      if (event.data?.source !== "specforge-cli-create") return;
      handleSidebarMessage(event.data.message || {});
    });
    window.addEventListener("specforge-cli-sidebar-message", event => {
      handleSidebarMessage(event.detail || {});
    });
    window.addEventListener("specforge-cli-create-form-message", event => {
      handleSidebarMessage(event.detail || {});
    });
    syncPortalUiState();
    applyArtifactFocusFromState();
    if (createSurface instanceof HTMLElement) {
      mountInlineDocument(createSurface, initialCreateHtml);
      dispatchCreateSurfaceMessage({ command: "updateCreateSourceReferences", files: [] });
    }
  })();
</script>`;

function escapeHtmlAttr(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

function buildEmptyWorkflowPageHtml(reason) {
  const message = String(reason || "Select a user story from the sidebar to inspect its workflow.");
  return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>SpecForge Workflow Portal</title>
  <style>
    :root { color-scheme: dark; }
    * { box-sizing: border-box; }
    body { margin: 0; background: #071018; color: rgba(255,255,255,0.92); font-family: ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; }
    .workflow-page { min-height: 100vh; }
    .specforge-empty-shell { min-height: 100vh; display: grid; place-items: center; padding: 40px; }
    .specforge-empty-card { width: min(760px, 100%); border-radius: 24px; border: 1px solid rgba(114, 241, 184, 0.16); background: linear-gradient(180deg, rgba(15, 23, 32, 0.96), rgba(7, 16, 24, 0.98)); box-shadow: 0 28px 96px rgba(0, 0, 0, 0.38); padding: 28px; display: grid; gap: 18px; }
    .specforge-empty-kicker { color: rgba(114, 241, 184, 0.84); font: 800 0.78rem/1.2 ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; letter-spacing: 0.14em; text-transform: uppercase; }
    .specforge-empty-card h1 { margin: 0; font-size: clamp(1.8rem, 3vw, 2.6rem); line-height: 1.08; }
    .specforge-empty-copy { margin: 0; color: rgba(216, 226, 236, 0.82); font-size: 1rem; line-height: 1.6; }
    .specforge-empty-hints { display: grid; gap: 10px; padding: 16px 18px; border-radius: 18px; background: rgba(255,255,255,0.04); border: 1px solid rgba(255,255,255,0.06); }
    .specforge-empty-hints strong { color: rgba(255,255,255,0.94); }
  </style>
</head>
<body>
  <main class="workflow-page">
    <section class="specforge-empty-shell">
      <article class="specforge-empty-card">
        <div class="specforge-empty-kicker">Workflow Portal</div>
        <h1>No user story selected</h1>
        <p class="specforge-empty-copy">${escapeHtmlAttr(message)}</p>
        <div class="specforge-empty-hints">
          <div><strong>What to do next:</strong> use the sidebar to pick a visible story, or widen the scope from <em>Sidebar view options</em>.</div>
          <div><strong>Supported fallback:</strong> direct links with a valid <code>usId</code> still open that exact story even if it is outside the default owner scope.</div>
        </div>
      </article>
    </section>
  </main>
</body>
</html>`;
}

function buildCreateWorkflowPageHtml() {
  const initialCreateSurfaceMarkup = extractInlineDocumentMarkup(createHtml);
  return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>SpecForge Workflow Portal</title>
  <style>
    :root { color-scheme: dark; }
    * { box-sizing: border-box; }
    body { margin: 0; background: #071018; color: rgba(255,255,255,0.92); font-family: ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; }
    .workflow-page { min-height: 100vh; }
    .specforge-create-shell { min-height: 100vh; padding: 24px; overflow: auto; }
    .specforge-create-surface { width: min(980px, 100%); margin: 0 auto; }
  </style>
</head>
<body>
  <main class="workflow-page">
    <section class="specforge-create-shell">
      <div class="specforge-create-surface" data-cli-create-surface>${initialCreateSurfaceMarkup}</div>
    </section>
  </main>
</body>
</html>`;
}

function extractInlineDocumentMarkup(html) {
  const source = String(html || "");
  const styleBlocks = source.match(/<style[\s\S]*?<\/style>/gi)?.join("\n") || "";
  const bodyMatch = source.match(/<body[^>]*>([\s\S]*?)<\/body>/i);
  const bodyContent = bodyMatch ? bodyMatch[1] : source;
  return `${styleBlocks}\n${bodyContent}`.trim();
}

const baseHtml = showCreateForm
  ? buildCreateWorkflowPageHtml()
  : (workflow ? buildWorkflowHtml(workflow, state, "idle", "", "") : buildEmptyWorkflowPageHtml(payload.noSelectionReason));

const html = baseHtml
  .replace("<body>", `<body>${sidebarShell}`)
  .replace(
    "<script>\n  (() => {",
    showCreateForm ? "<script>\n  (() => {" : `${browserShim}\n<script>\n  (() => {`
  )
  .replace("</body>", `${showCreateForm ? `${browserShim}\n` : ""}${refreshShim}\n</body>`);

process.stdout.write(html);
}

main().catch((error) => {
  process.stderr.write(error instanceof Error ? (error.stack || error.message) : String(error));
  process.exit(1);
});
