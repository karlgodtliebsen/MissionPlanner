using Avalonia;
using Avalonia.Headless;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;
using MissionPlanner.App.Views.Navigation;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.Shared.Models.Vehicles.Models;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

[Collection("Document rendering")]
public sealed class HardwareParityTests
{
    public static AppBuilder BuildAvaloniaApp() => HardwareRefreshTests.BuildAvaloniaApp();

    [Fact]
    public void BitChangesPreserveUnknownBitsAndDirtyState()
    {
        var row = new PeripheralSettingViewModel(new("ADSB_OPTIONS", "Options", 128, MavParamType.Int32, false, [])
        {
            Bits = new Dictionary<int, string> { [0] = "First", [2] = "Third" },
        }, _ => Task.CompletedTask);
        row.Bits[0].IsSelected = true;
        Assert.Equal(129, row.Value);
        Assert.True(row.IsDirty);
        row.AcceptValue(129);
        Assert.False(row.IsDirty);
        row.Bits[1].IsSelected = true;
        Assert.Equal(133, row.Value);
    }

    [Fact]
    public void ServoUsesOutputSpecificMetadataAndPartialCapabilities()
    {
        var row = new ServoOutputItemViewModel(new(20, 33, "Motor", false, 0, 0, 0, null, true, 800, 2200)
        {
            AvailableFields = new HashSet<string> { "FUNCTION" },
            FunctionOptions = [new(33, "Motor"), new(99, "Special")],
        }, [], _ => { });
        Assert.True(row.HasFunction);
        Assert.False(row.HasMinimum);
        Assert.False(row.HasLivePwm);
        Assert.Contains(row.Functions, option => option.Value == 99);
        Assert.Equal(20, row.Settings.ChannelNumber);
        Assert.Single(row.Settings.AvailableFields!);
    }

    [Fact]
    public async Task ApplyingOneRowPreservesOtherEditsAndBatchEnablesLast()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(HardwareParityTests));
        try
        {
            await session.Dispatch(async () =>
            {
                var active = Substitute.For<IActiveVehicleContext>();
                active.IsOnline.Returns(true);
                active.VehicleId.Returns(new VehicleId(1, 1));
                using var model = new TestModel(active);
                await model.ActivateAsync();
                model.Settings[0].NumericValue = 1;
                model.Settings[1].NumericValue = 12;
                model.Settings[2].NumericValue = 24;
                await model.Settings[1].ApplyCommand.ExecuteAsync(null);
                Assert.False(model.Settings[1].IsDirty);
                Assert.Equal(24, model.Settings[2].Value);
                Assert.True(model.Settings[2].IsDirty);
                await model.RefreshCommand.ExecuteAsync(null);
                Assert.Equal(24, model.Settings[2].Value);
                await model.ApplyModifiedCommand.ExecuteAsync(null);
                Assert.Equal(new[] { "ADSB_FIRST", "ADSB_SECOND", "ADSB_ENABLE" }, model.Writes);
                Assert.All(model.Settings, row => Assert.False(row.IsDirty));
                model.SearchText = "second";
                Assert.Equal("ADSB_SECOND", Assert.Single(model.VisibleSettings).Name);
                model.Settings[2].NumericValue = 25;
                model.FailedParameter = "ADSB_SECOND";
                await model.ApplyModifiedCommand.ExecuteAsync(null);
                Assert.True(model.Settings[2].IsDirty);
                Assert.Contains("Not confirmed", model.ErrorMessage);
                await model.DeactivateAsync();
            }, TestContext.Current.CancellationToken);
        }
        finally { await Task.Run(session.Dispose, CancellationToken.None); }
    }

    private sealed class TestModel(IActiveVehicleContext active) : MandatoryParameterViewModel(active,
        NullLogger.Instance, Substitute.For<INavigationService>())
    {
        public List<string> Writes { get; } = [];
        public string? FailedParameter { get; set; }
        private readonly Dictionary<string, double> values = new() { ["ADSB_ENABLE"] = 0, ["ADSB_FIRST"] = 0, ["ADSB_SECOND"] = 0 };

        protected override Task<MandatoryParameterConfiguration> LoadConfigurationAsync(VehicleId id, CancellationToken token)
            => Task.FromResult(new MandatoryParameterConfiguration(values.Select(pair => new PeripheralSetting(pair.Key, pair.Key, pair.Value, MavParamType.Int32, false, [])).ToArray(), []));

        protected override Task<MandatoryParameterApplyResult> ApplySettingAsync(VehicleId id, string name, double value, CancellationToken token)
        {
            if (name == FailedParameter)
            {
                return Task.FromResult(new MandatoryParameterApplyResult(false, "Not confirmed"));
            }
            Writes.Add(name);
            values[name] = value;
            return Task.FromResult(new MandatoryParameterApplyResult(true, "Confirmed"));
        }
    }
}
