using MissionPlanner.AvaloniaUI.App.Utilities.Dispatching;

namespace MissionPlanner.AvaloniaUI.App.Presentation;

/// <summary>MAUI implementation of mission-planning prompts and choices.</summary>
public sealed class MauiMissionPlanningDialogService(IUiDispatcher dispatcher) : IUserPromptService, IUserChoiceService
{
    /// <inheritdoc />
    public async Task<string?> PromptAsync(string title, string message, string? initialValue = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();

        //cancellationToken.ThrowIfCancellationRequested();
        //string? result = null;
        //await dispatcher.DispatchAsync(async () =>
        //{
        //    var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        //    if (page is not null)
        //    {
        //        result = await page.DisplayPromptAsync(title, message, initialValue: initialValue);
        //    }
        //});
        //cancellationToken.ThrowIfCancellationRequested();
        //return result;
    }

    /// <inheritdoc />
    public async Task<string?> ChooseAsync(string title, IReadOnlyList<string> choices, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();

        //cancellationToken.ThrowIfCancellationRequested();
        //string? result = null;
        //await dispatcher.DispatchAsync(async () =>
        //{
        //    var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        //    if (page is not null)
        //    {
        //        result = await page.DisplayActionSheetAsync(title, "Cancel", null, choices.ToArray());
        //    }
        //});
        //cancellationToken.ThrowIfCancellationRequested();
        //return result == "Cancel" ? null : result;
    }
}
