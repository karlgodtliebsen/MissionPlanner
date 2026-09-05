// Keep diagnostics outside .NET so they still work after the runtime aborts.
export function installStartupDiagnostics(host = globalThis) {
    const output = [];
    const firstOutput = [];
    let failure;
    const pending = [];
    const describe = value => String(value?.stack ?? value).slice(0, 12000);

    // A failed runtime cannot service timers or render callbacks. Cancel those
    // owned by this app page, including callbacks queued before the failure.
    for (const [schedule, cancel, repeating] of [
        ['setTimeout', 'clearTimeout', false],
        ['setInterval', 'clearInterval', true],
        ['requestAnimationFrame', 'cancelAnimationFrame', false]
    ]) {
        const original = host[schedule].bind(host);
        const clear = host[cancel].bind(host);
        const ids = new Set();
        pending.push(() => { for (const id of ids) clear(id); ids.clear(); });
        host[cancel] = id => { ids.delete(id); clear(id); };
        host[schedule] = (callback, ...args) => {
            if (failure) return 0;
            let id;
            id = original(typeof callback === 'function' ? (...values) => {
                if (!repeating) ids.delete(id);
                if (!failure) callback(...values);
            } : callback, ...args);
            ids.add(id);
            return id;
        };
    }

    function report(error) {
        if (failure) return;
        failure = ['MissionPlanner browser runtime stopped.', '', describe(error),
            '', 'First runtime error output:', ...firstOutput,
            '', 'Recent runtime error output:', ...output].join('\n');
        for (const cancel of pending) cancel();
        host.missionPlannerStartupFailure = failure;
        const panel = host.document.createElement('section');
        panel.style.cssText = 'position:fixed;inset:0;z-index:2147483647;background:#182238;color:#fff;padding:24px;overflow:auto;font:14px monospace';
        const heading = host.document.createElement('h1');
        heading.textContent = 'MissionPlanner could not start or continue';
        const instructions = host.document.createElement('p');
        instructions.textContent = 'Copy the report below and share it for diagnosis. Reload the page to retry.';
        const details = host.document.createElement('textarea');
        details.setAttribute('aria-label', 'Browser runtime failure report');
        details.readOnly = true;
        details.value = failure;
        details.style.cssText = 'width:100%;height:70vh;box-sizing:border-box;white-space:pre;font:13px monospace';
        panel.append(heading, instructions, details);
        host.document.body.append(panel);
    }

    host.addEventListener('error', event => {
        if (!event.error) return;
        const alreadyFailed = Boolean(failure);
        report(event.error);
        if (alreadyFailed) event.preventDefault();
    });
    host.addEventListener('unhandledrejection', event => {
        const alreadyFailed = Boolean(failure);
        report(event.reason);
        if (alreadyFailed) event.preventDefault();
    });

    return {
        report,
        moduleConfig: {
            printErr: (...args) => {
                if (failure) return;
                const line = args.map(describe).join(' ').slice(0, 12000);
                if (firstOutput.length < 30) firstOutput.push(line);
                else {
                    output.push(line);
                    if (output.length > 30) output.shift();
                }
                host.console.error(...args);
            },
            onAbort: reason => report(reason),
            onExit: code => { if (code !== 0) report(`.NET runtime exited with code ${code}`); }
        }
    };
}
