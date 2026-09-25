using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Setup.OptionalHardware;

using MissionPlanner.App.Views.Navigation;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

public sealed partial class CubeIdUpdateViewModel(ILogger<CubeIdUpdateViewModel> logger, INavigationService navigation) : OptionalHardwareBaseViewModel(logger)
{
    /// <summary>Refreshes the current page state without starting a hardware operation.</summary>
    [RelayCommand]
    private Task RefreshAsync()
    {
        if (string.IsNullOrWhiteSpace(FirmwarePath))
        {
            FirmwareSummary = "Select a local .bin image. Updating remains disabled until a CubeID component is detected.";
            return Task.CompletedTask;
        }
        return InspectAsync();
    }

    /// <summary>Opens the shared Full Parameters workspace when no operation is running.</summary>
    [RelayCommand]
    private async Task OpenFullParametersAsync()
    {
        if (IsBusy)
        {
            return;
        }
        await navigation.NavigateAsync(MissionPlannerRoutes.ConfigFullParameters);
    }

    [ObservableProperty] public partial string FirmwarePath { get; set; } = string.Empty;
    [ObservableProperty] public partial string FirmwareSummary { get; set; } = "Select a local .bin image. Updating remains disabled until a CubeID component is detected.";
    [ObservableProperty]
    public partial bool TargetDetected
    {
        get; set;
    }
    [RelayCommand]
    private async Task InspectAsync()
    {
        try
        {
            var data = await File.ReadAllBytesAsync(FirmwarePath);
            if (!FirmwarePath.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("CubeID firmware must be a .bin file.");
            }

            FirmwareSummary = $"{Path.GetFileName(FirmwarePath)} — {data.Length:N0} bytes — CRC32 {CubeFirmwareCodec.Crc32(data):X8} — {CubeFirmwareCodec.Chunk(data).Count} chunks";
        }
        catch (Exception ex) { SetMessages(ex); }
    }
}

