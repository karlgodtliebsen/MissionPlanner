namespace MissionPlanner.Maps.Custom;

/// <summary>Describes the most recent custom source connection test.</summary>
/// <param name="Succeeded">Whether the endpoint responded and metadata matched.</param>
/// <param name="Message">Redacted status text.</param>
/// <param name="TestedAt">Test timestamp.</param>
/// <param name="Metadata">Optional parsed WMS/WMTS metadata.</param>
public sealed record CustomMapConnectionStatus(bool Succeeded, string Message, DateTimeOffset TestedAt, MapServiceMetadata? Metadata);
