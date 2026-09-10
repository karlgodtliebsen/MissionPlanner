using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.InitSetup.InstallFirmware.SubViews;
using MissionPlanner.Firmware.Images;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Preparation;
using MissionPlanner.Library.EventHub.Abstractions;
namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>Owns custom panel state and commands.</summary>
public sealed partial class CustomFirmwareViewModel : DialogViewModelBase
{
    private readonly IFirmwareFilePicker filePicker;
    private readonly IFirmwarePreparationService preparationService;

    /// <summary>Initializes the custom panel.</summary>
    public CustomFirmwareViewModel(
        IFirmwareFilePicker filePicker,
        IFirmwarePreparationService preparationService,

        DetectedDeviceViewModel devicesModel,
        ValidatedPackageViewModel validatedModel,

        ILogger<CustomFirmwareViewModel> logger,
        IUiDispatcher dispatcher,
        IDomainEventHub eventHub) : base(logger, dispatcher, eventHub)
    {
        this.filePicker = filePicker;
        this.preparationService = preparationService;
        DevicesModel = devicesModel;
        ValidatedModel = validatedModel;
    }
    /// <summary>Gets the shared devices panel.</summary>
    public DetectedDeviceViewModel DevicesModel
    {
        get;
    }
    /// <summary>Gets the shared validated panel.</summary>
    public ValidatedPackageViewModel ValidatedModel
    {
        get;
    }

    /// <summary>Gets the imported local artifact and provenance, independent of target compatibility.</summary>
    public LocalFirmwarePreparationResult? PreparedLocalFirmware { get; private set; }
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


    /// <summary>
    /// Gets whether parsed custom metadata is available.
    /// </summary>
    [ObservableProperty]
    public partial bool HasCustomFirmware
    {
        get;
        set;
    }

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

    public Task InstallAsync(CancellationToken cancellationToken)
    {
        return FirmwarePanelRequest.SendAsync(OperationRequested, FirmwarePanelAction.Install, cancellationToken);
    }

    public async Task LoadCustomFirmwareAsync(CancellationToken cancellationToken, FirmwareFileSelection? selection = null)
    {
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, viewLifetime?.Token ?? CancellationToken.None);
        cancellationToken = operation.Token;
        try
        {
            var file = selection ?? await filePicker.PickAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (file is null)
            {
                return;
            }

            var extension = Path.GetExtension(file.FileName);
            if (!extension.Equals(".apj", StringComparison.OrdinalIgnoreCase))
            {
                SetMessages("The ArduPilot serial plan requires an .apj application package. STM32 DFU requires *_with_bl.hex.");
                NotificationManager?.Show(StatusMessage ?? "");
                return;
            }

            await using var stream = await file.OpenReadAsync(cancellationToken);
            var imported = await preparationService.ImportAsync(stream, file.FileName, file.LocalPath, cancellationToken);
            var package = imported.Package;
            cancellationToken.ThrowIfCancellationRequested();
            PreparedLocalFirmware = imported;
            CustomPackage = package;

            CustomFirmwareName = file.FileName;
            CustomFirmwareDescription = package.Description ?? "Custom ArduPilot firmware";
            CustomFirmwarePlatform = package.Summary ?? "Platform declared by board ID";
            CustomFirmwareBuild = package.Version ?? package.GitIdentity ?? "Unknown build";
            CustomFirmwareBoardId = package.BoardId;
            CustomFirmwareImageSize = package.Image.Length;
            HasCustomFirmware = CustomPackage is not null;

            SetMessages("Local firmware validated and imported. Target compatibility will be checked against the bootloader identity.");
            NotificationManager?.Show(StatusMessage ?? "");

        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            HasCustomFirmware = false;
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Custom firmware selection failed.");
            CustomPackage = null;
            HasCustomFirmware = false;
            SetMessages(exception);
            NotificationManager?.Show(ErrorMessage ?? "");
        }
    }

    public void Reset()
    {
        HasCustomFirmware = false;
        CustomPackage = null;
        CustomFirmwareName = null;
        CustomFirmwareDescription = null;
        CustomFirmwarePlatform = null;
        CustomFirmwareBuild = null;
        CustomFirmwareBoardId = 0;
        CustomFirmwareImageSize = 0;
    }

    /// <summary>Notifies the active parent about panel changes.</summary>
    public event Action<ApjFirmwarePackage?>? PackageChanged;

    /// <summary>
    /// Gets whether a serial controller is selected.
    /// </summary>
    [ObservableProperty]
    public partial bool HasDevice
    {
        get; set;
    }

    /// <summary>
    /// Gets whether the selected device uses the separate DFU path.
    /// </summary>
    [ObservableProperty]
    public partial bool HasDetectedDfuDevice
    {
        get; set;
    }
    partial void OnCustomPackageChanged(ApjFirmwarePackage? value)
    {
        if (value is null)
        {
            PreparedLocalFirmware = null;
        }
        HasCustomFirmware = false;
        OnPropertyChanged(nameof(HasCustomFirmware));
        PackageChanged?.Invoke(value);
    }

}
