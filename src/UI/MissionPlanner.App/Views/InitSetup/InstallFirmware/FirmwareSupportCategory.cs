namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>Groups firmware support links by owner and purpose.</summary>
public enum FirmwareSupportCategory
{
    /// <summary>Official ArduPilot firmware and documentation resources.</summary>
    ArduPilot,

    /// <summary>Official STMicroelectronics programming resources.</summary>
    StMicroelectronics,

    /// <summary>Clearly identified third-party driver fallback resources.</summary>
    DriverFallback
}

