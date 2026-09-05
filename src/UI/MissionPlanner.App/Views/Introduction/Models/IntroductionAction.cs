namespace MissionPlanner.App.Views.Introduction.Models;

/// <summary>
/// Represents an action that can be performed in the introduction view.
/// </summary>
public sealed class IntroductionAction
{
    public string Label { get; set; } = string.Empty;

    public IntroductionActionKind Kind { get; set; } = IntroductionActionKind.Topic;

    /// <summary>
    /// Topic id, Shell route, or URI depending on <see cref="Kind"/>.
    /// Not used for <see cref="IntroductionActionKind.Back"/>.
    /// </summary>
    public string? Target { get; set; }
}

