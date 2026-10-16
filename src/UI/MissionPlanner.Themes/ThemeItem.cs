using Avalonia.Styling;

namespace MissionPlanner.Themes;

/// <summary>Describes an Avalonia theme that can be selected by the user.</summary>
public sealed class ThemeItem(string id, string name, ThemeVariant theme)
{
    /// <summary>Gets the stable identifier stored in Planner settings.</summary>
    public string Id { get; } = id;

    /// <summary>Gets the user-facing theme name.</summary>
    public string Name { get; } = name;

    /// <summary>Gets the Avalonia theme variant.</summary>
    public ThemeVariant Theme { get; } = theme;
}
