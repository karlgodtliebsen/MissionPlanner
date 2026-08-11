namespace MissionPlanner.Firmware.Model;

/// <summary>Reports the result of a platform firmware flashing operation.</summary>
/// <param name="Succeeded">Whether flashing completed successfully.</param>
/// <param name="Message">The adapter result detail.</param>
public sealed record FirmwareFlashResult(bool Succeeded, string Message);
