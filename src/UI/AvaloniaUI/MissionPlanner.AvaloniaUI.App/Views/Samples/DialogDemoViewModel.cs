using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.AvaloniaUI.App.Utilities;
using MissionPlanner.AvaloniaUI.App.Utilities.Dialogs;
using MissionPlanner.AvaloniaUI.App.Views.Connect;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using ErrorView = MissionPlanner.AvaloniaUI.App.Utilities.Dialogs.SubViews.ErrorView;
using ErrorViewModel = MissionPlanner.AvaloniaUI.App.Utilities.Dialogs.SubViews.ErrorViewModel;

namespace MissionPlanner.AvaloniaUI.App.Views.Samples;

/// <summary>Coordinates persisted simulator profiles and the observable simulation session.</summary>
public sealed partial class DialogDemoViewModel : ViewModelBase
{
    private readonly IDialogService dialogService;
    private readonly IServiceFactory serviceFactory;
    private readonly IDomainFactory domainFactory;

    /// <inheritdoc />
    public DialogDemoViewModel(IDialogService dialogService, IServiceFactory serviceFactory, IDomainFactory domainFactory, ILogger<DialogDemoViewModel> logger) : base(logger)
    {
        this.serviceFactory = serviceFactory;
        this.domainFactory = domainFactory;
        this.dialogService = dialogService;
    }

    [RelayCommand]
    private async Task ShowConnect(CancellationToken cancellationToken)
    {
        var options = AvaloniaDialogService.CreateDialogOptions("Connect Vehicle", "Ok", null);
        var viewModel = serviceFactory.Create<ConnectPopupViewModel>();
        var result = await dialogService.ShowOverlayDialogAsync<ConnectPopupView, ConnectPopupViewModel>(viewModel, options, cancellationToken: cancellationToken);
        StatusMessage = result.SelectedChannel;
    }

    [RelayCommand]
    private async Task ShowError(CancellationToken cancellationToken)
    {
        var options = AvaloniaDialogService.CreateDialogOptions("Error Occured", "Ok", null);
        var viewModel = domainFactory.Create<ErrorViewModel, string>("The exception Message" + "\nEnsure there is a connection and try again");
        var result = await dialogService.ShowOverlayDialogAsync<ErrorView, ErrorViewModel>(viewModel, options, cancellationToken: cancellationToken);
        StatusMessage = result.ErrorMessage;
    }

    [RelayCommand]
    private async Task ShowConfirm(CancellationToken cancellationToken)
    {
        var options = AvaloniaDialogService.CreateDialogOptions("Please Confirm", "Ok", null);
        var message = "Would you like a Cold Beer";
        var result = await dialogService.ConfirmAsync(options, message, cancellationToken);
        StatusMessage = result.ToString();
    }


    [RelayCommand]
    private async Task ShowConfirm1(CancellationToken cancellationToken)
    {
        var options = AvaloniaDialogService.CreateDialogOptions("Please Confirm", "Ok", null);
        var message = "Would you like a Cold Beer";
        var result = await dialogService.ConfirmAsync(options, message, cancellationToken);
        StatusMessage = result.ToString();
    }


    [RelayCommand]
    private async Task ShowStringPrompt(CancellationToken cancellationToken)
    {
        var result = await dialogService.PromptAsync("the title", "the message", "initial value", cancellationToken: cancellationToken);
        StatusMessage = result ?? "";
    }
    [RelayCommand]
    private async Task ShowStringPrompt2(CancellationToken cancellationToken)
    {
        var options = AvaloniaDialogService.CreateDialogOptions("The title", "Ok", null);
        var result = await dialogService.PromptAsync(options, "the message", "initial value", cancellationToken: cancellationToken);
        StatusMessage = result ?? "";
    }

    [RelayCommand]
    private async Task ShowIntPrompt(CancellationToken cancellationToken)
    {
        var options = AvaloniaDialogService.CreateDialogOptions("Please Confirm", "Ok", null);

        int initialValue = 0, minimum = 0, maximum = 100;
        var result = await dialogService.PromptAsync(options, "the message", initialValue, minimum, maximum, cancellationToken: cancellationToken);
        StatusMessage = result.ToString();
    }

    [RelayCommand]
    private async Task ShowIntPrompt2(CancellationToken cancellationToken)
    {
        int initialValue = 0, minimum = 0, maximum = 100;
        var result = await dialogService.PromptAsync("the title", "the message", initialValue, minimum, maximum, cancellationToken: cancellationToken);
        StatusMessage = result.ToString();
    }

    [RelayCommand]
    private async Task ShowDoublePrompt(CancellationToken cancellationToken)
    {
        var options = AvaloniaDialogService.CreateDialogOptions("the title", "Ok", null);

        double initialValue = 0.0, minimum = 0.0, maximum = 100.0;
        var result = await dialogService.PromptAsync(options, "the message", initialValue, minimum, maximum, cancellationToken: cancellationToken);
        StatusMessage = result.ToString();
    }

    [RelayCommand]
    private async Task ShowDoublePrompt2(CancellationToken cancellationToken)
    {
        double initialValue = 0.0, minimum = 0.0, maximum = 100.0;
        var result = await dialogService.PromptAsync("the title", "the message", initialValue, minimum, maximum, cancellationToken: cancellationToken);
        StatusMessage = result.ToString();
    }

    [RelayCommand]
    private async Task ShowChoosePrompt(CancellationToken cancellationToken)
    {
        var options = AvaloniaDialogService.CreateDialogOptions("Please Choose", "Ok", null);
        var result = await dialogService.ChooseAsync(options, ["Option 1", "Option 2", "Option 3"], cancellationToken: cancellationToken);
        StatusMessage = result ?? "";
    }


    [RelayCommand]
    private async Task ShowPhrasePrompt(CancellationToken cancellationToken)
    {
        var platform = "Pixhawk";
        var boardId = 1234;
        var requiredPhrase = $"FLASH {platform}";
        var options = AvaloniaDialogService.CreateDialogOptions("Confirm initial ArduPilot installation", "Continue", null);
        var message = $"This replaces Betaflight and installs ArduPilot plus its bootloader for {platform}{(boardId is int id ? $"(board ID {id})" : string.Empty)}. \nType exactly: {requiredPhrase}";
        var result = await dialogService.PromptAsync(options, message, string.Empty, cancellationToken);
        StatusMessage = result ?? "";
    }


}

