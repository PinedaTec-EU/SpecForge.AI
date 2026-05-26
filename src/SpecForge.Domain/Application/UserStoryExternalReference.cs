namespace SpecForge.Domain.Application;

public sealed record UserStoryExternalReference(
    string Url,
    string Label,
    string Provider);
