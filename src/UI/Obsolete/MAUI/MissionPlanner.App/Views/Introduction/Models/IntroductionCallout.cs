namespace MissionPlanner.App.Views.Introduction.Models;

/// <summary>
/// Represents a callout in the introduction view.
/// </summary>
public sealed class IntroductionCallout
{
    public int Number { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;
}
