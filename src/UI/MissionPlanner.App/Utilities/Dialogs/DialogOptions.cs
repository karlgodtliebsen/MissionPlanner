namespace MissionPlanner.App.Utilities.Dialogs;

public sealed record DialogOptions
{
    public DialogPresentation Presentation
    {
        get; init;
    }
        = DialogPresentation.Window;

    public string Title { get; init; } = string.Empty;

    public double? Width { get; init; } = 460;

    public double? Height { get; init; } = 300;

    public string OkText { get; init; } = "OK";

    public string CloseText { get; init; } = "Close";

    public bool CanResize { get; init; } = true;

    public bool CanLightDismiss
    {
        get; init;
    }


    public bool ShowOkButton { get; init; } = true;

    public bool ShowCloseButton { get; init; } = true;

    /// <summary>
    /// Gets an optional progress-dialog cancellation request. When supplied, Cancel and
    /// window-close request cancellation; the caller disposes the handle when work safely ends.
    /// </summary>
    public Action? RequestCancellation { get; init; }
}
