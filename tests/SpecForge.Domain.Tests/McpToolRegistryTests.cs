using System.Text.Json.Nodes;
using SpecForge.McpServer;

namespace SpecForge.Domain.Tests;

public sealed class McpToolRegistryTests
{
    [Fact]
    public void BuildToolsList_IncludesCompactFacadesAndGranularCompatibilityTools()
    {
        var toolNames = GetToolNames(McpToolRegistry.BuildToolsList());

        Assert.Contains("specforge_query", toolNames);
        Assert.Contains("specforge_action", toolNames);
        Assert.Contains("specforge_prompts", toolNames);
        Assert.Contains("create_us_from_chat", toolNames);
        Assert.Contains("generate_next_phase", toolNames);
        Assert.Contains("approve_phase", toolNames);
    }

    [Fact]
    public void BuildToolsList_SpecForgeActionRequiresWorkspaceRootAndAction()
    {
        var tool = GetTool(McpToolRegistry.BuildToolsList(), "specforge_action");
        var required = tool["inputSchema"]?["required"]?.AsArray()
            .Select(static item => item!.GetValue<string>())
            .ToArray();

        Assert.Equal(["workspaceRoot", "action"], required!);
    }

    [Fact]
    public void BuildToolsList_SpecForgeActionExposesGoalDecompositionAction()
    {
        var tool = GetTool(McpToolRegistry.BuildToolsList(), "specforge_action");
        var actionEnum = tool["inputSchema"]?["properties"]?["action"]?["enum"]?.AsArray()
            .Select(static item => item!.GetValue<string>())
            .ToArray();

        Assert.Contains("create_user_stories_from_goal", actionEnum!);
    }

    [Fact]
    public void BuildToolsList_CreateFromChatDocumentsRefinementForVagueCapture()
    {
        var tool = GetTool(McpToolRegistry.BuildToolsList(), "create_us_from_chat");
        var description = tool["inputSchema"]?["properties"]?["sourceText"]?["description"]?.GetValue<string>();

        Assert.Contains("refinement will keep asking", description);
        Assert.Contains("buildable", description);
    }

    [Fact]
    public void BuildToolsList_GeneratesValidObjectSchemas()
    {
        foreach (var tool in McpToolRegistry.BuildToolsList()["tools"]!.AsArray().Cast<JsonObject>())
        {
            Assert.False(string.IsNullOrWhiteSpace(tool["name"]?.GetValue<string>()));
            Assert.Equal("object", tool["inputSchema"]?["type"]?.GetValue<string>());
            Assert.NotNull(tool["inputSchema"]?["properties"]?.AsObject());
            Assert.NotNull(tool["inputSchema"]?["required"]?.AsArray());
        }
    }

    private static IReadOnlyCollection<string> GetToolNames(JsonObject toolsList) =>
        toolsList["tools"]!.AsArray()
            .Select(static item => item!["name"]!.GetValue<string>())
            .ToArray();

    private static JsonObject GetTool(JsonObject toolsList, string toolName) =>
        toolsList["tools"]!.AsArray()
            .Select(static item => item!.AsObject())
            .Single(tool => string.Equals(tool["name"]?.GetValue<string>(), toolName, StringComparison.Ordinal));
}
