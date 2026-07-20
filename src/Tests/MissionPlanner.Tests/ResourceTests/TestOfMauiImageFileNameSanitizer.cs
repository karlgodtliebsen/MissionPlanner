using FluentAssertions;

namespace MissionPlanner.Tests.ResourceTests;

/// <summary>
/// Provides the public API for TestOfMauiImageFileNameSanitizer.
/// </summary>
public class TestOfMauiImageFileNameSanitizer(ITestOutputHelper output)
{
    /// <summary>
    /// Run this test to fix the app's resource images in place. It prints every rename so the
    /// old names can be located and updated in XAML/C# references afterwards.
    /// </summary>
    [Theory]
    [InlineData(@"c:\projects\MissionPlanner\src\UI\MissionPlanner.App\Resources\Images")]
    public void RenameInvalidResourceImages(string relativeDirectory)
    {
        var directory = Path.Combine(FindRepositoryRoot(), relativeDirectory);

        var renames = MauiImageFileNameSanitizer.RenameInvalidFiles(directory);

        output.WriteLine($"Renamed {renames.Count} file(s) in {directory}:");
        foreach (var (originalPath, newPath) in renames)
        {
            output.WriteLine($"  {Path.GetFileName(originalPath)} -> {Path.GetFileName(newPath)}");
        }

        Directory.EnumerateFiles(directory)
            .Where(f => Path.GetExtension(f) is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".svg" or ".webp")
            .Where(f => !MauiImageFileNameSanitizer.IsValid(Path.GetFileName(f)))
            .Should().BeEmpty();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new DirectoryNotFoundException("Repository root (.git) not found above " + AppContext.BaseDirectory);
    }
}
