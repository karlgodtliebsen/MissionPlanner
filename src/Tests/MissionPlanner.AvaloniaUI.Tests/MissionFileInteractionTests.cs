using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.App.Views.Missions;
using MissionPlanner.Core.Missions;
using MissionPlanner.Core.Missions.Files;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Missions.Planning;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Verifies the file pickers, save formats and prompts behind the mission editor.</summary>
public sealed class MissionFileInteractionTests
{
    private readonly IFileOpenService openService = Substitute.For<IFileOpenService>();
    private readonly IFileSaveService saveService = Substitute.For<IFileSaveService>();
    private readonly IDialogService dialogService = Substitute.For<IDialogService>();
    private readonly IUserConfirmationService confirmation = Substitute.For<IUserConfirmationService>();
    private readonly IGeospatialImportService geospatial = Substitute.For<IGeospatialImportService>();
    private readonly MissionFileCodec codec = new(new MissionProtocolMapper());
    private readonly MissionFileInteraction interaction;

    public MissionFileInteractionTests()
    {
        interaction = new MissionFileInteraction(openService, saveService, dialogService, confirmation, codec, geospatial,
            NullLogger<MissionFileInteraction>.Instance);
    }

    private static Mission MissionWithOneWaypoint()
    {
        var mission = new Mission(MissionId.New(), "Survey");
        mission.Add(new WaypointMissionItem(MissionItemId.New(), 0, new GeoPosition(56, 10),
            new MissionAltitude(80, MissionAltitudeReference.Home), TimeSpan.Zero, 10));
        return mission;
    }

    private static OpenedPlanningFile TextFile(string name, string text, string? fullPath = null)
    {
        return new OpenedPlanningFile(name, new MemoryStream(Encoding.UTF8.GetBytes(text)), fullPath);
    }

    [Fact]
    public async Task SaveMission_EmptyMission_ReportsEmptyWithoutPrompting()
    {
        var result = await interaction.SaveMissionAsync(new Mission(MissionId.New(), "Empty"), null, CancellationToken.None);

        result.Message.Should().Be("Mission is empty.");
        result.ClearDirtyRows.Should().BeFalse();
        await dialogService.DidNotReceiveWithAnyArgs().ChooseAsync(default!, default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SaveMission_FormatPickerCancelled_IsSilent()
    {
        dialogService.ChooseAsync(Arg.Any<Ursa.Controls.OverlayDialogOptions>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);

        var result = await interaction.SaveMissionAsync(MissionWithOneWaypoint(), null, CancellationToken.None);

        result.Should().Be(new MissionSaveResult(null, false));
        await saveService.DidNotReceiveWithAnyArgs().SaveAsync(default!, default!, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("Waypoints (.waypoints)", "Survey.waypoints")]
    [InlineData("Text (.txt)", "Survey.txt")]
    [InlineData("Mission JSON (.mission)", "Survey.mission")]
    public async Task SaveMission_WritesTheChosenFormatUnderTheMissionName(string choice, string expectedFileName)
    {
        dialogService.ChooseAsync(Arg.Any<Ursa.Controls.OverlayDialogOptions>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(choice);
        string? written = null;
        saveService.SaveAsync(expectedFileName, Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                written = new StreamReader(call.ArgAt<Stream>(1)).ReadToEnd();
                return Task.FromResult<string?>($"C:\\out\\{expectedFileName}");
            });

        var result = await interaction.SaveMissionAsync(MissionWithOneWaypoint(), null, CancellationToken.None);

        result.Message.Should().Be($"Mission saved to C:\\out\\{expectedFileName}.");
        result.ClearDirtyRows.Should().BeTrue();
        written.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SaveMission_PickerReturnsNoPath_ReportsCancelled()
    {
        dialogService.ChooseAsync(Arg.Any<Ursa.Controls.OverlayDialogOptions>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns("Text (.txt)");
        saveService.SaveAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns((string?)null);

        var result = await interaction.SaveMissionAsync(MissionWithOneWaypoint(), null, CancellationToken.None);

        result.Should().Be(new MissionSaveResult("Save cancelled.", true));
    }

    [Fact]
    public async Task SaveMission_WriterThrows_ReportsFailure()
    {
        dialogService.ChooseAsync(Arg.Any<Ursa.Controls.OverlayDialogOptions>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns("Text (.txt)");
        saveService.SaveAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns<Task<string?>>(_ => throw new IOException("disk full"));

        var result = await interaction.SaveMissionAsync(MissionWithOneWaypoint(), null, CancellationToken.None);

        result.Should().Be(new MissionSaveResult("Save failed: disk full", true));
    }

    [Fact]
    public async Task LoadMission_PickerCancelled_ReturnsNeitherContentNorMessage()
    {
        openService.OpenAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<CancellationToken>())
            .Returns((OpenedPlanningFile?)null);

        var result = await interaction.LoadMissionAsync(CancellationToken.None);

        result.Content.Should().BeNull();
        result.Message.Should().BeNull();
    }

    [Fact]
    public async Task LoadMission_ParsesTheFileAndBuildsTheSuccessMessage()
    {
        var content = codec.Build(MissionWithOneWaypoint(), new GeoPosition(56, 10), MissionFileFormat.QgcWpl);
        openService.OpenAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<CancellationToken>())
            .Returns(TextFile("plan.waypoints", content));

        var result = await interaction.LoadMissionAsync(CancellationToken.None);

        result.Message.Should().BeNull();
        result.Content!.Items.Should().HaveCount(1);
        result.SuccessMessage.Should().Contain("from plan.waypoints");
    }

    [Fact]
    public async Task LoadMission_UnparsableFile_ReportsFailure()
    {
        openService.OpenAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<CancellationToken>())
            .Returns(TextFile("bad.mission", "{ not a mission"));

        var result = await interaction.LoadMissionAsync(CancellationToken.None);

        result.Content.Should().BeNull();
        result.Message.Should().StartWith("Load failed:");
    }

    [Fact]
    public void LoadResult_SuccessMessage_MentionsSkippedItems()
    {
        var result = new MissionLoadResult(new MissionFileContent([], null, 2), "a.txt", null);

        result.SuccessMessage.Should().Be("Loaded 0 items from a.txt; skipped 2 unsupported.");
    }

    [Fact]
    public async Task SaveText_WritesUtf8ContentThroughTheSaveService()
    {
        string? written = null;
        saveService.SaveAsync("fence.mpfence", Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                written = new StreamReader(call.ArgAt<Stream>(1), Encoding.UTF8).ReadToEnd();
                return Task.FromResult<string?>("C:\\fence.mpfence");
            });

        var path = await interaction.SaveTextAsync("fence.mpfence", "æøå", CancellationToken.None);

        path.Should().Be("C:\\fence.mpfence");
        written.Should().Be("æøå");
    }

    [Fact]
    public async Task OpenText_ReturnsFileNameAndText()
    {
        openService.OpenAsync("Open it", Arg.Any<IReadOnlyList<string>?>(), Arg.Any<CancellationToken>())
            .Returns(TextFile("poly.mppolygon", "payload"));

        var opened = await interaction.OpenTextAsync("Open it", CancellationToken.None);

        opened.Should().Be(new OpenedText("poly.mppolygon", "payload"));
    }

    [Fact]
    public async Task OpenText_PickerCancelled_ReturnsNull()
    {
        openService.OpenAsync("Open it", Arg.Any<IReadOnlyList<string>?>(), Arg.Any<CancellationToken>())
            .Returns((OpenedPlanningFile?)null);

        (await interaction.OpenTextAsync("Open it", CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task ImportGeospatial_KmlNeedsNoCompanionsOrPrompt()
    {
        var imported = new GeospatialImportResult(true, "ok", [], new GeospatialImportPreview(0, 0, 0, 0, 0));
        openService.OpenAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<CancellationToken>())
            .Returns(TextFile("area.kml", "<kml/>"));
        geospatial.Import(Arg.Any<GeospatialSource>()).Returns(imported);

        var result = await interaction.ImportGeospatialAsync("Open KML", false, CancellationToken.None);

        result.Imported.Should().BeSameAs(imported);
        await confirmation.DidNotReceiveWithAnyArgs().ConfirmAsync(default!, default!, default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ImportGeospatial_ShapefileWithoutProjection_AsksBeforeAssumingWgs84()
    {
        openService.OpenAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<CancellationToken>())
            .Returns(TextFile("area.shp", "shp", Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.shp")));
        confirmation.ConfirmAsync("Missing coordinate system", Arg.Any<string>(), "Use WGS84", Arg.Any<CancellationToken>()).Returns(false);

        var result = await interaction.ImportGeospatialAsync("Open shapefile", true, CancellationToken.None);

        result.Should().Be(new GeospatialFileResult(null, null));
        geospatial.DidNotReceiveWithAnyArgs().Import(default!);
    }

    [Fact]
    public async Task ImportGeospatial_AcceptedWgs84_PassesAProjectionCompanion()
    {
        GeospatialSource? seen = null;
        openService.OpenAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<CancellationToken>())
            .Returns(TextFile("area.shp", "shp", Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.shp")));
        confirmation.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        geospatial.Import(Arg.Do<GeospatialSource>(source => seen = source))
            .Returns(new GeospatialImportResult(false, "Unsupported geometry", [], new GeospatialImportPreview(0, 0, 0, 0, 0)));

        var result = await interaction.ImportGeospatialAsync("Open shapefile", true, CancellationToken.None);

        seen!.Companions.Should().ContainKey(".prj");
        result.Should().Be(new GeospatialFileResult(null, "Unsupported geometry"));
    }
}
