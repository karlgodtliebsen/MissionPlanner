using System.Text;
using MissionPlanner.App.Models;
using MissionPlanner.App.Presentation;
using MissionPlanner.MavLink.Parameters;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Verifies consistent extensions at the shared desktop/browser picker boundary.</summary>
public sealed class PlanningFilePickerTests
{
    [Theory]
    [InlineData("ardupilot.params.csv", "csv")]
    [InlineData("parameter-comparison.csv", "csv")]
    [InlineData("ardupilot.params.json", "json")]
    [InlineData("mission.waypoints", "waypoints")]
    public void SaveOptionsSpecifyTheWrittenFormat(string name, string extension)
    {
        var options = PlanningFilePickerOptions.ForSave(name);
        Assert.Equal(name, options.SuggestedFileName);
        Assert.Equal(extension, options.DefaultExtension);
        var type = Assert.Single(options.FileTypeChoices!);
        Assert.Equal($"*.{extension}", Assert.Single(type.Patterns!));
        Assert.Contains($"*.{extension}", type.Name);
    }

    [Fact]
    public void ExtensionlessDownloadsAreNotMislabelled()
    {
        var options = PlanningFilePickerOptions.ForSave("README");
        Assert.Null(options.DefaultExtension);
        Assert.Null(options.FileTypeChoices);
    }

    [Fact]
    public async Task ParameterCsvExportCanBeReopenedAlongsideLegacyFiles()
    {
        var open = Substitute.For<IFileOpenService>();
        var save = Substitute.For<IFileSaveService>();
        string? savedName = null;
        byte[]? savedBytes = null;
        save.SaveAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            savedName = call.Arg<string>();
            using var output = new MemoryStream();
            call.Arg<Stream>().CopyTo(output);
            savedBytes = output.ToArray();
            return Task.FromResult<string?>(savedName);
        });
        var handler = new ParametersFileHandler(open, save);
        var parameter = new VehicleParameter("MAV_SYSID", 16, MavParamType.Int32, 0, 1);
        await handler.SaveParametersToFile([parameter], TestContext.Current.CancellationToken);
        Assert.Equal("ardupilot.params.csv", savedName);
        Assert.Contains("MAV_SYSID,16", Encoding.UTF8.GetString(savedBytes!));
        open.OpenAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var patterns = call.Arg<IReadOnlyList<string>>();
            Assert.Equal(new[] { "*.csv", "*.params", "*.param", "*.txt" }, patterns);
            var filters = PlanningFilePickerOptions.ForOpen("Parameters", patterns).FileTypeFilter!;
            Assert.Contains(filters, filter => filter.Name == "CSV files (*.csv)");
            return Task.FromResult<OpenedPlanningFile?>(new(savedName!, new MemoryStream(savedBytes!)));
        });
        var loaded = await handler.LoadParametersFromFileAsync([parameter with { Value = 1 }], TestContext.Current.CancellationToken);
        Assert.Equal(16, Assert.Single(loaded).Value);
    }
}
