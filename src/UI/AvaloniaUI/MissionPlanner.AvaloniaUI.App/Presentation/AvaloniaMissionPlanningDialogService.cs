using MissionPlanner.AvaloniaUI.App.Utilities.Dialogs;

namespace MissionPlanner.AvaloniaUI.App.Presentation;

/// <summary>Adapts mission-planning prompt contracts to the shared application dialog service.</summary>
public sealed class AvaloniaMissionPlanningDialogService(IDialogService dialogService)
    : IUserPromptService, IUserChoiceService
{
    public Task<bool> ConfirmAsync(string title, string message, string acceptText,
        CancellationToken cancellationToken = default) =>
        dialogService.ConfirmAsync(message, new DialogOptions
        {
            Title = title,
            Width = 460,
            Height = 220,
            CanResize = false,
            OkText = acceptText,
            CloseText = "Cancel"
        }, cancellationToken);

    public Task<string?> PromptAsync(string title, string message, string? initialValue = null,
        CancellationToken cancellationToken = default) =>
        dialogService.PromptAsync(new DialogOptions
        {
            Title = title,
            Width = 460,
            Height = 220,
            CanResize = false,
            OkText = "OK",
            CloseText = "Cancel"
        }, message, initialValue, cancellationToken);

    public Task<string?> ChooseAsync(string title, IReadOnlyList<string> choices,
        CancellationToken cancellationToken = default) =>
        dialogService.ChooseAsync(new DialogOptions
        {
            Title = title,
            Width = 460,
            Height = 220,
            CanResize = false,
            OkText = "OK",
            CloseText = "Cancel"
        }, choices, cancellationToken);
}
