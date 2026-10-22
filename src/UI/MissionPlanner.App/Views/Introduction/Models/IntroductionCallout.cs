namespace MissionPlanner.App.Views.Introduction.Models;

/// <summary>
/// Represents a callout in the introduction view.
/// </summary>
public sealed class IntroductionCallout
{
    /// <summary>Gets or sets the callout number shown on the image.</summary>
    public int Number { get; set; }

    /// <summary>Gets or sets the callout title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the callout text.</summary>
    public string Text { get; set; } = string.Empty;
}

