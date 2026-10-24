using System.Text;
using AsyncAwaitBestPractices;
using Avalonia;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Core.Setup.Advanced;
using MissionPlanner.Core.Setup.Advanced.Inspector;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.Advanced.Inspector;

/// <summary>Coordinates bounded inspection collection with four display updates per second.</summary>
public sealed partial class MavLinkInspectorViewModel(InspectorListViewModel list, InspectorDetailViewModel detail,
    InspectorSession session, IFileSaveService files, ILogger<MavLinkInspectorViewModel> logger,
    IUiDispatcher dispatcher, IDomainEventHub events) : ViewModelBase(logger, dispatcher, events)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private AdvancedToolLifetime? lifetime;
    private InspectorKey? selected;
    private Task export = Task.CompletedTask;
    private InspectorSnapshot? frozen;
    private readonly InspectorFieldHistory history = new();
    /// <summary>Gets available finite numeric fields for the selected message.</summary>
    [ObservableProperty] public partial IReadOnlyList<string> NumericFields { get; private set; } = [];
    /// <summary>Gets or sets the scalar field plotted at up to four samples per second.</summary>
    [ObservableProperty] public partial string? SelectedField { get; set; }
    /// <summary>Gets the bounded graph with elapsed seconds on the horizontal axis.</summary>
    [ObservableProperty] public partial IReadOnlyList<Point> Graph { get; private set; } = [];
    /// <summary>Gets the selected field and graph sampling policy.</summary>
    public string GraphCaption => $"{SelectedField ?? "Select a numeric field"} · {InspectorFieldHistory.Units(selected, SelectedField)} · {selected?.Direction} {selected?.SystemId}/{selected?.ComponentId} message {selected?.MessageId} · elapsed seconds · ≤4 Hz";

    partial void OnSelectedFieldChanged(string? value)
    {
        history.Clear();
        Graph = [];
        OnPropertyChanged(nameof(GraphCaption));
    }

    /// <summary>Gets the aggregate-list child.</summary>
    public InspectorListViewModel List => list;
    /// <summary>Gets the selected-details child.</summary>
    public InspectorDetailViewModel Detail => detail;
    /// <summary>Gets queue or capacity losses.</summary>
    [ObservableProperty]
    public partial long Dropped { get; private set; }

    /// <inheritdoc />
    public override async Task ActivateAsync()
    {
        await gate.WaitAsync();
        try
        {
            if (lifetime is not null)
            {
                return;
            }
            var activation = new AdvancedToolLifetime();
            lifetime = activation;
            list.SelectionChanged += Select;
            list.ClearRequested += Clear;
            list.FreezeChanged += Freeze;
            activation.OnClosing(async () =>
            {
                list.SelectionChanged -= Select;
                list.ClearRequested -= Clear;
                list.FreezeChanged -= Freeze;
                await session.StopAsync();
                await export;
                await Dispatcher.DispatchAsync(() =>
                {
                    list.Rows = [];
                    detail.Text = string.Empty;
                    selected = null;
                    frozen = null;
                    history.Clear();
                    Graph = [];
                    NumericFields = [];
                });
            });
            session.Start(activation.Token);
            StatusMessage = "Collecting; rates average five one-second buckets. Signature authentication is not verified by the inspector.";
            var display = DisplayAsync(activation.Token);
            activation.OnClosing(async () => await display);
        }
        catch (Exception exception)
        {
            StatusMessage = "Inspection could not start. Connect a vehicle and reopen this page.";
            Logger.LogWarning("Inspector startup failed ({FailureType}).", exception.GetType().Name);
            if (lifetime is not null)
            {
                await lifetime.DisposeAsync();
                lifetime = null;
            }
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public override async Task DeactivateAsync()
    {
        lifetime?.Cancel();
        await gate.WaitAsync();
        try
        {
            if (lifetime is not null)
            {
                await lifetime.DisposeAsync();
                lifetime = null;
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task DisplayAsync(CancellationToken token)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
            while (await timer.WaitForNextTickAsync(token))
            {
                await Dispatcher.DispatchAsync(() =>
                {
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }
                    Dropped = session.Dropped;
                    if (!session.IsCollecting)
                    {
                        StatusMessage = "Collection stopped at a connection boundary. Reconnect and reopen the inspector.";
                    }
                    if (list.Frozen)
                    {
                        return;
                    }
                    var key = selected;
                    var rows = session.Rows(list.Search);
                    list.Rows = list.SortByCount ? rows.OrderByDescending(row => row.Count).ToArray() : rows;
                    list.Selected = list.Rows.FirstOrDefault(row => row.Key == key);
                    ShowDetails();
                });
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
    }

    private void Select(InspectorKey? key)
    {
        if (selected != key)
        {
            history.Clear();
            Graph = [];
            SelectedField = null;
        }
        selected = key;
        OnPropertyChanged(nameof(GraphCaption));
        ShowDetails();
    }

    private void ShowDetails()
    {
        var snapshot = selected is null ? null : frozen is not null
            ? frozen.Messages.FirstOrDefault(item => item.Row.Key == selected) : session.Details(selected);
        detail.Text = snapshot is null ? string.Empty : JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
        if (snapshot is null)
        {
            NumericFields = [];
            return;
        }
        var fields = InspectorFieldHistory.NumericFields(snapshot);
        if (!NumericFields.SequenceEqual(fields))
        {
            NumericFields = fields;
        }
        if (SelectedField is null || !NumericFields.Contains(SelectedField))
        {
            SelectedField = NumericFields.FirstOrDefault();
        }
        if (!list.Frozen && SelectedField is { } field && history.Observe(snapshot, field))
        {
            var values = history.Samples;
            var start = values[0].ObservedAt;
            Graph = values.Select(sample => new Point((sample.ObservedAt - start).TotalSeconds, sample.Value)).ToArray();
        }
    }

    private void Clear(bool _)
    {
        session.Clear();
        history.Clear();
        Graph = [];
        NumericFields = [];
        list.Rows = [];
        detail.Text = string.Empty;
        selected = null;
        frozen = list.Frozen ? session.Export() : null;
    }

    private void Freeze(bool value)
    {
        frozen = value ? session.Export() : null;
        if (frozen is not null)
        {
            var key = selected;
            list.Rows = frozen.Messages.Select(item => item.Row).Where(row => InspectorAggregator.Matches(row, list.Search)).ToArray();
            list.Selected = list.Rows.FirstOrDefault(item => item.Key == key);
        }
        ShowDetails();
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        lifetime?.Cancel();
        CloseAfterDisposalAsync().SafeFireAndForget();
        base.Dispose();
    }

    private async Task CloseAfterDisposalAsync()
    {
        try
        {
            await DeactivateAsync();
        }
        catch (Exception exception)
        {
            Logger.LogWarning("Inspector cleanup failed ({FailureType}).", exception.GetType().Name);
        }
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task ExportGraphAsync(CancellationToken cancellationToken)
    {
        if (lifetime is null || lifetime.Token.IsCancellationRequested || !export.IsCompleted || history.Samples.Count == 0)
            return Task.CompletedTask;
        var bytes = history.ExportCsv();
        export = ExportGraphCoreAsync(bytes, lifetime.Token, cancellationToken);
        return export;
    }

    private async Task ExportGraphCoreAsync(byte[] bytes, CancellationToken pageToken, CancellationToken commandToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(pageToken, commandToken);
        try
        {
            using var content = new MemoryStream(bytes);
            var saved = await files.SaveNewAsync("mavlink-field.csv", content, linked.Token);
            linked.Token.ThrowIfCancellationRequested();
            StatusMessage = saved is null ? "Graph export cancelled." : "Graph samples exported with source identity and receive times. Review telemetry before sharing.";
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { }
        catch (Exception exception) { Logger.LogWarning(exception, "Inspector graph export failed."); StatusMessage = "Graph export failed. Choose a new file and try again."; }
    }

    [RelayCommand]
    private Task ExportAsync()
    {
        if (lifetime is null || lifetime.Token.IsCancellationRequested || !export.IsCompleted)
        {
            return Task.CompletedTask;
        }
        export = ExportCoreAsync(lifetime.Token);
        return export;
    }

    private async Task ExportCoreAsync(CancellationToken token)
    {
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(session.Export());
            using var content = new MemoryStream(bytes);
            var saved = await files.SaveAsync("mavlink-inspection.json", content, token);
            token.ThrowIfCancellationRequested();
            StatusMessage = saved is null ? "Export cancelled." : "Inspection snapshot exported. Review telemetry before sharing.";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Logger.LogWarning("Inspector export failed ({FailureType}).", exception.GetType().Name);
            StatusMessage = "Export failed. Check file permissions and try again.";
        }
    }
}
