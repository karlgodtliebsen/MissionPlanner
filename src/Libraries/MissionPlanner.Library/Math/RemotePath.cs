namespace MissionPlanner.Core.Vehicles;

/// <summary>
/// Provides the public API for RemotePath.
/// </summary>
public static class RemotePath
{
    /// <summary>
    /// Provides the public API for Normalize.
    /// </summary>
    public static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "/") return "/";
        var parts = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var normalized = new List<string>();
        foreach (var part in parts)
        {
            if (part == ".") continue;
            if (part == "..") { if (normalized.Count > 0) normalized.RemoveAt(normalized.Count - 1); continue; }
            normalized.Add(part);
        }
        return "/" + string.Join('/', normalized);
    }

    /// <summary>
    /// Provides the public API for Join.
    /// </summary>
    public static string Join(string parent, string child) => Normalize($"{Normalize(parent).TrimEnd('/')}/{child}");
    /// <summary>
    /// Provides the public API for Parent.
    /// </summary>
    public static string Parent(string path)
    {
        var normalized = Normalize(path);
        var separator = normalized.LastIndexOf('/');
        return separator <= 0 ? "/" : normalized[..separator];
    }
}
