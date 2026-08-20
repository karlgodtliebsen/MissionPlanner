namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Contains the parameter settings currently supported by one mandatory-hardware workflow.</summary>
/// <param name="Settings">The reported, metadata-enriched settings.</param>
/// <param name="Guidance">Safety or usage guidance for the workflow.</param>
public sealed record MandatoryParameterConfiguration(
    IReadOnlyList<PeripheralSetting> Settings,
    IReadOnlyList<string> Guidance);

/// <summary>Reports the result of applying one mandatory-hardware parameter.</summary>
/// <param name="Success">Whether the vehicle accepted the value.</param>
/// <param name="Message">A user-facing result message.</param>
public sealed record MandatoryParameterApplyResult(bool Success, string Message);
