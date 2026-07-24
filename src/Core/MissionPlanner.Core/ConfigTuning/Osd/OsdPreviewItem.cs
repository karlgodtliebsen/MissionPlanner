namespace MissionPlanner.Core.ConfigTuning.Osd;

/// <summary>Provides one item for rendering in a platform-neutral character-grid preview.</summary>
/// <param name="Key">The item key.</param>
/// <param name="Title">The short preview label.</param>
/// <param name="Column">The zero-based column.</param>
/// <param name="Row">The zero-based row.</param>
/// <param name="IsEnabled">Whether the item is enabled.</param>
public sealed record OsdPreviewItem(string Key, string Title, int Column, int Row, bool IsEnabled);
