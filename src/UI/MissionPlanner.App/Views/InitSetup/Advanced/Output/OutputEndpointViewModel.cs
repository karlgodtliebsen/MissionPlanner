using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Core.Setup.Advanced.Output;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.Advanced.Output;

/// <summary>Reusable output profile editor, with native capability checks supplied by the platform.</summary>
public sealed partial class OutputEndpointViewModel(IOutputSinkFactory factory, ILogger<OutputEndpointViewModel> logger,
    IUiDispatcher dispatcher, IDomainEventHub events) : ViewModelBase(logger, dispatcher, events)
{
    /// <summary>Signals a profile change to its active parent.</summary>
    public event Action<OutputEndpoint>? ProfileChanged;
    /// <summary>Gets the supported kinds.</summary>
    public IReadOnlyList<OutputEndpointKind> Kinds { get; } = Enum.GetValues<OutputEndpointKind>();
    /// <summary>Gets or sets the output kind.</summary>
    [ObservableProperty] public partial OutputEndpointKind Kind { get; set; } = OutputEndpointKind.Udp;
    /// <summary>Gets or sets the serial port or network host.</summary>
    [ObservableProperty] public partial string Address { get; set; } = "127.0.0.1";
    /// <summary>Gets or sets the destination port.</summary>
    [ObservableProperty] public partial int Port { get; set; } = 14551;
    /// <summary>Gets or sets the serial baud rate.</summary>
    [ObservableProperty] public partial int BaudRate { get; set; } = 57600;
    /// <summary>Gets or sets the total reconnect budget.</summary>
    [ObservableProperty] public partial int ReconnectAttempts { get; set; }
    /// <summary>Gets or sets the delay between reconnect attempts.</summary>
    [ObservableProperty] public partial int ReconnectDelaySeconds { get; set; } = 2;
    /// <summary>Gets or sets the native write timeout.</summary>
    [ObservableProperty] public partial int WriteTimeoutSeconds { get; set; } = 3;
    /// <summary>Gets or sets whether an active parent owns this profile.</summary>
    [ObservableProperty] public partial bool Locked { get; set; }
    /// <summary>Gets current validation feedback.</summary>
    [ObservableProperty] public partial string Validation { get; private set; } = "Choose an output-only endpoint.";
    /// <summary>Creates an immutable profile.</summary>
    public OutputEndpoint Profile() => new(Kind, Address, Port, BaudRate);
    /// <summary>Creates bounded queue/reconnect settings.</summary>
    public OutputSessionOptions Options() => new(256, ReconnectAttempts, ReconnectDelaySeconds, WriteTimeoutSeconds);
    /// <summary>Validates without opening a native handle.</summary>
    public string? Validate() => Profile().Validate() ?? factory.UnavailableReason(Profile());
    /// <inheritdoc />
    public override Task ActivateAsync() { Refresh(); return Task.CompletedTask; }
    partial void OnKindChanged(OutputEndpointKind value) => Refresh();
    partial void OnAddressChanged(string value) => Refresh();
    partial void OnPortChanged(int value) => Refresh();
    partial void OnBaudRateChanged(int value) => Refresh();
    private void Refresh()
    {
        Validation = Validate() ?? "Output endpoint is valid. No listener or return path is opened.";
        ProfileChanged?.Invoke(Profile());
    }
}
