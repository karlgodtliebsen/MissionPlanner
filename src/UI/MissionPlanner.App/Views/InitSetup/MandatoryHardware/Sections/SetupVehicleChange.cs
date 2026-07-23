using MissionPlanner.Core.Vehicles;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

internal static class SetupVehicleChange
{
    internal static bool IsConnectionOrIdentityBoundary(ActiveVehicleChangedEventArgs args)
    {
        return args.Previous.VehicleId != args.Current.VehicleId ||
            args.Previous.IsOnline != args.Current.IsOnline ||
            args.Previous.State?.Identity != args.Current.State?.Identity;
    }
}
