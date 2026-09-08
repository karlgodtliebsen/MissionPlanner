namespace MissionPlanner.App.Theming;

/// <summary>Describes one concrete MissionPlanner color palette.</summary>
/// <param name="Id">The stable persisted identifier.</param>
/// <param name="DisplayName">The user-facing theme name.</param>
/// <param name="BaseAppearance">The native-control base appearance.</param>
/// <param name="ResourcePath">The MAUI resource dictionary path.</param>
public sealed record ThemeDescriptor(
    string Id,
    string DisplayName,
    ThemeBaseAppearance BaseAppearance,
    string ResourcePath);
