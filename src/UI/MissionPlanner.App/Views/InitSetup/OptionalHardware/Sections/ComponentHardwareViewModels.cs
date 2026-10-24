using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.Core.Setup.OptionalHardware;
using MissionPlanner.Core.Setup.OptionalHardware.DroneCan;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>A typed node parameter with local text for review before writing.</summary>
public sealed partial class DroneCanParameterRow(DroneCanParameter parameter) : ObservableObject
{
    /// <summary>Gets the immutable readback.</summary>
    public DroneCanParameter Parameter { get; } = parameter;
    /// <summary>Gets the parameter name.</summary>
    public string Name => Parameter.Name;
    /// <summary>Gets the wire type and reported bounds.</summary>
    public string Limits => $"{Parameter.Value.GetType().Name}; min {Parameter.Minimum ?? "—"}, max {Parameter.Maximum ?? "—"}, default {Parameter.DefaultValue ?? "—"}";
    /// <summary>Gets or sets the proposed value text.</summary>
    [ObservableProperty] public partial string Text { get; set; } = DroneCanManagementCodec.Format(parameter.Value);
}

/// <summary>Reviews DroneCAN nodes, verified changes, firmware transfer and isolated-bus allocation.</summary>
public sealed partial class DroneCanUavCanViewModel(IDroneCanService service, ILogger<DroneCanUavCanViewModel> logger,
    IUserConfirmationService confirmation, IFileOpenService files, IFileSaveService saves) : OptionalHardwareBaseViewModel(logger)
{
    private CancellationTokenSource lifetime = new();
    private bool disposed;
    /// <summary>Gets supported routes.</summary>
    public IReadOnlyList<DroneCanTransportKind> TransportKinds { get; } = Enum.GetValues<DroneCanTransportKind>();
    /// <summary>Gets supported direct-adapter CAN rates.</summary>
    public IReadOnlyList<int> CanRates => SlcanFrameLink.BitRates;
    /// <summary>Gets observed nodes.</summary>
    public ObservableCollection<DroneCanNode> Nodes { get; } = [];
    /// <summary>Gets the fully read inventory.</summary>
    public ObservableCollection<DroneCanParameterRow> Parameters { get; } = [];
    /// <summary>Gets or sets the route.</summary>
    [ObservableProperty] public partial DroneCanTransportKind TransportKind { get; set; }
    /// <summary>Gets or sets the explicit serial port.</summary>
    [ObservableProperty] public partial string Port { get; set; } = "";
    /// <summary>Gets or sets adapter baud.</summary>
    [ObservableProperty] public partial int Baud { get; set; } = 115200;
    /// <summary>Gets or sets CAN bit rate.</summary>
    [ObservableProperty] public partial int CanBitRate { get; set; } = 1_000_000;
    /// <summary>Gets or sets the one-based autopilot bus.</summary>
    [ObservableProperty] public partial int Bus { get; set; } = 1;
    /// <summary>Gets or sets maintenance node ID 126 or 127.</summary>
    [ObservableProperty] public partial int LocalNodeId { get; set; } = 126;
    /// <summary>Gets or sets the distinct persistent isolated-network name.</summary>
    [ObservableProperty] public partial string AllocationNetwork { get; set; } = "";
    /// <summary>Gets or sets the reviewed node.</summary>
    [ObservableProperty] public partial DroneCanNode? SelectedNode { get; set; }
    partial void OnSelectedNodeChanged(DroneCanNode? value) { Parameters.Clear(); SelectedParameter = null; }
    /// <summary>Gets or sets the parameter selected for a single write.</summary>
    [ObservableProperty] public partial DroneCanParameterRow? SelectedParameter { get; set; }
    /// <summary>Gets connection state.</summary>
    [ObservableProperty] public partial bool IsConnected { get; private set; }
    /// <summary>Gets allocator state.</summary>
    [ObservableProperty] public partial bool AllocatorEnabled { get; private set; }
    /// <summary>Gets the bounded raw-frame snapshot.</summary>
    [ObservableProperty] public partial string InspectorText { get; private set; } = "Refresh to display the latest 1,000 raw classic-CAN frames.";
    /// <summary>Gets firmware delivery progress.</summary>
    [ObservableProperty] public partial double TransferProgress { get; private set; }

    /// <summary>Gets or sets the localhost listener port.</summary>
    [ObservableProperty] public partial int BridgePort { get; set; } = 2001;
    /// <summary>Gets or sets GPS serial mode instead of RTCM forwarding.</summary>
    [ObservableProperty] public partial bool BridgeGpsSerial { get; set; }
    /// <summary>Gets or sets explicitly enabled incoming TCP writes.</summary>
    [ObservableProperty] public partial bool BridgeAllowWrites { get; set; }
    /// <summary>Gets or sets UART -1 (firmware default) or 0�15.</summary>
    [ObservableProperty] public partial int BridgeSerialId { get; set; } = -1;
    /// <summary>Gets or sets explicit GPS UART locking.</summary>
    [ObservableProperty] public partial bool BridgeLockPort { get; set; }
    /// <summary>Gets or sets the UART baud, with zero preserving its current value.</summary>
    [ObservableProperty] public partial int BridgeBaud { get; set; }
    [RelayCommand(IncludeCancelCommand = true)]
    private Task PassthroughAsync(CancellationToken token) => RunAsync(async ct =>
    {
        var node = RequiredNode();
        var options = new DroneCanPassthroughOptions { Port = BridgePort, GpsSerial = BridgeGpsSerial, AllowWrites = BridgeAllowWrites, SerialId = BridgeSerialId, LockPort = BridgeLockPort, Baud = BridgeBaud };
        options.Validate();
        if (!await confirmation.ConfirmAsync("Start localhost CAN bridge", $"{NodeLabel(node)}\nListen on 127.0.0.1:{options.Port}. Mode: {(options.GpsSerial ? "GPS serial tunnel" : "RTCM3 / moving-baseline stream")}. Client writes: {options.AllowWrites}.\n{(options.GpsSerial ? $"UART {options.SerialId}, lock {options.LockPort}, baud {options.Baud} (0 = unchanged). Locking interrupts the normal device driver. Unlock is requested on stop; previous baud cannot be restored automatically. Forwarded client commands can alter the device." : "Incoming CRC-valid corrections, when enabled, are broadcast to all CAN receivers; output is restricted to the selected node.")}\nOnly one local client is accepted; disconnect/cancel stops the bridge.", "Start bridge", ct)) return;
        await service.RunPassthroughAsync(node.NodeId, options, true, new Progress<string>(value => SetMessages(value)), ct);
        SetMessages("Local client disconnected; bridge stopped.");
    }, token);

    private async Task RunAsync(Func<CancellationToken, Task> action, CancellationToken token)
    {
        if (IsBusy || disposed) return;
        IsBusy = true;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
        try { SetMessages(""); await action(linked.Token); }
        catch (OperationCanceledException) { SetMessages("Cancelled or timed out. A sent command may have taken effect; refresh before continuing."); }
        catch (Exception ex) { SetMessages(ex); }
        finally { IsBusy = false; IsConnected = service.IsConnected; AllocatorEnabled = service.AllocatorEnabled; RefreshInspector(); }
    }
    [RelayCommand(IncludeCancelCommand = true)]
    private Task ConnectAsync(CancellationToken token) => RunAsync(async ct =>
    {
        var options = new DroneCanConnectionOptions { Port = Port, Baud = Baud, CanBitRate = CanBitRate, Bus = checked((byte)Bus), LocalNodeId = checked((byte)LocalNodeId), AllocationNetwork = AllocationNetwork.Trim() };
        options.Validate(TransportKind);
        if (TransportKind == DroneCanTransportKind.DirectSlcan && !await confirmation.ConfirmAsync("Open direct CAN adapter",
            $"Open {Port} at {Baud} baud, CAN {CanBitRate} bit/s, maintenance ID {LocalNodeId}? Use a bench bus with actuators unable to move. Connect configures the adapter and publishes a tool heartbeat.", "Connect", ct)) return;
        Nodes.Clear(); Parameters.Clear(); SelectedNode = null;
        await service.ConnectAsync(TransportKind, options, ct); await LoadNodesAsync(ct);
    }, token);
    [RelayCommand(IncludeCancelCommand = true)]
    private Task RefreshAsync(CancellationToken token) => RunAsync(LoadNodesAsync, token);
    private async Task LoadNodesAsync(CancellationToken token)
    {
        var nodes = await service.DiscoverAsync(token); var selected = SelectedNode?.NodeId;
        Nodes.Clear(); foreach (var node in nodes) Nodes.Add(node);
        SelectedNode = Nodes.FirstOrDefault(node => node.NodeId == selected);
        SetMessages($"{Nodes.Count} node(s). Offline/unidentified nodes cannot be changed. This connection supports classic DroneCAN v0.");
    }
    [RelayCommand(IncludeCancelCommand = true)]
    private Task ReadParametersAsync(CancellationToken token) => RunAsync(async ct =>
    {
        var node = RequiredNode(); var values = await service.ReadParametersAsync(node.NodeId, ct);
        if (SelectedNode?.NodeId != node.NodeId) return;
        Parameters.Clear(); foreach (var value in values) Parameters.Add(new(value));
        SetMessages($"Read {values.Count} parameters from {node.Name} ({node.NodeId}). Some nodes persist writes immediately; Save parameters explicitly requests storage where supported.");
    }, token);
    [RelayCommand(IncludeCancelCommand = true)]
    private Task WriteParameterAsync(CancellationToken token) => RunAsync(async ct =>
    {
        var node = RequiredNode(); var row = SelectedParameter ?? throw new InvalidOperationException("Select a parameter first.");
        if (row.Parameter.NodeId != node.NodeId) throw new InvalidOperationException("Read this node's parameters first.");
        var value = DroneCanManagementCodec.ParseValue(row.Parameter, row.Text);
        if (!await confirmation.ConfirmAsync("Change DroneCAN parameter", $"{NodeLabel(node)}\n{row.Name}: {DroneCanManagementCodec.Format(row.Parameter.Value)} → {DroneCanManagementCodec.Format(value)}\nWrite and verify this value? Some nodes persist writes immediately.", "Write", ct)) return;
        await service.WriteParameterAsync(node.NodeId, row.Name, value, ct);
        if (Parameters.Contains(row)) { var updated = new DroneCanParameterRow(row.Parameter with { Value = value }); Parameters[Parameters.IndexOf(row)] = updated; SelectedParameter = updated; }
        SetMessages("Write/readback matched. Use Save parameters where supported; some nodes persist writes immediately.");
    }, token);
    [RelayCommand(IncludeCancelCommand = true)]
    private Task SaveParametersAsync(CancellationToken token) => MaintainAsync(false, token);
    [RelayCommand(IncludeCancelCommand = true)]
    private Task RestartAsync(CancellationToken token) => MaintainAsync(true, token);
    private Task MaintainAsync(bool restart, CancellationToken token) => RunAsync(async ct =>
    {
        var node = RequiredNode();
        if (!await confirmation.ConfirmAsync(restart ? "Restart CAN node" : "Save CAN parameters", $"{NodeLabel(node)}\n{(restart ? "Restart this node? Sensor/output data will be interrupted." : "Persist all current node parameters, including changes from other tools?")}", restart ? "Restart" : "Save", ct)) return;
        if (restart) { await service.RestartNodeAsync(node.NodeId, ct); Parameters.Clear(); }
        else await service.SaveParametersAsync(node.NodeId, ct);
        SetMessages(restart ? "Restart request accepted. Refresh after the node returns." : "Node acknowledged persistent save.");
    }, token);
    [RelayCommand(IncludeCancelCommand = true)]
    private Task FirmwareAsync(CancellationToken token) => RunAsync(async ct =>
    {
        var node = RequiredNode();
        using var file = await files.OpenAsync("Select matching DroneCAN firmware", ["*.bin"], ct); if (file is null) return;
        using var memory = new MemoryStream(); var buffer = new byte[8192]; int count;
        while ((count = await file.Content.ReadAsync(buffer, ct)) > 0)
        {
            if (memory.Length + count > 4 * 1024 * 1024) throw new InvalidDataException("Firmware exceeds 4 MiB.");
            memory.Write(buffer, 0, count);
        }
        var data = memory.ToArray(); if (data.Length == 0) throw new InvalidDataException("Empty firmware file.");
        if (!await confirmation.ConfirmAsync("Update DroneCAN firmware", $"{NodeLabel(node)}\nImage: {file.FileName}, {data.Length:N0} bytes, CRC32 {CubeFirmwareCodec.Crc32(data):X8}\nConfirm this image matches the exact board and bootloader. A wrong image can require device recovery. Keep stable power; cancellation stops serving bytes and does not restore firmware.", "Transfer", ct)) return;
        TransferProgress = 0;
        SetMessages(await service.UpdateFirmwareAsync(node.NodeId, file.FileName, data, true, new Progress<double>(value => TransferProgress = value), ct));
    }, token);
    [RelayCommand(IncludeCancelCommand = true)]
    private Task ToggleAllocatorAsync(CancellationToken token) => RunAsync(async ct =>
    {
        var enable = !service.AllocatorEnabled;
        if (enable && !await confirmation.ConfirmAsync("Enable isolated-bus allocator", "Confirm an isolated direct SLCAN bench network with no autopilot or other allocator. The network name entered before connecting identifies a persistent reservation table; reuse the name for this bus. IDs are assigned to requesting hardware until disabled or disconnected.", "Enable allocator", ct)) return;
        await service.SetAllocatorAsync(enable, enable, ct); SetMessages(enable ? "Allocator enabled. Another allocator's traffic disables it." : "Allocator disabled; reservations retained.");
    }, token);
    [RelayCommand]
    private void RefreshInspector()
    {
        InspectorText = string.Join(Environment.NewLine, service.Trace.Select(item => $"{item.At:HH:mm:ss.fff} {(item.Transmitted ? "TX" : "RX")} {item.Identifier:X8} {item.Data}"));
        AllocatorEnabled = service.AllocatorEnabled;
    }
    [RelayCommand(IncludeCancelCommand = true)]
    private Task ExportAsync(CancellationToken token) => RunAsync(async ct =>
    {
        var csv = "utc,direction,extended_can_id,data_hex\n" + string.Join("\n", service.Trace.Select(item => $"{item.At:O},{(item.Transmitted ? "TX" : "RX")},{item.Identifier:X8},{item.Data}"));
        using var memory = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        if (await saves.SaveNewAsync("dronecan-frames.csv", memory, ct) is not null) SetMessages("Saved raw-frame snapshot. Payloads may contain sensitive configuration.");
    }, token);
    private static string NodeLabel(DroneCanNode node) => $"Node {node.NodeId}: {node.Name}, hardware {node.UniqueId}, software {node.Version}";
    private DroneCanNode RequiredNode() => SelectedNode is { UniqueId.Length: 32 } node ? node : throw new InvalidOperationException("Refresh and select an identified node.");
    [RelayCommand]
    private async Task DisconnectAsync()
    {
        lifetime.Cancel(); await WaitForCommandsAsync();
        try { await service.DisconnectAsync(CancellationToken.None); SetMessages("Disconnected. Previously sent configuration/firmware changes remain on the node."); }
        catch (Exception ex) { SetMessages(ex); }
        finally { IsConnected = false; AllocatorEnabled = false; Parameters.Clear(); Nodes.Clear(); SelectedNode = null; }
        if (!disposed) { lifetime.Dispose(); lifetime = new(); }
    }
    private Task WaitForCommandsAsync() => Task.WhenAll(new[] { ConnectCommand.ExecutionTask, RefreshCommand.ExecutionTask, ReadParametersCommand.ExecutionTask,
        WriteParameterCommand.ExecutionTask, SaveParametersCommand.ExecutionTask, RestartCommand.ExecutionTask, FirmwareCommand.ExecutionTask, ToggleAllocatorCommand.ExecutionTask, ExportCommand.ExecutionTask, PassthroughCommand.ExecutionTask }.OfType<Task>());
    /// <inheritdoc />
    public override async Task DeactivateAsync() { await DisconnectCommand.ExecuteAsync(null); await base.DeactivateAsync(); }
    /// <inheritdoc />
    public override void Dispose()
    {
        if (disposed) return; disposed = true; lifetime.Cancel();
        DisconnectCommand.ExecuteAsync(null).SafeFireAndForget(); base.Dispose();
    }
}
