using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Setup.OptionalHardware;
using MissionPlanner.Firmware.Devices;

namespace MissionPlanner.AvaloniaUI.App.Views.InitSetup.OptionalHardware.Sections;

public sealed partial class SikRadioViewModel(IFirmwareSerialDeviceCatalog devices, ISikRadioConfigurator configurator, ILogger<SikRadioViewModel> logger)
    : ExternalSerialToolViewModel(devices, logger)
{
    [ObservableProperty] public partial string Identity { get; private set; } = string.Empty;
    [ObservableProperty] public partial string SettingsText { get; set; } = string.Empty;
    [ObservableProperty] public partial int BaudRate { get; set; } = 57600;

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (SelectedPort is null)
        {
            return;
        }

        try
        {
            SetBusy();
            var snapshot = await configurator.ReadAsync(SelectedPort, BaudRate, Token);
            Identity = snapshot.Identity;
            SettingsText = string.Join(Environment.NewLine, snapshot.LocalSettings.Select(pair => $"{pair.Key}={pair.Value}"));
            Status = snapshot.RemoteSettings.Count == 0 ? "Local radio detected; remote settings are unavailable." : "Local and remote radio settings loaded.";
        }
        catch (Exception exception) when (exception is not OperationCanceledException) { Status = exception.Message; }
        finally { ResetBusy(); }
    }

    [RelayCommand]
    private async Task ApplyAsync()
    {
        if (SelectedPort is null)
        {
            return;
        }

        var settings = SettingsText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim(), StringComparer.Ordinal);
        await configurator.ApplyAsync(SelectedPort, BaudRate, settings, Token);
        Status = "Settings saved; the radio was rebooted.";
    }
}

