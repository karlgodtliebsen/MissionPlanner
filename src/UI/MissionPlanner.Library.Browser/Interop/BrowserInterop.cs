using System.Runtime.InteropServices.JavaScript;

namespace MissionPlanner.Library.Browser.Interop;

internal static partial class BrowserInterop
{
    private const string Module = "MissionPlanner.Browser.Platform";

    [JSImport("getBridgeUrl", Module)]
    internal static partial string GetBridgeUrl();

    [JSImport("getLocation", Module)]
    [return: JSMarshalAs<JSType.Promise<JSType.String>>]
    internal static partial Task<string?> GetLocationAsync();

    [JSImport("readSettings", Module)]
    internal static partial string? ReadSettings();

    [JSImport("writeSettings", Module)]
    internal static partial void WriteSettings(string document);

    [JSImport("clearSettings", Module)]
    internal static partial void ClearSettings();
}
