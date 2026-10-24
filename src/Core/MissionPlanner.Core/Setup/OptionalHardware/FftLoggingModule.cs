using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>Projects reported batch logging and onboard FFT controls without changing logging automatically.</summary>
public sealed class FftLoggingModule : IOptionalHardwareModule
{
    /// <inheritdoc />
    public string Key => "fft-logging";

    /// <inheritdoc />
    public string Title => "FFT batch logging";

    /// <inheritdoc />
    public bool IsAvailable(IReadOnlyDictionary<string, VehicleParameter> parameters)
    {
        return parameters.Keys.Any(IsSetting);
    }

    /// <inheritdoc />
    public OptionalHardwareModuleView Build(IReadOnlyDictionary<string, VehicleParameter> parameters,
        IReadOnlyDictionary<string, ParameterMetadata> metadata)
    {
        var settings = parameters.Keys.Where(IsSetting).Order(StringComparer.Ordinal)
            .Select(name => PeripheralSettingFactory.TryBuild(name, parameters, metadata)).OfType<PeripheralSetting>().ToArray();
        return new(Key, Title, "Select the IMUs and batch options for this firmware, review log capacity, then apply individual settings. Capture flight logs in DataFlash Logs and analyze them in FFT / Vibration Analysis.", settings, [], null);
    }

    private static bool IsSetting(string name)
    {
        return name.StartsWith("INS_LOG_BAT", StringComparison.Ordinal) || name.StartsWith("FFT_", StringComparison.Ordinal) ||
               name is "LOG_BITMASK" or "LOG_DISARMED" or "LOG_BACKEND_TYPE";
    }
}
