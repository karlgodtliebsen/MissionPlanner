using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Configuration;
using MissionPlanner.App.Views.Missions;
using MissionPlanner.App.Views.Missions.Workflows;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Verifies the production registrations cover the mission editor and every dependency of its collaborators.</summary>
public sealed class MissionEditorCompositionTests
{
    private static ServiceCollection BuildRegistrations()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false)
            .Build();
        var services = new ServiceCollection();
        services.AddApplicationConfiguration(configuration);
        services.AddLogging();
        return services;
    }

    public static TheoryData<Type, Type> Collaborators => new()
    {
        { typeof(IMissionRowProjector), typeof(MissionRowProjector) },
        { typeof(IMissionFileInteraction), typeof(MissionFileInteraction) },
        { typeof(IMissionMapInteractionCoordinator), typeof(MissionMapInteractionCoordinator) },
        { typeof(IMissionRowEditor), typeof(MissionRowEditor) },
        { typeof(IMissionFileWorkflow), typeof(MissionFileWorkflow) },
        { typeof(IMissionEditingWorkflow), typeof(MissionEditingWorkflow) },
        { typeof(IVehiclePlanWorkflow), typeof(VehiclePlanWorkflow) },
        { typeof(IPolygonPlanningWorkflow), typeof(PolygonPlanningWorkflow) },
        { typeof(IGeneratedMissionWorkflow), typeof(GeneratedMissionWorkflow) },
        { typeof(IMapToolsWorkflow), typeof(MapToolsWorkflow) }
    };

    [Theory]
    [MemberData(nameof(Collaborators))]
    public void Collaborator_IsRegisteredAsTransient_WithEveryConstructorDependencyRegistered(Type serviceType, Type implementationType)
    {
        var services = BuildRegistrations();

        var descriptor = services.Should().ContainSingle(d => d.ServiceType == serviceType).Subject;
        descriptor.ImplementationType.Should().Be(implementationType);
        descriptor.Lifetime.Should().Be(ServiceLifetime.Transient, "stateful collaborators must not be shared between the two editors");

        var constructor = implementationType.GetConstructors().Single();
        foreach (var parameter in constructor.GetParameters())
        {
            var parameterType = parameter.ParameterType;
            var registered = services.Any(d => d.ServiceType == parameterType)
                || (parameterType.IsGenericType && services.Any(d => d.ServiceType == parameterType.GetGenericTypeDefinition()))
                || (parameterType.IsGenericType && parameterType.GetGenericTypeDefinition() == typeof(ILogger<>));
            registered.Should().BeTrue($"{implementationType.Name} needs {parameterType.Name}");
        }
    }
}
