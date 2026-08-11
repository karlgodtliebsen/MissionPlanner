using System.Text.Json;

namespace MissionPlanner.Core.ConfigTuning.Fences;

/// <summary>Serializes the complete local fence workspace in a bounded, versioned JSON format.</summary>
public interface IFencePlanFileCodec
{
    /// <summary>Serializes a fence plan.</summary>
    string Serialize(FencePlan plan);
    /// <summary>Parses and validates a fence plan.</summary>
    FencePlan Deserialize(string json);
}

/// <summary>Default MissionPlanner fence-file codec.</summary>
public sealed class FencePlanFileCodec(IFenceGeometryValidator validator) : IFencePlanFileCodec
{
    private const int MaxCharacters = 4 * 1024 * 1024;
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <inheritdoc />
    public string Serialize(FencePlan plan) => JsonSerializer.Serialize(new FenceDocument(1, plan), Options);

    /// <inheritdoc />
    public FencePlan Deserialize(string json)
    {
        if (json.Length > MaxCharacters) throw new InvalidDataException("Fence file exceeds the 4 MiB limit.");
        var document = JsonSerializer.Deserialize<FenceDocument>(json, Options) ?? throw new InvalidDataException("Fence file is empty.");
        if (document.Version != 1) throw new InvalidDataException($"Fence file version {document.Version} is not supported.");
        var validation = validator.Validate(document.Plan);
        if (!validation.IsValid) throw new InvalidDataException(string.Join(" ", validation.Issues.Select(issue => issue.Message)));
        return document.Plan;
    }

    private sealed record FenceDocument(int Version, FencePlan Plan);
}
