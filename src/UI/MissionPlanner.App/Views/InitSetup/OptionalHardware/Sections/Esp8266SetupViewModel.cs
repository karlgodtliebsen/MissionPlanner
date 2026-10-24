using AsyncAwaitBestPractices;
using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.Core.Setup.OptionalHardware;
using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>An explicitly reported bridge numeric or IPv4 setting.</summary>
public sealed partial class EspBridgeField(string name, string value, bool address) : ObservableObject
{
    /// <summary>Gets the firmware parameter name.</summary>
    public string Name { get; } = name;
    /// <summary>Gets whether the value is an IPv4 address.</summary>
    public bool IsAddress { get; } = address;
    /// <summary>Gets or sets the locally staged value.</summary>
    [ObservableProperty] public partial string Value { get; set; } = value;
}

/// <summary>Reads, stages, verifies and persists the selected vehicle's ESP8266 bridge settings.</summary>
public sealed partial class Esp8266SetupViewModel(ILogger<Esp8266SetupViewModel> logger,
    ComponentParameterService service, IActiveVehicleContext active, IUserConfirmationService confirmation)
    : OptionalHardwareBaseViewModel(logger)
{
    private IReadOnlyDictionary<string, float> loaded = new Dictionary<string, float>();
    private ComponentTarget? loadedTarget;
    private CancellationTokenSource lifetime = new();
    private bool disposed;
    /// <summary>The MAVLink UDP bridge component identifier.</summary>
    public const byte UdpBridgeComponentId = 240;
    /// <summary>Gets or sets the access-point name.</summary>
    [ObservableProperty] public partial string Ssid { get; set; } = string.Empty;
    /// <summary>Gets or sets the access-point password; never included in status text.</summary>
    [ObservableProperty] public partial string Password { get; set; } = string.Empty;
    /// <summary>Gets or sets the station network name.</summary>
    [ObservableProperty] public partial string StationSsid { get; set; } = string.Empty;
    /// <summary>Gets or sets the station password.</summary>
    [ObservableProperty] public partial string StationPassword { get; set; } = string.Empty;
    /// <summary>Gets whether a complete bridge parameter list was read.</summary>
    [ObservableProperty] public partial bool ComponentDetected { get; private set; }
    /// <summary>Gets the component being configured.</summary>
    public string Target => $"MAVLink UDP bridge component {UdpBridgeComponentId}";
    /// <summary>Gets numeric and address settings actually reported by the bridge.</summary>
    public ObservableCollection<EspBridgeField> Fields { get; } = [];

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (IsBusy) return;
        IsBusy = true;
        ComponentDetected = false;
        loadedTarget = null;
        loaded = new Dictionary<string, float>();
        Fields.Clear();
        Password = StationPassword = string.Empty;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        try
        {
            var id = active.VehicleId ?? throw new InvalidOperationException("Select a connected vehicle first.");
            var target = new ComponentTarget(id.SystemId, UdpBridgeComponentId);
            loaded = await service.ReadAsync(target, linked.Token);
            loadedTarget = target;
            Ssid = ReadText("WIFI_SSID");
            Password = ReadText("WIFI_PASSWORD");
            StationSsid = ReadText("WIFI_SSIDSTA");
            StationPassword = ReadText("WIFI_PWDSTA");
            Fields.Clear();
            foreach (var name in EspBridgeSettings.IntegerRanges.Keys.Where(loaded.ContainsKey))
                Fields.Add(new(name, EspBridgeSettings.DecodeInteger(loaded[name]).ToString(CultureInfo.InvariantCulture), false));
            foreach (var name in new[] { "WIFI_IPSTA", "WIFI_GATEWAYSTA", "WIFI_SUBNET_STA" }.Where(loaded.ContainsKey))
                Fields.Add(new(name, EspBridgeSettings.DecodeAddress(loaded[name]), true));
            ComponentDetected = true;
            SetMessages("Bridge settings loaded. Changes are staged locally; Apply verifies and saves, Reboot is separate.", null);
        }
        catch (OperationCanceledException) { SetMessages("Bridge read cancelled or timed out.", null); }
        catch (Exception ex) { SetMessages(ex); }
        finally { IsBusy = false; }
    }

    private string ReadText(string prefix) => Enumerable.Range(1, 4).All(i => loaded.ContainsKey(prefix + i))
        ? PackedParameterStringCodec.Decode(Enumerable.Range(1, 4).Select(i => loaded[prefix + i])) : string.Empty;

    private Dictionary<string, float> Propose()
    {
        if (!ComponentDetected || loadedTarget is null) throw new InvalidOperationException("Read the bridge first.");
        var proposed = new Dictionary<string, float>();
        foreach (var (prefix, text, password) in new[]
        {
            ("WIFI_SSID", Ssid, false), ("WIFI_PASSWORD", Password, true),
            ("WIFI_SSIDSTA", StationSsid, false), ("WIFI_PWDSTA", StationPassword, true),
        })
        {
            // Unchanged legacy 16-byte values are preserved; new strings reserve a terminator.
            if (text == ReadText(prefix)) continue;
            var packed = EspBridgeSettings.EncodeText(text, password);
            for (var i = 0; i < 4; i++)
            {
                var name = prefix + (i + 1);
                if (!loaded.ContainsKey(name)) throw new InvalidOperationException("This bridge does not report the requested network field.");
                proposed[name] = packed[i];
            }
        }
        foreach (var field in Fields)
        {
            var raw = field.IsAddress ? EspBridgeSettings.EncodeAddress(field.Value)
                : EspBridgeSettings.EncodeInteger(field.Name, uint.Parse(field.Value, CultureInfo.InvariantCulture));
            proposed[field.Name] = raw;
        }
        return proposed.Where(pair => BitConverter.SingleToInt32Bits(pair.Value) != BitConverter.SingleToInt32Bits(loaded[pair.Key]))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    [RelayCommand]
    private void Validate()
    {
        try { SetMessages($"{Propose().Count} parameter changes are ready for review.", null); }
        catch (Exception ex) { SetMessages(ex); }
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task ApplyAsync(CancellationToken cancellationToken)
    {
        if (IsBusy) return;
        IsBusy = true;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        try
        {
            var changes = Propose();
            var target = loadedTarget!;
            if (changes.Count == 0) { SetMessages("No changed settings.", null); return; }
            if (!await confirmation.ConfirmAsync("Apply ESP8266 settings", $"Write and persist {changes.Count} parameters on system {target.SystemId}, component 240? Network credentials are hidden. Keep a recovery connection available; new link settings take effect on reboot.", "Apply and save", linked.Token)) return;
            await service.ApplyAsync(target, changes, true, linked.Token);
            var updated = loaded.ToDictionary(pair => pair.Key, pair => pair.Value);
            foreach (var pair in changes) updated[pair.Key] = pair.Value;
            loaded = updated;
            SetMessages("Changes verified and save acknowledged. Use Reboot when ready to reconnect with the new settings.", null);
        }
        catch (OperationCanceledException) { SetMessages("Bridge operation cancelled or timed out. Some changes may have applied; read again.", null); ComponentDetected = false; }
        catch (Exception ex) { ComponentDetected = false; SetMessages(ex); }
        finally { IsBusy = false; }
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task RebootAsync(CancellationToken cancellationToken) => MaintainAsync(false, cancellationToken);

    [RelayCommand(IncludeCancelCommand = true)]
    private Task RestoreDefaultsAsync(CancellationToken cancellationToken) => MaintainAsync(true, cancellationToken);

    private async Task MaintainAsync(bool defaults, CancellationToken cancellationToken)
    {
        if (IsBusy || !ComponentDetected || loadedTarget is null) return;
        IsBusy = true;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        try
        {
            var target = loadedTarget;
            if (!await confirmation.ConfirmAsync(defaults ? "Restore bridge defaults" : "Reboot bridge",
                defaults ? "Restore and save bridge defaults? Record the current network settings first. Reboot remains a separate action."
                : "Reboot only the ESP8266 bridge? This may disconnect telemetry; reconnect with the saved network settings.", "Continue", linked.Token)) return;
            await service.MaintainAsync(target, defaults, linked.Token);
            ComponentDetected = false;
            SetMessages(defaults ? "Defaults and save acknowledged. Read again before rebooting." : "Reboot acknowledged. Reconnect and read to verify operation.", null);
        }
        catch (OperationCanceledException) { ComponentDetected = false; SetMessages("Operation cancelled or timed out; device state is unconfirmed.", null); }
        catch (Exception ex) { ComponentDetected = false; SetMessages(ex); }
        finally { IsBusy = false; }
    }

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (lifetime.IsCancellationRequested) { lifetime.Dispose(); lifetime = new(); }
        return base.ActivateAsync();
    }

    /// <inheritdoc />
    public override async Task DeactivateAsync()
    {
        lifetime.Cancel();
        await Task.WhenAll(new[] { RefreshCommand.ExecutionTask, ApplyCommand.ExecutionTask, RebootCommand.ExecutionTask, RestoreDefaultsCommand.ExecutionTask }.OfType<Task>());
        ComponentDetected = false;
        loadedTarget = null;
        loaded = new Dictionary<string, float>();
        Password = StationPassword = string.Empty;
        await base.DeactivateAsync();
    }
    /// <inheritdoc />
    public override void Dispose()
    {
        if (disposed) return;
        disposed = true;
        DeactivateAsync().SafeFireAndForget(exception => Logger.LogWarning(exception, "Page operation cleanup failed."));
        base.Dispose();
    }

}
