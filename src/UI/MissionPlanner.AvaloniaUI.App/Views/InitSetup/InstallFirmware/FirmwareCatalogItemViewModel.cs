using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner.Firmware.Catalog;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.AvaloniaUI.App.Views.InitSetup.InstallFirmware;

/// <summary>One data-driven firmware choice displayed by the installation page.</summary>
public sealed partial class FirmwareCatalogItemViewModel : ObservableObject
{
    private readonly FirmwareTargetRecommendation recommendation;

    [ObservableProperty] public partial bool IsSelected { get; set; }

    /// <summary>
    /// One data-driven firmware choice displayed by the install page.
    /// </summary>
    public FirmwareCatalogItemViewModel(FirmwareTargetRecommendation recommendation)
    {
        this.recommendation = recommendation;
        Entry = recommendation.Entry;
    }

    /// <summary>
    /// Gets the firmware version.
    /// </summary>
    public FirmwareVersion FirmwareVersion => Entry.Version;

    /// <summary>Gets the normalized release.</summary>
    public FirmwareManifestEntry Entry { get; }

    /// <summary>Gets the target match explanation.</summary>
    public string MatchReason => recommendation.Reason switch
    {
        FirmwareTargetMatchReason.ExactUsbMatch => "Exact USB match",
        FirmwareTargetMatchReason.ExactBootloaderAliasMatch => "Exact bootloader alias match",
        FirmwareTargetMatchReason.PreviouslySelectedTarget => "Previously selected target",
        var _ => "Manual selection"
    };

    /// <summary>Gets whether hardware evidence supports automatic selection.</summary>
    public bool IsHighConfidence => recommendation.Confidence == FirmwareTargetConfidence.High;

    /// <summary>Gets manufacturer or brand metadata when supplied.</summary>
    public string Manufacturer => Entry.RawMetadata.FirstOrDefault(pair => pair.Key.Equals("manufacturer", StringComparison.OrdinalIgnoreCase) || pair.Key.Equals("brand", StringComparison.OrdinalIgnoreCase)).Value ?? "Unknown manufacturer";

    /// <summary>Gets the source revision.</summary>
    public string GitSha => Entry.GitSha ?? "Unknown";

    /// <summary>Gets the artifact URL.</summary>
    public string ArtifactUrl => Entry.Artifact.DownloadUri.AbsoluteUri;

    /// <summary>Gets the artifact format.</summary>
    public FirmwareImageFormat Format => Entry.Artifact.Format;

    /// <summary>Gets known USB identifiers.</summary>
    public string UsbIdentifiers => Entry.Target.UsbIdentifiers.Count == 0 ? "None declared" : string.Join(", ", Entry.Target.UsbIdentifiers);

    /// <summary>Gets known bootloader aliases.</summary>
    public string BootloaderAliases => Entry.Target.BootloaderNames.Count == 0 ? "None declared" : string.Join(", ", Entry.Target.BootloaderNames);

    /// <summary>Gets the vehicle label.</summary>
    public string VehicleType => Entry.Target.VehicleType.ToString();

    /// <summary>Gets the version label.</summary>
    public string Version => Entry.Version.ToString();

    /// <summary>Gets the platform label.</summary>
    public string Platform => Entry.Target.Platform;

    /// <summary>Gets the board identifier.</summary>
    public int BoardId => Entry.Target.BoardId;

    /// <summary>Gets the release channel.</summary>
    public FirmwareReleaseChannel Channel => Entry.Channel;
}

