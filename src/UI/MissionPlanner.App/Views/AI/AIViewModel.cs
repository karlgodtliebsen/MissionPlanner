using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.AI;
/// <summary>
/// 
/// </summary>
public partial class AIViewModel : ViewModelBase
{
    private readonly IDialogService dialogService;

    private readonly IDomainEventHub domainEventHub;
    private readonly IVehicleRegistry vehicleRegistry;
    private readonly IList<IDisposable> disposables = [];
    private readonly bool isActive;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIViewModel"/> class.
    /// </summary>
    public AIViewModel(
        IDialogService dialogService,
        IDomainEventHub domainEventHub,
        IVehicleRegistry vehicleRegistry,
        ILogger<AIViewModel> logger) : base(logger)
    {
        this.dialogService = dialogService;
        this.domainEventHub = domainEventHub;
        this.vehicleRegistry = vehicleRegistry;
    }
}
