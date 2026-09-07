using System.Text;
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
        selected = key;
        ShowDetails();
    }

    private void ShowDetails()
    {
        var snapshot = selected is null ? null : frozen is not null
            ? frozen.Messages.FirstOrDefault(item => item.Row.Key == selected) : session.Details(selected);
        detail.Text = snapshot is null ? string.Empty : JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
    }

    private void Clear(bool _)
    {
        session.Clear();
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
        _ = CloseAfterDisposalAsync();
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
