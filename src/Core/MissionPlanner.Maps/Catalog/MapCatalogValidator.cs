namespace MissionPlanner.Maps.Catalog;

/// <summary>Represents one map catalog validation error.</summary>
/// <param name="Path">Logical catalog path.</param>
/// <param name="Message">Validation message.</param>
public sealed record MapCatalogValidationIssue(string Path, string Message);

/// <summary>Validates map catalog structure and cross-references.</summary>
public static class MapCatalogValidator
{
    /// <summary>Returns every detected catalog error.</summary>
    /// <param name="catalog">Catalog to validate.</param>
    /// <returns>Validation issues; empty when valid.</returns>
    public static IReadOnlyList<MapCatalogValidationIssue> Validate(MapCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var issues = new List<MapCatalogValidationIssue>();
        if (catalog.SchemaVersion != 1)
            issues.Add(new("schemaVersion", $"Unsupported schema version {catalog.SchemaVersion}."));
        if (string.IsNullOrWhiteSpace(catalog.CatalogVersion))
            issues.Add(new("catalogVersion", "A catalog version is required."));

        ValidateUnique(catalog.Providers, item => item.Id, "providers", issues);
        ValidateUnique(catalog.Products, item => item.Id, "products", issues);
        ValidateUnique(catalog.Policies, item => item.Id, "policies", issues);
        ValidateUnique(catalog.Attributions, item => item.Id, "attributions", issues);
        ValidateUnique(catalog.Sources, item => item.Id, "sources", issues);

        var providerIds = catalog.Providers.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var productIds = catalog.Products.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var policyIds = catalog.Policies.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var attributionIds = catalog.Attributions.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var product in catalog.Products)
        {
            if (!providerIds.Contains(product.ProviderId))
                issues.Add(new($"products/{product.Id}/providerId", $"Unknown provider '{product.ProviderId}'."));
        }

        foreach (var source in catalog.Sources)
        {
            var path = $"sources/{source.Id}";
            if (!productIds.Contains(source.ProductId))
                issues.Add(new($"{path}/productId", $"Unknown product '{source.ProductId}'."));
            if (!policyIds.Contains(source.PolicyId))
                issues.Add(new($"{path}/policyId", $"Unknown policy '{source.PolicyId}'."));
            foreach (var attributionId in source.AttributionIds.Distinct(StringComparer.Ordinal))
            {
                if (!attributionIds.Contains(attributionId))
                    issues.Add(new($"{path}/attributionIds", $"Unknown attribution '{attributionId}'."));
            }

            if (source.MinimumZoom < 0 || source.MaximumZoom < source.MinimumZoom)
                issues.Add(new($"{path}/zoom", "Zoom limits must be non-negative and ordered."));

            var isHttp = source.AccessKind is MapAccessKind.HttpXyz or MapAccessKind.HttpTms or MapAccessKind.Wms or MapAccessKind.Wmts;
            if (isHttp && !IsHttpTemplate(source.UriTemplate))
                issues.Add(new($"{path}/uriTemplate", "Network sources require an absolute HTTP or HTTPS URI template."));
            if (!isHttp && source.UriTemplate is not null)
                issues.Add(new($"{path}/uriTemplate", "Only network sources may define a URI template."));
            if (source.AccessKind == MapAccessKind.LocalArchive && source.ArchiveFormat == MapArchiveFormat.None)
                issues.Add(new($"{path}/archiveFormat", "Local archive sources require an archive format."));
            if (source.AccessKind != MapAccessKind.LocalArchive && source.ArchiveFormat != MapArchiveFormat.None)
                issues.Add(new($"{path}/archiveFormat", "Only local archive sources may define an archive format."));
            if (source.AccessKind == MapAccessKind.Blank && source.CredentialRequirement != MapCredentialRequirement.None)
                issues.Add(new($"{path}/credentialRequirement", "Blank sources cannot require credentials."));
        }

        return issues;
    }

    /// <summary>Throws when a catalog is invalid.</summary>
    /// <param name="catalog">Catalog to validate.</param>
    public static void ValidateAndThrow(MapCatalog catalog)
    {
        var issues = Validate(catalog);
        if (issues.Count != 0)
            throw new InvalidDataException("Invalid map catalog:" + Environment.NewLine + string.Join(Environment.NewLine, issues.Select(issue => $"- {issue.Path}: {issue.Message}")));
    }

    private static void ValidateUnique<T>(IEnumerable<T> values, Func<T, string> idSelector, string path, ICollection<MapCatalogValidationIssue> issues)
    {
        foreach (var group in values.GroupBy(idSelector, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(group.Key))
                issues.Add(new(path, "Identifiers cannot be empty."));
            else if (group.Skip(1).Any())
                issues.Add(new($"{path}/{group.Key}", $"Duplicate identifier '{group.Key}'."));
        }
    }

    private static bool IsHttpTemplate(string? value) =>
        Uri.TryCreate(value?.Replace("{z}", "0", StringComparison.Ordinal)
            .Replace("{x}", "0", StringComparison.Ordinal)
            .Replace("{y}", "0", StringComparison.Ordinal), UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https";
}
