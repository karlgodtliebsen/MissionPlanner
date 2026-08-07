namespace MissionPlanner.Core.FlightData.Preflight;

/// <summary>Contains one explainable preflight readiness result.</summary>
public sealed record PreflightCheckResult(
    string Key,
    PreflightCheckCategory Category,
    string Title,
    PreflightCheckStatus Status,
    string Summary,
    PreflightEvidence Evidence,
    string Remediation,
    IReadOnlyList<string> RelatedParameters)
{
    ///// <inheritdoc />
    ///// <inheritdoc />
    //public bool Equals(PreflightCheckResult? other)
    //{
    //    return ReferenceEquals(this, other)
    //        ? true
    //        : other is null
    //        ? false
    //        : Key == other.Key &&
    //          Category == other.Category &&
    //          Title == other.Title &&
    //          Status == other.Status &&
    //          Summary == other.Summary &&
    //          EqualityComparer<PreflightEvidence>.Default.Equals(Evidence, other.Evidence) &&
    //          Remediation == other.Remediation &&
    //          RelatedParameters.SequenceEqual(other.RelatedParameters);
    //}
}
