using AsyncAwaitBestPractices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.Core.Setup.OptionalHardware;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>Reviews a bounded local image and explicitly transfers it to a freshly detected CubeID.</summary>
public sealed partial class CubeIdUpdateViewModel(ILogger<CubeIdUpdateViewModel> logger,
    CubeIdFirmwareService updater, IUserConfirmationService confirmation, IFileOpenService files)
    : OptionalHardwareBaseViewModel(logger)
{
    private CubeFirmwareImage? image;
    private CancellationTokenSource lifetime = new();
    private bool disposed;
    /// <summary>Gets or sets a local firmware path.</summary>
    [ObservableProperty] public partial string FirmwarePath { get; set; } = string.Empty;
    /// <summary>Gets the inspected image identity.</summary>
    [ObservableProperty] public partial string FirmwareSummary { get; private set; } = "Select a CubeID .bin image. A fresh component heartbeat is required before updating.";
    /// <summary>Gets whether the latest detection succeeded.</summary>
    [ObservableProperty] public partial bool TargetDetected { get; private set; }
    /// <summary>Gets the acknowledged transfer progress.</summary>
    [ObservableProperty] public partial double TransferProgress { get; private set; }

    partial void OnFirmwarePathChanged(string value)
    {
        image = null;
        FirmwareSummary = "Inspect the selected path before updating.";
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (IsBusy) return;
        IsBusy = true;
        TargetDetected = false;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        try
        {
            var target = await updater.DetectAsync(linked.Token);
            TargetDetected = true;
            SetMessages($"Remote-ID component {target.SystemId}/{target.ComponentId} detected. Confirm that the attached hardware is a CubeID before selecting its firmware.", null);
        }
        catch (OperationCanceledException) { SetMessages("CubeID discovery cancelled or timed out.", null); }
        catch (Exception ex) { SetMessages(ex); }
        finally { IsBusy = false; }
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task BrowseAsync(CancellationToken cancellationToken)
    {
        if (IsBusy) return;
        IsBusy = true;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        try
        {
            using var file = await files.OpenAsync("Select CubeID firmware", ["*.bin"], linked.Token);
            if (file is null) return;
            FirmwarePath = file.FullPath ?? file.FileName;
            await LoadImageAsync(file.FileName, file.Content, linked.Token);
        }
        catch (OperationCanceledException) { SetMessages("Firmware selection cancelled.", null); }
        catch (Exception ex) { image = null; SetMessages(ex); }
        finally { IsBusy = false; }
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task InspectAsync(CancellationToken cancellationToken)
    {
        if (IsBusy) return;
        IsBusy = true;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        try
        {
            await using var stream = File.OpenRead(FirmwarePath);
            await LoadImageAsync(Path.GetFileName(FirmwarePath), stream, linked.Token);
        }
        catch (OperationCanceledException) { SetMessages("Image inspection cancelled.", null); }
        catch (Exception ex) { image = null; SetMessages(ex); }
        finally { IsBusy = false; }
    }

    private async Task LoadImageAsync(string name, Stream stream, CancellationToken token)
    {
        image = null;
        if (!name.EndsWith(".bin", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Select a CubeID .bin image.");
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(chunk, token)) != 0)
        {
            if (buffer.Length + count > CubeIdFirmwareService.MaximumImageBytes) throw new InvalidOperationException("CubeID images are limited to 4 MiB.");
            buffer.Write(chunk, 0, count);
        }
        if (buffer.Length == 0) throw new InvalidOperationException("The firmware image is empty.");
        image = new(name, FirmwareSourceKind.Local, buffer.ToArray());
        FirmwareSummary = $"{name} — {image.Data.Length:N0} bytes — CRC32 {image.Crc32:X8}. Verify that this image matches your CubeID hardware.";
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task UpdateAsync(CancellationToken cancellationToken)
    {
        if (IsBusy || image is not { } selected) return;
        IsBusy = true;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        try
        {
            var target = await updater.DetectAsync(linked.Token);
            TargetDetected = true;
            if (!await confirmation.ConfirmPhraseAsync("Update CubeID firmware",
                $"System {target.SystemId}, component {target.ComponentId}: {selected.Name}, {selected.Data.Length:N0} bytes, CRC32 {selected.Crc32:X8}. Confirm this is CubeID hardware and the correct image. Keep power connected and a recovery method available. An interrupted transfer may require recovery.", "UPDATE CUBEID", linked.Token)) return;
            TransferProgress = 0;
            await updater.TransferAsync(target, selected, true, new Progress<double>(value => TransferProgress = value), linked.Token);
            SetMessages("All firmware bytes acknowledged by CubeID. Reconnect and verify the running firmware version; successful boot is not yet verified.", null);
        }
        catch (OperationCanceledException) { SetMessages("Firmware transfer cancelled or timed out. Device firmware state is unconfirmed; use its recovery procedure if it does not reconnect.", null); }
        catch (Exception ex) { SetMessages(ex); }
        finally { IsBusy = false; TargetDetected = false; }
    }

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (lifetime.IsCancellationRequested) { lifetime.Dispose(); lifetime = new(); }
        return base.ActivateAsync();
    }

    /// <inheritdoc />
    public override async Task DeactivateAsync()
    {
        lifetime.Cancel();
        await Task.WhenAll(new[] { RefreshCommand.ExecutionTask, BrowseCommand.ExecutionTask, InspectCommand.ExecutionTask, UpdateCommand.ExecutionTask }.OfType<Task>());
        TargetDetected = false;
        await base.DeactivateAsync();
    }
    /// <inheritdoc />
    public override void Dispose()
    {
        if (disposed) return;
        disposed = true;
        DeactivateAsync().SafeFireAndForget(exception => Logger.LogWarning(exception, "Page operation cleanup failed."));
        base.Dispose();
    }

}
