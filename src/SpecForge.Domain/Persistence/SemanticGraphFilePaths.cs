namespace SpecForge.Domain.Persistence;

public sealed class SemanticGraphFilePaths
{
    public SemanticGraphFilePaths(string graphsDirectoryPath)
    {
        if (string.IsNullOrWhiteSpace(graphsDirectoryPath))
        {
            throw new ArgumentException("Graphs directory path is required.", nameof(graphsDirectoryPath));
        }

        GraphsDirectoryPath = graphsDirectoryPath;
        GlobalGraphPath = Path.Combine(graphsDirectoryPath, "global-graph.json");
        GlobalGraphMetadataPath = Path.Combine(graphsDirectoryPath, "global-graph.meta.json");
        GraphBuildLogPath = Path.Combine(graphsDirectoryPath, "graph-build-log.jsonl");
        GraphCostLedgerPath = Path.Combine(graphsDirectoryPath, "graph-cost-ledger.json");
    }

    public static SemanticGraphFilePaths FromWorkspaceRoot(string workspaceRoot)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            throw new ArgumentException("Workspace root is required.", nameof(workspaceRoot));
        }

        return new SemanticGraphFilePaths(Path.Combine(workspaceRoot, ".specs", "cache", "graphs"));
    }

    public string GraphsDirectoryPath { get; }

    public string GlobalGraphPath { get; }

    public string GlobalGraphMetadataPath { get; }

    public string GraphBuildLogPath { get; }

    public string GraphCostLedgerPath { get; }
}
