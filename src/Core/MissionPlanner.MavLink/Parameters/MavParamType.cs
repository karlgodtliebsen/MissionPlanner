namespace MissionPlanner.MavLink.Parameters;

/// <summary>
/// MAVLink parameter types as defined in the MAVLink specification.
/// </summary>
public enum MavParamType : byte
{
    /// <summary>
    /// 8-bit unsigned integer
    /// </summary>
    Uint8 = 1,

    /// <summary>
    /// 8-bit signed integer
    /// </summary>
    Int8 = 2,

    /// <summary>
    /// 16-bit unsigned integer
    /// </summary>
    Uint16 = 3,

    /// <summary>
    /// 16-bit signed integer
    /// </summary>
    Int16 = 4,

    /// <summary>
    /// 32-bit unsigned integer
    /// </summary>
    Uint32 = 5,

    /// <summary>
    /// 32-bit signed integer
    /// </summary>
    Int32 = 6,

    /// <summary>
    /// 32-bit IEEE 754 single precision floating point number
    /// </summary>
    Real32 = 9
}
