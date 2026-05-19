import * as fs from "node:fs";
import * as path from "node:path";
import * as vscode from "vscode";
import { getNextAttachmentPathAsync } from "./utils";

export async function attachWorkflowFilesAsync(
  summaryDirectoryPath: string,
  usId: string,
  kind: "context" | "attachment",
  refreshAsync: () => Promise<void>
): Promise<void> {
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
    const targetPath = await getNextAttachmentPathAsync(attachmentsDirectoryPath, path.basename(source.fsPath));
    await fs.promises.copyFile(source.fsPath, targetPath);
  }

  await refreshAsync();
  void vscode.window.showInformationMessage(
    `${selection.length} file(s) added to ${kind === "context" ? "context" : "user story info"} for ${usId}.`
  );
}

export async function addContextFilesFromPathsAsync(
  summaryDirectoryPath: string,
  usId: string,
  pathsToAdd: readonly string[],
  refreshAsync: () => Promise<void>
): Promise<void> {
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

    const targetPath = await getNextAttachmentPathAsync(contextDirectoryPath, path.basename(sourcePath));
    await fs.promises.copyFile(sourcePath, targetPath);
    copiedFiles += 1;
  }

  await refreshAsync();
  if (copiedFiles > 0) {
    void vscode.window.showInformationMessage(
      `${copiedFiles} suggested context file(s) added to ${usId}.`
    );
  }
}

export async function setWorkflowFileKindAsync(
  summaryDirectoryPath: string,
  usId: string,
  filePath: string,
  targetKind: "context" | "attachment",
  refreshAsync: () => Promise<void>
): Promise<void> {
  const sourcePath = path.normalize(filePath);
  const targetDirectory = path.join(summaryDirectoryPath, targetKind === "context" ? "context" : "attachments");
  const sourceDirectory = path.dirname(sourcePath);

  if (path.normalize(sourceDirectory) === path.normalize(targetDirectory)) {
    return;
  }

  await fs.promises.mkdir(targetDirectory, { recursive: true });
  const targetPath = await getNextAttachmentPathAsync(targetDirectory, path.basename(sourcePath));
  await fs.promises.rename(sourcePath, targetPath);
  await refreshAsync();
  void vscode.window.showInformationMessage(
    `Moved ${path.basename(sourcePath)} to ${targetKind === "context" ? "context" : "user story info"} in ${usId}.`
  );
}
