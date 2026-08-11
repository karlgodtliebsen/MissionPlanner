namespace MissionPlanner.Core.FlightData.Auxiliary;

/// <summary>Executes typed, acknowledged auxiliary-function commands.</summary>
public interface IAuxiliaryFunctionService
{
    /// <summary>Executes one request against its current vehicle.</summary>
    Task<AuxiliaryFunctionResult> ExecuteAsync(AuxiliaryFunctionRequest request, CancellationToken cancellationToken);
}
