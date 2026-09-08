namespace MissionPlanner.App.Views.Common;

/// <summary>Describes one stable Optional Hardware tab and its availability signature.</summary>
public sealed record TabDescriptor(string Key, string Title, string Description, string? ConfigDestination = null);
