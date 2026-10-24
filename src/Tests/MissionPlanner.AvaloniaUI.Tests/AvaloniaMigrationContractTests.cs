using System.Globalization;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using MissionPlanner.Themes.Controls;
using MissionPlanner.App.Maps;
using MissionPlanner.App.Models;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.App.Views.ConfigTuning.Sections;
using MissionPlanner.App.Views.ConfigTuning.Sections.ParametersEditor;
using MissionPlanner.App.Views.FlightData.Tabs;
using MissionPlanner.App.Views.InitSetup.InstallFirmware;
using MissionPlanner.App.Views.InitSetup.InstallFirmware.SubViews.Models;
using MissionPlanner.App.Views.Missions;
using MissionPlanner.App.Views.Navigation;


namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Protects the Avalonia migration boundaries formerly covered by UI-coupled Core tests.</summary>
public sealed class AvaloniaMigrationContractTests
{
    /// <summary>Verifies the Core test project has no UI project reference.</summary>
    [Fact]
    public void CoreTestsDoNotReferenceTheAvaloniaApplication()
    {
        var project = XDocument.Load(RepositoryPath(
            "src", "Tests", "MissionPlanner.Core.Tests", "MissionPlanner.Core.Tests.csproj"));
        var references = project.Descendants("ProjectReference")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(value => value is not null)
            .ToArray();

        Assert.DoesNotContain(references, value =>
            value!.Contains("MissionPlanner.App", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Verifies migrated feature ViewModels are present in the Avalonia assembly.</summary>
    [Fact]
    public void MigratedFeatureViewModelsAreAvailable()
    {
        Type[] viewModels =
        [
            typeof(BasicTuningTabViewModel),
            typeof(ExtendedTuningTabViewModel),
            typeof(ParametersEditorTabViewModel),
            typeof(GeoFenceTabViewModel),
            typeof(OnboardOsdTabViewModel),
            typeof(MavFtpTabViewModel),
            typeof(ActionsTabViewModel),
            typeof(MessagesTabViewModel),
            typeof(InstallFirmwareViewModel)
        ];

        Assert.All(viewModels, type =>
        {
            Assert.False(type.IsAbstract);
            Assert.True(typeof(ViewModelBase).IsAssignableFrom(type));
        });
    }

    /// <summary>Verifies migrated ViewModels use current UI boundary abstractions.</summary>
    [Fact]
    public void ViewModelConstructorsUseAvaloniaBoundaries()
    {
        Type[] boundaryTypes =
        [
            typeof(IFileOpenService),
            typeof(IFileSaveService),
            typeof(IUserConfirmationService),
            typeof(IDialogService)
        ];

        var constructors = typeof(ParametersEditorTabViewModel).Assembly
            .GetTypes()
            .Where(type => typeof(ViewModelBase).IsAssignableFrom(type))
            .SelectMany(type => type.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        Assert.All(boundaryTypes, boundary => Assert.Contains(boundary, constructors));
        Assert.DoesNotContain(constructors, type =>
            type.Namespace?.Contains("Maui", StringComparison.OrdinalIgnoreCase) == true);
    }

    /// <summary>Verifies map, firmware, file, navigation, and virtual-grid adapters are migrated.</summary>
    [Fact]
    public void AvaloniaIntegrationTypesArePresent()
    {
        Type[] integrationTypes =
        [
            typeof(MapBasemapController),
            typeof(CompositeMapsuiBasemapFactory),
            typeof(MissionMapProjection),
            typeof(ParametersFileHandler),
            typeof(AvaloniaFirmwareFilePicker),
            typeof(AvaloniaNavigationService),
            typeof(NavigationPageFactory),
            typeof(VirtualizedItemsGrid)
        ];

        Assert.All(integrationTypes, type => Assert.NotNull(type.AssemblyQualifiedName));
    }

    /// <summary>Verifies representative migrated pages contain complete current controls.</summary>
    [Theory]
    [InlineData("Views/ConfigTuning/Sections/ParametersEditor/ParametersMainEditorView.axaml", "VirtualizedItemsGrid")]
    [InlineData("Views/InitSetup/InstallFirmware/FirmwareCatalogueView.axaml", "VirtualizedItemsGrid")]
    [InlineData("Views/Navigation/MainShellView.axaml", "u:NavMenu")]
    [InlineData("Views/FlightData/FlightDataPage.axaml", "FlightDataMissionMapView")]
    [InlineData("Views/FlightPlanner/FlightPlannerPage.axaml", "MissionMapView")]
    public void RepresentativeAxamlUsesMigratedControl(string relativePath, string expectedControl)
    {
        var pathParts = new[] { "src", "UI", "MissionPlanner.App" }
            .Concat(relativePath.Split('/'))
            .ToArray();
        var content = File.ReadAllText(RepositoryPath(pathParts));

        Assert.Contains(expectedControl, content, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.Maui", content, StringComparison.Ordinal);
        Assert.DoesNotContain("UraniumUI", content, StringComparison.Ordinal);
    }

    [Fact]
    public void FirmwareNavigationSeparatesMechanismAndSourceWithoutApjDfuComposition()
    {
        var path = RepositoryPath("src", "UI", "MissionPlanner.App", "Views", "InitSetup", "InstallFirmware");
        var page = System.Xml.Linq.XDocument.Load(Path.Combine(path, "InstallFirmwarePage.axaml"));
        System.Xml.Linq.XNamespace ui = "https://github.com/avaloniaui";
        var tabs = page.Descendants(ui + "TabControl").First();
        var top = tabs.Elements(ui + "TabItem").ToArray();
        Assert.Equal(new[] { "Information", "Configuration" }, top.Select(item => (string?)item.Attribute("Header")));
        Assert.Single(page.Descendants(ui + "TabControl"));
        var landing = XDocument.Load(Path.Combine(path, "LandingView.axaml"));
        Assert.Empty(landing.Descendants(ui + "Button"));
        var firmware = top[1].ToString();
        Assert.Contains("SelectedArtifact.Source", firmware);
        Assert.Contains("TargetCompatibilityText", firmware);
        Assert.Contains("HasCompatibilityFailure", firmware);
        Assert.Contains("CurrentPlan.CanExecute", firmware);
        Assert.DoesNotContain("IsConnectedMode", firmware);
        Assert.DoesNotContain("CustomFirmwareView", firmware);
        Assert.DoesNotContain("ValidatedPackageView", firmware);
    }

    /// <summary>Guards the first-connect MAVFTP refresh against self-cancelling initialization.</summary>
    [Fact]
    public void MavFtpConnectionHandlerCreatesItsTokenAfterResettingThePreviousOperation()
    {
        var source = File.ReadAllText(RepositoryPath(
            "src", "UI", "MissionPlanner.App", "Views",
            "ConfigTuning", "Sections", "MAVFtpTabViewModel.cs"));
        var handlerStart = source.IndexOf("private async Task OnVehicleConnected", StringComparison.Ordinal);
        var handlerEnd = source.IndexOf("private async Task Start", handlerStart, StringComparison.Ordinal);
        var handler = source[handlerStart..handlerEnd];

        var reset = handler.IndexOf("await ResetFilesystemService", StringComparison.Ordinal);
        var tokenCreation = handler.IndexOf(
            "CancellationTokenSource.CreateLinkedTokenSource",
            StringComparison.Ordinal);
        var refresh = handler.IndexOf("await Start()", StringComparison.Ordinal);

        Assert.True(reset >= 0, "The handler must reset the previous MAVFTP connection.");
        Assert.True(tokenCreation > reset, "The new connection token must not be cancelled by reset.");
        Assert.True(refresh > tokenCreation, "The root-directory refresh must follow initialization.");
    }

    /// <summary>Rejects numeric literals that compile into a failing Decimal.Parse call when a view opens.</summary>
    [Fact]
    public void NumericUpDownDecimalLiteralsCanBeParsedWhenViewsLoad()
    {
        var root = RepositoryPath("src", "UI", "MissionPlanner.App");
        XNamespace ui = "https://github.com/avaloniaui";
        var invalid = new List<string>();
        var literalCount = 0;
        foreach (var path in Directory.EnumerateFiles(root, "*.axaml", SearchOption.AllDirectories))
        {
            var document = XDocument.Parse(File.ReadAllText(path), LoadOptions.SetLineInfo);
            foreach (var attribute in document.Descendants(ui + "NumericUpDown")
                .Attributes().Where(attribute => attribute.Name.LocalName is "Value" or "Minimum" or "Maximum" or "Increment"))
            {
                if (attribute.Value.TrimStart().StartsWith('{'))
                {
                    continue;
                }

                literalCount++;
                // Avalonia's compiled decimal literals use Decimal.Parse(string, IFormatProvider),
                // whose default NumberStyles.Number does not allow exponent notation.
                if (!decimal.TryParse(attribute.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
                {
                    var line = ((IXmlLineInfo)attribute).LineNumber;
                    invalid.Add($"{Path.GetRelativePath(root, path)}:{line}: {attribute.Name}={attribute.Value}");
                }
            }
        }

        Assert.True(literalCount > 0, "The scan must inspect application NumericUpDown literals.");
        Assert.True(invalid.Count == 0, "Decimal literals must use plain decimal notation:\n" + string.Join("\n", invalid));
    }

    private static string RepositoryPath(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine([directory.FullName, .. parts]);
    }
}
