namespace MissionPlanner.App.Theming;

/// <summary>Represents a user-selectable theme policy or concrete palette.</summary>
/// <param name="Id">The stable selection identifier.</param>
/// <param name="DisplayName">The user-facing name.</param>
public sealed record ThemeOption(string Id, string DisplayName)
{
    /// <inheritdoc />
    public override string ToString()
    {
        return DisplayName;
    }
}
