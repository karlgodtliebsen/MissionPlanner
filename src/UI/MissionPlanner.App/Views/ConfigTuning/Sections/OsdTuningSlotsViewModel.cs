using AsyncAwaitBestPractices;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.Core.ConfigTuning.Osd;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.App.Views.ConfigTuning.Sections;

/// <summary>Reviews and assigns the adjustable parameters on OSD screens 5 and 6.</summary>
public sealed partial class OsdTuningSlotsViewModel(OsdTuningSlotService service, IActiveVehicleContext active,
    IVehicleParameterRegistry parameters, IUserConfirmationService confirmation, ILogger<OsdTuningSlotsViewModel> logger) : ViewModelBase(logger)
{
    private CancellationTokenSource lifetime = new();
    private bool disposed;
    private VehicleId? loadedVehicle;
    private CancellationToken loadedBoundary;
    /// <summary>Gets reported parameter names for assignment.</summary>
    public ObservableCollection<string> ParameterNames { get; } = [];
    /// <summary>Gets slots returned by the selected screen.</summary>
    public ObservableCollection<OsdTuningSlot> Slots { get; } = [];
    /// <summary>Gets or sets the screen to read.</summary>
    [ObservableProperty] public partial int Screen { get; set; } = 5;
    /// <summary>Gets or sets the slot being edited.</summary>
    [ObservableProperty] public partial OsdTuningSlot? SelectedSlot { get; set; }
    /// <summary>Gets or sets the parameter assignment.</summary>
    [ObservableProperty] public partial string ParameterName { get; set; } = string.Empty;
    /// <summary>Gets or sets the firmware presentation type (0 custom, 1–7 known enumeration types).</summary>
    [ObservableProperty] public partial int SlotType { get; set; }
    /// <summary>Gets or sets the custom minimum.</summary>
    [ObservableProperty] public partial double Minimum { get; set; }
    /// <summary>Gets or sets the custom maximum.</summary>
    [ObservableProperty] public partial double Maximum { get; set; }
    /// <summary>Gets or sets the custom adjustment increment.</summary>
    [ObservableProperty] public partial double Increment { get; set; }

    partial void OnSelectedSlotChanged(OsdTuningSlot? value)
    {
        if (value is null) return;
        ParameterName = value.Parameter;
        SlotType = value.Type;
        Minimum = value.Minimum;
        Maximum = value.Maximum;
        Increment = value.Increment;
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task ReadAsync(CancellationToken cancellationToken)
    {
        if (IsBusy) return;
        IsBusy = true;
        Slots.Clear();
        loadedVehicle = null;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token, active.ConnectionCancellationToken);
        linked.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            var id = active.VehicleId ?? throw new InvalidOperationException("Select a connected vehicle first.");
            var boundary = active.ConnectionCancellationToken;
            var screen = checked((byte)Screen);
            var slots = new List<OsdTuningSlot>();
            for (byte index = 1; index <= 9; index++) slots.Add(await service.ReadAsync(id, screen, index, linked.Token));
            loadedVehicle = id;
            loadedBoundary = boundary;
            ParameterNames.Clear();
            foreach (var name in parameters.GetAllParameters(id).Keys.Order(StringComparer.Ordinal)) ParameterNames.Add(name);
            foreach (var slot in slots) Slots.Add(slot);
            SelectedSlot = Slots.FirstOrDefault();
            SetMessages("Nine tuning slots read. Select a slot, review its assignment and apply explicitly. Screen enabling remains in the OSD parameter editor.", null);
        }
        catch (OperationCanceledException) { SetMessages("OSD slot read cancelled or timed out.", null); }
        catch (Exception ex) { SetMessages(ex); }
        finally { IsBusy = false; }
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task ApplyAsync(CancellationToken cancellationToken)
    {
        if (IsBusy || SelectedSlot is not { } previous || loadedVehicle is not { } id) return;
        IsBusy = true;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token, loadedBoundary);
        try
        {
            if (active.VehicleId != id || active.ConnectionCancellationToken != loadedBoundary) throw new InvalidOperationException("Read the slots again after a connection change.");
            var proposed = previous with { Parameter = ParameterName.Trim().ToUpperInvariant(), Type = checked((byte)SlotType), Minimum = (float)Minimum, Maximum = (float)Maximum, Increment = (float)Increment };
            if (!await confirmation.ConfirmAsync("Assign OSD tuning slot", $"OSD {previous.Screen}, slot {previous.Index}: {previous.Parameter} → {proposed.Parameter}. Type {proposed.Type}; range {proposed.Minimum}–{proposed.Maximum}, step {proposed.Increment}. This changes the adjustable parameter assignment, not its current value.", "Apply assignment", linked.Token)) return;
            var actual = await service.ApplyAsync(id, previous, proposed, true, linked.Token);
            var position = Slots.IndexOf(previous);
            if (position >= 0) Slots[position] = actual;
            SelectedSlot = actual;
            SetMessages("OSD slot assignment acknowledged and verified by readback.", null);
        }
        catch (OperationCanceledException) { SetMessages("OSD slot operation cancelled or timed out. Read again to determine the saved assignment.", null); }
        catch (Exception ex) { SetMessages(ex); }
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
        await Task.WhenAll(new[] { ReadCommand.ExecutionTask, ApplyCommand.ExecutionTask }.OfType<Task>());
        loadedVehicle = null;
        Slots.Clear();
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
