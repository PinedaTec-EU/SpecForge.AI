using SpecForge.Domain.Application;
using SpecForge.OpenAICompatible;

namespace SpecForge.McpServer;

internal static class PhaseExecutionProviderFactory
{
    public static IPhaseExecutionProvider Create() =>
        OpenAiCompatiblePhaseExecutionProviderFactory.Create();
}
