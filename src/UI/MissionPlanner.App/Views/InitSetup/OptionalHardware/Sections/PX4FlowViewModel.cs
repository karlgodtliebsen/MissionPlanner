using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Core.Setup.OptionalHardware;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>Live PX4Flow imagery and sensor lens-focus mode.</summary>
public sealed partial class PX4FlowViewModel(
    IActiveVehicleContext active, IVehicleParameterService parameters,
    IVehicleParameterRegistry registry, IEventHub events,
    ILogger<PX4FlowViewModel> logger, IUiDispatcher dispatcher, IDomainEventHub domainEvents) : OptionalHardwareBaseViewModel(logger, dispatcher, domainEvents)
{
    private readonly PX4FlowImageAssembler assembler = new();
    private IDisposable? subscription;
    private CancellationTokenSource? lifetime;
    private bool opened;
    private Task stopping = Task.CompletedTask;
    private bool focusRequested;

    [ObservableProperty] public partial WriteableBitmap? Image { get; private set; }
    [ObservableProperty] public partial bool IsFocusMode { get; private set; }
    [ObservableProperty] public partial string ImageStatus { get; private set; } = "Waiting for a PX4Flow image stream.";
    [ObservableProperty] public partial bool IsExpanded { get; set; }
    [ObservableProperty] public partial bool CanFocus { get; private set; }

    private void UpdateAvailability()
    {
        CanFocus = opened && active.IsOnline && !IsBusy && active.VehicleId is { } id &&
            registry.GetParameter(id, "VIDEO_ONLY") is not null;

    }

    [RelayCommand]
    private async Task ToggleFocusAsync()
    {
        if (!CanFocus || active.VehicleId is not { } id || lifetime is null) return;
        var parameter = registry.GetParameter(id, "VIDEO_ONLY");
        if (parameter is null) return;
        var enable = !IsFocusMode;
        using var request = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, active.ConnectionCancellationToken);
        IsBusy = true;
        UpdateAvailability();
        try
        {
            // Track even an unacknowledged start so leaving the view attempts to restore normal mode.
            focusRequested |= enable;
            if (!await parameters.SetParameterAsync(id, "VIDEO_ONLY", enable ? 1 : 0, parameter.Type, request.Token))
                throw new InvalidOperationException("The PX4Flow focus-mode request could not be sent.");
            request.Token.ThrowIfCancellationRequested();
            IsFocusMode = enable;
            focusRequested = enable;
            SetMessages(enable ? "Focus mode requested. Adjust the lens while watching the image." : "Normal optical-flow mode requested.");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { SetMessages(ex); }
        finally { IsBusy = false; UpdateAvailability(); }
    }

    private Task ReceiveAsync(MavLinkMessage message, CancellationToken token)
    {
        var session = lifetime;
        if (session is null || message.MessageId is not (130 or 131)) return Task.CompletedTask;
        return Dispatcher.DispatchAsync(() =>
        {
            if (!opened || session != lifetime || session.IsCancellationRequested ||
                active.VehicleId is not { } id || message.SystemId != id.SystemId || message.ComponentId != id.ComponentId)
                return;
            if (assembler.Accept(message) is not { } frame) return;
            var bitmap = new WriteableBitmap(new PixelSize(frame.Width, frame.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
            using (var buffer = bitmap.Lock())
            {
                var row = new byte[frame.Width * 4];
                for (var y = 0; y < frame.Height; y++)
                {
                    for (var x = 0; x < frame.Width; x++)
                    {
                        var gray = frame.Pixels[y * frame.Width + x];
                        row[x * 4] = row[x * 4 + 1] = row[x * 4 + 2] = gray;
                        row[x * 4 + 3] = 255;
                    }
                    Marshal.Copy(row, 0, buffer.Address + y * buffer.RowBytes, row.Length);
                }
            }
            var previous = Image;
            Image = bitmap;
            previous?.Dispose();
            ImageStatus = $"{frame.Width} x {frame.Height} - Last image {DateTime.Now:T}";
        });
    }

    private void ParameterChanged(VehicleParameterChangedEventArgs args) => Dispatcher.Dispatch(() =>
    {
        if (!opened || args.VehicleId != active.VehicleId) return;
        if (active.VehicleId is { } id && registry.GetParameter(id, "VIDEO_ONLY") is { } value)
            IsFocusMode = value.Value != 0;
        UpdateAvailability();
    });
    private void VehicleChanged(ActiveVehicleChangedEventArgs args) => Dispatcher.Dispatch(() =>
    {
        if (!opened) return;
        lifetime?.Cancel();
        lifetime?.Dispose();
        lifetime = new CancellationTokenSource();
        assembler.Reset();
        var previous = Image;
        Image = null;
        previous?.Dispose();
        focusRequested = false;
        IsFocusMode = false;
        ImageStatus = "Waiting for a PX4Flow image stream from the selected device.";
        UpdateAvailability();
    });

    /// <inheritdoc />
    public override async Task ActivateAsync()
    {
        await stopping;
        if (opened) return;
        opened = true;
        lifetime = new CancellationTokenSource();
        active.Changed += VehicleChanged;
        registry.Changed += ParameterChanged;
        subscription = events.SubscribeAsync<MavLinkMessage>(MavLinkEventTopics.ReceivedMessage, ReceiveAsync);
        if (active.VehicleId is { } selected && registry.GetParameter(selected, "VIDEO_ONLY") is { } current)
            IsFocusMode = current.Value != 0;
        UpdateAvailability();
        SetMessages("Connect directly to PX4Flow and select its MAVLink component. Focus mode requires VIDEO_ONLY; imagery requires a RAW8 image stream.");
        if (active.IsOnline && active.VehicleId is { } id)
        {
            using var request = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, active.ConnectionCancellationToken);
            try { await parameters.RequestParameterAsync(id, "VIDEO_ONLY", request.Token); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { SetMessages(ex); }
        }
    }

    /// <inheritdoc />
    public override Task DeactivateAsync() => opened ? stopping = CloseAsync() : stopping;

    private async Task CloseAsync()
    {
        if (!opened) return;
        var target = active.VehicleId;
        var connectionToken = active.ConnectionCancellationToken;
        opened = false;
        active.Changed -= VehicleChanged;
        registry.Changed -= ParameterChanged;
        subscription?.Dispose();
        subscription = null;
        lifetime?.Cancel();
        if (ToggleFocusCommand.ExecutionTask is { } running) await running;
        if (focusRequested && !connectionToken.IsCancellationRequested && active.IsOnline && active.VehicleId == target && target is { } id && registry.GetParameter(id, "VIDEO_ONLY") is { } parameter)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(connectionToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            try
            {
                if (!await parameters.SetParameterAsync(id, "VIDEO_ONLY", 0, parameter.Type, timeout.Token))
                    SetMessages("Could not restore normal PX4Flow mode.", "Set VIDEO_ONLY to 0 on the sensor.");
            }
            catch (Exception ex) { SetMessages(ex); }
        }
        focusRequested = false;
        lifetime?.Dispose();
        lifetime = null;
        assembler.Reset();
        var previous = Image;
        Image = null;
        previous?.Dispose();
        CanFocus = false;
    }
}
