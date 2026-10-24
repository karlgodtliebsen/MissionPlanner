using CommunityToolkit.Mvvm.Input;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.App.Views.ConfigTuning.Sections;

public partial class MavFtpTabViewModel
{
    [RelayCommand(IncludeCancelCommand = true)]
    private Task UploadAsync(CancellationToken cancellationToken)
    {
        var directory = CurrentPath;
        return ManageAsync(async (files, id, ct) =>
        {
            using var source = await fileOpener.OpenAsync("Upload a new file to the vehicle", cancellationToken: ct);
            if (source is null)
            {
                return;
            }
            var path = RemotePath.Join(directory, EntryName(source.FileName));
            if (!await confirmations.ConfirmAsync("Upload file", $"Create {path} on vehicle {id}? Existing files will be refused. A cancelled transfer may leave a partial file.", "Upload", ct))
            {
                return;
            }
            ct.ThrowIfCancellationRequested();
            var progress = new Progress<VehicleFileTransferProgress>(value =>
            {
                TransferProgress = value.TotalBytes > 0 ? (double)value.BytesTransferred / value.TotalBytes.Value : 0;
                TransferDetails = $"{value.BytesTransferred:N0} / {value.TotalBytes?.ToString("N0") ?? "?"} bytes";
            });
            await files.UploadFileAsync(id, path, source.Content, progress: progress, cancellationToken: ct);
            SetMessages($"Uploaded and CRC verified {path}. Refresh the directory to view it.");
        }, cancellationToken);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task CreateDirectoryAsync(CancellationToken cancellationToken)
    {
        var directory = CurrentPath;
        return ManageAsync(async (files, id, ct) =>
        {
            var name = await dialogService.PromptAsync("Create directory", $"New directory name in {directory}", "", cancellationToken: ct);
            if (name is null)
            {
                return;
            }
            var path = RemotePath.Join(directory, EntryName(name));
            await files.CreateDirectoryAsync(id, path, ct);
            SetMessages($"Created {path}. Refresh the directory to view it.");
        }, cancellationToken);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task RemoveSelectedAsync(CancellationToken cancellationToken)
    {
        var entry = SelectedEntry;
        var path = entry is null ? null : RemotePath.Join(CurrentPath, entry.Name);
        return ManageAsync(async (files, id, ct) =>
        {
            if (entry is null || path is null)
            {
                SetMessages("Select a file or empty directory first.");
                return;
            }
            if (await confirmations.ConfirmAsync("Delete remote entry", $"Permanently delete {path} on vehicle {id}? Directories must be empty.", "Delete", ct))
            {
                await files.RemoveAsync(id, path, entry.IsDirectory, ct);
                Entries.Remove(entry);
                HasEntries = Entries.Count > 0;
                SelectedEntry = null;
                SetMessages($"Deleted {path}.");
            }
        }, cancellationToken);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task RenameSelectedAsync(CancellationToken cancellationToken)
    {
        var entry = SelectedEntry;
        var directory = CurrentPath;
        return ManageAsync(async (files, id, ct) =>
        {
            if (entry is null)
            {
                SetMessages("Select an entry first.");
                return;
            }
            var name = await dialogService.PromptAsync("Rename remote entry", $"New name for {entry.Name}", entry.Name, cancellationToken: ct);
            if (name is null || name == entry.Name)
            {
                return;
            }
            var destination = RemotePath.Join(directory, EntryName(name));
            if (await confirmations.ConfirmAsync("Rename remote entry", $"Rename {entry.Name} to {destination} on vehicle {id}?", "Rename", ct))
            {
                await files.RenameAsync(id, RemotePath.Join(directory, entry.Name), destination, ct);
                SetMessages($"Renamed to {destination}. Refresh the directory to view it.");
            }
        }, cancellationToken);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task CheckCrcAsync(CancellationToken cancellationToken)
    {
        var entry = SelectedEntry;
        var directory = CurrentPath;
        return ManageAsync(async (files, id, ct) =>
        {
            if (entry is null || entry.IsDirectory)
            {
                SetMessages("Select a file first.");
                return;
            }
            var crc = await files.CalculateCrc32Async(id, RemotePath.Join(directory, entry.Name), ct);
            SetMessages($"{entry.Name}: remote CRC-32 {crc:X8}.");
        }, cancellationToken);
    }

    private async Task ManageAsync(Func<IVehicleFileSystemService, VehicleId, CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        if (!activeContext.IsOnline || activeContext.VehicleId is not { } id || fileSystem is not { } files || IsBusy)
        {
            SetMessages("Connect and select a vehicle, then wait for the current operation to finish.");
            return;
        }
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, activeContext.ConnectionCancellationToken);
        await RunAsync(lifetime.Token, async ct =>
        {
            if (activeContext.VehicleId != id || fileSystem != files)
            {
                throw new OperationCanceledException("Vehicle changed.", ct);
            }
            await action(files, id, ct);
        });
    }

    private static string EntryName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value is "." or ".." || value.Any(character => char.IsControl(character) || character is '/' or '\\'))
        {
            throw new IOException("Enter a single file or directory name without path separators.");
        }
        return value;
    }
}
