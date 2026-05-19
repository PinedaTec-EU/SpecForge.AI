using SpecForge.Domain.Persistence;

namespace SpecForge.Domain.Application;

internal sealed class UserStoryFilesService
{
    public Task<UserStoryFilesResult> ListUserStoryFilesAsync(
        string workspaceRoot,
        string usId,
        CancellationToken cancellationToken = default)
    {
        var paths = UserStoryFilePaths.ResolveFromWorkspaceRoot(workspaceRoot, usId);
        return Task.FromResult(new UserStoryFilesResult(
            usId,
            BuildFileDetails(paths.ContextDirectoryPath),
            BuildFileDetails(paths.AttachmentsDirectoryPath)));
    }

    public async Task<UserStoryFilesResult> AddUserStoryFilesAsync(
        string workspaceRoot,
        string usId,
        IReadOnlyCollection<string> sourcePaths,
        string kind,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(usId);
        var normalizedKind = NormalizeUserStoryFileKind(kind);
        var paths = UserStoryFilePaths.ResolveFromWorkspaceRoot(workspaceRoot, usId);
        var targetDirectoryPath = GetDirectoryPathForFileKind(paths, normalizedKind);
        Directory.CreateDirectory(targetDirectoryPath);

        foreach (var sourcePath in sourcePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var resolvedSourcePath = ResolveWorkspaceOrAbsolutePath(workspaceRoot, sourcePath);
            if (!File.Exists(resolvedSourcePath))
            {
                throw new FileNotFoundException($"The provided file path does not exist: {resolvedSourcePath}.", resolvedSourcePath);
            }

            var targetPath = GetNextAvailableFilePath(targetDirectoryPath, Path.GetFileName(resolvedSourcePath));
            await using var sourceStream = File.OpenRead(resolvedSourcePath);
            await using var targetStream = File.Create(targetPath);
            await sourceStream.CopyToAsync(targetStream, cancellationToken);
        }

        return await ListUserStoryFilesAsync(workspaceRoot, usId, cancellationToken);
    }

    public async Task<UserStoryFilesResult> SetUserStoryFileKindAsync(
        string workspaceRoot,
        string usId,
        string filePath,
        string kind,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(usId);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var normalizedKind = NormalizeUserStoryFileKind(kind);
        var paths = UserStoryFilePaths.ResolveFromWorkspaceRoot(workspaceRoot, usId);
        var resolvedFilePath = ResolveWorkspaceOrAbsolutePath(workspaceRoot, filePath);
        var normalizedFilePath = Path.GetFullPath(resolvedFilePath);
        var normalizedContextDirectory = Path.GetFullPath(paths.ContextDirectoryPath);
        var normalizedAttachmentsDirectory = Path.GetFullPath(paths.AttachmentsDirectoryPath);

        if (!File.Exists(normalizedFilePath))
        {
            throw new FileNotFoundException($"The provided file path does not exist: {normalizedFilePath}.", normalizedFilePath);
        }

        var currentDirectory = Path.GetDirectoryName(normalizedFilePath)
            ?? throw new InvalidOperationException("The file path does not have a parent directory.");
        var isContextFile = string.Equals(currentDirectory, normalizedContextDirectory, StringComparison.Ordinal);
        var isAttachmentFile = string.Equals(currentDirectory, normalizedAttachmentsDirectory, StringComparison.Ordinal);
        if (!isContextFile && !isAttachmentFile)
        {
            throw new InvalidOperationException("The file must already belong to the current user story.");
        }

        var targetDirectoryPath = GetDirectoryPathForFileKind(paths, normalizedKind);
        Directory.CreateDirectory(targetDirectoryPath);
        if (string.Equals(Path.GetFullPath(targetDirectoryPath), currentDirectory, StringComparison.Ordinal))
        {
            return await ListUserStoryFilesAsync(workspaceRoot, usId, cancellationToken);
        }

        var targetPath = GetNextAvailableFilePath(targetDirectoryPath, Path.GetFileName(normalizedFilePath));
        File.Move(normalizedFilePath, targetPath);
        return await ListUserStoryFilesAsync(workspaceRoot, usId, cancellationToken);
    }

    public static IReadOnlyCollection<UserStoryFileDetails> BuildFileDetails(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return [];
        }

        return Directory.GetFiles(directoryPath, "*", SearchOption.TopDirectoryOnly)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .Select(static path => new UserStoryFileDetails(Path.GetFileName(path), path))
            .ToArray();
    }

    private static string NormalizeUserStoryFileKind(string kind) => kind.Trim().ToLowerInvariant() switch
    {
        "context" => "context",
        "attachment" => "attachment",
        "us-info" => "attachment",
        "user-story" => "attachment",
        "user-story-info" => "attachment",
        _ => throw new InvalidOperationException($"Unsupported file kind '{kind}'. Expected 'context' or 'attachment'.")
    };

    private static string GetDirectoryPathForFileKind(UserStoryFilePaths paths, string kind) => kind switch
    {
        "context" => paths.ContextDirectoryPath,
        "attachment" => paths.AttachmentsDirectoryPath,
        _ => throw new InvalidOperationException($"Unsupported file kind '{kind}'.")
    };

    private static string ResolveWorkspaceOrAbsolutePath(string workspaceRoot, string filePath) =>
        Path.GetFullPath(Path.IsPathRooted(filePath) ? filePath : Path.Combine(workspaceRoot, filePath));

    private static string GetNextAvailableFilePath(string directoryPath, string fileName)
    {
        var extension = Path.GetExtension(fileName);
        var baseName = extension.Length > 0 ? fileName[..^extension.Length] : fileName;

        for (var attempt = 0; attempt < 100; attempt += 1)
        {
            var suffix = attempt == 0 ? string.Empty : $".{attempt + 1:00}";
            var candidate = Path.Combine(directoryPath, $"{baseName}{suffix}{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException($"Unable to persist '{fileName}' after 100 attempts.");
    }
}
