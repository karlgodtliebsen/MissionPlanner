const settingsKey = "MissionPlanner.PlannerSettings";

export function getLocation() {
    if (!globalThis.isSecureContext || !globalThis.navigator?.geolocation) {
        return Promise.resolve(null);
    }
    return new Promise(resolve => navigator.geolocation.getCurrentPosition(
        position => resolve(JSON.stringify({
            latitude: position.coords.latitude,
            longitude: position.coords.longitude
        })),
        () => resolve(null),
        { enableHighAccuracy: false, maximumAge: 30000, timeout: 10000 }));
}

// Only non-secret Planner settings are persisted. Tokens stay in managed memory.
export function readSettings() { return localStorage.getItem(settingsKey); }
export function writeSettings(document) { localStorage.setItem(settingsKey, document); }
export function clearSettings() { localStorage.removeItem(settingsKey); }

export function getBridgeUrl() {
    const url = new URL('/bridge/udp', globalThis.location.href);
    url.protocol = url.protocol === 'https:' ? 'wss:' : 'ws:';
    return url.href;
}
