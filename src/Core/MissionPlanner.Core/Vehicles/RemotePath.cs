namespace MissionPlanner.Core.Vehicles;

public static class RemotePath
{
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

    public static string Join(string parent, string child) => Normalize($"{Normalize(parent).TrimEnd('/')}/{child}");
    public static string Parent(string path)
    {
        var normalized = Normalize(path);
        var separator = normalized.LastIndexOf('/');
        return separator <= 0 ? "/" : normalized[..separator];
    }
}
