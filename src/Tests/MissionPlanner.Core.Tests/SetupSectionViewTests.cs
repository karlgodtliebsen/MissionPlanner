using FluentAssertions;
using Microsoft.Maui.Controls;
using MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies the dedicated Mandatory Hardware workflow view composition.</summary>
public sealed class SetupSectionViewTests
{
    /// <summary>Gets every dedicated workflow view and its corresponding ViewModel type.</summary>
    public static TheoryData<Type, Type> SectionViewPairs => new()
    {
        { typeof(FirmwareSetupView), typeof(FirmwareSetupViewModel) },
        { typeof(FrameSetupView), typeof(FrameSetupViewModel) },
        { typeof(AccelerometerSetupView), typeof(AccelerometerSetupViewModel) },
        { typeof(CompassSetupView), typeof(CompassSetupViewModel) },
        { typeof(RadioSetupView), typeof(RadioSetupViewModel) },
        { typeof(FlightModesSetupView), typeof(FlightModesSetupViewModel) },
        { typeof(BatterySetupView), typeof(BatterySetupViewModel) },
        { typeof(EscMotorSetupView), typeof(EscMotorSetupViewModel) },
        { typeof(ServoOutputSetupView), typeof(ServoOutputSetupViewModel) },
        { typeof(OptionalHardwareSetupView), typeof(OptionalHardwareSetupViewModel) },
        { typeof(SafetySetupView), typeof(SafetySetupViewModel) },
        { typeof(SetupSummaryView), typeof(SetupSummaryViewModel) }
    };

    /// <summary>Verifies each workflow has a constructible ContentView paired by name with its section ViewModel.</summary>
    /// <param name="viewType">The dedicated section view type.</param>
    /// <param name="viewModelType">The corresponding workflow ViewModel type.</param>
    [Theory]
    [MemberData(nameof(SectionViewPairs))]
    public void WorkflowSectionHasDedicatedView(Type viewType, Type viewModelType)
    {
        typeof(ContentView).IsAssignableFrom(viewType).Should().BeTrue();
        typeof(SetupWorkflowDetailViewModel).IsAssignableFrom(viewModelType).Should().BeTrue();
        viewType.Name.Should().Be(viewModelType.Name.Replace("ViewModel", "View", StringComparison.Ordinal));
        viewType.GetConstructor(Type.EmptyTypes).Should().NotBeNull();
    }
}
