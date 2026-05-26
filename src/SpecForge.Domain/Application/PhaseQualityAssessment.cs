namespace SpecForge.Domain.Application;

public sealed record PhaseQualityAssessment(
    string PhaseId,
    int QualityScore,
    int ConfidenceScore,
    int GateScore,
    string ComparableInputFingerprint,
    string Decision = "measured",
    int? ThresholdPercent = null,
    bool? MeetsThreshold = null,
    string? SelectedArtifactPath = null,
    string? PreviousBestArtifactPath = null,
    string? Summary = null);
