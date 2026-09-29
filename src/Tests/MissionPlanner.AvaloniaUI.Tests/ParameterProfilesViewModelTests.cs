using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Views.ConfigTuning.Sections.ParametersEditor;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.ConfigTuning.Comparison;
using MissionPlanner.Core.ConfigTuning.Profiles;
using MissionPlanner.Firmware;
using MissionPlanner.Firmware.Model;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.Shared.Models.Vehicles.Models;
using NSubstitute;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Exercises local profile creation and review without controller writes.</summary>
[Collection("Document rendering")]
public sealed class ParameterProfilesViewModelTests
{
    /// <summary>Creates an offline renderer with production styles.</summary>
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure(() =>
        new MissionPlanner.App.App(new ServiceCollection().AddLogging().BuildServiceProvider()))
        .UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

    /// <summary>The profile browser and comparison render with compiled bindings.</summary>
    [Fact]
    public async Task ProfileDialogRenders()
    {
        var renderer = HeadlessUnitTestSession.StartNew(typeof(ParameterProfilesViewModelTests));
        try
        {
            await renderer.Dispatch(() =>
            {
                using var fixture = new Fixture();
                var profile = fixture.Workflow.Create(fixture.Session, "Camera setup");
                fixture.Model.Profiles.Add(profile);
                fixture.Model.SelectedProfile = profile;
                var view = new ParameterProfilesView { DataContext = fixture.Model };
                var window = new Window { Content = view, Width = 900, Height = 850 };
                window.Show();
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "GAIN");
                Assert.True(view.GetVisualDescendants().OfType<ListBox>().Single().Bounds.Height > 100);
                var directory = Environment.GetEnvironmentVariable("MISSIONPLANNER_VISUAL_TEST_OUTPUT");
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                    using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(900, 850));
                    bitmap.Render(window);
                    bitmap.Save(Path.Combine(directory, "parameter-profiles.png"));
                }
                window.Close();
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            await Task.Run(renderer.Dispose);
        }
    }

    /// <summary>Imports never overwrite an existing ID; export and deletion operate on explicit selection.</summary>
    [Fact]
    public async Task ImportExportAndDeleteAreLocalAndExplicit()
    {
        using var fixture = new Fixture();
        var profile = fixture.Workflow.Create(fixture.Session, "Backup");
        await fixture.Repository.SaveAsync(profile);
        fixture.Model.SelectedProfile = profile;
        byte[]? exported = null;
        fixture.Exports.SaveAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            Assert.EndsWith(".json", call.Arg<string>());
            using var copy = new MemoryStream();
            call.Arg<Stream>().CopyTo(copy);
            exported = copy.ToArray();
            return Task.FromResult<string?>("backup.json");
        });
        await fixture.Model.ExportCommand.ExecuteAsync(null);
        Assert.NotNull(exported);
        fixture.Files.OpenAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new OpenedPlanningFile("backup.json", new MemoryStream(exported)));
        await fixture.Model.ImportCommand.ExecuteAsync(null);
        Assert.Equal(2, fixture.Model.Profiles.Count);
        Assert.NotEqual(profile.Id, fixture.Model.SelectedProfile!.Id);
        fixture.Confirmation.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        await fixture.Model.DeleteCommand.ExecuteAsync(null);
        Assert.Equal(2, (await fixture.Repository.GetAllAsync()).Count);
        fixture.Confirmation.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        await fixture.Model.DeleteCommand.ExecuteAsync(null);
        Assert.Equal(profile.Id, Assert.Single(await fixture.Repository.GetAllAsync()).Id);
        AssertNoWrites(fixture.Session);
    }
    /// <summary>Each creation scope persists the intended values and metadata.</summary>
    [Theory]
    [InlineData("All parameters", 2)]
    [InlineData("Modified parameters", 1)]
    [InlineData("Selected names", 1)]
    public async Task CreatesAndReloadsNamedProfiles(string scope, int count)
    {
        using var fixture = new Fixture();
        var model = fixture.Model;
        await model.InitializeAsync();
        Assert.Empty(model.Profiles);
        model.ProfileName = "Camera setup";
        model.Description = "Reusable settings";
        model.Tags = "camera, test, camera";
        model.Scope = scope;
        model.SelectedNames = "GAIN";
        await model.CreateCommand.ExecuteAsync(null);
        var saved = Assert.Single(await fixture.Repository.GetAllAsync());
        Assert.Equal(count, saved.Values.Count);
        Assert.Equal(2, saved.Values["GAIN"]);
        Assert.Equal("Reusable settings", saved.Description);
        Assert.Equal(new[] { "camera", "test" }, saved.Tags);
        Assert.Equal(FirmwareFamily.ArduCopter, saved.FirmwareFamily);
        Assert.Equal(saved.Id, model.SelectedProfile!.Id);
        await model.DuplicateCommand.ExecuteAsync(null);
        Assert.Equal(2, model.Profiles.Count);
        model.ProfileName = "Camera copy";
        await model.SaveDetailsCommand.ExecuteAsync(null);
        Assert.Equal("Camera copy", model.SelectedProfile!.Name);
        model.SelectedProfile = model.Profiles.Single(item => item.Id == saved.Id);
        Assert.Equal("Camera setup", model.ProfileName);
        AssertNoWrites(fixture.Session);
    }

    /// <summary>Bad input is reported without empty or silently incomplete profiles.</summary>
    [Fact]
    public async Task RejectsUnknownSubsetAndEmptyName()
    {
        using var fixture = new Fixture();
        fixture.Model.ProfileName = "Subset";
        fixture.Model.Scope = "Selected names";
        fixture.Model.SelectedNames = "GAIN,UNKNOWN";
        await fixture.Model.CreateCommand.ExecuteAsync(null);
        Assert.Contains("UNKNOWN", fixture.Model.Feedback);
        Assert.Empty(await fixture.Repository.GetAllAsync());
        fixture.Model.Scope = "All parameters";
        fixture.Model.ProfileName = " ";
        await fixture.Model.CreateCommand.ExecuteAsync(null);
        Assert.Contains("failed", fixture.Model.Feedback);
        Assert.Empty(await fixture.Repository.GetAllAsync());
    }

    /// <summary>Mismatch and missing parameters remain visible, and only explicitly selected values stage.</summary>
    [Fact]
    public async Task ReviewAndStageRequireSelectionAndDoNotWrite()
    {
        using var fixture = new Fixture();
        var profile = fixture.Workflow.Create(fixture.Session, "Plane settings") with
        {
            FirmwareFamily = FirmwareFamily.ArduPlane,
            Values = new Dictionary<string, double> { ["GAIN"] = 3, ["UNKNOWN"] = 7 }
        };
        await fixture.Repository.SaveAsync(profile);
        await fixture.Model.InitializeAsync();
        fixture.Model.SelectedProfile = Assert.Single(fixture.Model.Profiles);
        Assert.Contains("does not match", fixture.Model.ReviewText);
        Assert.False(fixture.Model.Rows.Single(row => row.Name == "UNKNOWN").CanStage);
        Assert.DoesNotContain(fixture.Session.ReceivedCalls(), call => call.GetMethodInfo().Name == "TrySetPending");
        await fixture.Model.StageCommand.ExecuteAsync(null);
        Assert.Contains("Select compatible", fixture.Model.Feedback);
        fixture.Model.SelectSafeCommand.Execute(null);
        fixture.Session.TrySetPending("GAIN", 3, out Arg.Any<string?>()).Returns(true);
        await fixture.Model.StageCommand.ExecuteAsync(null);
        Assert.Contains("Staged 1 of 1", fixture.Model.Feedback);
        fixture.Session.Received(1).TrySetPending("GAIN", 3, out Arg.Any<string?>());
        AssertNoWrites(fixture.Session);
    }

    /// <summary>A vehicle change during confirmation cannot stage into the old session.</summary>
    [Fact]
    public async Task InvalidatedSessionDuringConfirmationDoesNotStage()
    {
        using var fixture = new Fixture();
        fixture.Model.SelectedProfile = fixture.Workflow.Create(fixture.Session, "Settings");
        fixture.Model.SelectSafeCommand.Execute(null);
        fixture.Confirmation.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                fixture.Session.IsValid.Returns(false);
                return Task.FromResult(true);
            });
        await fixture.Model.StageCommand.ExecuteAsync(null);
        Assert.Contains("session changed", fixture.Model.Feedback);
        Assert.DoesNotContain(fixture.Session.ReceivedCalls(), call => call.GetMethodInfo().Name == "TrySetPending");
        AssertNoWrites(fixture.Session);
    }

    private static void AssertNoWrites(IParameterEditSession session) =>
        Assert.DoesNotContain(session.ReceivedCalls(), call => call.GetMethodInfo().Name is "ApplyAsync" or "RetryFailedAsync");

    private sealed class Fixture : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "MissionPlanner.ProfileTests", Guid.NewGuid().ToString("N"));
        internal IParameterEditSession Session { get; } = Substitute.For<IParameterEditSession>();
        internal IUserConfirmationService Confirmation { get; } = Substitute.For<IUserConfirmationService>();
        internal IFileOpenService Files { get; } = Substitute.For<IFileOpenService>();
        internal IFileSaveService Exports { get; } = Substitute.For<IFileSaveService>();
        internal IParameterProfileService Workflow { get; } = new ParameterProfileService(new ParameterComparisonService(new ParameterValueEquivalence()));
        internal IParameterProfileRepository Repository { get; }
        internal ParameterProfilesViewModel Model { get; }

        internal Fixture()
        {
            Repository = new JsonParameterProfileRepository(Options.Create(new ParameterProfileRepositoryOptions { Directory = directory }));
            var firmware = new VehicleFirmwareIdentity(FirmwareFamily.ArduCopter, 2, 3,
                new FirmwareSemanticVersion(4, 6, 0, FirmwareReleaseType.Official), null, 0, 0, 0, 0, null, null);
            var id = new VehicleId(1, 1);
            Session.Scope.Returns(new ParameterEditScope(id, firmware));
            Session.VehicleId.Returns(id);
            Session.IsValid.Returns(true);
            var metadata = new ParameterFieldMetadata("Gain", null, null, null, null, 0.1, false, false, [], []);
            ParameterEditField[] fields = [new("GAIN", MavParamType.Real32, 1, 1, 2, metadata, null), new("OTHER", MavParamType.Real32, 1, 1, 1, metadata, null)];
            Session.Fields.Returns(fields);
            Session.GetField(Arg.Any<string>()).Returns(call => fields.FirstOrDefault(field => field.Name == call.Arg<string>()));
            Confirmation.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
            Model = new ParameterProfilesViewModel(Repository, Workflow, Confirmation, Files, Exports, Session,
                NullLogger<ParameterProfilesViewModel>.Instance, Substitute.For<IUiDispatcher>(), Substitute.For<IDomainEventHub>());
        }

        public void Dispose()
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
