using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.Missions.DockView;

public partial class MissionItemListDockViewModel : ObservableObject, IDisposable
{
    private readonly IDomainEventHub domainEventHub;
    [ObservableProperty] public partial bool IsExpanded { get; set; }
    [ObservableProperty] public partial double CalculatedWidth { get; set; }
    [ObservableProperty] public partial string GuidingText { get; set; } = "<<";
    [ObservableProperty] public partial double ShrinkWidth { get; set; } = 40;
    [ObservableProperty] public partial double ExpandWidth { get; set; } = 500;

    /// <inheritdoc />
    public MissionItemListDockViewModel(IDomainEventHub domainEventHub)
    {
        this.domainEventHub = domainEventHub;
        CalculatedWidth = ShrinkWidth;
    }


    [RelayCommand]
    private void Expand()
    {
        IsExpanded = !IsExpanded;
        CalculatedWidth = IsExpanded ? ExpandWidth : ShrinkWidth;
        GuidingText = IsExpanded ? ">>" : "<<";
    }

    [RelayCommand]
    private async Task EditAsync(CancellationToken cancellationToken)
    {
        await domainEventHub.PublishDomainEventAsync(new EditorDisplayEvent("EditorOpen"), cancellationToken);
    }

    [RelayCommand]
    private async Task CloseAsync(CancellationToken cancellationToken)
    {
        await domainEventHub.PublishDomainEventAsync(new EditorDisplayEvent("EditorClose"), cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
