import { installStartupDiagnostics } from './startup-diagnostics.js'
import * as browserPlatform from './browser-platform.js'

if (typeof window === 'undefined') throw new Error('Expected to be running in a browser');

const diagnostics = installStartupDiagnostics();
try {
    // Install diagnostics before loading the runtime, including native startup.
    const { dotnet } = await import('./_framework/dotnet.js');
    const dotnetRuntime = await dotnet
        .withModuleConfig(diagnostics.moduleConfig)
        .withDiagnosticTracing(false)
        .withApplicationArgumentsFromQuery()
        .create();

    const config = dotnetRuntime.getConfig();
    // Resolve the bridge beside main.js, not relative to the runtime in _framework.
    dotnetRuntime.setModuleImports('MissionPlanner.Browser.Platform', browserPlatform);
    await dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href]);
} catch (error) {
    diagnostics.report(error);
}
