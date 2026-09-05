namespace MissionPlanner.App.Utilities.Dialogs;

public sealed record DialogOptions
{
    public DialogPresentation Presentation
    {
        get; init;
    }
        = DialogPresentation.Window;

    public string Title { get; init; } = string.Empty;

    public double? Width { get; init; } = 800;

    public double? Height { get; init; } = 600;

    public string OkText { get; init; } = "OK";

    public string CloseText { get; init; } = "Close";

    public bool CanResize { get; init; } = true;

    public bool CanLightDismiss
    {
        get; init;
    }

    public bool ShowOkButton { get; init; } = true;

    public bool ShowCloseButton { get; init; } = true;
}
