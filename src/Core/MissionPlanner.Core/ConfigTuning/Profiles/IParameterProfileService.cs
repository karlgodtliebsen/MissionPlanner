namespace MissionPlanner.Core.ConfigTuning.Profiles;

/// <summary>Creates, validates, compares and stages named parameter profiles.</summary>
public interface IParameterProfileService
{
    /// <summary>Creates a profile snapshot from all, modified, or selected session values.</summary>
    ParameterProfile Create(
        IParameterEditSession session,
        string name,
        string? description = null,
        bool modifiedOnly = false,
        IReadOnlyCollection<string>? selectedNames = null,
        IReadOnlyList<string>? tags = null);

    /// <summary>Compares a profile with the current live session and reports compatibility.</summary>
    ParameterProfileReview Review(ParameterProfile profile, IParameterEditSession session);

    /// <summary>Stages selected safe profile differences without writing.</summary>
    IReadOnlyList<string> Stage(
        ParameterProfileReview review,
        IParameterEditSession session,
        IReadOnlyCollection<string> selectedNames);
}
