# MissionPlanner Browser

The WebAssembly host starts `MissionPlanner.App` with an awaited browser lifetime
and a `MainView` containing the toolbar, navigation shell, and status bar.
Configuration is embedded from the shared app's `appsettings.json`; startup does
not depend on a local configuration file or a filesystem watcher. Application
initialization is asynchronous, and browser logging writes to the console.

## Run

With the .NET 10 SDK installed, from the repository root:

```powershell
dotnet workload install wasm-tools
dotnet --version
dotnet workload list
dotnet run --configuration Debug --project src/Platforms/MissionPlanner.Browser

# Or, from the browser project folder:

cd ./src/Platforms/MissionPlanner.Browser
dotnet run --configuration Debug --project MissionPlanner.Browser.csproj
```

For C# debugging from a shell, leave the app running and use a second PowerShell
in the browser project directory:

```powershell
# Chrome:
./src/Platforms/MissionPlanner.Browser/Start-DebugBrowser.ps1 -Browser Chrome
# Edge:
./src/Platforms/MissionPlanner.Browser/Start-DebugBrowser.ps1 -Browser Edge
# Thorium:
./src/Platforms/MissionPlanner.Browser/Start-DebugBrowser.ps1 -Browser Thorium
# Comet:
./src/Platforms/MissionPlanner.Browser/Start-DebugBrowser.ps1 -Browser Comet
```

Thorium is the default when `-Browser` is omitted. Edge and Chrome remain supported.
The script searches standard installation directories, including per-user Thorium
and Perplexity Comet installations. For a portable or custom installation, also
pass `-BrowserPath 'C:\path\to\browser.exe'`. Run one debugging browser at a time
because they use the same port 9222.

Keep the app tab at `https://localhost:7169/` open. Press Ctrl+T and open
`https://localhost:7169/_framework/debug?url=https%3A%2F%2Flocalhost%3A7169%2F`
in the new tab. The launcher prints this filtered URL for your chosen `AppUrl`.
The `url` parameter restricts attachment to the application tab. Without it,
the picker can select the debugger page itself, producing a recursive DevTools
view. If this happens, close the recursive DevTools tab, reopen the app, and
open the filtered debugger URL in a separate tab. Debug build
configuration alone does not enable the browser's remote-debugging endpoint.
The helper uses a separate browser profile and loopback port 9222; this port is
not opened in the home-network firewall rule. The debugger page is a tab picker,
not the app entry point. HTTPS certificate trust must be resolved separately.

Native WebAssembly linking is required by Avalonia/Skia and SQLite. A managed
build with `WasmBuildNative=false` checks C# and XAML only; it does not produce a
validated runnable application. Trimming is disabled while shared application
services depend on reflection-based discovery and configuration binding.

## Home-network HTTPS

The launch profile binds localhost and `192.168.1.175` on HTTP port 5235 and
HTTPS port 7169. The setup creates an ASP.NET Core development certificate with
the friendly name `MissionPlanner Browser Development`, including the LAN IP.
The .NET WebAssembly host uses the default development certificate from the
current user's Personal store; it does not apply ordinary Kestrel certificate
settings. Other local ASP.NET Core applications may also select this certificate.
Existing development certificates are preserved.

On this Windows PC, run the following in PowerShell as Administrator under the
same account that runs Visual Studio/dotnet, and accept the certificate trust prompt:

```powershell
 ./src/Platforms/MissionPlanner.Browser/Setup-DevNetwork.ps1
```

The setup script creates/reuses a certificate covering localhost, loopback, and
`192.168.1.175`, trusts it for the current user, and creates an inbound TCP rule
for ports 5235/7169. The rule is restricted to the dotnet executable, the network
interface owning that IP, and source addresses `192.168.1.0/24`. It also works
when this home Wi-Fi connection is classified as Public. Restart the browser host
after setup to load the launch settings and certificate.

On each other Windows PC, copy only
`dev-certs/MissionPlanner-Browser.cer` through a trusted transfer, then run:

```powershell
Import-Certificate -FilePath .\MissionPlanner-Browser.cer -CertStoreLocation Cert:\CurrentUser\Root
```

Accept the trust prompt and open `https://192.168.1.175:7169/`. Certificate trust
is local to each PC/user; the server cannot install it on other PCs automatically.
Use HTTPS for browser features such as geolocation. The private key is
non-exportable and stays in the host's certificate store. Generated certificate
files are ignored by Git. No router port forwarding is needed.

To remove network access, run `Remove-NetFirewallRule -Name MissionPlanner-Browser-HomeNetwork`
as Administrator. The certificate expires after twelve months; rerun setup to
renew it and distribute the renewed public certificate to client PCs.

## Connect through the local UDP bridge

From this browser project directory, run:

```powershell
./src/Platforms/MissionPlanner.Browser/Start-UdpBridge.ps1
```

Keep the shell running and open **http://127.0.0.1:7170/**. This native host serves
the browser app and its WebSocket endpoint together. Select **UDP**, port **14550**,
then Connect. Configure the local simulator/MAVLink sender to send to
**127.0.0.1:14550**. This is a listening port: the relay learns the sender's UDP
endpoint from its first packet and sends replies back to that endpoint. The
existing `localhost:5235` / `localhost:7169` WebAssembly dev host has no bridge.

The first implementation supports one browser connection and one local UDP peer.
Close other applications listening on UDP 14550 first. The bridge binds only to
loopback and accepts WebSocket handshakes only from its own origin. It does not
accept arbitrary UDP destinations from browser input, change certificates, or
open firewall ports. Access from another PC and direct browser TCP/serial are
not included. HTTP loopback is treated as a secure context by browsers.

`MissionPlanner.Library.Browser/Transport` contains the WebSocket transport,
browser session factory and serial-discovery implementation. Browser configuration
limits the available channels to UDP. The shared MAVLink parser, vehicle registry,
transmission policy and telemetry pipeline are reused. `MissionPlanner.BrowserBridge`
contains only the native hosting/UDP relay. The wire format is binary: server to
browser messages contain four IPv4 address bytes, a two-byte big-endian sender
port, then one UDP payload; browser to server messages contain one UDP payload.
Reload/disconnect releases the socket. Reconnect to select a new UDP peer.

For an alternate local port, start the native host directly with `--UdpPort N`
and select that same port in the app:

```powershell
dotnet run --project ..\MissionPlanner.BrowserBridge -- --BrowserAssets "$PWD\bin\Debug\net10.0-browser\MissionPlanner.Browser.staticwebassets.runtime.json" --UdpPort 14551
```

The standalone bridge does not include the WebAssembly C# debugger proxy. Use the
existing WebAssembly host for that debugger workflow. Browser DevTools remains
available on the bridge host.

## Platform services

Browser map services use `/missionplanner` in the WebAssembly virtual filesystem
and `HttpClientHandler` for browser fetch requests. OS application-data folders
are unavailable in WebAssembly. Map packs, custom map sources, HTTP cache and
terrain cache in this filesystem last only for the current page instance; these
registrations do not add persistent browser offline-map storage.

`MissionPlanner.Library.Browser` owns the browser implementations and JavaScript
bridge. The build copies that bridge into the host's `wwwroot/browser-platform.js`
before static-asset discovery. The generated copy is ignored by Git; edit the
library's source file. `main.js` imports and registers it before managed startup.

| Windows implementation | Browser implementation | Behavior |
| --- | --- | --- |
| `WindowsAppConfigurator` | `BrowserAppConfigurator` | Registers browser implementations before shared service defaults. |
| `WindowsPlatformLocationService` | `BrowserPlatformLocationService` | Uses browser geolocation with permission, a ten-second timeout, cancellation, and coordinate validation. Unavailable/denied locations return null. HTTPS or localhost is required. |
| `SecurePlannerSecretStore` | `BrowserPlannerSecretStore` | Stores secrets only in the current application instance's memory. Reloading clears them. This is not a persistent native credential vault. |
| Desktop JSON settings store | `BrowserPlannerSettingsStore` | Persists non-secret Planner settings in origin-scoped local storage. Blocked reads log a warning and start with defaults; write errors propagate rather than reporting successful persistence. |

The Windows implementations remain available for the desktop host. This migration
does not provide browser equivalents for local SITL processes, native serial/UDP/TCP
connections, Windows firmware tools, desktop window dialogs, or all filesystem
features elsewhere in the shared application. Those require separate platform
work. Map and network requests also remain subject to browser CORS rules.

## Focused checks

If the runtime fails, the page displays a selectable error report containing the
first and most recent runtime error output. Copy that report instead of trying to
catch the first line in a scrolling console. Pending timers and animation frames
are stopped after a fatal failure; reload the page to start a new instance.
The same report is available as `window.missionPlannerStartupFailure` in DevTools.
`curl` can verify asset delivery but does not execute WebAssembly startup.

```powershell
node --test src/Tests/browser-platform.test.mjs
dotnet test src/Tests/MissionPlanner.BrowserBridge.Tests
dotnet test src/Tests/MissionPlanner.AvaloniaUI.Tests --filter FullyQualifiedName~BrowserPlannerSecretStoreTests
```

The tests cover geolocation success/unavailability/denial/timeout, settings
persistence and storage errors, and secret replacement, isolation, removal, and
cancellation. A browser smoke test is still required after installing `wasm-tools`.
