using CommunityToolkit.Mvvm.Input;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Firmware;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.App.Views.ConfigTuning.Sections.ParametersEditor;

public partial class ParametersEditorTabViewModel
{
    [RelayCommand(IncludeCancelCommand = true)]
    private async Task RefreshMetadataAsync(CancellationToken cancellationToken)
    {
        if (IsBusy || !activeVehicle.IsOnline || activeVehicle.VehicleId is not { } id || EditSession is not { } session)
        {
            SetMessages("Connect a vehicle and load its parameters first.");
            return;
        }
        if (ModifiedParameterCount > 0)
        {
            SetMessages("Apply or discard pending edits before refreshing metadata.");
            return;
        }
        VehicleType? type = activeVehicle.State?.Identity.Firmware.Family switch
        {
            FirmwareFamily.ArduCopter => VehicleType.ArduCopter,
            FirmwareFamily.ArduPlane => VehicleType.ArduPlane,
            FirmwareFamily.Rover => VehicleType.Rover,
            FirmwareFamily.ArduSub => VehicleType.ArduSub,
            FirmwareFamily.AntennaTracker => VehicleType.AntennaTracker,
            _ => null
        };
        if (type is null)
        {
            SetMessages("No maintained parameter metadata feed is mapped to this firmware family.");
            return;
        }
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, activeVehicle.ConnectionCancellationToken);
        SetBusy();
        try
        {
            SetMessages($"Refreshing {type} metadata from the configured provider…");
            var metadata = domainFactory.Create<IVehicleParameterMetadataService>();
            await metadata.RefreshMetadataAsync(type.Value, lifetime.Token);
            lifetime.Token.ThrowIfCancellationRequested();
            if (activeVehicle.VehicleId != id || EditSession != session)
            {
                return;
            }
            if (session.IsDirty)
            {
                SetMessages("Metadata downloaded. Pending edits were made during the download; apply or discard them before reloading this editor.");
                return;
            }
            await session.LoadAsync(cancellationToken: lifetime.Token);
            SetMessages($"Refreshed {type} metadata and reloaded this editor. Reopen other setup editors to use the updated descriptions and limits.");
        }
        catch (OperationCanceledException)
        {
            SetMessages("Metadata refresh cancelled.");
        }
        catch (Exception exception)
        {
            SetMessages(exception);
        }
        finally
        {
            ResetBusy();
        }
    }
}
