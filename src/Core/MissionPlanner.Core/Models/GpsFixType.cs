namespace MissionPlanner.Core.Models;

public enum GpsFixType : byte
{
    Unknown = 0,
    NoGps = 1,
    NoFix = 2,
    Fix2D = 3,
    Fix3D = 4,
    DifferentialGps = 5,
    RtkFloat = 6,
    RtkFixed = 7,
    Static = 8,
    Ppp = 9
}
