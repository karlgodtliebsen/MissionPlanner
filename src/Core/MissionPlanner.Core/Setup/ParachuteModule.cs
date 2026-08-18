using MissionPlanner.MavLink.Parameters;
namespace MissionPlanner.Core.Setup;
/// <summary>Projects parachute configuration without deployment commands.</summary>
public sealed class ParachuteModule : IOptionalHardwareModule
{
    /// <inheritdoc />
    public string Key => "parachute";
    /// <inheritdoc />
    public string Title => "Parachute";
    /// <inheritdoc />
    public bool IsAvailable(IReadOnlyDictionary<string, VehicleParameter> parameters) => parameters.Keys.Any(name=>name.StartsWith("CHUTE_",StringComparison.Ordinal));
    /// <inheritdoc />
    public OptionalHardwareModuleView Build(IReadOnlyDictionary<string,VehicleParameter> parameters,IReadOnlyDictionary<string,ParameterMetadata> metadata) => OpticalFlowModule.BuildModule(Key,Title,parameters,metadata,"CHUTE_");
}
