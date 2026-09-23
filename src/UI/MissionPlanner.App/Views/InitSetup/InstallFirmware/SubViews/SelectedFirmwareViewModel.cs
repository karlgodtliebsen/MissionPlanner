using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.InitSetup.InstallFirmware.SubViews.Models;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware.SubViews;

/// <summary>
/// Owns selected panel state and commands.
/// </summary>
public sealed partial class SelectedFirmwareViewModel : ViewModelBase
{
    private readonly ITextClipboardService clipboard;
    /// <summary>
    /// Initializes the selected panel.
    /// </summary>
    public SelectedFirmwareViewModel(ITextClipboardService clipboard, IUiDispatcher dispatcher, IDomainEventHub eventHub, ILogger<SelectedFirmwareViewModel> logger)
        : base(logger, dispatcher, eventHub)
    {
        this.clipboard = clipboard;
    }

    /// <summary>
    /// Gets the catalogue selection displayed by this panel.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedFirmware))]
    public partial FirmwareCatalogItemViewModel? Selected
    {
        get; set;
    }

    /// <summary>
    /// Gets whether a release is selected.
    /// </summary>
    public bool HasSelectedFirmware => Selected is not null;

    [RelayCommand]
    private Task CopyDownloadUrlAsync()
    {
        if (Selected is not null)
        {
            clipboard.SetTextAsync(Selected.Entry.Artifact.DownloadUri.AbsoluteUri);
            NotificationManager?.Show(Selected.Entry.Artifact.DownloadUri.AbsoluteUri);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Notifies the active parent about panel changes.
    /// </summary>
    public event Action<FirmwarePanelRequest>? OperationRequested;

    [RelayCommand]
    private Task DownloadAndValidateAsync(CancellationToken cancellationToken)
    {
        return FirmwarePanelRequest.SendAsync(OperationRequested, FirmwarePanelAction.Download, cancellationToken);
    }
}
