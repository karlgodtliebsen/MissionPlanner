using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Generated;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.OptionalHardware;

public enum CompassMotorCalibrationState { Idle, Starting, Running, Stopping, Completed, Failed, Disconnected }
public sealed record CompassMotorCalibrationSample(double ThrottlePercent,double CurrentAmps,double InterferencePercent,double CompensationX,double CompensationY,double CompensationZ,DateTimeOffset Timestamp);
public sealed record CompassMotorCalibrationSnapshot(CompassMotorCalibrationState State,string Instruction,IReadOnlyList<CompassMotorCalibrationSample> Samples,string? FailureReason)
{ public static CompassMotorCalibrationSnapshot Initial { get; }=new(CompassMotorCalibrationState.Idle,"Remove all propellers before starting CompassMot.",[],null); }
public interface ICompassMotorCalibrationService : IDisposable
{ CompassMotorCalibrationSnapshot Current { get; } event EventHandler<CompassMotorCalibrationSnapshot>? Changed; Task<bool> StartAsync(VehicleId vehicleId,CancellationToken cancellationToken=default); Task StopAsync(CancellationToken cancellationToken=default); }

/// <summary>Runs safety-gated CompassMot using generated MAVLink messages.</summary>
public sealed class CompassMotorCalibrationService : ICompassMotorCalibrationService
{
    private const ushort Command=(ushort)MavCmd.PreflightCalibration; private const int MaximumSamples=240;
    private readonly IActiveVehicleContext active; private readonly IVehicleRegistry registry; private readonly IEventHub events; private readonly IMavLinkConnection connection; private readonly IMavLinkCommandEncoder encoder; private readonly IVehicleOperationGate gate; private readonly ILogger<CompassMotorCalibrationService> logger;
    private IDisposable? subscription; private IDisposable? lease; private VehicleId? target; private readonly List<CompassMotorCalibrationSample> samples=[];
    public CompassMotorCalibrationService(IActiveVehicleContext active,IVehicleRegistry registry,IEventHub events,IMavLinkConnection connection,IMavLinkCommandEncoder encoder,IVehicleOperationGate gate,ILogger<CompassMotorCalibrationService> logger){this.active=active;this.registry=registry;this.events=events;this.connection=connection;this.encoder=encoder;this.gate=gate;this.logger=logger;active.Changed+=ActiveChanged;}
    public CompassMotorCalibrationSnapshot Current { get; private set; }=CompassMotorCalibrationSnapshot.Initial; public event EventHandler<CompassMotorCalibrationSnapshot>? Changed;
    public async Task<bool> StartAsync(VehicleId id,CancellationToken token=default){if(!active.IsOnline||active.VehicleId!=id||active.State is not { } state){Transition(CompassMotorCalibrationState.Disconnected,"Connect the target vehicle first.","Target unavailable.");return false;}if(state.IsArmed){Transition(CompassMotorCalibrationState.Failed,"Disarm the vehicle before CompassMot.","Vehicle is armed.");return false;}if(!gate.TryAcquire(id,"CompassMot calibration",out lease)){Transition(CompassMotorCalibrationState.Failed,$"Cannot start while {gate.GetCurrentOperation(id)} is active.","Operation conflict.");return false;}target=id;samples.Clear();subscription=events.SubscribeAsync<MavLinkMessage>(MavLinkEventTopics.ReceivedMessage,HandleAsync);Transition(CompassMotorCalibrationState.Starting,"Starting CompassMot…",null);try{await SendAsync(id,[0,0,0,0,0,1,0],token);Transition(CompassMotorCalibrationState.Running,"Raise throttle gradually while monitoring interference.",null);return true;}catch(Exception ex)when(ex is not OperationCanceledException){logger.LogError(ex,"CompassMot start failed.");Finish(CompassMotorCalibrationState.Failed,ex.Message);return false;}}
    public async Task StopAsync(CancellationToken token=default){var id=target;Transition(CompassMotorCalibrationState.Stopping,"Stopping CompassMot…",null);try{if(id is { } vehicle&&active.IsOnline)await SendAsync(vehicle,[0,0,0,0,0,0,0],token);}finally{Finish(CompassMotorCalibrationState.Completed,"CompassMot stopped. Review the final samples.");}}
    private Task HandleAsync(MavLinkMessage message,CancellationToken token){if(message is CompassmotStatusMessage status&&target is { } id&&message.SystemId==id.SystemId&&message.ComponentId==id.ComponentId){samples.Add(new(status.Throttle/10d,status.Current,status.Interference,status.Compensationx,status.Compensationy,status.Compensationz,status.ReceivedAt));if(samples.Count>MaximumSamples)samples.RemoveRange(0,samples.Count-MaximumSamples);Transition(CompassMotorCalibrationState.Running,"Collecting CompassMot samples.",null);}return Task.CompletedTask;}
    private async Task SendAsync(VehicleId id,IReadOnlyList<float> args,CancellationToken token){var session=registry.GetRequired(id)??throw new InvalidOperationException("Vehicle session unavailable.");await connection.SendRawAsync(encoder.EncodeCommandLong(id.SystemId,id.ComponentId,Command,args),session.EndPoint,token);}
    private void ActiveChanged(object? s,ActiveVehicleChangedEventArgs e){if(target is { } id&&(!e.Current.IsOnline||e.Current.VehicleId!=id))Finish(CompassMotorCalibrationState.Disconnected,"Vehicle disconnected during CompassMot.");}
    private void Finish(CompassMotorCalibrationState state,string instruction){Transition(state,instruction,state is CompassMotorCalibrationState.Failed or CompassMotorCalibrationState.Disconnected?instruction:null);subscription?.Dispose();subscription=null;lease?.Dispose();lease=null;target=null;}
    private void Transition(CompassMotorCalibrationState state,string instruction,string? error){Current=new(state,instruction,samples.ToArray(),error);Changed?.Invoke(this,Current);}
    public void Dispose(){active.Changed-=ActiveChanged;subscription?.Dispose();lease?.Dispose();}
}
