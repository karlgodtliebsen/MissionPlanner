using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.Core.Notifications;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.MavLink;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <summary>
/// Identifies the source stream of an item displayed in the Messages tab.
/// </summary>
public enum MessageListOrigin
{
    /// <summary>The message originated as MAVLink STATUSTEXT.</summary>
    MavLink,

    /// <summary>The message originated in an application workflow.</summary>
    Application
}

/// <summary>
/// Represents one display and export row in the Messages tab.
/// </summary>
/// <param name="Identity">The source-qualified stable identity.</param>
/// <param name="Origin">The source stream.</param>
/// <param name="ReceivedAt">The complete timestamp.</param>
/// <param name="Source">The source identity.</param>
/// <param name="Severity">The filterable severity name.</param>
/// <param name="SeverityDisplay">The severity label and non-color marker.</param>
/// <param name="Text">The message text.</param>
/// <param name="IsAssembled">Whether MAVLink chunks were assembled.</param>
/// <param name="IsTruncated">Whether the message is explicitly incomplete.</param>
public sealed record MessageListItem(
    string Identity,
    MessageListOrigin Origin,
    DateTimeOffset ReceivedAt,
    string Source,
    string Severity,
    string SeverityDisplay,
    string Text,
    bool IsAssembled,
    bool IsTruncated)
{
    /// <summary>Gets a concise source/chunk detail for the row.</summary>
    public string Details => $"{ReceivedAt:O}  {Source}" +
        (IsAssembled ? "  • assembled" : string.Empty) +
        (IsTruncated ? "  • TRUNCATED" : string.Empty);
}

/// <summary>
/// Presents a bounded, filterable active-vehicle message history with separate MAVLink and application origins.
/// </summary>
public partial class MessagesTabViewModel : ObservableObject, IFlightDataTabLifecycle, IDisposable
{
    private static readonly JsonSerializerOptions jsonOptions = new() { WriteIndented = true };
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IVehicleMessageStore vehicleMessages;
    private readonly IApplicationNotificationStore applicationMessages;
    private readonly ITextClipboardService clipboard;
    private readonly IFileSaver fileSaver;
    private readonly IDispatcher dispatcher;
    private readonly ILogger<MessagesTabViewModel> logger;
    private readonly FlightDataTabLifecycle lifecycle;

    /// <summary>Initializes a Messages-tab view model.</summary>
    /// <param name="activeVehicle">The active-vehicle context.</param>
    /// <param name="vehicleMessages">The bounded MAVLink status-text history.</param>
    /// <param name="applicationMessages">The separate local application-notification history.</param>
    /// <param name="clipboard">The platform-neutral clipboard adapter.</param>
    /// <param name="fileSaver">The platform file saver.</param>
    /// <param name="dispatcher">The UI dispatcher.</param>
    /// <param name="logger">The logger.</param>
    public MessagesTabViewModel(
        IActiveVehicleContext activeVehicle,
        IVehicleMessageStore vehicleMessages,
        IApplicationNotificationStore applicationMessages,
        ITextClipboardService clipboard,
        IFileSaver fileSaver,
        IDispatcher dispatcher,
        ILogger<MessagesTabViewModel> logger)
    {
        this.activeVehicle = activeVehicle;
        this.vehicleMessages = vehicleMessages;
        this.applicationMessages = applicationMessages;
        this.clipboard = clipboard;
        this.fileSaver = fileSaver;
        this.dispatcher = dispatcher;
        this.logger = logger;
        lifecycle = new FlightDataTabLifecycle("Messages", activeVehicle, startAsync: _ =>
        {
            vehicleMessages.MessageAdded += OnVehicleMessageAdded;
            applicationMessages.NotificationAdded += OnApplicationMessageAdded;
            dispatcher.Dispatch(Refresh);
            return Task.FromResult<IDisposable?>(new CallbackDisposable(() =>
            {
                vehicleMessages.MessageAdded -= OnVehicleMessageAdded;
                applicationMessages.NotificationAdded -= OnApplicationMessageAdded;
            }));
        });
        Refresh();
    }

    /// <inheritdoc />
    public string Key => lifecycle.Key;

    /// <inheritdoc />
    public bool IsActive => lifecycle.IsActive;

    /// <inheritdoc />
    public bool IsInitialized => lifecycle.IsInitialized;

    /// <summary>Gets all available exact-severity filters.</summary>
    public IReadOnlyList<string> SeverityFilters { get; } =
        ["All", "Emergency", "Alert", "Critical", "Error", "Warning", "Notice", "Info", "Debug", "Application"];

    /// <summary>Gets the filtered current-vehicle rows in arrival order.</summary>
    public ObservableCollection<MessageListItem> Items { get; } = [];

    /// <summary>Gets or sets the selected exact severity or origin filter.</summary>
    [ObservableProperty]
    public partial string SelectedSeverity { get; set; } = "All";

    /// <summary>Gets or sets case-insensitive text search.</summary>
    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    /// <summary>Gets or sets the selected message row.</summary>
    [ObservableProperty]
    public partial MessageListItem? SelectedMessage { get; set; }

    /// <summary>Gets whether automatic scrolling is paused.</summary>
    [ObservableProperty]
    public partial bool IsAutoScrollPaused { get; private set; }

    /// <summary>Gets a monotonically increasing request used by the view for auto-scroll.</summary>
    [ObservableProperty]
    public partial int ScrollRequestVersion { get; private set; }

    /// <summary>Gets the latest copy/export status.</summary>
    [ObservableProperty]
    public partial string Status { get; private set; } = "No messages";

    /// <summary>Gets the pause/resume button label.</summary>
    public string PauseButtonText => IsAutoScrollPaused ? "Resume Auto-scroll" : "Pause Auto-scroll";

    /// <inheritdoc />
    public Task ActivateAsync(CancellationToken cancellationToken = default) => lifecycle.ActivateAsync(cancellationToken);

    /// <inheritdoc />
    public Task DeactivateAsync() => lifecycle.DeactivateAsync();

    /// <summary>Creates a UTF-8-ready text export of the current filtered view.</summary>
    /// <returns>The complete timestamped text representation.</returns>
    public string CreateTextExport() => string.Join(
        Environment.NewLine,
        Items.Select(item =>
            $"{item.ReceivedAt:O}\t{item.Origin}\t{item.Source}\t{item.Severity}\t{EscapeText(item.Text)}\tassembled={item.IsAssembled}\ttruncated={item.IsTruncated}"));

    /// <summary>Creates a JSON export of the current filtered view.</summary>
    /// <returns>The indented JSON representation.</returns>
    public string CreateJsonExport() => JsonSerializer.Serialize(Items, jsonOptions);

    /// <inheritdoc />
    public void Dispose() => lifecycle.DisposeAsync().AsTask().GetAwaiter().GetResult();

    partial void OnSelectedSeverityChanged(string value) => Refresh();

    partial void OnSearchTextChanged(string value) => Refresh();

    partial void OnIsAutoScrollPausedChanged(bool value) => OnPropertyChanged(nameof(PauseButtonText));

    [RelayCommand]
    private void TogglePause()
    {
        IsAutoScrollPaused = !IsAutoScrollPaused;
        if (!IsAutoScrollPaused && Items.Count > 0)
        {
            ScrollRequestVersion++;
        }
    }

    [RelayCommand]
    private void ClearCurrentView()
    {
        if (activeVehicle.VehicleId is not { } vehicleId)
        {
            return;
        }

        var vehicleIdentities = Items
            .Where(item => item.Origin == MessageListOrigin.MavLink)
            .Select(item => long.Parse(item.Identity.AsSpan(4), CultureInfo.InvariantCulture))
            .ToHashSet();
        var applicationIdentities = Items
            .Where(item => item.Origin == MessageListOrigin.Application)
            .Select(item => long.Parse(item.Identity.AsSpan(4), CultureInfo.InvariantCulture))
            .ToHashSet();
        vehicleMessages.Clear(vehicleId, message => vehicleIdentities.Contains(message.Identity));
        applicationMessages.Clear(vehicleId, message => applicationIdentities.Contains(message.Identity));
        Refresh();
        Status = "Cleared the current filtered view.";
    }

    [RelayCommand]
    private async Task CopySelectedAsync()
    {
        if (SelectedMessage is null)
        {
            Status = "Select a message to copy.";
            return;
        }

        await clipboard.SetTextAsync(FormatRow(SelectedMessage));
        Status = "Selected message copied.";
    }

    [RelayCommand]
    private async Task CopyAllAsync()
    {
        await clipboard.SetTextAsync(CreateTextExport());
        Status = $"Copied {Items.Count} visible messages.";
    }

    [RelayCommand]
    private Task ExportTextAsync(CancellationToken cancellationToken) =>
        ExportAsync("missionplanner-messages.txt", CreateTextExport(), cancellationToken);

    [RelayCommand]
    private Task ExportJsonAsync(CancellationToken cancellationToken) =>
        ExportAsync("missionplanner-messages.json", CreateJsonExport(), cancellationToken);

    private async Task ExportAsync(string fileName, string content, CancellationToken cancellationToken)
    {
        logger.LogInformation("Exporting {MessageCount} visible messages to {FileName}.", Items.Count, fileName);
        try
        {
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            var result = await fileSaver.SaveAsync(fileName, stream, cancellationToken);
            Status = result.IsSuccessful ? $"Exported to {result.FilePath}." : "Export cancelled.";
        }
        catch (OperationCanceledException)
        {
            Status = "Export cancelled.";
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Message export failed.");
            Status = $"Export failed: {exception.Message}";
        }
    }

    private void OnVehicleMessageAdded(object? sender, VehicleStatusTextAddedEventArgs args)
    {
        if (args.Message.VehicleId == activeVehicle.VehicleId)
        {
            dispatcher.Dispatch(RefreshAfterAppend);
        }
    }

    private void OnApplicationMessageAdded(object? sender, ApplicationNotificationAddedEventArgs args)
    {
        if (activeVehicle.VehicleId is { } vehicleId &&
            (args.Notification.VehicleId is null || args.Notification.VehicleId == vehicleId))
        {
            dispatcher.Dispatch(RefreshAfterAppend);
        }
    }

    private void RefreshAfterAppend()
    {
        Refresh();
        if (!IsAutoScrollPaused && Items.Count > 0)
        {
            ScrollRequestVersion++;
        }
    }

    private void Refresh()
    {
        var selectedIdentity = SelectedMessage?.Identity;
        Items.Clear();
        if (activeVehicle.VehicleId is not { } vehicleId)
        {
            Status = "No active vehicle";
            return;
        }

        var rows = vehicleMessages.GetMessages(vehicleId).Select(ToRow)
            .Concat(applicationMessages.GetMessages(vehicleId).Select(ToRow))
            .Where(MatchesFilter)
            .OrderBy(row => row.ReceivedAt)
            .ThenBy(row => row.Identity);
        foreach (var row in rows)
        {
            Items.Add(row);
        }

        SelectedMessage = Items.FirstOrDefault(item => item.Identity == selectedIdentity);
        Status = $"{Items.Count} visible messages for {activeVehicle.Current.DisplayName}.";
    }

    private bool MatchesFilter(MessageListItem item)
    {
        var severityMatches = SelectedSeverity == "All" ||
            (SelectedSeverity == "Application" && item.Origin == MessageListOrigin.Application) ||
            string.Equals(item.Severity, SelectedSeverity, StringComparison.OrdinalIgnoreCase);
        return severityMatches &&
            (string.IsNullOrWhiteSpace(SearchText) ||
             item.Text.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase) ||
             item.Source.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static MessageListItem ToRow(VehicleStatusText message) =>
        new(
            $"mav:{message.Identity}",
            MessageListOrigin.MavLink,
            message.ReceivedAt,
            $"MAVLink {message.SourceSystemId}:{message.SourceComponentId}" +
            (message.ProtocolId is { } id ? $" ID {id}" : string.Empty),
            message.Severity.ToString(),
            SeverityDisplay(message.Severity),
            message.Text,
            message.IsAssembled,
            message.IsTruncated);

    private static MessageListItem ToRow(ApplicationNotificationEntry message) =>
        new(
            $"app:{message.Identity}",
            MessageListOrigin.Application,
            message.ReceivedAt,
            message.Title is null ? "Application" : $"Application: {message.Title}",
            "Application",
            message.Severity switch
            {
                UserNotificationSeverity.Error => "✖ APP ERROR",
                UserNotificationSeverity.Warning => "⚠ APP WARNING",
                _ => "ℹ APP INFO"
            },
            message.Message,
            false,
            false);

    private static string SeverityDisplay(MavSeverity severity) => severity switch
    {
        MavSeverity.Emergency => "⛔ EMERGENCY",
        MavSeverity.Alert => "⛔ ALERT",
        MavSeverity.Critical => "● CRITICAL",
        MavSeverity.Error => "✖ ERROR",
        MavSeverity.Warning => "⚠ WARNING",
        MavSeverity.Notice => "◆ NOTICE",
        MavSeverity.Info => "ℹ INFO",
        _ => "· DEBUG"
    };

    private static string EscapeText(string text) => text.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);

    private static string FormatRow(MessageListItem item) =>
        $"{item.ReceivedAt:O} [{item.SeverityDisplay}] {item.Source}: {item.Text}";

    private sealed class CallbackDisposable(Action callback) : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                callback();
            }
        }
    }
}
