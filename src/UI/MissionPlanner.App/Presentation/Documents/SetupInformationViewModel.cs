using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Setup.Reporting;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Presentation.Documents;

/// <summary>Maintains reports only while their Information tab is visible.</summary>
public sealed partial class SetupInformationViewModel : ViewModelBase
{
    private readonly ISetupReportService reports;
    private readonly SetupReportDocumentFactory documents;
    private readonly IActiveVehicleContext vehicle;
    private readonly IDomainEventHub events;
    private CancellationTokenSource? lifetime;
    private IDisposable? loadSubscription;
    private Task? refreshLoop;
    private int generation;
    private UserDocument? overview;
    private UserDocument? parameters;
    private UserDocument? additionalDocument;

    /// <summary>Gets or sets supplemental reporting from the owning setup workflow.</summary>
    public UserDocument? AdditionalDocument
    {
        get => additionalDocument;
        set
        {
            if (SetProperty(ref additionalDocument, value))
            {
                OnPropertyChanged(nameof(HasAdditionalDocument));
            }
        }
    }

    /// <summary>Gets whether supplemental workflow evidence is available.</summary>
    public bool HasAdditionalDocument => AdditionalDocument is not null;

    /// <summary>Initializes reporting independently of the setup command ViewModel.</summary>
    public SetupInformationViewModel(ISetupReportService reports, SetupReportDocumentFactory documents,
        IActiveVehicleContext vehicle, IUiDispatcher dispatcher, IDomainEventHub events,
        ILogger<SetupInformationViewModel> logger) : base(logger, dispatcher, events)
    {
        this.reports = reports;
        this.documents = documents;
        this.vehicle = vehicle;
        this.events = events;
    }

    /// <summary>Gets or sets the subsystem whose evidence is displayed.</summary>
    public SetupReportTopic Topic { get; set; }

    /// <summary>Gets or sets the latest operation message, separate from confirmed parameter evidence.</summary>
    public string? WorkflowStatus { get; set; }

    /// <summary>Gets or sets the latest setup-operation error.</summary>
    public string? WorkflowError { get; set; }

    /// <summary>Gets the current subsystem overview.</summary>
    public UserDocument? Overview
    {
        get => overview;
        private set => SetProperty(ref overview, value);
    }

    /// <summary>Gets the copyable confirmed parameter evidence.</summary>
    public UserDocument? Parameters
    {
        get => parameters;
        private set => SetProperty(ref parameters, value);
    }

    /// <summary>Gets whether a parameter document is available.</summary>
    public bool HasParameters => Parameters is not null;

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        if (lifetime is not null)
        {
            return Task.CompletedTask;
        }
        lifetime = new CancellationTokenSource();
        var epoch = ++generation;
        vehicle.Changed += VehicleChanged;
        loadSubscription = events.SubscribeDomainEventAsync<VehicleParameterLoadStatusChanged>((change, token) =>
        {
            if (change.Status.VehicleId == vehicle.VehicleId)
            {
                QueueRefresh(epoch);
            }
            return Task.CompletedTask;
        });
        Refresh();
        refreshLoop = RefreshPeriodicallyAsync(epoch, lifetime.Token);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override async Task DeactivateAsync()
    {
        var cancellation = lifetime;
        lifetime = null;
        ++generation;
        vehicle.Changed -= VehicleChanged;
        loadSubscription?.Dispose();
        loadSubscription = null;
        var loop = refreshLoop;
        refreshLoop = null;
        if (cancellation is not null)
        {
            await cancellation.CancelAsync();
            if (loop is not null)
            {
                await loop;
            }
            cancellation.Dispose();
        }
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        ++generation;
        vehicle.Changed -= VehicleChanged;
        loadSubscription?.Dispose();
        loadSubscription = null;
        lifetime?.Cancel();
        lifetime?.Dispose();
        lifetime = null;
        base.Dispose();
    }

    /// <summary>Refreshes cached reporting evidence; this never downloads parameters or starts an operation.</summary>
    [RelayCommand]
    public void Refresh()
    {
        if (lifetime is null)
        {
            return;
        }
        try
        {
            var report = reports.Capture(Topic);
            var nextOverview = documents.CreateOverview(report, WorkflowStatus, WorkflowError);
            var nextParameters = documents.CreateParameters(report);
            // Preserve the renderer document and selection when only unrelated telemetry changes.
            if (Overview?.Markdown != nextOverview.Markdown)
            {
                Overview = nextOverview;
            }
            if (Parameters?.Markdown != nextParameters?.Markdown)
            {
                Parameters = nextParameters;
                OnPropertyChanged(nameof(HasParameters));
            }
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Could not capture setup report {Topic}.", Topic);
            Overview = new UserDocumentBuilder().Heading("Report unavailable")
                .Paragraph("The current report could not be read. Refresh to try again.").Build();
            Parameters = null;
            OnPropertyChanged(nameof(HasParameters));
        }
    }

    private void VehicleChanged(ActiveVehicleChangedEventArgs change) => QueueRefresh(generation);

    private void QueueRefresh(int epoch)
    {
        Dispatcher.Dispatch(() =>
        {
            if (epoch == generation && lifetime is not null)
            {
                Refresh();
            }
        });
    }

    private async Task RefreshPeriodicallyAsync(int epoch, CancellationToken token)
    {
        try
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), token).ConfigureAwait(false);
                QueueRefresh(epoch);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
    }
}
