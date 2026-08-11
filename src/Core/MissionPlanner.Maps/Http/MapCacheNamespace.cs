namespace MissionPlanner.Maps.Http;

/// <summary>Identifies one isolated map cache namespace.</summary>
/// <param name="SourceId">Source identifier.</param>
/// <param name="ProductId">Product identifier.</param>
/// <param name="StyleId">Style identifier.</param>
public sealed record MapCacheNamespace(string SourceId, string ProductId, string StyleId)
{
    /// <summary>Gets a filesystem-safe stable key.</summary>
    public string Key => string.Join("_", new[] { SourceId, ProductId, StyleId }.Select(value => string.Concat(value.Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '_'))));
}
