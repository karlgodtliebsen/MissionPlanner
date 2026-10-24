using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.Core.Setup.OptionalHardware;
using MissionPlanner.Firmware.Devices;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>Reviews and updates one directly attached radio with an explicit board/image match.</summary>
public sealed partial class SikFirmwareViewModel(SikFirmwareUpdater updater, IFirmwareSerialDeviceCatalog devices,
    IFileOpenService files, IUserConfirmationService confirmation, ILogger<SikFirmwareViewModel> logger) : OptionalHardwareBaseViewModel(logger)
{
    private CancellationTokenSource lifetime = new();
    private SikFirmwareImage? image;
    /// <summary>Gets the available serial ports; selection is never automatic.</summary>
    public ObservableCollection<string> Ports { get; } = [];
    /// <summary>Gets explicitly supported board identities.</summary>
    public IReadOnlyList<SikFirmwareBoard> Boards => SikFirmwareBoard.Supported;
    /// <summary>Gets or sets the directly connected radio port.</summary>
    [ObservableProperty] public partial string? Port { get; set; }
    /// <summary>Gets or sets the application baud used to request bootloader entry.</summary>
    [ObservableProperty] public partial int Baud { get; set; } = 57600;
    /// <summary>Gets or sets the explicitly chosen hardware and bootloader family.</summary>
    [ObservableProperty] public partial SikFirmwareBoard? Board { get; set; }
    partial void OnBoardChanged(SikFirmwareBoard? value) { image = null; ImageSummary = "Choose a firmware file after selecting the exact board/revision."; }
    /// <summary>Gets or sets whether the user has already placed the radio in its bootloader.</summary>
    [ObservableProperty] public partial bool AlreadyInBootloader { get; set; }
    /// <summary>Gets the reviewed image identity and validation scope.</summary>
    [ObservableProperty] public partial string ImageSummary { get; private set; } = "Select the exact board/revision and a local firmware image.";
    /// <summary>Gets transfer/readback progress.</summary>
    [ObservableProperty] public partial double TransferProgress { get; private set; }
    private async Task RunAsync(Func<CancellationToken, Task> action, CancellationToken token)
    {
        if (IsBusy) return; IsBusy = true;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
        try { SetMessages(""); await action(linked.Token); }
        catch (OperationCanceledException) { SetMessages("Cancelled or timed out. If erase/programming began, the radio may need bootloader recovery. A failed transfer does not request boot; retry a complete matching image."); }
        catch (Exception ex) { SetMessages(ex); }
        finally { IsBusy = false; }
    }
    [RelayCommand(IncludeCancelCommand = true)]
    private Task RefreshPortsAsync(CancellationToken token) => RunAsync(async ct =>
    {
        var result = await devices.GetDevicesAsync(ct); Ports.Clear(); foreach (var device in result) Ports.Add(device.PortName);
        if (Port is not null && !Ports.Contains(Port)) Port = null;
    }, token);
    [RelayCommand(IncludeCancelCommand = true)]
    private Task BrowseAsync(CancellationToken token) => RunAsync(async ct =>
    {
        var board = Board ?? throw new InvalidOperationException("Select the exact radio board and revision first.");
        using var file = await files.OpenAsync("Select matching radio firmware", board.XSeries ? ["*.bin"] : ["*.ihx", "*.hex"], ct); if (file is null) return;
        image = null; ImageSummary = "Reading image…";
        using var memory = new MemoryStream(); var buffer = new byte[8192]; int count;
        while ((count = await file.Content.ReadAsync(buffer, ct)) > 0)
        {
            if (memory.Length + count > 1024 * 1024) throw new InvalidDataException("Radio firmware source exceeds 1 MiB.");
            memory.Write(buffer, 0, count);
        }
        var candidate = SikFirmwareImage.Parse(file.FileName, memory.ToArray(), board);
        if (Board != board) throw new InvalidOperationException("Board selection changed while reading. Select the image again.");
        image = candidate;
        ImageSummary = $"{candidate.Name} · {board.Name} · {candidate.Source.Length:N0} source bytes\nSHA-256 {candidate.Sha256}\n" +
            (board.XSeries ? board.ImageToken.Length == 0 ? "Revision-2 binary is opaque to this tool. Select the manufacturer's image for the exact board/revision; compatibility cannot be inferred from its filename." : "Board and certified-firmware markers found. These markers are not cryptographic proof of compatibility; use the manufacturer's matching image." : "HEX checksums, protected address ranges, board marker and boot-valid signature checked. Confirm this exact hardware/image pairing.");
    }, token);
    [RelayCommand(IncludeCancelCommand = true)]
    private Task UploadAsync(CancellationToken token) => RunAsync(async ct =>
    {
        var selected = image ?? throw new InvalidOperationException("Read a matching firmware image first.");
        var port = Port ?? throw new InvalidOperationException("Select the directly attached radio port.");
        var baud = Baud; var bootloader = AlreadyInBootloader;
        if (Board != selected.Board) throw new InvalidOperationException("Select the image again after changing board.");
        if (!await confirmation.ConfirmAsync("Program directly attached radio",
            $"Port {port}, application baud {baud}, board {selected.Board.Name}. Already in bootloader: {bootloader}.\nImage {selected.Name}\nSHA-256 {selected.Sha256}\nConfirm the exact hardware revision and matching manufacturer firmware. This interrupts the link and changes firmware on the local radio only. Keep stable power. Cancellation after erase/programming may require recovery. Stored parameters are not explicitly erased, but firmware migrations can change them; record settings first. No temporary reference firmware or country-lock bypass is used.", "Program radio", ct)) return;
        TransferProgress = 0;
        SetMessages(await updater.UploadAsync(port, baud, selected, bootloader, true, new Progress<string>(value => SetMessages(value)), new Progress<double>(value => TransferProgress = value), ct));
    }, token);
    /// <inheritdoc />
    public override Task ActivateAsync() { if (lifetime.IsCancellationRequested) { lifetime.Dispose(); lifetime = new(); } return base.ActivateAsync(); }
    /// <inheritdoc />
    public override async Task DeactivateAsync()
    {
        lifetime.Cancel(); await Task.WhenAll(new[] { RefreshPortsCommand.ExecutionTask, BrowseCommand.ExecutionTask, UploadCommand.ExecutionTask }.OfType<Task>());
        await base.DeactivateAsync();
    }
    /// <inheritdoc />
    public override void Dispose() { lifetime.Cancel(); base.Dispose(); }
}
