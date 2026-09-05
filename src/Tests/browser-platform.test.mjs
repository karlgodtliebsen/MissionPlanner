import assert from "node:assert/strict";
import { readFile, mkdtemp, mkdir, writeFile, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { pathToFileURL } from "node:url";
import test from "node:test";

const source = await readFile(new URL("../UI/MissionPlanner.Library.Browser/Interop/browser-platform.js", import.meta.url), "utf8");
const bridge = await import(`data:text/javascript;base64,${Buffer.from(source).toString("base64")}`);
const diagnosticsSource = await readFile(new URL("../Platforms/MissionPlanner.Browser/wwwroot/startup-diagnostics.js", import.meta.url), "utf8");
const { installStartupDiagnostics } = await import(`data:text/javascript;base64,${Buffer.from(diagnosticsSource).toString("base64")}`);

test("bootstrap imports the bridge from the app directory before managed startup", async () => {
    const root = await mkdtemp(join(tmpdir(), "missionplanner-bootstrap-"));
    const previousWindow = globalThis.window;
    const previousLocation = globalThis.location;
    const originals = Object.fromEntries(['setTimeout', 'clearTimeout', 'setInterval', 'clearInterval',
        'requestAnimationFrame', 'cancelAnimationFrame', 'addEventListener'].map(key => [key, globalThis[key]]));
    try {
        // A nested deployment must work too; the bridge is never in _framework.
        const app = join(root, "nested-app");
        await mkdir(join(app, "_framework"), { recursive: true });
        await writeFile(join(root, "package.json"), '{"type":"module"}');
        await writeFile(join(app, "browser-platform.js"), source);
        await writeFile(join(app, "startup-diagnostics.js"), diagnosticsSource);
        await writeFile(join(app, "main.js"), await readFile(
            new URL("../Platforms/MissionPlanner.Browser/wwwroot/main.js", import.meta.url), "utf8"));
        await writeFile(join(app, "_framework/dotnet.js"), `
            import assert from 'node:assert/strict';
            let registered;
            export let started = false;
            export const dotnet = {
                withModuleConfig(config) {
                    assert.equal(typeof config.onAbort, 'function');
                    assert.equal(typeof config.printErr, 'function');
                    return this;
                },
                withDiagnosticTracing() { return this; },
                withApplicationArgumentsFromQuery() { return this; },
                async create() { return {
                    getConfig: () => ({ mainAssemblyName: 'MissionPlanner.Browser' }),
                    setModuleImports(name, module) {
                        assert.equal(name, 'MissionPlanner.Browser.Platform');
                        registered = module;
                    },
                    async runMain() {
                        assert.equal(typeof registered?.getLocation, 'function');
                        assert.equal(typeof registered?.readSettings, 'function');
                        started = true;
                    }
                }; }
            };
        `);
        globalThis.window = {};
        globalThis.requestAnimationFrame = () => 1;
        globalThis.cancelAnimationFrame = () => {};
        globalThis.addEventListener = () => {};
        globalThis.location = { href: 'http://localhost/nested-app/' };
        await import(pathToFileURL(join(app, "main.js")).href);
        assert.equal((await import(pathToFileURL(join(app, "_framework/dotnet.js")).href)).started, true);
    } finally {
        globalThis.window = previousWindow;
        globalThis.location = previousLocation;
        Object.assign(globalThis, originals);
        await rm(root, { recursive: true, force: true });
    }
});

test("fatal diagnostics preserve the first failure and stop queued runtime callbacks", () => {
    let nextId = 0;
    const callbacks = new Map();
    const listeners = {};
    const panels = [];
    const host = {
        console: { error() {} },
        document: {
            createElement: () => ({ style: {}, append() {}, setAttribute() {} }),
            body: { append: panel => panels.push(panel) }
        },
        addEventListener: (name, callback) => { listeners[name] = callback; }
    };
    for (const [schedule, cancel] of [['setTimeout', 'clearTimeout'], ['setInterval', 'clearInterval'],
        ['requestAnimationFrame', 'cancelAnimationFrame']]) {
        host[schedule] = callback => { callbacks.set(++nextId, callback); return nextId; };
        host[cancel] = id => callbacks.delete(id);
    }
    const diagnostics = installStartupDiagnostics(host);
    let calls = 0;
    host.setInterval(() => calls++);
    host.setTimeout(() => calls++);
    host.requestAnimationFrame(() => calls++);
    const queued = [...callbacks.values()];
    queued[0]();
    assert.equal(calls, 1);
    diagnostics.moduleConfig.onExit(0);
    assert.equal(panels.length, 0);
    diagnostics.moduleConfig.printErr('System.PlatformNotSupportedException: original managed error');
    for (let i = 0; i < 100; i++) diagnostics.moduleConfig.printErr(`stack frame ${i}`);
    diagnostics.moduleConfig.onAbort('native abort');
    const first = host.missionPlannerStartupFailure;
    assert.match(first, /original managed error/);
    assert.match(first, /native abort/);
    assert.match(first, /stack frame 99/);
    assert.doesNotMatch(first, /stack frame 50\n/);
    assert.equal(callbacks.size, 0);
    for (const callback of queued) callback();
    assert.equal(calls, 1);
    assert.equal(host.setInterval(() => calls++), 0);
    let suppressed = 0;
    for (let i = 0; i < 500; i++) {
        listeners.error({ error: new Error('runtime already exited'), preventDefault() { suppressed++; } });
        diagnostics.moduleConfig.printErr('runtime already exited');
    }
    assert.equal(suppressed, 500);
    assert.equal(host.missionPlannerStartupFailure, first);
    assert.equal(panels.length, 1);
});

test("location handles unavailable, denied, timed-out and successful requests", async () => {
    globalThis.isSecureContext = false;
    assert.equal(await bridge.getLocation(), null);
    globalThis.isSecureContext = true;
    Object.defineProperty(globalThis, "navigator", { configurable: true, value: {} });
    assert.equal(await bridge.getLocation(), null);
    for (const code of [1, 2, 3]) {
        navigator.geolocation = { getCurrentPosition: (_, error) => error({ code }) };
        assert.equal(await bridge.getLocation(), null);
    }
    navigator.geolocation = { getCurrentPosition: (success, _, options) => {
        assert.equal(options.timeout, 10000);
        success({ coords: { latitude: 55.67, longitude: 12.56 } });
    } };
    assert.deepEqual(JSON.parse(await bridge.getLocation()), { latitude: 55.67, longitude: 12.56 });
});

test("settings round-trip and clear only the application key", () => {
    const values = new Map([["unrelated", "keep"]]);
    globalThis.localStorage = {
        getItem: key => values.get(key) ?? null,
        setItem: (key, value) => values.set(key, value),
        removeItem: key => values.delete(key)
    };
    assert.equal(bridge.readSettings(), null);
    bridge.writeSettings('{"theme":"dark"}');
    assert.equal(bridge.readSettings(), '{"theme":"dark"}');
    bridge.clearSettings();
    assert.equal(bridge.readSettings(), null);
    assert.equal(values.get("unrelated"), "keep");
});

test("storage failures reach the caller instead of reporting successful persistence", () => {
    globalThis.localStorage = {
        getItem() { throw new Error("blocked"); },
        setItem() { throw new Error("quota"); },
        removeItem() { throw new Error("blocked"); }
    };
    assert.throws(() => bridge.readSettings(), /blocked/);
    assert.throws(() => bridge.writeSettings("{}"), /quota/);
    assert.throws(() => bridge.clearSettings(), /blocked/);
});
