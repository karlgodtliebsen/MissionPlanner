using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Core.Diagnostics;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.App.Views.Diagnostics;

/// <summary>One session-owned Inspector presentation, reused by the drawer and desktop window.</summary>
public sealed partial class LiveTelemetryInspectorViewModel : ViewModelBase
{
    private readonly IVehicleLiveDiagnostics diagnostics;
    private readonly IActiveVehicleContext activeVehicle;
    private readonly ITextClipboardService clipboard;
    private readonly IInspectorWindowService windows;
    private readonly TimeProvider clock;
    private readonly DispatcherTimer timer;
    private long displayedVersion = -1;
    private bool presentationDisposed;
    private bool contextSelection;
    private bool manualPanelSelection;

    /// <summary>Initializes a coalesced UI boundary over the always-on diagnostic service.</summary>
    public LiveTelemetryInspectorViewModel(IVehicleLiveDiagnostics diagnostics, IActiveVehicleContext activeVehicle, ITextClipboardService clipboard,
        IInspectorWindowService windows, TimeProvider clock, IUiDispatcher dispatcher,
        IDomainEventHub events, ILogger<LiveTelemetryInspectorViewModel> logger) : base(logger, dispatcher, events)
    {
        this.diagnostics = diagnostics;
        this.activeVehicle = activeVehicle;
        this.clipboard = clipboard;
        this.windows = windows;
        this.clock = clock;
        timer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Background, (_, _) => Refresh());
        timer.Start();
    }

    /// <summary>Whether either presentation host is open.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDrawerOpen))]
    public partial bool IsOpen
    {
        get; set;
    }

    /// <summary>Whether desktop detached mode owns the content.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDrawerOpen))]
    public partial bool IsDetached
    {
        get; private set;
    }

    /// <summary>Whether the right-side drawer is visible.</summary>
    public bool IsDrawerOpen => IsOpen && !IsDetached;

    /// <summary>Whether this platform supports a detached window.</summary>
    public bool CanDetach => windows.IsSupported;

    /// <summary>Session-retained drawer width.</summary>
    [ObservableProperty]
    public partial double DrawerWidth { get; set; } = 560;

    /// <summary>Available vehicle identities, including retained disconnected vehicles.</summary>
    public ObservableCollection<VehicleId> Vehicles { get; } = [];

    /// <summary>Selected vehicle, independent of other views' current selection.</summary>
    [ObservableProperty]
    public partial VehicleId? SelectedVehicle
    {
        get; set;
    }

    /// <summary>Available diagnostic panels.</summary>
    public IReadOnlyList<string> Panels { get; } = ["Status", "RC", "Outputs", "Power", "Sensors", "Raw"];

    /// <summary>Session-retained selected panel.</summary>
    [ObservableProperty]
    public partial string SelectedPanel { get; set; } = "Status";

    /// <summary>Prominent vehicle, connection, mode and arming summary.</summary>
    [ObservableProperty]
    public partial string Header { get; private set; } = "No vehicle telemetry received";

    /// <summary>Current panel's bounded evidence.</summary>
    public ObservableCollection<string> Details { get; } = [];

    /// <summary>Bounded, virtualized advanced wire observations.</summary>
    public ObservableCollection<VehicleRawDiagnostic> Raw { get; } = [];

    /// <summary>Raw name or numeric message-ID filter.</summary>
    [ObservableProperty]
    public partial string RawFilter { get; set; } = "";

    /// <summary>Optional source system filter.</summary>
    [ObservableProperty]
    public partial string RawSystem { get; set; } = "";

    /// <summary>Optional source component filter.</summary>
    [ObservableProperty]
    public partial string RawComponent { get; set; } = "";

    /// <summary>Whether the Raw panel follows incoming samples.</summary>
    [ObservableProperty]
    public partial bool FollowRaw { get; set; } = true;

    /// <summary>Whether the Raw panel is selected.</summary>
    public bool ShowRaw => SelectedPanel == "Raw";

    /// <summary>Bounded RC channel bars with configured limits and roles.</summary>
    public ObservableCollection<VehicleDiagnosticChannel> Channels { get; } = [];

    /// <summary>Whether RC channel visualization is selected.</summary>
    public bool ShowRc => SelectedPanel == "RC";

    /// <summary>Recent significant diagnostic events, newest first.</summary>
    public ObservableCollection<VehicleDiagnosticEvent> Events { get; } = [];

    /// <summary>Optional text for the next marker.</summary>
    [ObservableProperty]
    public partial string MarkerText { get; set; } = "";

    /// <summary>Whether presentation is frozen while ingestion continues.</summary>
    [ObservableProperty]
    public partial bool IsFrozen
    {
        get; private set;
    }

    /// <summary>Live/frozen timestamp and collection explanation.</summary>
    [ObservableProperty]
    public partial string LiveLabel { get; private set; } = "LIVE";

    partial void OnSelectedVehicleChanged(VehicleId? value)
    {
        IsFrozen = false;
        displayedVersion = -1;
        Refresh();
    }

    partial void OnSelectedPanelChanged(string value)
    {
        manualPanelSelection |= !contextSelection;
        OnPropertyChanged(nameof(ShowRc));
        OnPropertyChanged(nameof(ShowRaw));
        displayedVersion = -1;
        Refresh();
    }

    /// <summary>Opens or focuses the Inspector with an optional explicit panel choice.</summary>
    public void Open(string? panel = null)
    {
        if (panel is not null && Panels.Contains(panel))
        {
            SelectedPanel = panel;
        }
        IsOpen = true;
        SelectedVehicle ??= activeVehicle.VehicleId;
        if (SelectedVehicle is null && diagnostics.Vehicles.Count > 0)
        {
            SelectedVehicle = diagnostics.Vehicles[0];
        }
        Refresh();
        windows.Focus();
    }

    /// <summary>Applies initial page context only while open and until the user chooses a panel.</summary>
    public void SuggestContext(string? context)
    {
        if (!IsOpen || manualPanelSelection || context is null)
        {
            return;
        }
        var panel = context.Contains("Radio", StringComparison.OrdinalIgnoreCase) ? "RC" :
            context.Contains("Motor", StringComparison.OrdinalIgnoreCase) || context.Contains("Servo", StringComparison.OrdinalIgnoreCase) ? "Outputs" :
            context.Contains("Compass", StringComparison.OrdinalIgnoreCase) || context.Contains("Accelerometer", StringComparison.OrdinalIgnoreCase) ? "Sensors" :
            context.Contains("Battery", StringComparison.OrdinalIgnoreCase) ? "Power" :
            context.Contains("FlightData", StringComparison.OrdinalIgnoreCase) ? "Status" : null;
        if (panel is not null)
        {
            contextSelection = true;
            SelectedPanel = panel;
            contextSelection = false;
        }
    }

    [RelayCommand]
    private void Show()
    {
        Open();
    }

    [RelayCommand]
    private void Why()
    {
        Open("Status");
    }

    [RelayCommand]
    private void Close()
    {
        IsOpen = false;
        manualPanelSelection = false;
        windows.Close();
        IsDetached = false;
    }
    public void Toggle()
    {
        if (IsOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    [RelayCommand]
    private void Detach()
    {
        if (!CanDetach)
        {
            return;
        }
        IsDetached = true;
        windows.Show(this, () =>
        {
            IsDetached = false;
            IsOpen = false;
        });
    }

    [RelayCommand]
    private void Freeze()
    {
        IsFrozen = !IsFrozen;
        LiveLabel = IsFrozen ? $"FROZEN at {clock.GetUtcNow():HH:mm:ss.fff} · Live data is still being collected" : "LIVE";
        if (!IsFrozen)
        {
            displayedVersion = -1;
            Refresh();
        }
    }

    [RelayCommand]
    private void ClearEvents()
    {
        if (SelectedVehicle is { } id)
        {
            diagnostics.ClearEvents(id);
            if (!IsFrozen)
            {
                Events.Clear();
            }
        }
    }
    [RelayCommand]
    private async Task CopyEvents()
    {
        if (SelectedVehicle is { } id)
        {
            var text = await Task.Run(() => diagnostics.CreateSnapshotJson(id));
            await clipboard.SetTextAsync(text);
        }
    }

    [RelayCommand]
    private void AddMarker()
    {
        if (SelectedVehicle is { } id)
        {
            diagnostics.AddMarker(id, MarkerText);
            MarkerText = "";
            Refresh();
        }
    }

    /// <summary>Refreshes presentation at most once per 100 ms timer tick; no per-packet UI dispatch.</summary>
    public void Refresh()
    {
        if (presentationDisposed || !IsOpen || IsFrozen)
        {
            return;
        }
        foreach (var id in diagnostics.Vehicles)
        {
            if (!Vehicles.Contains(id))
            {
                Vehicles.Add(id);
            }
        }
        if (SelectedVehicle is not { } vehicle)
        {
            if (Vehicles.Count > 0)
            {
                SelectedVehicle = activeVehicle.VehicleId ?? Vehicles[0];
            }
            return;
        }
        var snapshot = diagnostics.GetSnapshot(vehicle);
        var arming = diagnostics.GetArming(vehicle);
        var state = snapshot.State;
        Header = $"{state?.DisplayName ?? vehicle.ToString()} · {(snapshot.Disconnected ? "Disconnected" : state?.Connection.State.ToString() ?? "Unknown")} · {state?.Flight.Mode} · {arming.Summary}";
        LiveLabel = snapshot.Disconnected || state?.Connection.State == VehicleConnectionState.Offline
            ? "DISCONNECTED · Last values are stale"
            : state?.Connection.State == VehicleConnectionState.Online ? "LIVE" : "STALE / UNKNOWN · Inspect sample ages";
        ReplaceDetails(snapshot, arming);
        if (ShowRaw && FollowRaw)
        {
            if ((!string.IsNullOrWhiteSpace(RawSystem) && !byte.TryParse(RawSystem, out _)) ||
                (!string.IsNullOrWhiteSpace(RawComponent) && !byte.TryParse(RawComponent, out _)))
            {
                Raw.Clear();
            }
            else
            {
                var rows = diagnostics.GetRaw(vehicle, RawFilter, byte.TryParse(RawSystem, out var system) ? system : null,
                    byte.TryParse(RawComponent, out var component) ? component : null);
                if (!Raw.SequenceEqual(rows))
                {
                    Raw.Clear();
                    foreach (var row in rows)
                    {
                        Raw.Add(row);
                    }
                }
            }
        }
        if (displayedVersion != snapshot.Version)
        {
            displayedVersion = snapshot.Version;
            var recent = diagnostics.GetRecentEvents(vehicle, 200);
            if (!Events.SequenceEqual(recent))
            {
                Events.Clear();
                foreach (var item in recent)
                {
                    Events.Add(item);
                }
            }
        }
    }

    private void ReplaceDetails(VehicleLiveDiagnosticSnapshot snapshot, VehicleArmingDiagnostic arming)
    {
        var values = (SelectedPanel == "Outputs" ? diagnostics.GetOutputs(snapshot.VehicleId) :
            VehicleDiagnosticPanels.Describe(SelectedPanel, snapshot, arming, clock.GetUtcNow())).ToArray();
        if (ShowRc)
        {
            var channels = diagnostics.GetRcChannels(snapshot.VehicleId);
            for (var index = 0; index < channels.Count; index++)
            {
                if (index >= Channels.Count)
                {
                    Channels.Add(channels[index]);
                }
                else if (Channels[index] != channels[index])
                {
                    Channels[index] = channels[index];
                }
            }
            while (Channels.Count > channels.Count)
            {
                Channels.RemoveAt(Channels.Count - 1);
            }
        }
        for (var i = 0; i < values.Length; i++)
        {
            if (i >= Details.Count)
            {
                Details.Add(values[i]);
            }
            else if (Details[i] != values[i])
            {
                Details[i] = values[i];
            }
        }
        while (Details.Count > values.Length)
        {
            Details.RemoveAt(Details.Count - 1);
        }
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        presentationDisposed = true;
        timer.Stop();
        windows.Close();
        base.Dispose();
    }
}
