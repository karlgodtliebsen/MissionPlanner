using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Library.EventHub.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities;
using MissionPlanner.App.Presentation;
namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>Owns selected panel state and commands.</summary>
public sealed partial class SelectedFirmwareViewModel : ViewModelBase
{
    private readonly ITextClipboardService clipboard;
    /// <summary>Initializes the selected panel.</summary>
    public SelectedFirmwareViewModel(
        ITextClipboardService clipboard,
        ILogger<SelectedFirmwareViewModel> logger,
        IUiDispatcher dispatcher,
        IDomainEventHub eventHub) : base(logger, dispatcher, eventHub)
    {
        this.clipboard = clipboard;
    }
    /// <summary>Gets the catalogue selection displayed by this panel.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedFirmware))]
    public partial FirmwareCatalogItemViewModel? SelectedFirmware { get; set; }
    /// <summary>Gets whether a release is selected.</summary>
    public bool HasSelectedFirmware => SelectedFirmware is not null;
    [RelayCommand]
    private Task CopyDownloadUrlAsync()
    {
        return SelectedFirmware is null
            ? Task.CompletedTask
            : clipboard.SetTextAsync(SelectedFirmware.Entry.Artifact.DownloadUri.AbsoluteUri);
    }
    /// <summary>Notifies the active parent about panel changes.</summary>
    public event Action<FirmwarePanelRequest>? OperationRequested;
    [RelayCommand]
    private Task DownloadAndValidateAsync(CancellationToken cancellationToken) =>
        FirmwarePanelRequest.SendAsync(OperationRequested, FirmwarePanelAction.Download, cancellationToken);

}
