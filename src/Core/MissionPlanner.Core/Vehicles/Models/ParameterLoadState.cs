namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>Describes the lifecycle state of a connection-scoped parameter load.</summary>
public enum ParameterLoadState
{
    /// <summary>The load is being prepared.</summary>
    Starting,

    /// <summary>Parameters are being transferred from the vehicle.</summary>
    Downloading,

    /// <summary>The complete parameter set is available in the registry.</summary>
    Completed,

    /// <summary>The load ended without obtaining a complete parameter set.</summary>
    Failed,

    /// <summary>The load was cancelled, normally because the vehicle disconnected.</summary>
    Cancelled
}
