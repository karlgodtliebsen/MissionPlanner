using MissionPlanner.Library.EventHub.Abstractions;
using UraniumUI.Dialogs;
using UraniumUI.Extensions;
using UraniumUI.Pages;

namespace MissionPlanner.App.Views.Exit;

/// <inheritdoc />
public partial class ExitView : UraniumContentPage
{
    private readonly IDialogService dialogService;
    private readonly ExitContentView exitContentView;
    private readonly IDomainEventHub eventHub;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExitView"/> class.
    /// </summary>
    /// <param name="dialogService"></param>
    /// <param name="exitContentView">The content view for the exit view.</param>
    /// <param name="viewModel">The view model for the exit view.</param>
    /// <param name="eventHub">The domain event hub.</param>
    public ExitView(IDialogService dialogService, ExitContentView exitContentView, ExitViewModel viewModel, IDomainEventHub eventHub)
    {
        this.dialogService = dialogService;
        this.exitContentView = exitContentView;
        this.eventHub = eventHub;
        InitializeComponent();
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
        var result = await dialogService.DisplayViewAsync(
            "Exit MissionPlanner", exitContentView, "Ok", "Cancel");

        if (result)
        {
            // Handle the exit logic here
            await eventHub.PublishDomainEventAsync(new ExitApplicationRequested());
        }
    }
}
