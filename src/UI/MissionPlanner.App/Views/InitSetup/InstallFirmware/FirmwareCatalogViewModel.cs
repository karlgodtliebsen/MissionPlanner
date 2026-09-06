using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Firmware.Catalog;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Library.EventHub.Abstractions;
namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>Owns catalogue panel state and commands.</summary>
public sealed partial class FirmwareCatalogViewModel : ViewModelBase
{
    /// <summary>Initializes the catalogue panel.</summary>
    public FirmwareCatalogViewModel(
        DetectedDeviceViewModel devices,
        ValidatedPackageViewModel validated,
    //    DiagnosticsReportViewModel diagnostics,
        SelectedFirmwareViewModel selected,
        ILogger<FirmwareCatalogViewModel> logger,
        IUiDispatcher dispatcher,
        IDomainEventHub eventHub) : base(logger, dispatcher, eventHub)
    {
        Devices = devices;
        Validated = validated;
        // Diagnostics = diagnostics;
        Selected = selected;
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
    /// <summary>Gets the selected release details.</summary>
    public SelectedFirmwareViewModel Selected
    {
        get;
    }
    private IReadOnlyList<FirmwareManifestEntry> availableEntries = [];
    private IReadOnlyList<SerialDeviceDescriptor> availableDevices = [];
    private FirmwareManifestEntry? selectedFirmwareTarget;
    private bool showingAllOptions;
    private bool isClearing;

    ///
    /// <summary>Gets catalogue choices.
    /// </summary>
    [ObservableProperty]
    public partial ObservableRangeCollection<FirmwareCatalogItemViewModel> FirmwareChoices { get; set; } = [];

    /// <summary>
    /// Gets catalogue choices.
    /// </summary>
    [ObservableProperty]
    public partial ObservableRangeCollection<FirmwareCatalogItemViewModel> FilteredFirmwareChoices { get; set; } = [];

    ///
    /// <summary>
    /// Gets the distinct firmware versions available in the catalogue.
    /// </summary>
    [ObservableProperty]
    public partial ObservableRangeCollection<string> Versions { get; set; } = [];

    /// <summary>
    /// Gets or sets the selected firmware version for filtering the catalogue.
    /// </summary>
    [ObservableProperty]
    public partial string? SelectedVersion
    {
        get;
        set;
    }

    /// <summary>
    ///  Gets the distinct FrameTypes available in the catalogue.
    /// </summary>
    [ObservableProperty]
    public partial ObservableRangeCollection<string> FrameTypes
    {
        get;
        set;
    } = [];

    /// <summary>
    /// Gets or sets the selected FrameType for filtering the catalogue.
    /// </summary>
    [ObservableProperty]
    public partial string? SelectedFrameType
    {
        get;
        set;
    }

    /// <summary>
    ///  Gets the distinct Manufacturer available in the catalogue.
    /// </summary>
    [ObservableProperty]
    public partial ObservableRangeCollection<string> Manufacturers
    {
        get;
        set;
    } = [];

    /// <summary>
    ///  Gets or sets the selected Manufacturer for filtering the catalogue.
    /// </summary>
    [ObservableProperty]
    public partial string? SelectedManufacturer
    {
        get;
        set;
    }

    /// <summary>Gets release channels.</summary>
    public IReadOnlyList<FirmwareReleaseChannel> Channels { get; } = [FirmwareReleaseChannel.Stable, FirmwareReleaseChannel.Beta, FirmwareReleaseChannel.Latest];

    [ObservableProperty]
    public partial FirmwareReleaseChannel SelectedChannel { get; set; } = FirmwareReleaseChannel.Stable;

    [ObservableProperty]
    public partial FirmwareCatalogItemViewModel? SelectedFirmware
    {
        get;
        set;
    }

    public void Reset()
    {
        Clear();
        SelectedFirmware = null;
        selectedFirmwareTarget = null;
        SelectedChannel = FirmwareReleaseChannel.Stable;
    }
    /// <summary>Gets whether a firmware release from the catalogue is selected.</summary>
    public bool HasSelectedFirmware => SelectedFirmware is not null;

    [ObservableProperty]
    public partial bool IsVehicleConnected
    {
        get;
        set;
    }

    [RelayCommand]
    private void Clear()
    {
        if (isClearing)
        {
            return;
        }

        isClearing = true;
        try
        {
            FiltersChanged?.Invoke(true);
            SelectedFrameType = null;
            SelectedVersion = null;
            SelectedManufacturer = null;
        }
        finally
        {
            isClearing = false;
        }

        FilterData(null, null, null);

    }

    partial void OnSelectedVersionChanged(string? value)
    {
        FilterData(SelectedVersion, SelectedFrameType, SelectedManufacturer);
    }

    partial void OnSelectedFrameTypeChanged(string? value)
    {
        FilterData(SelectedVersion, SelectedFrameType, SelectedManufacturer);
    }

    partial void OnSelectedManufacturerChanged(string? value)
    {
        FilterData(SelectedVersion, SelectedFrameType, SelectedManufacturer);
    }

    private void FilterData(string? version, string? vehicleType, string? manufacturer)
    {
        if (isClearing)
        {
            return;
        }

        var choices = FirmwareChoices.ToList();

        if (!string.IsNullOrEmpty(version))
        {
            choices = choices.Where(x => x.FirmwareVersion.ToString() == version).ToList();
            Debug.Print($"Filter Version {version} found {choices.Count} items");
        }

        if (!string.IsNullOrEmpty(vehicleType))
        {
            choices = choices.Where(x => x.VehicleType == vehicleType).ToList();
            Debug.Print($"Filter VehicleType {vehicleType} found {choices.Count} items");
        }

        if (!string.IsNullOrEmpty(manufacturer))
        {
            choices = choices.Where(x => x.Manufacturer == manufacturer).ToList();
            Debug.Print($"Filter Manufacturer {manufacturer} found {choices.Count} items");
        }

        FilteredFirmwareChoices.ReplaceRange(choices);
        SetMessages($"Found {choices.Count} after Applying Filter and Collection Now Holds {FilteredFirmwareChoices.Count}");
    }

    private void ApplyTargetQuery()
    {
        SelectedVersion = null;
        SelectedFrameType = null;
        SelectedManufacturer = null;

        // The grid may transiently clear SelectedFirmware while its collection is rebuilt.
        // Preserve the last deliberate/non-null selection independently of that UI event.
        var previousEntry = selectedFirmwareTarget;
        var recommendations =
            FirmwareTargetSelector.Query(availableEntries, new FirmwareTargetQuery(ReleaseChannel: showingAllOptions ? null : SelectedChannel),
                availableDevices, SelectedFirmware?.BoardId);

        var choices = recommendations.Select(recommendation => new FirmwareCatalogItemViewModel(recommendation))
            .ToArray();

        FirmwareChoices.ReplaceRange(choices);

        var versions = choices
            .Select(x => x.FirmwareVersion)
            .Distinct()
            .OrderByDescending(v => v.SemanticVersion ?? new System.Version(0, 0))
            .ThenByDescending(v => v.Value, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.ToString())
            .ToList();

        Versions.ReplaceRange(versions);

        //FirmwareManifestEntry -> FirmwareBoardTarget Target  -> FirmwareVehicleType VehicleType
        var frameTypes = choices
            .Select(x => x.VehicleType)
            .Distinct()
            .Order()
            .ToList();

        FrameTypes.ReplaceRange(frameTypes);

        var manufacturers = choices
            .Select(x => x.Manufacturer)
            .Distinct()
            .Order()
            .ToList();

        Manufacturers.Clear();
        Manufacturers.AddRange(manufacturers);

        // Keep the initial catalogue population on the same path as subsequent
        // filter changes. FilterData also updates HasFirmwareChoices, which controls
        // whether the Avalonia DataGrid is present in the visual tree.
        FilterData(SelectedVersion, SelectedFrameType, SelectedManufacturer);

        Debug.Print($"InstallFirmware ApplyTargetQuery with FirmwareChoices count: {FirmwareChoices.Count}");

        var retained = previousEntry is null ? null : FirmwareChoices.FirstOrDefault(item => SameEntry(item.Entry, previousEntry));
        var automatic = FirmwareTargetSelector.UnambiguousHighConfidence(recommendations);
        SelectedFirmware = retained ?? (automatic is null ? null : FirmwareChoices.Single(item => ReferenceEquals(item.Entry, automatic.Entry)));
    }

    private static bool SameEntry(FirmwareManifestEntry left, FirmwareManifestEntry right)
    {
        return left.Target.BoardId == right.Target.BoardId &&
               left.Channel == right.Channel &&
               left.Artifact.DownloadUri == right.Artifact.DownloadUri;
    }
    /// <summary>Notifies the active parent about panel changes.</summary>
    public event Action<FirmwareCatalogItemViewModel?>? SelectionChanged;
    /// <summary>Notifies the active parent about panel changes.</summary>
    public event Action<FirmwareReleaseChannel>? ChannelChanged;
    /// <summary>Notifies the active parent about panel changes.</summary>
    public event Action<bool>? FiltersChanged;
    /// <summary>Notifies the active parent about panel changes.</summary>
    public event Action<FirmwarePanelRequest>? OperationRequested;
    [RelayCommand]
    private Task RefreshCatalogAsync(CancellationToken cancellationToken)
    {
        return FirmwarePanelRequest.SendAsync(OperationRequested, FirmwarePanelAction.Refresh, cancellationToken);
    }

    [RelayCommand]
    private Task ShowAllOptionsAsync(CancellationToken cancellationToken)
    {
        return FirmwarePanelRequest.SendAsync(OperationRequested, FirmwarePanelAction.Refresh, cancellationToken, true);
    }

    partial void OnSelectedChannelChanged(FirmwareReleaseChannel value)
    {
        selectedFirmwareTarget = null;
        ChannelChanged?.Invoke(value);
    }
    partial void OnSelectedFirmwareChanged(FirmwareCatalogItemViewModel? value)
    {
        if (value is not null)
        {
            selectedFirmwareTarget = value.Entry;
        }
        OnPropertyChanged(nameof(HasSelectedFirmware));
        SelectionChanged?.Invoke(value);
    }
    [RelayCommand]
    private void ClearCatalogueFirmware()
    {
        Clear();
        SelectedFirmware = null;
        selectedFirmwareTarget = null;
        SelectedChannel = FirmwareReleaseChannel.Stable;
    }
    /// <summary>Rebuilds catalogue recommendations from the latest snapshot.</summary>
    public void SetCatalogue(IReadOnlyList<FirmwareManifestEntry> entries, IReadOnlyList<SerialDeviceDescriptor> devices, bool allOptions)
    {
        availableEntries = entries;
        availableDevices = devices;
        showingAllOptions = allOptions;
        ApplyTargetQuery();
    }
    /// <summary>Clears a deliberate selection when a local file takes precedence.</summary>
    public void ClearSelection()
    {
        selectedFirmwareTarget = null;
        SelectedFirmware = null;
    }

}
