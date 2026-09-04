namespace MissionPlanner.Core.Tests;

/// <summary>
/// Guards MissionPlanner-owned XAML against reintroducing view-level light/dark selection.
/// </summary>
public sealed class ThemeArchitectureTests
{
    private static readonly string[] ForbiddenPatterns =
    [
        "AppThemeBinding",
        "SurfaceDark",
        "PrimaryDark",
        "BackgroundDark",
        "WarningDark",
        "ErrorDark"
    ];

    /// <summary>Verifies application XAML relies on semantic resources only.</summary>
    [Fact]
    public void ApplicationXamlDoesNotSelectLightOrDarkColors()
    {
        var applicationDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "UI",
            "MAUI",
            "MissionPlanner.App");
        var violations = new List<string>();

        foreach (var path in Directory.EnumerateFiles(applicationDirectory, "*.xaml", SearchOption.AllDirectories)
                     .Where(path => !IsGeneratedPath(path)))
        {
            var content = File.ReadAllText(path);
            foreach (var pattern in ForbiddenPatterns.Where(content.Contains))
            {
                violations.Add($"{Path.GetRelativePath(applicationDirectory, path)}: {pattern}");
            }
        }

        Assert.Empty(violations);
    }

    private static bool IsGeneratedPath(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Contains("bin", StringComparer.OrdinalIgnoreCase) ||
               segments.Contains("obj", StringComparer.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, ".git")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the MissionPlanner repository root.");
    }
}
