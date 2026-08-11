namespace MissionPlanner.Firmware.Dfu;

/// <summary>Records an operator-approved association without inferring it from MCU identity.</summary>
public sealed record DfuRememberedAssociation(
    string Platform,
    int? BoardId,
    string ApplicationIdentity,
    string DfuSerialNumber);
