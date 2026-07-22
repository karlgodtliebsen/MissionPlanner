namespace MissionPlanner.MavLink.Generator;

/// <summary>
/// Describes one resolved MAVLink protocol enum.
/// </summary>
/// <param name="Name">The XML enum name.</param>
/// <param name="Dialect">The dialect that first declares the enum.</param>
/// <param name="Description">The enum description.</param>
/// <param name="IsBitmask">Whether the XML declares bitmask semantics.</param>
/// <param name="UnderlyingType">The inferred C# storage type.</param>
/// <param name="Entries">The merged enum entries.</param>
public sealed record DialectEnumDefinition(
    string Name,
    string Dialect,
    string Description,
    bool IsBitmask,
    string UnderlyingType,
    IReadOnlyList<DialectEnumEntryDefinition> Entries);

/// <summary>
/// Describes one MAVLink protocol enum entry.
/// </summary>
/// <param name="Name">The XML entry name.</param>
/// <param name="Value">The unsigned numeric value.</param>
/// <param name="Description">The entry description.</param>
public sealed record DialectEnumEntryDefinition(string Name, ulong Value, string Description);
