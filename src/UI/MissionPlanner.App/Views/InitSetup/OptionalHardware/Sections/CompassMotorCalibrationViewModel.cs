using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.App.Presentation;
using MissionPlanner.Core.Setup.OptionalHardware;
using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>Presents the safety-gated CompassMot workflow.</summary>
public sealed partial class CompassMotorCalibrationViewModel : OptionalHardwareBaseViewModel
{
    private readonly IActiveVehicleContext active; private readonly ICompassMotorCalibrationService service; private readonly IUserConfirmationService confirmation; private readonly IDispatcher dispatcher;
    public CompassMotorCalibrationViewModel(IActiveVehicleContext active,ICompassMotorCalibrationService service,IUserConfirmationService confirmation,IDispatcher dispatcher){this.active=active;this.service=service;this.confirmation=confirmation;this.dispatcher=dispatcher;service.Changed+=Changed;Show(service.Current);}
    public ObservableCollection<CompassMotorCalibrationSample> Samples { get; }=[];
    [ObservableProperty] public partial string Instruction { get; private set; }=string.Empty;
    [ObservableProperty] public partial string Compensation { get; private set; }=string.Empty;
    [RelayCommand] private async Task StartAsync(){if(active.VehicleId is not { } id)return;if(!await confirmation.ConfirmAsync("CompassMot safety","Confirm ALL propellers are removed and the area is clear. Motors may spin.","Propellers removed – start"))return;await service.StartAsync(id,active.ConnectionCancellationToken);}
    [RelayCommand] private Task StopAsync()=>service.StopAsync();
    private void Changed(object? s,CompassMotorCalibrationSnapshot snapshot)=>dispatcher.Dispatch(()=>Show(snapshot));
    private void Show(CompassMotorCalibrationSnapshot snapshot){Instruction=snapshot.Instruction;Samples.Clear();foreach(var sample in snapshot.Samples)Samples.Add(sample);if(snapshot.Samples.LastOrDefault() is { } last)Compensation=$"Compensation: {last.CompensationX:0.00}, {last.CompensationY:0.00}, {last.CompensationZ:0.00} · Current {last.CurrentAmps:0.0} A · Interference {last.InterferencePercent:0}%";}
    public override void Dispose(){service.Changed-=Changed;_ = service.StopAsync();service.Dispose();}
}
