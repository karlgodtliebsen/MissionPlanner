namespace MissionPlanner.Maps.Catalog;

/// <summary>Represents one map catalog validation error.</summary>
/// <param name="Path">Logical catalog path.</param>
/// <param name="Message">Validation message.</param>
public sealed record MapCatalogValidationIssue(string Path, string Message);
