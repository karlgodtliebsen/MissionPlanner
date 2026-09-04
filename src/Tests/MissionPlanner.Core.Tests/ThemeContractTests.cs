using System.Xml.Linq;
using Microsoft.Maui.Graphics;
using MissionPlanner.App.Theming;

namespace MissionPlanner.Core.Tests;

/// <summary>
/// Verifies that every registered application palette implements the semantic contract.
/// </summary>
public sealed class ThemeContractTests
{
    private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2009/xaml";

    /// <summary>
    /// Verifies registered palettes have matching, valid, unsuffixed color resources.
    /// </summary>
    [Fact]
    public void ConcreteThemesImplementCompleteColorContract()
    {
        var catalog = new ThemeCatalog();
        var expectedKeys = ThemeResourceKeys.RequiredColorKeys.ToHashSet(StringComparer.Ordinal);

        foreach (var descriptor in catalog.ConcreteThemes)
        {
            var document = XDocument.Load(GetThemePath(descriptor));
            var resources = document.Root!.Elements()
                .Select(element => new
                {
                    Element = element,
                    Key = (string?)element.Attribute(XamlNamespace + "Key")
                })
                .Where(resource => resource.Key is not null)
                .ToArray();

            var duplicates = resources
                .GroupBy(resource => resource.Key!, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();
            Assert.Empty(duplicates);

            var colors = resources
                .Where(resource => resource.Element.Name.LocalName == nameof(Color))
                .ToDictionary(resource => resource.Key!, resource => resource.Element.Value, StringComparer.Ordinal);

            Assert.Equal(expectedKeys, colors.Keys.ToHashSet(StringComparer.Ordinal));
            Assert.DoesNotContain(colors.Keys, key => key.EndsWith("Dark", StringComparison.Ordinal));

            foreach (var key in expectedKeys)
            {
                Assert.False(string.IsNullOrWhiteSpace(colors[key]));
                Assert.IsType<Color>(Color.FromArgb(colors[key]));
            }
        }
    }

    private static string GetThemePath(ThemeDescriptor descriptor)
    {
        var root = FindRepositoryRoot();
        return Path.Combine(root, "src", "UI", "MAUI", "MissionPlanner.App", descriptor.ResourcePath.Replace('/', Path.DirectorySeparatorChar));
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
