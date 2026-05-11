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
        Assert.Contains("open_workflow_portal", toolNames);
        Assert.Contains("create_us_from_chat", toolNames);
        Assert.Contains("generate_next_phase", toolNames);
        Assert.Contains("approve_phase", toolNames);
    }

    [Fact]
    public void BuildToolsList_DoesNotExposeDuplicateToolNames()
    {
        var toolNames = GetToolNames(McpToolRegistry.BuildToolsList());

        Assert.Equal(toolNames.Count, toolNames.Distinct(StringComparer.Ordinal).Count());
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
    public void BuildToolsList_SpecForgeQueryRequiresWorkspaceRootAndQuery()
    {
        var tool = GetTool(McpToolRegistry.BuildToolsList(), "specforge_query");
        var required = GetRequiredProperties(tool);

        Assert.Equal(["workspaceRoot", "query"], required);
    }

    [Fact]
    public void BuildToolsList_SpecForgePromptsRequiresWorkspaceRootAndOperation()
    {
        var tool = GetTool(McpToolRegistry.BuildToolsList(), "specforge_prompts");
        var required = GetRequiredProperties(tool);

        Assert.Equal(["workspaceRoot", "operation"], required);
    }

    [Fact]
    public void BuildToolsList_OpenWorkflowPortalRequiresWorkspaceRootAndUsId()
    {
        var tool = GetTool(McpToolRegistry.BuildToolsList(), "open_workflow_portal");
        var required = GetRequiredProperties(tool);

        Assert.Equal(["workspaceRoot", "usId"], required);
    }

    [Fact]
    public void BuildToolsList_SpecForgeActionExposesGoalDecompositionAction()
    {
        var tool = GetTool(McpToolRegistry.BuildToolsList(), "specforge_action");
        var actionEnum = tool["inputSchema"]?["properties"]?["action"]?["enum"]?.AsArray()
            .Select(static item => item!.GetValue<string>())
            .ToArray();

        Assert.Contains("create_user_stories_from_goal", actionEnum!);
        Assert.Contains("update_user_story_info", actionEnum!);
    }

    [Fact]
    public void BuildToolsList_SpecForgeQueryExposesEveryCompactReadOperation()
    {
        var tool = GetTool(McpToolRegistry.BuildToolsList(), "specforge_query");
        var queryEnum = GetEnumValues(tool, "query");

        Assert.Equal(
            [
                "list_user_stories",
                "summary",
                "workflow",
                "current_phase",
                "runtime_status",
                "lineage",
                "files"
            ],
            queryEnum);
    }

    [Fact]
    public void BuildToolsList_PhaseMovementToolsRestrictTargetPhaseValues()
    {
        foreach (var toolName in new[] { "request_regression", "rewind_workflow" })
        {
            var tool = GetTool(McpToolRegistry.BuildToolsList(), toolName);
            var phases = GetEnumValues(tool, "targetPhase");

            Assert.Equal(
                [
                    "capture",
                    "refinement",
                    "spec",
                    "technical-design",
                    "implementation",
                    "review",
                    "release-approval",
                    "pr-preparation"
                ],
                phases);
        }
    }

    [Fact]
    public void BuildToolsList_FileMutationToolsRestrictKindValues()
    {
        foreach (var toolName in new[] { "add_user_story_files", "set_user_story_file_kind" })
        {
            var tool = GetTool(McpToolRegistry.BuildToolsList(), toolName);
            var kinds = GetEnumValues(tool, "kind");

            Assert.Equal(["context", "attachment"], kinds);
        }
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

    [Fact]
    public void BuildToolsList_EveryRequiredPropertyIsDeclaredInSchema()
    {
        foreach (var tool in McpToolRegistry.BuildToolsList()["tools"]!.AsArray().Cast<JsonObject>())
        {
            var properties = tool["inputSchema"]!["properties"]!.AsObject();
            foreach (var requiredProperty in GetRequiredProperties(tool))
            {
                Assert.True(
                    properties.ContainsKey(requiredProperty),
                    $"Tool '{tool["name"]?.GetValue<string>()}' requires undeclared property '{requiredProperty}'.");
            }
        }
    }

    [Fact]
    public void BuildToolsList_ArrayPropertiesDeclareStringItems()
    {
        var submitRefinementAnswers = GetTool(McpToolRegistry.BuildToolsList(), "submit_refinement_answers");
        var createFromChat = GetTool(McpToolRegistry.BuildToolsList(), "create_us_from_chat");
        var importFromMarkdown = GetTool(McpToolRegistry.BuildToolsList(), "import_us_from_markdown");
        var addFiles = GetTool(McpToolRegistry.BuildToolsList(), "add_user_story_files");

        Assert.Equal("string", GetArrayItemType(submitRefinementAnswers, "answers"));
        Assert.Equal("string", GetArrayItemType(createFromChat, "tags"));
        Assert.Equal("string", GetArrayItemType(importFromMarkdown, "tags"));
        Assert.Equal("string", GetArrayItemType(addFiles, "sourcePaths"));
    }

    private static IReadOnlyCollection<string> GetToolNames(JsonObject toolsList) =>
        toolsList["tools"]!.AsArray()
            .Select(static item => item!["name"]!.GetValue<string>())
            .ToArray();

    private static JsonObject GetTool(JsonObject toolsList, string toolName) =>
        toolsList["tools"]!.AsArray()
            .Select(static item => item!.AsObject())
            .Single(tool => string.Equals(tool["name"]?.GetValue<string>(), toolName, StringComparison.Ordinal));

    private static string[] GetRequiredProperties(JsonObject tool) =>
        tool["inputSchema"]!["required"]!.AsArray()
            .Select(static item => item!.GetValue<string>())
            .ToArray();

    private static string[] GetEnumValues(JsonObject tool, string propertyName) =>
        tool["inputSchema"]!["properties"]![propertyName]!["enum"]!.AsArray()
            .Select(static item => item!.GetValue<string>())
            .ToArray();

    private static string GetArrayItemType(JsonObject tool, string propertyName) =>
        tool["inputSchema"]!["properties"]![propertyName]!["items"]!["type"]!.GetValue<string>();
}
