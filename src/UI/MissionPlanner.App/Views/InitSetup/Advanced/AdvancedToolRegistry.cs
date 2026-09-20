using Avalonia.Controls;
using MissionPlanner.Core.Setup.Advanced;

namespace MissionPlanner.App.Views.InitSetup.Advanced;

/// <summary>
/// Registers a tool's page factory without constructing it before navigation.
/// </summary>
public sealed record AdvancedToolRegistration(AdvancedFeatureId Id, Func<Page> CreatePage);

/// <summary>Validates explicit child routes and creates only registered pages.</summary>
public sealed class AdvancedToolRegistry
{
    private readonly Dictionary<AdvancedFeatureId, AdvancedToolRegistration> registrations = [];

    /// <summary>Initializes destinations, rejecting duplicate IDs and missing factories.</summary>
    public AdvancedToolRegistry(IEnumerable<AdvancedToolRegistration> entries)
    {
        foreach (var entry in entries)
        {
            if (!Enum.IsDefined(entry.Id) || entry.CreatePage is null || !registrations.TryAdd(entry.Id, entry))
            {
                throw new ArgumentException("Advanced destinations require unique known IDs and a page factory.", nameof(entries));
            }
        }
    }

    /// <summary>Gets whether the feature has an implemented destination.</summary>
    public bool Contains(AdvancedFeatureId id)
    {
        return registrations.ContainsKey(id);
    }

    /// <summary>Creates the exact registered destination, rejecting missing targets.</summary>
    public Page Create(string route)
    {
        var feature = AdvancedFeatureCatalog.All.SingleOrDefault(item => item.Route == route);
        return feature is null || !registrations.TryGetValue(feature.Id, out var registration)
            ? throw new ArgumentException("No Advanced tool is registered for this route.", nameof(route))
            : registration.CreatePage() ?? throw new InvalidOperationException("The Advanced page factory returned no page.");
    }
}
