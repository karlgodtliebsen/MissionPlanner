using System.Security.Cryptography;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Core.Setup.Advanced;
using MissionPlanner.Core.Setup.Advanced.Signing;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.Advanced.Signing;

/// <summary>Coordinates explicit signing setup and non-secret status polling.</summary>
public sealed partial class SigningViewModel(SigningKeyViewModel keys, SigningSetupService setup,
    IAdvancedPlatformCapabilities capabilities, IDialogService dialogs, TimeProvider clock,
    ILogger<SigningViewModel> logger, IUiDispatcher dispatcher, IDomainEventHub events) : ViewModelBase(logger, dispatcher, events)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private CancellationTokenSource? lifetime;
    private Task polling = Task.CompletedTask;
    private Task configuring = Task.CompletedTask;
    /// <summary>Gets the dedicated key-selection panel.</summary>
    public SigningKeyViewModel Keys => keys;
    /// <summary>Gets this platform's persistence explanation.</summary>
    public string Storage => capabilities.Current.SecureKeyStorage
        ? "Keys and reserved timestamps use the Windows credential vault. Loading a saved key never automatically changes the vehicle."
        : "Session only: keys stay in application memory and are lost on reload. Export your key explicitly for recovery; ordinary browser storage is never used.";
    /// <summary>Gets the configuration result or recovery instruction.</summary>
    [ObservableProperty] public partial string Message { get; private set; } = "Select a key and a unique outbound link ID. Use a trusted physical connection for setup.";
    /// <summary>Gets non-secret connection status and counters.</summary>
    [ObservableProperty] public partial string Status { get; private set; } = "Disabled";
    /// <summary>Gets whether setup is running.</summary>
    [ObservableProperty] public partial bool IsBusy { get; private set; }

    /// <inheritdoc />
    public override async Task ActivateAsync()
    {
        await gate.WaitAsync();
        try
        {
            if (lifetime is not null) { return; }
            lifetime = new();
            keys.KeyChanged += KeyChanged;
            await keys.ActivateAsync();
            polling = PollAsync(lifetime.Token);
        }
        finally { gate.Release(); }
    }

    /// <inheritdoc />
    public override async Task DeactivateAsync()
    {
        lifetime?.Cancel();
        await gate.WaitAsync();
        try
        {
            if (lifetime is null) { return; }
            keys.KeyChanged -= KeyChanged;
            await configuring;
            await polling;
            await keys.DeactivateAsync();
            keys.Clear();
            lifetime.Dispose();
            lifetime = null;
        }
        finally { gate.Release(); }
    }

    private void KeyChanged(string fingerprint) => Message = $"Selected key fingerprint: {fingerprint}. Configuration requires confirmation.";

    [RelayCommand]
    private Task ConfigureAsync()
    {
        if (IsBusy || keys.IsBusy || lifetime is null) { return Task.CompletedTask; }
        configuring = ConfigureCoreAsync(lifetime.Token);
        return configuring;
    }

    private async Task ConfigureCoreAsync(CancellationToken token)
    {
        IsBusy = keys.Locked = true;
        byte[]? secret = null;
        try
        {
            if (keys.LinkId is < 0 or > 255) { throw new InvalidOperationException("Invalid link identifier."); }
            secret = keys.CopyKey();
            var link = (byte)keys.LinkId;
            var accepted = await dialogs.ConfirmAsync(dialogs.CreateOptions("Configure vehicle signing", "Configure signing", "Cancel"),
                "This sends a secret key to the connected vehicle. Use a trusted USB/wired connection. Other ground stations may be locked out; retain a recovery copy before continuing. A timeout can mean the vehicle changed even though local setup failed. Configure signing now?", token);
            token.ThrowIfCancellationRequested();
            if (!accepted) { Message = "Signing setup cancelled before transmission."; return; }
            Message = "Waiting for a verified signed exchange from the selected vehicle…";
            await setup.ConfigureAsync(secret, link, true, token);
            Message = "Vehicle signature verified; local outbound signing is active. Unsigned traffic is still accepted by policy.";
        }
        catch (OperationCanceledException)
        {
            Message = "Signing setup cancelled, disconnected or timed out. The vehicle may already have changed. Reconnect over trusted USB with the saved key and retry; local success was not assumed.";
        }
        catch (Exception exception)
        {
            Logger.LogWarning("Signing setup failed ({FailureType}).", exception.GetType().Name);
            Message = "Signing setup failed. Check connection, selected key, link ID and secure storage. The vehicle may have changed; recover over trusted USB using the saved key.";
        }
        finally
        {
            if (secret is not null) { CryptographicOperations.ZeroMemory(secret); }
            IsBusy = keys.Locked = false;
        }
    }

    private async Task PollAsync(CancellationToken token)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1), clock);
            do
            {
                var snapshot = setup.Snapshot();
                await Dispatcher.DispatchAsync(() =>
                {
                    if (!token.IsCancellationRequested)
                    {
                        Status = $"{snapshot.State} · fingerprint {snapshot.Fingerprint} · link {snapshot.LinkId} · timestamp {snapshot.Timestamp}\n"
                            + $"Verified: {snapshot.Verified} · unsigned: {snapshot.Unsigned} · invalid: {snapshot.Invalid} · replayed: {snapshot.Replayed}\n"
                            + $"Last verified inbound timestamp: {snapshot.LastVerifiedTimestamp}";
                    }
                });
            } while (await timer.WaitForNextTickAsync(token));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception)
        {
            Logger.LogWarning("Signing diagnostics unavailable ({FailureType}).", exception.GetType().Name);
        }
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        lifetime?.Cancel();
        _ = CloseAsync();
        base.Dispose();
    }

    private async Task CloseAsync()
    {
        try { await DeactivateAsync(); }
        catch (Exception exception) { Logger.LogWarning("Signing view cleanup failed ({FailureType}).", exception.GetType().Name); }
    }
}
