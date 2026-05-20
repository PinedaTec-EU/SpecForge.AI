using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using SpecForge.Domain.Persistence;

namespace SpecForge.Domain.Application;

public sealed record SemanticGraphStatusSnapshot(
    string ContractKey,
    SemanticGraphArtifactStatus GlobalGraph,
    SemanticGraphArtifactStatus? ImpactGraph,
    IReadOnlyCollection<string> AvailableQueryKinds);

public sealed record SemanticGraphArtifactStatus(
    string Scope,
    string State,
    bool Exists,
    string ArtifactPath,
    string MetadataPath,
    string? AuxiliaryPath,
    string? Reason,
    string? BuiltAtUtc,
    string? BuilderStrategy,
    string? Fingerprint,
    IReadOnlyCollection<string> AvailableQueryKinds);

public sealed record SemanticGraphGlobalOperationRequest(
    string Mode,
    string Actor,
    string Reason,
    bool DryRun = false,
    bool ConfirmOverwrite = false);

public sealed record SemanticGraphImpactOperationRequest(
    string UsId,
    string Actor,
    string Reason,
    bool DryRun = false);

public sealed record SemanticGraphOperationResult(
    string Scope,
    string Mode,
    bool DryRun,
    bool Executed,
    bool RequiresOverwriteConfirmation,
    string CurrentState,
    string TargetState,
    IReadOnlyCollection<string> Warnings,
    IReadOnlyCollection<string> BlockedReasons,
    IReadOnlyCollection<string> ArtifactsWritten);

public sealed record SemanticGraphQueryRequest(
    string QueryKind,
    string Actor,
    string? UsId = null,
    string? Phase = null,
    string? Reason = null,
    string? FilePath = null,
    string? SymbolId = null,
    int MaxDepth = 1,
    bool IncludeTests = false,
    string? SourcePreference = null);

public sealed record SemanticGraphQueryResult(
    string QueryKind,
    string SourceGraphUsed,
    string FreshnessState,
    bool FallbackUsed,
    IReadOnlyCollection<string> IncludedFiles,
    IReadOnlyCollection<string> IncludedNodes,
    IReadOnlyCollection<string> IncludedEdges,
    IReadOnlyCollection<string> InclusionReasons,
    IReadOnlyCollection<string> Warnings,
    int LatencyMs,
    object Payload);

internal sealed record SemanticGraphMetadata(
    string ContractKey,
    string Scope,
    string State,
    string BuiltAtUtc,
    string BuilderStrategy,
    string Fingerprint,
    string? SourceFingerprint,
    string? ParentFingerprint,
    string? GraphScopeRequestSha256,
    IReadOnlyCollection<string> AvailableQueryKinds,
    string? RepositoryHeadSha = null,
    string? FailureCode = null);

internal sealed record SemanticGlobalGraphArtifact(
    string ContractKey,
    string BuiltAtUtc,
    string BuilderStrategy,
    string? RepositoryHeadSha,
    IReadOnlyCollection<SemanticGraphProjectNode> Projects,
    IReadOnlyCollection<SemanticGraphFileNode> Files,
    IReadOnlyCollection<SemanticGraphReferenceEdge> ProjectReferenceEdges,
    IReadOnlyCollection<string> AvailableQueryKinds);

internal sealed record SemanticGraphProjectNode(
    string Path,
    string Name,
    bool IsTestProject,
    IReadOnlyCollection<string> ProjectReferences,
    IReadOnlyCollection<string> SourceFiles);

internal sealed record SemanticGraphFileNode(
    string Path,
    string Kind,
    string? ProjectPath,
    string? Sha256);

internal sealed record SemanticGraphReferenceEdge(
    string Kind,
    string FromPath,
    string ToPath);

internal sealed record SemanticImpactGraphArtifact(
    string ContractKey,
    string UsId,
    string BuiltAtUtc,
    string DerivationMode,
    string? ParentFingerprint,
    string? GraphScopeRequestSha256,
    IReadOnlyCollection<SemanticImpactFileInclusion> IncludedFiles,
    IReadOnlyCollection<string> Warnings);

internal sealed record SemanticImpactFileInclusion(
    string Path,
    string Reason,
    string Source,
    string? ProjectPath = null);

public static class SemanticGraphOperations
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly string[] BaselineQueryKinds =
    [
        "status",
        "explain-freshness",
        "derive-impact-graph",
        "neighbors:file",
        "tests-adjacent:file",
        "why-included:file"
    ];

    private static readonly string[] IgnoredDirectories =
    [
        ".git",
        ".specs",
        "bin",
        "obj",
        "node_modules"
    ];

    public static SemanticGraphStatusSnapshot DescribeStatus(string workspaceRoot, string? usId = null)
    {
        var globalPaths = SemanticGraphFilePaths.FromWorkspaceRoot(workspaceRoot);
        var globalStatus = DescribeGlobalStatus(workspaceRoot, globalPaths);
        SemanticGraphArtifactStatus? impactStatus = null;
        if (!string.IsNullOrWhiteSpace(usId))
        {
            var userStoryPaths = UserStoryFilePaths.ResolveFromWorkspaceRoot(workspaceRoot, usId);
            impactStatus = DescribeImpactStatus(workspaceRoot, userStoryPaths, globalStatus);
        }

        return new SemanticGraphStatusSnapshot(
            SemanticGraphLifecycleCatalog.ContractKey,
            globalStatus,
            impactStatus,
            globalStatus.AvailableQueryKinds);
    }

    public static async Task<SemanticGraphOperationResult> RunGlobalOperationAsync(
        string workspaceRoot,
        SemanticGraphGlobalOperationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(request);

        var globalPaths = SemanticGraphFilePaths.FromWorkspaceRoot(workspaceRoot);
        var currentStatus = DescribeGlobalStatus(workspaceRoot, globalPaths);
        var blockedReasons = new List<string>();
        var warnings = new List<string>();
        var requiresConfirmation = false;
        var targetState = request.Mode switch
        {
            "build" => currentStatus.Exists ? "fresh" : "fresh",
            "refresh" => "fresh",
            "rebuild" => "fresh",
            _ => throw new InvalidOperationException($"Unsupported global graph mode '{request.Mode}'.")
        };

        if (request.Mode == "rebuild" && currentStatus.Exists)
        {
            requiresConfirmation = true;
            warnings.Add("Existing global graph baseline will be replaced.");
            if (!request.ConfirmOverwrite)
            {
                blockedReasons.Add("Overwrite confirmation is required before rebuilding an existing global graph from zero.");
            }
        }

        if (request.Mode == "refresh" && !currentStatus.Exists)
        {
            warnings.Add("Global graph is missing, so refresh will create the first baseline graph.");
        }

        if (request.DryRun || blockedReasons.Count > 0)
        {
            return new SemanticGraphOperationResult(
                Scope: "global",
                Mode: request.Mode,
                DryRun: request.DryRun,
                Executed: false,
                RequiresOverwriteConfirmation: requiresConfirmation,
                CurrentState: currentStatus.State,
                TargetState: targetState,
                Warnings: warnings,
                BlockedReasons: blockedReasons,
                ArtifactsWritten: []);
        }

        Directory.CreateDirectory(globalPaths.GraphsDirectoryPath);
        var artifact = BuildBaselineGlobalGraph(workspaceRoot);
        var artifactJson = JsonSerializer.Serialize(artifact, SerializerOptions);
        var metadata = new SemanticGraphMetadata(
            ContractKey: SemanticGraphLifecycleCatalog.ContractKey,
            Scope: "global",
            State: "fresh",
            BuiltAtUtc: artifact.BuiltAtUtc,
            BuilderStrategy: artifact.BuilderStrategy,
            Fingerprint: PhaseExecutionReceiptStore.ComputeSha256(artifactJson) ?? string.Empty,
            SourceFingerprint: ComputeWorkspaceFingerprint(workspaceRoot),
            ParentFingerprint: null,
            GraphScopeRequestSha256: null,
            AvailableQueryKinds: artifact.AvailableQueryKinds,
            RepositoryHeadSha: artifact.RepositoryHeadSha,
            FailureCode: null);
        var metadataJson = JsonSerializer.Serialize(metadata, SerializerOptions);
        var artifactsWritten = new List<string>
        {
            WriteText(globalPaths.GlobalGraphPath, artifactJson),
            WriteText(globalPaths.GlobalGraphMetadataPath, metadataJson)
        };

        var buildLogEntry = new
        {
            eventFamily = $"graph.{request.Mode}.completed",
            timestamp = DateTimeOffset.UtcNow.ToString("O"),
            actor = request.Actor,
            reason = request.Reason,
            overwriteConfirmed = request.ConfirmOverwrite,
            targetState,
            metadataPath = PhaseExecutionReceiptStore.NormalizePath(globalPaths.GlobalGraphMetadataPath)
        };
        artifactsWritten.Add(AppendJsonLine(globalPaths.GraphBuildLogPath, buildLogEntry));

        return new SemanticGraphOperationResult(
            Scope: "global",
            Mode: request.Mode,
            DryRun: false,
            Executed: true,
            RequiresOverwriteConfirmation: requiresConfirmation,
            CurrentState: currentStatus.State,
            TargetState: targetState,
            Warnings: warnings,
            BlockedReasons: [],
            ArtifactsWritten: artifactsWritten.Select(PhaseExecutionReceiptStore.NormalizePath).ToArray());
    }

    public static async Task<SemanticGraphOperationResult> MaterializeImpactGraphAsync(
        string workspaceRoot,
        SemanticGraphImpactOperationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(request);

        var userStoryPaths = UserStoryFilePaths.ResolveFromWorkspaceRoot(workspaceRoot, request.UsId);
        var globalStatus = DescribeGlobalStatus(workspaceRoot, SemanticGraphFilePaths.FromWorkspaceRoot(workspaceRoot));
        var impactStatus = DescribeImpactStatus(workspaceRoot, userStoryPaths, globalStatus);
        var warnings = new List<string>();
        var blockedReasons = new List<string>();

        var graphScopeRequest = TryLoadGraphScopeRequest(userStoryPaths.GraphScopeRequestPath);
        if (graphScopeRequest is null)
        {
            blockedReasons.Add("Graph scope request is missing, so impact graph materialization cannot determine its seeds.");
        }

        if (request.DryRun || blockedReasons.Count > 0)
        {
            return new SemanticGraphOperationResult(
                Scope: "impact",
                Mode: "derive-impact-graph",
                DryRun: request.DryRun,
                Executed: false,
                RequiresOverwriteConfirmation: false,
                CurrentState: impactStatus.State,
                TargetState: "fresh",
                Warnings: warnings,
                BlockedReasons: blockedReasons,
                ArtifactsWritten: []);
        }

        Directory.CreateDirectory(userStoryPaths.ContextDirectoryPath);
        var globalGraph = TryLoadGlobalGraph(SemanticGraphFilePaths.FromWorkspaceRoot(workspaceRoot).GlobalGraphPath);
        var derivationMode = globalGraph is null ? "fallback-derived" : "global-graph-derived";
        var artifact = BuildImpactGraphArtifact(workspaceRoot, request.UsId, userStoryPaths, graphScopeRequest!, globalGraph, warnings);
        var artifactJson = JsonSerializer.Serialize(artifact, SerializerOptions);
        var metadata = new SemanticGraphMetadata(
            ContractKey: SemanticGraphLifecycleCatalog.ContractKey,
            Scope: "impact",
            State: "fresh",
            BuiltAtUtc: artifact.BuiltAtUtc,
            BuilderStrategy: derivationMode,
            Fingerprint: PhaseExecutionReceiptStore.ComputeSha256(artifactJson) ?? string.Empty,
            SourceFingerprint: null,
            ParentFingerprint: TryLoadMetadata(SemanticGraphFilePaths.FromWorkspaceRoot(workspaceRoot).GlobalGraphMetadataPath)?.Fingerprint,
            GraphScopeRequestSha256: PhaseExecutionReceiptStore.TryComputeFileSha256(userStoryPaths.GraphScopeRequestPath),
            AvailableQueryKinds: BaselineQueryKinds,
            RepositoryHeadSha: PhaseExecutionReceiptStore.TryReadGitHeadSha(workspaceRoot),
            FailureCode: null);
        var summary = BuildImpactSummaryMarkdown(artifact, request.Reason);
        var artifactsWritten = new List<string>
        {
            WriteText(userStoryPaths.ImpactGraphPath, artifactJson),
            WriteText(userStoryPaths.ImpactGraphMetadataPath, JsonSerializer.Serialize(metadata, SerializerOptions)),
            WriteText(userStoryPaths.ImpactGraphSummaryPath, summary)
        };

        return new SemanticGraphOperationResult(
            Scope: "impact",
            Mode: "derive-impact-graph",
            DryRun: false,
            Executed: true,
            RequiresOverwriteConfirmation: false,
            CurrentState: impactStatus.State,
            TargetState: "fresh",
            Warnings: warnings,
            BlockedReasons: [],
            ArtifactsWritten: artifactsWritten.Select(PhaseExecutionReceiptStore.NormalizePath).ToArray());
    }

    public static SemanticGraphQueryResult ExecuteQuery(string workspaceRoot, SemanticGraphQueryRequest request)
    {
        var startedAt = Environment.TickCount64;
        var warnings = new List<string>();

        return request.QueryKind switch
        {
            "status" => BuildStatusQueryResult(workspaceRoot, request, startedAt, warnings),
            "explain-freshness" => BuildFreshnessQueryResult(workspaceRoot, request, startedAt, warnings),
            "neighbors:file" => BuildNeighborsQueryResult(workspaceRoot, request, startedAt, warnings),
            "tests-adjacent:file" => BuildTestsAdjacentQueryResult(workspaceRoot, request, startedAt, warnings),
            "why-included:file" => BuildWhyIncludedQueryResult(workspaceRoot, request, startedAt, warnings),
            _ => throw new InvalidOperationException($"Query kind '{request.QueryKind}' is not supported by the baseline semantic graph contract.")
        };
    }

    private static SemanticGraphArtifactStatus DescribeGlobalStatus(string workspaceRoot, SemanticGraphFilePaths globalPaths)
    {
        var metadata = TryLoadMetadata(globalPaths.GlobalGraphMetadataPath);
        var exists = File.Exists(globalPaths.GlobalGraphPath) && metadata is not null;
        if (!exists)
        {
            return new SemanticGraphArtifactStatus(
                Scope: "global",
                State: "missing",
                Exists: false,
                ArtifactPath: PhaseExecutionReceiptStore.NormalizePath(globalPaths.GlobalGraphPath),
                MetadataPath: PhaseExecutionReceiptStore.NormalizePath(globalPaths.GlobalGraphMetadataPath),
                AuxiliaryPath: PhaseExecutionReceiptStore.NormalizePath(globalPaths.GraphBuildLogPath),
                Reason: "Global graph artifact or metadata does not exist yet.",
                BuiltAtUtc: null,
                BuilderStrategy: null,
                Fingerprint: null,
                AvailableQueryKinds: BaselineQueryKinds);
        }

        var effectiveMetadata = metadata!;
        var currentHead = PhaseExecutionReceiptStore.TryReadGitHeadSha(workspaceRoot);
        var currentRootFingerprint = ComputeWorkspaceFingerprint(workspaceRoot);
        var state = effectiveMetadata.ContractKey != SemanticGraphLifecycleCatalog.ContractKey
            ? "incompatible"
            : !string.IsNullOrWhiteSpace(effectiveMetadata.FailureCode)
                ? "failed"
                : !string.Equals(effectiveMetadata.RepositoryHeadSha, currentHead, StringComparison.Ordinal)
                  || !string.Equals(effectiveMetadata.SourceFingerprint, currentRootFingerprint, StringComparison.Ordinal)
                    ? "stale-refreshable"
                    : "fresh";
        var reason = state switch
        {
            "incompatible" => "Graph metadata contract key does not match the current semantic graph lifecycle contract.",
            "failed" => $"Last graph build recorded failure '{effectiveMetadata.FailureCode}'.",
            "stale-refreshable" => "Repository fingerprint or HEAD differs from the graph metadata baseline, so incremental refresh is preferred.",
            _ => "Global graph metadata matches the current repository baseline."
        };

        return new SemanticGraphArtifactStatus(
            Scope: "global",
            State: state,
            Exists: true,
            ArtifactPath: PhaseExecutionReceiptStore.NormalizePath(globalPaths.GlobalGraphPath),
            MetadataPath: PhaseExecutionReceiptStore.NormalizePath(globalPaths.GlobalGraphMetadataPath),
            AuxiliaryPath: PhaseExecutionReceiptStore.NormalizePath(globalPaths.GraphBuildLogPath),
            Reason: reason,
            BuiltAtUtc: effectiveMetadata.BuiltAtUtc,
            BuilderStrategy: effectiveMetadata.BuilderStrategy,
            Fingerprint: effectiveMetadata.Fingerprint,
            AvailableQueryKinds: effectiveMetadata.AvailableQueryKinds.Count > 0 ? effectiveMetadata.AvailableQueryKinds : BaselineQueryKinds);
    }

    private static SemanticGraphArtifactStatus DescribeImpactStatus(
        string workspaceRoot,
        UserStoryFilePaths userStoryPaths,
        SemanticGraphArtifactStatus globalStatus)
    {
        var metadata = TryLoadMetadata(userStoryPaths.ImpactGraphMetadataPath);
        var exists = File.Exists(userStoryPaths.ImpactGraphPath) && metadata is not null;
        if (!exists)
        {
            return new SemanticGraphArtifactStatus(
                Scope: "impact",
                State: "missing",
                Exists: false,
                ArtifactPath: PhaseExecutionReceiptStore.NormalizePath(userStoryPaths.ImpactGraphPath),
                MetadataPath: PhaseExecutionReceiptStore.NormalizePath(userStoryPaths.ImpactGraphMetadataPath),
                AuxiliaryPath: PhaseExecutionReceiptStore.NormalizePath(userStoryPaths.ImpactGraphSummaryPath),
                Reason: "Impact graph artifact or metadata does not exist yet for this user story.",
                BuiltAtUtc: null,
                BuilderStrategy: null,
                Fingerprint: null,
                AvailableQueryKinds: BaselineQueryKinds);
        }

        var effectiveMetadata = metadata!;
        var currentScopeSha = PhaseExecutionReceiptStore.TryComputeFileSha256(userStoryPaths.GraphScopeRequestPath);
        var state = effectiveMetadata.ContractKey != SemanticGraphLifecycleCatalog.ContractKey
            ? "incompatible"
            : !string.IsNullOrWhiteSpace(effectiveMetadata.FailureCode)
                ? "failed"
                : effectiveMetadata.ParentFingerprint is not null && globalStatus.Fingerprint is not null
                  && !string.Equals(effectiveMetadata.ParentFingerprint, globalStatus.Fingerprint, StringComparison.Ordinal)
                    ? "stale-refreshable"
                    : !string.Equals(effectiveMetadata.GraphScopeRequestSha256, currentScopeSha, StringComparison.Ordinal)
                        ? "stale-refreshable"
                        : "fresh";
        var reason = state switch
        {
            "incompatible" => "Impact graph metadata contract key does not match the current semantic graph lifecycle contract.",
            "failed" => $"Last impact graph derivation recorded failure '{effectiveMetadata.FailureCode}'.",
            "stale-refreshable" => "Impact graph scope fingerprint or parent global graph fingerprint changed since the last derivation.",
            _ => "Impact graph metadata matches the current graph-scope request and parent graph fingerprint."
        };

        return new SemanticGraphArtifactStatus(
            Scope: "impact",
            State: state,
            Exists: true,
            ArtifactPath: PhaseExecutionReceiptStore.NormalizePath(userStoryPaths.ImpactGraphPath),
            MetadataPath: PhaseExecutionReceiptStore.NormalizePath(userStoryPaths.ImpactGraphMetadataPath),
            AuxiliaryPath: PhaseExecutionReceiptStore.NormalizePath(userStoryPaths.ImpactGraphSummaryPath),
            Reason: reason,
            BuiltAtUtc: effectiveMetadata.BuiltAtUtc,
            BuilderStrategy: effectiveMetadata.BuilderStrategy,
            Fingerprint: effectiveMetadata.Fingerprint,
            AvailableQueryKinds: effectiveMetadata.AvailableQueryKinds.Count > 0 ? effectiveMetadata.AvailableQueryKinds : BaselineQueryKinds);
    }

    private static SemanticGlobalGraphArtifact BuildBaselineGlobalGraph(string workspaceRoot)
    {
        var builtAtUtc = DateTimeOffset.UtcNow.ToString("O");
        var repositoryHeadSha = PhaseExecutionReceiptStore.TryReadGitHeadSha(workspaceRoot);
        var projectPaths = EnumerateWorkspaceFiles(workspaceRoot, "*.csproj").OrderBy(static path => path, StringComparer.Ordinal).ToArray();
        var projects = projectPaths.Select(path => BuildProjectNode(workspaceRoot, path)).ToArray();
        var projectLookup = projects.ToDictionary(project => project.Path, StringComparer.Ordinal);
        var files = projects
            .SelectMany(project => project.SourceFiles.Select(file => new SemanticGraphFileNode(
                file,
                file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ? "csharp-source" : "source",
                project.Path,
                PhaseExecutionReceiptStore.TryComputeFileSha256(Path.Combine(workspaceRoot, file.Replace('/', Path.DirectorySeparatorChar))))))
            .DistinctBy(static file => file.Path, StringComparer.Ordinal)
            .ToArray();
        var edges = projects
            .SelectMany(project => project.ProjectReferences
                .Where(projectLookup.ContainsKey)
                .Select(reference => new SemanticGraphReferenceEdge(
                    "project-reference",
                    project.Path,
                    reference)))
            .ToArray();

        return new SemanticGlobalGraphArtifact(
            ContractKey: SemanticGraphLifecycleCatalog.ContractKey,
            BuiltAtUtc: builtAtUtc,
            BuilderStrategy: "baseline-dotnet-project-map/v1",
            RepositoryHeadSha: repositoryHeadSha,
            Projects: projects,
            Files: files,
            ProjectReferenceEdges: edges,
            AvailableQueryKinds: BaselineQueryKinds);
    }

    private static SemanticGraphProjectNode BuildProjectNode(string workspaceRoot, string projectPath)
    {
        var normalizedProjectPath = NormalizeWorkspaceRelative(workspaceRoot, projectPath);
        var projectDirectory = Path.GetDirectoryName(projectPath)!;
        var document = XDocument.Load(projectPath);
        var referencedProjects = document
            .Descendants()
            .Where(element => string.Equals(element.Name.LocalName, "ProjectReference", StringComparison.Ordinal))
            .Select(element => element.Attribute("Include")?.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(value => Path.GetFullPath(Path.Combine(projectDirectory, value!)))
            .Where(File.Exists)
            .Select(path => NormalizeWorkspaceRelative(workspaceRoot, path))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();

        var packageReferences = document
            .Descendants()
            .Where(element => string.Equals(element.Name.LocalName, "PackageReference", StringComparison.Ordinal))
            .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
            .ToArray();
        var isTestProject = Path.GetFileNameWithoutExtension(projectPath).Contains("test", StringComparison.OrdinalIgnoreCase)
            || packageReferences.Any(static package =>
                package.Contains("xunit", StringComparison.OrdinalIgnoreCase)
                || package.Contains("nunit", StringComparison.OrdinalIgnoreCase)
                || package.Contains("mstest", StringComparison.OrdinalIgnoreCase));
        var sourceFiles = Directory.EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsIgnoredPath(path))
            .Select(path => NormalizeWorkspaceRelative(workspaceRoot, path))
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();

        return new SemanticGraphProjectNode(
            normalizedProjectPath,
            Path.GetFileNameWithoutExtension(projectPath),
            isTestProject,
            referencedProjects,
            sourceFiles);
    }

    private static SemanticImpactGraphArtifact BuildImpactGraphArtifact(
        string workspaceRoot,
        string usId,
        UserStoryFilePaths userStoryPaths,
        RefinementGraphScopeRequest graphScopeRequest,
        SemanticGlobalGraphArtifact? globalGraph,
        ICollection<string> warnings)
    {
        var included = new Dictionary<string, SemanticImpactFileInclusion>(StringComparer.Ordinal);
        foreach (var seedFile in graphScopeRequest.SeedFiles)
        {
            var workspaceRelativePath = NormalizePossiblyRelativeSeedPath(workspaceRoot, seedFile.Path);
            if (workspaceRelativePath is null)
            {
                continue;
            }

            included[workspaceRelativePath] = new SemanticImpactFileInclusion(
                workspaceRelativePath,
                $"Seed file from graph scope request ({seedFile.PhaseId ?? "context"}).",
                "graph-scope-request");

            var siblings = Directory.Exists(Path.GetDirectoryName(Path.Combine(workspaceRoot, workspaceRelativePath.Replace('/', Path.DirectorySeparatorChar)))!)
                ? Directory.EnumerateFiles(
                        Path.GetDirectoryName(Path.Combine(workspaceRoot, workspaceRelativePath.Replace('/', Path.DirectorySeparatorChar)))!,
                        "*.cs",
                        SearchOption.TopDirectoryOnly)
                    .Where(path => !IsIgnoredPath(path))
                    .Select(path => NormalizeWorkspaceRelative(workspaceRoot, path))
                    .Where(path => !string.Equals(path, workspaceRelativePath, StringComparison.Ordinal))
                    .Take(6)
                : [];

            foreach (var sibling in siblings)
            {
                included.TryAdd(
                    sibling,
                    new SemanticImpactFileInclusion(
                        sibling,
                        $"Sibling source file near seed '{workspaceRelativePath}'.",
                        "directory-neighbor"));
            }
        }

        if (globalGraph is not null)
        {
            foreach (var inclusion in included.Values.ToArray())
            {
                var owningProject = globalGraph.Projects.FirstOrDefault(project =>
                    project.SourceFiles.Contains(inclusion.Path, StringComparer.Ordinal));
                if (owningProject is null)
                {
                    continue;
                }

                included[inclusion.Path] = inclusion with { ProjectPath = owningProject.Path };
                included.TryAdd(
                    owningProject.Path,
                    new SemanticImpactFileInclusion(
                        owningProject.Path,
                        $"Owning project for seed-related file '{inclusion.Path}'.",
                        "project-ownership"));

                foreach (var testProject in globalGraph.Projects.Where(project =>
                             project.IsTestProject
                             && project.ProjectReferences.Contains(owningProject.Path, StringComparer.Ordinal)))
                {
                    included.TryAdd(
                        testProject.Path,
                        new SemanticImpactFileInclusion(
                            testProject.Path,
                            $"Test project '{testProject.Name}' references owning project '{owningProject.Name}'.",
                            "test-adjacency",
                            testProject.Path));
                }
            }
        }
        else
        {
            warnings.Add("Global graph is missing, so impact graph was materialized from fallback seed inspection only.");
        }

        return new SemanticImpactGraphArtifact(
            ContractKey: SemanticGraphLifecycleCatalog.ContractKey,
            UsId: usId.Trim().ToUpperInvariant(),
            BuiltAtUtc: DateTimeOffset.UtcNow.ToString("O"),
            DerivationMode: globalGraph is null ? "fallback-derived" : "global-graph-derived",
            ParentFingerprint: globalGraph is null ? null : PhaseExecutionReceiptStore.ComputeSha256(JsonSerializer.Serialize(globalGraph, SerializerOptions)),
            GraphScopeRequestSha256: PhaseExecutionReceiptStore.TryComputeFileSha256(userStoryPaths.GraphScopeRequestPath),
            IncludedFiles: included.Values.OrderBy(static item => item.Path, StringComparer.Ordinal).ToArray(),
            Warnings: warnings.ToArray());
    }

    private static SemanticGraphQueryResult BuildStatusQueryResult(
        string workspaceRoot,
        SemanticGraphQueryRequest request,
        long startedAt,
        ICollection<string> warnings)
    {
        var status = DescribeStatus(workspaceRoot, request.UsId);
        return new SemanticGraphQueryResult(
            request.QueryKind,
            "status",
            status.GlobalGraph.State,
            false,
            [],
            [],
            [],
            [],
            warnings.ToArray(),
            (int)(Environment.TickCount64 - startedAt),
            status);
    }

    private static SemanticGraphQueryResult BuildFreshnessQueryResult(
        string workspaceRoot,
        SemanticGraphQueryRequest request,
        long startedAt,
        ICollection<string> warnings)
    {
        var status = DescribeStatus(workspaceRoot, request.UsId);
        var inclusionReasons = new List<string> { status.GlobalGraph.Reason ?? "No global graph freshness reason was recorded." };
        if (status.ImpactGraph is not null)
        {
            inclusionReasons.Add(status.ImpactGraph.Reason ?? "No impact graph freshness reason was recorded.");
        }

        return new SemanticGraphQueryResult(
            request.QueryKind,
            request.UsId is null ? "global-graph" : "impact-graph",
            status.ImpactGraph?.State ?? status.GlobalGraph.State,
            false,
            [],
            [],
            [],
            inclusionReasons,
            warnings.ToArray(),
            (int)(Environment.TickCount64 - startedAt),
            new
            {
                global = status.GlobalGraph,
                impact = status.ImpactGraph
            });
    }

    private static SemanticGraphQueryResult BuildNeighborsQueryResult(
        string workspaceRoot,
        SemanticGraphQueryRequest request,
        long startedAt,
        ICollection<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            throw new InvalidOperationException("neighbors:file requires filePath.");
        }

        var globalGraph = TryLoadGlobalGraph(SemanticGraphFilePaths.FromWorkspaceRoot(workspaceRoot).GlobalGraphPath)
            ?? throw new InvalidOperationException("Global graph does not exist yet. Build or refresh it before running neighbors:file.");
        var normalizedFilePath = NormalizePossiblyRelativeSeedPath(workspaceRoot, request.FilePath)
            ?? throw new InvalidOperationException($"File '{request.FilePath}' is outside the workspace root.");
        var owningProject = globalGraph.Projects.FirstOrDefault(project => project.SourceFiles.Contains(normalizedFilePath, StringComparer.Ordinal));
        var siblings = owningProject?.SourceFiles
            .Where(path => !string.Equals(path, normalizedFilePath, StringComparison.Ordinal))
            .Where(path => string.Equals(Path.GetDirectoryName(path), Path.GetDirectoryName(normalizedFilePath), StringComparison.Ordinal))
            .Take(Math.Clamp(request.MaxDepth * 4, 1, 12))
            .ToArray()
            ?? [];
        var includedFiles = siblings.ToList();
        var reasons = siblings.Select(path => $"Sibling source file under the same directory as '{normalizedFilePath}'.").ToList();
        if (owningProject is not null)
        {
            includedFiles.Insert(0, owningProject.Path);
            reasons.Insert(0, $"Owning project for '{normalizedFilePath}'.");
        }

        return new SemanticGraphQueryResult(
            request.QueryKind,
            "global-graph",
            DescribeGlobalStatus(workspaceRoot, SemanticGraphFilePaths.FromWorkspaceRoot(workspaceRoot)).State,
            false,
            includedFiles,
            owningProject is null ? [] : [owningProject.Name],
            owningProject is null ? [] : ["project-ownership"],
            reasons,
            warnings.ToArray(),
            (int)(Environment.TickCount64 - startedAt),
            new
            {
                filePath = normalizedFilePath,
                owningProject = owningProject?.Path,
                neighbors = siblings
            });
    }

    private static SemanticGraphQueryResult BuildTestsAdjacentQueryResult(
        string workspaceRoot,
        SemanticGraphQueryRequest request,
        long startedAt,
        ICollection<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            throw new InvalidOperationException("tests-adjacent:file requires filePath.");
        }

        var globalGraph = TryLoadGlobalGraph(SemanticGraphFilePaths.FromWorkspaceRoot(workspaceRoot).GlobalGraphPath)
            ?? throw new InvalidOperationException("Global graph does not exist yet. Build or refresh it before running tests-adjacent:file.");
        var normalizedFilePath = NormalizePossiblyRelativeSeedPath(workspaceRoot, request.FilePath)
            ?? throw new InvalidOperationException($"File '{request.FilePath}' is outside the workspace root.");
        var owningProject = globalGraph.Projects.FirstOrDefault(project => project.SourceFiles.Contains(normalizedFilePath, StringComparer.Ordinal));
        var adjacentTests = owningProject is null
            ? []
            : globalGraph.Projects
                .Where(project => project.IsTestProject && project.ProjectReferences.Contains(owningProject.Path, StringComparer.Ordinal))
                .Select(project => project.Path)
                .ToArray();

        return new SemanticGraphQueryResult(
            request.QueryKind,
            "global-graph",
            DescribeGlobalStatus(workspaceRoot, SemanticGraphFilePaths.FromWorkspaceRoot(workspaceRoot)).State,
            false,
            adjacentTests,
            [],
            adjacentTests.Select(static _ => "test-adjacency").ToArray(),
            adjacentTests.Select(path => $"Test project '{path}' references owning project '{owningProject?.Path}'.").ToArray(),
            warnings.ToArray(),
            (int)(Environment.TickCount64 - startedAt),
            new
            {
                filePath = normalizedFilePath,
                owningProject = owningProject?.Path,
                tests = adjacentTests
            });
    }

    private static SemanticGraphQueryResult BuildWhyIncludedQueryResult(
        string workspaceRoot,
        SemanticGraphQueryRequest request,
        long startedAt,
        ICollection<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(request.UsId))
        {
            throw new InvalidOperationException("why-included:file requires usId.");
        }

        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            throw new InvalidOperationException("why-included:file requires filePath.");
        }

        var userStoryPaths = UserStoryFilePaths.ResolveFromWorkspaceRoot(workspaceRoot, request.UsId);
        var impactGraph = TryLoadImpactGraph(userStoryPaths.ImpactGraphPath)
            ?? throw new InvalidOperationException("Impact graph does not exist yet for this user story. Materialize it before running why-included:file.");
        var normalizedFilePath = NormalizePossiblyRelativeSeedPath(workspaceRoot, request.FilePath)
            ?? throw new InvalidOperationException($"File '{request.FilePath}' is outside the workspace root.");
        var inclusion = impactGraph.IncludedFiles.FirstOrDefault(item => string.Equals(item.Path, normalizedFilePath, StringComparison.Ordinal));
        if (inclusion is null)
        {
            warnings.Add($"File '{normalizedFilePath}' is not included in the current impact graph.");
        }

        return new SemanticGraphQueryResult(
            request.QueryKind,
            "impact-graph",
            DescribeStatus(workspaceRoot, request.UsId).ImpactGraph?.State ?? "missing",
            string.Equals(impactGraph.DerivationMode, "fallback-derived", StringComparison.Ordinal),
            inclusion is null ? [] : [inclusion.Path],
            [],
            inclusion is null ? [] : [inclusion.Source],
            inclusion is null ? [] : [inclusion.Reason],
            warnings.ToArray(),
            (int)(Environment.TickCount64 - startedAt),
            new
            {
                filePath = normalizedFilePath,
                inclusion
            });
    }

    private static SemanticGraphMetadata? TryLoadMetadata(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<SemanticGraphMetadata>(File.ReadAllText(path), SerializerOptions);
    }

    private static SemanticGlobalGraphArtifact? TryLoadGlobalGraph(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<SemanticGlobalGraphArtifact>(File.ReadAllText(path), SerializerOptions);
    }

    private static SemanticImpactGraphArtifact? TryLoadImpactGraph(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<SemanticImpactGraphArtifact>(File.ReadAllText(path), SerializerOptions);
    }

    private static RefinementGraphScopeRequest? TryLoadGraphScopeRequest(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<RefinementGraphScopeRequest>(File.ReadAllText(path), SerializerOptions);
    }

    private static string NormalizeWorkspaceRelative(string workspaceRoot, string absolutePath)
    {
        var relativePath = Path.GetRelativePath(workspaceRoot, absolutePath);
        return PhaseExecutionReceiptStore.NormalizePath(relativePath);
    }

    private static string? NormalizePossiblyRelativeSeedPath(string workspaceRoot, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var normalizedPath = path.Replace('\\', '/');
        if (!Path.IsPathRooted(path))
        {
            return normalizedPath.TrimStart('/');
        }

        var fullPath = Path.GetFullPath(path);
        var fullWorkspace = Path.GetFullPath(workspaceRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(fullWorkspace, StringComparison.Ordinal))
        {
            return null;
        }

        return NormalizeWorkspaceRelative(workspaceRoot, fullPath);
    }

    private static string ComputeWorkspaceFingerprint(string workspaceRoot)
    {
        var files = EnumerateWorkspaceFiles(workspaceRoot, "*.csproj")
            .Concat(EnumerateWorkspaceFiles(workspaceRoot, "*.sln"))
            .Concat(EnumerateWorkspaceFiles(workspaceRoot, "*.cs"))
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();
        var content = string.Join(
            '\n',
            files.Select(path => $"{NormalizeWorkspaceRelative(workspaceRoot, path)}:{PhaseExecutionReceiptStore.TryComputeFileSha256(path)}"));
        return PhaseExecutionReceiptStore.ComputeSha256(content) ?? string.Empty;
    }

    private static IEnumerable<string> EnumerateWorkspaceFiles(string workspaceRoot, string pattern)
    {
        return Directory.EnumerateFiles(workspaceRoot, pattern, SearchOption.AllDirectories)
            .Where(path => !IsIgnoredPath(path));
    }

    private static bool IsIgnoredPath(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(segment => IgnoredDirectories.Contains(segment, StringComparer.OrdinalIgnoreCase));
    }

    private static string WriteText(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, Encoding.UTF8);
        return path;
    }

    private static string AppendJsonLine(string path, object entry)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.AppendAllText(path, JsonSerializer.Serialize(entry, SerializerOptions) + Environment.NewLine, Encoding.UTF8);
        return path;
    }

    private static string BuildImpactSummaryMarkdown(SemanticImpactGraphArtifact artifact, string reason)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Impact Graph Summary");
        builder.AppendLine();
        builder.AppendLine($"- User story: `{artifact.UsId}`");
        builder.AppendLine($"- Built at: `{artifact.BuiltAtUtc}`");
        builder.AppendLine($"- Derivation mode: `{artifact.DerivationMode}`");
        builder.AppendLine($"- Reason: {reason}");
        builder.AppendLine($"- Included files: `{artifact.IncludedFiles.Count}`");
        if (artifact.Warnings.Count > 0)
        {
            builder.AppendLine($"- Warnings: `{artifact.Warnings.Count}`");
        }

        builder.AppendLine();
        builder.AppendLine("## Included Files");
        builder.AppendLine();
        foreach (var inclusion in artifact.IncludedFiles)
        {
            builder.AppendLine($"- `{inclusion.Path}`");
            builder.AppendLine($"  Reason: {inclusion.Reason}");
            builder.AppendLine($"  Source: `{inclusion.Source}`");
        }

        return builder.ToString();
    }
}
