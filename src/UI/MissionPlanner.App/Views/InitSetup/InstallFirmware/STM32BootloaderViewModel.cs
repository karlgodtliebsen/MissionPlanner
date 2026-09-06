using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>Owns dfu panel state and commands.</summary>
public sealed partial class STM32BootloaderViewModel : ViewModelBase
{
    private readonly IFirmwareFilePicker filePicker;
    /// <summary>Initializes the dfu panel.</summary>
    public STM32BootloaderViewModel(
        IFirmwareFilePicker filePicker,
        SelectedFirmwareViewModel selected,
        ILogger<STM32BootloaderViewModel> logger,
        IUiDispatcher dispatcher,
        IDomainEventHub eventHub) : base(logger, dispatcher, eventHub)
    {
        this.filePicker = filePicker;
        Selected = selected;
    }
    /// <summary>Gets the shared selected panel.</summary>
    public SelectedFirmwareViewModel Selected
    {
        get;
    }
    [ObservableProperty]
    public partial IReadOnlyList<DfuDeviceItemViewModel> DfuDevices
    {
        get;
        set;
    } = [];

    [ObservableProperty]
    public partial DfuDeviceItemViewModel? SelectedDfuDevice
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial string DfuStatus { get; set; } = "Enter STM32 DFU mode, then refresh the catalogue.";

    [ObservableProperty]
    public partial string? LocalDfuFirmwarePath
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial string? LocalDfuFirmwareName
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial string? LocalDfuPlatform
    {
        get;
        set;
    }

    /// <summary>Gets whether a local combined application-and-bootloader HEX file is selected.</summary>
    public bool HasLocalDfuFirmware => !string.IsNullOrWhiteSpace(LocalDfuFirmwarePath);

    /// <summary>Gets whether an STM32 DFU device is selected.</summary>
    public bool HasDfuBootLoader => SelectedDfuDevice is not null;

    private CancellationTokenSource? viewLifetime;

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        viewLifetime ??= new CancellationTokenSource();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        var previous = viewLifetime;
        viewLifetime = null;
        previous?.Cancel();
        previous?.Dispose();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        var previous = viewLifetime;
        viewLifetime = null;
        previous?.Cancel();
        previous?.Dispose();
        base.Dispose();
    }

    [RelayCommand(CanExecute = nameof(HasDfuBootLoader))]
    private async Task LoadCustomBlWithFirmwareAsync(CancellationToken cancellationToken)
    {
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, viewLifetime?.Token ?? CancellationToken.None);
        cancellationToken = operation.Token;
        try
        {
            var file = await filePicker.PickAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (file is null)
            {
                return;
            }

            var extension = Path.GetExtension(file.FileName);

            if (!extension.Equals(".hex", StringComparison.OrdinalIgnoreCase))
            {
                SetMessages("Only .hex firmware packages are supported by the modern bootloader workflow.");
                NotificationManager?.Show(StatusMessage ?? "");
                return;
            }

            if (!file.FileName.EndsWith("_with_bl.hex", StringComparison.OrdinalIgnoreCase))
            {
                SetMessages("For STM32 DFU installation, select a combined application-and-bootloader file named *_with_bl.hex.");
                NotificationManager?.Show(StatusMessage ?? "");
                return;
            }

            if (string.IsNullOrWhiteSpace(file.LocalPath) || !File.Exists(file.LocalPath))
            {
                SetMessages("The selected HEX file does not expose a local path required by STM32CubeProgrammer.");
                NotificationManager?.Show(StatusMessage ?? "");
                return;
            }

            LocalDfuFirmwarePath = Path.GetFullPath(file.LocalPath);
            LocalDfuFirmwareName = file.FileName;

            SetMessages("Local *_with_bl.hex selected. Enter its exact ArduPilot platform and select the detected STM32 DFU device.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Custom firmware selection failed.");

            SetMessages(exception);
            NotificationManager?.Show(ErrorMessage ?? "");
        }
    }

    [RelayCommand]
    private void ClearLocalDfuFirmware()
    {
        LocalDfuFirmwarePath = null;
        LocalDfuFirmwareName = null;
        LocalDfuPlatform = null;
        SetMessages("Local STM32 DFU firmware selection cleared.");
    }
    /// <summary>Notifies the active parent about panel changes.</summary>
    public event Action<DfuDeviceItemViewModel?>? SelectionChanged;
    /// <summary>Notifies the active parent about panel changes.</summary>
    public event Action<string?>? LocalFirmwareChanged;
    /// <summary>Notifies the active parent about panel changes.</summary>
    public event Action<string?>? PlatformChanged;
    /// <summary>Notifies the active parent about panel changes.</summary>
    public event Action<FirmwarePanelRequest>? OperationRequested;
    [RelayCommand]
    private Task RefreshCatalogAsync(CancellationToken cancellationToken)
    {
        return FirmwarePanelRequest.SendAsync(OperationRequested, FirmwarePanelAction.Refresh, cancellationToken);
    }

    [RelayCommand]
    private Task DownloadAndValidateAsync(CancellationToken cancellationToken)
    {
        return FirmwarePanelRequest.SendAsync(OperationRequested, FirmwarePanelAction.Download, cancellationToken);
    }

    [RelayCommand(CanExecute = nameof(CanInstallDfu))]
    private Task InstallDfuFirmwareAsync(CancellationToken cancellationToken)
    {
        return FirmwarePanelRequest.SendAsync(OperationRequested, FirmwarePanelAction.InstallDfu, cancellationToken);
    }

    /// <summary>Gets whether the parent permits DFU installation.</summary>
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(InstallDfuFirmwareCommand))]
    public partial bool CanInstallDfu
    {
        get; set;
    }

    partial void OnSelectedDfuDeviceChanged(DfuDeviceItemViewModel? value)
    {
        OnPropertyChanged(nameof(HasDfuBootLoader));
        LoadCustomBlWithFirmwareCommand.NotifyCanExecuteChanged();
        SelectionChanged?.Invoke(value);
    }
    partial void OnLocalDfuFirmwarePathChanged(string? value)
    {
        OnPropertyChanged(nameof(HasLocalDfuFirmware));
        LocalFirmwareChanged?.Invoke(value);
    }
    partial void OnLocalDfuPlatformChanged(string? value) => PlatformChanged?.Invoke(value);

    public void Reset()
    {
        CanInstallDfu = false;
    }
}
