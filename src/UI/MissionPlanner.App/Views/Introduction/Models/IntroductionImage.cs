namespace MissionPlanner.App.Views.Introduction.Models;

/// <summary>
/// Represents an image used in the introduction view.
/// </summary>
public sealed class IntroductionImage
{
    /// <summary>
    /// Logical app-package path relative to the Introduction asset root,
    /// for example "Images/topbar.png".
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Caption for the introduction image.
    /// </summary>
    public string? Caption { get; set; }

    /// <summary>
    /// Maximum visual height used by the introduction image control.
    /// The image is rendered with AspectFit.
    /// </summary>
    public double Height { get; set; } = 460;
}
