namespace SpecForge.Domain.Application;

public static class UserStoryKinds
{
    public static readonly string[] Supported =
    [
        "feature",
        "bug",
        "hotfix",
        "chore",
        "refactor",
        "spike"
    ];

    public static bool IsSupported(string? kind) =>
        !string.IsNullOrWhiteSpace(kind)
        && Supported.Contains(kind.Trim().ToLowerInvariant(), StringComparer.Ordinal);
}
