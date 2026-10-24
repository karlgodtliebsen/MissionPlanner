using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Views.Navigation;
using MissionPlanner.Core.Setup.OptionalHardware;
using MissionPlanner.Firmware.Devices;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>One named register with independently staged local and remote values.</summary>
public sealed partial class SikSettingViewModel : ObservableObject
{
    /// <summary>Creates a paired register from the reported values.</summary>
    public SikSettingViewModel(string register, string name, string? local, string? remote, string? remoteName = null)
    {
        Register = register;
        NamesMatch = remoteName is null || remoteName == name;
        Name = NamesMatch ? name : $"Local: {name}; remote: {remoteName}";
        OriginalLocal = Local = local ?? string.Empty;
        OriginalRemote = Remote = remote ?? string.Empty;
        HasLocal = local is not null && register != "S0";
        HasRemote = remote is not null && register != "S0";
    }
    /// <summary>Gets whether local and remote firmware give the register the same meaning.</summary>
    public bool NamesMatch { get; }
    /// <summary>Gets the firmware register code.</summary>
    public string Register { get; }
    /// <summary>Gets the reported setting name.</summary>
    public string Name { get; }
    /// <summary>Gets whether the local register is writable.</summary>
    public bool HasLocal { get; }
    /// <summary>Gets whether the remote register is writable.</summary>
    public bool HasRemote { get; }
    /// <summary>Gets the original local value.</summary>
    public string OriginalLocal { get; }
    /// <summary>Gets the original remote value.</summary>
    public string OriginalRemote { get; }
    /// <summary>Gets or sets the proposed local value.</summary>
    [ObservableProperty] public partial string Local { get; set; }
    /// <summary>Gets or sets the proposed remote value.</summary>
    [ObservableProperty] public partial string Remote { get; set; }
}

/// <summary>Reviews paired radio settings and saves only explicit changes.</summary>
public sealed partial class SikRadioViewModel(IFirmwareSerialDeviceCatalog devices, ISikRadioConfigurator configurator,
    ILogger<SikRadioViewModel> logger, INavigationService navigation, IUserConfirmationService confirmation)
    : ExternalSerialToolViewModel(devices, logger, navigation)
{
    private string? loadedPort;
    private int loadedBaud;
    private SikRadioSnapshot? loadedSnapshot;
    /// <summary>Gets the detected radio identity.</summary>
    [ObservableProperty] public partial string Identity { get; private set; } = string.Empty;
    /// <summary>Gets or sets the direct serial baud rate.</summary>
    [ObservableProperty] public partial int BaudRate { get; set; } = 57600;
    /// <summary>Gets paired local/remote editors.</summary>
    public ObservableCollection<SikSettingViewModel> Settings { get; } = [];
    /// <summary>Gets or sets the explicit target of Restore defaults.</summary>
    [ObservableProperty] public partial bool RestoreRemote { get; set; }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (SelectedPort is not { } port || IsBusy)
        {
            return;
        }
        var baud = BaudRate;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, Token);
        SetBusy();
        try
        {
            loadedPort = null; loadedSnapshot = null; Settings.Clear();
            var snapshot = await configurator.ReadAsync(port, baud, linked.Token);
            Identity = snapshot.Identity;
            Settings.Clear();
            foreach (var key in snapshot.LocalSettings.Keys.Concat(snapshot.RemoteSettings.Keys).Distinct().OrderBy(key => int.Parse(key[1..])))
            {
                Settings.Add(new(key, snapshot.Names.GetValueOrDefault(key, key), snapshot.LocalSettings.GetValueOrDefault(key), snapshot.RemoteSettings.GetValueOrDefault(key), snapshot.RemoteNames.GetValueOrDefault(key)));
            }
            loadedPort = port; loadedSnapshot = snapshot;
            loadedBaud = baud;
            Status = snapshot.RemoteSettings.Count == 0 ? "Local radio read; remote unavailable." : "Both radios read. Review changed values before saving.";
        }
        catch (OperationCanceledException) { Status = "Radio read cancelled."; }
        catch (Exception exception) { loadedPort = null; loadedSnapshot = null; Status = exception.Message; }
        finally { ResetBusy(); }
    }

    [RelayCommand]
    private void CopyLocalToRemote()
    {
        if (IsBusy)
        {
            return;
        }
        foreach (var row in Settings.Where(row => row.HasLocal && row.HasRemote && row.NamesMatch))
        {
            row.Remote = row.Local;
        }
        Status = "Local settings copied into the remote draft. Review before Apply; nothing has been transmitted.";
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task ApplyAsync(CancellationToken cancellationToken)
    {
        if (IsBusy || loadedPort is null || loadedSnapshot is null || SelectedPort != loadedPort || BaudRate != loadedBaud)
        {
            Status = "Read the selected radio and baud rate before applying.";
            return;
        }
        var local = Settings.Where(row => row.HasLocal && row.Local != row.OriginalLocal).ToDictionary(row => row.Register, row => row.Local.Trim());
        var remote = Settings.Where(row => row.HasRemote && row.Remote != row.OriginalRemote).ToDictionary(row => row.Register, row => row.Remote.Trim());
        if (local.Count + remote.Count == 0)
        {
            Status = "No radio settings changed.";
            return;
        }
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, Token);
        SetBusy();
        try
        {
            if (!await confirmation.ConfirmAsync("Save paired radio settings",
                $"{Identity} on {loadedPort} at {loadedBaud} baud. Write {local.Count} local and {remote.Count} remote changes, verify readback, save and request reboot? Link, frequency or baud changes may disconnect the radios. Ensure the chosen radio settings are permitted in your location.", "Save and reboot", linked.Token))
            {
                return;
            }
            linked.Token.ThrowIfCancellationRequested();
            await configurator.ApplyReviewedAsync(loadedPort, loadedBaud, loadedSnapshot, local, remote, linked.Token);
            loadedPort = null;
            Status = "Readback and save acknowledged; reboot requested. Reconnect using the new settings and read both radios again.";
        }
        catch (OperationCanceledException) { loadedPort = null; loadedSnapshot = null; Status = "Cancelled. Partial settings may remain; read both radios before retrying."; }
        catch (Exception exception) { loadedPort = null; loadedSnapshot = null; Status = exception.Message; }
        finally { ResetBusy(); }
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task RestoreDefaultsAsync(CancellationToken cancellationToken)
    {
        if (IsBusy || SelectedPort is not { } port)
        {
            return;
        }
        var remote = RestoreRemote;
        var baud = BaudRate;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, Token);
        SetBusy();
        try
        {
            if (!await confirmation.ConfirmAsync("Restore radio defaults", $"Restore {(remote ? "remote" : "local")} radio defaults, save and request reboot? The radio link and baud rate may change. Export or record the existing settings first.", "Restore defaults", linked.Token))
            {
                return;
            }
            linked.Token.ThrowIfCancellationRequested();
            await configurator.RestoreDefaultsAsync(port, baud, remote, linked.Token);
            loadedPort = null;
            Status = "Defaults and save acknowledged; reboot requested. Reconnect and read settings.";
        }
        catch (OperationCanceledException) { Status = "Cancelled; radio configuration may have changed."; }
        catch (Exception exception) { loadedPort = null; loadedSnapshot = null; Status = exception.Message; }
        finally { ResetBusy(); }
    }
    /// <inheritdoc />
    protected override async Task StopOperationsAsync()
    {
        await Task.WhenAll(new[] { ConnectCommand.ExecutionTask, ApplyCommand.ExecutionTask, RestoreDefaultsCommand.ExecutionTask }.OfType<Task>());
        loadedPort = null;
        loadedSnapshot = null;
    }

}
