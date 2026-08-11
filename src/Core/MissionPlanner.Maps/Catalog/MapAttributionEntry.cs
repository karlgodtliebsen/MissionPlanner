namespace MissionPlanner.Maps.Catalog;

/// <summary>Describes an attribution requirement.</summary>
/// <param name="Id">Stable attribution identifier.</param>
/// <param name="Text">Required attribution text.</param>
/// <param name="Uri">Optional attribution link.</param>
/// <param name="RequiredOnMap">Whether the text is required on interactive maps.</param>
/// <param name="RequiredOnExport">Whether the text is required on exports.</param>
public sealed record MapAttributionEntry(string Id, string Text, Uri? Uri, bool RequiredOnMap, bool RequiredOnExport);
