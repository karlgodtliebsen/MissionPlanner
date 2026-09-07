using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Core.Setup.Advanced.Warnings;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.Advanced.Warnings;

/// <summary>Owns rule selection and list operations.</summary>
public sealed partial class WarningRuleListViewModel(ILogger<WarningRuleListViewModel> logger,
    IUiDispatcher dispatcher, IDomainEventHub events) : ViewModelBase(logger, dispatcher, events)
{
    /// <summary>Gets the saved rules.</summary>
    public ObservableCollection<WarningRule> Rules { get; } = [];

    /// <summary>Gets or sets the selected rule.</summary>
    [ObservableProperty]
    public partial WarningRule? Selected { get; set; }

    /// <summary>Requests editing a selected rule or creating a new one.</summary>
    public event Action<WarningRule?>? EditRequested;
    /// <summary>Requests deletion of the selected rule.</summary>
    public event Action<Guid>? DeleteRequested;
    /// <summary>Requests saving the enabled state.</summary>
    public event Action<WarningRule>? ChangeRequested;

    partial void OnSelectedChanged(WarningRule? value) => EditRequested?.Invoke(value);

    [RelayCommand]
    private void New() => EditRequested?.Invoke(null);

    [RelayCommand]
    private void Duplicate()
    {
        if (Selected is not null)
        {
            EditRequested?.Invoke(Selected with { Id = Guid.NewGuid(), Name = "Copy of " + Selected.Name });
        }
    }

    [RelayCommand]
    private void Delete()
    {
        if (Selected is not null)
        {
            DeleteRequested?.Invoke(Selected.Id);
        }
    }

    [RelayCommand]
    private void Toggle()
    {
        if (Selected is not null)
        {
            ChangeRequested?.Invoke(Selected with { Enabled = !Selected.Enabled });
        }
    }
}
