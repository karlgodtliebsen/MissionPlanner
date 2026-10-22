namespace MissionPlanner.App.Utilities.Dialogs;

/// <summary>Options that control how a dialog is presented.</summary>
public sealed record DialogOptions
{
    /// <summary>Gets how the dialog is presented, as a window or an overlay.</summary>
    public DialogPresentation Presentation
    {
        get; init;
    }
        = DialogPresentation.Window;

    /// <summary>Gets the dialog title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>The owning window's overlay host, or null for the main window.</summary>
    public string? HostId { get; init; }

    /// <summary>Gets the dialog width, or null to size to the content.</summary>
    public double? Width { get; init; } = 460;

    /// <summary>Gets the dialog height, or null to size to the content.</summary>
    public double? Height { get; init; } = 300;

    /// <summary>Gets the text of the OK button.</summary>
    public string OkText { get; init; } = "OK";

    /// <summary>Gets the text of the Close button.</summary>
    public string CloseText { get; init; } = "Close";

    /// <summary>Gets whether the user can resize the dialog.</summary>
    public bool CanResize { get; init; } = true;

    /// <summary>Gets whether clicking outside the dialog dismisses it.</summary>
    public bool CanLightDismiss
    {
        get; init;
    }


    /// <summary>Gets whether the OK button is shown.</summary>
    public bool ShowOkButton { get; init; } = true;

    /// <summary>Gets whether the Close button is shown.</summary>
    public bool ShowCloseButton { get; init; } = true;

    /// <summary>
    /// Gets an optional progress-dialog cancellation request. When supplied, Cancel and
    /// dialog-close request cancellation; the caller disposes the handle when work safely ends.
    /// </summary>
    public Action? RequestCancellation { get; init; }
}
