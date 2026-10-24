using System.Buffers.Binary;
using System.Text;

namespace MissionPlanner.Core.Setup.OptionalHardware.DroneCan;

public sealed partial class DroneCanTransport
{
    private FirmwareTransfer? firmware;
    private sealed class FirmwareTransfer(byte node, string path, byte[] data, IProgress<double>? progress)
    {
        internal readonly byte Node = node;
        internal readonly string Path = path;
        internal readonly byte[] Data = data;
        internal readonly IProgress<double>? Progress = progress;
        internal readonly TaskCompletionSource<bool> Complete = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal uint Contiguous;
        internal int Requests;
    }
    /// <inheritdoc />
    public async Task<string> UpdateFirmwareAsync(byte node, string name, byte[] image, bool confirmed, IProgress<double>? progress, CancellationToken token)
    {
        if (!confirmed || !name.EndsWith(".bin", StringComparison.OrdinalIgnoreCase) || image.Length is 0 or > 4 * 1024 * 1024)
            throw new ArgumentException("Select a matching .bin image of at most 4 MiB and explicitly confirm the target hardware.");
        await operations.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var expected = RequireNode(node);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token); timeout.CancelAfter(TimeSpan.FromMinutes(10));
            await VerifyNodeAsync(expected, timeout.Token).ConfigureAwait(false);
            var transfer = new FirmwareTransfer(node, "fw-" + Guid.NewGuid().ToString("N") + ".bin", image.ToArray(), progress);
            lock (sync) firmware = transfer;
            var response = await RequestAsync(node, 40, [options.LocalNodeId, .. Encoding.ASCII.GetBytes(transfer.Path)], timeout.Token).ConfigureAwait(false);
            if (response.Length < 1 || response[0] != 0) throw new IOException($"The node rejected firmware update (error {(response.Length > 0 ? response[0] : -1)}).");
            await transfer.Complete.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
            lock (sync) InvalidateParameters(node);
            progress?.Report(100);
            return "All firmware bytes and the end-of-file response were served to the selected node. Flash programming and a successful reboot are not acknowledged by File.Read. Refresh node identity/version after it restarts; do not disconnect power while programming.";
        }
        finally { lock (sync) firmware = null; operations.Release(); }
    }
    private byte[] ReadFirmware(DroneCanTransfer request)
    {
        lock (sync)
        {
            if (firmware is not { } current || current.Node != request.Source || request.Payload.Length is < 6 or > 205 ||
                !request.Payload.AsSpan(5).SequenceEqual(Encoding.ASCII.GetBytes(current.Path))) return [2, 0]; // NOT_FOUND
            var offset = (ulong)BinaryPrimitives.ReadUInt32LittleEndian(request.Payload) | (ulong)request.Payload[4] << 32;
            if (offset > (ulong)current.Data.Length || offset > current.Contiguous || ++current.Requests > 100_000)
            {
                current.Complete.TrySetException(new IOException("The node requested a gap, invalid offset or excessive firmware retries.")); return [22, 0];
            }
            var count = Math.Min(256, current.Data.Length - (int)offset);
            return [0, 0, .. current.Data.AsSpan((int)offset, count)];
        }
    }
    private void FirmwareResponseSent(DroneCanTransfer request, byte[] response)
    {
        if (request.TypeId != 48 || response.Length < 2 || response[0] != 0 || response[1] != 0) return;
        lock (sync)
        {
            if (firmware is not { } current || current.Node != request.Source) return;
            var offset = BinaryPrimitives.ReadUInt32LittleEndian(request.Payload); var count = response.Length - 2;
            current.Contiguous = Math.Max(current.Contiguous, offset + (uint)count);
            current.Progress?.Report(100d * current.Contiguous / current.Data.Length);
            // A short final block is EOF; an exact 256-byte multiple requires a subsequent empty block.
            if (count < 256 && current.Contiguous == current.Data.Length) current.Complete.TrySetResult(true);
        }
    }
}
