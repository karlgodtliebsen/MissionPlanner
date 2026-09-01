namespace MissionPlanner.AvaloniaUI.App.Views.InitSetup.MandatoryHardware.Models;

/// <summary>Describes how an RC input channel should be presented.</summary>
public enum RadioChannelPresentationKind
{
    /// <summary>A centered pilot axis such as roll, pitch, or yaw.</summary>
    CenteredAxis,

    /// <summary>A throttle channel whose low endpoint is operationally significant.</summary>
    Throttle,

    /// <summary>An auxiliary channel that remains a continuous PWM input.</summary>
    Auxiliary
}

