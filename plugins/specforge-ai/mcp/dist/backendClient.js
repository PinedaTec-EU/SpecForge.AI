"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.onModelResponseDiagnostic = onModelResponseDiagnostic;
exports.notifyModelResponseDiagnostic = notifyModelResponseDiagnostic;
exports.createMcpBackendClient = createMcpBackendClient;
const stdioMcpBackendClient_1 = require("./stdioMcpBackendClient");
const modelResponseListeners = new Set();
function onModelResponseDiagnostic(listener) {
    modelResponseListeners.add(listener);
    return () => {
        modelResponseListeners.delete(listener);
    };
}
function notifyModelResponseDiagnostic(diagnostic) {
    for (const listener of modelResponseListeners) {
        listener(diagnostic);
    }
}
function createMcpBackendClient(workspaceRoot, hostRoot, settings) {
    return new stdioMcpBackendClient_1.StdioMcpBackendClient(workspaceRoot, hostRoot, settings);
}
//# sourceMappingURL=backendClient.js.map