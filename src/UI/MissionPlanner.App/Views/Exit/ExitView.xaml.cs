using MissionPlanner.App.Configuration;
using MissionPlanner.Library.EventHub.Abstractions;
using UraniumUI.Dialogs;
using UraniumUI.Extensions;
using UraniumUI.Pages;

namespace MissionPlanner.App.Views.Exit;

/// <inheritdoc />
public partial class ExitView : UraniumContentPage
{
    private readonly IDialogService dialogService;
    private readonly IDomainEventHub eventHub;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExitView"/> class.
    /// </summary>
    public ExitView()
    {
        InitializeComponent();
        dialogService = ServiceHelper.GetRequiredService<IDialogService>();
        eventHub = ServiceHelper.GetRequiredService<IDomainEventHub>();
        var viewModel = ServiceHelper.GetRequiredService<ExitViewModel>();
        BindingContext = viewModel;
    }

    /// <inheritdoc />
    protected override void OnAppearing()
    {
        base.OnAppearing();
        ShowExitDialog().FireAndForget();
    }

    private async Task ShowExitDialog()
    {
        var exitContentView = ServiceHelper.GetRequiredService<ExitContentView>();
        var result = await dialogService.DisplayViewAsync(
            "Exit MissionPlanner", exitContentView, "Yes", "Cancel");

        if (result)
        {
            // Handle the exit logic here
            await eventHub.PublishDomainEventAsync(new ExitApplicationRequested());
        }
    }
}
