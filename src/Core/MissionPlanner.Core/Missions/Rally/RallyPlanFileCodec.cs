using System.Text.Json;

namespace MissionPlanner.Core.Missions.Rally;

/// <summary>Bounded versioned rally file codec.</summary>
public sealed class RallyPlanFileCodec : IRallyPlanFileCodec
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    /// <inheritdoc />
    public string Serialize(RallyPlan plan, DateTimeOffset createdAt) => JsonSerializer.Serialize(new Document(1, createdAt, plan), Options);
    /// <inheritdoc />
    public RallyPlan Deserialize(string json)
    {
        if (json.Length > 4 * 1024 * 1024) throw new InvalidDataException("Rally file exceeds the 4 MiB limit.");
        var document = JsonSerializer.Deserialize<Document>(json, Options) ?? throw new InvalidDataException("Rally file is empty.");
        if (document.SchemaVersion != 1 || document.Plan.Points.Any(point => !point.Position.IsValid)) throw new InvalidDataException("Rally file version or coordinates are invalid.");
        return document.Plan;
    }
    private sealed record Document(int SchemaVersion, DateTimeOffset CreatedAt, RallyPlan Plan);
}