using System.Text.RegularExpressions;

namespace MissionPlanner.Tests.ResourceTests;

/// <summary>
/// Renames image files so they satisfy the MAUI Resizetizer resource-name rule:
/// lowercase, start with a letter, contain only [a-z0-9_], and not end with an underscore
/// (validation regex: ^[a-z]([a-z0-9_]*[a-z0-9])?$).
/// </summary>
public static partial class MauiImageFileNameSanitizer
{
    [GeneratedRegex("^[a-z]([a-z0-9_]*[a-z0-9])?$")]
    private static partial Regex ValidResourceName();

    private static readonly HashSet<string> DefaultImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".gif",
        ".bmp",
        ".svg",
        ".webp",
        ".tiff",
        ".ico"
    };

    /// <summary>
    /// Provides the public API for IsValid.
    /// </summary>
    public static bool IsValid(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        return ValidResourceName().IsMatch(stem) && extension == extension.ToLowerInvariant();
    }

    /// <summary>
    /// Produces a valid resource file name from an arbitrary one, e.g.
    /// "Antenna Tracker-01_.png" -> "antenna_tracker_01.png".
    /// </summary>
    public static string Sanitize(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        if (name.StartsWith("x_") == false)
        {
            name = "x_" + name;
        }

        name = name.ToLowerInvariant().Replace(" ", "_").Replace("-", "_");

        if (name.EndsWith("_x") == false)
        {
            name += "_x";
        }

        return name + extension;
    }

    /// <summary>
    /// Renames every image file in <paramref name="directory"/> whose name violates the rule.
    /// Returns the (originalPath, newPath) pairs so callers can update XAML/C# references.
    /// Safe to run repeatedly; valid names are left untouched and collisions get a numeric suffix.
    /// </summary>
    public static IReadOnlyList<(string OriginalPath, string NewPath)> RenameInvalidFiles(string directory, bool recurse = false, IReadOnlySet<string>? extensions = null)
    {
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException(directory);
        }

        extensions ??= DefaultImageExtensions;
        var searchOption = recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var renames = new List<(string OriginalPath, string NewPath)>();

        foreach (var path in Directory.EnumerateFiles(directory, "*", searchOption))
        {
            var fileName = Path.GetFileName(path);

            var targetDirectory = Path.GetDirectoryName(path)!;
            var newPath = Path.Combine(targetDirectory, Sanitize(fileName));

            File.Move(path, newPath);
            renames.Add((path, newPath));
        }

        return renames;
    }
}
