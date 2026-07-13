using Microsoft.Extensions.Logging;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Models;
using MissionPlanner.Core.Services.Abstractions;
using MissionPlanner.Core.VehicleHandler.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.VehicleHandler;

/// <summary>
/// Handles PARAM_VALUE messages and updates the parameter registry accordingly.
/// </summary>
public sealed class ParamValueVehicleHandler(
    IVehicleParameterRegistry parameterRegistry,
    IDomainEventHub domainEventHub,
    ILogger<ParamValueVehicleHandler> logger)
    : IParamValueVehicleHandler
{
    /// <inheritdoc />
    public async Task Handle(ParamValueMessage message, CancellationToken cancellationToken)
    {
        if (logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace("ParamValueVehicleHandler - Handling PARAM_VALUE from vehicle {VehicleId}: {ParamId} = {Value} (type: {Type}, index: {Index}/{Count})",
                new VehicleId(message.SystemId, message.ComponentId),
                message.ParamId,
                message.ParamValue,
                message.ParamType,
                message.ParamIndex,
                message.ParamCount);
        }

        var vehicleId = new VehicleId(message.SystemId, message.ComponentId);

        var parameter = new VehicleParameter(
            message.ParamId,
            message.ParamValue,
            message.ParamType,
            message.ParamIndex,
            message.ParamCount);

        parameterRegistry.StoreParameter(vehicleId, parameter, cancellationToken);
        await domainEventHub.PublishDomainEventAsync(new VehicleParameterReceived(new VehicleParameterReceivedData(vehicleId, parameter)), cancellationToken);
    }
}
