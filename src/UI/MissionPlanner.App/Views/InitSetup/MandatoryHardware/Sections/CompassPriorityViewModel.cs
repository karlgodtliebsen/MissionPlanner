using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;
using MissionPlanner.App.Views.Navigation;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>A reported or stored compass identity in the editable priority list.</summary>
/// <param name="Id">Firmware device ID.</param>
/// <param name="Reported">Whether a COMPASS_DEV_ID slot reports the ID.</param>
public sealed record CompassPriorityItem(uint Id, bool Reported)
{
    /// <summary>Gets a label that distinguishes reported IDs from missing stored priorities.</summary>
    public string Label => $"{Id} · {(Reported ? "reported" : "missing from current device slots")}";
}

/// <summary>Reviews reordered or cleaned compass priorities separately from calibration.</summary>
public sealed partial class CompassPriorityViewModel : ParameterHardwareViewModel
{
    private readonly IActiveVehicleContext active;
    private readonly IVehicleParameterRegistry parameters;
    private readonly ICompassConfigurationService compass;
    private readonly IUserConfirmationService confirmation;
    private IReadOnlyDictionary<string, float> baseline = new Dictionary<string, float>();
    private VehicleId? loadedId;
    private CancellationToken connection;

    /// <summary>Creates a priority and advanced-settings editor.</summary>
    public CompassPriorityViewModel(IActiveVehicleContext active, IVehicleParameterRegistry parameters,
        ICompassConfigurationService compass, IOptionalHardwareService hardware, IUserConfirmationService confirmation,
        ILogger<CompassPriorityViewModel> logger, INavigationService navigation)
        : base("compass-advanced", active, hardware, logger, navigation)
    {
        this.active = active;
        this.parameters = parameters;
        this.compass = compass;
        this.confirmation = confirmation;
    }

    /// <summary>Gets the staged order; the first three IDs become firmware priorities.</summary>
    public ObservableCollection<CompassPriorityItem> Order { get; } = [];
    /// <summary>Gets or sets the identity to move or remove when missing.</summary>
    [ObservableProperty] public partial CompassPriorityItem? Selected { get; set; }

    /// <inheritdoc />
    public override async Task ActivateAsync()
    {
        await base.ActivateAsync();
        ReadOrder();
    }

    [RelayCommand]
    private void ReadOrder()
    {
        if (IsBusy)
        {
            return;
        }
        Order.Clear();
        loadedId = active.VehicleId;
        connection = active.ConnectionCancellationToken;
        if (!active.IsOnline || loadedId is not { } id)
        {
            return;
        }
        baseline = parameters.GetAllParameters(id).Where(pair => pair.Key.StartsWith("COMPASS_DEV_ID", StringComparison.Ordinal) ||
            pair.Key is "COMPASS_PRIO1_ID" or "COMPASS_PRIO2_ID" or "COMPASS_PRIO3_ID")
            .ToDictionary(pair => pair.Key, pair => pair.Value.Value);
        static uint Id(float value) => float.IsFinite(value) && value > 0 && value < uint.MaxValue ? (uint)value : 0;
        var reported = baseline.Where(pair => pair.Key.StartsWith("COMPASS_DEV_ID", StringComparison.Ordinal)).Select(pair => Id(pair.Value)).Where(value => value != 0).ToHashSet();
        var priorities = Enumerable.Range(1, 3).Select(index => Id(baseline.GetValueOrDefault($"COMPASS_PRIO{index}_ID"))).Where(value => value != 0);
        foreach (var value in priorities.Concat(reported.Order()).Distinct())
        {
            Order.Add(new(value, reported.Contains(value)));
        }
        Selected = Order.FirstOrDefault();
    }

    [RelayCommand] private void MoveUp() => Move(-1);
    [RelayCommand] private void MoveDown() => Move(1);
    private void Move(int offset)
    {
        if (IsBusy || Selected is not { } selected)
        {
            return;
        }
        var index = Order.IndexOf(selected);
        var next = index + offset;
        if (index >= 0 && next >= 0 && next < Order.Count)
        {
            Order.Move(index, next);
            Selected = selected;
        }
    }

    [RelayCommand]
    private void RemoveMissing()
    {
        if (!IsBusy && Selected is { Reported: false } missing)
        {
            Order.Remove(missing);
        }
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task ApplyOrderAsync(CancellationToken token)
    {
        if (IsBusy || loadedId is not { } id || active.VehicleId != id || connection != active.ConnectionCancellationToken || connection.IsCancellationRequested)
        {
            SetMessages(errorMessage: "Refresh the priority order for this connection before applying.");
            return;
        }
        var proposed = Order.Take(3).Select(item => item.Id).ToArray();
        var review = string.Join(Environment.NewLine, Enumerable.Range(1, 3).Select(index =>
            $"COMPASS_PRIO{index}_ID: {baseline.GetValueOrDefault($"COMPASS_PRIO{index}_ID")} → {(index <= proposed.Length ? proposed[index - 1] : 0)}"));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, connection);
        SetBusy();
        try
        {
            if (!await confirmation.ConfirmAsync("Review compass priorities", review + "\nApply this order? Reboot and recheck calibration afterwards.", "Apply priorities", linked.Token))
            {
                return;
            }
            linked.Token.ThrowIfCancellationRequested();
            var result = await compass.SetPrioritiesAsync(id, baseline, proposed, true, linked.Token);
            SetMessages(result.Success ? result.Message : null, result.Success ? null : result.Message);
        }
        catch (OperationCanceledException)
        {
            SetMessages(errorMessage: "Priority update cancelled; a partial order may remain. Refresh before retrying.");
        }
        catch (Exception exception) { SetMessages(exception); }
        finally { ResetBusy(); }
    }

    /// <inheritdoc />
    public override async Task DeactivateAsync()
    {
        ApplyOrderCommand.Cancel();
        if (ApplyOrderCommand.ExecutionTask is { } pending) await pending;
        await base.DeactivateAsync();
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        ApplyOrderCommand.Cancel();
        base.Dispose();
    }
}
