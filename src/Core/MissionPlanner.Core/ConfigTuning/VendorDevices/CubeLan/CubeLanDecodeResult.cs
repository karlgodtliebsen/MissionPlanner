namespace MissionPlanner.Core.ConfigTuning.VendorDevices.CubeLan;

/// <summary>Describes a CubeLAN decode attempt.</summary>
/// <param name="Success">Whether a documented configuration envelope was decoded.</param>
/// <param name="Configuration">The decoded configuration.</param>
/// <param name="Message">The decode failure reason.</param>
public sealed record CubeLanDecodeResult(bool Success, CubeLanConfiguration? Configuration, string? Message);
