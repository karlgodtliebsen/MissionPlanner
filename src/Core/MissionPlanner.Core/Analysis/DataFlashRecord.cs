namespace MissionPlanner.Core.Analysis;

internal sealed record DataFlashRecord(string Name, IReadOnlyDictionary<string, object> Fields)
{
    internal double Number(string key, double fallback = double.NaN)
    {
        return Fields.TryGetValue(key, out var value) && value is double number ? number : fallback;
    }
}
