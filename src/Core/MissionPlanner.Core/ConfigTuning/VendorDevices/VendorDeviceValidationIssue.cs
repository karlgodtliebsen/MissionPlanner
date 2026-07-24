namespace MissionPlanner.Core.ConfigTuning.VendorDevices;

/// <summary>Describes one device-specific validation failure.</summary>
/// <param name="Path">The configuration path.</param>
/// <param name="Message">The validation message.</param>
public sealed record VendorDeviceValidationIssue(string Path, string Message);
