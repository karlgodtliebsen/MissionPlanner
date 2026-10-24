using MissionPlanner.App.Views.ConfigTuning.Sections;
using MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.ConfigTuning.Tuning;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Firmware;
using MissionPlanner.Firmware.Model;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.Shared.Models.Vehicles.Models;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Verifies binding-driven materialization and metadata editors without a rendering backend.</summary>
public sealed class TuningAndPeripheralEditorTests
{
    /// <summary>The same property written by the expander creates rows; collapse/reopen retains pending state.</summary>
    [Fact]
    public void ExpanderPropertyMaterializesAndReusesRows()
    {
        var catalog = new ExtendedTuningProfileCatalog();
        var descriptor = catalog.GetProfile(FirmwareFamily.ArduCopter)!.Descriptors.First();
        var definition = catalog.Expand(descriptor).First();
        var name = definition.Parameter.ParameterNames.First();
        var session = Substitute.For<IParameterEditSession>();
        var identity = new VehicleFirmwareIdentity(FirmwareFamily.ArduCopter, 2, 3,
            new FirmwareSemanticVersion(4, 7, 0, FirmwareReleaseType.Official), null, 0, 0, 0, 0, null, null);
        session.Scope.Returns(new ParameterEditScope(new VehicleId(1, 1), identity));
        session.GetField(name).Returns(new ParameterEditField(name, MavParamType.Real32, 1, 1, 2, ParameterFieldMetadata.Empty, null));
        var service = Substitute.For<IExtendedTuningService>();
        service.CompareAxes(Arg.Any<ExtendedTuningWorkspace>(), Arg.Any<string>()).Returns(Array.Empty<AdvancedTuningComparisonValue>());
        var model = new ExtendedTuningGroupViewModel(new(descriptor, [new(definition, name)]), session, service,
            _ => Task.CompletedTask, _ => { }, _ => Task.CompletedTask, _ => { }, _ => Task.CompletedTask);
        Assert.Empty(model.Fields);
        model.IsExpanded = true;
        var first = Assert.Single(model.Fields);
        model.IsExpanded = false;
        Assert.Empty(model.Fields);
        model.IsExpanded = true;
        Assert.Same(first, Assert.Single(model.Fields));
        Assert.True(model.IsModified);
    }

    /// <summary>Unknown enumerations stay selected and named bit toggles preserve unknown flags.</summary>
    [Fact]
    public void UnknownEnumsAndBitsSurviveEditing()
    {
        var enumerated = new PeripheralSettingViewModel(new("MODE", "Mode", 77, MavParamType.Int32, false, [new(0, "Disabled")]), _ => Task.CompletedTask);
        Assert.Equal(77, enumerated.SelectedOption!.Value);
        Assert.Equal(77, enumerated.Value);
        var flags = new PeripheralSettingViewModel(new("OPTIONS", "Options", 128, MavParamType.Int32, false, [])
        {
            Bits = new Dictionary<int, string> { [0] = "Enabled" }
        }, _ => Task.CompletedTask);
        flags.Bits[0].IsSelected = true;
        Assert.Equal(129, flags.Value);
        flags.Bits[0].IsSelected = false;
        Assert.Equal(128, flags.Value);
    }

    /// <summary>Signed and fractional metadata ranges are preserved instead of imposing a zero minimum.</summary>
    [Fact]
    public void NumericEditorRetainsSignedRangeAndIncrement()
    {
        var model = new PeripheralSettingViewModel(new("OFFSET", "Offset", -2, MavParamType.Real32, false, [])
        {
            Minimum = -10, Maximum = 10, Increment = .01, Units = "m"
        }, _ => Task.CompletedTask);
        Assert.Equal(-10m, model.Minimum);
        Assert.Equal(.01m, model.Increment);
        Assert.Equal(-2, model.Value);
        Assert.True(model.IsNumericEditor);
    }
}
