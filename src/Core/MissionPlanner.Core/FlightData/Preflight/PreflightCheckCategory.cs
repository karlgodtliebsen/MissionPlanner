namespace MissionPlanner.Core.FlightData.Preflight;

/// <summary>Identifies a preflight readiness check category.</summary>
public enum PreflightCheckCategory
{
    /// <summary>Link and heartbeat checks.</summary>
    Connection,

    /// <summary>Firmware and vehicle identity checks.</summary>
    Identity,

    /// <summary>Armed and flight-state checks.</summary>
    Flight,

    /// <summary>Onboard sensor checks.</summary>
    Sensors,

    /// <summary>Position and estimator checks.</summary>
    Navigation,

    /// <summary>Power-system checks.</summary>
    Power,

    /// <summary>Operator-control link checks.</summary>
    Radio,

    /// <summary>Storage, logging, and diagnostic checks.</summary>
    Diagnostics
}
