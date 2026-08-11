using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.Missions.Planning;

/// <summary>Default in-memory mission-map interaction coordinator.</summary>
public sealed class MissionMapInteractionService : IMissionMapInteractionService
{
    private MissionPlanningOverlaySnapshot overlay = MissionPlanningOverlaySnapshot.Empty;

    /// <inheritdoc />
    public event EventHandler? Changed;
    /// <inheritdoc />
    public MissionMapInteractionState State { get; private set; } = MissionMapInteractionState.None;
    /// <inheritdoc />
    public MissionPlanningOverlaySnapshot Overlay => overlay;

    /// <inheritdoc />
    public void Enter(MissionMapInteractionMode mode, string prompt)
    {
        if (mode == MissionMapInteractionMode.None)
        {
            Cancel();
            return;
        }

        ClearTemporaryOverlay(State.Mode);
        State = new(mode, prompt, [], null);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public bool AcceptClick(GeoPosition position)
    {
        if (State.Mode == MissionMapInteractionMode.None || !position.IsValid)
            return false;

        var positions = State.Positions.Append(position).ToArray();
        State = State with { Positions = positions, PointerPosition = position };
        ApplyTemporaryPositions(positions);
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <inheritdoc />
    public void MovePointer(GeoPosition position)
    {
        if (State.Mode == MissionMapInteractionMode.None || !position.IsValid)
            return;
        State = State with { PointerPosition = position };
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void Complete()
    {
        if (State.Mode == MissionMapInteractionMode.None)
            return;
        State = MissionMapInteractionState.None;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void Cancel()
    {
        if (State.Mode == MissionMapInteractionMode.None && overlay.TemporaryMeasurement.Count == 0)
            return;
        var cancelledMode = State.Mode;
        State = MissionMapInteractionState.None;
        ClearTemporaryOverlay(cancelledMode);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyTemporaryPositions(IReadOnlyList<GeoPosition> positions)
    {
        overlay = State.Mode switch
        {
            MissionMapInteractionMode.DrawPolygon => overlay with { DrawnPolygon = positions },
            MissionMapInteractionMode.MeasureDistance => overlay with { TemporaryMeasurement = positions },
            _ => overlay
        };
    }

    private void ClearTemporaryOverlay(MissionMapInteractionMode mode)
    {
        overlay = mode switch
        {
            MissionMapInteractionMode.DrawPolygon => overlay with { DrawnPolygon = [] },
            MissionMapInteractionMode.MeasureDistance => overlay with { TemporaryMeasurement = [] },
            _ => overlay
        };
    }
}
