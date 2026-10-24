using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Notifications;

namespace MissionPlanner.Library.Desktop;

/// <summary>Uses installed desktop speech engines; text is passed on standard input, never evaluated as code.</summary>
public sealed class DesktopSpeechService : ISpeechService, IDisposable, IAsyncDisposable
{
    private readonly ILogger<DesktopSpeechService> logger;
    private readonly string? executable;
    private readonly Channel<string> queue = Channel.CreateBounded<string>(new BoundedChannelOptions(4)
    {
        SingleReader = true,
        FullMode = BoundedChannelFullMode.DropOldest
    });
    private readonly CancellationTokenSource lifetime = new();
    private readonly SemaphoreSlim speaking = new(1, 1);
    private readonly Task worker;

    /// <summary>Selects a locally installed engine and starts the owned notification queue.</summary>
    public DesktopSpeechService(ILogger<DesktopSpeechService> logger)
    {
        this.logger = logger;
        executable = OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe")
            : OperatingSystem.IsMacOS() ? "/usr/bin/say"
            : new[] { "/usr/bin/espeak-ng", "/usr/bin/espeak" }.FirstOrDefault(File.Exists);
        if (!File.Exists(executable))
        {
            executable = null;
        }
        worker = ProcessQueueAsync(lifetime.Token);
    }

    /// <inheritdoc />
    public bool IsAvailable => executable is not null;

    /// <inheritdoc />
    public string Status => executable is null ? "Speech unavailable. On Linux, install espeak-ng to enable local speech."
        : $"Local speech: {Path.GetFileNameWithoutExtension(executable)}. Only notifications at or above the saved severity are spoken.";

    /// <inheritdoc />
    public bool TryEnqueue(string text) => IsAvailable && !string.IsNullOrWhiteSpace(text) &&
        queue.Writer.TryWrite(text[..System.Math.Min(text.Length, 500)]);

    /// <inheritdoc />
    public Task TestAsync(CancellationToken cancellationToken) => SpeakAsync("Mission Planner speech is ready.", cancellationToken);

    private async Task ProcessQueueAsync(CancellationToken token)
    {
        try
        {
            await foreach (var text in queue.Reader.ReadAllAsync(token).ConfigureAwait(false))
            {
                try
                {
                    await SpeakAsync(text, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    logger.LogWarning("Local speech failed ({FailureType}). Visual notification remains available.", exception.GetType().Name);
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
    }

    private async Task SpeakAsync(string text, CancellationToken cancellationToken)
    {
        if (executable is null)
        {
            throw new NotSupportedException(Status);
        }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        await speaking.WaitAsync(timeout.Token).ConfigureAwait(false);
        try
        {
            var info = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            if (OperatingSystem.IsWindows())
            {
                foreach (var argument in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-Command",
                    "Add-Type -AssemblyName System.Speech; $speaker = [System.Speech.Synthesis.SpeechSynthesizer]::new(); try { $speaker.Speak([Console]::In.ReadToEnd()) } finally { $speaker.Dispose() }" })
                {
                    info.ArgumentList.Add(argument);
                }
            }
            else if (!OperatingSystem.IsMacOS())
            {
                info.ArgumentList.Add("--stdin");
            }
            using var process = Process.Start(info) ?? throw new IOException("Speech process could not start.");
            try
            {
                var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
                var errors = process.StandardError.ReadToEndAsync(timeout.Token);
                await process.StandardInput.WriteAsync(text.AsMemory(), timeout.Token).ConfigureAwait(false);
                process.StandardInput.Close();
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                await Task.WhenAll(output, errors).ConfigureAwait(false);
                if (process.ExitCode != 0)
                {
                    throw new IOException("The local speech engine failed. Check installed voices and audio output.");
                }
            }
            finally
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
        }
        finally
        {
            speaking.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        queue.Writer.TryComplete();
        lifetime.Cancel();
    }
    /// <summary>Stops speech and waits for the queue worker to release its child process.</summary>
    public async ValueTask DisposeAsync()
    {
        Dispose();
        await worker.ConfigureAwait(false);
    }

}
