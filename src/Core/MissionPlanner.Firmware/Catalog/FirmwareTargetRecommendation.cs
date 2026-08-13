using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Catalog;

/// <summary>Combines a manifest entry with explicit selection evidence.</summary>
public sealed record FirmwareTargetRecommendation(FirmwareManifestEntry Entry, FirmwareTargetMatchReason Reason, FirmwareTargetConfidence Confidence);
