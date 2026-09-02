namespace MissionPlanner.AvaloniaUI.App.Presentation;

///// <summary>Adapts mission-planning prompt contracts to the shared application dialog service.</summary>
//public sealed class AvaloniaMissionPlanningDialogService(IDialogService dialogService)
//    : IUserPromptService, IUserChoiceService
//{
//    public Task<bool> ConfirmAsync(string title, string message, string acceptText, CancellationToken cancellationToken = default)
//    {
//        var options = AvaloniaDialogService.CreateDialogOptions(title, acceptText, "Cancel");
//        return dialogService.ConfirmAsync(options, message, cancellationToken);
//    }

//    public Task<string?> PromptAsync(string title, string message, string? initialValue = null, CancellationToken cancellationToken = default)
//    {
//        var options = AvaloniaDialogService.CreateDialogOptions(title, "Ok", "Cancel");
//        return dialogService.PromptAsync(options, message, initialValue, cancellationToken);
//    }

//    public Task<string?> ChooseAsync(string title, IReadOnlyList<string> choices, CancellationToken cancellationToken = default)
//    {
//        var options = AvaloniaDialogService.CreateDialogOptions(title, "Ok", "Cancel");
//        return dialogService.ChooseAsync(options, choices, cancellationToken);
//    }
//}
