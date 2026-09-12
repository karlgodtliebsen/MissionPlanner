using System.Security.Cryptography;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Core.Setup.Advanced.Signing;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Signing;

namespace MissionPlanner.App.Views.InitSetup.Advanced.Signing;

/// <summary>Owns an editable secret buffer; key import/export requires an explicit confirmed file operation.</summary>
public sealed partial class SigningKeyViewModel(SigningKeyRepository repository, IFileOpenService open,
    IFileSaveService save, IDialogService dialogs, ILogger<SigningKeyViewModel> logger,
    IUiDispatcher dispatcher, IDomainEventHub events) : ViewModelBase(logger, dispatcher, events)
{
    private byte[]? key;
    private CancellationTokenSource? lifetime;
    private Task operation = Task.CompletedTask;
    /// <summary>Signals only the non-secret fingerprint of a changed selection.</summary>
    public event Action<string>? KeyChanged;
    /// <summary>Gets the visible key fingerprint.</summary>
    [ObservableProperty] public partial string Fingerprint { get; private set; } = "No key selected";
    /// <summary>Gets the result of the last key operation.</summary>
    [ObservableProperty] public partial string Message { get; private set; } = "Generate, import or load a key. Secret bytes are never displayed.";
    /// <summary>Gets or sets the outbound link identifier.</summary>
    [ObservableProperty] public partial int LinkId { get; set; } = 1;
    /// <summary>Gets or sets whether configuration currently owns this selection.</summary>
    [ObservableProperty]
    public partial bool Locked
    {
        get; set;
    }

    /// <summary>Copies the selected key for one operation; callers must erase their copy.</summary>
    public byte[] CopyKey()
    {
        return key?.ToArray() ?? throw new InvalidOperationException("Select a signing key first.");
    }

    /// <summary>Erases the selected key when the parent page closes.</summary>
    public void Clear()
    {
        if (key is not null)
        {
            CryptographicOperations.ZeroMemory(key);
            key = null;
        }
        Fingerprint = "No key selected";
        KeyChanged?.Invoke(Fingerprint);
    }

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        lifetime ??= new();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override async Task DeactivateAsync()
    {
        lifetime?.Cancel();
        await operation;
        lifetime?.Dispose();
        lifetime = null;
    }

    [RelayCommand]
    private Task GenerateAsync()
    {
        return RunAsync(async token =>
    {
        if (await ConfirmAsync("Generate signing key", "Replace the selected key with a new random key? The vehicle is not changed until setup is confirmed.", token))
        {
            Set(MavLinkSigningCodec.Generate());
        }
    });
    }

    [RelayCommand]
    private Task LoadAsync()
    {
        return RunAsync(async token =>
    {
        using var saved = await repository.LoadAsync(token);
        token.ThrowIfCancellationRequested();
        if (saved is null)
        {
            Message = "No saved key is available in this platform's secret store.";
            return;
        }
        LinkId = saved.LinkId;
        Set(saved.Key.ToArray());
        Message = "Saved key loaded for review. Previous setup may not have completed; local signing is not activated by loading.";
    });
    }

    [RelayCommand]
    private Task ImportAsync()
    {
        return RunAsync(async token =>
    {
        if (!await ConfirmAsync("Import signing key", "Import a secret key from a trusted local file? The file must contain exactly 64 hexadecimal characters.", token))
        {
            return;
        }
        using var file = await open.OpenAsync("Import signing key", ["*.key", "*.txt"], token);
        token.ThrowIfCancellationRequested();
        if (file is null)
        {
            return;
        }
        var bytes = new byte[65];
        try
        {
            var count = 0;
            while (count < bytes.Length)
            {
                var read = await file.Content.ReadAsync(bytes.AsMemory(count), token);
                if (read == 0)
                {
                    break;
                }
                count += read;
            }
            Set(MavLinkSigningCodec.Import(Encoding.ASCII.GetString(bytes, 0, count)));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    });
    }

    [RelayCommand]
    private Task ExportAsync()
    {
        return RunAsync(async token =>
    {
        if (!await ConfirmAsync("Export secret signing key", "This saves the unencrypted key to a file. Anyone with the file can authenticate commands. Store it securely and never attach it to diagnostics. Continue?", token))
        {
            return;
        }
        var secret = CopyKey();
        var bytes = Encoding.ASCII.GetBytes(Convert.ToHexString(secret));
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            var result = await save.SaveAsync("mavlink-signing.key", stream, token);
            token.ThrowIfCancellationRequested();
            Message = result is null ? "Key export cancelled." : "Secret key exported. Keep the file private.";
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
            CryptographicOperations.ZeroMemory(bytes);
        }
    });
    }

    private void Set(byte[] next)
    {
        Clear();
        key = next;
        Fingerprint = MavLinkSigningCodec.Fingerprint(key);
        Message = "Key selected; vehicle configuration has not been changed.";
        KeyChanged?.Invoke(Fingerprint);
    }

    private async Task<bool> ConfirmAsync(string title, string message, CancellationToken token)
    {
        var confirmed = await dialogs.ConfirmAsync(dialogs.CreateOptions(title, "Continue", "Cancel"), message, token);
        token.ThrowIfCancellationRequested();
        return confirmed;
    }

    private Task RunAsync(Func<CancellationToken, Task> action)
    {
        if (Locked || IsBusy || lifetime is null)
        {
            return Task.CompletedTask;
        }
        operation = RunCoreAsync(action, lifetime.Token);
        return operation;
    }

    private async Task RunCoreAsync(Func<CancellationToken, Task> action, CancellationToken token)
    {
        IsBusy = true;
        try
        {
            await action(token);
        }
        catch (OperationCanceledException) { Message = "Key operation cancelled."; }
        catch (Exception exception)
        {
            Logger.LogWarning("Signing key operation failed ({FailureType}).", exception.GetType().Name);
            Message = "Key operation failed. Check the file format and storage permission; no vehicle changes were made.";
        }
        finally { IsBusy = false; }
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        lifetime?.Cancel();
        Clear();
        base.Dispose();
    }
}
