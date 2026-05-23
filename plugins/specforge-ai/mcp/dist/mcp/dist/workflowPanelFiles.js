"use strict";
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    var desc = Object.getOwnPropertyDescriptor(m, k);
    if (!desc || ("get" in desc ? !m.__esModule : desc.writable || desc.configurable)) {
      desc = { enumerable: true, get: function() { return m[k]; } };
    }
    Object.defineProperty(o, k2, desc);
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __setModuleDefault = (this && this.__setModuleDefault) || (Object.create ? (function(o, v) {
    Object.defineProperty(o, "default", { enumerable: true, value: v });
}) : function(o, v) {
    o["default"] = v;
});
var __importStar = (this && this.__importStar) || (function () {
    var ownKeys = function(o) {
        ownKeys = Object.getOwnPropertyNames || function (o) {
            var ar = [];
            for (var k in o) if (Object.prototype.hasOwnProperty.call(o, k)) ar[ar.length] = k;
            return ar;
        };
        return ownKeys(o);
    };
    return function (mod) {
        if (mod && mod.__esModule) return mod;
        var result = {};
        if (mod != null) for (var k = ownKeys(mod), i = 0; i < k.length; i++) if (k[i] !== "default") __createBinding(result, mod, k[i]);
        __setModuleDefault(result, mod);
        return result;
    };
})();
Object.defineProperty(exports, "__esModule", { value: true });
exports.attachWorkflowFilesAsync = attachWorkflowFilesAsync;
exports.addContextFilesFromPathsAsync = addContextFilesFromPathsAsync;
exports.setWorkflowFileKindAsync = setWorkflowFileKindAsync;
const fs = __importStar(require("node:fs"));
const path = __importStar(require("node:path"));
const vscode = __importStar(require("vscode"));
const utils_1 = require("./utils");
async function attachWorkflowFilesAsync(summaryDirectoryPath, usId, kind, refreshAsync) {
    const selection = await vscode.window.showOpenDialog({
        canSelectFiles: true,
        canSelectFolders: false,
        canSelectMany: true,
        openLabel: kind === "context" ? "Add context files" : "Add user story files"
    });
    if (!selection || selection.length === 0) {
        return;
    }
    const attachmentsDirectoryPath = path.join(summaryDirectoryPath, kind === "context" ? "context" : "attachments");
    await fs.promises.mkdir(attachmentsDirectoryPath, { recursive: true });
    for (const source of selection) {
        const targetPath = await (0, utils_1.getNextAttachmentPathAsync)(attachmentsDirectoryPath, path.basename(source.fsPath));
        await fs.promises.copyFile(source.fsPath, targetPath);
    }
    await refreshAsync();
    void vscode.window.showInformationMessage(`${selection.length} file(s) added to ${kind === "context" ? "context" : "user story info"} for ${usId}.`);
}
async function addContextFilesFromPathsAsync(summaryDirectoryPath, usId, pathsToAdd, refreshAsync) {
    const uniquePaths = Array.from(new Set(pathsToAdd.map((filePath) => path.normalize(filePath))));
    if (uniquePaths.length === 0) {
        return;
    }
    const contextDirectoryPath = path.join(summaryDirectoryPath, "context");
    await fs.promises.mkdir(contextDirectoryPath, { recursive: true });
    let copiedFiles = 0;
    for (const sourcePath of uniquePaths) {
        const sourceStats = await fs.promises.stat(sourcePath).catch(() => null);
        if (!sourceStats?.isFile()) {
            continue;
        }
        const targetPath = await (0, utils_1.getNextAttachmentPathAsync)(contextDirectoryPath, path.basename(sourcePath));
        await fs.promises.copyFile(sourcePath, targetPath);
        copiedFiles += 1;
    }
    await refreshAsync();
    if (copiedFiles > 0) {
        void vscode.window.showInformationMessage(`${copiedFiles} suggested context file(s) added to ${usId}.`);
    }
}
async function setWorkflowFileKindAsync(summaryDirectoryPath, usId, filePath, targetKind, refreshAsync) {
    const sourcePath = path.normalize(filePath);
    const targetDirectory = path.join(summaryDirectoryPath, targetKind === "context" ? "context" : "attachments");
    const sourceDirectory = path.dirname(sourcePath);
    if (path.normalize(sourceDirectory) === path.normalize(targetDirectory)) {
        return;
    }
    await fs.promises.mkdir(targetDirectory, { recursive: true });
    const targetPath = await (0, utils_1.getNextAttachmentPathAsync)(targetDirectory, path.basename(sourcePath));
    await fs.promises.rename(sourcePath, targetPath);
    await refreshAsync();
    void vscode.window.showInformationMessage(`Moved ${path.basename(sourcePath)} to ${targetKind === "context" ? "context" : "user story info"} in ${usId}.`);
}
//# sourceMappingURL=workflowPanelFiles.js.map