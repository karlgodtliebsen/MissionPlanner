using System.Globalization;
using System.Text;

namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>Programs one explicitly confirmed, directly attached radio; remote radio flashing is never inferred.</summary>
public sealed class SikFirmwareUpdater(IDirectSerialSessionFactory ports)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    /// <summary>Validates board identity before erase, verifies classic flash readback or XMODEM ACKs, and requests boot only after completion.</summary>
    public async Task<string> UploadAsync(string portName, int applicationBaud, SikFirmwareImage image, bool alreadyInBootloader,
        bool confirmed, IProgress<string>? status, IProgress<double>? progress, CancellationToken token)
    {
        if (!confirmed || string.IsNullOrWhiteSpace(portName) || applicationBaud is < 9600 or > 2_000_000) throw new ArgumentException("Confirm the exact direct radio, port, baud and image before updating.");
        // Re-parse the bounded source so a caller cannot bypass image validation by mutating backing storage.
        image = SikFirmwareImage.Parse(image.Name, image.Source.Span, image.Board);
        await gate.WaitAsync(token).ConfigureAwait(false);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromMinutes(15));
        try
        {
            if (!alreadyInBootloader) await EnterBootloaderAsync(portName, applicationBaud, image.Board, status, timeout.Token).ConfigureAwait(false);
            status?.Report("Opening the radio bootloader; the reported identity must match before any erase/program request.");
            using var opening = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token); opening.CancelAfter(TimeSpan.FromSeconds(5));
            await using var port = await ports.OpenAsync(portName, image.Board.XSeries ? 57600 : 115200, opening.Token).ConfigureAwait(false);
            var io = new RadioIo(port);
            if (image.Board.XSeries)
            {
                await port.WriteAsync("U", timeout.Token).ConfigureAwait(false); await Task.Delay(200, timeout.Token).ConfigureAwait(false);
                await port.WriteAsync("\r\nCHIPID\r\n", timeout.Token).ConfigureAwait(false);
                var identity = await io.FindLineAsync(line => line.StartsWith("RFD", StringComparison.Ordinal), TimeSpan.FromSeconds(3), timeout.Token).ConfigureAwait(false);
                if (!string.Equals(identity, image.Board.ChipId, StringComparison.Ordinal)) throw new IOException($"Bootloader identity '{identity}' does not match {image.Board.ChipId}; nothing programmed.");
                return await UploadXmodemAsync(port, io, image, status, progress, timeout.Token).ConfigureAwait(false);
            }
            await port.WriteBytesAsync(new byte[34], timeout.Token).ConfigureAwait(false);
            await ClassicCommandAsync(port, io, [0x21, 0x20], 0, TimeSpan.FromSeconds(3), timeout.Token).ConfigureAwait(false);
            var device = await ClassicCommandAsync(port, io, [0x22, 0x20], 2, TimeSpan.FromSeconds(3), timeout.Token).ConfigureAwait(false);
            if (device[0] != image.Board.Id || device[1] is not (0x43 or 0x47 or 0x86 or 0x91)) throw new IOException($"Bootloader board/frequency {device[0]:X2}/{device[1]:X2} is unsupported or does not match the selected board. Nothing erased.");
            status?.Report($"Confirmed {image.Board.Name}, frequency code 0x{device[1]:X2}. Erasing application only; parameter erase is not requested.");
            await ClassicCommandAsync(port, io, [0x23, 0x20], 0, TimeSpan.FromSeconds(15), timeout.Token).ConfigureAwait(false);
            var data = image.Bytes.Where(pair => pair.Key != image.Board.SignatureAddress && pair.Key != image.Board.SignatureAddress + 1).ToArray();
            var done = 0; var total = data.Length * 2 + 4;
            foreach (var (address, bytes) in SikFirmwareImage.Chunks(data, 32))
            {
                await AddressAsync(port, io, image.Board, address, timeout.Token).ConfigureAwait(false);
                await ClassicCommandAsync(port, io, [0x27, (byte)bytes.Length, .. bytes, 0x20], 0, TimeSpan.FromSeconds(3), timeout.Token).ConfigureAwait(false);
                done += bytes.Length; progress?.Report(100d * done / total);
            }
            status?.Report("Reading back all programmed bytes before writing the application-valid signature.");
            foreach (var (address, bytes) in SikFirmwareImage.Chunks(data, 128))
            {
                await VerifyAsync(port, io, image.Board, address, bytes, timeout.Token).ConfigureAwait(false);
                done += bytes.Length; progress?.Report(100d * done / total);
            }
            await AddressAsync(port, io, image.Board, image.Board.SignatureAddress, timeout.Token).ConfigureAwait(false);
            await ClassicCommandAsync(port, io, [0x27, 2, 0x3D, 0xC2, 0x20], 0, TimeSpan.FromSeconds(3), timeout.Token).ConfigureAwait(false);
            await VerifyAsync(port, io, image.Board, image.Board.SignatureAddress, [0x3D, 0xC2], timeout.Token).ConfigureAwait(false);
            await port.WriteBytesAsync(new byte[] { 0x30 }, timeout.Token).ConfigureAwait(false); progress?.Report(100);
            return "All classic SiK application bytes and final validity signature matched flash readback. Reboot requested; reconnect at the application baud to verify the running firmware. Stored radio parameters were not explicitly erased.";
        }
        catch (OperationCanceledException) { throw new OperationCanceledException("Radio update cancelled or timed out. If erase/programming began, leave the radio in bootloader recovery and retry a matching complete image; no automatic reboot was sent after failure.", token); }
        finally { gate.Release(); }
    }
    private async Task EnterBootloaderAsync(string name, int baud, SikFirmwareBoard board, IProgress<string>? status, CancellationToken token)
    {
        using var opening = CancellationTokenSource.CreateLinkedTokenSource(token); opening.CancelAfter(TimeSpan.FromSeconds(5));
        await using var port = await ports.OpenAsync(name, baud, opening.Token).ConfigureAwait(false);
        var io = new RadioIo(port); var entered = false; var updating = false;
        try
        {
            status?.Report("Entering the explicitly selected local radio's command mode.");
            await Task.Delay(1100, token).ConfigureAwait(false); await port.WriteAsync("+++", token).ConfigureAwait(false);
            await io.FindLineAsync(line => line == "OK", TimeSpan.FromSeconds(3), token).ConfigureAwait(false); entered = true;
            await port.WriteAsync("ATI\r\n", token).ConfigureAwait(false);
            var banner = await io.FindLineAsync(line => line.StartsWith("SiK ", StringComparison.OrdinalIgnoreCase) || line.Contains("RFD", StringComparison.OrdinalIgnoreCase), TimeSpan.FromSeconds(3), token).ConfigureAwait(false);
            await port.WriteAsync("ATI2\r\n", token).ConfigureAwait(false);
            var identity = await io.FindLineAsync(line => byte.TryParse(line, NumberStyles.None, CultureInfo.InvariantCulture, out _), TimeSpan.FromSeconds(3), token).ConfigureAwait(false);
            if (byte.Parse(identity, CultureInfo.InvariantCulture) != board.Id) throw new IOException($"Radio identity {identity} does not match selected {board.Name}. Update mode was not requested.");
            status?.Report($"{banner}: requesting update mode. This interrupts the radio link.");
            updating = true; await port.WriteAsync("AT&UPDATE\r\n", token).ConfigureAwait(false); await Task.Delay(700, token).ConfigureAwait(false);
        }
        finally
        {
            if (entered && !updating)
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                try { await port.WriteAsync("ATO\r\n", cleanup.Token).ConfigureAwait(false); }
                catch (Exception ex) when (ex is IOException or OperationCanceledException) { }
            }
        }
    }
    private static Task<byte[]> AddressAsync(IDirectSerialSession port, RadioIo io, SikFirmwareBoard board, uint address, CancellationToken token) =>
        ClassicCommandAsync(port, io, board.Banked ? [0x24, (byte)address, (byte)(address >> 8), (byte)(address >> 16), 0x20] : [0x24, (byte)address, (byte)(address >> 8), 0x20], 0, TimeSpan.FromSeconds(3), token);
    private static async Task VerifyAsync(IDirectSerialSession port, RadioIo io, SikFirmwareBoard board, uint address, byte[] expected, CancellationToken token)
    {
        await AddressAsync(port, io, board, address, token).ConfigureAwait(false);
        var actual = await ClassicCommandAsync(port, io, [0x28, (byte)expected.Length, 0x20], expected.Length, TimeSpan.FromSeconds(3), token).ConfigureAwait(false);
        if (!actual.AsSpan().SequenceEqual(expected)) throw new IOException($"Radio flash readback failed at 0x{address:X}. No validity signature/reboot will be requested after this failure; use bootloader recovery.");
    }
    private static async Task<byte[]> ClassicCommandAsync(IDirectSerialSession port, RadioIo io, byte[] command, int count, TimeSpan duration, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(duration);
        await port.WriteBytesAsync(command, timeout.Token).ConfigureAwait(false);
        var result = new byte[count]; for (var i = 0; i < count; i++) result[i] = await io.ReadByteAsync(timeout.Token).ConfigureAwait(false);
        if (await io.ReadByteAsync(timeout.Token).ConfigureAwait(false) != 0x12 || await io.ReadByteAsync(timeout.Token).ConfigureAwait(false) != 0x10)
            throw new IOException("SiK bootloader did not acknowledge the operation. It is not automatically replayed.");
        return result;
    }
    private static async Task<string> UploadXmodemAsync(IDirectSerialSession port, RadioIo io, SikFirmwareImage image, IProgress<string>? status, IProgress<double>? progress, CancellationToken token)
    {
        var started = false; var complete = false;
        try
        {
            status?.Report("RFD bootloader identity matched. Requesting its XMODEM-CRC upload channel.");
            started = true; await port.WriteAsync("UPLOAD\r", token).ConfigureAwait(false);
            await io.FindLineAsync(line => line == "Ready", TimeSpan.FromSeconds(5), token).ConfigureAwait(false);
            using (var ready = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                ready.CancelAfter(TimeSpan.FromSeconds(5));
                if (await io.ReadByteAsync(ready.Token).ConfigureAwait(false) != 0x43) throw new IOException("RFD bootloader did not request XMODEM-CRC.");
            }
            var source = image.Source;
            for (var offset = 0; offset < source.Length; offset += 128)
            {
                var packet = RfdXmodemCodec.Block(unchecked((byte)(offset / 128 + 1)), source.Span.Slice(offset, Math.Min(128, source.Length - offset)));
                await SendXmodemAsync(port, io, packet, token).ConfigureAwait(false); progress?.Report(100d * Math.Min(offset + 128, source.Length) / source.Length);
            }
            await SendXmodemAsync(port, io, [4], token).ConfigureAwait(false); complete = true;
            await port.WriteAsync("\r\nBOOTNEW\r\n", token).ConfigureAwait(false);
            return "RFD bootloader acknowledged every CRC block and end-of-transfer. BOOTNEW requested. This protocol provides no flash readback here; reconnect and verify running firmware, hardware revision and radio link.";
        }
        finally
        {
            if (started && !complete)
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                try { await port.WriteBytesAsync(new byte[] { 0x18, 0x18 }, cleanup.Token).ConfigureAwait(false); }
                catch (Exception ex) when (ex is IOException or OperationCanceledException) { }
            }
        }
    }
    private static async Task SendXmodemAsync(IDirectSerialSession port, RadioIo io, byte[] bytes, CancellationToken token)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(3));
            try
            {
                await port.WriteBytesAsync(bytes, timeout.Token).ConfigureAwait(false);
                byte response; do { response = await io.ReadByteAsync(timeout.Token).ConfigureAwait(false); } while (response == 0x43);
                if (response == 0x06) return;
                if (response == 0x18) throw new IOException("RFD bootloader cancelled the transfer.");
                if (response != 0x15) throw new IOException("Unexpected RFD upload response; stopped without requesting boot.");
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { }
        }
        throw new IOException("RFD bootloader did not acknowledge a block after ten identical attempts.");
    }
    private sealed class RadioIo(IDirectSerialSession port)
    {
        private readonly byte[] buffer = new byte[512];
        private int offset, length;
        internal async Task<byte> ReadByteAsync(CancellationToken token)
        {
            if (offset == length) { length = await port.ReadAsync(buffer, token).ConfigureAwait(false); offset = 0; if (length == 0) throw new EndOfStreamException("Radio port closed."); }
            return buffer[offset++];
        }
        internal async Task<string> FindLineAsync(Func<string, bool> predicate, TimeSpan duration, CancellationToken token)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(duration);
            var line = new StringBuilder();
            for (var bytes = 0; bytes < 8192; bytes++)
            {
                var value = await ReadByteAsync(timeout.Token).ConfigureAwait(false);
                if (value == '\n')
                {
                    var text = line.ToString().Trim(); line.Clear();
                    // Multipoint firmware prefixes command responses with [node].
                    if (text.StartsWith('[') && text.IndexOf(']') is > 0 and < 8) text = text[(text.IndexOf(']') + 1)..].Trim();
                    if (predicate(text)) return text;
                }
                else if (value != '\r') { if (line.Length >= 256 || value > 127) throw new InvalidDataException("Unexpected radio command response."); line.Append((char)value); }
            }
            throw new IOException("Too much unexpected radio response data.");
        }
    }
}
