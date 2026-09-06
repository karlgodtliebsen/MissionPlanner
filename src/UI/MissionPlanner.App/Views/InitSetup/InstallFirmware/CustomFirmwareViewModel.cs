using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Firmware.Images;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Library.EventHub.Abstractions;
namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>Owns custom panel state and commands.</summary>
public sealed partial class CustomFirmwareViewModel : ViewModelBase
{
    private readonly IFirmwareFilePicker filePicker;
    private readonly IFirmwarePackageReader packageReader;

    /// <summary>Initializes the custom panel.</summary>
    public CustomFirmwareViewModel(
        IFirmwareFilePicker filePicker,
        IFirmwarePackageReader packageReader,
        DetectedDeviceViewModel devices,
        ValidatedPackageViewModel validated,
        ILogger<CustomFirmwareViewModel> logger,
        IUiDispatcher dispatcher,
        IDomainEventHub eventHub) : base(logger, dispatcher, eventHub)
    {
        this.filePicker = filePicker;
        this.packageReader = packageReader;
        Devices = devices;
        Validated = validated;
    }
    /// <summary>Gets the shared devices panel.</summary>
    public DetectedDeviceViewModel Devices
    {
        get;
    }
    /// <summary>Gets the shared validated panel.</summary>
    public ValidatedPackageViewModel Validated
    {
        get;
    }
    /// <summary>Gets the shared diagnostics panel.</summary>
    [ObservableProperty]
    public partial ApjFirmwarePackage? CustomPackage
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial string? CustomFirmwareName
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial string? CustomFirmwareDescription
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial string? CustomFirmwarePlatform
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial string? CustomFirmwareBuild
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial int CustomFirmwareBoardId
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial long CustomFirmwareImageSize
    {
        get;
        set;
    }


    [ObservableProperty]
    public partial bool RequireExactBoardIdMatch
    {
        get;
        set;
    } = true;

    /// <summary>Gets whether parsed custom metadata is available.</summary>
    public bool HasCustomFirmware => CustomPackage is not null;

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
    public event Action<FirmwarePanelRequest>? OperationRequested;
    /// <summary>Gets whether the parent permits installation.</summary>
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(InstallCommand))]
    public partial bool CanInstall
    {
        get; set;
    }

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private Task InstallAsync(CancellationToken cancellationToken)
    {
        return FirmwarePanelRequest.SendAsync(OperationRequested, FirmwarePanelAction.Install, cancellationToken);
    }

    [RelayCommand(CanExecute = nameof(HasDevice))]
    private async Task LoadCustomFirmwareAsync(CancellationToken cancellationToken)
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
            if (!extension.Equals(".apj", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".px4", StringComparison.OrdinalIgnoreCase))
            {
                SetMessages("Only .apj and .px4 application packages are supported here. Use the separate DFU/legacy workflow for *_with_bl.hex.");
                NotificationManager?.Show(StatusMessage ?? "");
                return;
            }

            await using var stream = await file.OpenReadAsync(cancellationToken);
            var package = await packageReader.ReadAsync(stream, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            RequireExactBoardIdMatch = true;
            CustomPackage = package;

            CustomFirmwareName = file.FileName;
            CustomFirmwareDescription = package.Description ?? "Custom ArduPilot firmware";
            CustomFirmwarePlatform = package.Summary ?? "Platform declared by board ID";
            CustomFirmwareBuild = package.Version ?? package.GitIdentity ?? "Unknown build";
            CustomFirmwareBoardId = package.BoardId;
            CustomFirmwareImageSize = package.Image.Length;

            SetMessages("Local firmware parsed and validated. Verify its board ID, then install it using the custom firmware panel.");
            NotificationManager?.Show(StatusMessage ?? "");

        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Custom firmware selection failed.");
            CustomPackage = null;
            SetMessages(exception);
            NotificationManager?.Show(ErrorMessage ?? "");
        }
    }

    [RelayCommand]
    private void ClearCustomFirmware()
    {
        CustomPackage = null;
        CustomFirmwareName = null;
        CustomFirmwareDescription = null;
        CustomFirmwarePlatform = null;
        CustomFirmwareBuild = null;
        CustomFirmwareBoardId = 0;
        CustomFirmwareImageSize = 0;
        RequireExactBoardIdMatch = true;

        SetMessages("Local firmware selection cleared.");

    }
    /// <summary>Notifies the active parent about panel changes.</summary>
    public event Action<ApjFirmwarePackage?>? PackageChanged;

    /// <summary>Gets whether a serial controller is selected.</summary>
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(LoadCustomFirmwareCommand))]
    public partial bool HasDevice
    {
        get; set;
    }
    /// <summary>Gets whether the selected device uses the separate DFU path.</summary>
    [ObservableProperty]
    public partial bool HasDfuBootLoader
    {
        get; set;
    }
    partial void OnCustomPackageChanged(ApjFirmwarePackage? value)
    {
        if (value is null)
        {
            RequireExactBoardIdMatch = true;
        }
        OnPropertyChanged(nameof(HasCustomFirmware));
        PackageChanged?.Invoke(value);
    }

}
