using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Core.Setup.Advanced;
using MissionPlanner.Core.Setup.Advanced.Warnings;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.App.Views.InitSetup.Advanced.Warnings;

/// <summary>Coordinates persistence and bounded visual evaluation for the active warning page.</summary>
public sealed partial class WarningManagerViewModel : ViewModelBase
{
    private readonly WarningRuleRepository repository;
    private readonly WarningSources sources;
    private readonly WarningEngine engine;
    private readonly IActiveVehicleContext vehicle;
    private readonly TimeProvider clock;
    private readonly IFileOpenService fileOpen;
    private readonly IFileSaveService fileSave;
    private readonly SemaphoreSlim lifecycleGate = new(1, 1);
    private AdvancedToolLifetime? lifetime;
    private Task mutation = Task.CompletedTask;
    private VehicleId? evaluatedVehicle;

    /// <summary>Initializes the coordinator with independently owned child panels.</summary>
    public WarningManagerViewModel(WarningRuleListViewModel rules, WarningRuleEditorViewModel editor,
        WarningStatusViewModel status, WarningRuleRepository repository, WarningSources sources,
        IActiveVehicleContext vehicle, TimeProvider clock, WarningEngine engine, ILogger<WarningManagerViewModel> logger,
        IUiDispatcher dispatcher, IDomainEventHub events, IFileOpenService fileOpen, IFileSaveService fileSave) : base(logger, dispatcher, events)
    {
        Rules = rules;
        Editor = editor;
        Status = status;
        this.repository = repository;
        this.sources = sources;
        this.vehicle = vehicle;
        this.clock = clock;
        this.engine = engine;
        this.fileOpen = fileOpen;
        this.fileSave = fileSave;
    }

    /// <summary>Gets the rule-list panel.</summary>
    public WarningRuleListViewModel Rules { get; }
    /// <summary>Gets the detached rule editor.</summary>
    public WarningRuleEditorViewModel Editor { get; }
    /// <summary>Gets the current visual warnings.</summary>
    public WarningStatusViewModel Status { get; }

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
            Logger.LogWarning("Warning page cleanup failed ({FailureType}).", exception.GetType().Name);
        }
    }

    /// <inheritdoc />
    public override async Task ActivateAsync()
    {
        await lifecycleGate.WaitAsync();
        try
        {
            if (lifetime is not null)
            {
                return;
            }
            var activation = new AdvancedToolLifetime();
            lifetime = activation;
            IsBusy = true;
            var loaded = await repository.LoadAsync(activation.Token);
            activation.Token.ThrowIfCancellationRequested();
            await Dispatcher.DispatchAsync(() =>
            {
                activation.Token.ThrowIfCancellationRequested();
                Rules.Rules.Clear();
                foreach (var rule in loaded.Rules)
                {
                    Rules.Rules.Add(rule);
                }
                engine.SetRules([]);
                engine.SetRules(loaded.Rules);
                evaluatedVehicle = null;
                Editor.Edit(null);
                StatusMessage = loaded.Diagnostic;
                IsBusy = false;
                Rules.EditRequested += Editor.Edit;
                Rules.DeleteRequested += Delete;
                Rules.ChangeRequested += Save;
                Editor.SaveRequested += Save;
                Status.AcknowledgeRequested += Acknowledge;
            });
            activation.OnClosing(async () =>
            {
                await Dispatcher.DispatchAsync(() =>
                {
                    Rules.EditRequested -= Editor.Edit;
                    Rules.DeleteRequested -= Delete;
                    Rules.ChangeRequested -= Save;
                    Editor.SaveRequested -= Save;
                    Status.AcknowledgeRequested -= Acknowledge;
                    Status.Warnings = [];
                });
                await mutation;
            });
            var polling = PollAsync(activation.Token);
            activation.OnClosing(async () => await polling);
        }
        catch (OperationCanceledException)
        {
            IsBusy = false;
        }
        finally
        {
            lifecycleGate.Release();
        }
    }

    /// <inheritdoc />
    public override async Task DeactivateAsync()
    {
        // Cancel before waiting for activation to finish its storage read.
        var current = lifetime;
        current?.Cancel();
        await lifecycleGate.WaitAsync();
        try
        {
            if (lifetime is not null)
            {
                await lifetime.DisposeAsync();
            }
            lifetime = null;
            IsBusy = false;
        }
        finally
        {
            lifecycleGate.Release();
        }
    }

    private async Task PollAsync(CancellationToken token)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250), clock);
            while (await timer.WaitForNextTickAsync(token))
            {
                await Dispatcher.DispatchAsync(() =>
                {
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }
                    var state = vehicle.IsOnline ? vehicle.State : null;
                    if (evaluatedVehicle != state?.VehicleId)
                    {
                        evaluatedVehicle = state?.VehicleId;
                        engine.SetRules([]);
                        engine.SetRules(Rules.Rules.ToArray());
                    }
                    var selected = Status.Selected?.RuleId;
                    Status.Warnings = engine.Evaluate(key => sources.Read(key, state));
                    Status.Selected = Status.Warnings.FirstOrDefault(item => item.RuleId == selected);
                });
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Logger.LogWarning("Visual warning evaluation stopped ({FailureType}).", exception.GetType().Name);
            await Dispatcher.DispatchAsync(() => StatusMessage = "Warning evaluation stopped. Close and reopen this page to retry.");
        }
    }

    private void Acknowledge(Guid id) => engine.Acknowledge(id);

    [RelayCommand]
    private Task ImportAsync() => StartFileOperationAsync(true);

    [RelayCommand]
    private Task ExportAsync() => StartFileOperationAsync(false);

    private Task StartFileOperationAsync(bool importing)
    {
        if (lifetime is null || lifetime.Token.IsCancellationRequested || IsBusy)
        {
            return Task.CompletedTask;
        }
        mutation = FileOperationAsync(importing, lifetime.Token);
        return mutation;
    }

    private async Task FileOperationAsync(bool importing, CancellationToken token)
    {
        IsBusy = true;
        try
        {
            if (importing)
            {
                using var file = await fileOpen.OpenAsync("Import warning rules (merges by rule ID)", ["*.json"], token);
                token.ThrowIfCancellationRequested();
                if (file is null)
                {
                    return;
                }
                using var reader = new StreamReader(file.Content, Encoding.UTF8, true, 4096, leaveOpen: true);
                var buffer = new char[WarningRuleRepository.MaximumDocumentLength + 1];
                var count = 0;
                while (count < buffer.Length)
                {
                    var read = await reader.ReadAsync(buffer.AsMemory(count), token);
                    if (read == 0)
                    {
                        break;
                    }
                    count += read;
                }
                if (count > WarningRuleRepository.MaximumDocumentLength)
                {
                    throw new InvalidDataException("The warning file exceeds the size limit.");
                }
                using var document = JsonDocument.Parse(new string(buffer, 0, count));
                if (document.RootElement.GetProperty("Version").GetInt32() != 1)
                {
                    throw new InvalidDataException("Unsupported warning file version.");
                }
                var imported = document.RootElement.GetProperty("Rules").Deserialize<List<WarningRule>>()
                    ?? throw new InvalidDataException("No rules found.");
                if (imported.Any(rule => rule is null || sources.Validate(rule).Count != 0)
                    || imported.Select(rule => rule.Id).Distinct().Count() != imported.Count)
                {
                    throw new InvalidDataException("Invalid warning rules.");
                }
                var ids = imported.Select(rule => rule.Id).ToHashSet();
                var next = Rules.Rules.Where(rule => !ids.Contains(rule.Id)).Concat(imported).ToArray();
                await PersistAsync(next, token);
            }
            else
            {
                var json = JsonSerializer.Serialize(new { Version = 1, Rules = Rules.Rules });
                using var content = new MemoryStream(Encoding.UTF8.GetBytes(json));
                var result = await fileSave.SaveAsync("missionplanner-warnings.json", content, token);
                token.ThrowIfCancellationRequested();
                StatusMessage = result is null ? "Export cancelled." : "Warning rules exported.";
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Logger.LogWarning("Warning file operation failed ({FailureType}).", exception.GetType().Name);
            StatusMessage = "The warning file operation failed. Check file format, size and storage permission; saved rules were not replaced by invalid input.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Save(WarningRule rule) => Change(Rules.Rules.Where(item => item.Id != rule.Id).Append(rule).ToArray());
    private void Delete(Guid id) => Change(Rules.Rules.Where(item => item.Id != id).ToArray());

    private void Change(IReadOnlyList<WarningRule> next)
    {
        if (lifetime is not null && !lifetime.Token.IsCancellationRequested && !IsBusy)
        {
            mutation = PersistAsync(next, lifetime.Token);
        }
    }

    private async Task PersistAsync(IReadOnlyList<WarningRule> next, CancellationToken token)
    {
        IsBusy = true;
        try
        {
            await repository.SaveAsync(next, token);
            await Dispatcher.DispatchAsync(() =>
            {
                token.ThrowIfCancellationRequested();
                Rules.Rules.Clear();
                foreach (var rule in next)
                {
                    Rules.Rules.Add(rule);
                }
                engine.SetRules(next);
                StatusMessage = "Warning rules saved.";
            });
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Logger.LogWarning("Warning rule persistence failed ({FailureType}).", exception.GetType().Name);
            await Dispatcher.DispatchAsync(() => StatusMessage = "Rules were not saved. Check storage permissions and the rule limit, then retry.");
        }
        finally
        {
            await Dispatcher.DispatchAsync(() => IsBusy = false);
        }
    }
}
